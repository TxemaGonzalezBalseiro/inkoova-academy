namespace Inkoova.Academy.Application.Billing;

/// <summary>
/// Stripe events the platform reacts to. Anything else is acknowledged and ignored, so a
/// newly enabled event type in the dashboard cannot break the endpoint.
/// </summary>
public enum PaymentEventKind
{
    CheckoutCompleted,
    InvoicePaid,
    InvoicePaymentFailed,
    SubscriptionUpdated,
    SubscriptionDeleted,
    PaymentIntentSucceeded,
    ChargeRefunded,
    ChargeDisputeCreated,
    Ignored
}

/// <summary>
/// Stripe payload normalised at the infrastructure boundary. Keeping the SDK types out of
/// Application is what lets the webhook tests replay recorded fixtures without the library.
/// </summary>
public sealed record PaymentEvent
{
    public required string EventId { get; init; }

    public required string RawType { get; init; }

    public required PaymentEventKind Kind { get; init; }

    public required DateTimeOffset OccurredAt { get; init; }

    public string? CustomerId { get; init; }

    public string? CustomerEmail { get; init; }

    public string? SubscriptionId { get; init; }

    public string? PaymentIntentId { get; init; }

    public string? InvoiceId { get; init; }

    /// <summary>Stripe price the line item refers to. Used to resolve the plan or the product.</summary>
    public string? PriceId { get; init; }

    /// <summary>Amount actually collected, in minor units.</summary>
    public long AmountTotalCents { get; init; }

    public long AmountTaxCents { get; init; }

    public long AmountFeeCents { get; init; }

    public long AmountRefundedCents { get; init; }

    public string Currency { get; init; } = "eur";

    public DateTimeOffset? CurrentPeriodEnd { get; init; }

    public bool CancelAtPeriodEnd { get; init; }

    /// <summary>Stripe subscription status verbatim: active, past_due, canceled, unpaid, ...</summary>
    public string? SubscriptionStatus { get; init; }

    public string? PromotionCode { get; init; }

    /// <summary>Metadata we set when creating the session: userId, productSlug, affiliateId.</summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}

public static class PaymentMetadataKeys
{
    public const string UserId = "inkoova_user_id";
    public const string ProductSlug = "inkoova_product_slug";
    public const string PlanCode = "inkoova_plan_code";
    public const string AffiliateId = "inkoova_affiliate_id";
    public const string VisitorId = "inkoova_visitor_id";
}
