using EventApi.Domain.Entities;
using EventApi.Infrastructure.Persistence;
using EventApi.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace EventApi.IntegrationTests;

[Collection(PostgreSqlCollection.Name)]
public sealed class UserRepositoryTests(PostgreSqlContainerFixture fixture) : IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        await fixture.ResetDatabaseAsync();
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    [Fact]
    public async Task AddAsync_UnitOfWorkSaveChangesAsync_AndGetByLoginAsync_ShouldPersistUser()
    {
        await using var context = fixture.CreateContext();
        var repository = new UserRepository(context);
        var unitOfWork = new EfUnitOfWork(context);
        var user = new User("repository-user", "HASH", UserRole.Admin);

        await repository.AddAsync(user);
        await unitOfWork.SaveChangesAsync();

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
    public async Task UnitOfWorkSaveChangesAsync_ShouldEnforceUniqueLogin()
    {
        await using var context = fixture.CreateContext();
        var repository = new UserRepository(context);
        var unitOfWork = new EfUnitOfWork(context);

        await repository.AddAsync(new User("duplicate-user", "HASH1", UserRole.User));
        await repository.AddAsync(new User("duplicate-user", "HASH2", UserRole.Admin));

        await Assert.ThrowsAsync<DbUpdateException>(() => unitOfWork.SaveChangesAsync());
    }
}
