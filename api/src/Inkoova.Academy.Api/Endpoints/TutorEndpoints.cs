using System.Text.Json;
using Inkoova.Academy.Api.Common;
using Inkoova.Academy.Application.Abstractions;
using Inkoova.Academy.Application.Tutoring;

namespace Inkoova.Academy.Api.Endpoints;

/// <summary>
/// Profesores de tutorías y lo que se les paga.
///
/// Todo el grupo es de administración: aquí se ve quién ha dado qué y cuánto se le debe, que es
/// información económica de terceros. La lista de nombres para elegir profesor al apuntar una
/// tutoría sale del mismo sitio, porque quien apunta también es del panel.
/// </summary>
public static class TutorEndpoints
{
    public static void MapTutorEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/admin/tutors")
            .WithTags("Profesores")
            .RequireAuthorization("admin");

        admin.MapGet("/", async (ListTutorsHandler handler, CancellationToken ct) =>
                Results.Ok(await handler.HandleAsync(ct)))
            .WithSummary("Profesores con lo que se les debe y lo ya pagado.");

        admin.MapGet("/{tutorId:guid}", async (
                Guid tutorId,
                GetTutorHandler handler,
                CancellationToken ct) =>
                (await handler.HandleAsync(tutorId, ct)).ToHttp())
            .WithSummary("Ficha de un profesor con el detalle de lo devengado.");

        admin.MapPost("/", async (
                SaveTutorRequest body,
                SaveTutorHandler handler,
                IAuditLogRepository audit,
                HttpContext context,
                IClock clock,
                CancellationToken ct) =>
            {
                var result = await handler.HandleAsync(null, body, ct);

                if (result.IsSuccess)
                {
                    await Audit(audit, context, clock, "tutor.create", result.Value.Id,
                        new { body.DisplayName, body.Email, body.CommissionPercent }, ct);
                }

                return result.ToHttp(dto => Results.Ok(dto));
            })
            .WithSummary("Da de alta un profesor.");

        admin.MapPut("/{tutorId:guid}", async (
                Guid tutorId,
                SaveTutorRequest body,
                SaveTutorHandler handler,
                IAuditLogRepository audit,
                HttpContext context,
                IClock clock,
                CancellationToken ct) =>
            {
                var result = await handler.HandleAsync(tutorId, body, ct);

                if (result.IsSuccess)
                {
                    // El porcentaje se audita siempre: es el acuerdo económico, y cambiarlo sin
                    // rastro deja una liquidación futura que nadie sabe de dónde salió.
                    await Audit(audit, context, clock, "tutor.update", tutorId,
                        new { body.DisplayName, body.Email, body.CommissionPercent, body.IsActive }, ct);
                }

                return result.ToHttp(dto => Results.Ok(dto));
            })
            .WithSummary("Edita un profesor. Cambiar su porcentaje no toca lo ya devengado.");

        admin.MapPost("/{tutorId:guid}/pay", async (
                Guid tutorId,
                PayTutorEarningsRequest body,
                PayTutorEarningsHandler handler,
                IAuditLogRepository audit,
                HttpContext context,
                IClock clock,
                CancellationToken ct) =>
            {
                var result = await handler.HandleAsync(tutorId, body, ct);

                if (result.IsSuccess)
                {
                    await Audit(audit, context, clock, "tutor.pay", tutorId,
                        new { count = body.EarningIds?.Count ?? 0, body.Note }, ct);
                }

                return result.ToHttp(dto => Results.Ok(dto));
            })
            .WithSummary("Apunta como pagados los devengos que se le han liquidado.");
    }

    private static Task Audit(
        IAuditLogRepository audit,
        HttpContext context,
        IClock clock,
        string action,
        Guid tutorId,
        object details,
        CancellationToken ct) =>
        audit.AppendAsync(
            new AuditEntry(
                Guid.CreateVersion7(),
                context.User.RequireUserId(),
                action,
                "tutor",
                tutorId.ToString(),
                JsonSerializer.Serialize(details),
                clock.UtcNow),
            ct);
}
