using Inkoova.Academy.Application.Abstractions;
using Inkoova.Academy.Application.Billing;
using Inkoova.Academy.Domain.Billing;
using Inkoova.Academy.Domain.Common;
using Inkoova.Academy.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Stripe;
using Stripe.Checkout;

namespace Inkoova.Academy.Infrastructure.Payments;

public sealed record StripeOptions
{
    public required string SecretKey { get; init; }

    public required string WebhookSecret { get; init; }

    /// <summary>Stripe Tax computes EU VAT/OSS on checkout (T-14).</summary>
    public bool EnableAutomaticTax { get; init; } = true;
}

/// <summary>
/// Stripe behind the <see cref="IPaymentGateway"/> port. Every write passes an idempotency
/// key derived from the operation, so a retried request cannot create a second customer,
/// price or session.
/// </summary>
public sealed class StripePaymentGateway : IPaymentGateway
{
    private readonly StripeOptions _options;
    private readonly ILogger<StripePaymentGateway> _logger;
    private readonly CustomerService _customers;
    private readonly SessionService _sessions;
    private readonly Stripe.BillingPortal.SessionService _portal;
    private readonly ProductService _products;
    private readonly PriceService _prices;
    private readonly CouponService _coupons;
    private readonly PromotionCodeService _promotionCodes;

    public StripePaymentGateway(StripeOptions options, ILogger<StripePaymentGateway> logger)
    {
        _options = options;
        _logger = logger;

        var client = new StripeClient(options.SecretKey);
        _customers = new CustomerService(client);
        _sessions = new SessionService(client);
        _portal = new Stripe.BillingPortal.SessionService(client);
        _products = new ProductService(client);
        _prices = new PriceService(client);
        _coupons = new CouponService(client);
        _promotionCodes = new PromotionCodeService(client);
    }

    public async Task<Result<CheckoutSessionResult, Error>> CreateCheckoutSessionAsync(
        CheckoutRequest request,
        CancellationToken ct)
    {
        try
        {
            var options = new SessionCreateOptions
            {
                Mode = request.IsSubscription ? "subscription" : "payment",
                Customer = request.StripeCustomerId,
                CustomerEmail = request.StripeCustomerId is null ? request.UserEmail : null,
                SuccessUrl = request.SuccessUrl,
                CancelUrl = request.CancelUrl,
                LineItems = [new SessionLineItemOptions { Price = request.PriceId, Quantity = 1 }],
                Metadata = new Dictionary<string, string>(request.Metadata),
                AutomaticTax = new SessionAutomaticTaxOptions { Enabled = _options.EnableAutomaticTax },
                ClientReferenceId = request.UserId.ToString()
            };

            if (request.IsSubscription)
            {
                // Metadata on the session does not reach invoice events; copying it onto the
                // subscription is what lets renewals resolve the user and the affiliate.
                options.SubscriptionData = new SessionSubscriptionDataOptions
                {
                    Metadata = new Dictionary<string, string>(request.Metadata)
                };
            }
            else
            {
                options.PaymentIntentData = new SessionPaymentIntentDataOptions
                {
                    Metadata = new Dictionary<string, string>(request.Metadata)
                };
                options.InvoiceCreation = new SessionInvoiceCreationOptions { Enabled = true };
            }

            if (!string.IsNullOrWhiteSpace(request.PromotionCodeId))
            {
                options.Discounts = [new SessionDiscountOptions { PromotionCode = request.PromotionCodeId }];
            }
            else
            {
                options.AllowPromotionCodes = true;
            }

            var session = await _sessions.CreateAsync(options, cancellationToken: ct);

            return new CheckoutSessionResult(
                session.Id,
                session.Url,
                session.CustomerId ?? request.StripeCustomerId ?? string.Empty);
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe checkout session failed for user {UserId}.", request.UserId);
            return Error.Unexpected("stripe.checkout_failed", "No se ha podido iniciar el pago. Inténtalo de nuevo.");
        }
    }

    public async Task<Result<string, Error>> CreateBillingPortalSessionAsync(
        string customerId,
        string returnUrl,
        CancellationToken ct)
    {
        try
        {
            var session = await _portal.CreateAsync(
                new Stripe.BillingPortal.SessionCreateOptions { Customer = customerId, ReturnUrl = returnUrl },
                cancellationToken: ct);

            return session.Url;
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe billing portal failed for customer {CustomerId}.", customerId);
            return Error.Unexpected("stripe.portal_failed", "No se ha podido abrir el portal de facturación.");
        }
    }

    public async Task<Result<string, Error>> EnsureCustomerAsync(
        Guid userId,
        string email,
        string displayName,
        CancellationToken ct)
    {
        try
        {
            // Search by our own id rather than by email: two accounts can share an email in
            // Stripe, and we need the one this platform created.
            var existing = await _customers.SearchAsync(
                new CustomerSearchOptions { Query = $"metadata['{PaymentMetadataKeys.UserId}']:'{userId}'" },
                cancellationToken: ct);

            if (existing.Data.Count > 0)
            {
                return existing.Data[0].Id;
            }

            var created = await _customers.CreateAsync(
                new CustomerCreateOptions
                {
                    Email = email,
                    Name = displayName,
                    Metadata = new Dictionary<string, string> { [PaymentMetadataKeys.UserId] = userId.ToString() }
                },
                new RequestOptions { IdempotencyKey = $"customer:{userId}" },
                ct);

            return created.Id;
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe customer creation failed for user {UserId}.", userId);
            return Error.Unexpected("stripe.customer_failed", "No se ha podido crear el cliente en Stripe.");
        }
    }

    /// <summary>
    /// Idempotent product and price creation for the seed script (T-04): the lookup key is
    /// the identity of the price, so re-running the script never creates duplicates.
    /// </summary>
    public async Task<Result<string, Error>> EnsurePriceAsync(
        string productName,
        string lookupKey,
        Money price,
        BillingInterval? recurringInterval,
        CancellationToken ct)
    {
        try
        {
            var existing = await _prices.ListAsync(
                new PriceListOptions { LookupKeys = [lookupKey], Active = true },
                cancellationToken: ct);

            if (existing.Data.Count > 0)
            {
                return existing.Data[0].Id;
            }

            var product = await _products.CreateAsync(
                new ProductCreateOptions { Name = productName },
                new RequestOptions { IdempotencyKey = $"product:{lookupKey}" },
                ct);

            var options = new PriceCreateOptions
            {
                Product = product.Id,
                UnitAmount = price.AmountInCents,
                Currency = price.Currency.ToLowerInvariant(),
                LookupKey = lookupKey,
                TaxBehavior = "inclusive"
            };

            if (recurringInterval is { } interval && interval != BillingInterval.Lifetime)
            {
                options.Recurring = ToRecurring(interval);
            }

            var created = await _prices.CreateAsync(
                options, new RequestOptions { IdempotencyKey = $"price:{lookupKey}" }, ct);

            return created.Id;
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe price creation failed for {LookupKey}.", lookupKey);
            return Error.Unexpected("stripe.price_failed", "No se ha podido crear el precio en Stripe.");
        }
    }

    public async Task<Result<string, Error>> EnsurePromotionCodeAsync(
        string code,
        decimal percentOff,
        Guid? affiliateId,
        CancellationToken ct)
    {
        try
        {
            var existing = await _promotionCodes.ListAsync(
                new PromotionCodeListOptions { Code = code, Active = true }, cancellationToken: ct);

            if (existing.Data.Count > 0)
            {
                return existing.Data[0].Id;
            }

            var coupon = await _coupons.CreateAsync(
                new CouponCreateOptions { PercentOff = percentOff, Duration = "once", Name = code },
                new RequestOptions { IdempotencyKey = $"coupon:{code}" },
                ct);

            var metadata = new Dictionary<string, string>(StringComparer.Ordinal);
            if (affiliateId is { } id)
            {
                // Attribution travels with the code, so a purchase made with it is credited
                // even when the referral cookie is gone (T-16 priority rule).
                metadata[PaymentMetadataKeys.AffiliateId] = id.ToString();
            }

            var promotion = await _promotionCodes.CreateAsync(
                new PromotionCodeCreateOptions
                {
                    Promotion = new PromotionCodePromotionOptions { Coupon = coupon.Id, Type = "coupon" },
                    Code = code,
                    Metadata = metadata
                },
                new RequestOptions { IdempotencyKey = $"promo:{code}" },
                ct);

            return promotion.Id;
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe promotion code creation failed for {Code}.", code);
            return Error.Unexpected("stripe.promotion_failed", "No se ha podido crear el código en Stripe.");
        }
    }

    private static PriceRecurringOptions ToRecurring(BillingInterval interval) => interval switch
    {
        BillingInterval.Monthly => new PriceRecurringOptions { Interval = "month", IntervalCount = 1 },
        BillingInterval.Quarterly => new PriceRecurringOptions { Interval = "month", IntervalCount = 3 },
        BillingInterval.Biannual => new PriceRecurringOptions { Interval = "month", IntervalCount = 6 },
        BillingInterval.Yearly => new PriceRecurringOptions { Interval = "year", IntervalCount = 1 },
        _ => throw new ArgumentOutOfRangeException(nameof(interval), interval, "Lifetime no es un intervalo recurrente.")
    };
}
