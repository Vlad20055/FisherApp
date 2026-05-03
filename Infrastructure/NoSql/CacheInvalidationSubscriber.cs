using System;
using System.Threading.Tasks;
using StackExchange.Redis;

namespace Infrastructure.NoSql;

public class CacheInvalidationSubscriber : IDisposable
{
    private readonly RedisCache _redis;
    private readonly MongoLogger _logger;
    private readonly ISubscriber _subscriber;

    public CacheInvalidationSubscriber(RedisCache redis, MongoLogger logger)
    {
        _redis = redis;
        _logger = logger;
        _subscriber = _redis.GetSubscriber();
    }

    public Task StartAsync()
    {
        return _subscriber.SubscribeAsync(RedisChannel.Literal("cache:invalidate"), async (channel, message) =>
        {
            try
            {
                var key = message.ToString();
                // remove key just in case
                await _redis.RemoveAsync(key);
                await _logger.LogAsync("info", "system", $"Cache invalidation received for key {key}", eventType: "CACHE");
            }
            catch (Exception ex)
            {
                await _logger.LogAsync("error", "system", "Failed to handle cache:invalidate message", meta: new MongoDB.Bson.BsonDocument { ["error"] = ex.Message }, eventType: "ERROR");
            }
        });
    }

    public void Dispose()
    {
        // no explicit unsubscribe; ConnectionMultiplexer will be disposed elsewhere
    }
}
