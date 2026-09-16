using System.Text.Json;
using Inkoova.Academy.Api.Common;
using Inkoova.Academy.Application.Abstractions;
using Inkoova.Academy.Application.Billing;

namespace Inkoova.Academy.Api.Endpoints;

/// <summary>
/// Facturación fiscal: las cadenas de registros de Veri*Factu, su verificación y el estado de
/// la remisión a la AEAT.
///
/// Todo es de administración. Aquí se ve la facturación entera de la academia, que es dato
/// económico de terceros; el alumno ve las suyas en <c>/api/me/invoices</c> y solo las suyas.
/// </summary>
public static class InvoicingEndpoints
{
    public static void MapInvoicingEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/admin/facturacion")
            .WithTags("Facturación")
            .RequireAuthorization("admin");

        admin.MapGet("/estado", async (
                VerifyVerifactuChainHandler chains,
                VerifactuService verifactu,
                IVerifactuSubmitter submitter,
                CancellationToken ct) =>
            {
                var reports = await chains.HandleAsync(ct);
                var issuer = await verifactu.ResolveIssuerAsync(null, ct);

                return Results.Ok(new
                {
                    // Si el envío está conectado. Es lo primero que hay que saber: sin él, todo
                    // lo demás está bien y nada está declarado.
                    submissionEnabled = submitter.IsEnabled,

                    // Y si además está descrito el sistema informático, que el registro exige
                    // aparte del emisor. Sin ese bloque la AEAT rechaza el registro entero, así
                    // que se avisa aquí y no en el primer cobro real.
                    softwareDeclared = verifactu.Software.IsComplete,
                    canSubmit = verifactu.CanSubmit,

                    // Si se puede facturar. Sin NIF de la marca no se emite ninguna factura, y
                    // eso se ve aquí y no cuando falle el primer cobro.
                    canIssue = issuer.IsSuccess,
                    issuerProblem = issuer.IsSuccess ? null : issuer.Error.Message,
                    issuer = issuer.IsSuccess
                        ? new { issuer.Value.Name, issuer.Value.TaxId, issuer.Value.Address }
                        : null,

                    chains = reports
                });
            })
            .WithSummary("Estado de la facturación: emisor, envío a la AEAT y cadenas de registros.");

        admin.MapGet("/cadena/{issuerTaxId}", async (
                string issuerTaxId,
                VerifyVerifactuChainHandler chains,
                CancellationToken ct) =>
                Results.Ok(await chains.VerifyAsync(issuerTaxId, ct)))
            .WithSummary("Verifica una cadena entera y dice qué eslabón está roto, si lo hay.");

        admin.MapPost("/reintentar", async (
                VerifactuService verifactu,
                IVerifactuSubmitter submitter,
                IAuditLogRepository audit,
                HttpContext context,
                IClock clock,
                CancellationToken ct) =>
            {
                if (!submitter.IsEnabled)
                {
                    // Decirlo en vez de contestar «0 reenviados», que suena a que no había nada
                    // pendiente cuando lo que pasa es que no hay a dónde enviarlo.
                    return Results.Problem(
                        title: "Envío no configurado",
                        detail: "El envío a la AEAT no está conectado. Falta la librería Veri*Factu " +
                                "y el certificado cualificado.",
                        statusCode: StatusCodes.Status409Conflict);
                }

                var sent = await verifactu.SubmitPendingAsync(ct);

                await audit.AppendAsync(
                    new AuditEntry(
                        Guid.CreateVersion7(),
                        context.User.RequireUserId(),
                        "verifactu.retry",
                        "verifactu_record",
                        null,
                        JsonSerializer.Serialize(new { sent }),
                        clock.UtcNow),
                    ct);

                return Results.Ok(new { sent });
            })
            .WithSummary("Reintenta la remisión de lo pendiente y lo rechazado.");
    }
}
