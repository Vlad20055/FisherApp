using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Domain.Entities;
using Domain.Repositories;
using Infrastructure.NoSql;
using Infrastructure.Repositories;

namespace Infrastructure.Caching;

public class CachedOrderRepository : IOrderRepository
{
    private readonly OrderRepository _inner;
    private readonly RedisCache _cache;
    private readonly MongoLogger _logger;

    public CachedOrderRepository(OrderRepository inner, RedisCache cache, MongoLogger logger)
    {
        _inner = inner;
        _cache = cache;
        _logger = logger;
    }

    private string ByIdKey(Guid id) => $"order:{id}";
    private string ByStoreKey(Guid storeId) => $"orders:store:{storeId}";

    public async Task<Guid?> CreateAsync(Order entity, CancellationToken cancellationToken)
    {
        var id = await _inner.CreateAsync(entity, cancellationToken);
        if (id != null)
        {
            await _cache.RemoveAsync(ByStoreKey(entity.StoreId));
            await _cache.PublishAsync("cache:invalidate", ByStoreKey(entity.StoreId));
            await _logger.LogAsync("info", "system", $"Order created for store {entity.StoreId}", eventType: "CREATE");
        }
        return id;
    }

    public async Task<Guid?> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var o = await _inner.ReadAsync(id, cancellationToken);
        var res = await _inner.DeleteAsync(id, cancellationToken);
        if (o != null)
        {
            await _cache.RemoveAsync(ByIdKey(id));
            await _cache.RemoveAsync(ByStoreKey(o.StoreId));
            await _cache.PublishAsync("cache:invalidate", ByIdKey(id));
            await _cache.PublishAsync("cache:invalidate", ByStoreKey(o.StoreId));
            await _logger.LogAsync("info", "system", $"Order deleted {id}", eventType: "DELETE");
        }
        return res;
    }

    public async Task<Order?> ReadAsync(Guid Id, CancellationToken cancellationToken)
    {
        var key = ByIdKey(Id);
        var cached = await _cache.GetStringAsync(key);
        if (!string.IsNullOrEmpty(cached)) return JsonSerializer.Deserialize<Order>(cached);
        var o = await _inner.ReadAsync(Id, cancellationToken);
        if (o != null)
        {
            await _cache.SetStringAsync(key, JsonSerializer.Serialize(o), TimeSpan.FromMinutes(10));
            await _logger.LogAsync("info", "system", $"PostgreSQL read order by id (cache miss) {Id}", eventType: "READ");
        }
        return o;
    }

    public async Task<Guid?> UpdateAsync(Order entity, CancellationToken cancellationToken)
    {
        var res = await _inner.UpdateAsync(entity, cancellationToken);
        if (res != null)
        {
            await _cache.RemoveAsync(ByIdKey(entity.Id));
            await _cache.RemoveAsync(ByStoreKey(entity.StoreId));
            await _cache.PublishAsync("cache:invalidate", ByIdKey(entity.Id));
            await _cache.PublishAsync("cache:invalidate", ByStoreKey(entity.StoreId));
            await _logger.LogAsync("info", "system", $"Order updated {entity.Id}", eventType: "UPDATE");
        }
        return res;
    }

    public async Task<List<Order>> GetByStoreIdAsync(Guid storeId, CancellationToken cancellationToken)
    {
        var key = ByStoreKey(storeId);
        var cached = await _cache.GetStringAsync(key);
        if (!string.IsNullOrEmpty(cached)) return JsonSerializer.Deserialize<List<Order>>(cached) ?? new List<Order>();
        var list = await _inner.GetByStoreIdAsync(storeId, cancellationToken);
        await _cache.SetStringAsync(key, JsonSerializer.Serialize(list), TimeSpan.FromMinutes(10));
        await _logger.LogAsync("info", "system", $"PostgreSQL read orders by store (cache miss) {storeId}", eventType: "READ");
        return list;
    }

    public async Task<List<Order>> GetAllAsync(CancellationToken cancellationToken)
    {
        // delegate to inner
        return await _inner.GetAllAsync(cancellationToken);
    }
}
