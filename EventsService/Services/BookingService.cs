using System.ComponentModel.DataAnnotations;

public class BookingService(IEventService eventService, IBookingStore bookingStore) : IBookingService
{
    private readonly Lock _bookingLock = new();

    public BookingService(IEventService eventService)
        : this(eventService, new InMemoryBookingStore())
    {
    }

    public Task<Booking> CreateBookingAsync(int eventId)
    {
        ValidateEventId(eventId);

        using (_bookingLock.EnterScope())
        {
            var @event = eventService.GetById(eventId)
                ?? throw new NotFoundException("Event not found.");

            if (!@event.TryReserveSeats())
                throw new NoAvailableSeatsException("No available seats for this event");

            eventService.Update(@event);

            var booking = new Booking(eventId);
            if (!bookingStore.TryAdd(booking))
                throw new ValidationException("Booking with the same Id already exists.");

            return Task.FromResult(booking);
        }
    }

    public Task<Booking?> GetBookingByIdAsync(int bookingId)
    {
        return Task.FromResult(bookingStore.GetById(bookingId));
    }

    public Task<IReadOnlyCollection<Booking>> GetPendingBookingsAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyCollection<Booking> pendingBookings = bookingStore.GetPending()
            .OrderBy(x => x.Id)
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

        if (bookingStore.GetById(bookingId) is not { } booking)
            return Task.FromResult<Booking?>(null);

        if (booking.Status != BookingStatus.Pending)
            return Task.FromResult<Booking?>(booking);

        if (status == BookingStatus.Confirmed)
            booking.Confirm();
        else
        {
            booking.Reject();

            if (eventService.GetById(booking.EventId) is { } @event)
            {
                @event.ReleaseSeats();
                eventService.Update(@event);
            }
        }

        bookingStore.TryUpdate(booking);

        return Task.FromResult<Booking?>(booking);
    }

    private static void ValidateEventId(int eventId)
    {
        if (eventId <= 0)
            throw new ValidationException("EventId is required.");
    }
}
