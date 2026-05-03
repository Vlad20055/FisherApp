using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Infrastructure.Security;

public class JwtService
{
    private readonly byte[] _key;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly TimeSpan _expiry;

    public JwtService(string secretKey, string issuer = "fisher", string audience = "fisher_users", TimeSpan? expiry = null)
    {
        // HS256 requires >= 256 bits; derive fixed 256-bit key from any UTF-8 secret
        _key = SHA256.HashData(Encoding.UTF8.GetBytes(secretKey ?? string.Empty));
        _issuer = issuer;
        _audience = audience;
        _expiry = expiry ?? TimeSpan.FromHours(1);
    }

    public string GenerateToken(Guid userId, Guid roleId, string username)
    {
        var now = DateTime.UtcNow;
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim("roleId", roleId.ToString()),
            new Claim("username", username),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var credentials = new SigningCredentials(new SymmetricSecurityKey(_key), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(_issuer, _audience, claims, notBefore: now, expires: now.Add(_expiry), signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public ClaimsPrincipal? ValidateToken(string token)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        try
        {
            var principal = tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = _issuer,
                ValidateAudience = true,
                ValidAudience = _audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(_key),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30)
            }, out var validatedToken);
            return principal;
        }
        catch
        {
            return null;
        }
    }

    public (string? jti, DateTime? expiry) GetJtiAndExpiry(string token)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(token);
            var jti = jwt.Id; // Jti claim
            var exp = jwt.ValidTo; // UTC
            return (jti, exp);
        }
        catch
        {
            return (null, null);
        }
    }
}
