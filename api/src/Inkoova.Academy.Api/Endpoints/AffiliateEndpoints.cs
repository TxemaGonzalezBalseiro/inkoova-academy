using Inkoova.Academy.Api.Common;
using Inkoova.Academy.Application.Abstractions;
using Inkoova.Academy.Application.Affiliates;
using Inkoova.Academy.Domain.Common;

namespace Inkoova.Academy.Api.Endpoints;

public static class AffiliateEndpoints
{
    public static void MapAffiliateEndpoints(this IEndpointRouteBuilder app)
    {
        // Click tracking is anonymous and cheap; the SPA calls it on first load with ?ref=.
        app.MapPost("/api/referrals/{code}", async (
                string code,
                TrackReferralHandler handler,
                HttpContext context,
                CancellationToken ct) =>
            {
                var visitorId = VisitorCookie.GetOrCreate(context);
                await handler.HandleAsync(code, visitorId, ct);

                // Always 204: an unknown code must not tell a visitor which codes exist.
                return Results.NoContent();
            })
            .AllowAnonymous()
            .RequireRateLimiting("referral")
            .WithTags("Afiliados")
            .WithSummary("Registra un clic de referido y fija la cookie de atribución.");

        var group = app.MapGroup("/api/affiliate").WithTags("Afiliados").RequireAuthorization();

        group.MapGet("/", async (
                GetAffiliateDashboardHandler handler,
                HttpContext context,
                CancellationToken ct) =>
                (await handler.HandleAsync(context.User.RequireUserId(), ct)).ToHttp())
            .WithSummary("Panel del afiliado: clics, conversiones, comisiones y liquidaciones.");

        group.MapPut("/tax-data", async (
                TaxDataBody body,
                UpdateAffiliateTaxDataHandler handler,
                HttpContext context,
                CancellationToken ct) =>
                (await handler.HandleAsync(
                    context.User.RequireUserId(), body.TaxId, body.CountryCode, body.Iban, ct)).ToNoContent())
            .WithSummary("Datos fiscales necesarios para poder liquidar.");

        group.MapGet("/payouts/{payoutId:guid}/statement", async (
                Guid payoutId,
                IPayoutRepository payouts,
                IAffiliateRepository affiliates,
                IContentStorage storage,
                HttpContext context,
                CancellationToken ct) =>
            {
                var affiliate = await affiliates.GetByUserIdAsync(context.User.RequireUserId(), ct);
                if (affiliate is null)
                {
                    return Error.NotFound("affiliate.not_found", "No tienes un perfil de afiliado.").ToProblem();
                }

                var payout = await payouts.GetByIdAsync(payoutId, ct);

                // Ownership check, not just existence: an affiliate must never read another's
                // settlement (T-16 acceptance criteria).
                if (payout is null || payout.AffiliateId != affiliate.Id)
                {
                    return Error.NotFound("payout.not_found", "No existe esa liquidación.").ToProblem();
                }

                if (payout.StatementContentRef is null)
                {
                    return Error.NotFound("payout.no_statement", "La liquidación no tiene documento.").ToProblem();
                }

                var stream = await storage.OpenReadAsync(payout.StatementContentRef, ct);
                return stream is null
                    ? Error.NotFound("payout.no_statement", "La liquidación no tiene documento.").ToProblem()
                    : Results.Stream(stream, "application/pdf", $"liquidacion-{payout.PeriodStart:yyyy-MM}.pdf");
            })
            .WithSummary("Descarga el PDF de una liquidación propia.");
    }

    public sealed record TaxDataBody(string TaxId, string CountryCode, string Iban);
}
