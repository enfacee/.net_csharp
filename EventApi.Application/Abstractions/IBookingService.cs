using EventApi.Domain.Entities;

namespace EventApi.Application.Abstractions;

public interface IBookingService
{
    Task<Booking> CreateBookingAsync(int eventId, int userId, CancellationToken cancellationToken = default);
    Task<Booking?> GetBookingByIdAsync(int bookingId);
    Task<IReadOnlyCollection<Booking>> GetPendingBookingsAsync(CancellationToken cancellationToken = default);
    Task<Booking?> UpdateBookingStatusAsync(int bookingId, BookingStatus status, CancellationToken cancellationToken = default);
    Task<bool> CancelBookingAsync(
        int bookingId,
        int currentUserId,
        UserRole currentUserRole,
        CancellationToken cancellationToken = default);
}

