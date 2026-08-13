using EventApi.Events.Domain.Entities;
using EventApi.Events.Infrastructure.Persistence.Repositories;

namespace EventApi.IntegrationTests;

[Collection(PostgreSqlCollection.Name)]
public sealed class EventRepositoryTests(PostgreSqlContainerFixture fixture) : IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        await fixture.ResetEventsDatabaseAsync();
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    [Fact]
    public async Task AddAsync_GetByIdAsync_AndExistsAsync_ShouldPersistEventWithDatabaseGeneratedId()
    {
        await using var context = fixture.CreateEventsContext();
        var repository = new EventRepository(context);
        var @event = CreateEvent("Architecture review");

        await repository.AddAsync(@event);

        Assert.True(@event.Id > 0);
        Assert.True(await repository.ExistsAsync(@event.Id));
        Assert.False(await repository.ExistsAsync(@event.Id + 1));

        var result = await repository.GetByIdAsync(@event.Id);
        Assert.NotNull(result);
        Assert.Equal("Architecture review", result.Title);
        Assert.Equal(10, result.AvailableSeats);
    }

    [Fact]
    public async Task GetAllAsync_ShouldFilterByTitleDateAndPaginate()
    {
        await SeedEventsAsync(
            CreateEvent("Team sync old", startAt: UtcDate(2026, 5, 1), endAt: UtcDate(2026, 5, 1, 11)),
            CreateEvent("Team sync target", startAt: UtcDate(2026, 5, 10), endAt: UtcDate(2026, 5, 10, 11)),
            CreateEvent("Client sync target", startAt: UtcDate(2026, 5, 11), endAt: UtcDate(2026, 5, 11, 11)),
            CreateEvent("Workshop", startAt: UtcDate(2026, 5, 10), endAt: UtcDate(2026, 5, 10, 11)));

        await using var context = fixture.CreateEventsContext();
        var repository = new EventRepository(context);

        var result = await repository.GetAllAsync(
            title: "sync",
            from: UtcDate(2026, 5, 5),
            to: UtcDate(2026, 5, 12),
            page: 1,
            pageSize: 1);

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(1, result.Page);
        Assert.Equal(1, result.PageSize);
        Assert.Equal("Team sync target", Assert.Single(result.Items).Title);
    }

    [Fact]
    public async Task SaveChangesAsync_ShouldPersistTrackedChanges()
    {
        var @event = await SeedEventAsync(CreateEvent("Original"));

        await using var context = fixture.CreateEventsContext();
        var repository = new EventRepository(context);
        var persistedEvent = await repository.GetByIdAsync(@event.Id);
        Assert.NotNull(persistedEvent);

        persistedEvent.TryReserveSeats();
        await repository.SaveChangesAsync();

        await using var assertContext = fixture.CreateEventsContext();
        var result = await new EventRepository(assertContext).GetByIdAsync(@event.Id);
        Assert.Equal(9, result!.AvailableSeats);
    }

    [Fact]
    public async Task RemoveAsync_ShouldRemoveExistingEventAndReturnFalseForMissingEvent()
    {
        var @event = await SeedEventAsync(CreateEvent("To remove"));

        await using var context = fixture.CreateEventsContext();
        var repository = new EventRepository(context);

        Assert.True(await repository.RemoveAsync(@event.Id));
        Assert.False(await repository.RemoveAsync(@event.Id));
        Assert.Null(await repository.GetByIdAsync(@event.Id));
    }

    private async Task<Event> SeedEventAsync(Event @event)
    {
        await SeedEventsAsync(@event);
        return @event;
    }

    private async Task SeedEventsAsync(params Event[] events)
    {
        await using var context = fixture.CreateEventsContext();
        var repository = new EventRepository(context);

        foreach (var @event in events)
        {
            await repository.AddAsync(@event);
        }
    }

    private static Event CreateEvent(
        string title,
        DateTime? startAt = null,
        DateTime? endAt = null,
        int totalSeats = 10)
    {
        var actualStartAt = startAt ?? UtcDate(2026, 5, 10);
        var actualEndAt = endAt ?? actualStartAt.AddHours(1);

        return new Event(title, null, actualStartAt, actualEndAt, totalSeats);
    }

    private static DateTime UtcDate(int year, int month, int day, int hour = 10)
    {
        return new DateTime(year, month, day, hour, 0, 0, DateTimeKind.Utc);
    }
}
