using EventApi.Application.Abstractions;
using EventApi.Application.Common;
using EventApi.Domain.Entities;
using EventApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EventApi.Infrastructure.Persistence.Repositories;

public class EventRepository(AppDbContext context) : IEventRepository
{
    public Task AddAsync(Event @event, CancellationToken cancellationToken = default)
    {
        context.Events.Add(@event);
        return Task.CompletedTask;
    }

    public async Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default)
    {
        return await context.Events.AnyAsync(@event => @event.Id == id, cancellationToken);
    }

    public async Task<PaginatedResult<Event>> GetAllAsync(
        string? title = null,
        DateTime? from = null,
        DateTime? to = null,
        int page = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var query = context.Events
            .AsNoTracking()
            .OrderBy(@event => @event.Id)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(title))
        {
            var normalizedTitle = title.Trim().ToLower();
            query = query.Where(@event => @event.Title.ToLower().Contains(normalizedTitle));
        }

        if (from.HasValue)
        {
            query = query.Where(@event => @event.StartAt >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(@event => @event.EndAt <= to.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToArrayAsync(cancellationToken);

        return new PaginatedResult<Event>
        {
            TotalCount = totalCount,
            Items = items,
            Page = page,
            PageSize = items.Length
        };
    }

    public async Task<Event?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await context.Events.FindAsync([id], cancellationToken);
    }

    public async Task<bool> RemoveAsync(int id, CancellationToken cancellationToken = default)
    {
        var @event = await context.Events.FindAsync([id], cancellationToken);
        if (@event is null)
            return false;

        context.Events.Remove(@event);
        return true;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await context.SaveChangesAsync(cancellationToken);
    }
}
