using EventApi.Bookings.Infrastructure.Persistence;
using EventApi.Events.Infrastructure.Persistence;
using EventApi.Users.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace EventApi.IntegrationTests;

public sealed class PostgreSqlContainerFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer container = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("eventapi_integration_tests")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public async Task InitializeAsync()
    {
        await container.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await container.DisposeAsync();
    }

    public UsersDbContext CreateUsersContext()
    {
        var options = new DbContextOptionsBuilder<UsersDbContext>()
            .UseNpgsql(container.GetConnectionString())
            .Options;

        return new UsersDbContext(options);
    }

    public EventsDbContext CreateEventsContext()
    {
        var options = new DbContextOptionsBuilder<EventsDbContext>()
            .UseNpgsql(container.GetConnectionString())
            .Options;

        return new EventsDbContext(options);
    }

    public BookingsDbContext CreateBookingsContext()
    {
        var options = new DbContextOptionsBuilder<BookingsDbContext>()
            .UseNpgsql(container.GetConnectionString())
            .Options;

        return new BookingsDbContext(options);
    }

    public async Task ResetUsersDatabaseAsync()
    {
        await using var context = CreateUsersContext();
        await context.Database.EnsureDeletedAsync();
        await context.Database.MigrateAsync();
    }

    public async Task ResetEventsDatabaseAsync()
    {
        await using var context = CreateEventsContext();
        await context.Database.EnsureDeletedAsync();
        await context.Database.MigrateAsync();
    }

    public async Task ResetBookingsDatabaseAsync()
    {
        await using var context = CreateBookingsContext();
        await context.Database.EnsureDeletedAsync();
        await context.Database.MigrateAsync();
    }
}
