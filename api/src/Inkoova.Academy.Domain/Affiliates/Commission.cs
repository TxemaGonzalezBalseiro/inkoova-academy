using Inkoova.Academy.Domain.Common;
using Inkoova.Academy.Domain.ValueObjects;

namespace Inkoova.Academy.Domain.Affiliates;

public enum CommissionStatus
{
    /// <summary>Inside the 14-day withdrawal window. Not payable yet.</summary>
    Pending,

    /// <summary>Window closed without a refund. Payable.</summary>
    Approved,

    /// <summary>Included in a settled payout.</summary>
    Paid,

    /// <summary>Refund or chargeback. Never payable (T-16 antifraud).</summary>
    Reversed
}

/// <summary>
/// An affiliate's share of one purchase. Approval is time-based: the commission only
/// becomes payable once the statutory withdrawal window has closed without a refund.
/// </summary>
public sealed class Commission
{
    /// <summary>Withdrawal window, art. 102 TRLGDCU. Also the approval delay in T-16.</summary>
    public static readonly TimeSpan ApprovalDelay = TimeSpan.FromDays(14);

    public Guid Id { get; private set; }
    public Guid PurchaseId { get; private set; }
    public Guid AffiliateId { get; private set; }
    public Money NetBase { get; private set; }
    public decimal Percent { get; private set; }
    public Money Amount { get; private set; }
    public CommissionStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ApprovableAt { get; private set; }
    public DateTimeOffset? ApprovedAt { get; private set; }
    public Guid? PayoutId { get; private set; }
    public string? ReversalReason { get; private set; }

    private Commission(
        Guid id,
        Guid purchaseId,
        Guid affiliateId,
        Money netBase,
        decimal percent,
        Money amount,
        CommissionStatus status,
        DateTimeOffset createdAt,
        DateTimeOffset approvableAt,
        DateTimeOffset? approvedAt,
        Guid? payoutId,
        string? reversalReason)
    {
        Id = id;
        PurchaseId = purchaseId;
        AffiliateId = affiliateId;
        NetBase = netBase;
        Percent = percent;
        Amount = amount;
        Status = status;
        CreatedAt = createdAt;
        ApprovableAt = approvableAt;
        ApprovedAt = approvedAt;
        PayoutId = payoutId;
        ReversalReason = reversalReason;
    }

    public static Result<Commission, Error> Accrue(
        Guid id,
        Guid purchaseId,
        Guid affiliateId,
        Money netBase,
        decimal percent,
        DateTimeOffset paidAt)
    {
        if (percent is <= 0 or > 50)
        {
            return Error.Validation("commission.percent_out_of_range", "El porcentaje debe estar entre 0 y 50 %.");
        }

        if (netBase.AmountInCents <= 0)
        {
            return Error.Validation("commission.base_not_positive", "La base neta debe ser mayor que cero.");
        }

        return new Commission(
            id, purchaseId, affiliateId, netBase, percent, netBase.Percentage(percent),
            CommissionStatus.Pending, paidAt, paidAt + ApprovalDelay,
            approvedAt: null, payoutId: null, reversalReason: null);
    }

    public static Commission Rehydrate(
        Guid id,
        Guid purchaseId,
        Guid affiliateId,
        Money netBase,
        decimal percent,
        Money amount,
        CommissionStatus status,
        DateTimeOffset createdAt,
        DateTimeOffset approvableAt,
        DateTimeOffset? approvedAt,
        Guid? payoutId,
        string? reversalReason) =>
        new(id, purchaseId, affiliateId, netBase, percent, amount, status,
            createdAt, approvableAt, approvedAt, payoutId, reversalReason);

    public Result<Unit, Error> Approve(DateTimeOffset now)
    {
        if (Status == CommissionStatus.Reversed)
        {
            return Error.Conflict("commission.reversed", "Una comisión revertida no puede aprobarse.");
        }

        if (Status != CommissionStatus.Pending)
        {
            return Error.Conflict("commission.not_pending", "Solo una comisión pendiente puede aprobarse.");
        }

        if (now < ApprovableAt)
        {
            return Error.Conflict(
                "commission.window_open",
                $"La ventana de desistimiento no cierra hasta {ApprovableAt:yyyy-MM-dd}.");
        }

        Status = CommissionStatus.Approved;
        ApprovedAt = now;
        return Unit.Value;
    }

    /// <summary>
    /// A refund reverses the commission whatever its state. Reversing an already-paid one
    /// is allowed and produces a negative carry-over handled by the next settlement.
    /// </summary>
    public Result<Unit, Error> Reverse(string reason)
    {
        if (Status == CommissionStatus.Reversed)
        {
            return Error.Conflict("commission.already_reversed", "La comisión ya estaba revertida.");
        }

        Status = CommissionStatus.Reversed;
        ReversalReason = reason;
        return Unit.Value;
    }

    public Result<Unit, Error> AttachToPayout(Guid payoutId)
    {
        if (Status != CommissionStatus.Approved)
        {
            return Error.Conflict("commission.not_approved", "Solo se liquidan comisiones aprobadas.");
        }

        PayoutId = payoutId;
        Status = CommissionStatus.Paid;
        return Unit.Value;
    }

    public bool IsPayable => Status == CommissionStatus.Approved && PayoutId is null;
}
