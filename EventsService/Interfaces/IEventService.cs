public interface IEventService
{
    Task<Event> CreateEventAsync(
        string title,
        string? description,
        DateTime startAt,
        DateTime endAt,
        int totalSeats,
        CancellationToken cancellationToken = default);

    Task<PaginatedResult<Event>> GetAllAsync(
        string? title = null,
        DateTime? from = null,
        DateTime? to = null,
        int page = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default);

    Task<Event?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task AddAsync(Event @event, CancellationToken cancellationToken = default);
    Task UpdateAsync(Event @event, CancellationToken cancellationToken = default);
    Task<bool> RemoveAsync(int id, CancellationToken cancellationToken = default);
}
