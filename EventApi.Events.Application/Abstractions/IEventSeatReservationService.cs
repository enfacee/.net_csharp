using EventApi.Shared.Contracts;

namespace EventApi.Events.Application.Abstractions;

public interface IEventSeatReservationService
{
    Task HandleBookingCreatedAsync(BookingCreated message, CancellationToken cancellationToken = default);
}
