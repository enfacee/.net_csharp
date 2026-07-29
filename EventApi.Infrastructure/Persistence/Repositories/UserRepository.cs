using EventApi.Application.Abstractions;
using EventApi.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EventApi.Infrastructure.Persistence.Repositories;

public class UserRepository(AppDbContext context) : IUserRepository
{
    public Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        context.Users.Add(user);
        return Task.CompletedTask;
    }

    public async Task<User?> GetByLoginAsync(string login, CancellationToken cancellationToken = default)
    {
        return await context.Users
            .SingleOrDefaultAsync(user => user.Login == login, cancellationToken);
    }

    public async Task<bool> ExistsByLoginAsync(string login, CancellationToken cancellationToken = default)
    {
        return await context.Users
            .AnyAsync(user => user.Login == login, cancellationToken);
    }

}
