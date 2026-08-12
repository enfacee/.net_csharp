using System.ComponentModel.DataAnnotations;
using EventApi.Bookings.Application.Abstractions;
using EventApi.Bookings.Domain.Entities;
using EventApi.Bookings.Domain.Exceptions;

namespace EventApi.Bookings.Application.Services;

public class BookingService(
    IBookingRepository bookingRepository,
    TimeProvider timeProvider) : IBookingService
{
    private const int ActiveBookingLimit = 10;
    private static readonly SemaphoreSlim BookingSemaphore = new(1, 1);

    public async Task<Booking> CreateBookingAsync(int eventId, int userId, CancellationToken cancellationToken = default)
    {
        ValidateEventId(eventId);
        ValidateUserId(userId);

        await BookingSemaphore.WaitAsync(cancellationToken);
        try
        {
            var activeBookingCount = await bookingRepository.CountActiveByUserIdAsync(userId, cancellationToken);
            if (activeBookingCount >= ActiveBookingLimit)
                throw new ActiveBookingLimitExceededException(
                    $"Active booking limit exceeded. Limit is {ActiveBookingLimit}.");

            var booking = new Booking(eventId, userId, timeProvider.GetUtcNow().UtcDateTime);

            await bookingRepository.AddAsync(booking, cancellationToken);

            return booking;
        }
        finally
        {
            BookingSemaphore.Release();
        }
    }

    public async Task<Booking?> GetBookingByIdAsync(int bookingId, CancellationToken cancellationToken = default)
    {
        return await bookingRepository.GetByIdAsync(bookingId, cancellationToken);
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
        if (status is not (BookingStatus.Confirmed or BookingStatus.Rejected))
            throw new ValidationException("Status must be Confirmed or Rejected.");

        await BookingSemaphore.WaitAsync(cancellationToken);
        try
        {
            if (await bookingRepository.GetByIdAsync(bookingId, cancellationToken) is not { } booking)
                return null;

            if (booking.Status != BookingStatus.Pending)
                return booking;

            if (status == BookingStatus.Confirmed)
                booking.Confirm(timeProvider.GetUtcNow().UtcDateTime);
            else
                booking.Reject(timeProvider.GetUtcNow().UtcDateTime);

            await bookingRepository.SaveChangesAsync(cancellationToken);

            return booking;
        }
        finally
        {
            BookingSemaphore.Release();
        }
    }

    public async Task<bool> CancelBookingAsync(
        int bookingId,
        int currentUserId,
        UserRole currentUserRole,
        CancellationToken cancellationToken = default)
    {
        ValidateUserId(currentUserId);

        await BookingSemaphore.WaitAsync(cancellationToken);
        try
        {
            if (await bookingRepository.GetByIdAsync(bookingId, cancellationToken) is not { } booking)
                return false;

            if (booking.UserId != currentUserId && currentUserRole != UserRole.Admin)
                throw new ForbiddenOperationException("You do not have permission to cancel this booking.");

            booking.Cancel(timeProvider.GetUtcNow().UtcDateTime);
            await bookingRepository.SaveChangesAsync(cancellationToken);

            return true;
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
