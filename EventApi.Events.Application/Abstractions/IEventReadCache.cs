using EventApi.Events.Domain.Entities;

namespace EventApi.Events.Application.Abstractions;

public interface IEventReadCache
{
    Task<Event?> GetEventAsync(int id, CancellationToken cancellationToken = default);
    Task SetEventAsync(Event @event, CancellationToken cancellationToken = default);
    Task RemoveEventAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<Event>?> GetTopEventsAsync(CancellationToken cancellationToken = default);
    Task SetTopEventsAsync(IReadOnlyCollection<Event> events, CancellationToken cancellationToken = default);
}
