using Dapper;
using Inkoova.Academy.Application.Abstractions;
using Inkoova.Academy.Domain.Affiliates;
using Inkoova.Academy.Domain.ValueObjects;

namespace Inkoova.Academy.Infrastructure.Persistence;

public sealed class AffiliateRepository(IDbConnectionFactory connections) : IAffiliateRepository
{
    private const string Columns = """
        id, user_id, code, commission_percent, status, recurring_months, tax_id, country_code,
        iban, invoicing_mode, self_billing_agreement_ref, created_at
        """;

    public async Task<Affiliate?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        var row = await connection.QuerySingleOrDefaultAsync<AffiliateRow>(
            $"SELECT {Columns} FROM affiliate WHERE id = @id", new { id });

        return row?.ToDomain();
    }

    public async Task<Affiliate?> GetByCodeAsync(string code, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        var row = await connection.QuerySingleOrDefaultAsync<AffiliateRow>(
            $"SELECT {Columns} FROM affiliate WHERE code = @code", new { code });

        return row?.ToDomain();
    }

    public async Task<Affiliate?> GetByUserIdAsync(Guid userId, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        var row = await connection.QuerySingleOrDefaultAsync<AffiliateRow>(
            $"SELECT {Columns} FROM affiliate WHERE user_id = @userId", new { userId });

        return row?.ToDomain();
    }

    public async Task<IReadOnlyList<Affiliate>> GetActiveAsync(CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        var rows = await connection.QueryAsync<AffiliateRow>(
            $"SELECT {Columns} FROM affiliate WHERE status = 'active' ORDER BY code");

        return rows.Select(r => r.ToDomain()).ToList();
    }

    public async Task UpsertAsync(Affiliate affiliate, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        await connection.ExecuteAsync(
            """
            INSERT INTO affiliate (id, user_id, code, commission_percent, status, recurring_months,
                                   tax_id, country_code, iban, invoicing_mode,
                                   self_billing_agreement_ref, created_at)
            VALUES (@Id, @UserId, @Code, @CommissionPercent, @Status, @RecurringMonths,
                    @TaxId, @CountryCode, @Iban, @InvoicingMode, @SelfBillingAgreementRef, @CreatedAt)
            ON CONFLICT (id) DO UPDATE SET
                code = EXCLUDED.code,
                commission_percent = EXCLUDED.commission_percent,
                status = EXCLUDED.status,
                recurring_months = EXCLUDED.recurring_months,
                tax_id = EXCLUDED.tax_id,
                country_code = EXCLUDED.country_code,
                iban = EXCLUDED.iban,
                invoicing_mode = EXCLUDED.invoicing_mode,
                self_billing_agreement_ref = EXCLUDED.self_billing_agreement_ref
            """,
            new
            {
                affiliate.Id,
                affiliate.UserId,
                affiliate.Code,
                affiliate.CommissionPercent,
                Status = EnumMapping.ToDb(affiliate.Status),
                affiliate.RecurringMonths,
                affiliate.TaxId,
                affiliate.CountryCode,
                affiliate.Iban,
                InvoicingMode = EnumMapping.ToDb(affiliate.InvoicingMode),
                affiliate.SelfBillingAgreementRef,
                affiliate.CreatedAt
            });
    }

    private sealed record AffiliateRow(
        Guid Id,
        Guid UserId,
        string Code,
        decimal CommissionPercent,
        string Status,
        int RecurringMonths,
        string? TaxId,
        string? CountryCode,
        string? Iban,
        string InvoicingMode,
        string? SelfBillingAgreementRef,
        DateTimeOffset CreatedAt)
    {
        public Affiliate ToDomain() => Affiliate.Rehydrate(
            Id, UserId, Code, CommissionPercent, EnumMapping.FromDb<AffiliateStatus>(Status),
            RecurringMonths, TaxId, CountryCode, Iban,
            EnumMapping.FromDb<Domain.Affiliates.InvoicingMode>(InvoicingMode),
            SelfBillingAgreementRef, CreatedAt);
    }
}

public sealed class DiscountCodeRepository(IDbConnectionFactory connections) : IDiscountCodeRepository
{
    private const string Columns = """
        id, code, kind, percent, fixed_amount_cents, currency, applicable_product_ids,
        valid_from, valid_until, max_redemptions, redemptions, affiliate_id,
        stripe_promotion_code_id, is_active
        """;

    public async Task<DiscountCode?> GetByCodeAsync(string code, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        var row = await connection.QuerySingleOrDefaultAsync<DiscountRow>(
            $"SELECT {Columns} FROM discount_code WHERE code = @code", new { code });

        return row?.ToDomain();
    }

    public async Task<IReadOnlyList<DiscountCode>> GetAllAsync(CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        var rows = await connection.QueryAsync<DiscountRow>($"SELECT {Columns} FROM discount_code ORDER BY code");
        return rows.Select(r => r.ToDomain()).ToList();
    }

    public async Task UpsertAsync(DiscountCode code, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        await connection.ExecuteAsync(
            """
            INSERT INTO discount_code (id, code, kind, percent, fixed_amount_cents, currency,
                                       applicable_product_ids, valid_from, valid_until, max_redemptions,
                                       redemptions, affiliate_id, stripe_promotion_code_id, is_active)
            VALUES (@Id, @Code, @Kind, @Percent, @FixedAmountCents, @Currency,
                    @ApplicableProductIds, @ValidFrom, @ValidUntil, @MaxRedemptions,
                    @Redemptions, @AffiliateId, @StripePromotionCodeId, @IsActive)
            ON CONFLICT (id) DO UPDATE SET
                percent = EXCLUDED.percent,
                fixed_amount_cents = EXCLUDED.fixed_amount_cents,
                applicable_product_ids = EXCLUDED.applicable_product_ids,
                valid_from = EXCLUDED.valid_from,
                valid_until = EXCLUDED.valid_until,
                max_redemptions = EXCLUDED.max_redemptions,
                redemptions = EXCLUDED.redemptions,
                affiliate_id = EXCLUDED.affiliate_id,
                stripe_promotion_code_id = EXCLUDED.stripe_promotion_code_id,
                is_active = EXCLUDED.is_active
            """,
            new
            {
                code.Id,
                code.Code,
                Kind = EnumMapping.ToDb(code.Kind),
                code.Percent,
                FixedAmountCents = code.FixedAmount?.AmountInCents,
                Currency = code.FixedAmount?.Currency ?? "EUR",
                ApplicableProductIds = code.ApplicableProductIds.ToArray(),
                code.ValidFrom,
                code.ValidUntil,
                code.MaxRedemptions,
                code.Redemptions,
                code.AffiliateId,
                code.StripePromotionCodeId,
                code.IsActive
            });
    }

    private sealed record DiscountRow(
        Guid Id,
        string Code,
        string Kind,
        decimal Percent,
        long? FixedAmountCents,
        string Currency,
        Guid[] ApplicableProductIds,
        DateTimeOffset ValidFrom,
        DateTimeOffset? ValidUntil,
        int? MaxRedemptions,
        int Redemptions,
        Guid? AffiliateId,
        string? StripePromotionCodeId,
        bool IsActive)
    {
        public DiscountCode ToDomain() => DiscountCode.Rehydrate(
            Id, Code, EnumMapping.FromDb<DiscountKind>(Kind), Percent,
            FixedAmountCents is { } cents ? Money.Create(cents, Currency).Value : null,
            ApplicableProductIds, ValidFrom, ValidUntil, MaxRedemptions, Redemptions,
            AffiliateId, StripePromotionCodeId, IsActive);
    }
}

public sealed class ReferralRepository(IDbConnectionFactory connections) : IReferralRepository
{
    private const string Columns =
        "id, visitor_id, affiliate_id, first_click_at, last_click_at, expires_at, converted_purchase_id";

    public async Task<Referral?> GetByVisitorAsync(string visitorId, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        var row = await connection.QuerySingleOrDefaultAsync<ReferralRow>(
            $"SELECT {Columns} FROM referral WHERE visitor_id = @visitorId", new { visitorId });

        return row?.ToDomain();
    }

    public async Task<int> CountClicksAsync(Guid affiliateId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        return await connection.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*)::int FROM referral
            WHERE affiliate_id = @affiliateId
              AND last_click_at >= @from AND last_click_at < @toExclusive
            """,
            new
            {
                affiliateId,
                from = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                toExclusive = to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)
            });
    }

    public async Task UpsertAsync(Referral referral, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        await connection.ExecuteAsync(
            """
            INSERT INTO referral (id, visitor_id, affiliate_id, first_click_at, last_click_at,
                                  expires_at, converted_purchase_id)
            VALUES (@Id, @VisitorId, @AffiliateId, @FirstClickAt, @LastClickAt,
                    @ExpiresAt, @ConvertedPurchaseId)
            ON CONFLICT (visitor_id) DO UPDATE SET
                affiliate_id = EXCLUDED.affiliate_id,
                last_click_at = EXCLUDED.last_click_at,
                expires_at = EXCLUDED.expires_at,
                converted_purchase_id = EXCLUDED.converted_purchase_id
            """,
            new
            {
                referral.Id,
                referral.VisitorId,
                referral.AffiliateId,
                referral.FirstClickAt,
                referral.LastClickAt,
                referral.ExpiresAt,
                referral.ConvertedPurchaseId
            });
    }

    private sealed record ReferralRow(
        Guid Id,
        string VisitorId,
        Guid AffiliateId,
        DateTimeOffset FirstClickAt,
        DateTimeOffset LastClickAt,
        DateTimeOffset ExpiresAt,
        Guid? ConvertedPurchaseId)
    {
        public Referral ToDomain() => Referral.Rehydrate(
            Id, VisitorId, AffiliateId, FirstClickAt, LastClickAt, ExpiresAt, ConvertedPurchaseId);
    }
}

public sealed class CommissionRepository(IDbConnectionFactory connections) : ICommissionRepository
{
    private const string Columns = """
        id, purchase_id, affiliate_id, net_base_cents, percent, amount_cents, currency,
        status, created_at, approvable_at, approved_at, payout_id, reversal_reason
        """;

    public async Task<IReadOnlyList<Commission>> GetForAffiliateAsync(Guid affiliateId, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        var rows = await connection.QueryAsync<CommissionRow>(
            $"SELECT {Columns} FROM commission WHERE affiliate_id = @affiliateId ORDER BY created_at DESC",
            new { affiliateId });

        return rows.Select(r => r.ToDomain()).ToList();
    }

    public async Task<IReadOnlyList<Commission>> GetByPurchaseAsync(Guid purchaseId, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        var rows = await connection.QueryAsync<CommissionRow>(
            $"SELECT {Columns} FROM commission WHERE purchase_id = @purchaseId", new { purchaseId });

        return rows.Select(r => r.ToDomain()).ToList();
    }

    public async Task<IReadOnlyList<Commission>> GetApprovableAsync(DateTimeOffset instant, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        var rows = await connection.QueryAsync<CommissionRow>(
            $"SELECT {Columns} FROM commission WHERE status = 'pending' AND approvable_at <= @instant",
            new { instant });

        return rows.Select(r => r.ToDomain()).ToList();
    }

    public async Task<IReadOnlyList<Commission>> GetPayableAsync(
        Guid affiliateId,
        DateOnly periodEnd,
        CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        var rows = await connection.QueryAsync<CommissionRow>(
            $"""
             SELECT {Columns} FROM commission
             WHERE affiliate_id = @affiliateId AND status = 'approved' AND payout_id IS NULL
               AND approved_at < @cutoff
             ORDER BY created_at, id
             """,
            new { affiliateId, cutoff = periodEnd.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) });

        return rows.Select(r => r.ToDomain()).ToList();
    }

    public async Task UpsertAsync(Commission commission, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        await connection.ExecuteAsync(
            """
            INSERT INTO commission (id, purchase_id, affiliate_id, net_base_cents, percent, amount_cents,
                                    currency, status, created_at, approvable_at, approved_at,
                                    payout_id, reversal_reason)
            VALUES (@Id, @PurchaseId, @AffiliateId, @NetBaseCents, @Percent, @AmountCents,
                    @Currency, @Status, @CreatedAt, @ApprovableAt, @ApprovedAt,
                    @PayoutId, @ReversalReason)
            ON CONFLICT (id) DO UPDATE SET
                status = EXCLUDED.status,
                approved_at = EXCLUDED.approved_at,
                payout_id = EXCLUDED.payout_id,
                reversal_reason = EXCLUDED.reversal_reason
            """,
            new
            {
                commission.Id,
                commission.PurchaseId,
                commission.AffiliateId,
                NetBaseCents = commission.NetBase.AmountInCents,
                commission.Percent,
                AmountCents = commission.Amount.AmountInCents,
                Currency = commission.Amount.Currency,
                Status = EnumMapping.ToDb(commission.Status),
                commission.CreatedAt,
                commission.ApprovableAt,
                commission.ApprovedAt,
                commission.PayoutId,
                commission.ReversalReason
            });
    }

    private sealed record CommissionRow(
        Guid Id,
        Guid PurchaseId,
        Guid AffiliateId,
        long NetBaseCents,
        decimal Percent,
        long AmountCents,
        string Currency,
        string Status,
        DateTimeOffset CreatedAt,
        DateTimeOffset ApprovableAt,
        DateTimeOffset? ApprovedAt,
        Guid? PayoutId,
        string? ReversalReason)
    {
        public Commission ToDomain() => Commission.Rehydrate(
            Id, PurchaseId, AffiliateId,
            Money.Create(NetBaseCents, Currency).Value, Percent,
            Money.Create(AmountCents, Currency).Value,
            EnumMapping.FromDb<CommissionStatus>(Status),
            CreatedAt, ApprovableAt, ApprovedAt, PayoutId, ReversalReason);
    }
}

public sealed class PayoutRepository(IDbConnectionFactory connections) : IPayoutRepository
{
    private const string Columns = """
        id, affiliate_id, period_start, period_end, total_cents, carried_over_in_cents,
        currency, status, invoice_ref, statement_content_ref, created_at, paid_at
        """;

    public async Task<IReadOnlyList<Payout>> GetForAffiliateAsync(Guid affiliateId, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        var rows = await connection.QueryAsync<PayoutRow>(
            $"SELECT {Columns} FROM payout WHERE affiliate_id = @affiliateId ORDER BY period_start DESC",
            new { affiliateId });

        return rows.Select(r => r.ToDomain()).ToList();
    }

    public async Task<Payout?> GetForPeriodAsync(Guid affiliateId, DateOnly periodStart, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        var row = await connection.QuerySingleOrDefaultAsync<PayoutRow>(
            $"SELECT {Columns} FROM payout WHERE affiliate_id = @affiliateId AND period_start = @periodStart",
            new { affiliateId, periodStart });

        return row?.ToDomain();
    }

    public async Task<Payout?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        var row = await connection.QuerySingleOrDefaultAsync<PayoutRow>(
            $"SELECT {Columns} FROM payout WHERE id = @id", new { id });

        return row?.ToDomain();
    }

    public async Task UpsertAsync(Payout payout, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        await connection.ExecuteAsync(
            """
            INSERT INTO payout (id, affiliate_id, period_start, period_end, total_cents,
                                carried_over_in_cents, currency, status, invoice_ref,
                                statement_content_ref, created_at, paid_at)
            VALUES (@Id, @AffiliateId, @PeriodStart, @PeriodEnd, @TotalCents,
                    @CarriedOverInCents, @Currency, @Status, @InvoiceRef,
                    @StatementContentRef, @CreatedAt, @PaidAt)
            ON CONFLICT (id) DO UPDATE SET
                total_cents = EXCLUDED.total_cents,
                status = EXCLUDED.status,
                invoice_ref = EXCLUDED.invoice_ref,
                statement_content_ref = EXCLUDED.statement_content_ref,
                paid_at = EXCLUDED.paid_at
            """,
            new
            {
                payout.Id,
                payout.AffiliateId,
                payout.PeriodStart,
                payout.PeriodEnd,
                TotalCents = payout.Total.AmountInCents,
                CarriedOverInCents = payout.CarriedOverIn.AmountInCents,
                Currency = payout.Total.Currency,
                Status = EnumMapping.ToDb(payout.Status),
                payout.InvoiceRef,
                payout.StatementContentRef,
                payout.CreatedAt,
                payout.PaidAt
            });
    }

    private sealed record PayoutRow(
        Guid Id,
        Guid AffiliateId,
        DateOnly PeriodStart,
        DateOnly PeriodEnd,
        long TotalCents,
        long CarriedOverInCents,
        string Currency,
        string Status,
        string? InvoiceRef,
        string? StatementContentRef,
        DateTimeOffset CreatedAt,
        DateTimeOffset? PaidAt)
    {
        public Payout ToDomain() => Payout.Rehydrate(
            Id, AffiliateId, PeriodStart, PeriodEnd,
            Money.Create(TotalCents, Currency).Value,
            Money.Create(CarriedOverInCents, Currency).Value,
            EnumMapping.FromDb<PayoutStatus>(Status),
            InvoiceRef, StatementContentRef, CreatedAt, PaidAt);
    }
}
