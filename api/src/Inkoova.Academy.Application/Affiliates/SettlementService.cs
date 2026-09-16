using System.Globalization;
using Inkoova.Academy.Application.Abstractions;
using Inkoova.Academy.Domain.Affiliates;
using Inkoova.Academy.Domain.Common;
using Inkoova.Academy.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Inkoova.Academy.Application.Affiliates;

/// <summary>
/// Monthly settlement (T-16). Deterministic: the same period over the same commissions
/// produces the same total and the same statement, which is what makes the acceptance
/// criterion "same period, same PDF" testable.
/// </summary>
public sealed class SettlementService(
    IAffiliateRepository affiliates,
    ICommissionRepository commissions,
    IPayoutRepository payouts,
    IPurchaseRepository purchases,
    IProductRepository products,
    IUserRepository users,
    IAffiliateStatementGenerator statements,
    IContentStorage storage,
    IEmailSender email,
    IEmailTemplateRenderer templates,
    IClock clock,
    ILogger<SettlementService> logger)
{
    public async Task<IReadOnlyList<Guid>> RunForPeriodAsync(DateOnly periodStart, DateOnly periodEnd, CancellationToken ct)
    {
        var created = new List<Guid>();

        foreach (var affiliate in await affiliates.GetActiveAsync(ct))
        {
            var result = await SettleAsync(affiliate, periodStart, periodEnd, ct);
            if (result.IsFailure)
            {
                logger.LogWarning(
                    "Settlement skipped for affiliate {Code}: {Error}", affiliate.Code, result.Error);
                continue;
            }

            if (result.Value is { } payoutId)
            {
                created.Add(payoutId);
            }
        }

        return created;
    }

    private async Task<Result<Guid?, Error>> SettleAsync(
        Affiliate affiliate,
        DateOnly periodStart,
        DateOnly periodEnd,
        CancellationToken ct)
    {
        var existing = await payouts.GetForPeriodAsync(affiliate.Id, periodStart, ct);
        if (existing is not null)
        {
            return existing.Id;
        }

        var payable = await commissions.GetPayableAsync(affiliate.Id, periodEnd, ct);
        var carried = await ComputeCarryOverAsync(affiliate.Id, ct);

        if (payable.Count == 0 && carried.AmountInCents == 0)
        {
            return (Guid?)null;
        }

        var payout = Payout.Settle(
            Guid.CreateVersion7(), affiliate, periodStart, periodEnd, payable, carried, clock.UtcNow);

        if (payout.IsFailure)
        {
            return payout.Error;
        }

        foreach (var commission in payable)
        {
            var attached = commission.AttachToPayout(payout.Value.Id);
            if (attached.IsFailure)
            {
                return attached.Error;
            }

            await commissions.UpsertAsync(commission, ct);
        }

        var statement = await BuildStatementAsync(affiliate, payout.Value, payable, ct);
        var contentRef = $"affiliates/{affiliate.Code}/{periodStart:yyyy-MM}-liquidacion.pdf";

        using (var stream = new MemoryStream(statements.Generate(statement)))
        {
            await storage.WriteAsync(contentRef, stream, ct);
        }

        payout.Value.AttachStatement(contentRef);
        await payouts.UpsertAsync(payout.Value, ct);

        await NotifyAsync(affiliate, payout.Value, ct);

        return payout.Value.Id;
    }

    /// <summary>
    /// Balance from previous periods that never reached the minimum. Rolls forward until the
    /// total clears 50 €, so the platform does not issue two-euro transfers.
    /// </summary>
    private async Task<Money> ComputeCarryOverAsync(Guid affiliateId, CancellationToken ct)
    {
        var history = await payouts.GetForAffiliateAsync(affiliateId, ct);

        var pending = history
            .Where(p => p.Status is PayoutStatus.AwaitingInvoice or PayoutStatus.ReadyToPay && !p.ReachesMinimum)
            .OrderByDescending(p => p.PeriodEnd)
            .FirstOrDefault();

        return pending?.Total ?? Money.Euros(0);
    }

    private async Task<AffiliateStatementModel> BuildStatementAsync(
        Affiliate affiliate,
        Payout payout,
        IReadOnlyList<Commission> included,
        CancellationToken ct)
    {
        var user = await users.GetByIdAsync(affiliate.UserId, ct);
        var lines = new List<(DateOnly, string, decimal, decimal, decimal)>();

        // Ordered by creation then id so a regenerated statement lists rows identically.
        foreach (var commission in included.OrderBy(c => c.CreatedAt).ThenBy(c => c.Id))
        {
            var purchase = await purchases.GetByIdAsync(commission.PurchaseId, ct);
            var product = purchase is null ? null : await products.GetByIdAsync(purchase.ProductId, ct);

            lines.Add((
                DateOnly.FromDateTime(commission.CreatedAt.UtcDateTime),
                product?.Title ?? "Suscripción",
                commission.NetBase.ToDecimal(),
                commission.Percent,
                commission.Amount.ToDecimal()));
        }

        return new AffiliateStatementModel(
            user?.DisplayName ?? affiliate.Code,
            affiliate.Code,
            affiliate.TaxId ?? "TODO(T-16): datos fiscales pendientes",
            payout.PeriodStart,
            payout.PeriodEnd,
            lines,
            payout.CarriedOverIn.ToDecimal(),
            payout.Total.ToDecimal(),
            payout.Total.Currency);
    }

    private async Task NotifyAsync(Affiliate affiliate, Payout payout, CancellationToken ct)
    {
        var user = await users.GetByIdAsync(affiliate.UserId, ct);
        if (user is null)
        {
            return;
        }

        var template = payout.ReachesMinimum ? "affiliate-payout-ready" : "affiliate-payout-carried";

        var rendered = await templates.RenderAsync(template, new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["nombre"] = user.DisplayName,
            ["periodo"] = $"{payout.PeriodStart:MM/yyyy}",
            ["importe"] = payout.Total.ToDecimal().ToString("0.00", CultureInfo.InvariantCulture),
            ["minimo"] = Payout.MinimumPayout.ToDecimal().ToString("0.00", CultureInfo.InvariantCulture),
            ["modo_factura"] = affiliate.InvoicingMode == InvoicingMode.SelfBilling
                ? "autofactura emitida por Inkoova"
                : "envíanos tu factura"
        }, ct);

        if (rendered.IsFailure)
        {
            logger.LogWarning("Affiliate statement email template failed: {Error}", rendered.Error);
            return;
        }

        await email.SendAsync(
            new EmailMessage(user.Email, rendered.Value.Subject, rendered.Value.Html, rendered.Value.Text), ct);
    }
}
