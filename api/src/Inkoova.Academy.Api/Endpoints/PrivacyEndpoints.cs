using System.Text.Json;
using Inkoova.Academy.Api.Common;
using Inkoova.Academy.Application.Abstractions;

namespace Inkoova.Academy.Api.Endpoints;

public static class PrivacyEndpoints
{
    /// <summary>
    /// Version of the privacy and cookie policy the consent refers to. Bumping it makes the
    /// banner ask again, which is what the regulation expects when the terms change (T-14).
    /// </summary>
    private const string PolicyVersion = "2026-08-30";

    public static void MapPrivacyEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/privacy").WithTags("Privacidad");

        group.MapPost("/consent", async (
                ConsentBody body,
                IConsentRepository consents,
                IClock clock,
                HttpContext context,
                CancellationToken ct) =>
            {
                var visitorId = VisitorCookie.GetOrCreate(context);

                await consents.RecordAsync(
                    new ConsentRecord(
                        Guid.CreateVersion7(),
                        context.User.UserId(),
                        visitorId,
                        body.Analytics,
                        body.Marketing,
                        PolicyVersion,
                        clock.UtcNow),
                    ct);

                return Results.NoContent();
            })
            .AllowAnonymous()
            .WithSummary("Registra la decisión del banner de cookies.");

        group.MapGet("/consent", async (
                IConsentRepository consents,
                HttpContext context,
                CancellationToken ct) =>
            {
                var visitorId = VisitorCookie.Read(context);

                if (visitorId is null)
                {
                    return Results.Ok(new { policyVersion = PolicyVersion, analytics = false, marketing = false, decided = false });
                }

                var latest = await consents.GetLatestAsync(visitorId, ct);

                // A consent recorded against an older policy version does not count as a
                // decision: the banner must be shown again.
                var decided = latest is not null && latest.PolicyVersion == PolicyVersion;

                return Results.Ok(new
                {
                    policyVersion = PolicyVersion,
                    analytics = decided && latest!.Analytics,
                    marketing = decided && latest!.Marketing,
                    decided
                });
            })
            .AllowAnonymous()
            .WithSummary("Estado del consentimiento para esta visita.");

        // ── portabilidad ───────────────────────────────────────────────────────────────

        app.MapGet("/api/me/data-export", async (
                IUserRepository users,
                IProgressRepository progress,
                ICourseRepository courses,
                IQuizRepository quizzes,
                ICertificateRepository certificates,
                IPurchaseRepository purchases,
                IEntitlementRepository entitlements,
                HttpContext context,
                CancellationToken ct) =>
            {
                var userId = context.User.RequireUserId();
                var user = await users.GetByIdAsync(userId, ct);

                if (user is null)
                {
                    return Results.NotFound();
                }

                var catalog = await courses.GetPublicCatalogAsync(ct);
                var progressByCourse = new List<object>();

                foreach (var course in catalog)
                {
                    var rows = await progress.GetForCourseAsync(userId, course.Id, ct);
                    if (rows.Count == 0)
                    {
                        continue;
                    }

                    progressByCourse.Add(new
                    {
                        curso = course.Slug.Value,
                        lecciones = rows.Select(r => new
                        {
                            leccion = course.AllLessons.FirstOrDefault(l => l.Id == r.LessonId)?.Slug.Value,
                            completada = r.CompletedAt,
                            ultimaPosicion = r.LastPositionRef
                        })
                    });
                }

                var export = new
                {
                    exportadoEl = DateTimeOffset.UtcNow,
                    cuenta = new { user.Email, user.DisplayName, user.CreatedAt },
                    progreso = progressByCourse,
                    intentosDeTest = (await quizzes.GetAttemptsForUserAsync(userId, ct))
                        .Select(a => new { a.Score, a.TotalQuestions, a.Verdict, a.TakenAt }),
                    certificados = (await certificates.GetForUserAsync(userId, ct))
                        .Select(c => new { codigo = c.Code.Value, c.IssuedAt, revocado = c.RevokedAt }),
                    compras = (await purchases.GetForUserAsync(userId, ct))
                        .Select(p => new
                        {
                            importe = p.GrossAmount.ToDecimal(),
                            moneda = p.GrossAmount.Currency,
                            estado = p.Status.ToString(),
                            p.PaidAt
                        }),
                    accesos = (await entitlements.GetForUserAsync(userId, ct))
                        .Select(e => new { origen = e.Source.ToString(), e.ValidFrom, e.ValidUntil })
                };

                var json = JsonSerializer.SerializeToUtf8Bytes(
                    export, new JsonSerializerOptions { WriteIndented = true });

                return Results.File(json, "application/json", "inkoova-academy-datos.json");
            })
            .RequireAuthorization()
            .WithTags("Privacidad")
            .WithSummary("Exporta los datos personales del alumno (RGPD art. 20).");
    }

    public sealed record ConsentBody(bool Analytics, bool Marketing);
}
