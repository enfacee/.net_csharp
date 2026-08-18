using EventApi.Events.Application.Caching;
using EventApi.Events.Application.Options;
using EventApi.Events.Domain.Entities;
using EventApi.Events.Infrastructure.Caching;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Testcontainers.Redis;

namespace EventApi.IntegrationTests;

public sealed class RedisEventCacheTests : IAsyncLifetime
{
    private readonly RedisContainer container = new RedisBuilder("redis:7.4-alpine")
        .Build();

    private IConnectionMultiplexer connection = null!;

    public async Task InitializeAsync()
    {
        await container.StartAsync();
        connection = await ConnectionMultiplexer.ConnectAsync(container.GetConnectionString());
    }

    public async Task DisposeAsync()
    {
        await connection.DisposeAsync();
        await container.DisposeAsync();
    }

    [Fact]
    public async Task SetStringAsync_ShouldWriteValueWithTtl()
    {
        var cache = CreateCache();

        await cache.SetStringAsync("test:key", "cached value", TimeSpan.FromMinutes(5));

        var database = connection.GetDatabase();
        Assert.Equal("cached value", await database.StringGetAsync("test:key"));

        var ttl = await database.KeyTimeToLiveAsync("test:key");
        Assert.NotNull(ttl);
        Assert.True(ttl.Value > TimeSpan.Zero);
        Assert.True(ttl.Value <= TimeSpan.FromMinutes(5));
    }

    [Fact]
    public async Task EventReadCache_ShouldSerializeAndDeserializeEvent()
    {
        var eventCache = new EventReadCache(
            CreateCache(),
            Options.Create(new EventCacheOptions
            {
                EventByIdTtlSeconds = 60,
                TopEventsTtlSeconds = 300
            }));
        var @event = Event.Rehydrate(
            id: 10,
            title: "Redis round-trip",
            description: "serialized event",
            startAt: UtcDate(2030, 1, 1),
            endAt: UtcDate(2030, 1, 1, 11),
            totalSeats: 20,
            availableSeats: 7);

        await eventCache.SetEventAsync(@event);

        var result = await eventCache.GetEventAsync(@event.Id);

        Assert.NotNull(result);
        Assert.Equal(@event.Id, result.Id);
        Assert.Equal(@event.Title, result.Title);
        Assert.Equal(@event.Description, result.Description);
        Assert.Equal(@event.StartAt, result.StartAt);
        Assert.Equal(@event.EndAt, result.EndAt);
        Assert.Equal(@event.TotalSeats, result.TotalSeats);
        Assert.Equal(@event.AvailableSeats, result.AvailableSeats);
    }

    private RedisEventCache CreateCache()
    {
        return new RedisEventCache(connection, NullLogger<RedisEventCache>.Instance);
    }

    private static DateTime UtcDate(int year, int month, int day, int hour = 10)
    {
        return new DateTime(year, month, day, hour, 0, 0, DateTimeKind.Utc);
    }
}
