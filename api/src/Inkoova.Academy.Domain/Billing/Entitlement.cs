using Inkoova.Academy.Domain.Common;

namespace Inkoova.Academy.Domain.Billing;

/// <summary>Why the student has this product. Determines whether it can be revoked.</summary>
public enum EntitlementSource
{
    /// <summary>One-off purchase. Permanent; survives plan expiry.</summary>
    Purchase,

    /// <summary>Derived from an active plan. Withdrawn when the plan ends.</summary>
    PlanIncluded,

    /// <summary>Granted from admin: scholarship, company seat, support gesture.</summary>
    Manual
}

/// <summary>
/// The single gate for access (ADR-007). Every way of selling writes one of these;
/// <c>IAccessPolicy</c> reads nothing else.
/// </summary>
public sealed class Entitlement
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid ProductId { get; private set; }
    public EntitlementSource Source { get; private set; }
    public DateTimeOffset ValidFrom { get; private set; }

    /// <summary>Null means permanent. Plan-derived entitlements always carry a date as a safety net.</summary>
    public DateTimeOffset? ValidUntil { get; private set; }

    /// <summary>Set when withdrawn early (refund, chargeback, admin revocation).</summary>
    public DateTimeOffset? RevokedAt { get; private set; }

    public string? Note { get; private set; }

    private Entitlement(
        Guid id,
        Guid userId,
        Guid productId,
        EntitlementSource source,
        DateTimeOffset validFrom,
        DateTimeOffset? validUntil,
        DateTimeOffset? revokedAt,
        string? note)
    {
        Id = id;
        UserId = userId;
        ProductId = productId;
        Source = source;
        ValidFrom = validFrom;
        ValidUntil = validUntil;
        RevokedAt = revokedAt;
        Note = note;
    }

    public static Result<Entitlement, Error> Grant(
        Guid id,
        Guid userId,
        Guid productId,
        EntitlementSource source,
        DateTimeOffset validFrom,
        DateTimeOffset? validUntil,
        string? note = null)
    {
        if (validUntil is not null && validUntil <= validFrom)
        {
            return Error.Validation("entitlement.invalid_window", "La validez debe terminar después de empezar.");
        }

        if (source == EntitlementSource.PlanIncluded && validUntil is null)
        {
            return Error.Validation(
                "entitlement.plan_without_expiry",
                "Un entitlement derivado de un plan necesita fecha de fin.");
        }

        if (source == EntitlementSource.Manual && string.IsNullOrWhiteSpace(note))
        {
            return Error.Validation(
                "entitlement.manual_without_note",
                "Un acceso manual necesita una nota que justifique la concesión.");
        }

        return new Entitlement(id, userId, productId, source, validFrom, validUntil, revokedAt: null, note?.Trim());
    }

    public static Entitlement Rehydrate(
        Guid id,
        Guid userId,
        Guid productId,
        EntitlementSource source,
        DateTimeOffset validFrom,
        DateTimeOffset? validUntil,
        DateTimeOffset? revokedAt,
        string? note) =>
        new(id, userId, productId, source, validFrom, validUntil, revokedAt, note);

    public Result<Unit, Error> Revoke(DateTimeOffset now, string reason)
    {
        if (RevokedAt is not null)
        {
            return Error.Conflict("entitlement.already_revoked", "El acceso ya estaba revocado.");
        }

        RevokedAt = now;
        Note = string.IsNullOrWhiteSpace(Note) ? reason : $"{Note} · revocado: {reason}";
        return Unit.Value;
    }

    public void ExtendTo(DateTimeOffset newValidUntil)
    {
        if (ValidUntil is null || newValidUntil > ValidUntil)
        {
            ValidUntil = newValidUntil;
        }
    }

    public bool IsValidAt(DateTimeOffset instant) =>
        RevokedAt is null
        && instant >= ValidFrom
        && (ValidUntil is null || instant <= ValidUntil);
}
