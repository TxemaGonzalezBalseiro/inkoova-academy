using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Inkoova.Academy.Domain.Common;

namespace Inkoova.Academy.Domain.ValueObjects;

/// <summary>
/// Public verification code, format INK-XXXX-XXXX. The alphabet excludes characters that
/// are misread when a code is copied from a printed PDF or dictated on a call.
/// </summary>
public sealed partial record CertificateCode
{
    /// <summary>Crockford-style alphabet: no I, L, O, U, 0 or 1.</summary>
    private const string Alphabet = "23456789ABCDEFGHJKMNPQRSTVWXYZ";

    public string Value { get; }

    private CertificateCode(string value) => Value = value;

    public static Result<CertificateCode, Error> Create(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Error.Validation("certificate_code.empty", "El código de certificado no puede estar vacío.");
        }

        var normalized = raw.Trim().ToUpperInvariant();

        if (!CodePattern().IsMatch(normalized))
        {
            return Error.Validation(
                "certificate_code.invalid",
                "El código debe tener el formato INK-XXXX-XXXX.");
        }

        return new CertificateCode(normalized);
    }

    /// <summary>Cryptographically random code. Uniqueness is enforced by a unique index.</summary>
    public static CertificateCode NewCode()
    {
        Span<char> buffer = stackalloc char[8];
        for (var i = 0; i < buffer.Length; i++)
        {
            buffer[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }

        return new CertificateCode($"INK-{new string(buffer[..4])}-{new string(buffer[4..])}");
    }

    public override string ToString() => Value;

    [GeneratedRegex("^INK-[23456789ABCDEFGHJKMNPQRSTVWXYZ]{4}-[23456789ABCDEFGHJKMNPQRSTVWXYZ]{4}$")]
    private static partial Regex CodePattern();
}
