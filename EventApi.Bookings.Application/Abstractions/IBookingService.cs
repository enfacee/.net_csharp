using EventApi.Bookings.Domain.Entities;

namespace EventApi.Bookings.Application.Abstractions;

public interface IBookingService
{
    Task<Booking> CreateBookingAsync(int eventId, int userId, CancellationToken cancellationToken = default);
    Task<Booking?> GetBookingByIdAsync(int bookingId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<Booking>> GetPendingBookingsAsync(CancellationToken cancellationToken = default);
    Task<Booking?> UpdateBookingStatusAsync(int bookingId, BookingStatus status, CancellationToken cancellationToken = default);
    Task<Booking?> ConfirmBookingAsync(int bookingId, CancellationToken cancellationToken = default);
    Task<Booking?> RejectBookingAsync(int bookingId, CancellationToken cancellationToken = default);
    Task<bool> CancelBookingAsync(
        int bookingId,
        int currentUserId,
        UserRole currentUserRole,
        CancellationToken cancellationToken = default);
}
