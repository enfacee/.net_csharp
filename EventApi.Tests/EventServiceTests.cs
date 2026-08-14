using EventApi.Events.Application.Abstractions;
using EventApi.Events.Application.Caching;
using EventApi.Events.Application.DTO;
using EventApi.Events.Application.Options;
using EventApi.Events.Application.Services;
using EventApi.Events.Domain.Entities;
using EventApi.Events.Infrastructure.Persistence;
using EventApi.Events.Infrastructure.Persistence.Repositories;
using EventApi.Shared.Contracts;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace EventApi.Tests;

public sealed class EventServiceTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly FakeEventCache _cache = new();
    private readonly FakeEventSeatReservationPublisher _publisher = new();

    public EventServiceTests()
    {
        var services = new ServiceCollection();
        services.AddDbContext<EventsDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IEventCache>(_cache);
        services.AddSingleton(Options.Create(new EventCacheOptions
        {
            EventByIdTtlSeconds = 60,
            TopEventsTtlSeconds = 300
        }));
        services.AddSingleton<IEventReadCache, EventReadCache>();
        services.AddSingleton<IEventSeatReservationPublisher>(_publisher);
        services.AddScoped<IEventRepository, EventRepository>();
        services.AddScoped<IEventService, EventService>();
        services.AddScoped<IEventSeatReservationService, EventSeatReservationService>();

        _serviceProvider = services.BuildServiceProvider();
    }

    [Fact]
    public async Task CreateEventAsync_ShouldCreateEventWithAvailableSeats()
    {
        using var scope = _serviceProvider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IEventService>();
        var startAt = DateTime.UtcNow.AddDays(30);

        var @event = await service.CreateEventAsync(
            "Architecture review",
            null,
            startAt,
            startAt.AddHours(1),
            totalSeats: 15);

        var result = await service.GetByIdAsync(@event.Id);

        result.Should().NotBeNull();
        result!.Title.Should().Be("Architecture review");
        result.TotalSeats.Should().Be(15);
        result.AvailableSeats.Should().Be(15);
    }

    [Fact]
    public async Task GetAllAsync_ShouldFilterAndPaginateEvents()
    {
        using var scope = _serviceProvider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IEventService>();
        var from = DateTime.UtcNow.AddDays(10);
        var to = from.AddDays(2);

        await service.CreateEventAsync("Team sync", null, from, from.AddHours(1), 10);
        await service.CreateEventAsync("Client sync", null, from.AddDays(1), from.AddDays(1).AddHours(1), 10);
        await service.CreateEventAsync("Team retro", null, from.AddDays(3), from.AddDays(3).AddHours(1), 10);
        await service.CreateEventAsync("Workshop", null, from, from.AddHours(2), 10);

        var result = await service.GetAllAsync(title: "sync", from: from, to: to, page: 1, pageSize: 10);

        result.TotalCount.Should().Be(2);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(10);
        result.Items.Select(x => x.Title).Should().ContainInOrder("Team sync", "Client sync");
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReadFromCache_WhenCacheContainsEvent()
    {
        using var scope = _serviceProvider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IEventService>();
        var cachedEvent = Event.Rehydrate(
            id: 777,
            title: "Cached event",
            description: null,
            startAt: DateTime.UtcNow.AddDays(30),
            endAt: DateTime.UtcNow.AddDays(30).AddHours(1),
            totalSeats: 10,
            availableSeats: 4);
        await _cache.SetStringAsync(
            EventCacheKeys.EventById(cachedEvent.Id),
            System.Text.Json.JsonSerializer.Serialize(EventCacheItem.FromEvent(cachedEvent), new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)),
            TimeSpan.FromMinutes(1));

        var result = await service.GetByIdAsync(cachedEvent.Id);

        result.Should().NotBeNull();
        result!.Title.Should().Be("Cached event");
        _cache.GetCalls.Should().Be(1);
        _cache.SetCalls.Should().Be(1);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReadFromRepositoryAndSaveCache_WhenCacheMisses()
    {
        using var scope = _serviceProvider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IEventService>();
        var startAt = DateTime.UtcNow.AddDays(30);
        var @event = await service.CreateEventAsync("Cache miss event", null, startAt, startAt.AddHours(1), 10);
        _cache.Clear();

        var result = await service.GetByIdAsync(@event.Id);

        result.Should().NotBeNull();
        result!.Title.Should().Be("Cache miss event");
        _cache.GetCalls.Should().Be(1);
        _cache.SetCalls.Should().Be(1);
        _cache.Values.Should().ContainKey(EventCacheKeys.EventById(@event.Id));
    }

    [Fact]
    public async Task GetTopAsync_ShouldReturnTopTenEventsBySoldPercentageAndCacheResult()
    {
        using var scope = _serviceProvider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IEventService>();
        var startAt = DateTime.UtcNow.AddDays(30);

        for (var i = 0; i < 12; i++)
        {
            var @event = await service.CreateEventAsync($"Event {i}", null, startAt.AddDays(i), startAt.AddDays(i).AddHours(1), 10);
            if (i > 0)
                @event.TryReserveSeats(Math.Min(i, 10));
        }
        await scope.ServiceProvider.GetRequiredService<IEventRepository>().SaveChangesAsync();
        _cache.Clear();

        var result = await service.GetTopAsync();

        result.Should().HaveCount(10);
        result.First().AvailableSeats.Should().Be(0);
        _cache.Values.Should().ContainKey(EventCacheKeys.TopEvents);
    }

    [Fact]
    public async Task UpdateEventAsync_ShouldKeepReservedSeats_WhenTotalSeatsChanges()
    {
        using var scope = _serviceProvider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IEventService>();
        var startAt = DateTime.UtcNow.AddDays(30);
        var @event = await service.CreateEventAsync("Sprint planning", null, startAt, startAt.AddHours(1), 5);
        @event.TryReserveSeats(2);

        var updated = await service.UpdateEventAsync(
            @event.Id,
            new EventRequest
            {
                Title = "Updated sprint planning",
                Description = "Updated description",
                StartAt = startAt,
                EndAt = startAt.AddHours(2),
                TotalSeats = 7
            });

        var result = await service.GetByIdAsync(@event.Id);

        updated.Should().BeTrue();
        result.Should().NotBeNull();
        result!.Title.Should().Be("Updated sprint planning");
        result.TotalSeats.Should().Be(7);
        result.AvailableSeats.Should().Be(5);
        _cache.RemoveCalls.Should().Be(1);
        _cache.RemovedKeys.Should().Contain(EventCacheKeys.EventById(@event.Id));
    }

    [Fact]
    public async Task RemoveAsync_ShouldInvalidateEventCache_WhenEventExists()
    {
        using var scope = _serviceProvider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IEventService>();
        var startAt = DateTime.UtcNow.AddDays(30);
        var @event = await service.CreateEventAsync("To remove", null, startAt, startAt.AddHours(1), 5);
        _cache.Clear();

        var removed = await service.RemoveAsync(@event.Id);

        removed.Should().BeTrue();
        _cache.RemoveCalls.Should().Be(1);
        _cache.RemovedKeys.Should().Contain(EventCacheKeys.EventById(@event.Id));
    }

    [Fact]
    public async Task ReserveSeatsAsync_ShouldInvalidateEventCache_AfterSavingChanges()
    {
        using var scope = _serviceProvider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IEventService>();
        var startAt = DateTime.UtcNow.AddDays(30);
        var @event = await service.CreateEventAsync("Reserve cache", null, startAt, startAt.AddHours(1), 5);
        _cache.Clear();

        var reserved = await service.ReserveSeatsAsync(@event.Id, 1);

        reserved.Should().BeTrue();
        _cache.RemoveCalls.Should().Be(1);
        _cache.RemovedKeys.Should().Contain(EventCacheKeys.EventById(@event.Id));
    }

    [Fact]
    public async Task HandleBookingCreatedAsync_ShouldReserveSeatAndPublishReservedEvent()
    {
        using var scope = _serviceProvider.CreateScope();
        var eventService = scope.ServiceProvider.GetRequiredService<IEventService>();
        var reservationService = scope.ServiceProvider.GetRequiredService<IEventSeatReservationService>();
        var startAt = DateTime.UtcNow.AddDays(30);
        var @event = await eventService.CreateEventAsync("Kafka event", null, startAt, startAt.AddHours(1), 2);

        await reservationService.HandleBookingCreatedAsync(new BookingCreated(1, @event.Id, 10, 1, DateTime.UtcNow));

        var result = await eventService.GetByIdAsync(@event.Id);
        result!.AvailableSeats.Should().Be(1);
        _cache.RemovedKeys.Should().Contain(EventCacheKeys.EventById(@event.Id));
        _publisher.Reserved.Should().ContainSingle(message =>
            message.BookingId == 1 &&
            message.EventId == @event.Id &&
            message.UserId == 10 &&
            message.Seats == 1);
        _publisher.Unavailable.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleBookingCreatedAsync_ShouldPublishUnavailable_WhenEventAlreadyStarted()
    {
        using var scope = _serviceProvider.CreateScope();
        var eventRepository = scope.ServiceProvider.GetRequiredService<IEventRepository>();
        var reservationService = scope.ServiceProvider.GetRequiredService<IEventSeatReservationService>();
        var startAt = DateTime.UtcNow.AddHours(-2);
        var @event = new Event("Started event", null, startAt, startAt.AddHours(1), 2);
        await eventRepository.AddAsync(@event);

        await reservationService.HandleBookingCreatedAsync(new BookingCreated(1, @event.Id, 10, 1, DateTime.UtcNow));

        _publisher.Reserved.Should().BeEmpty();
        _publisher.Unavailable.Should().ContainSingle(message =>
            message.BookingId == 1 &&
            message.EventId == @event.Id &&
            message.Reason.Contains("already started"));
    }

    [Fact]
    public async Task GetAllAsync_ShouldThrowArgumentOutOfRangeException_WhenPageOrPageSizeIsInvalid()
    {
        using var scope = _serviceProvider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IEventService>();

        Func<Task> invalidPage = () => service.GetAllAsync(page: 0);
        Func<Task> invalidPageSize = () => service.GetAllAsync(pageSize: 0);

        await invalidPage.Should().ThrowAsync<ArgumentOutOfRangeException>()
            .WithMessage("*Page must be greater than 0*");
        await invalidPageSize.Should().ThrowAsync<ArgumentOutOfRangeException>()
            .WithMessage("*PageSize must be greater than 0*");
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
    }

    private sealed class FakeEventSeatReservationPublisher : IEventSeatReservationPublisher
    {
        public List<EventSeatReserved> Reserved { get; } = [];
        public List<EventSeatUnavailable> Unavailable { get; } = [];

        public Task PublishSeatReservedAsync(EventSeatReserved message, CancellationToken cancellationToken = default)
        {
            Reserved.Add(message);
            return Task.CompletedTask;
        }

        public Task PublishSeatUnavailableAsync(EventSeatUnavailable message, CancellationToken cancellationToken = default)
        {
            Unavailable.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeEventCache : IEventCache
    {
        public Dictionary<string, string> Values { get; } = [];
        public List<string> RemovedKeys { get; } = [];
        public int GetCalls { get; private set; }
        public int SetCalls { get; private set; }
        public int RemoveCalls { get; private set; }

        public Task<string?> GetStringAsync(string key, CancellationToken cancellationToken = default)
        {
            GetCalls++;
            return Task.FromResult(Values.GetValueOrDefault(key));
        }

        public Task SetStringAsync(
            string key,
            string value,
            TimeSpan timeToLive,
            CancellationToken cancellationToken = default)
        {
            SetCalls++;
            Values[key] = value;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            RemoveCalls++;
            RemovedKeys.Add(key);
            Values.Remove(key);
            return Task.CompletedTask;
        }

        public void Clear()
        {
            Values.Clear();
            RemovedKeys.Clear();
            GetCalls = 0;
            SetCalls = 0;
            RemoveCalls = 0;
        }
    }
}
