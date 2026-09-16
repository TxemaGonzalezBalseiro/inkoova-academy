using FluentAssertions;
using Inkoova.Academy.Application.Billing;

namespace Inkoova.Academy.Domain.Tests;

/// <summary>
/// El desglose de IVA que pide el registro (<c>Desglose</c>, obligatorio en el alta).
///
/// Se deduce del total y la cuota, que es lo único que se guarda hoy. Estos tests fijan hasta
/// dónde vale esa deducción, porque el día que una factura mezcle tipos dejará de valer y más
/// vale que esté escrito dónde está el límite.
/// </summary>
public class DesgloseTests
{
    [Fact]
    public void A_normal_sale_breaks_down_into_base_and_the_rate_that_produced_it()
    {
        // 121,00 € con 21,00 € de cuota: base 100,00 al 21 %.
        var lines = VerifactuService.Breakdown(totalCents: 12100, taxCents: 2100);

        lines.Should().ContainSingle();
        lines[0].BaseCents.Should().Be(10000);
        lines[0].TaxCents.Should().Be(2100);
        lines[0].Rate.Should().Be(21m);
    }

    [Fact]
    public void The_rate_keeps_two_decimals_because_that_is_how_it_is_declared()
    {
        // Un importe que no da un tipo redondo: se declara lo que sale, no un 21 forzado.
        var lines = VerifactuService.Breakdown(totalCents: 9900, taxCents: 1718);

        lines[0].BaseCents.Should().Be(8182);
        lines[0].Rate.Should().Be(21m);
    }

    [Fact]
    public void An_exempt_invoice_declares_a_zero_rate_and_not_a_guess()
    {
        var lines = VerifactuService.Breakdown(totalCents: 9900, taxCents: 0);

        lines[0].Rate.Should().Be(0m);
        lines[0].BaseCents.Should().Be(9900);
    }

    [Fact]
    public void An_invoice_with_no_base_does_not_try_to_deduce_a_rate()
    {
        // Sin base no hay tipo que deducir, y dividir por cero sería el fallo. Pasa en una
        // factura a importe cero, que existe: una rectificativa total.
        var lines = VerifactuService.Breakdown(totalCents: 0, taxCents: 0);

        lines.Should().ContainSingle();
        lines[0].Rate.Should().Be(0m);
        lines[0].BaseCents.Should().Be(0);
    }

    [Fact]
    public void The_breakdown_always_adds_up_to_the_invoice_total()
    {
        foreach (var (total, tax) in new[] { (12100L, 2100L), (9900L, 1718L), (4900L, 850L) })
        {
            var lines = VerifactuService.Breakdown(total, tax);

            // Si base y cuota no suman el total, la AEAT rechaza el registro por descuadre.
            lines.Sum(l => l.BaseCents + l.TaxCents).Should().Be(total);
        }
    }
}
