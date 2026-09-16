using Inkoova.Academy.Domain.Common;
using Inkoova.Academy.Domain.ValueObjects;

namespace Inkoova.Academy.Domain.Billing;

public enum PurchaseStatus
{
    Paid,
    Refunded,
    PartiallyRefunded,
    ChargedBack
}

/// <summary>
/// A one-off payment: single course, sector pack, pack bundle or lifetime program.
/// It is also the base for affiliate commissions (T-16), so it stores the fee and tax
/// that Stripe reported rather than recomputing them later.
/// </summary>
public sealed class Purchase
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid ProductId { get; private set; }

    /// <summary>What the customer paid, taxes included.</summary>
    public Money GrossAmount { get; private set; }

    /// <summary>VAT reported by Stripe Tax. Excluded from the commission base (T-16).</summary>
    public Money TaxAmount { get; private set; }

    /// <summary>Stripe processing fee. Also excluded from the commission base.</summary>
    public Money ProcessingFee { get; private set; }

    public Money RefundedAmount { get; private set; }
    public PurchaseStatus Status { get; private set; }
    public string StripePaymentIntentId { get; private set; }
    public string? DiscountCodeUsed { get; private set; }
    public DateTimeOffset PaidAt { get; private set; }
    public DateTimeOffset? RefundedAt { get; private set; }

    private Purchase(
        Guid id,
        Guid userId,
        Guid productId,
        Money grossAmount,
        Money taxAmount,
        Money processingFee,
        Money refundedAmount,
        PurchaseStatus status,
        string stripePaymentIntentId,
        string? discountCodeUsed,
        DateTimeOffset paidAt,
        DateTimeOffset? refundedAt)
    {
        Id = id;
        UserId = userId;
        ProductId = productId;
        GrossAmount = grossAmount;
        TaxAmount = taxAmount;
        ProcessingFee = processingFee;
        RefundedAmount = refundedAmount;
        Status = status;
        StripePaymentIntentId = stripePaymentIntentId;
        DiscountCodeUsed = discountCodeUsed;
        PaidAt = paidAt;
        RefundedAt = refundedAt;
    }

    public static Result<Purchase, Error> Record(
        Guid id,
        Guid userId,
        Guid productId,
        Money grossAmount,
        Money taxAmount,
        Money processingFee,
        string stripePaymentIntentId,
        string? discountCodeUsed,
        DateTimeOffset paidAt)
    {
        if (string.IsNullOrWhiteSpace(stripePaymentIntentId))
        {
            return Error.Validation("purchase.payment_intent_empty", "Falta el PaymentIntent de Stripe.");
        }

        if (grossAmount.AmountInCents <= 0)
        {
            return Error.Validation("purchase.amount_invalid", "El importe de la compra debe ser mayor que cero.");
        }

        if (taxAmount.AmountInCents + processingFee.AmountInCents > grossAmount.AmountInCents)
        {
            return Error.Validation(
                "purchase.deductions_exceed_gross",
                "Impuestos y comisión no pueden superar el importe cobrado.");
        }

        return new Purchase(
            id, userId, productId, grossAmount, taxAmount, processingFee,
            Money.Zero(grossAmount.Currency), PurchaseStatus.Paid,
            stripePaymentIntentId, discountCodeUsed, paidAt, refundedAt: null);
    }

    public static Purchase Rehydrate(
        Guid id,
        Guid userId,
        Guid productId,
        Money grossAmount,
        Money taxAmount,
        Money processingFee,
        Money refundedAmount,
        PurchaseStatus status,
        string stripePaymentIntentId,
        string? discountCodeUsed,
        DateTimeOffset paidAt,
        DateTimeOffset? refundedAt) =>
        new(id, userId, productId, grossAmount, taxAmount, processingFee, refundedAmount,
            status, stripePaymentIntentId, discountCodeUsed, paidAt, refundedAt);

    public Result<Unit, Error> Refund(Money amount, DateTimeOffset now)
    {
        var total = RefundedAmount.Add(amount);
        if (total.IsFailure)
        {
            return total.Error;
        }

        if (total.Value.AmountInCents > GrossAmount.AmountInCents)
        {
            return Error.Validation("purchase.refund_exceeds_gross", "El reembolso supera el importe cobrado.");
        }

        RefundedAmount = total.Value;
        RefundedAt = now;
        Status = RefundedAmount.AmountInCents == GrossAmount.AmountInCents
            ? PurchaseStatus.Refunded
            : PurchaseStatus.PartiallyRefunded;

        return Unit.Value;
    }

    public void MarkChargedBack(DateTimeOffset now)
    {
        Status = PurchaseStatus.ChargedBack;
        RefundedAt = now;
    }

    /// <summary>
    /// Commission base: what was collected, minus VAT, minus the processor fee, minus refunds.
    /// Defined once here so the settlement job and the affiliate dashboard cannot drift (T-16).
    /// </summary>
    public Money NetBase()
    {
        var afterTax = GrossAmount.Subtract(TaxAmount);
        if (afterTax.IsFailure)
        {
            return Money.Zero(GrossAmount.Currency);
        }

        var afterFee = afterTax.Value.Subtract(ProcessingFee);
        if (afterFee.IsFailure)
        {
            return Money.Zero(GrossAmount.Currency);
        }

        var afterRefund = afterFee.Value.Subtract(RefundedAmount);
        return afterRefund.IsFailure ? Money.Zero(GrossAmount.Currency) : afterRefund.Value;
    }

    /// <summary>A refunded or charged-back purchase never pays commission (T-16 antifraud).</summary>
    public bool IsCommissionable => Status == PurchaseStatus.Paid && NetBase().AmountInCents > 0;
}
