using EventApi.Users.Domain.Entities;
using EventApi.Users.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace EventApi.IntegrationTests;

[Collection(PostgreSqlCollection.Name)]
public sealed class UserRepositoryTests(PostgreSqlContainerFixture fixture) : IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        await fixture.ResetUsersDatabaseAsync();
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    [Fact]
    public async Task AddAsync_GetByLoginAsync_AndExistsByLoginAsync_ShouldPersistUser()
    {
        await using var context = fixture.CreateUsersContext();
        var repository = new UserRepository(context);
        var user = new User("repository-user", "HASH", UserRole.Admin);

        await repository.AddAsync(user);

        var result = await repository.GetByLoginAsync("repository-user");

        Assert.NotNull(result);
        Assert.True(result.Id > 0);
        Assert.Equal("repository-user", result.Login);
        Assert.Equal("HASH", result.PasswordHash);
        Assert.Equal(UserRole.Admin, result.Role);
        Assert.True(await repository.ExistsByLoginAsync("repository-user"));
        Assert.False(await repository.ExistsByLoginAsync("missing-user"));
    }

    [Fact]
    public async Task AddAsync_ShouldEnforceUniqueLogin()
    {
        await using var context = fixture.CreateUsersContext();
        var repository = new UserRepository(context);

        await repository.AddAsync(new User("duplicate-user", "HASH1", UserRole.User));

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            repository.AddAsync(new User("duplicate-user", "HASH2", UserRole.Admin)));
    }
}
