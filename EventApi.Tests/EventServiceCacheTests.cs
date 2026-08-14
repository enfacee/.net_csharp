using EventApi.Events.Application.Abstractions;
using EventApi.Events.Application.Common;
using EventApi.Events.Application.DTO;
using EventApi.Events.Application.Services;
using EventApi.Events.Domain.Entities;
using FluentAssertions;

namespace EventApi.Tests;

public sealed class EventServiceCacheTests
{
    [Fact]
    public async Task GetByIdAsync_ShouldNotCallRepository_WhenCacheContainsEvent()
    {
        var @event = CreateEvent(id: 1, title: "Cached event");
        var repository = new FakeEventRepository();
        var cache = new FakeEventReadCache();
        cache.Events[@event.Id] = @event;
        var service = new EventService(repository, cache);

        var result = await service.GetByIdAsync(@event.Id);

        result.Should().BeSameAs(@event);
        cache.GetEventCalls.Should().Be(1);
        repository.GetByIdCalls.Should().Be(0);
        cache.SetEventCalls.Should().Be(0);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldCallRepositoryAndSaveCache_WhenCacheMisses()
    {
        var @event = CreateEvent(id: 1, title: "Repository event");
        var operations = new List<string>();
        var repository = new FakeEventRepository(operations) { EventById = @event };
        var cache = new FakeEventReadCache(operations);
        var service = new EventService(repository, cache);

        var result = await service.GetByIdAsync(@event.Id);

        result.Should().BeSameAs(@event);
        cache.GetEventCalls.Should().Be(1);
        repository.GetByIdCalls.Should().Be(1);
        cache.SetEventCalls.Should().Be(1);
        cache.SavedEvents.Should().ContainSingle(x => x.Id == @event.Id);
        operations.Should().Equal($"cache:get:{@event.Id}", $"repo:get:{@event.Id}", $"cache:set:{@event.Id}");
    }

    [Fact]
    public async Task GetTopAsync_ShouldNotCallRepository_WhenCacheContainsTopEvents()
    {
        var cachedEvents = new[] { CreateEvent(id: 1, title: "Top cached") };
        var repository = new FakeEventRepository();
        var cache = new FakeEventReadCache { CachedTopEvents = cachedEvents };
        var service = new EventService(repository, cache);

        var result = await service.GetTopAsync();

        result.Should().BeSameAs(cachedEvents);
        cache.GetTopEventsCalls.Should().Be(1);
        repository.GetTopCalls.Should().Be(0);
        cache.SetTopEventsCalls.Should().Be(0);
    }

    [Fact]
    public async Task GetTopAsync_ShouldCallRepositoryAndSaveCache_WhenCacheMisses()
    {
        var topEvents = new[] { CreateEvent(id: 1, title: "Top repository") };
        var operations = new List<string>();
        var repository = new FakeEventRepository(operations) { TopEvents = topEvents };
        var cache = new FakeEventReadCache(operations);
        var service = new EventService(repository, cache);

        var result = await service.GetTopAsync();

        result.Should().BeSameAs(topEvents);
        cache.GetTopEventsCalls.Should().Be(1);
        repository.GetTopCalls.Should().Be(1);
        repository.LastTopCount.Should().Be(10);
        cache.SetTopEventsCalls.Should().Be(1);
        cache.SavedTopEvents.Should().BeSameAs(topEvents);
        operations.Should().Equal("cache:get-top", "repo:get-top:10", "cache:set-top");
    }

    [Fact]
    public async Task UpdateEventAsync_ShouldInvalidateEventCache_AfterSavingChanges()
    {
        var @event = CreateEvent(id: 1, title: "Before update");
        var operations = new List<string>();
        var repository = new FakeEventRepository(operations) { EventById = @event };
        var cache = new FakeEventReadCache(operations);
        var service = new EventService(repository, cache);

        var result = await service.UpdateEventAsync(
            @event.Id,
            new EventRequest
            {
                Title = "After update",
                Description = "Updated",
                StartAt = @event.StartAt,
                EndAt = @event.EndAt,
                TotalSeats = 12
            });

        result.Should().BeTrue();
        repository.SaveChangesCalls.Should().Be(1);
        cache.RemoveEventCalls.Should().Be(1);
        cache.RemovedEventIds.Should().ContainSingle().Which.Should().Be(@event.Id);
        operations.Should().Equal($"repo:get:{@event.Id}", "repo:save", $"cache:remove:{@event.Id}");
    }

    [Fact]
    public async Task RemoveAsync_ShouldInvalidateEventCache_WhenRepositoryRemovesEvent()
    {
        var operations = new List<string>();
        var repository = new FakeEventRepository(operations) { RemoveResult = true };
        var cache = new FakeEventReadCache(operations);
        var service = new EventService(repository, cache);

        var result = await service.RemoveAsync(1);

        result.Should().BeTrue();
        repository.RemoveCalls.Should().Be(1);
        cache.RemoveEventCalls.Should().Be(1);
        cache.RemovedEventIds.Should().ContainSingle().Which.Should().Be(1);
        operations.Should().Equal("repo:remove:1", "cache:remove:1");
    }

    [Fact]
    public async Task ReserveSeatsAsync_ShouldInvalidateEventCache_AfterSavingChanges()
    {
        var @event = CreateEvent(id: 1, title: "Reservable event", totalSeats: 5, availableSeats: 5);
        var operations = new List<string>();
        var repository = new FakeEventRepository(operations) { EventById = @event };
        var cache = new FakeEventReadCache(operations);
        var service = new EventService(repository, cache);

        var result = await service.ReserveSeatsAsync(@event.Id, seats: 1);

        result.Should().BeTrue();
        @event.AvailableSeats.Should().Be(4);
        repository.SaveChangesCalls.Should().Be(1);
        cache.RemoveEventCalls.Should().Be(1);
        cache.RemovedEventIds.Should().ContainSingle().Which.Should().Be(@event.Id);
        operations.Should().Equal($"repo:get:{@event.Id}", "repo:save", $"cache:remove:{@event.Id}");
    }

    private static Event CreateEvent(
        int id,
        string title,
        int totalSeats = 10,
        int availableSeats = 10)
    {
        var startAt = DateTime.UtcNow.AddDays(30);

        return Event.Rehydrate(
            id,
            title,
            description: null,
            startAt,
            startAt.AddHours(1),
            totalSeats,
            availableSeats);
    }

    private sealed class FakeEventRepository(List<string>? operations = null) : IEventRepository
    {
        public Event? EventById { get; init; }
        public IReadOnlyCollection<Event> TopEvents { get; init; } = [];
        public bool RemoveResult { get; init; }
        public int GetByIdCalls { get; private set; }
        public int GetTopCalls { get; private set; }
        public int LastTopCount { get; private set; }
        public int RemoveCalls { get; private set; }
        public int SaveChangesCalls { get; private set; }

        public Task AddAsync(Event @event, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<PaginatedResult<Event>> GetAllAsync(
            string? title = null,
            DateTime? from = null,
            DateTime? to = null,
            int page = 1,
            int pageSize = 10,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<Event?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            GetByIdCalls++;
            operations?.Add($"repo:get:{id}");
            return Task.FromResult(EventById?.Id == id ? EventById : null);
        }

        public Task<IReadOnlyCollection<Event>> GetTopBySoldPercentageAsync(
            int count,
            CancellationToken cancellationToken = default)
        {
            GetTopCalls++;
            LastTopCount = count;
            operations?.Add($"repo:get-top:{count}");
            return Task.FromResult(TopEvents);
        }

        public Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<bool> RemoveAsync(int id, CancellationToken cancellationToken = default)
        {
            RemoveCalls++;
            operations?.Add($"repo:remove:{id}");
            return Task.FromResult(RemoveResult);
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveChangesCalls++;
            operations?.Add("repo:save");
            return Task.CompletedTask;
        }
    }

    private sealed class FakeEventReadCache(List<string>? operations = null) : IEventReadCache
    {
        public Dictionary<int, Event> Events { get; } = [];
        public IReadOnlyCollection<Event>? CachedTopEvents { get; init; }
        public List<Event> SavedEvents { get; } = [];
        public IReadOnlyCollection<Event>? SavedTopEvents { get; private set; }
        public List<int> RemovedEventIds { get; } = [];
        public int GetEventCalls { get; private set; }
        public int SetEventCalls { get; private set; }
        public int RemoveEventCalls { get; private set; }
        public int GetTopEventsCalls { get; private set; }
        public int SetTopEventsCalls { get; private set; }

        public Task<Event?> GetEventAsync(int id, CancellationToken cancellationToken = default)
        {
            GetEventCalls++;
            operations?.Add($"cache:get:{id}");
            return Task.FromResult(Events.GetValueOrDefault(id));
        }

        public Task SetEventAsync(Event @event, CancellationToken cancellationToken = default)
        {
            SetEventCalls++;
            SavedEvents.Add(@event);
            operations?.Add($"cache:set:{@event.Id}");
            return Task.CompletedTask;
        }

        public Task RemoveEventAsync(int id, CancellationToken cancellationToken = default)
        {
            RemoveEventCalls++;
            RemovedEventIds.Add(id);
            operations?.Add($"cache:remove:{id}");
            return Task.CompletedTask;
        }

        public Task<IReadOnlyCollection<Event>?> GetTopEventsAsync(CancellationToken cancellationToken = default)
        {
            GetTopEventsCalls++;
            operations?.Add("cache:get-top");
            return Task.FromResult(CachedTopEvents);
        }

        public Task SetTopEventsAsync(
            IReadOnlyCollection<Event> events,
            CancellationToken cancellationToken = default)
        {
            SetTopEventsCalls++;
            SavedTopEvents = events;
            operations?.Add("cache:set-top");
            return Task.CompletedTask;
        }
    }
}
