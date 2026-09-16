using System.Text.Json;
using Inkoova.Academy.Api.Common;
using Inkoova.Academy.Application.Abstractions;
using Inkoova.Academy.Application.Access;
using Inkoova.Academy.Application.Affiliates;
using Inkoova.Academy.Domain.Affiliates;
using Inkoova.Academy.Domain.Catalog;
using Inkoova.Academy.Domain.Common;
using Inkoova.Academy.Domain.Users;
using Inkoova.Academy.Domain.ValueObjects;

namespace Inkoova.Academy.Api.Endpoints;

public static class AdminEndpoints
{
    public static void MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin")
            .WithTags("Administración")
            .RequireAuthorization("admin");

        // ── catálogo ───────────────────────────────────────────────────────────────────

        group.MapGet("/courses", async (ICourseRepository courses, CancellationToken ct) =>
            {
                var all = await courses.GetAllAsync(ct);

                return Results.Ok(all.Select(c => new
                {
                    id = c.Id,
                    slug = c.Slug.Value,
                    title = c.Title,
                    status = c.Status.ToString().ToLowerInvariant(),
                    level = c.Level.ToString().ToLowerInvariant(),
                    isFeatured = c.IsFeatured,
                    isNew = c.IsNew,
                    sections = c.TotalSections,
                    lessons = c.TotalLessons,
                    hours = c.TotalHours
                }));
            })
            .WithSummary("Todos los cursos, incluidos los borradores.");

        group.MapPost("/courses/{id:guid}/publish", async (
                Guid id,
                ICourseRepository courses,
                ICatalogCache cache,
                IAuditLogRepository audit,
                IClock clock,
                HttpContext context,
                CancellationToken ct) =>
            {
                var course = await courses.GetByIdAsync(id, ct);
                if (course is null)
                {
                    return Error.NotFound("course.not_found", "No existe ese curso.").ToProblem();
                }

                var published = course.Publish(clock.UtcNow);
                if (published.IsFailure)
                {
                    return published.Error.ToProblem();
                }

                await courses.SaveAsync(course, ct);

                // Without this the landing keeps serving the cached catalogue for 5 minutes
                // (T-11 acceptance criteria: changes appear without a redeploy).
                cache.Invalidate();

                await AuditAsync(audit, context, clock, "course.publish", "course", id.ToString(), null, ct);
                return Results.NoContent();
            })
            .WithSummary("Publica un curso e invalida la caché del catálogo.");

        group.MapPost("/courses/{id:guid}/unpublish", async (
                Guid id,
                ICourseRepository courses,
                ICatalogCache cache,
                IAuditLogRepository audit,
                IClock clock,
                HttpContext context,
                CancellationToken ct) =>
            {
                var course = await courses.GetByIdAsync(id, ct);
                if (course is null)
                {
                    return Error.NotFound("course.not_found", "No existe ese curso.").ToProblem();
                }

                course.Unpublish();
                await courses.SaveAsync(course, ct);
                cache.Invalidate();

                await AuditAsync(audit, context, clock, "course.unpublish", "course", id.ToString(), null, ct);
                return Results.NoContent();
            })
            .WithSummary("Despublica un curso.");

        group.MapPut("/courses/{id:guid}/flags", async (
                Guid id,
                CourseFlagsBody body,
                ICourseRepository courses,
                ICatalogCache cache,
                IAuditLogRepository audit,
                IClock clock,
                HttpContext context,
                CancellationToken ct) =>
            {
                var course = await courses.GetByIdAsync(id, ct);
                if (course is null)
                {
                    return Error.NotFound("course.not_found", "No existe ese curso.").ToProblem();
                }

                course.SetFeatured(body.IsFeatured);
                course.SetNew(body.IsNew);

                if (body.CoverImageUrl is not null)
                {
                    course.SetCoverImage(body.CoverImageUrl);
                }

                await courses.SaveAsync(course, ct);
                cache.Invalidate();

                await AuditAsync(audit, context, clock, "course.flags", "course", id.ToString(),
                    JsonSerializer.Serialize(body), ct);

                return Results.NoContent();
            })
            .WithSummary("Marca destacado, nuevo y portada.");

        group.MapPut("/courses/{id:guid}/sections/order", async (
                Guid id,
                ReorderBody body,
                ICourseRepository courses,
                ICatalogCache cache,
                CancellationToken ct) =>
            {
                var course = await courses.GetByIdAsync(id, ct);
                if (course is null)
                {
                    return Error.NotFound("course.not_found", "No existe ese curso.").ToProblem();
                }

                for (var i = 0; i < body.OrderedIds.Count; i++)
                {
                    course.Sections.FirstOrDefault(s => s.Id == body.OrderedIds[i])?.Reorder(i);
                }

                await courses.SaveAsync(course, ct);
                cache.Invalidate();
                return Results.NoContent();
            })
            .WithSummary("Reordena secciones (drag and drop en el panel).");

        group.MapPut("/sections/{sectionId:guid}/lessons/order", async (
                Guid sectionId,
                ReorderBody body,
                ICourseRepository courses,
                ICatalogCache cache,
                CancellationToken ct) =>
            {
                var lesson = await courses.GetLessonAsync(body.OrderedIds.FirstOrDefault(), ct);
                if (lesson is null)
                {
                    return Error.NotFound("lesson.not_found", "No existe esa lección.").ToProblem();
                }

                var productId = await courses.GetProductIdForLessonAsync(lesson.Id, ct);
                if (productId is null)
                {
                    return Error.NotFound("course.not_found", "No existe el curso de esa lección.").ToProblem();
                }

                var course = await courses.GetByProductIdAsync(productId.Value, ct);
                if (course is null)
                {
                    return Error.NotFound("course.not_found", "No existe el curso de esa lección.").ToProblem();
                }

                var section = course.Sections.FirstOrDefault(s => s.Id == sectionId);
                if (section is null)
                {
                    return Error.NotFound("section.not_found", "No existe esa sección.").ToProblem();
                }

                for (var i = 0; i < body.OrderedIds.Count; i++)
                {
                    section.Lessons.FirstOrDefault(l => l.Id == body.OrderedIds[i])?.Reorder(i);
                }

                await courses.SaveAsync(course, ct);
                cache.Invalidate();
                return Results.NoContent();
            })
            .WithSummary("Reordena lecciones dentro de una sección.");

        // ── importación de contenido (T-07) ────────────────────────────────────────────

        group.MapPost("/import/preview", async (
                Application.Content.CourseManifest manifest,
                Application.Content.ImportManifestHandler handler,
                CancellationToken ct) =>
                (await handler.PreviewAsync(manifest, ct)).ToHttp())
            .WithSummary("Diff de lo que cambiaría al aplicar el manifest, sin escribir nada.");

        group.MapPost("/import/apply", async (
                Application.Content.CourseManifest manifest,
                Application.Content.ImportManifestHandler handler,
                IAuditLogRepository audit,
                IClock clock,
                HttpContext context,
                CancellationToken ct) =>
            {
                var result = await handler.ApplyAsync(manifest, ct);
                if (result.IsFailure)
                {
                    return result.Error.ToProblem();
                }

                await AuditAsync(audit, context, clock, "content.import", "course",
                    manifest.CourseSlug, JsonSerializer.Serialize(result.Value), ct);

                return Results.Ok(result.Value);
            })
            .WithSummary("Aplica el manifest. El curso no se publica: eso es una acción aparte.");

        // ── usuarios y accesos ─────────────────────────────────────────────────────────

        // `q` opcional: sin ella se listan los últimos altas, que es lo que se quiere al abrir
        // la pantalla. Declarada como no anulable, la petición sin `?q=` reventaba en el
        // binding y salía como 500.
        group.MapGet("/users", async (
                string? q,
                IUserRepository users,
                ISubscriptionRepository subscriptions,
                IPlanRepository plans,
                CancellationToken ct) =>
            {
                var found = await users.SearchAsync(q ?? string.Empty, 50, ct);
                var result = new List<object>(found.Count);

                foreach (var user in found)
                {
                    var subscription = await subscriptions.GetActiveForUserAsync(user.Id, ct);
                    var plan = subscription is null ? null : await plans.GetByIdAsync(subscription.PlanId, ct);

                    result.Add(new
                    {
                        user.Id,
                        user.Email,
                        user.DisplayName,
                        user.EmailConfirmed,
                        user.CreatedAt,
                        plan = plan?.Code,
                        subscriptionStatus = subscription?.Status.ToString().ToLowerInvariant(),
                        currentPeriodEnd = subscription?.CurrentPeriodEnd
                    });
                }

                return Results.Ok(result);
            })
            .WithSummary("Busca alumnos por email o nombre.");

        group.MapPost("/users/{userId:guid}/grant", async (
                Guid userId,
                GrantBody body,
                IProductRepository products,
                EntitlementService entitlements,
                IAuditLogRepository audit,
                IClock clock,
                HttpContext context,
                CancellationToken ct) =>
            {
                var slug = Slug.Create(body.ProductSlug);
                if (slug.IsFailure)
                {
                    return slug.Error.ToProblem();
                }

                var product = await products.GetBySlugAsync(slug.Value, ct);
                if (product is null)
                {
                    return Error.NotFound("product.not_found", "No existe ese producto.").ToProblem();
                }

                var granted = await entitlements.GrantManualAsync(
                    userId, product.Id, body.ValidUntil, body.Note, ct);

                if (granted.IsFailure)
                {
                    return granted.Error.ToProblem();
                }

                await AuditAsync(audit, context, clock, "entitlement.grant", "user", userId.ToString(),
                    JsonSerializer.Serialize(body), ct);

                return Results.NoContent();
            })
            .WithSummary("Concede acceso manual (beca, empresa, soporte).");

        group.MapPost("/users/{userId:guid}/revoke", async (
                Guid userId,
                RevokeBody body,
                IProductRepository products,
                EntitlementService entitlements,
                IAuditLogRepository audit,
                IClock clock,
                HttpContext context,
                CancellationToken ct) =>
            {
                var slug = Slug.Create(body.ProductSlug);
                if (slug.IsFailure)
                {
                    return slug.Error.ToProblem();
                }

                var product = await products.GetBySlugAsync(slug.Value, ct);
                if (product is null)
                {
                    return Error.NotFound("product.not_found", "No existe ese producto.").ToProblem();
                }

                var revoked = await entitlements.RevokeAsync(userId, product.Id, body.Reason, ct);
                if (revoked.IsFailure)
                {
                    return revoked.Error.ToProblem();
                }

                await AuditAsync(audit, context, clock, "entitlement.revoke", "user", userId.ToString(),
                    JsonSerializer.Serialize(body), ct);

                return Results.NoContent();
            })
            .WithSummary("Revoca un acceso.");

        group.MapPost("/users/{userId:guid}/roles/{role}", async (
                Guid userId,
                string role,
                Application.Auth.IAccountService accounts,
                IAuditLogRepository audit,
                IClock clock,
                HttpContext context,
                CancellationToken ct) =>
            {
                var granted = await accounts.GrantRoleAsync(userId, role, ct);
                if (granted.IsFailure)
                {
                    return granted.Error.ToProblem();
                }

                await AuditAsync(audit, context, clock, "role.grant", "user", userId.ToString(), role, ct);
                return Results.NoContent();
            })
            .WithSummary("Otorga un rol.");

        // ── métricas ───────────────────────────────────────────────────────────────────

        group.MapGet("/metrics", async (
                IUserRepository users,
                ICourseRepository courses,
                IPlanRepository plans,
                ISubscriptionRepository subscriptions,
                IProgressRepository progress,
                IClock clock,
                CancellationToken ct) =>
            {
                var catalog = await courses.GetPublicCatalogAsync(ct);
                var perCourse = new List<object>(catalog.Count);

                foreach (var course in catalog)
                {
                    var dropOffs = await progress.GetDropOffPointsAsync(course.Id, ct);

                    perCourse.Add(new
                    {
                        slug = course.Slug.Value,
                        title = course.Title,
                        lessons = course.TotalLessons,
                        dropOffPoints = dropOffs
                            .Take(5)
                            .Select(d => new
                            {
                                lessonId = d.LessonId,
                                lessonTitle = course.AllLessons.FirstOrDefault(l => l.Id == d.LessonId)?.Title,
                                count = d.DropOffCount
                            })
                    });
                }

                // MRR is computed from active subscriptions rather than read from Stripe: it
                // must agree with what this database believes access is worth.
                var expiring = await subscriptions.GetExpiringBeforeAsync(clock.UtcNow.AddYears(2), ct);
                var activePlans = await plans.GetActiveAsync(ct);
                var planById = activePlans.ToDictionary(p => p.Id);

                var mrrCents = expiring
                    .Where(s => s.Status is Domain.Billing.SubscriptionStatus.Active)
                    .Select(s => planById.TryGetValue(s.PlanId, out var plan) ? MonthlyValueCents(plan) : 0)
                    .Sum();

                return Results.Ok(new
                {
                    courses = perCourse,
                    mrr = mrrCents / 100m,
                    activeSubscriptions = expiring.Count(s => s.Status is Domain.Billing.SubscriptionStatus.Active),
                    pastDueSubscriptions = expiring.Count(s => s.Status is Domain.Billing.SubscriptionStatus.PastDue),
                    // TODO(T-15): churn needs a month of history; the beta will supply it.
                    churn = (decimal?)null
                });
            })
            .WithSummary("Métricas básicas: MRR, suscripciones y abandono por lección.");

        group.MapGet("/audit", async (IAuditLogRepository audit, CancellationToken ct) =>
                Results.Ok(await audit.GetRecentAsync(200, ct)))
            .WithSummary("Últimas acciones de administración.");

        // ── afiliados ──────────────────────────────────────────────────────────────────

        group.MapPost("/affiliates", async (
                CreateAffiliateBody body,
                IUserRepository users,
                IAffiliateRepository affiliates,
                Application.Auth.IAccountService accounts,
                IAuditLogRepository audit,
                IClock clock,
                HttpContext context,
                CancellationToken ct) =>
            {
                var user = await users.GetByEmailAsync(body.Email, ct);
                if (user is null)
                {
                    return Error.NotFound("user.not_found", "No hay ninguna cuenta con ese email.").ToProblem();
                }

                var created = Affiliate.Create(
                    Guid.CreateVersion7(), user.Id, body.Code, body.CommissionPercent,
                    body.RecurringMonths, clock.UtcNow);

                if (created.IsFailure)
                {
                    return created.Error.ToProblem();
                }

                created.Value.Activate();
                await affiliates.UpsertAsync(created.Value, ct);
                await accounts.GrantRoleAsync(user.Id, Roles.Affiliate, ct);

                await AuditAsync(audit, context, clock, "affiliate.create", "affiliate",
                    created.Value.Id.ToString(), JsonSerializer.Serialize(body), ct);

                return Results.Ok(new { id = created.Value.Id, code = created.Value.Code });
            })
            .WithSummary("Alta de afiliado.");

        group.MapPost("/affiliates/settle", async (
                SettleBody body,
                SettlementService settlement,
                IAuditLogRepository audit,
                IClock clock,
                HttpContext context,
                CancellationToken ct) =>
            {
                var created = await settlement.RunForPeriodAsync(body.PeriodStart, body.PeriodEnd, ct);

                await AuditAsync(audit, context, clock, "affiliate.settle", "payout", null,
                    JsonSerializer.Serialize(new { body.PeriodStart, body.PeriodEnd, count = created.Count }), ct);

                return Results.Ok(new { payouts = created });
            })
            .WithSummary("Ejecuta la liquidación mensual de afiliados.");

        group.MapPost("/payouts/{payoutId:guid}/invoice", async (
                Guid payoutId,
                InvoiceRefBody body,
                IPayoutRepository payouts,
                IAuditLogRepository audit,
                IClock clock,
                HttpContext context,
                CancellationToken ct) =>
            {
                var payout = await payouts.GetByIdAsync(payoutId, ct);
                if (payout is null)
                {
                    return Error.NotFound("payout.not_found", "No existe esa liquidación.").ToProblem();
                }

                var registered = payout.RegisterInvoice(body.InvoiceRef);
                if (registered.IsFailure)
                {
                    return registered.Error.ToProblem();
                }

                await payouts.UpsertAsync(payout, ct);
                await AuditAsync(audit, context, clock, "payout.invoice", "payout", payoutId.ToString(),
                    body.InvoiceRef, ct);

                return Results.NoContent();
            })
            .WithSummary("Registra la factura recibida del afiliado.");

        group.MapPost("/payouts/{payoutId:guid}/paid", async (
                Guid payoutId,
                IPayoutRepository payouts,
                IAuditLogRepository audit,
                IClock clock,
                HttpContext context,
                CancellationToken ct) =>
            {
                var payout = await payouts.GetByIdAsync(payoutId, ct);
                if (payout is null)
                {
                    return Error.NotFound("payout.not_found", "No existe esa liquidación.").ToProblem();
                }

                var paid = payout.MarkPaid(clock.UtcNow);
                if (paid.IsFailure)
                {
                    return paid.Error.ToProblem();
                }

                await payouts.UpsertAsync(payout, ct);
                await AuditAsync(audit, context, clock, "payout.paid", "payout", payoutId.ToString(), null, ct);

                return Results.NoContent();
            })
            .WithSummary("Marca una liquidación como pagada.");

        // ── packs ──────────────────────────────────────────────────────────────────────

        // Los packs de un alumno, con su estado. Existe para no tener que teclear seis slugs a
        // mano en el formulario de accesos: escribir «pack-administracion-publica» sin una
        // errata seis veces seguidas no es una tarea razonable para nadie.
        group.MapGet("/users/{userId:guid}/packs", async (
                Guid userId,
                IPackRepository packs,
                IEntitlementRepository entitlements,
                IAccessPolicy accessPolicy,
                CancellationToken ct) =>
            {
                var published = await packs.GetPublishedAsync(ct);
                var suyos = await entitlements.GetForUserAsync(userId, ct);
                var result = new List<object>(published.Count);

                foreach (var pack in published)
                {
                    var propio = suyos.FirstOrDefault(e => e.ProductId == pack.ProductId);

                    result.Add(new
                    {
                        slug = pack.Slug.Value,
                        title = pack.Title,
                        sector = pack.Sector,

                        // La política es la que manda: un pack puede estar incluido en el plan
                        // sin que exista una fila de acceso manual.
                        hasAccess = await accessPolicy.CanAccessProductAsync(userId, pack.ProductId, ct),

                        // De dónde le viene. Revocar solo sirve sobre lo manual: quitar a mano
                        // algo que da el plan lo devolvería el siguiente pase de reconciliación,
                        // y quien lo intente merece saberlo antes de pulsar.
                        source = propio is null
                            ? null
                            : propio.Source.ToString().ToLowerInvariant()
                    });
                }

                return Results.Ok(result);
            })
            .WithSummary("Packs publicados con el acceso que tiene un alumno a cada uno.");

        group.MapGet("/packs", async (
                IPackRepository packs,
                IContentStorage storage,
                CancellationToken ct) =>
            {
                var all = await packs.GetAllAsync(ct);
                var result = new List<object>(all.Count);

                foreach (var pack in all)
                {
                    // Qué hay en el volumen de contenido frente a qué está registrado. Es lo que
                    // dice si falta generar los ficheros o solo falta registrarlos, que son dos
                    // problemas distintos con dos soluciones distintas.
                    var enDisco = new List<object>();

                    foreach (var file in pack.Files)
                    {
                        enDisco.Add(new
                        {
                            file.FileName,
                            file.SizeInBytes,
                            present = await storage.ExistsAsync(file.ContentRef, ct)
                        });
                    }

                    result.Add(new
                    {
                        id = pack.Id,
                        slug = pack.Slug.Value,
                        title = pack.Title,
                        sector = pack.Sector,
                        status = pack.Status.ToString().ToLowerInvariant(),
                        version = pack.Version,
                        changelog = pack.Changelog,
                        regulatoryCheckDate = pack.RegulatoryCheckDate,
                        totalSizeInBytes = pack.TotalSizeInBytes,
                        files = enDisco
                    });
                }

                return Results.Ok(result);
            })
            .WithSummary("Packs sectoriales con su versión, sus ficheros y si están en el volumen.");

        group.MapPost("/packs/{id:guid}/files", async (
                Guid id,
                PackFilesBody body,
                IPackRepository packs,
                IContentStorage storage,
                IAuditLogRepository audit,
                IClock clock,
                HttpContext context,
                CancellationToken ct) =>
            {
                var pack = await packs.GetByIdAsync(id, ct);
                if (pack is null)
                {
                    return Error.NotFound("pack.not_found", "No existe ese pack.").ToProblem();
                }

                var registrados = new List<string>();
                var orden = pack.Files.Count;

                foreach (var nombre in body.FileNames ?? [])
                {
                    var contentRef = $"packs/{body.Folder ?? pack.Slug.Value}/{nombre}";

                    // Se comprueba que el fichero EXISTE en el volumen antes de registrarlo. Un
                    // pack publicado con una referencia a un fichero que no está da un 404 al
                    // primer cliente que lo descargue, que es el peor momento para descubrirlo.
                    if (!await storage.ExistsAsync(contentRef, ct))
                    {
                        return Error.Validation(
                                "pack.file_missing",
                                $"No está en el volumen de contenido: {contentRef}")
                            .ToProblem();
                    }

                    var size = await storage.GetSizeAsync(contentRef, ct);

                    // La huella se calcula sobre el fichero servido, no sobre el de origen: es lo
                    // que permite comprobar después que se sirve exactamente lo que se registró.
                    string hash;
                    using (var stream = await storage.OpenReadAsync(contentRef, ct))
                    {
                        if (stream is null)
                        {
                            return Error.Unexpected("pack.file_unreadable", $"No se puede leer {contentRef}.")
                                .ToProblem();
                        }

                        hash = Convert.ToHexStringLower(
                            await System.Security.Cryptography.SHA256.HashDataAsync(stream, ct));
                    }

                    var added = pack.AddFile(new PackFile(
                        Guid.CreateVersion7(), pack.Id, nombre, contentRef, size, hash, orden++));

                    if (added.IsFailure)
                    {
                        // Ya estaba: no es un error, es que se vuelve a ejecutar. Se salta.
                        orden--;
                        continue;
                    }

                    registrados.Add(nombre);
                }

                await packs.SaveAsync(pack, ct);

                // El detalle de auditoría es una columna JSON: un texto suelto la rompe.
                await AuditAsync(audit, context, clock, "pack.files", "pack", id.ToString(),
                    JsonSerializer.Serialize(new { registrados }), ct);

                return Results.Ok(new { registered = registrados, total = pack.Files.Count });
            })
            .WithSummary("Registra los ficheros de un pack desde el volumen de contenido. Idempotente.");

        group.MapPost("/packs/{id:guid}/release", async (
                Guid id,
                ReleasePackBody body,
                IPackRepository packs,
                IDownloadLogRepository downloads,
                IUserRepository users,
                IEmailSender email,
                IEmailTemplateRenderer templates,
                IAuditLogRepository audit,
                IClock clock,
                HttpContext context,
                CancellationToken ct) =>
            {
                var pack = await packs.GetByIdAsync(id, ct);
                if (pack is null)
                {
                    return Error.NotFound("pack.not_found", "No existe ese pack.").ToProblem();
                }

                var released = pack.ReleaseVersion(
                    body.Version, body.Changelog, body.RegulatoryCheckDate, clock.UtcNow);

                if (released.IsFailure)
                {
                    return released.Error.ToProblem();
                }

                await packs.SaveAsync(pack, ct);

                // Everyone who ever downloaded a file of this pack is told there is a new
                // version. Failures are logged by the sender and do not block the release.
                foreach (var userId in await downloads.GetUserIdsWhoDownloadedPackAsync(pack.Id, ct))
                {
                    var user = await users.GetByIdAsync(userId, ct);
                    if (user is null || !user.IsActive)
                    {
                        continue;
                    }

                    var rendered = await templates.RenderAsync("pack-new-version", new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["nombre"] = user.DisplayName,
                        ["pack"] = pack.Title,
                        ["version"] = pack.Version,
                        ["changelog"] = pack.Changelog ?? string.Empty
                    }, ct);

                    if (rendered.IsSuccess)
                    {
                        await email.SendAsync(
                            new EmailMessage(user.Email, rendered.Value.Subject, rendered.Value.Html, rendered.Value.Text),
                            ct);
                    }
                }

                // Igual que arriba: el detalle va como JSON. Antes iba la versión suelta, que la
                // columna rechaza; no se había notado porque nunca se había publicado un pack.
                await AuditAsync(audit, context, clock, "pack.release", "pack", id.ToString(),
                    JsonSerializer.Serialize(new { body.Version, body.RegulatoryCheckDate }), ct);
                return Results.NoContent();
            })
            .WithSummary("Publica una versión nueva de un pack y avisa a los compradores.");
    }

    /// <summary>
    /// Every admin action lands in the audit log with actor and timestamp (T-11 acceptance
    /// criteria). Called explicitly rather than by a filter so each endpoint states what it did.
    /// </summary>
    private static Task AuditAsync(
        IAuditLogRepository audit,
        HttpContext context,
        IClock clock,
        string action,
        string entityType,
        string? entityId,
        string? detailsJson,
        CancellationToken ct) =>
        audit.AppendAsync(
            new AuditEntry(
                Guid.CreateVersion7(),
                context.User.RequireUserId(),
                action,
                entityType,
                entityId,
                detailsJson,
                clock.UtcNow),
            ct);

    /// <summary>Normalises a plan's price to a monthly figure so MRR is comparable.</summary>
    private static long MonthlyValueCents(Domain.Billing.Plan plan) => plan.Interval switch
    {
        Domain.Billing.BillingInterval.Monthly => plan.Price.AmountInCents,
        Domain.Billing.BillingInterval.Quarterly => plan.Price.AmountInCents / 3,
        Domain.Billing.BillingInterval.Biannual => plan.Price.AmountInCents / 6,
        Domain.Billing.BillingInterval.Yearly => plan.Price.AmountInCents / 12,
        // Lifetime is not recurring revenue; counting it would inflate MRR permanently.
        _ => 0
    };

    public sealed record CourseFlagsBody(bool IsFeatured, bool IsNew, string? CoverImageUrl);

    public sealed record ReorderBody(IReadOnlyList<Guid> OrderedIds);

    public sealed record GrantBody(string ProductSlug, DateTimeOffset? ValidUntil, string Note);

    public sealed record RevokeBody(string ProductSlug, string Reason);

    public sealed record CreateAffiliateBody(string Email, string Code, decimal CommissionPercent, int RecurringMonths);

    public sealed record SettleBody(DateOnly PeriodStart, DateOnly PeriodEnd);

    public sealed record InvoiceRefBody(string InvoiceRef);

    public sealed record ReleasePackBody(string Version, string Changelog, DateOnly RegulatoryCheckDate);

    /// <summary>Los nombres de fichero a registrar, tal y como están en el volumen.</summary>
    public sealed record PackFilesBody(string? Folder, IReadOnlyList<string>? FileNames);

}
