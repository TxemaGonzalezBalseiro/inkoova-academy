using Inkoova.Academy.Application.Abstractions;
using Inkoova.Academy.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Inkoova.Academy.Application.Billing;

/// <summary>
/// Emite la factura fiscal de cada cobro y su rectificativa en los reembolsos (T-14).
///
/// El orden importa y no es casual: primero se reserva el número, luego se crea el REGISTRO de
/// facturación —que es lo que se declara y lo que calcula la huella—, y solo entonces se imprime
/// el PDF con esa huella. Al revés, el documento diría una huella y lo declarado llevaría otra.
///
/// Se llama desde el webhook pero <b>nunca lo hace fallar</b>: si la facturación no puede
/// completarse, el pago ya está cobrado y el acceso concedido, y devolver un 500 haría que
/// Stripe reintentase el evento entero, duplicando entitlements. El fallo se registra y la
/// factura se reemite después desde el panel.
/// </summary>
public sealed class FiscalInvoiceService(
    IFiscalInvoiceRepository invoices,
    IInvoiceDocumentRenderer renderer,
    VerifactuService verifactu,
    IUserRepository users,
    IContentStorage storage,
    BillingOptions options,
    ILogger<FiscalInvoiceService> logger)
{
    public async Task IssueForPaymentAsync(
        Guid userId,
        string stripeInvoiceId,
        Money total,
        Money tax,
        string concept,
        DateTimeOffset paidAt,
        string customerCountry,
        CancellationToken ct)
    {
        try
        {
            if (await invoices.GetByStripeInvoiceIdAsync(stripeInvoiceId, ct) is not null)
            {
                return;
            }

            var user = await users.GetByIdAsync(userId, ct);
            if (user is null)
            {
                logger.LogWarning("Cannot invoice {StripeInvoiceId}: user {UserId} not found.", stripeInvoiceId, userId);
                return;
            }

            await EmitAsync(
                user.Id, user.DisplayName, customerCountry, concept, total, tax,
                DateOnly.FromDateTime(paidAt.UtcDateTime), stripeInvoiceId,
                rectifiesNumber: null, rectifiesInvoiceId: null, ct);
        }
        catch (Exception ex)
        {
            // Ver el comentario de clase: la facturación no puede tumbar el webhook.
            logger.LogError(ex, "Unexpected failure invoicing {StripeInvoiceId}.", stripeInvoiceId);
        }
    }

    /// <summary>Rectificativa por reembolso. El importe va en positivo y el concepto lo marca.</summary>
    public async Task IssueRectificationAsync(
        Guid userId,
        string stripeInvoiceId,
        Money refunded,
        Money tax,
        DateTimeOffset refundedAt,
        string customerCountry,
        CancellationToken ct)
    {
        try
        {
            var original = await invoices.GetByStripeInvoiceIdAsync(stripeInvoiceId, ct);
            if (original is null)
            {
                logger.LogWarning(
                    "No original invoice for {StripeInvoiceId}; skipping rectification.", stripeInvoiceId);
                return;
            }

            var user = await users.GetByIdAsync(userId, ct);
            if (user is null)
            {
                return;
            }

            var originalNumber = $"{original.Series}-{original.Number:D6}";

            await EmitAsync(
                user.Id, user.DisplayName, customerCountry,
                $"Rectificación de la factura {originalNumber} por reembolso",
                refunded, tax, DateOnly.FromDateTime(refundedAt.UtcDateTime), stripeInvoiceId,
                originalNumber, original.Id, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected failure rectifying {StripeInvoiceId}.", stripeInvoiceId);
        }
    }

    /// <summary>
    /// El camino común de las dos: número, registro, PDF y fila. Está en un sitio porque emitir
    /// y rectificar solo se diferencian en el concepto y en a qué factura apuntan; tenerlo dos
    /// veces acabaría con una de las dos olvidándose de crear el registro.
    /// </summary>
    private async Task EmitAsync(
        Guid userId,
        string customerName,
        string customerCountry,
        string concept,
        Money total,
        Money tax,
        DateOnly issueDate,
        string stripeInvoiceId,
        string? rectifiesNumber,
        Guid? rectifiesInvoiceId,
        CancellationToken ct)
    {
        // El emisor sale de la marca principal: con «marca + correo, alumnos compartidos» la
        // facturación es una sola, aunque el catálogo se venda bajo varios nombres. Si algún día
        // cada marca factura por su cuenta, aquí entra la marca del producto vendido.
        var issuer = await verifactu.ResolveIssuerAsync(null, ct);

        if (issuer.IsFailure)
        {
            // Sin NIF no se emite. Una factura con el emisor a medias es un documento fiscal
            // inválido que además ha consumido un número de la serie, y los números no se
            // reutilizan.
            logger.LogError(
                "No se puede facturar {StripeInvoiceId}: {Error}",
                stripeInvoiceId, issuer.Error.Message);
            return;
        }

        var number = await invoices.GetNextNumberAsync(options.InvoiceSeries, ct);
        var fullNumber = $"{options.InvoiceSeries}-{number:D6}";

        // El identificador se decide aquí porque lo comparten los dos: el asiento referencia a
        // la factura por clave ajena, así que la factura se inserta primero y el asiento después.
        var invoiceId = Guid.CreateVersion7();

        var record = await verifactu.PrepareAltaAsync(
            invoiceId, issuer.Value, fullNumber, issueDate, concept,
            total.AmountInCents, tax.AmountInCents, total.Currency, rectifiesNumber, ct);

        if (record.IsFailure)
        {
            logger.LogError(
                "No se ha podido registrar la factura {Number}: {Error}",
                fullNumber, record.Error.Message);
            return;
        }

        var pdf = renderer.Render(new InvoiceDocument(
            issuer.Value,
            fullNumber,
            issueDate,
            customerName,
            // TODO(T-14): capturar el NIF/VAT del cliente en el checkout para B2B. Mientras no
            // se capture, la factura es simplificada (F2) y el registro lo dice.
            CustomerTaxId: null,
            customerCountry,
            concept,
            total,
            tax,
            record.Value.Hash,
            record.Value.PreviousHash,
            rectifiesNumber is not null,
            rectifiesNumber,
            record.Value.State == Domain.Billing.SubmissionState.Sent));

        var contentRef = $"invoices/{fullNumber}.pdf";

        using (var stream = new MemoryStream(pdf))
        {
            await storage.WriteAsync(contentRef, stream, ct);
        }

        await invoices.InsertAsync(
            new FiscalInvoice(
                invoiceId,
                userId,
                options.InvoiceSeries,
                number,
                issueDate,
                total.AmountInCents,
                tax.AmountInCents,
                total.Currency,
                stripeInvoiceId,
                record.Value.PreviousHash,
                record.Value.Hash,
                contentRef,
                rectifiesNumber is not null,
                rectifiesInvoiceId),
            ct);

        // El asiento va después de la factura porque la referencia por clave ajena. Si esto
        // fallara quedaría una factura sin registro: se ve en el panel de facturación, que
        // marca las que no lo tienen, y se rehace desde ahí.
        var saved = await verifactu.SaveAsync(record.Value, ct);

        if (saved.IsFailure)
        {
            logger.LogError(
                "La factura {Number} se ha emitido pero su registro no se ha guardado: {Error}",
                fullNumber, saved.Error.Message);
            return;
        }

        logger.LogInformation("Issued invoice {Number} for {StripeInvoiceId}.", fullNumber, stripeInvoiceId);
    }
}
