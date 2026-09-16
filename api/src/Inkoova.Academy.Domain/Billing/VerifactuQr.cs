using System.Globalization;

namespace Inkoova.Academy.Domain.Billing;

/// <summary>Contra qué entorno de la AEAT se trabaja. Cambia el endpoint y la URL del QR a la vez.</summary>
public enum AeatEnvironment
{
    /// <summary>Portal de Pruebas Externas. Lo que valida aquí no cuenta como declarado.</summary>
    Pruebas,

    Produccion
}

/// <summary>
/// La URL de cotejo que va dentro del código QR de la factura.
///
/// Fuente: «Detalle de las especificaciones técnicas del código QR de la factura», AEAT,
/// versión 0.5.0, apartados 4 a 6.
///
/// Lo que fija la norma y no es negociable:
///
/// · Cuatro parámetros, ni uno más: <c>nif</c>, <c>numserie</c>, <c>fecha</c> e <c>importe</c>.
/// · Los valores van codificados para URL en UTF-8. El ejemplo del documento lleva un número de
///   serie con «&amp;» dentro, que sin codificar partiría la URL en dos parámetros.
/// · La fecha en <c>DD-MM-AAAA</c> y el importe con punto decimal.
/// · La URL base depende del entorno Y de si el sistema emite facturas verificables. Son cuatro
///   direcciones distintas y usar la que no es manda al cliente a una página que no encuentra
///   su factura.
/// </summary>
public static class VerifactuQr
{
    /// <summary>El texto que la norma exige encima del código. Apartado 3.</summary>
    public const string Heading = "QR tributario:";

    /// <summary>
    /// La frase que va justo debajo del código en los sistemas que emiten facturas verificables.
    /// La norma admite esta o «VERI*FACTU». Apartado 3.
    /// </summary>
    public const string VerifiableNotice = "Factura verificable en la sede electrónica de la AEAT";

    /// <summary>
    /// La URL de cotejo de una factura.
    /// </summary>
    /// <param name="verifiable">
    /// Si el sistema emite facturas verificables, es decir, si remite los registros a la AEAT.
    /// Con <c>false</c> apunta al servicio de facturas no verificables, que es otro.
    /// </param>
    public static string Url(
        AeatEnvironment environment,
        bool verifiable,
        string issuerTaxId,
        string seriesNumber,
        DateOnly issueDate,
        long totalCents)
    {
        var host = environment == AeatEnvironment.Produccion
            ? "https://www2.agenciatributaria.gob.es"
            : "https://prewww2.aeat.es";

        var service = verifiable ? "ValidarQR" : "ValidarQRNoVerifactu";

        // Cada valor codificado por separado. Codificar la cadena entera escaparía también los
        // «&» y los «=» que separan los parámetros, y la AEAT recibiría un solo parámetro.
        var query = string.Join('&',
            $"nif={Uri.EscapeDataString(issuerTaxId.Trim())}",
            $"numserie={Uri.EscapeDataString(seriesNumber.Trim())}",
            $"fecha={Uri.EscapeDataString(issueDate.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture))}",
            $"importe={Uri.EscapeDataString(Amount(totalCents))}");

        return $"{host}/wlpl/TIKE-CONT/{service}?{query}";
    }

    /// <summary>
    /// El importe con punto decimal y dos cifras. La coma que usaría una cultura española
    /// haría que la AEAT devolviera error de formato.
    /// </summary>
    private static string Amount(long cents) =>
        (cents / 100m).ToString("0.00", CultureInfo.InvariantCulture);
}
