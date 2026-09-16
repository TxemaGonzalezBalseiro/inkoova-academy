using Inkoova.Academy.Application.Abstractions;
using Inkoova.Academy.Domain.Affiliates;
using Inkoova.Academy.Domain.Common;

namespace Inkoova.Academy.Application.Affiliates;

public sealed record AffiliateDashboardDto(
    string Code,
    decimal CommissionPercent,
    string Status,
    int RecurringMonths,
    int ClicksThisMonth,
    int Conversions,
    decimal PendingAmount,
    decimal ApprovedAmount,
    decimal PaidAmount,
    string Currency,
    IReadOnlyList<AffiliateCommissionDto> RecentCommissions,
    IReadOnlyList<AffiliatePayoutDto> Payouts,
    string ReferralUrl);

public sealed record AffiliateCommissionDto(
    DateOnly Date,
    string Product,
    decimal NetBase,
    decimal Amount,
    string Status);

public sealed record AffiliatePayoutDto(
    Guid Id,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    decimal Total,
    string Status,
    bool HasStatement);

/// <summary>
/// The affiliate's own dashboard. Scoped by the caller's user id, never by a query
/// parameter, so one affiliate cannot read another's figures (T-16 acceptance criteria).
/// </summary>
public sealed class GetAffiliateDashboardHandler(
    IAffiliateRepository affiliates,
    ICommissionRepository commissions,
    IPayoutRepository payouts,
    IPurchaseRepository purchases,
    IProductRepository products,
    IReferralRepository referrals,
    IClock clock,
    string publicBaseUrl)
{
    public async Task<Result<AffiliateDashboardDto, Error>> HandleAsync(Guid userId, CancellationToken ct)
    {
        var affiliate = await affiliates.GetByUserIdAsync(userId, ct);
        if (affiliate is null)
        {
            return Error.NotFound("affiliate.not_found", "No tienes un perfil de afiliado.");
        }

        var all = await commissions.GetForAffiliateAsync(affiliate.Id, ct);
        var currency = all.Count > 0 ? all[0].Amount.Currency : "EUR";

        var now = clock.UtcNow;
        var monthStart = new DateOnly(now.Year, now.Month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);
        var clicks = await referrals.CountClicksAsync(affiliate.Id, monthStart, monthEnd, ct);

        var recent = new List<AffiliateCommissionDto>();
        foreach (var commission in all.OrderByDescending(c => c.CreatedAt).Take(50))
        {
            var purchase = await purchases.GetByIdAsync(commission.PurchaseId, ct);
            var product = purchase is null ? null : await products.GetByIdAsync(purchase.ProductId, ct);

            recent.Add(new AffiliateCommissionDto(
                DateOnly.FromDateTime(commission.CreatedAt.UtcDateTime),
                product?.Title ?? "Suscripción",
                commission.NetBase.ToDecimal(),
                commission.Amount.ToDecimal(),
                commission.Status.ToString().ToLowerInvariant()));
        }

        var history = await payouts.GetForAffiliateAsync(affiliate.Id, ct);

        return new AffiliateDashboardDto(
            affiliate.Code,
            affiliate.CommissionPercent,
            affiliate.Status.ToString().ToLowerInvariant(),
            affiliate.RecurringMonths,
            clicks,
            all.Count(c => c.Status != CommissionStatus.Reversed),
            SumOf(all, CommissionStatus.Pending),
            SumOf(all, CommissionStatus.Approved),
            SumOf(all, CommissionStatus.Paid),
            currency,
            recent,
            history
                .OrderByDescending(p => p.PeriodStart)
                .Select(p => new AffiliatePayoutDto(
                    p.Id, p.PeriodStart, p.PeriodEnd, p.Total.ToDecimal(),
                    p.Status.ToString().ToLowerInvariant(), p.StatementContentRef is not null))
                .ToList(),
            $"{publicBaseUrl.TrimEnd('/')}/?ref={affiliate.Code}");
    }

    private static decimal SumOf(IReadOnlyList<Commission> all, CommissionStatus status) =>
        all.Where(c => c.Status == status).Sum(c => c.Amount.ToDecimal());
}

/// <summary>Records a click on a referral link. Called from the SPA on first load with <c>?ref=</c>.</summary>
public sealed class TrackReferralHandler(
    IAffiliateRepository affiliates,
    IReferralRepository referrals,
    IClock clock)
{
    public async Task<Result<Unit, Error>> HandleAsync(string code, string visitorId, CancellationToken ct)
    {
        var affiliate = await affiliates.GetByCodeAsync(code.Trim().ToUpperInvariant(), ct);
        if (affiliate is null || affiliate.Status != AffiliateStatus.Active)
        {
            // An unknown code is not an error for the visitor; the page just loads normally.
            return Unit.Value;
        }

        var existing = await referrals.GetByVisitorAsync(visitorId, ct);

        if (existing is null)
        {
            var tracked = Referral.Track(Guid.CreateVersion7(), visitorId, affiliate.Id, clock.UtcNow);
            if (tracked.IsFailure)
            {
                return tracked.Error;
            }

            await referrals.UpsertAsync(tracked.Value, ct);
            return Unit.Value;
        }

        if (existing.ConvertedPurchaseId is not null)
        {
            // Already converted; do not re-attribute a customer who has already bought.
            return Unit.Value;
        }

        // Last click wins: a different affiliate replaces the attribution.
        if (existing.AffiliateId != affiliate.Id)
        {
            var replaced = Referral.Track(existing.Id, visitorId, affiliate.Id, clock.UtcNow);
            if (replaced.IsFailure)
            {
                return replaced.Error;
            }

            await referrals.UpsertAsync(replaced.Value, ct);
            return Unit.Value;
        }

        existing.RegisterClick(clock.UtcNow);
        await referrals.UpsertAsync(existing, ct);
        return Unit.Value;
    }
}

public sealed class UpdateAffiliateTaxDataHandler(IAffiliateRepository affiliates)
{
    public async Task<Result<Unit, Error>> HandleAsync(
        Guid userId,
        string taxId,
        string countryCode,
        string iban,
        CancellationToken ct)
    {
        var affiliate = await affiliates.GetByUserIdAsync(userId, ct);
        if (affiliate is null)
        {
            return Error.NotFound("affiliate.not_found", "No tienes un perfil de afiliado.");
        }

        var updated = affiliate.SetTaxData(taxId, countryCode, iban);
        if (updated.IsFailure)
        {
            return updated.Error;
        }

        await affiliates.UpsertAsync(affiliate, ct);
        return Unit.Value;
    }
}
