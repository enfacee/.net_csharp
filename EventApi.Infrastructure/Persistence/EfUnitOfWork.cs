using EventApi.Application.Abstractions;

namespace EventApi.Infrastructure.Persistence;

public sealed class EfUnitOfWork(AppDbContext context) : IUnitOfWork
{
    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await context.SaveChangesAsync(cancellationToken);
    }
}
