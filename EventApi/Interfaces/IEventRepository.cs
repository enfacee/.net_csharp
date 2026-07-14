namespace EventApi;

public interface IEventRepository
{
    Task AddAsync(Event @event, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default);
    Task<PaginatedResult<Event>> GetAllAsync(
        string? title = null,
        DateTime? from = null,
        DateTime? to = null,
        int page = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default);
    Task<Event?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<bool> RemoveAsync(int id, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
