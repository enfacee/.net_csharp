using System.ComponentModel.DataAnnotations;
using EventApi.Application.Abstractions;
using EventApi.Domain.Entities;
using EventApi.Domain.Exceptions;

namespace EventApi.Application.Services;

public class BookingService(
    IBookingRepository bookingRepository,
    IEventRepository eventRepository,
    TimeProvider timeProvider) : IBookingService
{
    private static readonly SemaphoreSlim BookingSemaphore = new(1, 1);

    public async Task<Booking> CreateBookingAsync(int eventId, int userId, CancellationToken cancellationToken = default)
    {
        ValidateEventId(eventId);
        ValidateUserId(userId);

        await BookingSemaphore.WaitAsync(cancellationToken);
        try
        {
            var @event = await eventRepository.GetByIdAsync(eventId, cancellationToken)
                ?? throw new NotFoundException("Event not found.");

            if (!@event.TryReserveSeats())
                throw new NoAvailableSeatsException("No available seats for this event");

            var booking = new Booking(eventId, userId, timeProvider.GetUtcNow().UtcDateTime);

            await bookingRepository.AddAsync(booking, cancellationToken);
            await bookingRepository.SaveChangesAsync(cancellationToken);

            return booking;
        }
        finally
        {
            BookingSemaphore.Release();
        }
    }

    public async Task<Booking?> GetBookingByIdAsync(int bookingId)
    {
        return await bookingRepository.GetByIdAsync(bookingId);
    }

    public async Task<IReadOnlyCollection<Booking>> GetPendingBookingsAsync(CancellationToken cancellationToken = default)
    {
        return await bookingRepository.GetPendingBookingsAsync(cancellationToken);
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
            if (await bookingRepository.GetByIdAsync(bookingId, cancellationToken) is not { } booking)
                return null;

            if (booking.Status != BookingStatus.Pending)
                return booking;

            if (status == BookingStatus.Confirmed)
            {
                if (!await eventRepository.ExistsAsync(booking.EventId, cancellationToken))
                {
                    booking.Reject(timeProvider.GetUtcNow().UtcDateTime);
                    await bookingRepository.SaveChangesAsync(cancellationToken);
                    return booking;
                }

                booking.Confirm(timeProvider.GetUtcNow().UtcDateTime);
            }
            else
            {
                booking.Reject(timeProvider.GetUtcNow().UtcDateTime);

                if (await eventRepository.GetByIdAsync(booking.EventId, cancellationToken) is { } @event)
                    @event.ReleaseSeats();
            }

            await bookingRepository.SaveChangesAsync(cancellationToken);

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
            throw new ValidationException("EventId must be greater than 0.");
    }

    private static void ValidateUserId(int userId)
    {
        if (userId <= 0)
            throw new ValidationException("UserId must be greater than 0.");
    }
}

