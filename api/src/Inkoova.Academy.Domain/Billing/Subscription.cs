using Inkoova.Academy.Domain.Common;

namespace Inkoova.Academy.Domain.Billing;

/// <summary>Mirrors the Stripe subscription status we care about.</summary>
public enum SubscriptionStatus
{
    /// <summary>Payment succeeded and the period is running.</summary>
    Active,

    /// <summary>An invoice failed. Access continues during the grace period (T-04).</summary>
    PastDue,

    /// <summary>Cancelled and the period is over. No access.</summary>
    Canceled,

    /// <summary>Stripe gave up after retries.</summary>
    Unpaid
}

/// <summary>
/// The student's subscription. Never consulted directly for access: it materialises
/// entitlements instead (ADR-007). Stripe is the source of truth for state transitions;
/// this entity only records them and computes the grace window.
/// </summary>
public sealed class Subscription
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid PlanId { get; private set; }
    public SubscriptionStatus Status { get; private set; }
    public DateTimeOffset CurrentPeriodEnd { get; private set; }
    public bool CancelAtPeriodEnd { get; private set; }
    public string StripeSubscriptionId { get; private set; }

    /// <summary>When the first invoice failed. Drives the grace period; cleared on recovery.</summary>
    public DateTimeOffset? PastDueSince { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private Subscription(
        Guid id,
        Guid userId,
        Guid planId,
        SubscriptionStatus status,
        DateTimeOffset currentPeriodEnd,
        bool cancelAtPeriodEnd,
        string stripeSubscriptionId,
        DateTimeOffset? pastDueSince,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        Id = id;
        UserId = userId;
        PlanId = planId;
        Status = status;
        CurrentPeriodEnd = currentPeriodEnd;
        CancelAtPeriodEnd = cancelAtPeriodEnd;
        StripeSubscriptionId = stripeSubscriptionId;
        PastDueSince = pastDueSince;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public static Result<Subscription, Error> Start(
        Guid id,
        Guid userId,
        Guid planId,
        string stripeSubscriptionId,
        DateTimeOffset currentPeriodEnd,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(stripeSubscriptionId))
        {
            return Error.Validation("subscription.stripe_id_empty", "Falta el identificador de suscripción de Stripe.");
        }

        if (currentPeriodEnd <= now)
        {
            return Error.Validation("subscription.period_in_past", "El fin de periodo no puede estar en el pasado.");
        }

        return new Subscription(
            id, userId, planId, SubscriptionStatus.Active, currentPeriodEnd,
            cancelAtPeriodEnd: false, stripeSubscriptionId, pastDueSince: null, now, now);
    }

    public static Subscription Rehydrate(
        Guid id,
        Guid userId,
        Guid planId,
        SubscriptionStatus status,
        DateTimeOffset currentPeriodEnd,
        bool cancelAtPeriodEnd,
        string stripeSubscriptionId,
        DateTimeOffset? pastDueSince,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt) =>
        new(id, userId, planId, status, currentPeriodEnd, cancelAtPeriodEnd,
            stripeSubscriptionId, pastDueSince, createdAt, updatedAt);

    public void Renew(DateTimeOffset newPeriodEnd, DateTimeOffset now)
    {
        CurrentPeriodEnd = newPeriodEnd;
        Status = SubscriptionStatus.Active;
        PastDueSince = null;
        UpdatedAt = now;
    }

    public void MarkPastDue(DateTimeOffset now)
    {
        Status = SubscriptionStatus.PastDue;
        PastDueSince ??= now;
        UpdatedAt = now;
    }

    public void MarkUnpaid(DateTimeOffset now)
    {
        Status = SubscriptionStatus.Unpaid;
        UpdatedAt = now;
    }

    /// <summary>Cancellation keeps access until the end of the paid period (T-04).</summary>
    public void ScheduleCancellation(DateTimeOffset now)
    {
        CancelAtPeriodEnd = true;
        UpdatedAt = now;
    }

    public void Cancel(DateTimeOffset now)
    {
        Status = SubscriptionStatus.Canceled;
        CancelAtPeriodEnd = false;
        UpdatedAt = now;
    }

    public void Resume(DateTimeOffset now)
    {
        CancelAtPeriodEnd = false;
        Status = SubscriptionStatus.Active;
        UpdatedAt = now;
    }

    /// <summary>
    /// Access window. Active runs to the end of the period; past due extends it by the
    /// configured grace so a failed card does not lock the student out immediately.
    /// </summary>
    public DateTimeOffset AccessValidUntil(TimeSpan gracePeriod) => Status switch
    {
        SubscriptionStatus.Active => CurrentPeriodEnd,
        SubscriptionStatus.PastDue => (PastDueSince ?? CurrentPeriodEnd) + gracePeriod,
        _ => CurrentPeriodEnd
    };

    public bool GrantsAccessAt(DateTimeOffset instant, TimeSpan gracePeriod) =>
        Status is SubscriptionStatus.Active or SubscriptionStatus.PastDue
        && instant <= AccessValidUntil(gracePeriod);
}
