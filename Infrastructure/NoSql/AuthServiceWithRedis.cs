using System;
using System.IdentityModel.Tokens.Jwt;
using System.Threading;
using System.Threading.Tasks;
using Infrastructure.NoSql;
using Infrastructure.Security;
using Domain.Repositories;
using Domain.Entities;

namespace Infrastructure.NoSql;

public class AuthServiceWithRedis
{
    private readonly IUserRepository _userRepo;
    private readonly RedisCache _redis;
    private readonly JwtService _jwt;
    private readonly MongoLogger _logger;

    // TTLs
    private readonly TimeSpan _accessTokenTtl = TimeSpan.FromHours(1);
    private readonly TimeSpan _refreshTokenTtl = TimeSpan.FromDays(7);

    public AuthServiceWithRedis(IUserRepository userRepo, RedisCache redis, JwtService jwt, MongoLogger logger)
    {
        _userRepo = userRepo;
        _redis = redis;
        _jwt = jwt;
        _logger = logger;
    }

    private string BlacklistKey(string username) => $"auth:blacklist:{username}";
    private string FailedCountKey(string username) => $"auth:fail:{username}";
    private string SessionKey(string token) => $"session:{token}";
    private string JtiBlacklistKey(string jti) => $"auth:blk:jti:{jti}";
    private string RefreshKey(string refreshToken) => $"refresh:{refreshToken}";
    private string UserRefreshListKey(Guid userId) => $"refreshs:user:{userId}";

    public async Task<(bool success, string? accessToken, string? refreshToken, User? user, string? message)> AuthenticateAsync(string username, string password, CancellationToken ct)
    {
        // check blacklist
        if (await _redis.KeyExistsAsync(BlacklistKey(username)))
        {
            await _logger.LogAsync("warn", username, "Login attempt while blacklisted", eventType: "AUTH");
            return (false, null, null, null, "User is temporarily locked due to multiple failed login attempts. Try later.");
        }

        var user = await _userRepo.AuthenticateAsync(username, password, ct);
        if (user == null)
        {
            // increment fail count
            var fails = await _redis.IncrementAsync(FailedCountKey(username), TimeSpan.FromMinutes(10));
            await _logger.LogAsync("warn", username, $"Failed login attempt #{fails}", eventType: "AUTH");
            if (fails >= 3)
            {
                // add to blacklist for 10 minutes
                await _redis.SetStringAsync(BlacklistKey(username), "1", TimeSpan.FromMinutes(10));
                // reset fail counter
                await _redis.RemoveAsync(FailedCountKey(username));
                await _logger.LogAsync("warn", username, "User blacklisted due to multiple failed attempts", eventType: "AUTH");
            }
            return (false, null, null, null, "Invalid credentials");
        }

        // successful login: clear fail counter
        await _redis.RemoveAsync(FailedCountKey(username));
        await _logger.LogAsync("info", username, "User authenticated", eventType: "AUTH");

        // generate access token
        var accessToken = _jwt.GenerateToken(user.Id, user.RoleId, user.Username);

        // store access session
        await _redis.SetStringAsync(SessionKey(accessToken), user.Id.ToString(), _accessTokenTtl);

        // store jti mapping for token expiry handling
        var (jti, expiry) = _jwt.GetJtiAndExpiry(accessToken);
        if (!string.IsNullOrEmpty(jti) && expiry.HasValue)
        {
            await _redis.SetStringAsync($"jti:{jti}", accessToken, expiry.Value - DateTime.UtcNow);
        }

        // create refresh token
        var refreshToken = Guid.NewGuid().ToString("N");
        await _redis.SetStringAsync(RefreshKey(refreshToken), user.Id.ToString(), _refreshTokenTtl);
        // add refresh to user's list (store as simple set member by storing key)
        await _redis.SetStringAsync($"{UserRefreshListKey(user.Id)}:{refreshToken}", "1", _refreshTokenTtl);

        await _logger.LogAsync("info", username, "Session created in Redis", meta: new MongoDB.Bson.BsonDocument { ["hasRefresh"] = true }, eventType: "SESSION");

        return (true, accessToken, refreshToken, user, null);
    }

    public async Task<(bool success, string? accessToken, string? refreshToken, string? message)> RefreshAsync(string refreshToken)
    {
        if (string.IsNullOrEmpty(refreshToken)) return (false, null, null, "Invalid refresh token");
        var uidStr = await _redis.GetStringAsync(RefreshKey(refreshToken));
        if (string.IsNullOrEmpty(uidStr)) return (false, null, null, "Refresh token expired or invalid");

        if (!Guid.TryParse(uidStr, out var userId)) return (false, null, null, "Invalid user id stored with refresh token");

        // issue new access token
        // load user minimal info
        var user = await _userRepo.ReadAsync(userId, CancellationToken.None);
        if (user == null) return (false, null, null, "User not found");

        var accessToken = _jwt.GenerateToken(user.Id, user.RoleId, user.Username);
        await _redis.SetStringAsync(SessionKey(accessToken), user.Id.ToString(), _accessTokenTtl);
        var (jti, expiry) = _jwt.GetJtiAndExpiry(accessToken);
        if (!string.IsNullOrEmpty(jti) && expiry.HasValue)
        {
            await _redis.SetStringAsync($"jti:{jti}", accessToken, expiry.Value - DateTime.UtcNow);
        }

        // rotate refresh token: revoke old and issue new
        await _redis.RemoveAsync(RefreshKey(refreshToken));
        await _redis.RemoveAsync($"{UserRefreshListKey(user.Id)}:{refreshToken}");
        var newRefresh = Guid.NewGuid().ToString("N");
        await _redis.SetStringAsync(RefreshKey(newRefresh), user.Id.ToString(), _refreshTokenTtl);
        await _redis.SetStringAsync($"{UserRefreshListKey(user.Id)}:{newRefresh}", "1", _refreshTokenTtl);

        await _logger.LogAsync("info", user.Username, "Refresh token used/rotated", meta: new MongoDB.Bson.BsonDocument { ["rotated"] = true }, eventType: "AUTH");

        return (true, accessToken, newRefresh, null);
    }

    public async Task LogoutAsync(string accessToken, string? refreshToken = null)
    {
        // remove access session and blacklist jti
        await _redis.RemoveAsync(SessionKey(accessToken));
        var (jti, expiry) = _jwt.GetJtiAndExpiry(accessToken);
        if (!string.IsNullOrEmpty(jti) && expiry.HasValue)
        {
            var ttl = expiry.Value - DateTime.UtcNow;
            if (ttl > TimeSpan.Zero)
                await _redis.SetStringAsync(JtiBlacklistKey(jti), "1", ttl);
        }

        if (!string.IsNullOrEmpty(refreshToken))
        {
            var uidStr = await _redis.GetStringAsync(RefreshKey(refreshToken));
            if (!string.IsNullOrEmpty(uidStr) && Guid.TryParse(uidStr, out var uid))
            {
                await _redis.RemoveAsync($"{UserRefreshListKey(uid)}:{refreshToken}");
            }
            await _redis.RemoveAsync(RefreshKey(refreshToken));
        }

        await _logger.LogAsync("info", "system", "User logged out", meta: new MongoDB.Bson.BsonDocument { ["hadRefresh"] = !string.IsNullOrEmpty(refreshToken) }, eventType: "AUTH");
    }

    public async Task RevokeAllRefreshTokensForUser(Guid userId)
    {
        // naive approach: remove keys with prefix (requires server-side scan in prod)
        // here we assume stored keys format: UserRefreshListKey:userId:refresh
        // We don't have list of tokens — in redis you may maintain a set. For simplicity, set a revocation flag for user.
        await _redis.SetStringAsync($"refreshs:revoked:{userId}", "1", _refreshTokenTtl);
        await _logger.LogAsync("info", "system", $"All refresh tokens revoked for user {userId}", eventType: "AUTH");
    }

    public async Task<bool> IsTokenValidAsync(string token)
    {
        var pr = _jwt.ValidateToken(token);
        if (pr == null) return false;
        // check blacklist by username claim
        var username = pr.FindFirst("username")?.Value;
        if (username != null && await _redis.KeyExistsAsync(BlacklistKey(username))) return false;
        // check jti blacklist
        var jti = pr.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
        if (!string.IsNullOrEmpty(jti) && await _redis.KeyExistsAsync(JtiBlacklistKey(jti))) return false;
        // check session exists
        return await _redis.KeyExistsAsync(SessionKey(token));
    }
}
