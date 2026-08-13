using EventApi.Users.Domain.Entities;

namespace EventApi.Users.Application.Abstractions;

public interface IUserRepository
{
    Task AddAsync(User user, CancellationToken cancellationToken = default);
    Task<User?> GetByLoginAsync(string login, CancellationToken cancellationToken = default);
    Task<bool> ExistsByLoginAsync(string login, CancellationToken cancellationToken = default);
}
