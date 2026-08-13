using EventApi.Users.Application.Abstractions;
using EventApi.Users.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EventApi.Users.Infrastructure.Persistence.Repositories;

public class UserRepository(UsersDbContext context) : IUserRepository
{
    public async Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        await context.Users.AddAsync(user, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<User?> GetByLoginAsync(string login, CancellationToken cancellationToken = default)
    {
        return await context.Users.FirstOrDefaultAsync(user => user.Login == login, cancellationToken);
    }

    public async Task<bool> ExistsByLoginAsync(string login, CancellationToken cancellationToken = default)
    {
        return await context.Users.AnyAsync(user => user.Login == login, cancellationToken);
    }
}
