using System.ComponentModel.DataAnnotations;
using EventApi.Users.Application.Abstractions;
using EventApi.Users.Application.Services;
using EventApi.Users.Domain.Entities;
using EventApi.Users.Infrastructure.Persistence;
using EventApi.Users.Infrastructure.Persistence.Repositories;
using EventApi.Users.Infrastructure.Security;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EventApi.Tests;

public sealed class UserServiceTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly FakeJwtTokenGenerator _jwtTokenGenerator = new();

    public UserServiceTests()
    {
        var services = new ServiceCollection();
        services.AddDbContext<UsersDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddSingleton<IPasswordHasher, Sha256PasswordHasher>();
        services.AddSingleton<IJwtTokenGenerator>(_jwtTokenGenerator);
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUserService, UserService>();

        _serviceProvider = services.BuildServiceProvider();
    }

    [Fact]
    public async Task RegisterAsync_ShouldPersistUserWithHashedPassword()
    {
        using var scope = _serviceProvider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IUserService>();
        var repository = scope.ServiceProvider.GetRequiredService<IUserRepository>();

        await service.RegisterAsync("admin", "password123", UserRole.Admin);

        var user = await repository.GetByLoginAsync("admin");
        user.Should().NotBeNull();
        user!.Role.Should().Be(UserRole.Admin);
        user.PasswordHash.Should().NotBe("password123");
        user.PasswordHash.Should().HaveLength(64);
    }

    [Fact]
    public async Task RegisterAsync_ShouldThrowValidationException_WhenLoginAlreadyExists()
    {
        using var scope = _serviceProvider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IUserService>();
        await service.RegisterAsync("duplicate", "password123");

        Func<Task> act = () => service.RegisterAsync("duplicate", "password456");

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*same login*");
    }

    [Fact]
    public async Task LoginAsync_ShouldReturnToken_WhenCredentialsAreValid()
    {
        using var scope = _serviceProvider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IUserService>();
        await service.RegisterAsync("user", "password123");

        var token = await service.LoginAsync("user", "password123");

        token.Should().Be("token-user");
        _jwtTokenGenerator.Users.Should().ContainSingle(user => user.Login == "user");
    }

    [Fact]
    public async Task LoginAsync_ShouldReturnNull_WhenCredentialsAreInvalid()
    {
        using var scope = _serviceProvider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IUserService>();
        await service.RegisterAsync("user", "password123");

        var token = await service.LoginAsync("user", "wrong-password");

        token.Should().BeNull();
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
    }

    private sealed class FakeJwtTokenGenerator : IJwtTokenGenerator
    {
        public List<User> Users { get; } = [];

        public string GenerateToken(User user)
        {
            Users.Add(user);
            return $"token-{user.Login}";
        }
    }
}
