using System.Text.Json;
using Dapper;
using Inkoova.Academy.Application.Abstractions;
using Inkoova.Academy.Domain.Billing;
using Inkoova.Academy.Domain.ValueObjects;

namespace Inkoova.Academy.Infrastructure.Persistence;

public sealed class PlanRepository(IDbConnectionFactory connections) : IPlanRepository
{
    private const string Columns = """
        id, code, name, billing_interval, price_cents, currency, benefits_json,
        stripe_price_id, includes_all_courses, includes_all_packs, display_order, is_active
        """;

    public async Task<IReadOnlyList<Plan>> GetActiveAsync(CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        var rows = await connection.QueryAsync<PlanRow>(
            $"SELECT {Columns} FROM plan WHERE is_active ORDER BY display_order");

        return await HydrateAsync(connection, [.. rows], ct);
    }

    /// <summary>Todos, incluidos los retirados: el panel tiene que poder volver a activarlos.</summary>
    public async Task<IReadOnlyList<Plan>> GetAllAsync(CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        var rows = await connection.QueryAsync<PlanRow>(
            $"SELECT {Columns} FROM plan ORDER BY display_order, name");

        return await HydrateAsync(connection, [.. rows], ct);
    }

    public async Task<Plan?> GetByCodeAsync(string code, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        var row = await connection.QuerySingleOrDefaultAsync<PlanRow>(
            $"SELECT {Columns} FROM plan WHERE code = @code", new { code });

        return row is null ? null : (await HydrateAsync(connection, [row], ct))[0];
    }

    public async Task<Plan?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        var row = await connection.QuerySingleOrDefaultAsync<PlanRow>(
            $"SELECT {Columns} FROM plan WHERE id = @id", new { id });

        return row is null ? null : (await HydrateAsync(connection, [row], ct))[0];
    }

    public async Task<Plan?> GetByStripePriceIdAsync(string stripePriceId, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        var row = await connection.QuerySingleOrDefaultAsync<PlanRow>(
            $"SELECT {Columns} FROM plan WHERE stripe_price_id = @stripePriceId", new { stripePriceId });

        return row is null ? null : (await HydrateAsync(connection, [row], ct))[0];
    }

    /// <summary>
    /// Añade a cada plan su lista de productos marcados, en UNA consulta para todos.
    ///
    /// Con una por plan, la página de precios haría seis viajes a la base para pintar cinco
    /// tarjetas, y la reconciliación de derechos uno por cada suscriptor.
    /// </summary>
    private static async Task<IReadOnlyList<Plan>> HydrateAsync(
        System.Data.IDbConnection connection, IReadOnlyList<PlanRow> rows, CancellationToken ct)
    {
        if (rows.Count == 0)
        {
            return [];
        }

        var links = await connection.QueryAsync<(Guid PlanId, Guid ProductId)>(
            "SELECT plan_id, product_id FROM plan_product WHERE plan_id = ANY(@planIds)",
            new { planIds = rows.Select(r => r.Id).ToArray() });

        var byPlan = links
            .GroupBy(l => l.PlanId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<Guid>)[.. g.Select(l => l.ProductId)]);

        return [.. rows.Select(r => r.ToDomain(
            byPlan.TryGetValue(r.Id, out var products) ? products : []))];
    }

    /// <summary>
    /// El plan y su lista de productos, en una transacción.
    ///
    /// La lista se borra y se reinserta en vez de calcular el diferencial: son cuatro filas por
    /// plan, y un borrar-e-insertar es incapaz de dejar a medias lo que un diferencial mal
    /// escrito sí puede. Va en transacción porque el estado intermedio —plan guardado, lista
    /// vacía— es un plan que no incluye nada, y si algo falla ahí, ese es el que se queda.
    /// </summary>
    public async Task UpsertAsync(Plan plan, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        using var transaction = connection.BeginTransaction();

        await connection.ExecuteAsync(
            """
            INSERT INTO plan (id, code, name, billing_interval, price_cents, currency, benefits_json,
                              stripe_price_id, includes_all_courses, includes_all_packs,
                              display_order, is_active)
            VALUES (@Id, @Code, @Name, @Interval, @PriceCents, @Currency, @BenefitsJson::jsonb,
                    @StripePriceId, @IncludesAllCourses, @IncludesAllPacks, @DisplayOrder, @IsActive)
            ON CONFLICT (id) DO UPDATE SET
                code = EXCLUDED.code,
                name = EXCLUDED.name,
                billing_interval = EXCLUDED.billing_interval,
                price_cents = EXCLUDED.price_cents,
                currency = EXCLUDED.currency,
                benefits_json = EXCLUDED.benefits_json,
                stripe_price_id = EXCLUDED.stripe_price_id,
                includes_all_courses = EXCLUDED.includes_all_courses,
                includes_all_packs = EXCLUDED.includes_all_packs,
                display_order = EXCLUDED.display_order,
                is_active = EXCLUDED.is_active
            """,
            new
            {
                plan.Id,
                plan.Code,
                plan.Name,
                Interval = EnumMapping.ToDb(plan.Interval),
                PriceCents = plan.Price.AmountInCents,
                Currency = plan.Price.Currency,
                plan.BenefitsJson,
                plan.StripePriceId,
                plan.IncludesAllCourses,
                plan.IncludesAllPacks,
                plan.DisplayOrder,
                plan.IsActive
            },
            transaction);

        await connection.ExecuteAsync(
            "DELETE FROM plan_product WHERE plan_id = @planId",
            new { planId = plan.Id },
            transaction);

        if (plan.IncludedProductIds.Count > 0)
        {
            await connection.ExecuteAsync(
                "INSERT INTO plan_product (plan_id, product_id) VALUES (@planId, @productId)",
                plan.IncludedProductIds.Select(productId => new { planId = plan.Id, productId }),
                transaction);
        }

        transaction.Commit();
    }

    private sealed record PlanRow(
        Guid Id,
        string Code,
        string Name,
        string BillingInterval,
        long PriceCents,
        string Currency,
        string BenefitsJson,
        string? StripePriceId,
        bool IncludesAllCourses,
        bool IncludesAllPacks,
        int DisplayOrder,
        bool IsActive)
    {
        public Plan ToDomain(IReadOnlyList<Guid> includedProductIds) => Plan.Rehydrate(
            Id, Code, Name, EnumMapping.FromDb<Domain.Billing.BillingInterval>(BillingInterval),
            Money.Create(PriceCents, Currency).Value, BenefitsJson, StripePriceId,
            IncludesAllCourses, IncludesAllPacks, includedProductIds, DisplayOrder, IsActive);
    }
}

public sealed class SubscriptionRepository(IDbConnectionFactory connections) : ISubscriptionRepository
{
    private const string Columns = """
        id, user_id, plan_id, status, current_period_end, cancel_at_period_end,
        stripe_subscription_id, past_due_since, created_at, updated_at
        """;

    public async Task<Subscription?> GetActiveForUserAsync(Guid userId, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        var row = await connection.QuerySingleOrDefaultAsync<SubscriptionRow>(
            $"""
             SELECT {Columns} FROM subscription
             WHERE user_id = @userId AND status IN ('active', 'pastdue')
             ORDER BY current_period_end DESC LIMIT 1
             """,
            new { userId });

        return row?.ToDomain();
    }

    public async Task<Subscription?> GetByStripeIdAsync(string stripeSubscriptionId, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        var row = await connection.QuerySingleOrDefaultAsync<SubscriptionRow>(
            $"SELECT {Columns} FROM subscription WHERE stripe_subscription_id = @stripeSubscriptionId",
            new { stripeSubscriptionId });

        return row?.ToDomain();
    }

    public async Task<IReadOnlyList<Subscription>> GetActiveAsync(CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);

        // `pastdue` entra: durante el periodo de gracia la plataforma sigue dando acceso, así
        // que un curso nuevo también le corresponde a quien tiene un recibo devuelto y todavía
        // dentro de plazo. Dejarlo fuera sería cortarle antes por un lado de lo que se le
        // mantiene por el otro.
        var rows = await connection.QueryAsync<SubscriptionRow>(
            $"SELECT {Columns} FROM subscription WHERE status IN ('active', 'pastdue')");

        return rows.Select(r => r.ToDomain()).ToList();
    }

    public async Task<IReadOnlyList<Subscription>> GetExpiringBeforeAsync(DateTimeOffset instant, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        var rows = await connection.QueryAsync<SubscriptionRow>(
            $"""
             SELECT {Columns} FROM subscription
             WHERE current_period_end < @instant AND status IN ('active', 'pastdue')
             """,
            new { instant });

        return rows.Select(r => r.ToDomain()).ToList();
    }

    public async Task UpsertAsync(Subscription subscription, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        await connection.ExecuteAsync(
            """
            INSERT INTO subscription (id, user_id, plan_id, status, current_period_end, cancel_at_period_end,
                                      stripe_subscription_id, past_due_since, created_at, updated_at)
            VALUES (@Id, @UserId, @PlanId, @Status, @CurrentPeriodEnd, @CancelAtPeriodEnd,
                    @StripeSubscriptionId, @PastDueSince, @CreatedAt, @UpdatedAt)
            ON CONFLICT (id) DO UPDATE SET
                plan_id = EXCLUDED.plan_id,
                status = EXCLUDED.status,
                current_period_end = EXCLUDED.current_period_end,
                cancel_at_period_end = EXCLUDED.cancel_at_period_end,
                past_due_since = EXCLUDED.past_due_since,
                updated_at = EXCLUDED.updated_at
            """,
            new
            {
                subscription.Id,
                subscription.UserId,
                subscription.PlanId,
                Status = EnumMapping.ToDb(subscription.Status),
                subscription.CurrentPeriodEnd,
                subscription.CancelAtPeriodEnd,
                subscription.StripeSubscriptionId,
                subscription.PastDueSince,
                subscription.CreatedAt,
                subscription.UpdatedAt
            });
    }

    private sealed record SubscriptionRow(
        Guid Id,
        Guid UserId,
        Guid PlanId,
        string Status,
        DateTimeOffset CurrentPeriodEnd,
        bool CancelAtPeriodEnd,
        string StripeSubscriptionId,
        DateTimeOffset? PastDueSince,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt)
    {
        public Subscription ToDomain() => Subscription.Rehydrate(
            Id, UserId, PlanId, EnumMapping.FromDb<SubscriptionStatus>(Status), CurrentPeriodEnd,
            CancelAtPeriodEnd, StripeSubscriptionId, PastDueSince, CreatedAt, UpdatedAt);
    }
}

public sealed class EntitlementRepository(IDbConnectionFactory connections) : IEntitlementRepository
{
    private const string Columns = "id, user_id, product_id, source, valid_from, valid_until, revoked_at, note";

    public async Task<IReadOnlyList<Entitlement>> GetForUserAsync(Guid userId, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        var rows = await connection.QueryAsync<EntitlementRow>(
            $"SELECT {Columns} FROM entitlement WHERE user_id = @userId AND revoked_at IS NULL",
            new { userId });

        return rows.Select(r => r.ToDomain()).ToList();
    }

    public async Task<Entitlement?> GetForUserAndProductAsync(Guid userId, Guid productId, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        var row = await connection.QuerySingleOrDefaultAsync<EntitlementRow>(
            $"""
             SELECT {Columns} FROM entitlement
             WHERE user_id = @userId AND product_id = @productId AND revoked_at IS NULL
             """,
            new { userId, productId });

        return row?.ToDomain();
    }

    public async Task<IReadOnlyList<Entitlement>> GetBySourceAsync(
        Guid userId,
        EntitlementSource source,
        CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        var rows = await connection.QueryAsync<EntitlementRow>(
            $"""
             SELECT {Columns} FROM entitlement
             WHERE user_id = @userId AND source = @source AND revoked_at IS NULL
             """,
            new { userId, source = EnumMapping.ToDb(source) });

        return rows.Select(r => r.ToDomain()).ToList();
    }

    public async Task<IReadOnlyList<Entitlement>> GetExpiredPlanIncludedAsync(
        DateTimeOffset instant,
        CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        var rows = await connection.QueryAsync<EntitlementRow>(
            $"""
             SELECT {Columns} FROM entitlement
             WHERE source = 'planincluded' AND revoked_at IS NULL
               AND valid_until IS NOT NULL AND valid_until < @instant
             """,
            new { instant });

        return rows.Select(r => r.ToDomain()).ToList();
    }

    public async Task UpsertAsync(Entitlement entitlement, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        await connection.ExecuteAsync(
            """
            INSERT INTO entitlement (id, user_id, product_id, source, valid_from, valid_until, revoked_at, note)
            VALUES (@Id, @UserId, @ProductId, @Source, @ValidFrom, @ValidUntil, @RevokedAt, @Note)
            ON CONFLICT (id) DO UPDATE SET
                source = EXCLUDED.source,
                valid_from = EXCLUDED.valid_from,
                valid_until = EXCLUDED.valid_until,
                revoked_at = EXCLUDED.revoked_at,
                note = EXCLUDED.note
            """,
            new
            {
                entitlement.Id,
                entitlement.UserId,
                entitlement.ProductId,
                Source = EnumMapping.ToDb(entitlement.Source),
                entitlement.ValidFrom,
                entitlement.ValidUntil,
                entitlement.RevokedAt,
                entitlement.Note
            });
    }

    private sealed record EntitlementRow(
        Guid Id,
        Guid UserId,
        Guid ProductId,
        string Source,
        DateTimeOffset ValidFrom,
        DateTimeOffset? ValidUntil,
        DateTimeOffset? RevokedAt,
        string? Note)
    {
        public Entitlement ToDomain() => Entitlement.Rehydrate(
            Id, UserId, ProductId, EnumMapping.FromDb<EntitlementSource>(Source),
            ValidFrom, ValidUntil, RevokedAt, Note);
    }
}

public sealed class PurchaseRepository(IDbConnectionFactory connections) : IPurchaseRepository
{
    private const string Columns = """
        id, user_id, product_id, gross_amount_cents, tax_amount_cents, processing_fee_cents,
        refunded_amount_cents, currency, status, stripe_payment_intent_id, discount_code_used,
        paid_at, refunded_at
        """;

    public async Task<Purchase?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        var row = await connection.QuerySingleOrDefaultAsync<PurchaseRow>(
            $"SELECT {Columns} FROM purchase WHERE id = @id", new { id });

        return row?.ToDomain();
    }

    public async Task<Purchase?> GetByPaymentIntentAsync(string paymentIntentId, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        var row = await connection.QuerySingleOrDefaultAsync<PurchaseRow>(
            $"SELECT {Columns} FROM purchase WHERE stripe_payment_intent_id = @paymentIntentId",
            new { paymentIntentId });

        return row?.ToDomain();
    }

    public async Task<IReadOnlyList<Purchase>> GetForUserAsync(Guid userId, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        var rows = await connection.QueryAsync<PurchaseRow>(
            $"SELECT {Columns} FROM purchase WHERE user_id = @userId ORDER BY paid_at DESC",
            new { userId });

        return rows.Select(r => r.ToDomain()).ToList();
    }

    public async Task UpsertAsync(Purchase purchase, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        await connection.ExecuteAsync(
            """
            INSERT INTO purchase (id, user_id, product_id, gross_amount_cents, tax_amount_cents,
                                  processing_fee_cents, refunded_amount_cents, currency, status,
                                  stripe_payment_intent_id, discount_code_used, paid_at, refunded_at)
            VALUES (@Id, @UserId, @ProductId, @Gross, @Tax, @Fee, @Refunded, @Currency, @Status,
                    @StripePaymentIntentId, @DiscountCodeUsed, @PaidAt, @RefundedAt)
            ON CONFLICT (id) DO UPDATE SET
                refunded_amount_cents = EXCLUDED.refunded_amount_cents,
                status = EXCLUDED.status,
                refunded_at = EXCLUDED.refunded_at
            """,
            new
            {
                purchase.Id,
                purchase.UserId,
                purchase.ProductId,
                Gross = purchase.GrossAmount.AmountInCents,
                Tax = purchase.TaxAmount.AmountInCents,
                Fee = purchase.ProcessingFee.AmountInCents,
                Refunded = purchase.RefundedAmount.AmountInCents,
                Currency = purchase.GrossAmount.Currency,
                Status = EnumMapping.ToDb(purchase.Status),
                purchase.StripePaymentIntentId,
                purchase.DiscountCodeUsed,
                purchase.PaidAt,
                purchase.RefundedAt
            });
    }

    private sealed record PurchaseRow(
        Guid Id,
        Guid UserId,
        Guid ProductId,
        long GrossAmountCents,
        long TaxAmountCents,
        long ProcessingFeeCents,
        long RefundedAmountCents,
        string Currency,
        string Status,
        string StripePaymentIntentId,
        string? DiscountCodeUsed,
        DateTimeOffset PaidAt,
        DateTimeOffset? RefundedAt)
    {
        public Purchase ToDomain() => Purchase.Rehydrate(
            Id, UserId, ProductId,
            Money.Create(GrossAmountCents, Currency).Value,
            Money.Create(TaxAmountCents, Currency).Value,
            Money.Create(ProcessingFeeCents, Currency).Value,
            Money.Create(RefundedAmountCents, Currency).Value,
            EnumMapping.FromDb<PurchaseStatus>(Status),
            StripePaymentIntentId, DiscountCodeUsed, PaidAt, RefundedAt);
    }
}

/// <summary>
/// Stripe event ledger. The insert is the idempotency check: a duplicate id violates the
/// primary key, so <c>ON CONFLICT DO NOTHING</c> returning zero rows means "already seen".
/// </summary>
public sealed class StripeEventStore(IDbConnectionFactory connections) : IStripeEventStore
{
    public async Task<bool> TryRecordAsync(
        string eventId,
        string eventType,
        DateTimeOffset receivedAt,
        CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        var inserted = await connection.ExecuteAsync(
            """
            INSERT INTO stripe_event (event_id, event_type, received_at)
            VALUES (@eventId, @eventType, @receivedAt)
            ON CONFLICT (event_id) DO NOTHING
            """,
            new { eventId, eventType, receivedAt });

        return inserted == 1;
    }

    public async Task MarkProcessedAsync(string eventId, DateTimeOffset processedAt, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        await connection.ExecuteAsync(
            "UPDATE stripe_event SET processed_at = @processedAt, error = NULL WHERE event_id = @eventId",
            new { eventId, processedAt });
    }

    public async Task MarkFailedAsync(string eventId, string error, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        await connection.ExecuteAsync(
            "UPDATE stripe_event SET error = @error WHERE event_id = @eventId",
            new { eventId, error });
    }
}

/// <summary>Kept next to the billing repositories because only they write benefits JSON.</summary>
internal static class JsonHelpers
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}
