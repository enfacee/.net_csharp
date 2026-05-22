using System.ComponentModel.DataAnnotations;

public class BookingService : IBookingService
{
    private readonly List<Booking> _bookings = new();
    private readonly Lock _lock = new();

    public Task<Booking> CreateBookingAsync(int eventId)
    {
        using (_lock.EnterScope())
        {
            var booking = new Booking(eventId);
            ValidateBooking(booking);
            _bookings.Add(booking);
            return Task.FromResult(booking);
        }
    }

    public Task<Booking?> GetBookingByIdAsync(int bookingId)
    {
        using (_lock.EnterScope())
        {
            return Task.FromResult(_bookings.FirstOrDefault(x => x.Id == bookingId));
        }
    }

    public Task<IReadOnlyCollection<Booking>> GetPendingBookingsAsync(CancellationToken cancellationToken = default)
    {
        using (_lock.EnterScope())
        {
            IReadOnlyCollection<Booking> pendingBookings = _bookings
                .Where(x => x.Status == BookingStatus.Pending)
                .ToArray();

            return Task.FromResult(pendingBookings);
        }
    }

    public Task<Booking?> ConfirmBookingAsync(int bookingId, CancellationToken cancellationToken = default)
    {
        using (_lock.EnterScope())
        {
            if (_bookings.FirstOrDefault(x => x.Id == bookingId) is not { } booking)
                return Task.FromResult<Booking?>(null);

            if (booking.Status != BookingStatus.Pending)
                return Task.FromResult<Booking?>(booking);

            booking.Status = BookingStatus.Confirmed;
            booking.ProcessedAt = DateTime.UtcNow;

            return Task.FromResult<Booking?>(booking);
        }
    }

    private static void ValidateBooking(Booking booking)
    {
        if (booking.EventId <= 0)
            throw new ValidationException("EventId is required.");
    }
}
