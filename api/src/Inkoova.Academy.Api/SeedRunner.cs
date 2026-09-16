using Inkoova.Academy.Application.Abstractions;
using Inkoova.Academy.Domain.Billing;
using Inkoova.Academy.Domain.Catalog;
using Inkoova.Academy.Domain.ValueObjects;
using Inkoova.Academy.Infrastructure.Identity;

namespace Inkoova.Academy.Api;

/// <summary>
/// Idempotent seed run at startup: plans and the bootstrap admin. Prices are only created in
/// Stripe when a secret key is configured, so a local run without Stripe still gets a working
/// catalogue of plans (T-04).
/// </summary>
public static class SeedRunner
{
    public static async Task RunAsync(WebApplication app)
    {
        if (!app.Configuration.GetValue("Academy:Seed:Enabled", true))
        {
            return;
        }

        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;
        var logger = app.Logger;
        var ct = app.Lifetime.ApplicationStopping;

        try
        {
            await SeedPlansAsync(services, app.Configuration, logger, ct);
            await SeedAdminAsync(services, app.Configuration, logger, ct);
        }
        catch (Exception ex)
        {
            // A failing seed must not stop the API: the catalogue may already be seeded and
            // the site should stay up while the cause is investigated.
            logger.LogError(ex, "Seed failed; the API starts anyway.");
        }
    }

    private static async Task SeedPlansAsync(
        IServiceProvider services,
        IConfiguration configuration,
        ILogger logger,
        CancellationToken ct)
    {
        var plans = services.GetRequiredService<IPlanRepository>();

        // Todos, no solo los activos: retirar los cinco planes es una decisión legítima del
        // panel, y mirando solo los activos el siguiente arranque los resucitaba.
        var existing = await plans.GetAllAsync(ct);

        if (existing.Count > 0)
        {
            return;
        }

        // Prices from T-04: 19 monthly, 49 quarterly, 79 biannual, 129 yearly, 249 lifetime.
        // Editable from admin afterwards; these are the starting values, not a hardcoded truth.
        var definitions = new (string Code, string Name, BillingInterval Interval, long Cents, bool Packs, int Order, string[] Benefits)[]
        {
            ("monthly", "Starter", BillingInterval.Monthly, 1_900, false, 1,
                ["Acceso a todos los cursos publicados", "Comunidad Discord", "Certificados de curso"]),
            ("quarterly", "Pro", BillingInterval.Quarterly, 4_900, false, 2,
                ["Todo lo de Starter", "Sesiones grupales mensuales", "Canal Pro en Discord"]),
            ("biannual", "Pro 6 meses", BillingInterval.Biannual, 7_900, false, 3,
                ["Todo lo de Pro", "Prioridad en soporte"]),
            ("yearly", "Elite", BillingInterval.Yearly, 12_900, true, 4,
                ["Todo lo de Pro", "Packs sectoriales incluidos", "Una sesión 1:1 al trimestre"]),
            ("lifetime", "Programa vitalicio", BillingInterval.Lifetime, 24_900, true, 5,
                ["Los cuatro cursos para siempre", "Packs sectoriales incluidos", "Certificado de programa"])
        };

        var payments = services.GetService<IPaymentGateway>();
        var stripeConfigured = !string.IsNullOrWhiteSpace(configuration["Academy:Stripe:SecretKey"]);

        foreach (var (code, name, interval, cents, packs, order, benefits) in definitions)
        {
            var plan = Plan.Create(
                Guid.CreateVersion7(), code, name, interval, Money.Euros(cents), benefits,
                // La siembra crea los planes con el comportamiento que tenían antes de que esto
                // fuera configurable: todos los cursos, y los packs en los dos de arriba.
                // Estrechar un plan es una decisión de negocio y se toma desde el panel.
                includesAllCourses: true, includesAllPacks: packs, includedProductIds: [], order);

            if (plan.IsFailure)
            {
                logger.LogError("Seed plan {Code} rejected: {Error}", code, plan.Error);
                continue;
            }

            if (stripeConfigured && payments is not null)
            {
                var price = await payments.EnsurePriceAsync(
                    $"Inkoova Academy · {name}",
                    $"academy_{code}",
                    plan.Value.Price,
                    interval == BillingInterval.Lifetime ? null : interval,
                    ct);

                if (price.IsSuccess)
                {
                    plan.Value.LinkStripePrice(price.Value);
                }
                else
                {
                    logger.LogWarning("Stripe price for {Code} could not be created: {Error}", code, price.Error);
                }
            }

            await plans.UpsertAsync(plan.Value, ct);
        }

        logger.LogInformation("Seeded {Count} plans (Stripe wired: {Stripe}).", definitions.Length, stripeConfigured);
    }

    /// <summary>
    /// Da a los administradores acceso a todo el catálogo: cursos, packs y programas.
    ///
    /// Se hace con entitlements de origen `manual`, no con una excepción en `IAccessPolicy`.
    /// Un `if (esAdmin) return true` sería una línea menos y rompería lo que sostiene el
    /// modelo de acceso (ADR-007): que todo derecho es una fila que se puede mirar, auditar y
    /// retirar. Además dejaría al administrador sin poder comprobar nunca si el muro de pago
    /// funciona, porque él lo atravesaría siempre.
    ///
    /// Se reconcilia en cada arranque: un producto creado después queda cubierto al reiniciar,
    /// y volver a conceder lo ya concedido no hace nada.
    /// </summary>
    private static async Task GrantAdminsEveryProductAsync(
        IServiceProvider services,
        IReadOnlyList<string> admins,
        ILogger logger,
        CancellationToken ct)
    {
        var identity = services.GetRequiredService<IdentityStore>();
        var products = services.GetRequiredService<IProductRepository>();
        var entitlements = services.GetRequiredService<Application.Access.EntitlementService>();

        var catalogue = new List<Product>();

        foreach (var type in new[] { ProductType.Course, ProductType.Pack, ProductType.Program })
        {
            catalogue.AddRange(await products.GetByTypeAsync(type, ct));
        }

        if (catalogue.Count == 0)
        {
            return;
        }

        foreach (var email in admins)
        {
            var row = await identity.FindByEmailAsync(email, ct);
            if (row is null)
            {
                continue;
            }

            var granted = 0;

            foreach (var product in catalogue)
            {
                var result = await entitlements.GrantManualAsync(
                    row.Id, product.Id, validUntil: null, note: "Administrador global", ct);

                if (result.IsFailure)
                {
                    logger.LogWarning(
                        "No se ha podido dar acceso a {Email} sobre {Product}: {Error}",
                        email, product.Slug.Value, result.Error.Message);

                    continue;
                }

                granted++;
            }

            logger.LogInformation("{Email}: acceso a {Count} producto(s).", email, granted);
        }
    }

    private static async Task SeedAdminAsync(
        IServiceProvider services,
        IConfiguration configuration,
        ILogger logger,
        CancellationToken ct)
    {
        var accounts = services.GetRequiredService<AccountService>();

        // Los administradores globales van por configuración (Academy:Auth:OwnerEmails) y se
        // reponen en cada arranque, así que no dependen de que nadie recuerde concederlos.
        var owners = await accounts.PromoteOwnersAsync(ct);

        if (owners.Count > 0)
        {
            logger.LogInformation("Administradores globales al día: {Owners}.", string.Join(", ", owners));
        }

        // El admin de arranque también recibe el catálogo completo, así que se crea antes de
        // repartir los accesos y se suma a la misma lista que los owners.
        var admins = new List<string>(owners);

        var email = configuration["Academy:Seed:AdminEmail"];
        var password = configuration["Academy:Seed:AdminPassword"];

        if (!string.IsNullOrWhiteSpace(email) && !string.IsNullOrWhiteSpace(password))
        {
            var created = await accounts.EnsureAdminAsync(
                email, password, configuration["Academy:Seed:AdminName"] ?? "Administración", ct);

            logger.LogInformation(
                created ? "Bootstrap admin {Email} created." : "Bootstrap admin {Email} already existed.", email);

            admins.Add(email);
        }

        if (admins.Count > 0)
        {
            await GrantAdminsEveryProductAsync(services, admins, logger, ct);
        }
    }
}
