using EventApi.Events.Domain.Entities;

namespace EventApi.Events.Application.Caching;

public sealed record EventCacheItem(
    int Id,
    string Title,
    string? Description,
    DateTime StartAt,
    DateTime EndAt,
    int TotalSeats,
    int AvailableSeats)
{
    public static EventCacheItem FromEvent(Event @event)
    {
        return new EventCacheItem(
            @event.Id,
            @event.Title,
            @event.Description,
            @event.StartAt,
            @event.EndAt,
            @event.TotalSeats,
            @event.AvailableSeats);
    }

    public Event ToEvent()
    {
        return Event.Rehydrate(
            Id,
            Title,
            Description,
            StartAt,
            EndAt,
            TotalSeats,
            AvailableSeats);
    }
}
