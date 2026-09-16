namespace Inkoova.Academy.Application.Billing;

public sealed record BillingOptions
{
    /// <summary>
    /// Days of access kept after a failed invoice before the student is locked out (T-04).
    /// Three by default; Stripe retries within that window.
    /// </summary>
    public int GracePeriodDays { get; init; } = 3;

    /// <summary>Where Stripe returns the customer after a successful checkout.</summary>
    public required string CheckoutSuccessUrl { get; init; }

    public required string CheckoutCancelUrl { get; init; }

    public required string BillingPortalReturnUrl { get; init; }

    /// <summary>Invoice series for Verifactu numbering (T-14).</summary>
    public string InvoiceSeries { get; init; } = "INK";

    public TimeSpan GracePeriod => TimeSpan.FromDays(GracePeriodDays);
}
