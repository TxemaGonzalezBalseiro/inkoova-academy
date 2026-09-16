using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Inkoova.Academy.Infrastructure.Identity;

public sealed record JwtOptions
{
    public required string Issuer { get; init; }

    public required string Audience { get; init; }

    /// <summary>At least 32 bytes. Validated at startup so a short key fails fast, not at first login.</summary>
    public required string SigningKey { get; init; }

    /// <summary>Short-lived by design (T-03): 15 minutes, refreshed silently.</summary>
    public int AccessTokenMinutes { get; init; } = 15;

    public int RefreshTokenDays { get; init; } = 30;
}

/// <summary>Issues access tokens and the opaque refresh tokens that rotate them.</summary>
public sealed class JwtTokenService(JwtOptions options)
{
    private readonly SigningCredentials _credentials = new(
        new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey)),
        SecurityAlgorithms.HmacSha256);

    public (string Token, DateTimeOffset ExpiresAt) IssueAccessToken(
        Guid userId,
        string email,
        IReadOnlyList<string> roles,
        string securityStamp,
        DateTimeOffset now)
    {
        var expires = now.AddMinutes(options.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            new(JwtRegisteredClaimNames.Jti, Guid.CreateVersion7().ToString()),
            // The stamp lets a password change invalidate tokens that have not expired yet.
            new("stamp", securityStamp)
        };

        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var token = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: options.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expires.UtcDateTime,
            signingCredentials: _credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }

    /// <summary>
    /// Opaque 256-bit random string. Refresh tokens are not JWTs on purpose: they must be
    /// revocable, and revoking a self-contained token requires a blocklist anyway.
    /// </summary>
    public static string GenerateRefreshToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

    /// <summary>Only the hash is stored, so a database dump does not hand over live sessions.</summary>
    public static string Hash(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    public TimeSpan RefreshLifetime => TimeSpan.FromDays(options.RefreshTokenDays);
}
