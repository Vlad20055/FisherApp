using System;
using System.Threading.Tasks;
using StackExchange.Redis;

namespace Infrastructure.NoSql;

public class RedisCache : IDisposable
{
    private readonly ConnectionMultiplexer _conn;
    private readonly IDatabase _db;

    public RedisCache(string connection)
    {
        _conn = ConnectionMultiplexer.Connect(connection);
        _db = _conn.GetDatabase();
    }

    public async Task<string?> GetStringAsync(string key)
    {
        return await _db.StringGetAsync(key);
    }

    public async Task SetStringAsync(string key, string value, TimeSpan? expiry = null)
    {
        await _db.StringSetAsync(key, value, expiry);
    }

    public async Task<long> IncrementAsync(string key, TimeSpan? expiry = null)
    {
        var v = await _db.StringIncrementAsync(key);
        if (expiry.HasValue)
            await _db.KeyExpireAsync(key, expiry);
        return v;
    }

    public async Task<bool> KeyExistsAsync(string key)
    {
        return await _db.KeyExistsAsync(key);
    }

    public async Task RemoveAsync(string key)
    {
        await _db.KeyDeleteAsync(key);
    }

    public ISubscriber GetSubscriber() => _conn.GetSubscriber();

    public async Task<long> PublishAsync(string channel, string message)
    {
        var sub = _conn.GetSubscriber();
        return await sub.PublishAsync(RedisChannel.Literal(channel), message);
    }

    public void Dispose()
    {
        _conn?.Dispose();
    }
}
