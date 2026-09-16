using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Inkoova.Academy.Domain.Common;
using Inkoova.Academy.Domain.ValueObjects;

namespace Inkoova.Academy.Domain.Learning;

public enum CertificateScope
{
    Course,
    Program
}

/// <summary>
/// Public, verifiable proof of completion. The hash is reproducible from the same inputs,
/// so a certificate can be re-derived and compared without trusting the stored row (T-10).
/// </summary>
public sealed class Certificate
{
    public Guid Id { get; private set; }
    public CertificateCode Code { get; private set; }
    public CertificateScope Scope { get; private set; }
    public Guid UserId { get; private set; }

    /// <summary>Course or program the certificate refers to, depending on <see cref="Scope"/>.</summary>
    public Guid SubjectId { get; private set; }

    public DateTimeOffset IssuedAt { get; private set; }
    public string Hash { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public string? RevocationReason { get; private set; }

    private Certificate(
        Guid id,
        CertificateCode code,
        CertificateScope scope,
        Guid userId,
        Guid subjectId,
        DateTimeOffset issuedAt,
        string hash,
        DateTimeOffset? revokedAt,
        string? revocationReason)
    {
        Id = id;
        Code = code;
        Scope = scope;
        UserId = userId;
        SubjectId = subjectId;
        IssuedAt = issuedAt;
        Hash = hash;
        RevokedAt = revokedAt;
        RevocationReason = revocationReason;
    }

    /// <summary>
    /// Invariant: no certificate without every required lesson completed (T-01, T-10).
    /// The caller passes the required set and the completed set; the entity does not query.
    /// </summary>
    public static Result<Certificate, Error> Issue(
        Guid id,
        CertificateScope scope,
        Guid userId,
        Guid subjectId,
        IReadOnlySet<Guid> requiredLessonIds,
        IReadOnlySet<Guid> completedLessonIds,
        DateTimeOffset issuedAt,
        string signingSecret)
    {
        if (requiredLessonIds.Count == 0)
        {
            return Error.Conflict(
                "certificate.no_required_lessons",
                "No se puede emitir un certificado de un curso sin lecciones obligatorias.");
        }

        var missing = requiredLessonIds.Count(id2 => !completedLessonIds.Contains(id2));
        if (missing > 0)
        {
            return Error.Conflict(
                "certificate.incomplete",
                $"Faltan {missing} lecciones obligatorias por completar.");
        }

        var code = CertificateCode.NewCode();
        var hash = ComputeHash(userId, subjectId, issuedAt, signingSecret);

        return new Certificate(
            id, code, scope, userId, subjectId, issuedAt, hash,
            revokedAt: null, revocationReason: null);
    }

    public static Certificate Rehydrate(
        Guid id,
        CertificateCode code,
        CertificateScope scope,
        Guid userId,
        Guid subjectId,
        DateTimeOffset issuedAt,
        string hash,
        DateTimeOffset? revokedAt,
        string? revocationReason) =>
        new(id, code, scope, userId, subjectId, issuedAt, hash, revokedAt, revocationReason);

    /// <summary>
    /// Deterministic: same inputs produce the same hash, which is what makes the PDF
    /// reproducible (T-10 acceptance criteria). The timestamp is truncated to the second
    /// because the database column has microsecond precision and round-trips would differ.
    /// </summary>
    public static string ComputeHash(Guid userId, Guid subjectId, DateTimeOffset issuedAt, string signingSecret)
    {
        var seconds = issuedAt.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var payload = $"{userId:N}|{subjectId:N}|{seconds}";
        var key = Encoding.UTF8.GetBytes(signingSecret);
        var bytes = HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexStringLower(bytes);
    }

    public bool Verify(string signingSecret) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(Hash),
            Encoding.UTF8.GetBytes(ComputeHash(UserId, SubjectId, IssuedAt, signingSecret)));


    public Result<Unit, Error> Revoke(DateTimeOffset now, string reason)
    {
        if (RevokedAt is not null)
        {
            return Error.Conflict("certificate.already_revoked", "El certificado ya estaba revocado.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return Error.Validation("certificate.revocation_reason_empty", "La revocación necesita un motivo.");
        }

        RevokedAt = now;
        RevocationReason = reason.Trim();
        return Unit.Value;
    }

    public bool IsValid => RevokedAt is null;
}
