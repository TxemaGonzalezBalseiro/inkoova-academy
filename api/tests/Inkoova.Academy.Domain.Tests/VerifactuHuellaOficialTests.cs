using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Inkoova.Academy.Domain.Billing;

namespace Inkoova.Academy.Domain.Tests;

/// <summary>
/// La huella, contra los ejemplos OFICIALES de la AEAT.
///
/// Fuente: «Detalle de las especificaciones técnicas para generación de la huella o hash de los
/// registros de facturación», AEAT, versión 0.1.2 de 27/08/2024, apartado 6.
///
/// Son los tres casos que publica el documento con su resultado esperado. Si estos tres pasan,
/// la huella que genera la plataforma es la que la AEAT recalcula al recibirla; si fallan, el
/// registro llegaría como «Aceptado con errores» y no nos enteraríamos hasta producción.
///
/// Por eso están clavados aquí y no en un comentario: es la única parte de todo esto que no se
/// puede comprobar razonando, solo contrastando.
/// </summary>
public class VerifactuHuellaOficialTests
{
    [Fact]
    public void Case_1_first_record_of_the_system()
    {
        // Apartado 6.1. Primer registro: el campo Huella va VACÍO, sin ceros ni relleno.
        var input = VerifactuRecord.BuildHashInput(
            VerifactuKind.Alta,
            issuerTaxId: "89890001K",
            seriesNumber: "12345678/G33",
            issueDate: new DateOnly(2024, 1, 1),
            invoiceType: "F1",
            taxCents: 1235,
            totalCents: 12345,
            previousHash: VerifactuRecord.GenesisHash,
            generatedAt: new DateTimeOffset(2024, 1, 1, 19, 20, 30, TimeSpan.FromHours(1)));

        input.Should().Be(
            "IDEmisorFactura=89890001K&NumSerieFactura=12345678/G33" +
            "&FechaExpedicionFactura=01-01-2024&TipoFactura=F1" +
            "&CuotaTotal=12.35&ImporteTotal=123.45" +
            "&Huella=&FechaHoraHusoGenRegistro=2024-01-01T19:20:30+01:00");

        Sha256(input).Should().Be(
            "3C464DAF61ACB827C65FDA19F352A4E3BDC2C640E9E9FC4CC058073F38F12F60");
    }

    [Fact]
    public void Case_2_a_record_chained_to_the_previous_one()
    {
        // Apartado 6.2. Segundo registro: la huella del anterior va entera en el campo Huella.
        var input = VerifactuRecord.BuildHashInput(
            VerifactuKind.Alta,
            issuerTaxId: "89890001K",
            seriesNumber: "12345679/G34",
            issueDate: new DateOnly(2024, 1, 1),
            invoiceType: "F1",
            taxCents: 1235,
            totalCents: 12345,
            previousHash: "3C464DAF61ACB827C65FDA19F352A4E3BDC2C640E9E9FC4CC058073F38F12F60",
            generatedAt: new DateTimeOffset(2024, 1, 1, 19, 20, 35, TimeSpan.FromHours(1)));

        Sha256(input).Should().Be(
            "F7B94CFD8924EDFF273501B01EE5153E4CE8F259766F88CF6ACB8935802A2B97");
    }

    [Fact]
    public void Case_3_an_annulment_uses_a_different_set_of_fields()
    {
        // Apartado 6.3. La anulación NO lleva tipo de factura ni importes, y sus campos se
        // llaman «…Anulada». Reutilizar los del alta aquí daría una huella distinta.
        var input = VerifactuRecord.BuildHashInput(
            VerifactuKind.Anulacion,
            issuerTaxId: "89890001K",
            seriesNumber: "12345679/G34",
            issueDate: new DateOnly(2024, 1, 1),
            invoiceType: "F1",
            taxCents: 1235,
            totalCents: 12345,
            previousHash: "F7B94CFD8924EDFF273501B01EE5153E4CE8F259766F88CF6ACB8935802A2B97",
            generatedAt: new DateTimeOffset(2024, 1, 1, 19, 20, 40, TimeSpan.FromHours(1)));

        input.Should().Be(
            "IDEmisorFacturaAnulada=89890001K&NumSerieFacturaAnulada=12345679/G34" +
            "&FechaExpedicionFacturaAnulada=01-01-2024" +
            "&Huella=F7B94CFD8924EDFF273501B01EE5153E4CE8F259766F88CF6ACB8935802A2B97" +
            "&FechaHoraHusoGenRegistro=2024-01-01T19:20:40+01:00");

        Sha256(input).Should().Be(
            "177547C0D57AC74748561D054A9CEC14B4C4EA23D1BEFD6F2E69E3A388F90C68");
    }

    [Fact]
    public void The_output_is_uppercase_hexadecimal_of_sixty_four_characters()
    {
        // Apartado 5: hexadecimal, en mayúsculas, 64 caracteres. En minúsculas no vale.
        var hash = Sha256("IDEmisorFactura=89890001K");

        hash.Should().HaveLength(64);
        hash.Should().MatchRegex("^[0-9A-F]{64}$");
    }

    [Fact]
    public void Leading_and_trailing_spaces_are_dropped_from_the_values()
    {
        // Apartado 3: los valores van «eliminando los espacios al inicio y al final». El ejemplo
        // del documento es justo este número de serie con espacios alrededor.
        var conEspacios = VerifactuRecord.BuildHashInput(
            VerifactuKind.Alta, "  89890001K ", "   12345678 / G33  ", new DateOnly(2024, 1, 1),
            " F1 ", 1235, 12345, VerifactuRecord.GenesisHash,
            new DateTimeOffset(2024, 1, 1, 19, 20, 30, TimeSpan.FromHours(1)));

        conEspacios.Should().Contain("NumSerieFactura=12345678 / G33&");
        conEspacios.Should().Contain("IDEmisorFactura=89890001K&");
        conEspacios.Should().Contain("TipoFactura=F1&");
    }

    /// <summary>
    /// La misma cuenta que hace <see cref="VerifactuRecord"/>: UTF-8, SHA-256 y hexadecimal en
    /// mayúsculas. Se repite aquí a propósito, para que el test compruebe el resultado y no la
    /// implementación llamándose a sí misma.
    /// </summary>
    private static string Sha256(string input) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input)));
}
