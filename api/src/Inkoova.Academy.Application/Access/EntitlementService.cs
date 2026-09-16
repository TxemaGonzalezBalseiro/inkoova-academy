using Inkoova.Academy.Application.Abstractions;
using Inkoova.Academy.Domain.Billing;
using Inkoova.Academy.Domain.Common;

namespace Inkoova.Academy.Application.Access;

/// <summary>
/// Writes entitlements. Every sales channel funnels through here, which is what keeps
/// <see cref="AccessPolicy"/> free of channel-specific rules (ADR-007).
/// </summary>
public sealed class EntitlementService(
    IEntitlementRepository entitlements,
    IProductCatalog productCatalog,
    IClock clock)
{
    /// <summary>Permanent access from a one-off purchase. Idempotent per (user, product).</summary>
    public async Task<Result<Unit, Error>> GrantFromPurchaseAsync(Guid userId, Guid productId, CancellationToken ct)
    {
        var existing = await entitlements.GetForUserAndProductAsync(userId, productId, ct);

        if (existing is { Source: EntitlementSource.Purchase, RevokedAt: null })
        {
            return Unit.Value;
        }

        var granted = Entitlement.Grant(
            Guid.CreateVersion7(), userId, productId, EntitlementSource.Purchase,
            clock.UtcNow, validUntil: null);

        if (granted.IsFailure)
        {
            return granted.Error;
        }

        await entitlements.UpsertAsync(granted.Value, ct);
        return Unit.Value;
    }

    /// <summary>
    /// Access derived from an active plan. Extends an existing plan entitlement instead of
    /// stacking a second one, so a renewal moves the window rather than duplicating rows.
    /// </summary>
    public async Task<Result<Unit, Error>> GrantFromPlanAsync(
        Guid userId,
        Guid productId,
        DateTimeOffset validUntil,
        CancellationToken ct)
    {
        var existing = await entitlements.GetForUserAndProductAsync(userId, productId, ct);

        if (existing is { Source: EntitlementSource.Purchase, RevokedAt: null })
        {
            // Already owned outright; a plan cannot downgrade a permanent entitlement.
            return Unit.Value;
        }

        if (existing is { Source: EntitlementSource.PlanIncluded, RevokedAt: null })
        {
            existing.ExtendTo(validUntil);
            await entitlements.UpsertAsync(existing, ct);
            return Unit.Value;
        }

        var granted = Entitlement.Grant(
            Guid.CreateVersion7(), userId, productId, EntitlementSource.PlanIncluded,
            clock.UtcNow, validUntil);

        if (granted.IsFailure)
        {
            return granted.Error;
        }

        await entitlements.UpsertAsync(granted.Value, ct);
        return Unit.Value;
    }

    /// <summary>
    /// Yearly and lifetime plans include every sector pack (T-04 addendum). Packs bought
    /// outright keep their purchase entitlement and survive the plan expiring.
    /// </summary>
    public async Task<Result<Unit, Error>> GrantAllPacksFromPlanAsync(
        Guid userId,
        DateTimeOffset validUntil,
        CancellationToken ct)
    {
        var packs = await productCatalog.GetAllPackProductsAsync(ct);

        foreach (var pack in packs)
        {
            var result = await GrantFromPlanAsync(userId, pack.Id, validUntil, ct);
            if (result.IsFailure)
            {
                return result.Error;
            }
        }

        return Unit.Value;
    }

    /// <summary>
    /// Acceso concedido a mano: beca, empresa, revisión interna.
    ///
    /// Reutiliza el identificador del entitlement vivo que ya hubiera. El esquema tiene un
    /// índice único de un entitlement vivo por (usuario, producto) —«el servicio extiende en
    /// vez de apilar»— pero esto generaba un id nuevo siempre, así que conceder dos veces el
    /// mismo producto violaba el índice y el panel devolvía un 500 al segundo clic.
    /// </summary>
    public async Task<Result<Unit, Error>> GrantManualAsync(
        Guid userId,
        Guid productId,
        DateTimeOffset? validUntil,
        string note,
        CancellationToken ct)
    {
        var existing = await entitlements.GetForUserAndProductAsync(userId, productId, ct);
        var id = existing is { RevokedAt: null } ? existing.Id : Guid.CreateVersion7();

        var granted = Entitlement.Grant(
            id, userId, productId, EntitlementSource.Manual,
            clock.UtcNow, validUntil, note);

        if (granted.IsFailure)
        {
            return granted.Error;
        }

        await entitlements.UpsertAsync(granted.Value, ct);
        return Unit.Value;
    }

    /// <summary>Refund, chargeback or admin revocation. A missing entitlement is not an error.</summary>
    public async Task<Result<Unit, Error>> RevokeAsync(
        Guid userId,
        Guid productId,
        string reason,
        CancellationToken ct)
    {
        var existing = await entitlements.GetForUserAndProductAsync(userId, productId, ct);
        if (existing is null || existing.RevokedAt is not null)
        {
            return Unit.Value;
        }

        var revoked = existing.Revoke(clock.UtcNow, reason);
        if (revoked.IsFailure)
        {
            return revoked.Error;
        }

        await entitlements.UpsertAsync(existing, ct);
        return Unit.Value;
    }

    /// <summary>
    /// Withdraws only plan-derived access. A pack the student bought outright keeps its
    /// purchase entitlement when the plan ends (T-04 addendum).
    /// </summary>
    public async Task<Result<Unit, Error>> RevokePlanDerivedAsync(
        Guid userId,
        Guid productId,
        string reason,
        CancellationToken ct)
    {
        var existing = await entitlements.GetForUserAndProductAsync(userId, productId, ct);

        if (existing is null || existing.RevokedAt is not null || existing.Source != EntitlementSource.PlanIncluded)
        {
            return Unit.Value;
        }

        var revoked = existing.Revoke(clock.UtcNow, reason);
        if (revoked.IsFailure)
        {
            return revoked.Error;
        }

        await entitlements.UpsertAsync(existing, ct);
        return Unit.Value;
    }

    /// <summary>
    /// Daily job (T-04, T-12): withdraw plan-derived access whose window has closed.
    /// Idempotent, so running it twice on the same day changes nothing.
    /// </summary>
    public async Task<int> RevokeExpiredPlanEntitlementsAsync(CancellationToken ct)
    {
        var now = clock.UtcNow;
        var expired = await entitlements.GetExpiredPlanIncludedAsync(now, ct);
        var revoked = 0;

        foreach (var entitlement in expired)
        {
            var result = entitlement.Revoke(now, "plan expirado");
            if (result.IsFailure)
            {
                continue;
            }

            await entitlements.UpsertAsync(entitlement, ct);
            revoked++;
        }

        return revoked;
    }
}
