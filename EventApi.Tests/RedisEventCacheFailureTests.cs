using EventApi.Events.Infrastructure.Caching;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;

namespace EventApi.Tests;

public sealed class RedisEventCacheFailureTests
{
    [Fact]
    public async Task GetStringAsync_ShouldReturnNull_WhenRedisConnectionFails()
    {
        await using var connection = await CreateBrokenConnectionAsync();
        var cache = CreateCache(connection);

        var result = await cache.GetStringAsync("missing");

        result.Should().BeNull();
    }

    [Fact]
    public async Task SetStringAsync_AndRemoveAsync_ShouldNotThrow_WhenRedisConnectionFails()
    {
        await using var connection = await CreateBrokenConnectionAsync();
        var cache = CreateCache(connection);

        Func<Task> action = async () =>
        {
            await cache.SetStringAsync("key", "value", TimeSpan.FromMinutes(1));
            await cache.RemoveAsync("key");
        };

        await action.Should().NotThrowAsync();
    }

    private static async Task<IConnectionMultiplexer> CreateBrokenConnectionAsync()
    {
        return await ConnectionMultiplexer.ConnectAsync(
            "localhost:6390,abortConnect=false,connectTimeout=500,syncTimeout=500");
    }

    private static RedisEventCache CreateCache(IConnectionMultiplexer connection)
    {
        return new RedisEventCache(connection, NullLogger<RedisEventCache>.Instance);
    }
}
