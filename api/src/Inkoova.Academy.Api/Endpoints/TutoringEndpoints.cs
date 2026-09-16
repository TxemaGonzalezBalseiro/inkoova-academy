using System.Text.Json;
using Inkoova.Academy.Api.Common;
using Inkoova.Academy.Application.Abstractions;
using Inkoova.Academy.Application.Tutoring;

namespace Inkoova.Academy.Api.Endpoints;

/// <summary>
/// Tutorías: el catálogo de paquetes, lo concedido a cada alumno y lo consumido.
///
/// Dos grupos con permisos distintos a propósito. El alumno solo lee lo suyo y nunca recibe el
/// identificador de otro: su bolsa sale del usuario del token, no de un parámetro de la ruta.
/// Conceder horas y apuntar consumo es del panel, y queda en el registro de auditoría porque
/// mueve algo que se ha pagado.
/// </summary>
public static class TutoringEndpoints
{
    public static void MapTutoringEndpoints(this IEndpointRouteBuilder app)
    {
        MapStudentEndpoints(app);
        MapAdminEndpoints(app);
    }

    private static void MapStudentEndpoints(IEndpointRouteBuilder app)
    {
        var me = app.MapGroup("/api/me/tutoring")
            .WithTags("Tutorías")
            .RequireAuthorization();

        me.MapGet("/", async (GetMyTutoringHandler handler, HttpContext context, CancellationToken ct) =>
                Results.Ok(await handler.HandleAsync(context.User.RequireUserId(), ct)))
            .WithSummary("Tutorías del alumno: qué tiene, qué ha consumido y qué le queda.");

        // El escaparate de paquetes es público: se enseña en la página de precios a quien
        // todavía no tiene cuenta.
        app.MapGet("/api/tutoring/packages", async (
                ListTutoringPackagesHandler handler,
                CancellationToken ct) =>
                Results.Ok(await handler.HandleAsync(onlyActive: true, ct)))
            .AllowAnonymous()
            .WithTags("Tutorías")
            .WithSummary("Paquetes de tutorías a la venta.");
    }

    private static void MapAdminEndpoints(IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/admin/tutoring")
            .WithTags("Tutorías")
            .RequireAuthorization("admin");

        // ── catálogo ───────────────────────────────────────────────────────────────────

        admin.MapGet("/packages", async (ListTutoringPackagesHandler handler, CancellationToken ct) =>
                Results.Ok(await handler.HandleAsync(onlyActive: false, ct)))
            .WithSummary("Todos los paquetes, retirados incluidos.");

        admin.MapPost("/packages", async (
                SaveTutoringPackageRequest body,
                SaveTutoringPackageHandler handler,
                IAuditLogRepository audit,
                HttpContext context,
                IClock clock,
                CancellationToken ct) =>
            {
                var result = await handler.HandleAsync(body, ct);

                if (result.IsSuccess)
                {
                    await Audit(audit, context, clock,
                        body.Id is null ? "tutoring_package.create" : "tutoring_package.update",
                        "tutoring_package", result.Value.Id.ToString(),
                        JsonSerializer.Serialize(new { body.Slug, body.Minutes, body.PriceCents }), ct);
                }

                return result.ToHttp(dto => Results.Ok(dto));
            })
            .WithSummary("Crea o edita un paquete. Sin Id crea; con Id edita.");

        admin.MapPost("/packages/{packageId:guid}/active", async (
                Guid packageId,
                ActiveBody body,
                SetTutoringPackageActiveHandler handler,
                IAuditLogRepository audit,
                HttpContext context,
                IClock clock,
                CancellationToken ct) =>
            {
                var result = await handler.HandleAsync(packageId, body.Active, ct);

                if (result.IsSuccess)
                {
                    await Audit(audit, context, clock,
                        body.Active ? "tutoring_package.activate" : "tutoring_package.deactivate",
                        "tutoring_package", packageId.ToString(), null, ct);
                }

                return result.ToNoContent();
            })
            .WithSummary("Pone o quita un paquete del escaparate. Nunca lo borra.");

        admin.MapPost("/stripe/sync", async (
                SyncTutoringWithStripeHandler handler,
                IAuditLogRepository audit,
                HttpContext context,
                IClock clock,
                CancellationToken ct) =>
            {
                var result = await handler.HandleAsync(ct);

                if (result.IsSuccess)
                {
                    await Audit(audit, context, clock, "tutoring.stripe_sync", "tutoring_package", null,
                        JsonSerializer.Serialize(result.Value.Select(r => new { r.Slug, r.Linked })), ct);
                }

                return result.ToHttp(items => Results.Ok(items));
            })
            .WithSummary("Crea en Stripe los precios que falten y los enlaza. Idempotente.");

        // ── alumnos ────────────────────────────────────────────────────────────────────

        admin.MapGet("/balances", async (ListTutoringBalancesHandler handler, CancellationToken ct) =>
                Results.Ok(await handler.HandleAsync(ct)))
            .WithSummary("Alumnos con tutorías y su saldo.");

        admin.MapGet("/students/{userId:guid}", async (
                Guid userId,
                GetStudentTutoringHandler handler,
                CancellationToken ct) =>
                (await handler.HandleAsync(userId, ct)).ToHttp())
            .WithSummary("Ficha de tutorías de un alumno, con sus bolsas y su histórico.");

        admin.MapPost("/grants", async (
                GrantTutoringRequest body,
                GrantTutoringHandler handler,
                IAuditLogRepository audit,
                HttpContext context,
                IClock clock,
                CancellationToken ct) =>
            {
                var result = await handler.HandleAsync(body, context.User.RequireUserId(), ct);

                if (result.IsSuccess)
                {
                    // Horas concedidas: tiene consecuencia económica, se registra siempre.
                    await Audit(audit, context, clock, "tutoring_grant.create", "tutoring_grant",
                        result.Value.Id.ToString(),
                        JsonSerializer.Serialize(new
                        {
                            body.UserId,
                            body.PackageId,
                            result.Value.MinutesTotal,
                            body.Source
                        }), ct);
                }

                return result.ToHttp(dto => Results.Ok(dto));
            })
            .WithSummary("Concede una bolsa de tutorías a un alumno.");

        admin.MapPost("/grants/{grantId:guid}/sessions", async (
                Guid grantId,
                RecordTutoringSessionRequest body,
                RecordTutoringSessionHandler handler,
                IAuditLogRepository audit,
                HttpContext context,
                IClock clock,
                CancellationToken ct) =>
            {
                var result = await handler.HandleAsync(grantId, body, context.User.RequireUserId(), ct);

                if (result.IsSuccess)
                {
                    await Audit(audit, context, clock, "tutoring_session.record", "tutoring_grant",
                        grantId.ToString(),
                        JsonSerializer.Serialize(new
                        {
                            body.Minutes,
                            body.Topic,
                            body.OccurredAt,
                            // Quién la dio va al registro porque de ahí sale lo que se le paga.
                            body.TutorId
                        }), ct);
                }

                return result.ToHttp(dto => Results.Ok(dto));
            })
            .WithSummary("Apunta una tutoría dada y descuenta el tiempo del saldo.");

        admin.MapDelete("/grants/{grantId:guid}/sessions/{sessionId:guid}", async (
                Guid grantId,
                Guid sessionId,
                DeleteTutoringSessionHandler handler,
                IAuditLogRepository audit,
                HttpContext context,
                IClock clock,
                CancellationToken ct) =>
            {
                var result = await handler.HandleAsync(grantId, sessionId, ct);

                if (result.IsSuccess)
                {
                    // Devolver tiempo al saldo se audita igual que gastarlo: son la misma cifra
                    // en direcciones opuestas.
                    await Audit(audit, context, clock, "tutoring_session.delete", "tutoring_grant",
                        grantId.ToString(), JsonSerializer.Serialize(new { sessionId }), ct);
                }

                return result.ToHttp(dto => Results.Ok(dto));
            })
            .WithSummary("Deshace una tutoría mal apuntada y devuelve el tiempo al saldo.");

        admin.MapPost("/grants/{grantId:guid}/revoke", async (
                Guid grantId,
                RevokeBody body,
                RevokeTutoringGrantHandler handler,
                IAuditLogRepository audit,
                HttpContext context,
                IClock clock,
                CancellationToken ct) =>
            {
                var result = await handler.HandleAsync(grantId, body.Reason, ct);

                if (result.IsSuccess)
                {
                    await Audit(audit, context, clock, "tutoring_grant.revoke", "tutoring_grant",
                        grantId.ToString(), JsonSerializer.Serialize(new { body.Reason }), ct);
                }

                return result.ToHttp(dto => Results.Ok(dto));
            })
            .WithSummary("Revoca una bolsa. Las tutorías ya dadas se conservan.");

        admin.MapPut("/grants/{grantId:guid}/total", async (
                Guid grantId,
                AdjustBody body,
                AdjustTutoringGrantHandler handler,
                IAuditLogRepository audit,
                HttpContext context,
                IClock clock,
                CancellationToken ct) =>
            {
                var result = await handler.HandleAsync(grantId, body.MinutesTotal, ct);

                if (result.IsSuccess)
                {
                    await Audit(audit, context, clock, "tutoring_grant.adjust", "tutoring_grant",
                        grantId.ToString(), JsonSerializer.Serialize(new { body.MinutesTotal }), ct);
                }

                return result.ToHttp(dto => Results.Ok(dto));
            })
            .WithSummary("Corrige el tiempo concedido. Nunca por debajo de lo ya consumido.");

        MapAppointmentEndpoints(admin);
    }

    /// <summary>
    /// Tutorías CONVOCADAS, que no es lo mismo que dadas: no descuentan saldo hasta que se
    /// confirman, y mandan invitación de calendario al alumno y al profesor.
    /// </summary>
    private static void MapAppointmentEndpoints(IEndpointRouteBuilder admin)
    {
        admin.MapGet("/appointments", async (
                ListUpcomingAppointmentsHandler handler,
                CancellationToken ct) =>
                Results.Ok(await handler.HandleAsync(ct)))
            .WithSummary("Agenda: tutorías convocadas y todavía sin apuntar.");

        admin.MapPost("/appointments", async (
                ScheduleTutoringRequest body,
                ScheduleTutoringHandler handler,
                IAuditLogRepository audit,
                HttpContext context,
                IClock clock,
                CancellationToken ct) =>
            {
                var result = await handler.HandleAsync(body, context.User.RequireUserId(), ct);

                if (result.IsSuccess)
                {
                    await Audit(audit, context, clock, "tutoring_appointment.create",
                        "tutoring_appointment", result.Value.Id.ToString(),
                        JsonSerializer.Serialize(new { body.GrantId, body.TutorId, body.StartsAt, body.Minutes }), ct);
                }

                return result.ToHttp(dto => Results.Ok(dto));
            })
            .WithSummary("Convoca una tutoría y manda la invitación de calendario.");

        admin.MapPut("/appointments/{appointmentId:guid}", async (
                Guid appointmentId,
                ScheduleTutoringRequest body,
                RescheduleTutoringHandler handler,
                IAuditLogRepository audit,
                HttpContext context,
                IClock clock,
                CancellationToken ct) =>
            {
                var result = await handler.HandleAsync(appointmentId, body, ct);

                if (result.IsSuccess)
                {
                    await Audit(audit, context, clock, "tutoring_appointment.reschedule",
                        "tutoring_appointment", appointmentId.ToString(),
                        JsonSerializer.Serialize(new { body.TutorId, body.StartsAt, body.Minutes }), ct);
                }

                return result.ToHttp(dto => Results.Ok(dto));
            })
            .WithSummary("Cambia una convocatoria y reenvía la invitación actualizada.");

        admin.MapPost("/appointments/{appointmentId:guid}/cancel", async (
                Guid appointmentId,
                RevokeBody body,
                CancelTutoringAppointmentHandler handler,
                IAuditLogRepository audit,
                HttpContext context,
                IClock clock,
                CancellationToken ct) =>
            {
                var result = await handler.HandleAsync(appointmentId, body.Reason, ct);

                if (result.IsSuccess)
                {
                    await Audit(audit, context, clock, "tutoring_appointment.cancel",
                        "tutoring_appointment", appointmentId.ToString(),
                        JsonSerializer.Serialize(new { body.Reason }), ct);
                }

                return result.ToHttp(dto => Results.Ok(dto));
            })
            .WithSummary("Anula una convocatoria y libera el hueco en los calendarios.");

        admin.MapPost("/appointments/{appointmentId:guid}/confirm", async (
                Guid appointmentId,
                ConfirmAppointmentRequest body,
                ConfirmTutoringAppointmentHandler handler,
                IAuditLogRepository audit,
                HttpContext context,
                IClock clock,
                CancellationToken ct) =>
            {
                var result = await handler.HandleAsync(appointmentId, body, context.User.RequireUserId(), ct);

                if (result.IsSuccess)
                {
                    // Confirmar descuenta saldo y devenga: tiene consecuencia económica igual
                    // que apuntar una tutoría suelta, y se registra igual.
                    await Audit(audit, context, clock, "tutoring_appointment.confirm",
                        "tutoring_appointment", appointmentId.ToString(),
                        JsonSerializer.Serialize(new { body.Minutes, result.Value.SessionId }), ct);
                }

                return result.ToHttp(dto => Results.Ok(dto));
            })
            .WithSummary("Da por dada una tutoría convocada: descuenta el tiempo y devenga.");
    }

    private static Task Audit(
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

    public sealed record ActiveBody(bool Active);

    public sealed record RevokeBody(string Reason);

    public sealed record AdjustBody(int MinutesTotal);
}
