using System.Globalization;
using Inkoova.Academy.Application.Abstractions;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Inkoova.Academy.Infrastructure.Documents;

/// <summary>
/// Monthly affiliate statement. Like the certificate it is deterministic: the rows arrive
/// pre-sorted and nothing here reads the clock, so regenerating a period yields the same
/// bytes (T-16 acceptance criteria).
/// </summary>
public sealed class AffiliateStatementGenerator : IAffiliateStatementGenerator
{
    private const string Blue = "#1E3A8A";
    private const string Ink = "#0F172A";
    private const string Muted = "#64748B";

    public byte[] Generate(AffiliateStatementModel model) =>
        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(text => text.FontSize(10).FontFamily(Fonts.Calibri).FontColor(Ink));

                page.Header().Column(header =>
                {
                    header.Item().Text("Liquidación de afiliado").FontSize(18).FontColor(Blue).Bold();
                    header.Item().Text($"{model.AffiliateName} · {model.AffiliateCode}").FontColor(Muted);
                    header.Item().Text($"NIF/VAT: {model.TaxId}").FontColor(Muted);
                    header.Item().Text(
                        $"Periodo: {model.PeriodStart:dd/MM/yyyy} — {model.PeriodEnd:dd/MM/yyyy}").FontColor(Muted);
                });

                page.Content().PaddingVertical(16).Column(column =>
                {
                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(70);
                            columns.RelativeColumn(3);
                            columns.ConstantColumn(70);
                            columns.ConstantColumn(45);
                            columns.ConstantColumn(70);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(HeaderCell).Text("Fecha");
                            header.Cell().Element(HeaderCell).Text("Producto");
                            header.Cell().Element(HeaderCell).AlignRight().Text("Base neta");
                            header.Cell().Element(HeaderCell).AlignRight().Text("%");
                            header.Cell().Element(HeaderCell).AlignRight().Text("Comisión");
                        });

                        foreach (var (date, product, netBase, percent, amount) in model.Lines)
                        {
                            table.Cell().Element(BodyCell).Text($"{date:dd/MM/yyyy}");
                            table.Cell().Element(BodyCell).Text(product);
                            table.Cell().Element(BodyCell).AlignRight().Text(Format(netBase, model.Currency));
                            table.Cell().Element(BodyCell).AlignRight().Text($"{percent:0.##} %");
                            table.Cell().Element(BodyCell).AlignRight().Text(Format(amount, model.Currency));
                        }
                    });

                    if (model.CarriedOverIn > 0)
                    {
                        column.Item().PaddingTop(10).AlignRight()
                            .Text($"Arrastre de periodos anteriores: {Format(model.CarriedOverIn, model.Currency)}")
                            .FontColor(Muted);
                    }

                    column.Item().PaddingTop(12).AlignRight()
                        .Text($"Total: {Format(model.Total, model.Currency)}")
                        .FontSize(14).FontColor(Blue).Bold();

                    column.Item().PaddingTop(20).Text(
                            "Documento informativo de liquidación. No es una factura: la factura la emite el "
                            + "afiliado, o Inkoova mediante autofactura cuando así se haya acordado por escrito.")
                        .FontSize(8).FontColor(Muted);
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Página ").FontSize(8).FontColor(Muted);
                    text.CurrentPageNumber().FontSize(8).FontColor(Muted);
                    text.Span(" de ").FontSize(8).FontColor(Muted);
                    text.TotalPages().FontSize(8).FontColor(Muted);
                });
            });
        }).GeneratePdf();

    private static string Format(decimal amount, string currency) =>
        $"{amount.ToString("0.00", CultureInfo.InvariantCulture)} {currency}";

    private static IContainer HeaderCell(IContainer container) =>
        container.BorderBottom(1).BorderColor(Blue).PaddingVertical(4).DefaultTextStyle(t => t.SemiBold());

    private static IContainer BodyCell(IContainer container) =>
        container.BorderBottom(1).BorderColor("#E2E8F0").PaddingVertical(3);
}
