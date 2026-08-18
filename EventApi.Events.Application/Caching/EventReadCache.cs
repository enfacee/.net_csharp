using System.Text.Json;
using EventApi.Events.Application.Abstractions;
using EventApi.Events.Application.Options;
using EventApi.Events.Domain.Entities;
using Microsoft.Extensions.Options;

namespace EventApi.Events.Application.Caching;

public sealed class EventReadCache(
    IEventCache cache,
    IOptions<EventCacheOptions> options) : IEventReadCache
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<Event?> GetEventAsync(int id, CancellationToken cancellationToken = default)
    {
        var cached = await cache.GetStringAsync(EventCacheKeys.EventById(id), cancellationToken);
        return DeserializeEvent(cached);
    }

    public async Task SetEventAsync(Event @event, CancellationToken cancellationToken = default)
    {
        await cache.SetStringAsync(
            EventCacheKeys.EventById(@event.Id),
            JsonSerializer.Serialize(EventCacheItem.FromEvent(@event), JsonOptions),
            options.Value.EventByIdTtl,
            cancellationToken);
    }

    public async Task RemoveEventAsync(int id, CancellationToken cancellationToken = default)
    {
        await cache.RemoveAsync(EventCacheKeys.EventById(id), cancellationToken);
    }

    public async Task<IReadOnlyCollection<Event>?> GetTopEventsAsync(CancellationToken cancellationToken = default)
    {
        var cached = await cache.GetStringAsync(EventCacheKeys.TopEvents, cancellationToken);
        return DeserializeEvents(cached);
    }

    public async Task SetTopEventsAsync(
        IReadOnlyCollection<Event> events,
        CancellationToken cancellationToken = default)
    {
        await cache.SetStringAsync(
            EventCacheKeys.TopEvents,
            JsonSerializer.Serialize(events.Select(EventCacheItem.FromEvent), JsonOptions),
            options.Value.TopEventsTtl,
            cancellationToken);
    }

    private static Event? DeserializeEvent(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        try
        {
            return JsonSerializer.Deserialize<EventCacheItem>(value, JsonOptions)?.ToEvent();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static IReadOnlyCollection<Event>? DeserializeEvents(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        try
        {
            return JsonSerializer.Deserialize<EventCacheItem[]>(value, JsonOptions)
                ?.Select(item => item.ToEvent())
                .ToArray();
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
