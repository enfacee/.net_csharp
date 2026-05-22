public interface IBookingService
{
    Task<Booking> CreateBookingAsync(int eventId);
    Task<Booking?> GetBookingByIdAsync(int bookingId);
    Task<IReadOnlyCollection<Booking>> GetPendingBookingsAsync(CancellationToken cancellationToken = default);
    Task<Booking?> ConfirmBookingAsync(int bookingId, CancellationToken cancellationToken = default);
}
