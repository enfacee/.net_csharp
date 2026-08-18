using System.Collections.Concurrent;
using EventApi.Events.Application.Abstractions;
using EventApi.Events.Application.Caching;
using EventApi.Events.Application.Common;
using EventApi.Events.Application.DTO;
using EventApi.Events.Domain.Entities;
using EventApi.Events.Domain.Exceptions;

namespace EventApi.Events.Application.Services;

public class EventService(
    IEventRepository eventRepository,
    IEventReadCache readCache) : IEventService
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> CacheMissLocks = new();

    public async Task<Event> CreateEventAsync(
        string title,
        string? description,
        DateTime startAt,
        DateTime endAt,
        int totalSeats,
        CancellationToken cancellationToken = default)
    {
        var @event = Event.Create(title, description, startAt, endAt, totalSeats);

        await eventRepository.AddAsync(@event, cancellationToken);

        // Top-events cache intentionally lives by TTL and is not invalidated on event creation.
        return @event;
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
        var cachedEvent = await readCache.GetEventAsync(id, cancellationToken);
        if (cachedEvent is not null)
            return cachedEvent;

        return await ExecuteSingleFlightAsync(
            EventCacheKeys.EventById(id),
            async () =>
            {
                cachedEvent = await readCache.GetEventAsync(id, cancellationToken);
                if (cachedEvent is not null)
                    return cachedEvent;

                var @event = await eventRepository.GetByIdAsync(id, cancellationToken);
                if (@event is null)
                    return null;

                await readCache.SetEventAsync(@event, cancellationToken);
                return @event;
            },
            cancellationToken);
    }

    public async Task<IReadOnlyCollection<Event>> GetTopAsync(CancellationToken cancellationToken = default)
    {
        var cachedEvents = await readCache.GetTopEventsAsync(cancellationToken);
        if (cachedEvents is not null)
            return cachedEvents;

        return await ExecuteSingleFlightAsync(
            EventCacheKeys.TopEvents,
            async () =>
            {
                cachedEvents = await readCache.GetTopEventsAsync(cancellationToken);
                if (cachedEvents is not null)
                    return cachedEvents;

                var events = await eventRepository.GetTopBySoldPercentageAsync(10, cancellationToken);
                await readCache.SetTopEventsAsync(events, cancellationToken);
                return events;
            },
            cancellationToken);
    }

    public async Task<bool> RemoveAsync(int id, CancellationToken cancellationToken = default)
    {
        if (!await eventRepository.RemoveAsync(id, cancellationToken))
            return false;

        await readCache.RemoveEventAsync(id, cancellationToken);
        return true;
    }

    public async Task<bool> UpdateEventAsync(
        int id,
        EventRequest request,
        CancellationToken cancellationToken = default)
    {
        var existingEvent = await eventRepository.GetByIdAsync(id, cancellationToken);
        if (existingEvent is null)
            return false;

        existingEvent.UpdateDetails(
            request.Title!,
            request.Description,
            request.StartAt,
            request.EndAt,
            request.TotalSeats!.Value);

        await eventRepository.SaveChangesAsync(cancellationToken);
        await readCache.RemoveEventAsync(id, cancellationToken);
        return true;
    }

    public async Task<bool> ReserveSeatsAsync(int id, int seats, CancellationToken cancellationToken = default)
    {
        var @event = await eventRepository.GetByIdAsync(id, cancellationToken);
        if (@event is null)
            return false;

        if (!@event.TryReserveSeats(seats))
            throw new NoAvailableSeatsException("No available seats for this event");

        await eventRepository.SaveChangesAsync(cancellationToken);
        await readCache.RemoveEventAsync(id, cancellationToken);
        return true;
    }

    private static async Task<T> ExecuteSingleFlightAsync<T>(
        string key,
        Func<Task<T>> factory,
        CancellationToken cancellationToken)
    {
        var semaphore = CacheMissLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(cancellationToken);

        try
        {
            return await factory();
        }
        finally
        {
            semaphore.Release();
        }
    }
}
