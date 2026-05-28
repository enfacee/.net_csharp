using System.ComponentModel.DataAnnotations;

public class EventService(IEventStore eventStore) : IEventService
{
    private readonly IEventStore _eventStore = eventStore;

    public EventService()
        : this(new InMemoryEventStore())
    {
    }

    public Task<Event> CreateEventAsync(
        string title,
        string? description,
        DateTime startAt,
        DateTime endAt,
        int totalSeats)
    {
        var @event = Event.Create(title, description, startAt, endAt, totalSeats);
        Add(@event);

        return Task.FromResult(@event);
    }

    public void Add(Event @event)
    {
        ValidateEvent(@event);

        if (!_eventStore.TryAdd(@event))
            throw new ValidationException("Event with the same Id already exists.");
    }

    public PaginatedResult<Event> GetAll(
        string? title = null,
        DateTime? from = null,
        DateTime? to = null,
        int page = 1,
        int pageSize = 10)
    {
        if (page < 1)
            throw new ArgumentOutOfRangeException(nameof(page), "Page must be greater than 0.");

        if (pageSize < 1)
            throw new ArgumentOutOfRangeException(nameof(pageSize), "PageSize must be greater than 0.");

        var query = _eventStore.GetAll()
            .OrderBy(e => e.Id)
            .AsEnumerable();

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

    public Event? GetById(int id)
    {
        return _eventStore.GetById(id);
    }

    public bool Remove(int id)
    {
        return _eventStore.TryRemove(id);
    }

    public void Update(Event @event)
    {
        if (_eventStore.GetById(@event.Id) is null)
            return;

        ValidateEvent(@event);
        _eventStore.TryUpdate(@event);
    }

    private static void ValidateEvent(Event @event)
    {
        if (string.IsNullOrWhiteSpace(@event.Title))
            throw new ValidationException("Title is required.");

        if (@event.EndAt <= @event.StartAt)
            throw new ValidationException("EndAt must be greater than StartAt.");

        if (@event.TotalSeats <= 0)
            throw new ValidationException("TotalSeats must be greater than 0.");

        if (@event.AvailableSeats < 0 || @event.AvailableSeats > @event.TotalSeats)
            throw new ValidationException("AvailableSeats must be between 0 and TotalSeats.");
    }
}
