using System.ComponentModel.DataAnnotations;
using EventApi.Application.Abstractions;
using EventApi.Domain.Entities;

namespace EventApi.Application.Services;

public sealed class UserService(
    IUserRepository userRepository,
    IUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher,
    IJwtTokenGenerator jwtTokenGenerator) : IUserService
{
    public async Task RegisterAsync(
        string login,
        string password,
        UserRole role = UserRole.User,
        CancellationToken cancellationToken = default)
    {
        ValidateCredentials(login, password);

        if (await userRepository.ExistsByLoginAsync(login, cancellationToken))
            throw new ValidationException("User with the same login already exists.");

        var user = new User(login, passwordHasher.Hash(password), role);
        await userRepository.AddAsync(user, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<string?> LoginAsync(
        string login,
        string password,
        CancellationToken cancellationToken = default)
    {
        ValidateCredentials(login, password);

        var user = await userRepository.GetByLoginAsync(login, cancellationToken);
        if (user is null || !passwordHasher.Verify(password, user.PasswordHash))
            return null;

        return jwtTokenGenerator.GenerateToken(user);
    }

    private static void ValidateCredentials(string login, string password)
    {
        if (string.IsNullOrWhiteSpace(login))
            throw new ValidationException("Login is required.");

        if (string.IsNullOrWhiteSpace(password))
            throw new ValidationException("Password is required.");
    }
}
