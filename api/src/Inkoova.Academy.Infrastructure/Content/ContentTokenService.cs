using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Inkoova.Academy.Application.Abstractions;
using Inkoova.Academy.Domain.Common;

namespace Inkoova.Academy.Infrastructure.Content;

public sealed record ContentTokenOptions
{
    /// <summary>HMAC key. Rotating it invalidates every outstanding token, which is the point.</summary>
    public required string SigningKey { get; init; }
}

/// <summary>
/// Mints the opaque token that stands in for a content path (ADR-003). Deliberately not a
/// JWT: there are no claims to carry beyond a path and an expiry, and a compact custom
/// format keeps the URL short enough to sit in an iframe src without truncation surprises.
/// Format: base64url(expiryUnixSeconds "|" contentRef) "." base64url(HMACSHA256(payload)).
/// </summary>
public sealed class ContentTokenService(ContentTokenOptions options) : IContentTokenService
{
    public string Issue(string contentRef, TimeSpan lifetime)
    {
        var expiry = DateTimeOffset.UtcNow.Add(lifetime).ToUnixTimeSeconds();
        var payload = $"{expiry}|{contentRef}";
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        var signature = Sign(payloadBytes);

        return $"{Base64Url.EncodeToString(payloadBytes)}.{Base64Url.EncodeToString(signature)}";
    }

    public Result<string, Error> Validate(string token)
    {
        var separator = token.IndexOf('.', StringComparison.Ordinal);
        if (separator <= 0 || separator == token.Length - 1)
        {
            return Error.Forbidden("content_token.malformed", "Token de contenido inválido.");
        }

        byte[] payloadBytes;
        byte[] signature;

        try
        {
            payloadBytes = Base64Url.DecodeFromChars(token.AsSpan(0, separator));
            signature = Base64Url.DecodeFromChars(token.AsSpan(separator + 1));
        }
        catch (FormatException)
        {
            return Error.Forbidden("content_token.malformed", "Token de contenido inválido.");
        }

        // Constant time: a timing oracle here would let an attacker forge a token offline.
        if (!CryptographicOperations.FixedTimeEquals(signature, Sign(payloadBytes)))
        {
            return Error.Forbidden("content_token.bad_signature", "Token de contenido inválido.");
        }

        var payload = Encoding.UTF8.GetString(payloadBytes);
        var pipe = payload.IndexOf('|', StringComparison.Ordinal);
        if (pipe <= 0)
        {
            return Error.Forbidden("content_token.malformed", "Token de contenido inválido.");
        }

        if (!long.TryParse(payload.AsSpan(0, pipe), out var expiry))
        {
            return Error.Forbidden("content_token.malformed", "Token de contenido inválido.");
        }

        if (DateTimeOffset.FromUnixTimeSeconds(expiry) < DateTimeOffset.UtcNow)
        {
            // The player treats this as its cue to ask for a fresh token (T-07).
            return Error.Forbidden("content_token.expired", "El acceso al contenido ha caducado.");
        }

        return payload[(pipe + 1)..];
    }

    private byte[] Sign(byte[] payload) =>
        HMACSHA256.HashData(Encoding.UTF8.GetBytes(options.SigningKey), payload);
}
