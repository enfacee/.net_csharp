using EventApi.Shared.Contracts;

namespace EventApi.Events.Application.Abstractions;

public interface IEventSeatReservationPublisher
{
    Task PublishSeatReservedAsync(EventSeatReserved message, CancellationToken cancellationToken = default);
    Task PublishSeatUnavailableAsync(EventSeatUnavailable message, CancellationToken cancellationToken = default);
}
