using FluentAssertions;
using Inkoova.Academy.Domain.Billing;

namespace Inkoova.Academy.Domain.Tests;

/// <summary>
/// La URL del QR, contra el ejemplo oficial de la AEAT.
///
/// Fuente: «Detalle de las especificaciones técnicas del código QR de la factura», AEAT,
/// versión 0.5.0, apartados 4 y 5.
/// </summary>
public class VerifactuQrTests
{
    [Fact]
    public void The_test_environment_url_matches_the_official_example()
    {
        // Apartado 4. El ejemplo del documento: un número de serie que lleva «&» dentro, que sin
        // codificar partiría la URL y la AEAT vería dos parámetros donde hay uno.
        var url = VerifactuQr.Url(
            AeatEnvironment.Pruebas, verifiable: true,
            "89890001K", "12345678&G33", new DateOnly(2024, 1, 1), 24140);

        url.Should().Be(
            "https://prewww2.aeat.es/wlpl/TIKE-CONT/ValidarQR" +
            "?nif=89890001K&numserie=12345678%26G33&fecha=01-01-2024&importe=241.40");
    }

    [Fact]
    public void Production_points_at_the_agency_and_not_at_the_test_portal()
    {
        var url = VerifactuQr.Url(
            AeatEnvironment.Produccion, verifiable: true,
            "89890001K", "INK-000001", new DateOnly(2026, 9, 1), 12100);

        // Publicar en producción con la URL de pruebas mandaría a cada cliente a un portal donde
        // su factura no existe.
        url.Should().StartWith("https://www2.agenciatributaria.gob.es/wlpl/TIKE-CONT/ValidarQR?");
        url.Should().NotContain("prewww");
    }

    [Fact]
    public void A_system_that_does_not_report_to_the_agency_uses_the_other_service()
    {
        var url = VerifactuQr.Url(
            AeatEnvironment.Produccion, verifiable: false,
            "89890001K", "INK-000001", new DateOnly(2026, 9, 1), 12100);

        url.Should().Contain("/ValidarQRNoVerifactu?");
    }

    [Fact]
    public void The_amount_always_carries_a_dot_and_two_decimals()
    {
        var url = VerifactuQr.Url(
            AeatEnvironment.Pruebas, verifiable: true,
            "89890001K", "INK-000001", new DateOnly(2026, 9, 1), 900);

        // Con coma decimal la AEAT devuelve error de formato de campo.
        url.Should().Contain("importe=9.00");
        url.Should().NotContain(",");
    }

    [Fact]
    public void The_legend_that_goes_with_the_code_is_the_one_the_rule_asks_for()
    {
        // Apartado 3: el texto va encima del código, y debajo la frase de verificable.
        VerifactuQr.Heading.Should().Be("QR tributario:");
        VerifactuQr.VerifiableNotice.Should().Contain("sede electrónica de la AEAT");
    }
}
