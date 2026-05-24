using System.ComponentModel.DataAnnotations;

public class BookingService : IBookingService
{
    private readonly List<Booking> _bookings = new();
    private readonly Lock _lock = new();
    private readonly IEventService _eventService;

    public BookingService(IEventService eventService)
    {
        _eventService = eventService;
    }

    public Task<Booking> CreateBookingAsync(int eventId)
    {
        ValidateEventId(eventId);

        if (_eventService.GetById(eventId) is null)
            throw new KeyNotFoundException("Event not found.");

        using (_lock.EnterScope())
        {
            var booking = new Booking(eventId);
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

    public Task<Booking?> UpdateBookingStatusAsync(
        int bookingId,
        BookingStatus status,
        CancellationToken cancellationToken = default)
    {
        if (status == BookingStatus.Pending)
            throw new ValidationException("Status must be Confirmed or Rejected.");

        using (_lock.EnterScope())
        {
            if (_bookings.FirstOrDefault(x => x.Id == bookingId) is not { } booking)
                return Task.FromResult<Booking?>(null);

            if (booking.Status != BookingStatus.Pending)
                return Task.FromResult<Booking?>(booking);

            booking.Status = status;
            booking.ProcessedAt = DateTime.UtcNow;

            return Task.FromResult<Booking?>(booking);
        }
    }

    private static void ValidateEventId(int eventId)
    {
        if (eventId <= 0)
            throw new ValidationException("EventId is required.");
    }
}
