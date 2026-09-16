using Inkoova.Academy.Application.Abstractions;
using Inkoova.Academy.Application.Access;
using Inkoova.Academy.Application.Affiliates;
using Inkoova.Academy.Domain.Billing;
using Inkoova.Academy.Domain.Common;
using Inkoova.Academy.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Inkoova.Academy.Application.Billing;

/// <summary>
/// Single entry point for Stripe events. Idempotent by construction: the event ledger admits
/// an id once, so a replayed event is acknowledged without touching state (T-04 acceptance
/// criteria). Everything downstream — entitlements, commissions, invoices — is driven here so
/// there is one place to read when asking "what happens when a payment lands?".
/// </summary>
public sealed class StripeWebhookProcessor(
    IStripeEventStore events,
    IUserRepository users,
    IPlanRepository plans,
    IProductRepository products,
    ISubscriptionRepository subscriptions,
    IPurchaseRepository purchases,
    EntitlementService entitlements,
    PlanEntitlementService planEntitlements,
    CommissionService commissions,
    FiscalInvoiceService invoicing,
    IEmailSender email,
    IEmailTemplateRenderer templates,
    IClock clock,
    BillingOptions options,
    ILogger<StripeWebhookProcessor> logger)
{
    public async Task<Result<Unit, Error>> ProcessAsync(PaymentEvent evt, CancellationToken ct)
    {
        var isNew = await events.TryRecordAsync(evt.EventId, evt.RawType, clock.UtcNow, ct);
        if (!isNew)
        {
            logger.LogInformation("Stripe event {EventId} already processed; ignoring replay.", evt.EventId);
            return Unit.Value;
        }

        try
        {
            var result = evt.Kind switch
            {
                PaymentEventKind.CheckoutCompleted => await OnCheckoutCompletedAsync(evt, ct),
                PaymentEventKind.PaymentIntentSucceeded => await OnPaymentIntentSucceededAsync(evt, ct),
                PaymentEventKind.InvoicePaid => await OnInvoicePaidAsync(evt, ct),
                PaymentEventKind.InvoicePaymentFailed => await OnInvoiceFailedAsync(evt, ct),
                PaymentEventKind.SubscriptionUpdated => await OnSubscriptionUpdatedAsync(evt, ct),
                PaymentEventKind.SubscriptionDeleted => await OnSubscriptionDeletedAsync(evt, ct),
                PaymentEventKind.ChargeRefunded => await OnRefundAsync(evt, ct),
                PaymentEventKind.ChargeDisputeCreated => await OnDisputeAsync(evt, ct),
                _ => Result.Ok(Unit.Value)
            };

            if (result.IsFailure)
            {
                await events.MarkFailedAsync(evt.EventId, result.Error.ToString(), ct);
                return result;
            }

            await events.MarkProcessedAsync(evt.EventId, clock.UtcNow, ct);
            return Unit.Value;
        }
        catch (Exception ex)
        {
            // The ledger keeps the failure so the event can be replayed from the Stripe dashboard.
            await events.MarkFailedAsync(evt.EventId, ex.Message, ct);
            throw;
        }
    }

    // ── checkout.session.completed ──────────────────────────────────────────────────────

    private async Task<Result<Unit, Error>> OnCheckoutCompletedAsync(PaymentEvent evt, CancellationToken ct)
    {
        var userId = ResolveUserId(evt);
        if (userId is null)
        {
            logger.LogWarning("Checkout {EventId} without user metadata; nothing to grant.", evt.EventId);
            return Unit.Value;
        }

        await LinkCustomerAsync(userId.Value, evt.CustomerId, ct);

        // A one-off product purchase. Subscriptions are granted by invoice.paid instead,
        // so that a renewal and a first payment take the same code path.
        if (evt.Metadata.TryGetValue(PaymentMetadataKeys.ProductSlug, out var productSlug))
        {
            return await RecordOneOffPurchaseAsync(userId.Value, productSlug, evt, ct);
        }

        // Lifetime is sold as a one-off payment against a plan code.
        if (evt.Metadata.TryGetValue(PaymentMetadataKeys.PlanCode, out var planCode))
        {
            var plan = await plans.GetByCodeAsync(planCode, ct);
            if (plan is { Interval: BillingInterval.Lifetime })
            {
                var applied = await planEntitlements.ApplyLifetimeAsync(userId.Value, plan, ct);
                if (applied.IsFailure)
                {
                    return applied.Error;
                }

                await SendWelcomeAsync(userId.Value, plan.Name, ct);
            }
        }

        return Unit.Value;
    }

    private async Task<Result<Unit, Error>> OnPaymentIntentSucceededAsync(PaymentEvent evt, CancellationToken ct)
    {
        // Safety net for one-off purchases whose checkout event we never saw. Recording is
        // keyed on the payment intent, so this cannot duplicate what checkout already wrote.
        if (!evt.Metadata.TryGetValue(PaymentMetadataKeys.ProductSlug, out var productSlug))
        {
            return Unit.Value;
        }

        var userId = ResolveUserId(evt);
        return userId is null
            ? Result.Ok(Unit.Value)
            : await RecordOneOffPurchaseAsync(userId.Value, productSlug, evt, ct);
    }

    private async Task<Result<Unit, Error>> RecordOneOffPurchaseAsync(
        Guid userId,
        string productSlug,
        PaymentEvent evt,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(evt.PaymentIntentId))
        {
            return Error.Validation("webhook.no_payment_intent", "El evento no trae PaymentIntent.");
        }

        var existing = await purchases.GetByPaymentIntentAsync(evt.PaymentIntentId, ct);
        if (existing is not null)
        {
            return Unit.Value;
        }

        var slug = Slug.Create(productSlug);
        if (slug.IsFailure)
        {
            return slug.Error;
        }

        var product = await products.GetBySlugAsync(slug.Value, ct);
        if (product is null)
        {
            return Error.NotFound("product.not_found", $"El webhook referencia un producto inexistente: {productSlug}.");
        }

        var currency = evt.Currency.ToUpperInvariant();
        var purchase = Purchase.Record(
            Guid.CreateVersion7(),
            userId,
            product.Id,
            Money.Create(evt.AmountTotalCents, currency).Value,
            Money.Create(evt.AmountTaxCents, currency).Value,
            Money.Create(evt.AmountFeeCents, currency).Value,
            evt.PaymentIntentId,
            evt.PromotionCode,
            evt.OccurredAt);

        if (purchase.IsFailure)
        {
            return purchase.Error;
        }

        await purchases.UpsertAsync(purchase.Value, ct);

        var granted = await entitlements.GrantFromPurchaseAsync(userId, product.Id, ct);
        if (granted.IsFailure)
        {
            return granted.Error;
        }

        await commissions.AccrueForPurchaseAsync(purchase.Value, evt.Metadata, ct);
        await SendWelcomeAsync(userId, product.Title, ct);

        return Unit.Value;
    }

    // ── invoice.paid ───────────────────────────────────────────────────────────────────

    private async Task<Result<Unit, Error>> OnInvoicePaidAsync(PaymentEvent evt, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(evt.SubscriptionId))
        {
            // A one-off invoice; the purchase path already handled it.
            return Unit.Value;
        }

        var userId = await ResolveUserIdFromCustomerAsync(evt, ct);
        if (userId is null)
        {
            return Error.NotFound("user.not_found", "No hay usuario asociado al cliente de Stripe del evento.");
        }

        var plan = evt.PriceId is null ? null : await plans.GetByStripePriceIdAsync(evt.PriceId, ct);
        if (plan is null)
        {
            return Error.NotFound("plan.not_found", "El precio del evento no corresponde a ningún plan.");
        }

        var periodEnd = evt.CurrentPeriodEnd ?? evt.OccurredAt + (plan.PeriodLength ?? TimeSpan.FromDays(30));

        var subscription = await subscriptions.GetByStripeIdAsync(evt.SubscriptionId, ct);
        if (subscription is null)
        {
            var started = Subscription.Start(
                Guid.CreateVersion7(), userId.Value, plan.Id, evt.SubscriptionId, periodEnd, evt.OccurredAt);

            if (started.IsFailure)
            {
                return started.Error;
            }

            subscription = started.Value;
            await SendWelcomeAsync(userId.Value, plan.Name, ct);
        }
        else
        {
            subscription.Renew(periodEnd, clock.UtcNow);
        }

        await subscriptions.UpsertAsync(subscription, ct);

        // Entitlements are extended to the end of the paid period plus the grace window, so a
        // failed renewal does not lock the student out the same second the period rolls over.
        var applied = await planEntitlements.ApplyAsync(
            userId.Value, plan, periodEnd + options.GracePeriod, ct);

        if (applied.IsFailure)
        {
            return applied.Error;
        }

        await commissions.AccrueForRenewalAsync(userId.Value, subscription, evt, plan, ct);

        // La factura fiscal se emite después de conceder el acceso y nunca puede tumbar el
        // webhook: si falla, el alumno ya está dentro y la factura se reemite a mano (T-14).
        if (!string.IsNullOrWhiteSpace(evt.InvoiceId))
        {
            var currency = evt.Currency.ToUpperInvariant();

            await invoicing.IssueForPaymentAsync(
                userId.Value,
                evt.InvoiceId,
                Money.Create(evt.AmountTotalCents, currency).Value,
                Money.Create(evt.AmountTaxCents, currency).Value,
                $"Suscripción Inkoova Academy · {plan.Name}",
                evt.OccurredAt,
                // TODO(T-14): Stripe Tax devuelve el país del cliente; mapearlo aquí cuando
                // el evento lo incluya, en vez de asumir España.
                customerCountry: "ES",
                ct);
        }

        return Unit.Value;
    }

    private async Task<Result<Unit, Error>> OnInvoiceFailedAsync(PaymentEvent evt, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(evt.SubscriptionId))
        {
            return Unit.Value;
        }

        var subscription = await subscriptions.GetByStripeIdAsync(evt.SubscriptionId, ct);
        if (subscription is null)
        {
            return Unit.Value;
        }

        subscription.MarkPastDue(clock.UtcNow);
        await subscriptions.UpsertAsync(subscription, ct);

        var user = await users.GetByIdAsync(subscription.UserId, ct);
        if (user is not null)
        {
            await SendTemplateAsync(user.Email, "payment-failed", new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["nombre"] = user.DisplayName,
                ["dias_gracia"] = options.GracePeriodDays.ToString(System.Globalization.CultureInfo.InvariantCulture)
            }, ct);
        }

        return Unit.Value;
    }

    // ── customer.subscription.* ────────────────────────────────────────────────────────

    private async Task<Result<Unit, Error>> OnSubscriptionUpdatedAsync(PaymentEvent evt, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(evt.SubscriptionId))
        {
            return Unit.Value;
        }

        var subscription = await subscriptions.GetByStripeIdAsync(evt.SubscriptionId, ct);
        if (subscription is null)
        {
            return Unit.Value;
        }

        if (evt.CurrentPeriodEnd is { } periodEnd && evt.SubscriptionStatus == "active")
        {
            subscription.Renew(periodEnd, clock.UtcNow);
        }

        switch (evt.SubscriptionStatus)
        {
            case "past_due":
                subscription.MarkPastDue(clock.UtcNow);
                break;
            case "unpaid":
                subscription.MarkUnpaid(clock.UtcNow);
                break;
            case "canceled":
                subscription.Cancel(clock.UtcNow);
                break;
        }

        if (evt.CancelAtPeriodEnd)
        {
            subscription.ScheduleCancellation(clock.UtcNow);
        }
        else if (subscription.CancelAtPeriodEnd && evt.SubscriptionStatus == "active")
        {
            subscription.Resume(clock.UtcNow);
        }

        await subscriptions.UpsertAsync(subscription, ct);
        return Unit.Value;
    }

    private async Task<Result<Unit, Error>> OnSubscriptionDeletedAsync(PaymentEvent evt, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(evt.SubscriptionId))
        {
            return Unit.Value;
        }

        var subscription = await subscriptions.GetByStripeIdAsync(evt.SubscriptionId, ct);
        if (subscription is null)
        {
            return Unit.Value;
        }

        subscription.Cancel(clock.UtcNow);
        await subscriptions.UpsertAsync(subscription, ct);

        // Access is NOT withdrawn here. Entitlements already carry the paid period as their
        // expiry, so cancelling keeps access until currentPeriodEnd (T-04 acceptance criteria).
        // The daily job revokes them once that date passes.

        var user = await users.GetByIdAsync(subscription.UserId, ct);
        if (user is not null)
        {
            await SendTemplateAsync(user.Email, "subscription-cancelled", new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["nombre"] = user.DisplayName,
                ["fin_acceso"] = subscription.CurrentPeriodEnd.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture)
            }, ct);
        }

        return Unit.Value;
    }

    // ── refunds and disputes ───────────────────────────────────────────────────────────

    private async Task<Result<Unit, Error>> OnRefundAsync(PaymentEvent evt, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(evt.PaymentIntentId))
        {
            return Unit.Value;
        }

        var purchase = await purchases.GetByPaymentIntentAsync(evt.PaymentIntentId, ct);
        if (purchase is null)
        {
            return Unit.Value;
        }

        var refunded = Money.Create(evt.AmountRefundedCents, evt.Currency.ToUpperInvariant());
        if (refunded.IsFailure)
        {
            return refunded.Error;
        }

        var applied = purchase.Refund(refunded.Value, clock.UtcNow);
        if (applied.IsFailure)
        {
            return applied.Error;
        }

        await purchases.UpsertAsync(purchase, ct);
        await commissions.ReverseForPurchaseAsync(purchase.Id, "reembolso", ct);

        if (purchase.Status == PurchaseStatus.Refunded)
        {
            var revoked = await entitlements.RevokeAsync(purchase.UserId, purchase.ProductId, "reembolso", ct);
            if (revoked.IsFailure)
            {
                return revoked.Error;
            }
        }

        // Rectificativa (T-14). El identificador de factura de Stripe es el que enlaza la
        // original con esta; sin él no hay nada que rectificar.
        if (!string.IsNullOrWhiteSpace(evt.InvoiceId))
        {
            await invoicing.IssueRectificationAsync(
                purchase.UserId,
                evt.InvoiceId,
                refunded.Value,
                purchase.TaxAmount,
                clock.UtcNow,
                customerCountry: "ES",
                ct);
        }

        return Unit.Value;
    }

    private async Task<Result<Unit, Error>> OnDisputeAsync(PaymentEvent evt, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(evt.PaymentIntentId))
        {
            return Unit.Value;
        }

        var purchase = await purchases.GetByPaymentIntentAsync(evt.PaymentIntentId, ct);
        if (purchase is null)
        {
            return Unit.Value;
        }

        purchase.MarkChargedBack(clock.UtcNow);
        await purchases.UpsertAsync(purchase, ct);
        await commissions.ReverseForPurchaseAsync(purchase.Id, "chargeback", ct);

        return await entitlements.RevokeAsync(purchase.UserId, purchase.ProductId, "chargeback", ct);
    }

    // ── helpers ────────────────────────────────────────────────────────────────────────

    private static Guid? ResolveUserId(PaymentEvent evt) =>
        evt.Metadata.TryGetValue(PaymentMetadataKeys.UserId, out var raw) && Guid.TryParse(raw, out var id)
            ? id
            : null;

    private async Task<Guid?> ResolveUserIdFromCustomerAsync(PaymentEvent evt, CancellationToken ct)
    {
        if (ResolveUserId(evt) is { } fromMetadata)
        {
            return fromMetadata;
        }

        if (!string.IsNullOrWhiteSpace(evt.CustomerId))
        {
            var byCustomer = await users.GetByStripeCustomerIdAsync(evt.CustomerId, ct);
            if (byCustomer is not null)
            {
                return byCustomer.Id;
            }
        }

        if (!string.IsNullOrWhiteSpace(evt.CustomerEmail))
        {
            var byEmail = await users.GetByEmailAsync(evt.CustomerEmail, ct);
            if (byEmail is not null)
            {
                return byEmail.Id;
            }
        }

        return null;
    }

    private async Task LinkCustomerAsync(Guid userId, string? customerId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(customerId))
        {
            return;
        }

        var user = await users.GetByIdAsync(userId, ct);
        if (user is null || user.StripeCustomerId == customerId)
        {
            return;
        }

        user.LinkStripeCustomer(customerId);
        await users.UpsertAsync(user, ct);
    }

    private async Task SendWelcomeAsync(Guid userId, string what, CancellationToken ct)
    {
        var user = await users.GetByIdAsync(userId, ct);
        if (user is null)
        {
            return;
        }

        await SendTemplateAsync(user.Email, "welcome", new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["nombre"] = user.DisplayName,
            ["producto"] = what
        }, ct);
    }

    /// <summary>
    /// Email failures are logged, never propagated: a transactional email that does not go
    /// out must not make Stripe retry a webhook that already changed state correctly.
    /// </summary>
    private async Task SendTemplateAsync(
        string to,
        string template,
        IReadOnlyDictionary<string, string> model,
        CancellationToken ct)
    {
        var rendered = await templates.RenderAsync(template, model, ct);
        if (rendered.IsFailure)
        {
            logger.LogWarning("Email template {Template} could not be rendered: {Error}", template, rendered.Error);
            return;
        }

        var sent = await email.SendAsync(
            new EmailMessage(to, rendered.Value.Subject, rendered.Value.Html, rendered.Value.Text), ct);

        if (sent.IsFailure)
        {
            logger.LogWarning("Email {Template} to {To} failed: {Error}", template, to, sent.Error);
        }
    }
}
