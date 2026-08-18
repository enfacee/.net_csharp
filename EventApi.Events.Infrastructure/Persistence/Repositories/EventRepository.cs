using EventApi.Events.Application.Abstractions;
using EventApi.Events.Application.Common;
using EventApi.Events.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EventApi.Events.Infrastructure.Persistence.Repositories;

public class EventRepository(EventsDbContext context) : IEventRepository
{
    public async Task AddAsync(Event @event, CancellationToken cancellationToken = default)
    {
        await context.Events.AddAsync(@event, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<PaginatedResult<Event>> GetAllAsync(
        string? title = null,
        DateTime? from = null,
        DateTime? to = null,
        int page = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var query = context.Events.AsQueryable();

        if (!string.IsNullOrWhiteSpace(title))
        {
            var loweredTitle = title.ToLower();
            query = query.Where(@event => @event.Title.ToLower().Contains(loweredTitle));
        }

        if (from.HasValue)
            query = query.Where(@event => @event.StartAt >= from.Value);

        if (to.HasValue)
            query = query.Where(@event => @event.EndAt <= to.Value);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(@event => @event.StartAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToArrayAsync(cancellationToken);

        return new PaginatedResult<Event>
        {
            TotalCount = totalCount,
            Items = items,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<Event?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await context.Events.FindAsync([id], cancellationToken);
    }

    public async Task<IReadOnlyCollection<Event>> GetTopBySoldPercentageAsync(
        int count,
        CancellationToken cancellationToken = default)
    {
        return await context.Events
            .OrderByDescending(@event => (double)(@event.TotalSeats - @event.AvailableSeats) / @event.TotalSeats)
            .ThenBy(@event => @event.StartAt)
            .Take(count)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default)
    {
        return await context.Events.AnyAsync(@event => @event.Id == id, cancellationToken);
    }

    public async Task<bool> RemoveAsync(int id, CancellationToken cancellationToken = default)
    {
        var @event = await GetByIdAsync(id, cancellationToken);

        if (@event is null)
            return false;

        context.Events.Remove(@event);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await context.SaveChangesAsync(cancellationToken);
    }
}
