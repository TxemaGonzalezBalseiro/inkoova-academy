using Inkoova.Academy.Domain.Common;
using Inkoova.Academy.Domain.ValueObjects;

namespace Inkoova.Academy.Domain.Affiliates;

public enum DiscountKind
{
    Percentage,
    FixedAmount
}

/// <summary>
/// Discount code, optionally tied to an affiliate. Stripe holds the promotion code; this
/// entity holds the rules the platform enforces before creating the Checkout session.
/// </summary>
public sealed class DiscountCode
{
    public Guid Id { get; private set; }
    public string Code { get; private set; }
    public DiscountKind Kind { get; private set; }

    /// <summary>Percentage when <see cref="Kind"/> is Percentage, otherwise ignored.</summary>
    public decimal Percent { get; private set; }

    /// <summary>Fixed amount when <see cref="Kind"/> is FixedAmount, otherwise ignored.</summary>
    public Money? FixedAmount { get; private set; }

    /// <summary>Empty means "any product".</summary>
    public IReadOnlySet<Guid> ApplicableProductIds { get; private set; }

    public DateTimeOffset ValidFrom { get; private set; }
    public DateTimeOffset? ValidUntil { get; private set; }

    /// <summary>Null means unlimited. Enforced before Checkout and again on the webhook.</summary>
    public int? MaxRedemptions { get; private set; }

    public int Redemptions { get; private set; }
    public Guid? AffiliateId { get; private set; }
    public string? StripePromotionCodeId { get; private set; }
    public bool IsActive { get; private set; }

    private DiscountCode(
        Guid id,
        string code,
        DiscountKind kind,
        decimal percent,
        Money? fixedAmount,
        IReadOnlySet<Guid> applicableProductIds,
        DateTimeOffset validFrom,
        DateTimeOffset? validUntil,
        int? maxRedemptions,
        int redemptions,
        Guid? affiliateId,
        string? stripePromotionCodeId,
        bool isActive)
    {
        Id = id;
        Code = code;
        Kind = kind;
        Percent = percent;
        FixedAmount = fixedAmount;
        ApplicableProductIds = applicableProductIds;
        ValidFrom = validFrom;
        ValidUntil = validUntil;
        MaxRedemptions = maxRedemptions;
        Redemptions = redemptions;
        AffiliateId = affiliateId;
        StripePromotionCodeId = stripePromotionCodeId;
        IsActive = isActive;
    }

    public static Result<DiscountCode, Error> CreatePercentage(
        Guid id,
        string code,
        decimal percent,
        IEnumerable<Guid> applicableProductIds,
        DateTimeOffset validFrom,
        DateTimeOffset? validUntil,
        int? maxRedemptions,
        Guid? affiliateId)
    {
        if (percent is <= 0 or > 100)
        {
            return Error.Validation("discount.percent_out_of_range", "El descuento debe estar entre 0 y 100 %.");
        }

        var normalized = code.Trim().ToUpperInvariant();
        if (normalized.Length is < 3 or > 40)
        {
            return Error.Validation("discount.code_length", "El código debe tener entre 3 y 40 caracteres.");
        }

        if (validUntil is not null && validUntil <= validFrom)
        {
            return Error.Validation("discount.invalid_window", "La validez debe terminar después de empezar.");
        }

        return new DiscountCode(
            id, normalized, DiscountKind.Percentage, percent, fixedAmount: null,
            applicableProductIds.ToHashSet(), validFrom, validUntil, maxRedemptions,
            redemptions: 0, affiliateId, stripePromotionCodeId: null, isActive: true);
    }

    public static DiscountCode Rehydrate(
        Guid id,
        string code,
        DiscountKind kind,
        decimal percent,
        Money? fixedAmount,
        IEnumerable<Guid> applicableProductIds,
        DateTimeOffset validFrom,
        DateTimeOffset? validUntil,
        int? maxRedemptions,
        int redemptions,
        Guid? affiliateId,
        string? stripePromotionCodeId,
        bool isActive) =>
        new(id, code, kind, percent, fixedAmount, applicableProductIds.ToHashSet(), validFrom,
            validUntil, maxRedemptions, redemptions, affiliateId, stripePromotionCodeId, isActive);

    public void LinkStripePromotionCode(string promotionCodeId) => StripePromotionCodeId = promotionCodeId;

    public Result<Unit, Error> Redeem(Guid productId, DateTimeOffset now)
    {
        if (!IsActive)
        {
            return Error.Conflict("discount.inactive", "El código de descuento no está activo.");
        }

        if (now < ValidFrom || (ValidUntil is not null && now > ValidUntil))
        {
            return Error.Conflict("discount.out_of_window", "El código de descuento no está vigente.");
        }

        if (MaxRedemptions is not null && Redemptions >= MaxRedemptions)
        {
            return Error.Conflict("discount.exhausted", "El código de descuento ha agotado sus usos.");
        }

        if (ApplicableProductIds.Count > 0 && !ApplicableProductIds.Contains(productId))
        {
            return Error.Conflict("discount.product_not_applicable", "El código no aplica a este producto.");
        }

        Redemptions++;
        return Unit.Value;
    }

    public void Deactivate() => IsActive = false;
}
