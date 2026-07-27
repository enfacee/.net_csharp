using EventApi.Domain.Entities;
using EventApi.Infrastructure.Persistence.Repositories;

namespace EventApi.IntegrationTests;

[Collection(PostgreSqlCollection.Name)]
public sealed class EventRepositoryTests(PostgreSqlContainerFixture fixture) : IAsyncLifetime
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
    public async Task AddAsync_SaveChangesAsync_GetByIdAsync_AndExistsAsync_ShouldPersistEventWithDatabaseGeneratedId()
    {
        await using var context = fixture.CreateContext();
        var repository = new EventRepository(context);
        var @event = CreateEvent("Architecture review");

        await repository.AddAsync(@event);
        await repository.SaveChangesAsync();

        Assert.True(@event.Id > 0);
        Assert.True(await repository.ExistsAsync(@event.Id));
        Assert.False(await repository.ExistsAsync(@event.Id + 1));

        var result = await repository.GetByIdAsync(@event.Id);
        Assert.NotNull(result);
        Assert.Equal("Architecture review", result.Title);
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnAllEventsOrderedById()
    {
        await SeedEventsAsync(
            CreateEvent("Second", startAt: UtcDate(2026, 5, 11)),
            CreateEvent("First", startAt: UtcDate(2026, 5, 10)),
            CreateEvent("Third", startAt: UtcDate(2026, 5, 12)));

        await using var context = fixture.CreateContext();
        var repository = new EventRepository(context);

        var result = await repository.GetAllAsync();

        Assert.Equal(3, result.TotalCount);
        Assert.Equal(1, result.Page);
        Assert.Equal(10, result.PageSize);
        Assert.Equal(["Second", "First", "Third"], result.Items.Select(@event => @event.Title).ToArray());
    }

    [Fact]
    public async Task GetAllAsync_ShouldFilterByTitleIgnoringCaseAndWhitespace()
    {
        await SeedEventsAsync(
            CreateEvent("Team Sync"),
            CreateEvent("Client sync"),
            CreateEvent("Workshop"));

        await using var context = fixture.CreateContext();
        var repository = new EventRepository(context);

        var result = await repository.GetAllAsync(title: "  SYNC ");

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(["Team Sync", "Client sync"], result.Items.Select(@event => @event.Title).ToArray());
    }

    [Fact]
    public async Task GetAllAsync_ShouldFilterByStartDate()
    {
        await SeedEventsAsync(
            CreateEvent("Before", startAt: UtcDate(2026, 5, 9)),
            CreateEvent("At boundary", startAt: UtcDate(2026, 5, 10)),
            CreateEvent("After", startAt: UtcDate(2026, 5, 11)));

        await using var context = fixture.CreateContext();
        var repository = new EventRepository(context);

        var result = await repository.GetAllAsync(from: UtcDate(2026, 5, 10));

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(["At boundary", "After"], result.Items.Select(@event => @event.Title).ToArray());
    }

    [Fact]
    public async Task GetAllAsync_ShouldFilterByEndDate()
    {
        await SeedEventsAsync(
            CreateEvent("Before", endAt: UtcDate(2026, 5, 10, 11)),
            CreateEvent("At boundary", endAt: UtcDate(2026, 5, 11, 11)),
            CreateEvent("After", endAt: UtcDate(2026, 5, 12, 11)));

        await using var context = fixture.CreateContext();
        var repository = new EventRepository(context);

        var result = await repository.GetAllAsync(to: UtcDate(2026, 5, 11, 11));

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(["Before", "At boundary"], result.Items.Select(@event => @event.Title).ToArray());
    }

    [Fact]
    public async Task GetAllAsync_ShouldCombineFilters()
    {
        await SeedEventsAsync(
            CreateEvent("Team sync old", startAt: UtcDate(2026, 5, 1), endAt: UtcDate(2026, 5, 1, 11)),
            CreateEvent("Team sync target", startAt: UtcDate(2026, 5, 10), endAt: UtcDate(2026, 5, 10, 11)),
            CreateEvent("Team sync late", startAt: UtcDate(2026, 5, 20), endAt: UtcDate(2026, 5, 20, 11)),
            CreateEvent("Workshop target", startAt: UtcDate(2026, 5, 10), endAt: UtcDate(2026, 5, 10, 11)));

        await using var context = fixture.CreateContext();
        var repository = new EventRepository(context);

        var result = await repository.GetAllAsync(
            title: "sync",
            from: UtcDate(2026, 5, 5),
            to: UtcDate(2026, 5, 15));

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("Team sync target", Assert.Single(result.Items).Title);
    }

    [Fact]
    public async Task GetAllAsync_ShouldPaginateFilteredResults()
    {
        await SeedEventsAsync(
            CreateEvent("Event 1"),
            CreateEvent("Event 2"),
            CreateEvent("Event 3"),
            CreateEvent("Event 4"),
            CreateEvent("Event 5"));

        await using var context = fixture.CreateContext();
        var repository = new EventRepository(context);

        var result = await repository.GetAllAsync(page: 2, pageSize: 2);

        Assert.Equal(5, result.TotalCount);
        Assert.Equal(2, result.Page);
        Assert.Equal(2, result.PageSize);
        Assert.Equal(["Event 3", "Event 4"], result.Items.Select(@event => @event.Title).ToArray());
    }

    [Fact]
    public async Task SaveChangesAsync_ShouldPersistTrackedChanges()
    {
        var @event = await SeedEventAsync(CreateEvent("Original"));

        await using var context = fixture.CreateContext();
        var repository = new EventRepository(context);
        var persistedEvent = await repository.GetByIdAsync(@event.Id);
        Assert.NotNull(persistedEvent);

        persistedEvent.Title = "Updated";
        await repository.SaveChangesAsync();

        await using var assertContext = fixture.CreateContext();
        var result = await new EventRepository(assertContext).GetByIdAsync(@event.Id);
        Assert.Equal("Updated", result!.Title);
    }

    [Fact]
    public async Task RemoveAsync_ShouldRemoveExistingEventAndReturnFalseForMissingEvent()
    {
        var @event = await SeedEventAsync(CreateEvent("To remove"));

        await using var context = fixture.CreateContext();
        var repository = new EventRepository(context);

        Assert.True(await repository.RemoveAsync(@event.Id));
        await repository.SaveChangesAsync();
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
        await using var context = fixture.CreateContext();
        var repository = new EventRepository(context);

        foreach (var @event in events)
        {
            await repository.AddAsync(@event);
        }

        await repository.SaveChangesAsync();
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
