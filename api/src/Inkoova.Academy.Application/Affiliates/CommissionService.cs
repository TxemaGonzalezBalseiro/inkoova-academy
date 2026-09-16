using Inkoova.Academy.Application.Abstractions;
using Inkoova.Academy.Application.Billing;
using Inkoova.Academy.Domain.Affiliates;
using Inkoova.Academy.Domain.Billing;
using Inkoova.Academy.Domain.Common;
using Microsoft.Extensions.Logging;

namespace Inkoova.Academy.Application.Affiliates;

/// <summary>
/// Accrues, approves and reverses affiliate commissions. All the antifraud rules live here
/// (T-16): no self-referral, no commission on refunded sales, recurring capped in months.
/// </summary>
public sealed class CommissionService(
    IAffiliateRepository affiliates,
    ICommissionRepository commissions,
    IPurchaseRepository purchases,
    IReferralRepository referrals,
    IUserRepository users,
    IPlanRepository plans,
    IClock clock,
    ILogger<CommissionService> logger)
{
    public async Task<Result<Unit, Error>> AccrueForPurchaseAsync(
        Purchase purchase,
        IReadOnlyDictionary<string, string> metadata,
        CancellationToken ct)
    {
        var affiliate = await ResolveAffiliateAsync(metadata, ct);
        if (affiliate is null)
        {
            return Unit.Value;
        }

        if (await IsSelfReferralAsync(affiliate, purchase.UserId, ct))
        {
            logger.LogWarning(
                "Self-referral blocked: affiliate {AffiliateId} on purchase {PurchaseId}.",
                affiliate.Id, purchase.Id);
            return Unit.Value;
        }

        var already = await commissions.GetByPurchaseAsync(purchase.Id, ct);
        if (already.Count > 0)
        {
            return Unit.Value;
        }

        if (!purchase.IsCommissionable)
        {
            return Unit.Value;
        }

        var commission = Commission.Accrue(
            Guid.CreateVersion7(), purchase.Id, affiliate.Id,
            purchase.NetBase(), affiliate.CommissionPercent, purchase.PaidAt);

        if (commission.IsFailure)
        {
            return commission.Error;
        }

        await commissions.UpsertAsync(commission.Value, ct);
        await MarkReferralConvertedAsync(metadata, purchase.Id, ct);

        return Unit.Value;
    }

    /// <summary>
    /// Subscription renewals pay commission for a configurable number of months from the
    /// first payment, not only on the first invoice (T-16).
    /// </summary>
    public async Task<Result<Unit, Error>> AccrueForRenewalAsync(
        Guid userId,
        Subscription subscription,
        PaymentEvent evt,
        Plan plan,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(evt.InvoiceId) || evt.AmountTotalCents <= 0)
        {
            return Unit.Value;
        }

        var affiliate = await ResolveAffiliateAsync(evt.Metadata, ct);
        if (affiliate is null)
        {
            // Metadata is only present on the first invoice of a subscription. Later renewals
            // inherit attribution from the commission recorded for the first one.
            affiliate = await ResolveInheritedAffiliateAsync(userId, subscription, ct);
        }

        if (affiliate is null || affiliate.RecurringMonths == 0)
        {
            return Unit.Value;
        }

        if (await IsSelfReferralAsync(affiliate, userId, ct))
        {
            return Unit.Value;
        }

        var monthsElapsed = MonthsBetween(subscription.CreatedAt, evt.OccurredAt);
        if (monthsElapsed >= affiliate.RecurringMonths)
        {
            return Unit.Value;
        }

        // Renewals are modelled as purchases so that the commission base, the refund path and
        // the settlement all share one shape. The invoice id keeps it idempotent.
        var existing = await purchases.GetByPaymentIntentAsync(evt.InvoiceId, ct);
        if (existing is not null)
        {
            return Unit.Value;
        }

        var currency = evt.Currency.ToUpperInvariant();
        var purchase = Purchase.Record(
            Guid.CreateVersion7(),
            userId,
            plan.Id,
            Domain.ValueObjects.Money.Create(evt.AmountTotalCents, currency).Value,
            Domain.ValueObjects.Money.Create(evt.AmountTaxCents, currency).Value,
            Domain.ValueObjects.Money.Create(evt.AmountFeeCents, currency).Value,
            evt.InvoiceId,
            evt.PromotionCode,
            evt.OccurredAt);

        if (purchase.IsFailure)
        {
            return purchase.Error;
        }

        await purchases.UpsertAsync(purchase.Value, ct);

        var commission = Commission.Accrue(
            Guid.CreateVersion7(), purchase.Value.Id, affiliate.Id,
            purchase.Value.NetBase(), affiliate.CommissionPercent, evt.OccurredAt);

        if (commission.IsFailure)
        {
            return commission.Error;
        }

        await commissions.UpsertAsync(commission.Value, ct);
        return Unit.Value;
    }

    /// <summary>A refund or chargeback reverses every commission of the purchase.</summary>
    public async Task<Result<Unit, Error>> ReverseForPurchaseAsync(Guid purchaseId, string reason, CancellationToken ct)
    {
        foreach (var commission in await commissions.GetByPurchaseAsync(purchaseId, ct))
        {
            if (commission.Reverse(reason).IsFailure)
            {
                continue;
            }

            await commissions.UpsertAsync(commission, ct);
        }

        return Unit.Value;
    }

    /// <summary>
    /// Daily job: move commissions past the 14-day withdrawal window to approved. Anything
    /// refunded in the meantime was already reversed and is skipped by the entity itself.
    /// </summary>
    public async Task<int> ApproveMaturedAsync(CancellationToken ct)
    {
        var now = clock.UtcNow;
        var approved = 0;

        foreach (var commission in await commissions.GetApprovableAsync(now, ct))
        {
            var purchase = await purchases.GetByIdAsync(commission.PurchaseId, ct);
            if (purchase is null || !purchase.IsCommissionable)
            {
                if (commission.Reverse("compra no comisionable").IsSuccess)
                {
                    await commissions.UpsertAsync(commission, ct);
                }

                continue;
            }

            if (commission.Approve(now).IsFailure)
            {
                continue;
            }

            await commissions.UpsertAsync(commission, ct);
            approved++;
        }

        return approved;
    }

    private async Task<Affiliate?> ResolveAffiliateAsync(
        IReadOnlyDictionary<string, string> metadata,
        CancellationToken ct)
    {
        if (!metadata.TryGetValue(PaymentMetadataKeys.AffiliateId, out var raw) || !Guid.TryParse(raw, out var id))
        {
            return null;
        }

        var affiliate = await affiliates.GetByIdAsync(id, ct);
        return affiliate is { Status: AffiliateStatus.Active } ? affiliate : null;
    }

    private async Task<Affiliate?> ResolveInheritedAffiliateAsync(
        Guid userId,
        Subscription subscription,
        CancellationToken ct)
    {
        var userPurchases = await purchases.GetForUserAsync(userId, ct);

        foreach (var purchase in userPurchases.OrderBy(p => p.PaidAt))
        {
            if (purchase.PaidAt < subscription.CreatedAt)
            {
                continue;
            }

            var existing = await commissions.GetByPurchaseAsync(purchase.Id, ct);
            var first = existing.FirstOrDefault();
            if (first is null)
            {
                continue;
            }

            var affiliate = await affiliates.GetByIdAsync(first.AffiliateId, ct);
            if (affiliate is { Status: AffiliateStatus.Active })
            {
                return affiliate;
            }
        }

        return null;
    }

    /// <summary>An affiliate cannot earn on their own account (T-16 antifraud).</summary>
    private async Task<bool> IsSelfReferralAsync(Affiliate affiliate, Guid buyerId, CancellationToken ct)
    {
        if (affiliate.UserId == buyerId)
        {
            return true;
        }

        var affiliateUser = await users.GetByIdAsync(affiliate.UserId, ct);
        var buyer = await users.GetByIdAsync(buyerId, ct);

        return affiliateUser is not null
               && buyer is not null
               && string.Equals(affiliateUser.Email, buyer.Email, StringComparison.OrdinalIgnoreCase);
    }

    private async Task MarkReferralConvertedAsync(
        IReadOnlyDictionary<string, string> metadata,
        Guid purchaseId,
        CancellationToken ct)
    {
        if (!metadata.TryGetValue(PaymentMetadataKeys.VisitorId, out var visitorId))
        {
            return;
        }

        var referral = await referrals.GetByVisitorAsync(visitorId, ct);
        if (referral is null || referral.Convert(purchaseId, clock.UtcNow).IsFailure)
        {
            return;
        }

        await referrals.UpsertAsync(referral, ct);
    }

    private static int MonthsBetween(DateTimeOffset from, DateTimeOffset to) =>
        ((to.Year - from.Year) * 12) + to.Month - from.Month;

    /// <summary>Exposed for the settlement job, which needs plan names on the statement.</summary>
    internal IPlanRepository Plans => plans;
}
