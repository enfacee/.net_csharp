using System.ComponentModel.DataAnnotations;
using EventApi.Application.Abstractions;
using EventApi.Domain.Entities;
using EventApi.Domain.Exceptions;

namespace EventApi.Application.Services;

public class BookingService(
    IBookingRepository bookingRepository,
    IEventRepository eventRepository) : IBookingService
{
    private static readonly SemaphoreSlim BookingSemaphore = new(1, 1);

    public async Task<Booking> CreateBookingAsync(int eventId)
    {
        ValidateEventId(eventId);

        await BookingSemaphore.WaitAsync();
        try
        {
            var @event = await eventRepository.GetByIdAsync(eventId)
                ?? throw new NotFoundException("Event not found.");

            if (!@event.TryReserveSeats())
                throw new NoAvailableSeatsException("No available seats for this event");

            var booking = new Booking(eventId);
            if (await bookingRepository.ExistsAsync(booking.Id))
                throw new ValidationException("Booking with the same Id already exists.");

            await bookingRepository.AddAsync(booking);
            await bookingRepository.SaveChangesAsync();

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
                booking.Confirm();
            else
            {
                booking.Reject();

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
            throw new ValidationException("EventId is required.");
    }
}

