using EventApi.Events.Application.Abstractions;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace EventApi.Events.Infrastructure.Caching;

public sealed class RedisEventCache(
    IConnectionMultiplexer connection,
    ILogger<RedisEventCache> logger) : IEventCache
{
    public async Task<string?> GetStringAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var value = await connection.GetDatabase().StringGetAsync(key);
            return value.HasValue ? value.ToString() : null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (IsRedisFailure(exception))
        {
            logger.LogWarning(exception, "Failed to read cache key {CacheKey}.", key);
            return null;
        }
    }

    public async Task SetStringAsync(
        string key,
        string value,
        TimeSpan timeToLive,
        CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            await connection.GetDatabase().StringSetAsync(key, value, timeToLive);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (IsRedisFailure(exception))
        {
            logger.LogWarning(exception, "Failed to write cache key {CacheKey}.", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            await connection.GetDatabase().KeyDeleteAsync(key);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (IsRedisFailure(exception))
        {
            logger.LogWarning(exception, "Failed to remove cache key {CacheKey}.", key);
        }
    }

    private static bool IsRedisFailure(Exception exception)
    {
        return exception is RedisException or TimeoutException or ObjectDisposedException;
    }
}
