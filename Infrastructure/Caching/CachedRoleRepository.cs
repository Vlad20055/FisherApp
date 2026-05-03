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

public class CachedRoleRepository : IRoleRepository
{
    private readonly RoleRepository _inner;
    private readonly RedisCache _cache;
    private readonly MongoLogger _logger;

    public CachedRoleRepository(RoleRepository inner, RedisCache cache, MongoLogger logger)
    {
        _inner = inner;
        _cache = cache;
        _logger = logger;
    }

    private string AllRolesKey() => "roles:all";
    private string RoleNameKey(string name) => $"role:name:{name}";

    public async Task<Guid?> CreateAsync(Role entity, CancellationToken cancellationToken)
    {
        var id = await _inner.CreateAsync(entity, cancellationToken);
        await _cache.RemoveAsync(AllRolesKey());
        await _cache.PublishAsync("cache:invalidate", AllRolesKey());
        await _logger.LogAsync("info", "system", $"Role created: {entity.Name}", eventType: "CREATE");
        return id;
    }

    public async Task<Guid?> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var role = await _inner.ReadAsync(id, cancellationToken);
        var res = await _inner.DeleteAsync(id, cancellationToken);
        if (role != null)
        {
            await _cache.RemoveAsync(AllRolesKey());
            await _cache.RemoveAsync(RoleNameKey(role.Name));
            await _cache.PublishAsync("cache:invalidate", AllRolesKey());
            await _cache.PublishAsync("cache:invalidate", RoleNameKey(role.Name));
            await _logger.LogAsync("info", "system", $"Role deleted: {role.Name}", eventType: "DELETE");
        }
        return res;
    }

    public async Task<Role?> ReadAsync(Guid Id, CancellationToken cancellationToken)
    {
        return await _inner.ReadAsync(Id, cancellationToken);
    }

    public async Task<Guid?> UpdateAsync(Role entity, CancellationToken cancellationToken)
    {
        var res = await _inner.UpdateAsync(entity, cancellationToken);
        if (res != null)
        {
            await _cache.RemoveAsync(AllRolesKey());
            await _cache.RemoveAsync(RoleNameKey(entity.Name));
            await _cache.PublishAsync("cache:invalidate", AllRolesKey());
            await _cache.PublishAsync("cache:invalidate", RoleNameKey(entity.Name));
            await _logger.LogAsync("info", "system", $"Role updated: {entity.Name}", eventType: "UPDATE");
        }
        return res;
    }

    public async Task<Role?> GetByNameAsync(string name, CancellationToken cancellationToken)
    {
        var key = RoleNameKey(name);
        var cached = await _cache.GetStringAsync(key);
        if (!string.IsNullOrEmpty(cached)) return JsonSerializer.Deserialize<Role>(cached);
        var r = await _inner.GetByNameAsync(name, cancellationToken);
        if (r != null)
        {
            await _cache.SetStringAsync(key, JsonSerializer.Serialize(r), TimeSpan.FromMinutes(10));
            await _logger.LogAsync("info", "system", $"PostgreSQL read role by name (cache miss) {name}", eventType: "READ");
        }
        return r;
    }

    public async Task<List<Role>> GetAllAsync(CancellationToken cancellationToken)
    {
        var key = AllRolesKey();
        var cached = await _cache.GetStringAsync(key);
        if (!string.IsNullOrEmpty(cached)) return JsonSerializer.Deserialize<List<Role>>(cached) ?? new List<Role>();
        var list = await _inner.GetAllAsync(cancellationToken);
        await _cache.SetStringAsync(key, JsonSerializer.Serialize(list), TimeSpan.FromMinutes(10));
        await _logger.LogAsync("info", "system", "PostgreSQL read all roles (cache miss)", eventType: "READ");
        return list;
    }
}
