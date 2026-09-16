namespace Inkoova.Academy.Domain.Identities;

/// <summary>
/// Claves de tipo de factura de Veri*factu, tal y como las enumera el esquema oficial.
///
/// Salen de <c>ClaveTipoFacturaType</c> en <c>SuministroInformacion.xsd</c>
/// (ver <c>docs/verifactu/esquemas/</c>). No se amplía esta lista sin cambiar el esquema: la
/// AEAT rechaza el registro entero si el valor no está en su enumeración, y el rechazo llega
/// horas después por el reintento, no en el momento de facturar.
///
/// Qué clave corresponde a cada venta es una decisión fiscal, no técnica, y por eso se
/// configura por marca en <see cref="LegalDetails"/> en vez de estar escrita en el código.
/// </summary>
public static class InvoiceTypes
{
    /// <summary>Facturas ordinarias. F3 es la que sustituye a simplificadas ya declaradas.</summary>
    public static readonly IReadOnlyList<(string Code, string Description)> Ordinary =
    [
        ("F1", "Factura completa (art. 6, 7.2 y 7.3 del RD 1619/2012)"),
        ("F2", "Factura simplificada y facturas sin identificación del destinatario (art. 6.1.d)"),
        ("F3", "Factura emitida en sustitución de facturas simplificadas facturadas y declaradas")
    ];

    /// <summary>Rectificativas. R5 es la que rectifica una simplificada.</summary>
    public static readonly IReadOnlyList<(string Code, string Description)> Corrective =
    [
        ("R1", "Rectificativa (art. 80.1 y 80.2 y error fundado en derecho)"),
        ("R2", "Rectificativa (art. 80.3)"),
        ("R3", "Rectificativa (art. 80.4)"),
        ("R4", "Rectificativa (resto)"),
        ("R5", "Rectificativa en facturas simplificadas")
    ];

    /// <summary>
    /// Por defecto F2: es lo que corresponde a una venta a consumidor final del que no se ha
    /// pedido el NIF, que es cómo se vende hoy. Quien facture a empresa lo cambia a F1 desde
    /// el panel el día que empiece a pedirlo.
    /// </summary>
    public const string DefaultOrdinary = "F2";

    /// <summary>
    /// Por defecto R5, que es la coherente con emitir F2: una rectificativa de una factura
    /// simplificada tiene su propia clave. Emparejar F2 con R1 sería incongruente.
    /// </summary>
    public const string DefaultCorrective = "R5";

    public static bool IsOrdinary(string? code) =>
        Ordinary.Any(t => string.Equals(t.Code, code, StringComparison.Ordinal));

    public static bool IsCorrective(string? code) =>
        Corrective.Any(t => string.Equals(t.Code, code, StringComparison.Ordinal));
}
