using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

public class EventService(AppDbContext context) : IEventService
{
    public async Task<Event> CreateEventAsync(
        string title,
        string? description,
        DateTime startAt,
        DateTime endAt,
        int totalSeats,
        CancellationToken cancellationToken = default)
    {
        var @event = Event.Create(title, description, startAt, endAt, totalSeats);
        await AddAsync(@event, cancellationToken);

        return @event;
    }

    public async Task AddAsync(Event @event, CancellationToken cancellationToken = default)
    {
        ValidateEvent(@event);

        if (await context.Events.AnyAsync(e => e.Id == @event.Id, cancellationToken))
            throw new ValidationException("Event with the same Id already exists.");

        context.Events.Add(@event);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<PaginatedResult<Event>> GetAllAsync(
        string? title = null,
        DateTime? from = null,
        DateTime? to = null,
        int page = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        if (page < 1)
            throw new ArgumentOutOfRangeException(nameof(page), "Page must be greater than 0.");

        if (pageSize < 1)
            throw new ArgumentOutOfRangeException(nameof(pageSize), "PageSize must be greater than 0.");

        var query = context.Events
            .AsNoTracking()
            .OrderBy(e => e.Id)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(title))
        {
            var normalizedTitle = title.Trim().ToLower();
            query = query.Where(e => e.Title.ToLower().Contains(normalizedTitle));
        }

        if (from.HasValue)
        {
            query = query.Where(e => e.StartAt >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(e => e.EndAt <= to.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToArrayAsync(cancellationToken);

        return new PaginatedResult<Event>
        {
            TotalCount = totalCount,
            Items = items,
            Page = page,
            PageSize = items.Length
        };
    }

    public async Task<Event?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await context.Events.FindAsync([id], cancellationToken);
    }

    public async Task<bool> RemoveAsync(int id, CancellationToken cancellationToken = default)
    {
        var @event = await context.Events.FindAsync([id], cancellationToken);
        if (@event is null)
            return false;

        context.Events.Remove(@event);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task UpdateAsync(Event @event, CancellationToken cancellationToken = default)
    {
        var existingEvent = await context.Events.FindAsync([@event.Id], cancellationToken);
        if (existingEvent is null)
            return;

        ValidateEvent(@event);

        if (!ReferenceEquals(existingEvent, @event))
            existingEvent.CopyFrom(@event);

        await context.SaveChangesAsync(cancellationToken);
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
