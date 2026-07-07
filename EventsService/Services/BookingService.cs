using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

public class BookingService(AppDbContext context) : IBookingService
{
    private static readonly SemaphoreSlim BookingSemaphore = new(1, 1);

    public async Task<Booking> CreateBookingAsync(int eventId)
    {
        ValidateEventId(eventId);

        await BookingSemaphore.WaitAsync();
        try
        {
            var @event = await context.Events.FindAsync(eventId)
                ?? throw new NotFoundException("Event not found.");

            if (!@event.TryReserveSeats())
                throw new NoAvailableSeatsException("No available seats for this event");

            var booking = new Booking(eventId);
            if (await context.Bookings.AnyAsync(b => b.Id == booking.Id))
                throw new ValidationException("Booking with the same Id already exists.");

            context.Bookings.Add(booking);
            await context.SaveChangesAsync();

            return booking;
        }
        finally
        {
            BookingSemaphore.Release();
        }
    }

    public async Task<Booking?> GetBookingByIdAsync(int bookingId)
    {
        return await context.Bookings.FindAsync(bookingId);
    }

    public async Task<IReadOnlyCollection<Booking>> GetPendingBookingsAsync(CancellationToken cancellationToken = default)
    {
        return await context.Bookings
            .AsNoTracking()
            .Where(booking => booking.Status == BookingStatus.Pending)
            .OrderBy(x => x.Id)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<Booking?> UpdateBookingStatusAsync(
        int bookingId,
        BookingStatus status,
        CancellationToken cancellationToken = default)
    {
        if (status == BookingStatus.Pending)
            throw new ValidationException("Status must be Confirmed or Rejected.");

        await BookingSemaphore.WaitAsync(cancellationToken);
        try
        {
            if (await context.Bookings.FindAsync([bookingId], cancellationToken) is not { } booking)
                return null;

            if (booking.Status != BookingStatus.Pending)
                return booking;

            if (status == BookingStatus.Confirmed)
                booking.Confirm();
            else
            {
                booking.Reject();

                if (await context.Events.FindAsync([booking.EventId], cancellationToken) is { } @event)
                    @event.ReleaseSeats();
            }

            await context.SaveChangesAsync(cancellationToken);

            return booking;
        }
        finally
        {
            BookingSemaphore.Release();
        }
    }

    private static void ValidateEventId(int eventId)
    {
        if (eventId <= 0)
            throw new ValidationException("EventId is required.");
    }
}
