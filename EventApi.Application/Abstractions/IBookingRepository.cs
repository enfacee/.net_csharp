using EventApi.Domain.Entities;

namespace EventApi.Application.Abstractions;

public interface IBookingRepository
{
    Task AddAsync(Booking booking, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default);
    Task<Booking?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<int> CountActiveByUserIdAsync(int userId, CancellationToken cancellationToken = default);
    Task<int[]> GetPendingBookingIdsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<Booking>> GetPendingBookingsAsync(CancellationToken cancellationToken = default);
}
