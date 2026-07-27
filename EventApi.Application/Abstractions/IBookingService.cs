using EventApi.Domain.Entities;

namespace EventApi.Application.Abstractions;

public interface IBookingService
{
    Task<Booking> CreateBookingAsync(int eventId, CancellationToken cancellationToken = default);
    Task<Booking?> GetBookingByIdAsync(int bookingId);
    Task<IReadOnlyCollection<Booking>> GetPendingBookingsAsync(CancellationToken cancellationToken = default);
    Task<Booking?> UpdateBookingStatusAsync(int bookingId, BookingStatus status, CancellationToken cancellationToken = default);
}

