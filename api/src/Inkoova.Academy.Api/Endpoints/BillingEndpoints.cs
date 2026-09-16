using Inkoova.Academy.Api.Common;
using Inkoova.Academy.Application.Abstractions;
using Inkoova.Academy.Application.Billing;
using Inkoova.Academy.Domain.Common;
using Inkoova.Academy.Infrastructure.Payments;

namespace Inkoova.Academy.Api.Endpoints;

public static class BillingEndpoints
{
    public static void MapBillingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api").WithTags("Pagos");

        group.MapPost("/checkout/plans/{planCode}", async (
                string planCode,
                CheckoutBody? body,
                StartPlanCheckoutHandler handler,
                HttpContext context,
                CancellationToken ct) =>
            {
                var result = await handler.HandleAsync(
                    context.User.RequireUserId(), planCode, body?.DiscountCode, VisitorCookie.Read(context), ct);

                return result.ToHttp(url => Results.Ok(new { url }));
            })
            .RequireAuthorization()
            .WithSummary("Inicia el checkout de un plan y devuelve la URL de Stripe.");

        group.MapPost("/checkout/products/{productSlug}", async (
                string productSlug,
                CheckoutBody? body,
                StartProductCheckoutHandler handler,
                HttpContext context,
                CancellationToken ct) =>
            {
                var result = await handler.HandleAsync(
                    context.User.RequireUserId(), productSlug, body?.DiscountCode, VisitorCookie.Read(context), ct);

                return result.ToHttp(url => Results.Ok(new { url }));
            })
            .RequireAuthorization()
            .WithSummary("Compra única de curso, pack o programa vitalicio.");

        group.MapGet("/billing/portal", async (
                OpenBillingPortalHandler handler,
                HttpContext context,
                CancellationToken ct) =>
            {
                var result = await handler.HandleAsync(context.User.RequireUserId(), ct);
                return result.ToHttp(url => Results.Ok(new { url }));
            })
            .RequireAuthorization()
            .WithSummary("Abre el Customer Portal de Stripe.");

        group.MapGet("/me/subscription", async (
                GetMySubscriptionHandler handler,
                HttpContext context,
                CancellationToken ct) =>
                Results.Ok(await handler.HandleAsync(context.User.RequireUserId(), ct)))
            .RequireAuthorization()
            .WithSummary("Estado de la suscripción y productos en propiedad.");

        group.MapGet("/me/invoices", async (
                IFiscalInvoiceRepository invoices,
                HttpContext context,
                CancellationToken ct) =>
            {
                var mine = await invoices.GetForUserAsync(context.User.RequireUserId(), ct);

                return Results.Ok(mine.Select(i => new
                {
                    number = $"{i.Series}-{i.Number:D6}",
                    issueDate = i.IssueDate,
                    total = i.TotalCents / 100m,
                    tax = i.TaxCents / 100m,
                    currency = i.Currency,
                    isRectification = i.IsRectification,
                    hasPdf = i.PdfContentRef is not null
                }));
            })
            .RequireAuthorization()
            .WithSummary("Facturas fiscales del alumno (T-14).");

        group.MapGet("/me/invoices/{number}/pdf", async (
                string number,
                IFiscalInvoiceRepository invoices,
                IContentStorage storage,
                HttpContext context,
                CancellationToken ct) =>
            {
                var mine = await invoices.GetForUserAsync(context.User.RequireUserId(), ct);

                // Se busca dentro de las facturas del usuario, no por número global: así no
                // hay forma de leer la factura de otra persona probando números.
                var invoice = mine.FirstOrDefault(i => $"{i.Series}-{i.Number:D6}" == number);

                if (invoice?.PdfContentRef is null)
                {
                    return Error.NotFound("invoice.not_found", "No existe esa factura.").ToProblem();
                }

                var stream = await storage.OpenReadAsync(invoice.PdfContentRef, ct);

                return stream is null
                    ? Error.NotFound("invoice.pdf_missing", "El PDF no está disponible.").ToProblem()
                    : Results.Stream(stream, "application/pdf", $"{number}.pdf");
            })
            .RequireAuthorization()
            .WithSummary("Descarga el PDF de una factura propia.");

        // ── webhook ────────────────────────────────────────────────────────────────────

        app.MapPost("/api/webhooks/stripe", async (
                HttpContext context,
                StripeEventMapper mapper,
                StripeWebhookProcessor processor,
                ILogger<StripeWebhookProcessor> logger,
                CancellationToken ct) =>
            {
                using var reader = new StreamReader(context.Request.Body);
                var json = await reader.ReadToEndAsync(ct);

                var signature = context.Request.Headers["Stripe-Signature"].ToString();
                var parsed = mapper.Parse(json, signature);

                if (parsed.IsFailure)
                {
                    // A bad signature is not a Stripe retry candidate: answering 400 stops it.
                    logger.LogWarning("Rejected Stripe webhook: {Error}", parsed.Error);
                    return Results.BadRequest();
                }

                var result = await processor.ProcessAsync(parsed.Value, ct);

                // A 500 makes Stripe retry, which is what we want when our side failed.
                return result.Match<IResult>(
                    _ => Results.Ok(),
                    error => error.Kind == ErrorKind.Validation
                        ? Results.BadRequest()
                        : Results.StatusCode(StatusCodes.Status500InternalServerError));
            })
            .AllowAnonymous()
            .WithTags("Pagos")
            .WithSummary("Webhook de Stripe. Idempotente por event id.");
    }

    public sealed record CheckoutBody(string? DiscountCode);
}
