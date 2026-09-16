using System.Security.Cryptography;

namespace Inkoova.Academy.Api.Common;

/// <summary>
/// First-party visitor id used for affiliate attribution and for claiming an anonymous quiz
/// attempt. It is a random opaque value with no personal data in it, set as a strictly
/// necessary cookie so it does not depend on analytics consent (T-14).
/// </summary>
public static class VisitorCookie
{
    public const string Name = "ink_vid";

    /// <summary>Matches the 30-day attribution window of a referral (T-16).</summary>
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(30);

    public static string GetOrCreate(HttpContext context)
    {
        if (context.Request.Cookies.TryGetValue(Name, out var existing) && IsWellFormed(existing))
        {
            return existing;
        }

        var value = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));

        context.Response.Cookies.Append(Name, value, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            MaxAge = Lifetime,
            IsEssential = true,
            Path = "/"
        });

        return value;
    }

    public static string? Read(HttpContext context) =>
        context.Request.Cookies.TryGetValue(Name, out var value) && IsWellFormed(value) ? value : null;

    /// <summary>32 lowercase hex characters. Anything else is treated as absent.</summary>
    private static bool IsWellFormed(string? value) =>
        value is { Length: 32 } && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
}
