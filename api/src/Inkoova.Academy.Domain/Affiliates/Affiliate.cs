using System.Text.RegularExpressions;
using Inkoova.Academy.Domain.Common;
using Inkoova.Academy.Domain.ValueObjects;

namespace Inkoova.Academy.Domain.Affiliates;

public enum AffiliateStatus
{
    Pending,
    Active,
    Suspended
}

/// <summary>How the affiliate's fee is invoiced. Both modes are legal; the choice is per affiliate.</summary>
public enum InvoicingMode
{
    /// <summary>The affiliate issues an invoice to Inkoova.</summary>
    AffiliateIssues,

    /// <summary>Self-billing agreed in writing, art. 5 RD 1619/2012. Requires a signed agreement.</summary>
    SelfBilling
}

/// <summary>
/// An influencer who earns a share of net sales attributed to them (T-16).
/// Tax data is required before any payout can be produced, not at signup.
/// </summary>
public sealed partial class Affiliate
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }

    /// <summary>Public code used in <c>?ref=</c> and as the Stripe promotion code.</summary>
    public string Code { get; private set; }

    public decimal CommissionPercent { get; private set; }
    public AffiliateStatus Status { get; private set; }

    /// <summary>Months of recurring commission on subscription renewals. 12 by default (T-16).</summary>
    public int RecurringMonths { get; private set; }

    public string? TaxId { get; private set; }
    public string? CountryCode { get; private set; }
    public string? Iban { get; private set; }
    public InvoicingMode InvoicingMode { get; private set; }

    /// <summary>Reference to the stored self-billing agreement. Required for that mode.</summary>
    public string? SelfBillingAgreementRef { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    private Affiliate(
        Guid id,
        Guid userId,
        string code,
        decimal commissionPercent,
        AffiliateStatus status,
        int recurringMonths,
        string? taxId,
        string? countryCode,
        string? iban,
        InvoicingMode invoicingMode,
        string? selfBillingAgreementRef,
        DateTimeOffset createdAt)
    {
        Id = id;
        UserId = userId;
        Code = code;
        CommissionPercent = commissionPercent;
        Status = status;
        RecurringMonths = recurringMonths;
        TaxId = taxId;
        CountryCode = countryCode;
        Iban = iban;
        InvoicingMode = invoicingMode;
        SelfBillingAgreementRef = selfBillingAgreementRef;
        CreatedAt = createdAt;
    }

    public static Result<Affiliate, Error> Create(
        Guid id,
        Guid userId,
        string code,
        decimal commissionPercent,
        int recurringMonths,
        DateTimeOffset createdAt)
    {
        var normalized = code.Trim().ToUpperInvariant();

        if (!CodePattern().IsMatch(normalized))
        {
            return Error.Validation(
                "affiliate.code_invalid",
                "El código de afiliado admite 3 a 20 caracteres alfanuméricos en mayúsculas.");
        }

        if (commissionPercent is <= 0 or > 50)
        {
            return Error.Validation(
                "affiliate.commission_out_of_range",
                "La comisión debe estar entre 0 y 50 %.");
        }

        if (recurringMonths is < 0 or > 36)
        {
            return Error.Validation(
                "affiliate.recurring_out_of_range",
                "Los meses de comisión recurrente deben estar entre 0 y 36.");
        }

        return new Affiliate(
            id, userId, normalized, commissionPercent, AffiliateStatus.Pending, recurringMonths,
            taxId: null, countryCode: null, iban: null, InvoicingMode.AffiliateIssues,
            selfBillingAgreementRef: null, createdAt);
    }

    public static Affiliate Rehydrate(
        Guid id,
        Guid userId,
        string code,
        decimal commissionPercent,
        AffiliateStatus status,
        int recurringMonths,
        string? taxId,
        string? countryCode,
        string? iban,
        InvoicingMode invoicingMode,
        string? selfBillingAgreementRef,
        DateTimeOffset createdAt) =>
        new(id, userId, code, commissionPercent, status, recurringMonths, taxId, countryCode,
            iban, invoicingMode, selfBillingAgreementRef, createdAt);

    public Result<Unit, Error> SetTaxData(string taxId, string countryCode, string iban)
    {
        if (string.IsNullOrWhiteSpace(taxId))
        {
            return Error.Validation("affiliate.tax_id_empty", "El NIF/VAT es obligatorio para liquidar.");
        }

        if (countryCode.Length != 2)
        {
            return Error.Validation("affiliate.country_invalid", "El país debe ser un código ISO de 2 letras.");
        }

        if (string.IsNullOrWhiteSpace(iban))
        {
            return Error.Validation("affiliate.iban_empty", "El IBAN es obligatorio para liquidar.");
        }

        TaxId = taxId.Trim().ToUpperInvariant();
        CountryCode = countryCode.Trim().ToUpperInvariant();
        Iban = iban.Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
        return Unit.Value;
    }

    public Result<Unit, Error> UseSelfBilling(string agreementRef)
    {
        if (string.IsNullOrWhiteSpace(agreementRef))
        {
            return Error.Validation(
                "affiliate.self_billing_without_agreement",
                "La autofactura exige registrar el acuerdo firmado (art. 5 RD 1619/2012).");
        }

        InvoicingMode = InvoicingMode.SelfBilling;
        SelfBillingAgreementRef = agreementRef.Trim();
        return Unit.Value;
    }

    public Result<Unit, Error> Activate()
    {
        if (Status == AffiliateStatus.Suspended)
        {
            return Error.Conflict("affiliate.suspended", "Un afiliado suspendido debe rehabilitarse explícitamente.");
        }

        Status = AffiliateStatus.Active;
        return Unit.Value;
    }

    public void Suspend() => Status = AffiliateStatus.Suspended;

    public void Reinstate() => Status = AffiliateStatus.Pending;

    /// <summary>A payout can only be produced when the tax data needed to invoice is complete.</summary>
    public bool CanBePaid =>
        Status == AffiliateStatus.Active
        && !string.IsNullOrWhiteSpace(TaxId)
        && !string.IsNullOrWhiteSpace(Iban)
        && (InvoicingMode == InvoicingMode.AffiliateIssues || !string.IsNullOrWhiteSpace(SelfBillingAgreementRef));

    [GeneratedRegex("^[A-Z0-9]{3,20}$")]
    private static partial Regex CodePattern();
}
