using System.ComponentModel.DataAnnotations;

public class EventService : IEventService
{
    private readonly List<Event> _events = new ();
    private readonly Lock _lock = new();

    public void Add(Event @event)
    {
        using (_lock.EnterScope())
        {
            ValidateEvent(@event);
            _events.Add(@event);
        }
    }

    public PaginatedResult<Event> GetAll(
        string? title = null,
        DateTime? from = null,
        DateTime? to = null,
        int page = 1,
        int pageSize = 10)
    {
        using (_lock.EnterScope())
        {
            if (page < 1)
                throw new ArgumentOutOfRangeException(nameof(page), "Page must be greater than 0.");

            if (pageSize < 1)
                throw new ArgumentOutOfRangeException(nameof(pageSize), "PageSize must be greater than 0.");

            var query = _events.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(title))
            {
                query = query.Where(e =>
                    e.Title.Contains(title, StringComparison.OrdinalIgnoreCase));
            }

            if (from.HasValue)
            {
                query = query.Where(e => e.StartAt >= from.Value);
            }

            if (to.HasValue)
            {
                query = query.Where(e => e.EndAt <= to.Value);
            }

            var totalCount = query.Count();
            var items = query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToArray();

            return new PaginatedResult<Event>
            {
                TotalCount = totalCount,
                Items = items,
                Page = page,
                PageSize = items.Length
            };
        }
    }

    public Event? GetById(int id)
    {
        using (_lock.EnterScope())
        {
            return _events.FirstOrDefault(e => e.Id == id);
        }
    }

    public bool Remove(int id)
    {
        using (_lock.EnterScope())
        {
            if (_events.FirstOrDefault(e => e.Id == id) is not { } item)
                return false;

            return _events.Remove(item);
        }
    }

    public void Update(Event @event)
    {
        using (_lock.EnterScope())
        {
            if (_events.FirstOrDefault(e => e.Id == @event.Id) is not { } existingEvent)
                return;

            ValidateEvent(@event);
            existingEvent.CopyFrom(@event);
        }
    }

    private static void ValidateEvent(Event @event)
    {
        if (string.IsNullOrWhiteSpace(@event.Title))
            throw new ValidationException("Title is required.");

        if (@event.EndAt <= @event.StartAt)
            throw new ValidationException("EndAt must be greater than StartAt.");
    }
}
