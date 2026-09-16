using Inkoova.Academy.Application.Billing;
using Inkoova.Academy.Domain.Common;
using Stripe;
using Stripe.Checkout;

namespace Inkoova.Academy.Infrastructure.Payments;

/// <summary>
/// Verifies the webhook signature and flattens the Stripe object graph into the
/// <see cref="PaymentEvent"/> the application understands. This is the only file in the
/// solution that knows the shape of Stripe payloads.
/// </summary>
public sealed class StripeEventMapper(StripeOptions options)
{
    public Result<PaymentEvent, Error> Parse(string json, string signatureHeader)
    {
        Event stripeEvent;

        try
        {
            // throwOnApiVersionMismatch: false — the account can be upgraded in the dashboard
            // without the deploy failing; the fields we read are stable across versions.
            stripeEvent = EventUtility.ConstructEvent(
                json, signatureHeader, options.WebhookSecret, throwOnApiVersionMismatch: false);
        }
        catch (StripeException)
        {
            return Error.Unauthorized("stripe.bad_signature", "Firma de webhook inválida.");
        }

        var occurredAt = stripeEvent.Created == default
            ? DateTimeOffset.UtcNow
            : new DateTimeOffset(stripeEvent.Created, TimeSpan.Zero);

        return stripeEvent.Type switch
        {
            EventTypes.CheckoutSessionCompleted => FromCheckoutSession(stripeEvent, occurredAt),
            EventTypes.InvoicePaid => FromInvoice(stripeEvent, occurredAt, PaymentEventKind.InvoicePaid),
            EventTypes.InvoicePaymentFailed => FromInvoice(stripeEvent, occurredAt, PaymentEventKind.InvoicePaymentFailed),
            EventTypes.CustomerSubscriptionUpdated => FromSubscription(stripeEvent, occurredAt, PaymentEventKind.SubscriptionUpdated),
            EventTypes.CustomerSubscriptionDeleted => FromSubscription(stripeEvent, occurredAt, PaymentEventKind.SubscriptionDeleted),
            EventTypes.PaymentIntentSucceeded => FromPaymentIntent(stripeEvent, occurredAt),
            EventTypes.ChargeRefunded => FromCharge(stripeEvent, occurredAt, PaymentEventKind.ChargeRefunded),
            EventTypes.ChargeDisputeCreated => FromDispute(stripeEvent, occurredAt),
            _ => Ignored(stripeEvent, occurredAt)
        };
    }

    private static PaymentEvent Ignored(Event stripeEvent, DateTimeOffset occurredAt) => new()
    {
        EventId = stripeEvent.Id,
        RawType = stripeEvent.Type,
        Kind = PaymentEventKind.Ignored,
        OccurredAt = occurredAt
    };

    private static Result<PaymentEvent, Error> FromCheckoutSession(Event stripeEvent, DateTimeOffset occurredAt)
    {
        if (stripeEvent.Data.Object is not Session session)
        {
            return Ignored(stripeEvent, occurredAt);
        }

        return new PaymentEvent
        {
            EventId = stripeEvent.Id,
            RawType = stripeEvent.Type,
            Kind = PaymentEventKind.CheckoutCompleted,
            OccurredAt = occurredAt,
            CustomerId = session.CustomerId,
            CustomerEmail = session.CustomerEmail ?? session.CustomerDetails?.Email,
            SubscriptionId = session.SubscriptionId,
            PaymentIntentId = session.PaymentIntentId,
            InvoiceId = session.InvoiceId,
            AmountTotalCents = session.AmountTotal ?? 0,
            AmountTaxCents = session.TotalDetails?.AmountTax ?? 0,
            Currency = session.Currency ?? "eur",
            PromotionCode = session.Discounts?.FirstOrDefault()?.PromotionCodeId,
            Metadata = session.Metadata ?? new Dictionary<string, string>()
        };
    }

    private static Result<PaymentEvent, Error> FromInvoice(
        Event stripeEvent,
        DateTimeOffset occurredAt,
        PaymentEventKind kind)
    {
        if (stripeEvent.Data.Object is not Invoice invoice)
        {
            return Ignored(stripeEvent, occurredAt);
        }

        var line = invoice.Lines?.Data?.FirstOrDefault();

        return new PaymentEvent
        {
            EventId = stripeEvent.Id,
            RawType = stripeEvent.Type,
            Kind = kind,
            OccurredAt = occurredAt,
            CustomerId = invoice.CustomerId,
            CustomerEmail = invoice.CustomerEmail,
            SubscriptionId = ResolveSubscriptionId(invoice),
            InvoiceId = invoice.Id,
            PriceId = line?.Pricing?.PriceDetails?.PriceId,
            AmountTotalCents = invoice.AmountPaid,
            AmountTaxCents = invoice.TotalTaxes?.Sum(t => t.Amount) ?? 0,
            Currency = invoice.Currency ?? "eur",
            CurrentPeriodEnd = ResolvePeriodEnd(line),
            // Subscription metadata is what carries userId and affiliateId into renewals.
            Metadata = invoice.Parent?.SubscriptionDetails?.Metadata
                       ?? invoice.Metadata
                       ?? new Dictionary<string, string>()
        };
    }

    private static Result<PaymentEvent, Error> FromSubscription(
        Event stripeEvent,
        DateTimeOffset occurredAt,
        PaymentEventKind kind)
    {
        if (stripeEvent.Data.Object is not Subscription subscription)
        {
            return Ignored(stripeEvent, occurredAt);
        }

        var item = subscription.Items?.Data?.FirstOrDefault();

        return new PaymentEvent
        {
            EventId = stripeEvent.Id,
            RawType = stripeEvent.Type,
            Kind = kind,
            OccurredAt = occurredAt,
            CustomerId = subscription.CustomerId,
            SubscriptionId = subscription.Id,
            PriceId = item?.Price?.Id,
            SubscriptionStatus = subscription.Status,
            CancelAtPeriodEnd = subscription.CancelAtPeriodEnd,
            CurrentPeriodEnd = item?.CurrentPeriodEnd is { } end
                ? new DateTimeOffset(end, TimeSpan.Zero)
                : null,
            Currency = subscription.Currency ?? "eur",
            Metadata = subscription.Metadata ?? new Dictionary<string, string>()
        };
    }

    private static Result<PaymentEvent, Error> FromPaymentIntent(Event stripeEvent, DateTimeOffset occurredAt)
    {
        if (stripeEvent.Data.Object is not PaymentIntent intent)
        {
            return Ignored(stripeEvent, occurredAt);
        }

        return new PaymentEvent
        {
            EventId = stripeEvent.Id,
            RawType = stripeEvent.Type,
            Kind = PaymentEventKind.PaymentIntentSucceeded,
            OccurredAt = occurredAt,
            CustomerId = intent.CustomerId,
            PaymentIntentId = intent.Id,
            AmountTotalCents = intent.AmountReceived,
            Currency = intent.Currency ?? "eur",
            Metadata = intent.Metadata ?? new Dictionary<string, string>()
        };
    }

    private static Result<PaymentEvent, Error> FromCharge(
        Event stripeEvent,
        DateTimeOffset occurredAt,
        PaymentEventKind kind)
    {
        if (stripeEvent.Data.Object is not Charge charge)
        {
            return Ignored(stripeEvent, occurredAt);
        }

        return new PaymentEvent
        {
            EventId = stripeEvent.Id,
            RawType = stripeEvent.Type,
            Kind = kind,
            OccurredAt = occurredAt,
            CustomerId = charge.CustomerId,
            PaymentIntentId = charge.PaymentIntentId,
            AmountTotalCents = charge.Amount,
            AmountRefundedCents = charge.AmountRefunded,
            Currency = charge.Currency ?? "eur",
            Metadata = charge.Metadata ?? new Dictionary<string, string>()
        };
    }

    private static Result<PaymentEvent, Error> FromDispute(Event stripeEvent, DateTimeOffset occurredAt)
    {
        if (stripeEvent.Data.Object is not Dispute dispute)
        {
            return Ignored(stripeEvent, occurredAt);
        }

        return new PaymentEvent
        {
            EventId = stripeEvent.Id,
            RawType = stripeEvent.Type,
            Kind = PaymentEventKind.ChargeDisputeCreated,
            OccurredAt = occurredAt,
            PaymentIntentId = dispute.PaymentIntentId,
            AmountTotalCents = dispute.Amount,
            AmountRefundedCents = dispute.Amount,
            Currency = dispute.Currency ?? "eur",
            Metadata = dispute.Metadata ?? new Dictionary<string, string>()
        };
    }

    /// <summary>
    /// Stripe moved the subscription reference from the invoice root onto the parent object
    /// in recent API versions; read both so the mapper survives an account upgrade.
    /// </summary>
    private static string? ResolveSubscriptionId(Invoice invoice) =>
        invoice.Parent?.SubscriptionDetails?.SubscriptionId;

    private static DateTimeOffset? ResolvePeriodEnd(InvoiceLineItem? line) =>
        line?.Period?.End is { } end ? new DateTimeOffset(end, TimeSpan.Zero) : null;
}
