using System.Text.Json;
using Inkoova.Academy.Api.Common;
using Inkoova.Academy.Application.Abstractions;
using Inkoova.Academy.Application.Access;
using Inkoova.Academy.Application.Billing;
using Inkoova.Academy.Domain.Catalog;
using Inkoova.Academy.Domain.Users;

namespace Inkoova.Academy.Api.Endpoints;

/// <summary>
/// Planes y configuración de cobro (T-04, T-11).
///
/// Los planes vivían en el seed y solo se podían cambiar tocando código. Aquí se editan, y el
/// escaparate de precios los lee de la base como ya hacía, así que un cambio de precio se ve en
/// la web sin desplegar nada.
/// </summary>
public static class BillingAdminEndpoints
{
    public static void MapBillingAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/billing")
            .WithTags("Planes y cobro")
            .RequireAuthorization("admin");

        // ── planes ─────────────────────────────────────────────────────────────────────

        group.MapGet("/plans", async (PlanAuthoringHandlers handlers, CancellationToken ct) =>
            {
                var plans = await handlers.ListAsync(ct);

                return Results.Ok(plans.Select(plan => new
                {
                    plan.Id,
                    plan.Code,
                    plan.Name,
                    Interval = plan.Interval.ToString().ToLowerInvariant(),
                    PriceCents = plan.Price.AmountInCents,
                    plan.Benefits,
                    plan.IncludesAllCourses,
                    plan.IncludesAllPacks,
                    plan.IncludedProductIds,
                    plan.DisplayOrder,
                    plan.IsActive,
                    plan.StripePriceId
                }));
            })
            .WithSummary("Todos los planes, activos y retirados.");

        // Lo que se puede marcar en un plan. Va aparte del catálogo público porque aquí interesa
        // el PRODUCTO —que es la unidad de acceso— y no el curso: son cosas distintas, y el
        // panel tiene que mandar identificadores de producto, no slugs de curso.
        group.MapGet("/plans/selectable-products", async (
                IProductRepository products,
                CancellationToken ct) =>
            {
                var courses = await products.GetByTypeAsync(ProductType.Course, ct);
                var packs = await products.GetByTypeAsync(ProductType.Pack, ct);

                return Results.Ok(new
                {
                    courses = courses
                        .OrderBy(p => p.Title, StringComparer.CurrentCulture)
                        .Select(p => new { id = p.Id, slug = p.Slug.Value, title = p.Title }),
                    packs = packs
                        .OrderBy(p => p.Title, StringComparer.CurrentCulture)
                        .Select(p => new { id = p.Id, slug = p.Slug.Value, title = p.Title })
                });
            })
            .WithSummary("Cursos y packs que se pueden marcar en un plan.");

        group.MapPost("/plans", async (
                CreatePlanRequest body,
                PlanAuthoringHandlers handlers,
                IAuditLogRepository audit,
                HttpContext context,
                IClock clock,
                CancellationToken ct) =>
            {
                var result = await handlers.CreateAsync(body, ct);

                if (result.IsSuccess)
                {
                    await Audit(audit, context, clock, "plan.create", result.Value.ToString(),
                        JsonSerializer.Serialize(new { body.Code, body.PriceCents }), ct);
                }

                return result.ToHttp(id => Results.Ok(new { id }));
            })
            .WithSummary("Crea un plan. Queda sin enlazar con Stripe hasta que se sincroniza.");

        group.MapPut("/plans/{planId:guid}", async (
                Guid planId,
                UpdatePlanRequest body,
                PlanAuthoringHandlers handlers,
                IAuditLogRepository audit,
                HttpContext context,
                IClock clock,
                CancellationToken ct) =>
            {
                var result = await handlers.UpdateAsync(planId, body, ct);

                if (result.IsSuccess)
                {
                    // El precio es el dato con consecuencias económicas: se registra siempre.
                    await Audit(audit, context, clock, "plan.update", planId.ToString(),
                        JsonSerializer.Serialize(new { body.Name, body.PriceCents, PriceChanged = result.Value }), ct);
                }

                return result.ToHttp(priceChanged => Results.Ok(new
                {
                    priceChanged,
                    needsStripeSync = priceChanged
                }));
            })
            .WithSummary("Edita un plan. Cambiar el precio obliga a sincronizar con Stripe.");

        group.MapPost("/plans/{planId:guid}/active", async (
                Guid planId,
                ActiveBody body,
                PlanAuthoringHandlers handlers,
                IAuditLogRepository audit,
                HttpContext context,
                IClock clock,
                CancellationToken ct) =>
            {
                var result = await handlers.SetActiveAsync(planId, body.Active, ct);

                if (result.IsSuccess)
                {
                    await Audit(audit, context, clock, body.Active ? "plan.activate" : "plan.deactivate",
                        planId.ToString(), null, ct);
                }

                return result.ToNoContent();
            })
            .WithSummary("Pone o quita un plan del escaparate. Nunca lo borra.");

        // ── reconciliar derechos ───────────────────────────────────────────────────────
        //
        // Los derechos de un plan se conceden cuando Stripe avisa de un pago. Eso deja un hueco
        // real: si entra un curso o un pack NUEVO en el catálogo, quien ya está suscrito no lo
        // recibe hasta su siguiente renovación, que puede ser dentro de once meses.
        //
        // Esto vuelve a aplicar el plan de cada suscriptor vivo. `GrantFromPlanAsync` es
        // idempotente —reutiliza el entitlement existente en vez de crear otro—, así que
        // ejecutarlo dos veces no duplica nada.
        group.MapPost("/entitlements/reconcile", async (
                ISubscriptionRepository subscriptions,
                IPlanRepository plans,
                PlanEntitlementService planEntitlements,
                BillingOptions billing,
                IAuditLogRepository audit,
                HttpContext context,
                IClock clock,
                CancellationToken ct) =>
            {
                var active = await subscriptions.GetActiveAsync(ct);
                var applied = 0;
                var skipped = 0;

                foreach (var subscription in active)
                {
                    var plan = await plans.GetByIdAsync(subscription.PlanId, ct);

                    if (plan is null)
                    {
                        // Un plan retirado del catálogo con suscriptores vivos. No se inventa
                        // qué incluía: se cuenta y se dice.
                        skipped++;
                        continue;
                    }

                    var result = await planEntitlements.ApplyAsync(
                        subscription.UserId, plan,
                        subscription.AccessValidUntil(billing.GracePeriod), ct);

                    if (result.IsSuccess)
                    {
                        applied++;
                    }
                    else
                    {
                        skipped++;
                    }
                }

                await Audit(audit, context, clock, "entitlements.reconcile", null,
                    JsonSerializer.Serialize(new { active = active.Count, applied, skipped }), ct);

                return Results.Ok(new { subscriptions = active.Count, applied, skipped });
            })
            .WithSummary("Vuelve a aplicar el plan a cada suscriptor. Necesario al añadir cursos o packs.");

        // ── Stripe ─────────────────────────────────────────────────────────────────────

        // Solo dice SI hay clave y de qué tipo, nunca la clave. Un panel que enseña secretos
        // los acaba enseñando en una captura de pantalla.
        group.MapGet("/stripe", (IConfiguration configuration, PlanAuthoringHandlers handlers, CancellationToken ct) =>
            {
                var secret = configuration["Academy:Stripe:SecretKey"];
                var webhook = configuration["Academy:Stripe:WebhookSecret"];

                return Results.Ok(new
                {
                    secretKey = Describe(secret, "sk_"),
                    webhookSecret = Describe(webhook, "whsec_"),
                    automaticTax = configuration.GetValue("Academy:Stripe:AutomaticTax", true),
                    mode = ModeOf(secret),
                    webhookPath = "/api/webhooks/stripe"
                });
            })
            .WithSummary("Qué hay configurado de Stripe. Nunca devuelve las claves.");

        group.MapPost("/stripe/sync", async (
                PlanAuthoringHandlers handlers,
                IAuditLogRepository audit,
                HttpContext context,
                IClock clock,
                CancellationToken ct) =>
            {
                var result = await handlers.SyncWithStripeAsync(ct);

                if (result.IsSuccess)
                {
                    await Audit(audit, context, clock, "stripe.sync", null,
                        JsonSerializer.Serialize(result.Value.Select(item => new { item.Code, item.Linked })), ct);
                }

                return result.ToHttp(Results.Ok);
            })
            .WithSummary("Crea en Stripe los precios que falten y los enlaza. Idempotente.");
    }

    public sealed record ActiveBody(bool Active);

    /// <summary>
    /// Estado de un secreto sin revelarlo: si está, si tiene la pinta que debe y sus cuatro
    /// últimos caracteres, que es lo justo para reconocer cuál de tus claves es.
    /// </summary>
    private static object Describe(string? value, string expectedPrefix)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new { configured = false, valid = false, hint = (string?)null };
        }

        // El valor de marcador de desarrollo no es una clave: decirlo evita media hora buscando
        // por qué Stripe contesta que no autoriza.
        var placeholder = value.Contains("placeholder", StringComparison.OrdinalIgnoreCase)
                          || value.Contains("dummy", StringComparison.OrdinalIgnoreCase);

        var valid = !placeholder && value.StartsWith(expectedPrefix, StringComparison.Ordinal);

        return new
        {
            configured = true,
            valid,
            placeholder,
            hint = value.Length >= 4 ? $"…{value[^4..]}" : null
        };
    }

    private static string ModeOf(string? secret) => secret switch
    {
        null or "" => "sin configurar",
        var s when s.Contains("placeholder", StringComparison.OrdinalIgnoreCase) => "marcador de desarrollo",
        var s when s.StartsWith("sk_live_", StringComparison.Ordinal) => "producción",
        var s when s.StartsWith("sk_test_", StringComparison.Ordinal) => "pruebas",
        _ => "desconocido"
    };

    private static Task Audit(
        IAuditLogRepository audit,
        HttpContext context,
        IClock clock,
        string action,
        string? entityId,
        string? detailsJson,
        CancellationToken ct) =>
        audit.AppendAsync(
            new AuditEntry(
                Guid.CreateVersion7(),
                context.User.RequireUserId(),
                action,
                "plan",
                entityId,
                detailsJson,
                clock.UtcNow),
            ct);
}
