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

public class CachedProductRepository : IProductRepository
{
    private readonly ProductRepository _inner;
    private readonly RedisCache _cache;
    private readonly MongoLogger _logger;

    public CachedProductRepository(ProductRepository inner, RedisCache cache, MongoLogger logger)
    {
        _inner = inner;
        _cache = cache;
        _logger = logger;
    }

    private string KeyById(Guid id) => $"product:{id}";
    private string KeyByName(string name) => $"product:name:{name.ToLowerInvariant()}";
    private string AllKey() => "products:all";

    public async Task<Guid?> CreateAsync(Product entity, CancellationToken cancellationToken)
    {
        var id = await _inner.CreateAsync(entity, cancellationToken);
        if (id != null)
        {
            await _cache.RemoveAsync(AllKey());
            await _cache.PublishAsync("cache:invalidate", AllKey());
            await _logger.LogAsync("info", "system", $"Product created: {entity.Name}", eventType: "CREATE");
        }
        return id;
    }

    public async Task<Guid?> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var p = await _inner.ReadAsync(id, cancellationToken);
        var res = await _inner.DeleteAsync(id, cancellationToken);
        if (p != null)
        {
            await _cache.RemoveAsync(KeyById(id));
            await _cache.RemoveAsync(KeyByName(p.Name));
            await _cache.RemoveAsync(AllKey());
            await _cache.PublishAsync("cache:invalidate", KeyById(id));
            await _cache.PublishAsync("cache:invalidate", KeyByName(p.Name));
            await _cache.PublishAsync("cache:invalidate", AllKey());
            await _logger.LogAsync("info", "system", $"Product deleted: {p.Name}", eventType: "DELETE");
        }
        return res;
    }

    public async Task<Product?> ReadAsync(Guid Id, CancellationToken cancellationToken)
    {
        var key = KeyById(Id);
        var cached = await _cache.GetStringAsync(key);
        if (!string.IsNullOrEmpty(cached)) return JsonSerializer.Deserialize<Product>(cached);
        var p = await _inner.ReadAsync(Id, cancellationToken);
        if (p != null)
        {
            await _cache.SetStringAsync(key, JsonSerializer.Serialize(p), TimeSpan.FromMinutes(30));
            await _logger.LogAsync("info", "system", $"PostgreSQL read product by id (cache miss) {Id}", eventType: "READ");
        }
        return p;
    }

    public async Task<Guid?> UpdateAsync(Product entity, CancellationToken cancellationToken)
    {
        var res = await _inner.UpdateAsync(entity, cancellationToken);
        if (res != null)
        {
            await _cache.RemoveAsync(KeyById(entity.Id));
            await _cache.RemoveAsync(KeyByName(entity.Name));
            await _cache.RemoveAsync(AllKey());
            await _cache.PublishAsync("cache:invalidate", KeyById(entity.Id));
            await _cache.PublishAsync("cache:invalidate", KeyByName(entity.Name));
            await _cache.PublishAsync("cache:invalidate", AllKey());
            await _logger.LogAsync("info", "system", $"Product updated: {entity.Name}", eventType: "UPDATE");
        }
        return res;
    }

    public async Task<Product?> GetByNameAsync(string name, CancellationToken cancellationToken)
    {
        var key = KeyByName(name);
        var cached = await _cache.GetStringAsync(key);
        if (!string.IsNullOrEmpty(cached)) return JsonSerializer.Deserialize<Product>(cached);
        var p = await _inner.GetByNameAsync(name, cancellationToken);
        if (p != null)
        {
            await _cache.SetStringAsync(key, JsonSerializer.Serialize(p), TimeSpan.FromMinutes(30));
            await _logger.LogAsync("info", "system", $"PostgreSQL read product by name (cache miss) {name}", eventType: "READ");
        }
        return p;
    }

    public async Task<List<Product>> GetAllAsync(CancellationToken cancellationToken)
    {
        var key = AllKey();
        var cached = await _cache.GetStringAsync(key);
        if (!string.IsNullOrEmpty(cached)) return JsonSerializer.Deserialize<List<Product>>(cached) ?? new List<Product>();
        var list = await _inner.GetAllAsync(cancellationToken);
        await _cache.SetStringAsync(key, JsonSerializer.Serialize(list), TimeSpan.FromMinutes(30));
        await _logger.LogAsync("info", "system", "PostgreSQL read all products (cache miss)", eventType: "READ");
        return list;
    }
}
