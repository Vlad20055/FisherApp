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

public class CachedCategoryRepository : ICategoryRepository
{
    private readonly CategoryRepository _inner;
    private readonly RedisCache _cache;
    private readonly MongoLogger _logger;

    public CachedCategoryRepository(CategoryRepository inner, RedisCache cache, MongoLogger logger)
    {
        _inner = inner;
        _cache = cache;
        _logger = logger;
    }

    private string AllKey() => "categories:all";
    private string NameKey(string name) => $"category:name:{name}";

    public async Task<Guid?> CreateAsync(Category entity, CancellationToken cancellationToken)
    {
        var id = await _inner.CreateAsync(entity, cancellationToken);
        await _cache.RemoveAsync(AllKey());
        await _logger.LogAsync("info", "system", $"Category created: {entity.Name}", eventType: "CREATE");
        return id;
    }

    public async Task<Guid?> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var cat = await _inner.ReadAsync(id, cancellationToken);
        var res = await _inner.DeleteAsync(id, cancellationToken);
        if (cat != null)
        {
            await _cache.RemoveAsync(AllKey());
            await _cache.RemoveAsync(NameKey(cat.Name));
            await _logger.LogAsync("info", "system", $"Category deleted: {cat.Name}", eventType: "DELETE");
        }
        return res;
    }

    public async Task<Category?> ReadAsync(Guid Id, CancellationToken cancellationToken)
    {
        return await _inner.ReadAsync(Id, cancellationToken);
    }

    public async Task<Guid?> UpdateAsync(Category entity, CancellationToken cancellationToken)
    {
        var res = await _inner.UpdateAsync(entity, cancellationToken);
        if (res != null)
        {
            await _cache.RemoveAsync(AllKey());
            await _cache.RemoveAsync(NameKey(entity.Name));
            await _logger.LogAsync("info", "system", $"Category updated: {entity.Name}", eventType: "UPDATE");
        }
        return res;
    }

    public async Task<Category?> GetByNameAsync(string name, CancellationToken cancellationToken)
    {
        var key = NameKey(name);
        var cached = await _cache.GetStringAsync(key);
        if (!string.IsNullOrEmpty(cached)) return JsonSerializer.Deserialize<Category>(cached);
        var r = await _inner.GetByNameAsync(name, cancellationToken);
        if (r != null)
        {
            await _cache.SetStringAsync(key, JsonSerializer.Serialize(r), TimeSpan.FromMinutes(30));
            await _logger.LogAsync("info", "system", $"PostgreSQL read category by name (cache miss) {name}", eventType: "READ");
        }
        return r;
    }

    public async Task<List<Category>> GetAllAsync(CancellationToken cancellationToken)
    {
        var key = AllKey();
        var cached = await _cache.GetStringAsync(key);
        if (!string.IsNullOrEmpty(cached)) return JsonSerializer.Deserialize<List<Category>>(cached) ?? new List<Category>();
        var list = await _inner.GetAllAsync(cancellationToken);
        await _cache.SetStringAsync(key, JsonSerializer.Serialize(list), TimeSpan.FromMinutes(30));
        await _logger.LogAsync("info", "system", "PostgreSQL read all categories (cache miss)", eventType: "READ");
        return list;
    }
}
