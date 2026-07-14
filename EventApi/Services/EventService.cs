using System.ComponentModel.DataAnnotations;

namespace EventApi;

public class EventService(IEventRepository eventRepository) : IEventService
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

        if (await eventRepository.ExistsAsync(@event.Id, cancellationToken))
            throw new ValidationException("Event with the same Id already exists.");

        await eventRepository.AddAsync(@event, cancellationToken);
        await eventRepository.SaveChangesAsync(cancellationToken);
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

        return await eventRepository.GetAllAsync(title, from, to, page, pageSize, cancellationToken);
    }

    public async Task<Event?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await eventRepository.GetByIdAsync(id, cancellationToken);
    }

    public async Task<bool> RemoveAsync(int id, CancellationToken cancellationToken = default)
    {
        if (!await eventRepository.RemoveAsync(id, cancellationToken))
            return false;

        await eventRepository.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task UpdateAsync(Event @event, CancellationToken cancellationToken = default)
    {
        var existingEvent = await eventRepository.GetByIdAsync(@event.Id, cancellationToken);
        if (existingEvent is null)
            return;

        ValidateEvent(@event);

        if (!ReferenceEquals(existingEvent, @event))
            existingEvent.CopyFrom(@event);

        await eventRepository.SaveChangesAsync(cancellationToken);
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

