using System.ComponentModel.DataAnnotations;

public class BookingService(IEventService eventService, IBookingStore bookingStore) : IBookingService
{
    private readonly IBookingStore _bookingStore = bookingStore;
    private readonly Lock _bookingLock = new();
    private readonly IEventService _eventService = eventService;

    public BookingService(IEventService eventService)
        : this(eventService, new InMemoryBookingStore())
    {
    }

    public Task<Booking> CreateBookingAsync(int eventId)
    {
        ValidateEventId(eventId);

        using (_bookingLock.EnterScope())
        {
            var @event = _eventService.GetById(eventId)
                ?? throw new KeyNotFoundException("Event not found.");

            if (!@event.TryReserveSeats())
                throw new NoAvailableSeatsException("No available seats for this event");

            _eventService.Update(@event);

            var booking = new Booking(eventId);
            if (!_bookingStore.TryAdd(booking))
                throw new ValidationException("Booking with the same Id already exists.");

            return Task.FromResult(booking);
        }
    }

    public Task<Booking?> GetBookingByIdAsync(int bookingId)
    {
        return Task.FromResult(_bookingStore.GetById(bookingId));
    }

    public Task<IReadOnlyCollection<Booking>> GetPendingBookingsAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyCollection<Booking> pendingBookings = _bookingStore.GetAll()
            .OrderBy(x => x.Id)
            .Where(x => x.Status == BookingStatus.Pending)
            .ToArray();

        return Task.FromResult(pendingBookings);
    }

    public Task<Booking?> UpdateBookingStatusAsync(
        int bookingId,
        BookingStatus status,
        CancellationToken cancellationToken = default)
    {
        if (status == BookingStatus.Pending)
            throw new ValidationException("Status must be Confirmed or Rejected.");

        if (_bookingStore.GetById(bookingId) is not { } booking)
            return Task.FromResult<Booking?>(null);

        if (booking.Status != BookingStatus.Pending)
            return Task.FromResult<Booking?>(booking);

        booking.Status = status;
        booking.ProcessedAt = DateTime.UtcNow;

        return Task.FromResult<Booking?>(booking);
    }

    private static void ValidateEventId(int eventId)
    {
        if (eventId <= 0)
            throw new ValidationException("EventId is required.");
    }
}
