using System.Globalization;
using Inkoova.Academy.Application.Abstractions;
using Inkoova.Academy.Domain.Billing;
using Inkoova.Academy.Infrastructure.Documents;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Inkoova.Academy.Infrastructure.Invoicing;

public sealed record InvoicingOptions
{
    /// <summary>
    /// La serie de numeración. Es lo único del emisor que sigue en configuración: la razón
    /// social, el NIF y el domicilio salen de los datos legales de la marca, que es donde se
    /// editan y donde el aviso legal ya los lee.
    /// </summary>
    public string Series { get; init; } = "INK";

    /// <summary>
    /// Contra qué entorno de la AEAT se trabaja.
    ///
    /// Manda a la vez sobre el endpoint de envío y sobre la URL del QR, y por eso es UN solo
    /// ajuste y no dos: con dos, alguien acabaría declarando en producción con el QR de pruebas
    /// impreso en las facturas, y cada cliente que lo escanease acabaría en un portal donde su
    /// factura no existe.
    ///
    /// Por defecto, pruebas. Pasar a producción tiene que ser una decisión explícita.
    /// </summary>
    public AeatEnvironment Environment { get; init; } = AeatEnvironment.Pruebas;

    /// <summary>
    /// Si el sistema remite los registros a la AEAT («facturas verificables»). Cambia el
    /// servicio al que apunta el QR y la leyenda que se imprime debajo.
    /// </summary>
    public bool Verifiable { get; init; }
}

/// <summary>
/// El PDF de la factura.
///
/// Aquí NO se decide nada fiscal: ni el número, ni la huella, ni si está declarada. Todo eso lo
/// trae ya resuelto el <see cref="InvoiceDocument"/> desde el registro de facturación. Esta
/// clase imprime, y si imprimir falla no se ha declarado nada de menos.
/// </summary>
public sealed class InvoiceDocumentRenderer(InvoicingOptions options) : IInvoiceDocumentRenderer
{
    public byte[] Render(InvoiceDocument invoice)
    {
        var net = invoice.Total.AmountInCents - invoice.Tax.AmountInCents;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(text => text.FontSize(10).FontFamily(Fonts.Calibri));

                // El QR va ARRIBA del todo y centrado, antes del contenido de la factura, con su
                // texto encima y la leyenda debajo. Lo pide el apartado 3 de las especificaciones
                // del código QR: tiene que ser el primero de la factura y ocupar un lugar
                // preeminente, no una esquina del pie.
                page.Header().Column(head =>
                {
                    head.Item().AlignCenter().Text(VerifactuQr.Heading).FontSize(9).SemiBold();

                    head.Item().AlignCenter()
                        .Width(110).Height(110)
                        // Margen blanco alrededor: la norma pide 2 mm como mínimo y recomienda 6.
                        .Padding(6, Unit.Millimetre)
                        .Image(QrCode.Generate(BuildQrUrl(invoice)));

                    if (options.Verifiable)
                    {
                        head.Item().AlignCenter().PaddingBottom(12)
                            .Text(VerifactuQr.VerifiableNotice).FontSize(9);
                    }

                    head.Item().Row(row =>
                    {
                        row.RelativeItem().Column(left =>
                        {
                            left.Item().Text(invoice.Issuer.Name).FontSize(14).Bold();
                            left.Item().Text(invoice.Issuer.TaxId);

                            if (!string.IsNullOrWhiteSpace(invoice.Issuer.Address))
                            {
                                left.Item().Text(invoice.Issuer.Address);
                            }
                        });

                        row.RelativeItem().AlignRight().Column(right =>
                        {
                            right.Item()
                                .Text(invoice.IsRectification ? "Factura rectificativa" : "Factura")
                                .FontSize(14).Bold();
                            right.Item().Text(invoice.FullNumber);
                            right.Item().Text($"Fecha: {invoice.IssueDate:dd/MM/yyyy}");

                            if (invoice.IsRectification && invoice.RectifiedNumber is not null)
                            {
                                right.Item().Text($"Rectifica a: {invoice.RectifiedNumber}");
                            }
                        });
                    });
                });

                page.Content().PaddingVertical(20).Column(column =>
                {
                    column.Spacing(12);

                    column.Item().Column(customer =>
                    {
                        customer.Item().Text("Cliente").SemiBold();
                        customer.Item().Text(invoice.CustomerName);
                        customer.Item().Text(invoice.CustomerTaxId ?? "Consumidor final");
                        customer.Item().Text($"País: {invoice.CustomerCountry}");
                    });

                    column.Item().PaddingTop(10).Row(row =>
                    {
                        row.RelativeItem(3).Text(invoice.Concept);
                        row.RelativeItem().AlignRight().Text(Cents(net, invoice.Total.Currency));
                    });

                    column.Item().AlignRight().Text($"Base imponible: {Cents(net, invoice.Total.Currency)}");
                    column.Item().AlignRight().Text(
                        $"IVA: {Cents(invoice.Tax.AmountInCents, invoice.Total.Currency)}");
                    column.Item().AlignRight()
                        .Text($"Total: {Cents(invoice.Total.AmountInCents, invoice.Total.Currency)}")
                        .FontSize(13).Bold();

                    // La huella al pie, en letra pequeña. El QR ya va arriba, que es donde la
                    // norma lo quiere; esto es la trazabilidad para quien la busque.
                    column.Item().PaddingTop(24).Column(foot =>
                    {
                        foot.Item().Text("Registro de facturación (RD 1007/2023)").FontSize(8).SemiBold();
                        foot.Item().Text($"Huella: {invoice.Hash}").FontSize(7);

                        if (invoice.PreviousHash != Domain.Billing.VerifactuRecord.GenesisHash)
                        {
                            foot.Item().Text($"Encadenada con: {invoice.PreviousHash}").FontSize(7);
                        }

                        if (!invoice.DeclaredToAeat)
                        {
                            // Se dice cuando NO está remitida. La leyenda de «verificable» va
                            // arriba, bajo el QR, y solo la imprime un sistema que sí remite.
                            foot.Item().PaddingTop(4)
                                .Text("Registro generado y encadenado, pendiente de remisión a la AEAT.")
                                .FontSize(7);
                        }
                    });
                });
            });
        }).GeneratePdf();
    }

    /// <summary>
    /// La URL de cotejo del QR. La compone el dominio a partir de las especificaciones de la
    /// AEAT; aquí solo se le dice contra qué entorno y si el sistema remite o no.
    /// </summary>
    private string BuildQrUrl(InvoiceDocument invoice) =>
        VerifactuQr.Url(
            options.Environment,
            options.Verifiable,
            invoice.Issuer.TaxId,
            invoice.FullNumber,
            invoice.IssueDate,
            invoice.Total.AmountInCents);

    private static string Cents(long cents, string currency) =>
        $"{(cents / 100m).ToString("0.00", CultureInfo.InvariantCulture)} {currency}";
}
