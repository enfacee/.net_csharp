using EventApi.Bookings.Application.Abstractions;
using EventApi.Bookings.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EventApi.Bookings.Infrastructure.Persistence.Repositories;

public class BookingRepository(BookingsDbContext context) : IBookingRepository
{
    public async Task AddAsync(Booking booking, CancellationToken cancellationToken = default)
    {
        await context.Bookings.AddAsync(booking, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<Booking?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await context.Bookings.FindAsync([id], cancellationToken);
    }

    public async Task<IReadOnlyCollection<Booking>> GetPendingBookingsAsync(CancellationToken cancellationToken = default)
    {
        return await context.Bookings
            .Where(booking => booking.Status == BookingStatus.Pending)
            .OrderBy(booking => booking.Id)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<int[]> GetPendingBookingIdsAsync(CancellationToken cancellationToken = default)
    {
        return await context.Bookings
            .Where(booking => booking.Status == BookingStatus.Pending)
            .OrderBy(booking => booking.Id)
            .Select(booking => booking.Id)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<int> CountActiveByUserIdAsync(int userId, CancellationToken cancellationToken = default)
    {
        return await context.Bookings.CountAsync(
            booking => booking.UserId == userId &&
                (booking.Status == BookingStatus.Pending || booking.Status == BookingStatus.Confirmed),
            cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await context.SaveChangesAsync(cancellationToken);
    }
}
