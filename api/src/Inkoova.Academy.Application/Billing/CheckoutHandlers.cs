using Inkoova.Academy.Application.Abstractions;
using Inkoova.Academy.Domain.Catalog;
using Inkoova.Academy.Domain.Common;
using Inkoova.Academy.Domain.ValueObjects;

namespace Inkoova.Academy.Application.Billing;

/// <summary>
/// Starts a Stripe Checkout session for a plan. Attribution is resolved here rather than in
/// the webhook because the discount code and the referral cookie are only known now (T-16).
/// </summary>
public sealed class StartPlanCheckoutHandler(
    IPlanRepository plans,
    IUserRepository users,
    IPaymentGateway payments,
    IDiscountCodeRepository discounts,
    IReferralRepository referrals,
    IAffiliateRepository affiliates,
    IClock clock,
    BillingOptions options)
{
    public async Task<Result<string, Error>> HandleAsync(
        Guid userId,
        string planCode,
        string? discountCode,
        string? visitorId,
        CancellationToken ct)
    {
        var plan = await plans.GetByCodeAsync(planCode, ct);
        if (plan is null || !plan.IsActive)
        {
            return Error.NotFound("plan.not_found", "No existe ese plan.");
        }

        if (string.IsNullOrWhiteSpace(plan.StripePriceId))
        {
            return Error.Conflict(
                "plan.no_stripe_price",
                "El plan no está enlazado con Stripe. Ejecuta el script de seed de precios.");
        }

        var user = await users.GetByIdAsync(userId, ct);
        if (user is null)
        {
            return Error.NotFound("user.not_found", "No existe ese usuario.");
        }

        var customerId = user.StripeCustomerId;
        if (string.IsNullOrWhiteSpace(customerId))
        {
            var ensured = await payments.EnsureCustomerAsync(userId, user.Email, user.DisplayName, ct);
            if (ensured.IsFailure)
            {
                return ensured.Error;
            }

            customerId = ensured.Value;
            user.LinkStripeCustomer(customerId);
            await users.UpsertAsync(user, ct);
        }

        var attribution = await ResolveAttributionAsync(discountCode, visitorId, ct);

        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [PaymentMetadataKeys.UserId] = userId.ToString(),
            [PaymentMetadataKeys.PlanCode] = plan.Code
        };

        if (attribution.AffiliateId is { } affiliateId)
        {
            metadata[PaymentMetadataKeys.AffiliateId] = affiliateId.ToString();
        }

        if (!string.IsNullOrWhiteSpace(visitorId))
        {
            metadata[PaymentMetadataKeys.VisitorId] = visitorId;
        }

        var session = await payments.CreateCheckoutSessionAsync(
            new CheckoutRequest(
                userId,
                user.Email,
                customerId,
                plan.StripePriceId,
                IsSubscription: plan.IsRecurring,
                options.CheckoutSuccessUrl,
                options.CheckoutCancelUrl,
                attribution.PromotionCodeId,
                metadata),
            ct);

        return session.Map(s => s.Url);
    }

    private async Task<(Guid? AffiliateId, string? PromotionCodeId)> ResolveAttributionAsync(
        string? discountCode,
        string? visitorId,
        CancellationToken ct)
    {
        // Priority: an explicit code beats the cookie (T-16).
        if (!string.IsNullOrWhiteSpace(discountCode))
        {
            var code = await discounts.GetByCodeAsync(discountCode.Trim().ToUpperInvariant(), ct);
            if (code is { IsActive: true })
            {
                return (code.AffiliateId, code.StripePromotionCodeId);
            }
        }

        if (!string.IsNullOrWhiteSpace(visitorId))
        {
            var referral = await referrals.GetByVisitorAsync(visitorId, ct);
            if (referral is not null && referral.IsValidAt(clock.UtcNow))
            {
                var affiliate = await affiliates.GetByIdAsync(referral.AffiliateId, ct);
                if (affiliate is { Status: Domain.Affiliates.AffiliateStatus.Active })
                {
                    return (affiliate.Id, null);
                }
            }
        }

        return (null, null);
    }
}

/// <summary>One-off checkout for a single course, a sector pack or the lifetime program.</summary>
public sealed class StartProductCheckoutHandler(
    IProductRepository products,
    IUserRepository users,
    IPaymentGateway payments,
    IEntitlementRepository entitlements,
    IDiscountCodeRepository discounts,
    IReferralRepository referrals,
    IAffiliateRepository affiliates,
    IClock clock,
    BillingOptions options)
{
    public async Task<Result<string, Error>> HandleAsync(
        Guid userId,
        string productSlug,
        string? discountCode,
        string? visitorId,
        CancellationToken ct)
    {
        var slug = Slug.Create(productSlug);
        if (slug.IsFailure)
        {
            return slug.Error;
        }

        var product = await products.GetBySlugAsync(slug.Value, ct);
        if (product is null)
        {
            return Error.NotFound("product.not_found", "No existe ese producto.");
        }

        if (!product.IsPurchasableOneOff)
        {
            return Error.Conflict(
                "product.not_purchasable",
                "Este producto no está a la venta por separado.");
        }

        var already = await entitlements.GetForUserAndProductAsync(userId, product.Id, ct);
        if (already is not null && already.IsValidAt(clock.UtcNow) && already.Source == Domain.Billing.EntitlementSource.Purchase)
        {
            return Error.Conflict("product.already_owned", "Ya tienes este producto.");
        }

        var user = await users.GetByIdAsync(userId, ct);
        if (user is null)
        {
            return Error.NotFound("user.not_found", "No existe ese usuario.");
        }

        var customerId = user.StripeCustomerId;
        if (string.IsNullOrWhiteSpace(customerId))
        {
            var ensured = await payments.EnsureCustomerAsync(userId, user.Email, user.DisplayName, ct);
            if (ensured.IsFailure)
            {
                return ensured.Error;
            }

            customerId = ensured.Value;
            user.LinkStripeCustomer(customerId);
            await users.UpsertAsync(user, ct);
        }

        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [PaymentMetadataKeys.UserId] = userId.ToString(),
            [PaymentMetadataKeys.ProductSlug] = product.Slug.Value
        };

        string? promotionCodeId = null;

        if (!string.IsNullOrWhiteSpace(discountCode))
        {
            var code = await discounts.GetByCodeAsync(discountCode.Trim().ToUpperInvariant(), ct);
            if (code is not null)
            {
                var redeemable = code.Redeem(product.Id, clock.UtcNow);
                if (redeemable.IsFailure)
                {
                    return redeemable.Error;
                }

                promotionCodeId = code.StripePromotionCodeId;
                if (code.AffiliateId is { } affiliateId)
                {
                    metadata[PaymentMetadataKeys.AffiliateId] = affiliateId.ToString();
                }
            }
        }
        else if (!string.IsNullOrWhiteSpace(visitorId))
        {
            var referral = await referrals.GetByVisitorAsync(visitorId, ct);
            if (referral is not null && referral.IsValidAt(clock.UtcNow))
            {
                var affiliate = await affiliates.GetByIdAsync(referral.AffiliateId, ct);
                if (affiliate is { Status: Domain.Affiliates.AffiliateStatus.Active })
                {
                    metadata[PaymentMetadataKeys.AffiliateId] = affiliate.Id.ToString();
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(visitorId))
        {
            metadata[PaymentMetadataKeys.VisitorId] = visitorId;
        }

        var session = await payments.CreateCheckoutSessionAsync(
            new CheckoutRequest(
                userId,
                user.Email,
                customerId,
                product.StripePriceId!,
                IsSubscription: false,
                options.CheckoutSuccessUrl,
                options.CheckoutCancelUrl,
                promotionCodeId,
                metadata),
            ct);

        return session.Map(s => s.Url);
    }
}

public sealed class OpenBillingPortalHandler(
    IUserRepository users,
    IPaymentGateway payments,
    BillingOptions options)
{
    public async Task<Result<string, Error>> HandleAsync(Guid userId, CancellationToken ct)
    {
        var user = await users.GetByIdAsync(userId, ct);
        if (user is null)
        {
            return Error.NotFound("user.not_found", "No existe ese usuario.");
        }

        if (string.IsNullOrWhiteSpace(user.StripeCustomerId))
        {
            return Error.Conflict("billing.no_customer", "Todavía no tienes pagos asociados a tu cuenta.");
        }

        return await payments.CreateBillingPortalSessionAsync(
            user.StripeCustomerId, options.BillingPortalReturnUrl, ct);
    }
}

public sealed record MySubscriptionDto(
    string? PlanCode,
    string? PlanName,
    string Status,
    DateTimeOffset? CurrentPeriodEnd,
    bool CancelAtPeriodEnd,
    bool IncludesPacks,
    IReadOnlyList<string> OwnedProductSlugs);

public sealed class GetMySubscriptionHandler(
    ISubscriptionRepository subscriptions,
    IPlanRepository plans,
    IEntitlementRepository entitlements,
    IProductRepository products,
    IClock clock)
{
    public async Task<MySubscriptionDto> HandleAsync(Guid userId, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var owned = new List<string>();

        foreach (var entitlement in await entitlements.GetForUserAsync(userId, ct))
        {
            if (!entitlement.IsValidAt(now))
            {
                continue;
            }

            var product = await products.GetByIdAsync(entitlement.ProductId, ct);
            if (product is not null)
            {
                owned.Add(product.Slug.Value);
            }
        }

        var subscription = await subscriptions.GetActiveForUserAsync(userId, ct);
        if (subscription is null)
        {
            return new MySubscriptionDto(null, null, "none", null, false, false, owned);
        }

        var plan = await plans.GetByIdAsync(subscription.PlanId, ct);

        return new MySubscriptionDto(
            plan?.Code,
            plan?.Name,
            subscription.Status.ToString().ToLowerInvariant(),
            subscription.CurrentPeriodEnd,
            subscription.CancelAtPeriodEnd,
            plan?.IncludesAllPacks ?? false,
            owned);
    }
}

public sealed class GetPlansHandler(IPlanRepository plans, IProductRepository products)
{
    public async Task<IReadOnlyList<Catalog.PlanDto>> HandleAsync(CancellationToken ct)
    {
        var active = await plans.GetActiveAsync(ct);

        // Se resuelve una vez para todos los planes: son cinco planes contra un catálogo corto,
        // y hacerlo por plan repetiría la misma consulta cinco veces.
        var referenced = active.SelectMany(p => p.IncludedProductIds).Distinct().ToList();
        var byId = new Dictionary<Guid, Product>();

        foreach (var id in referenced)
        {
            if (await products.GetByIdAsync(id, ct) is { } product)
            {
                byId[id] = product;
            }
        }

        return active
            .OrderBy(p => p.DisplayOrder)
            .Select(p => new Catalog.PlanDto(
                p.Code,
                p.Name,
                p.Interval.ToString().ToLowerInvariant(),
                p.Price.ToDecimal(),
                p.Price.Currency,
                p.Benefits,
                // Se conserva por compatibilidad de la pantalla de suscripción: «packs incluidos»
                // es cierto tanto si van todos por interruptor como si hay alguno marcado.
                p.IncludesAllPacks || p.IncludedProductIds.Any(
                    id => byId.TryGetValue(id, out var product) && product.Type == ProductType.Pack),
                p.DisplayOrder,
                p.IncludesAllCourses,
                p.IncludesAllPacks,
                [.. p.IncludedProductIds
                    .Where(byId.ContainsKey)
                    .Select(id => byId[id])
                    .OrderBy(product => product.Type)
                    .ThenBy(product => product.Title, StringComparer.CurrentCulture)
                    .Select(product => new Catalog.PlanProductDto(
                        product.Slug.Value,
                        product.Title,
                        product.Type.ToString().ToLowerInvariant()))])
            )
            .ToList();
    }
}
