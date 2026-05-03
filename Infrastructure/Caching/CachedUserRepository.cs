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

public class CachedUserRepository : IUserRepository
{
    private readonly UserRepository _inner;
    private readonly RedisCache _cache;
    private readonly MongoLogger _logger;

    public CachedUserRepository(UserRepository inner, RedisCache cache, MongoLogger logger)
    {
        _inner = inner;
        _cache = cache;
        _logger = logger;
    }

    private string RoleListKey(Guid roleId) => $"users:role:{roleId}";
    private string UserKey(Guid id) => $"user:{id}";

    public async Task<Guid?> CreateAsync(User entity, CancellationToken cancellationToken)
    {
        var id = await _inner.CreateAsync(entity, cancellationToken);
        // invalidate relevant caches
        await _cache.RemoveAsync($"users:all");
        await _cache.RemoveAsync(RoleListKey(entity.RoleId));
        await _cache.PublishAsync("cache:invalidate", $"users:all");
        await _cache.PublishAsync("cache:invalidate", RoleListKey(entity.RoleId));
        await _logger.LogAsync("info", entity.Username, "User created", meta: null, eventType: "CREATE");
        return id;
    }

    public async Task<Guid?> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var u = await _inner.ReadAsync(id, cancellationToken);
        var res = await _inner.DeleteAsync(id, cancellationToken);
        if (u != null)
        {
            var userKey = UserKey(id);
            await _cache.RemoveAsync(userKey);
            await _cache.RemoveAsync($"users:all");
            await _cache.RemoveAsync(RoleListKey(u.RoleId));
            await _cache.PublishAsync("cache:invalidate", userKey);
            await _cache.PublishAsync("cache:invalidate", $"users:all");
            await _cache.PublishAsync("cache:invalidate", RoleListKey(u.RoleId));
            await _logger.LogAsync("info", u.Username, "User deleted", meta: null, eventType: "DELETE");
        }
        return res;
    }

    public async Task<User?> ReadAsync(Guid Id, CancellationToken cancellationToken)
    {
        var key = UserKey(Id);
        var cached = await _cache.GetStringAsync(key);
        if (!string.IsNullOrEmpty(cached))
        {
            return JsonSerializer.Deserialize<User>(cached);
        }
        var u = await _inner.ReadAsync(Id, cancellationToken);
        if (u != null)
        {
            await _cache.SetStringAsync(key, JsonSerializer.Serialize(u), TimeSpan.FromMinutes(10));
            await _logger.LogAsync("info", u.Username, "PostgreSQL read user by id (cache miss)", meta: new MongoDB.Bson.BsonDocument { ["id"] = Id.ToString() }, eventType: "READ");
        }
        return u;
    }

    public async Task<Guid?> UpdateAsync(User entity, CancellationToken cancellationToken)
    {
        var res = await _inner.UpdateAsync(entity, cancellationToken);
        if (res != null)
        {
            var userKey = UserKey(entity.Id);
            await _cache.RemoveAsync(userKey);
            await _cache.RemoveAsync($"users:all");
            await _cache.RemoveAsync(RoleListKey(entity.RoleId));
            await _cache.PublishAsync("cache:invalidate", userKey);
            await _cache.PublishAsync("cache:invalidate", $"users:all");
            await _cache.PublishAsync("cache:invalidate", RoleListKey(entity.RoleId));
            await _logger.LogAsync("info", entity.Username, "User updated", meta: null, eventType: "UPDATE");
        }
        return res;
    }

    public async Task<User?> AuthenticateAsync(string username, string password, CancellationToken cancellationToken)
    {
        // delegate to inner implementation (no caching for auth check)
        return await _inner.AuthenticateAsync(username, password, cancellationToken);
    }

    public async Task<Guid?> CreateWithPasswordAsync(User entity, string password, CancellationToken cancellationToken)
    {
        var res = await _inner.CreateWithPasswordAsync(entity, password, cancellationToken);
        if (res != null)
        {
            await _cache.RemoveAsync($"users:all");
            await _cache.RemoveAsync(RoleListKey(entity.RoleId));
            await _cache.PublishAsync("cache:invalidate", $"users:all");
            await _cache.PublishAsync("cache:invalidate", RoleListKey(entity.RoleId));
            await _logger.LogAsync("info", entity.Username, "User created with password", meta: null, eventType: "CREATE");
        }
        return res;
    }

    public async Task<List<User>> GetByRoleIdAsync(Guid roleId, CancellationToken cancellationToken)
    {
        var key = RoleListKey(roleId);
        var cached = await _cache.GetStringAsync(key);
        if (!string.IsNullOrEmpty(cached))
        {
            return JsonSerializer.Deserialize<List<User>>(cached) ?? new List<User>();
        }
        var list = await _inner.GetByRoleIdAsync(roleId, cancellationToken);
        await _cache.SetStringAsync(key, JsonSerializer.Serialize(list), TimeSpan.FromMinutes(5));
        await _logger.LogAsync("info", "system", $"PostgreSQL read users by role (cache miss) role={roleId}", meta: null, eventType: "READ");
        return list;
    }
}
