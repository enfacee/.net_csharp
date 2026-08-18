using System.Collections.Concurrent;
using EventApi.Events.Application.Abstractions;
using EventApi.Events.Application.Common;
using EventApi.Events.Application.DTO;
using EventApi.Events.Application.Services;
using EventApi.Events.Domain.Entities;
using FluentAssertions;

namespace EventApi.Tests;

public sealed class EventServiceCacheTests
{
    private static readonly DateTime TestNow = new(2030, 1, 1, 10, 0, 0, DateTimeKind.Utc);

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
        cache.GetEventCalls.Should().Be(2);
        repository.GetByIdCalls.Should().Be(1);
        cache.SetEventCalls.Should().Be(1);
        cache.SavedEvents.Should().ContainSingle(x => x.Id == @event.Id);
        operations.Should().Equal(
            $"cache:get:{@event.Id}",
            $"cache:get:{@event.Id}",
            $"repo:get:{@event.Id}",
            $"cache:set:{@event.Id}");
    }

    [Fact]
    public async Task GetByIdAsync_ShouldUseSingleFlight_WhenManyRequestsMissSameKey()
    {
        var @event = CreateEvent(id: 1, title: "Concurrent repository event");
        var repository = new FakeEventRepository
        {
            EventById = @event,
            Delay = TimeSpan.FromMilliseconds(50)
        };
        var cache = new FakeEventReadCache();
        var service = new EventService(repository, cache);

        var results = await Task.WhenAll(
            Enumerable.Range(0, 20).Select(_ => service.GetByIdAsync(@event.Id)));

        results.Should().OnlyContain(result => ReferenceEquals(result, @event));
        repository.GetByIdCalls.Should().Be(1);
        cache.SetEventCalls.Should().Be(1);
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
        cache.GetTopEventsCalls.Should().Be(2);
        repository.GetTopCalls.Should().Be(1);
        repository.LastTopCount.Should().Be(10);
        cache.SetTopEventsCalls.Should().Be(1);
        cache.SavedTopEvents.Should().BeSameAs(topEvents);
        operations.Should().Equal("cache:get-top", "cache:get-top", "repo:get-top:10", "cache:set-top");
    }

    [Fact]
    public async Task GetTopAsync_ShouldUseSingleFlight_WhenManyRequestsMissSameKey()
    {
        var topEvents = new[] { CreateEvent(id: 1, title: "Concurrent top event") };
        var repository = new FakeEventRepository
        {
            TopEvents = topEvents,
            Delay = TimeSpan.FromMilliseconds(50)
        };
        var cache = new FakeEventReadCache();
        var service = new EventService(repository, cache);

        var results = await Task.WhenAll(
            Enumerable.Range(0, 20).Select(_ => service.GetTopAsync()));

        results.Should().OnlyContain(result => ReferenceEquals(result, topEvents));
        repository.GetTopCalls.Should().Be(1);
        cache.SetTopEventsCalls.Should().Be(1);
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
        var startAt = TestNow.AddDays(30);

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
        private int getByIdCalls;
        private int getTopCalls;
        private int removeCalls;
        private int saveChangesCalls;
        public TimeSpan Delay { get; init; }
        public int GetByIdCalls => getByIdCalls;
        public int GetTopCalls => getTopCalls;
        public int LastTopCount { get; private set; }
        public int RemoveCalls => removeCalls;
        public int SaveChangesCalls => saveChangesCalls;

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
            Interlocked.Increment(ref getByIdCalls);
            operations?.Add($"repo:get:{id}");
            if (Delay > TimeSpan.Zero)
                return GetByIdWithDelayAsync(id, cancellationToken);

            return Task.FromResult(EventById?.Id == id ? EventById : null);
        }

        public Task<IReadOnlyCollection<Event>> GetTopBySoldPercentageAsync(
            int count,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref getTopCalls);
            LastTopCount = count;
            operations?.Add($"repo:get-top:{count}");
            if (Delay > TimeSpan.Zero)
                return GetTopWithDelayAsync(cancellationToken);

            return Task.FromResult(TopEvents);
        }

        public Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<bool> RemoveAsync(int id, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref removeCalls);
            operations?.Add($"repo:remove:{id}");
            return Task.FromResult(RemoveResult);
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref saveChangesCalls);
            operations?.Add("repo:save");
            return Task.CompletedTask;
        }

        private async Task<Event?> GetByIdWithDelayAsync(int id, CancellationToken cancellationToken)
        {
            await Task.Delay(Delay, cancellationToken);
            return EventById?.Id == id ? EventById : null;
        }

        private async Task<IReadOnlyCollection<Event>> GetTopWithDelayAsync(CancellationToken cancellationToken)
        {
            await Task.Delay(Delay, cancellationToken);
            return TopEvents;
        }
    }

    private sealed class FakeEventReadCache(List<string>? operations = null) : IEventReadCache
    {
        public ConcurrentDictionary<int, Event> Events { get; } = [];
        public IReadOnlyCollection<Event>? CachedTopEvents { get; set; }
        public List<Event> SavedEvents { get; } = [];
        public IReadOnlyCollection<Event>? SavedTopEvents { get; private set; }
        public List<int> RemovedEventIds { get; } = [];
        private int getEventCalls;
        private int setEventCalls;
        private int removeEventCalls;
        private int getTopEventsCalls;
        private int setTopEventsCalls;
        public int GetEventCalls => getEventCalls;
        public int SetEventCalls => setEventCalls;
        public int RemoveEventCalls => removeEventCalls;
        public int GetTopEventsCalls => getTopEventsCalls;
        public int SetTopEventsCalls => setTopEventsCalls;

        public Task<Event?> GetEventAsync(int id, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref getEventCalls);
            operations?.Add($"cache:get:{id}");
            return Task.FromResult(Events.GetValueOrDefault(id));
        }

        public Task SetEventAsync(Event @event, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref setEventCalls);
            Events[@event.Id] = @event;
            SavedEvents.Add(@event);
            operations?.Add($"cache:set:{@event.Id}");
            return Task.CompletedTask;
        }

        public Task RemoveEventAsync(int id, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref removeEventCalls);
            RemovedEventIds.Add(id);
            Events.TryRemove(id, out _);
            operations?.Add($"cache:remove:{id}");
            return Task.CompletedTask;
        }

        public Task<IReadOnlyCollection<Event>?> GetTopEventsAsync(CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref getTopEventsCalls);
            operations?.Add("cache:get-top");
            return Task.FromResult(CachedTopEvents);
        }

        public Task SetTopEventsAsync(
            IReadOnlyCollection<Event> events,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref setTopEventsCalls);
            SavedTopEvents = events;
            CachedTopEvents = events;
            operations?.Add("cache:set-top");
            return Task.CompletedTask;
        }
    }
}
