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

    private static void ValidateBooking(Booking booking)
    {
        if (booking.EventId <= 0)
            throw new ValidationException("EventId is required.");
    }
}
