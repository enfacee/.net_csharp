using EventApi.Events.Application.Common;
using EventApi.Events.Application.DTO;
using EventApi.Events.Domain.Entities;

namespace EventApi.Events.Application.Abstractions;

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
    Task<bool> RemoveAsync(int id, CancellationToken cancellationToken = default);
    Task<bool> UpdateEventAsync(int id, EventRequest request, CancellationToken cancellationToken = default);
    Task<bool> ReserveSeatsAsync(int id, int seats, CancellationToken cancellationToken = default);
}
