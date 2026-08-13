using EventApi.Shared.Contracts;

namespace EventApi.Bookings.Application.Abstractions;

public interface IBookingEventPublisher
{
    Task PublishBookingCreatedAsync(BookingCreated message, CancellationToken cancellationToken = default);
    Task PublishBookingConfirmedAsync(BookingConfirmed message, CancellationToken cancellationToken = default);
    Task PublishBookingRejectedAsync(BookingRejected message, CancellationToken cancellationToken = default);
}
