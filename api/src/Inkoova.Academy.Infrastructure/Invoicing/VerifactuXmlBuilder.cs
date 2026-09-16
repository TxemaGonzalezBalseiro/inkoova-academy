using System.Globalization;
using System.Xml.Linq;
using Inkoova.Academy.Application.Abstractions;
using Inkoova.Academy.Domain.Billing;

namespace Inkoova.Academy.Infrastructure.Invoicing;

/// <summary>
/// Compone el <c>RegFactuSistemaFacturacion</c> que se remite a la AEAT.
///
/// Se separa del transporte porque es la parte que se puede comprobar sin red ni certificado:
/// el XML que sale de aquí se valida contra los XSD oficiales en los tests, y esa es la única
/// forma de saber que está bien antes de que lo diga Hacienda.
///
/// **No firma.** En modo Veri*Factu la firma electrónica de cada registro no es exigible —lo
/// dice el apartado 2 del documento de firma, v0.1.5: solo aplica a los sistemas NO Veri*Factu,
/// que son los que conservan en vez de remitir—. Lo que autentica aquí es el certificado de
/// cliente de la conexión, que pone <see cref="AeatVerifactuSubmitter"/>.
///
/// Los nombres, el orden y los formatos salen de <c>SuministroInformacion.xsd</c> y
/// <c>SuministroLR.xsd</c>, en <c>docs/verifactu/esquemas/</c>. El orden de los elementos NO es
/// decorativo: los tipos son <c>sequence</c>, así que un campo fuera de sitio invalida el
/// documento entero aunque estén todos.
/// </summary>
public static class VerifactuXmlBuilder
{
    public static readonly XNamespace Sf =
        "https://www2.agenciatributaria.gob.es/static_files/common/internet/dep/aplicaciones/es/aeat/tike/cont/ws/SuministroInformacion.xsd";

    public static readonly XNamespace SfLr =
        "https://www2.agenciatributaria.gob.es/static_files/common/internet/dep/aplicaciones/es/aeat/tike/cont/ws/SuministroLR.xsd";

    /// <summary>Versión del esquema. Constante: la fija la AEAT, no la instalación.</summary>
    private const string SchemaVersion = "1.0";

    /// <summary>SHA-256. Es el único algoritmo que admite hoy el campo <c>TipoHuella</c>.</summary>
    private const string HashTypeSha256 = "01";

    /// <summary>
    /// El envío completo: una cabecera con el obligado y hasta 1.000 registros.
    ///
    /// Todos los registros de un envío son del MISMO obligado tributario, porque el NIF va en la
    /// cabecera y no en cada línea. El troceado por emisor lo hace quien llama.
    /// </summary>
    public static XDocument BuildSubmission(IReadOnlyList<VerifactuSubmission> batch)
    {
        ArgumentOutOfRangeException.ThrowIfZero(batch.Count);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(batch.Count, VerifactuLimits.MaxRecordsPerSubmission);

        var issuer = batch[0].Issuer;

        var distinct = batch.Select(s => s.Record.IssuerTaxId).Distinct(StringComparer.Ordinal).Count();
        if (distinct > 1)
        {
            // Se para aquí y no en la AEAT: el NIF va en la cabecera, así que un lote mezclado
            // declararía las facturas de un emisor a nombre de otro. Lo aceptaría el esquema.
            throw new InvalidOperationException(
                "Un envío no puede mezclar emisores: el NIF del obligado va en la cabecera.");
        }

        return new XDocument(
            new XElement(SfLr + "RegFactuSistemaFacturacion",
                new XAttribute(XNamespace.Xmlns + "sf", Sf.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "sfLR", SfLr.NamespaceName),
                new XElement(SfLr + "Cabecera",
                    new XElement(Sf + "ObligadoEmision",
                        new XElement(Sf + "NombreRazon", issuer.Name),
                        new XElement(Sf + "NIF", issuer.TaxId))),
                batch.Select(BuildRegistroFactura)));
    }

    private static XElement BuildRegistroFactura(VerifactuSubmission submission) =>
        new(SfLr + "RegistroFactura",
            submission.Record.Kind == VerifactuKind.Anulacion
                ? BuildRegistroAnulacion(submission)
                : BuildRegistroAlta(submission));

    private static XElement BuildRegistroAlta(VerifactuSubmission submission)
    {
        var record = submission.Record;

        var alta = new XElement(Sf + "RegistroAlta",
            new XElement(Sf + "IDVersion", SchemaVersion),
            InvoiceId(record.IssuerTaxId, record.SeriesNumber, record.IssueDate),
            new XElement(Sf + "NombreRazonEmisor", record.IssuerName),

            // Subsanacion es para reenviar un registro que ya se mandó mal, y RechazoPrevio para
            // decir que la AEAT ya lo rechazó una vez. Los dos a "N": aquí cada registro se
            // remite una vez, y un reintento del job es el MISMO envío, no una subsanación.
            new XElement(Sf + "Subsanacion", "N"),
            new XElement(Sf + "RechazoPrevio", "N"),
            new XElement(Sf + "TipoFactura", record.InvoiceType));

        if (record.Rectifies is { Length: > 0 } rectificada)
        {
            // "I" (incremental) y no "S": lo que se declara es la diferencia del reembolso, no
            // una factura que sustituye a la original. La original sigue siendo válida por lo
            // que se cobró; lo que cambia es que después se devolvió parte o todo.
            alta.Add(new XElement(Sf + "TipoRectificativa", "I"));
            alta.Add(new XElement(Sf + "FacturasRectificadas",
                InvoiceId(record.IssuerTaxId, rectificada, record.IssueDate, "IDFacturaRectificada")));
        }

        alta.Add(new XElement(Sf + "DescripcionOperacion", Truncate(record.Description, 500)));

        // Destinatarios solo cuando hay alguien identificado. En una factura simplificada (F2)
        // no lo hay, y mandar el bloque vacío no es lo mismo que no mandarlo.
        if (!string.IsNullOrWhiteSpace(submission.CustomerTaxId)
            && !string.IsNullOrWhiteSpace(submission.CustomerName))
        {
            alta.Add(new XElement(Sf + "Destinatarios",
                new XElement(Sf + "IDDestinatario",
                    new XElement(Sf + "NombreRazon", Truncate(submission.CustomerName!, 120)),
                    Recipient(submission))));
        }

        alta.Add(new XElement(Sf + "Desglose",
            submission.Breakdown.Select(line =>
                new XElement(Sf + "DetalleDesglose",
                    // 01: régimen general. Es el que corresponde a la venta de formación en
                    // línea a un cliente al que se le repercute IVA español.
                    new XElement(Sf + "ClaveRegimen", "01"),
                    new XElement(Sf + "CalificacionOperacion", "S1"),
                    new XElement(Sf + "TipoImpositivo", Amount(line.Rate)),
                    new XElement(Sf + "BaseImponibleOimporteNoSujeto", Cents(line.BaseCents)),
                    new XElement(Sf + "CuotaRepercutida", Cents(line.TaxCents))))));

        alta.Add(new XElement(Sf + "CuotaTotal", Cents(record.TaxCents)));
        alta.Add(new XElement(Sf + "ImporteTotal", Cents(record.TotalCents)));
        alta.Add(Chaining(submission));
        alta.Add(Software(submission.Software));
        alta.Add(new XElement(Sf + "FechaHoraHusoGenRegistro", Moment(record.GeneratedAt)));
        alta.Add(new XElement(Sf + "TipoHuella", HashTypeSha256));
        alta.Add(new XElement(Sf + "Huella", record.Hash));

        return alta;
    }

    private static XElement BuildRegistroAnulacion(VerifactuSubmission submission)
    {
        var record = submission.Record;

        return new XElement(Sf + "RegistroAnulacion",
            new XElement(Sf + "IDVersion", SchemaVersion),
            new XElement(Sf + "IDFactura",
                new XElement(Sf + "IDEmisorFacturaAnulada", record.IssuerTaxId),
                new XElement(Sf + "NumSerieFacturaAnulada", record.SeriesNumber),
                new XElement(Sf + "FechaExpedicionFacturaAnulada", Date(record.IssueDate))),
            new XElement(Sf + "SinRegistroPrevio", "N"),
            new XElement(Sf + "RechazoPrevio", "N"),
            Chaining(submission),
            Software(submission.Software),
            new XElement(Sf + "FechaHoraHusoGenRegistro", Moment(record.GeneratedAt)),
            new XElement(Sf + "TipoHuella", HashTypeSha256),
            new XElement(Sf + "Huella", record.Hash));
    }

    /// <summary>
    /// El encadenamiento. El primer registro del emisor lleva <c>PrimerRegistro</c>; el resto,
    /// la identificación del anterior con su huella.
    ///
    /// La marca interna de génesis —64 ceros— NO viaja: se traduce a «este es el primero», igual
    /// que en el cálculo de la huella se traduce a cadena vacía.
    /// </summary>
    private static XElement Chaining(VerifactuSubmission submission)
    {
        var record = submission.Record;

        if (record.PreviousHash == VerifactuRecord.GenesisHash)
        {
            return new XElement(Sf + "Encadenamiento", new XElement(Sf + "PrimerRegistro", "S"));
        }

        if (submission.PreviousSeriesNumber is null || submission.PreviousIssueDate is null)
        {
            // Se para aquí. Con el bloque a medias el registro lo rechaza la AEAT, y con él
            // relleno a ojo se declararía un encadenamiento que apunta a una factura que no
            // existe: lo segundo pasa la validación y rompe la cadena en su sistema.
            throw new InvalidOperationException(
                $"Falta identificar el registro anterior de {record.SeriesNumber}: el " +
                "encadenamiento pide su número de serie y su fecha, no solo su huella.");
        }

        return new XElement(Sf + "Encadenamiento",
            new XElement(Sf + "RegistroAnterior",
                new XElement(Sf + "IDEmisorFactura", record.IssuerTaxId),
                new XElement(Sf + "NumSerieFactura", submission.PreviousSeriesNumber),
                new XElement(Sf + "FechaExpedicionFactura", Date(submission.PreviousIssueDate.Value)),
                new XElement(Sf + "Huella", record.PreviousHash)));
    }

    private static XElement Software(VerifactuSoftware software) =>
        new(Sf + "SistemaInformatico",
            new XElement(Sf + "NombreRazon", software.DeveloperName),
            new XElement(Sf + "NIF", software.DeveloperTaxId),
            new XElement(Sf + "NombreSistemaInformatico", software.SystemName),
            new XElement(Sf + "IdSistemaInformatico", software.SystemId),
            new XElement(Sf + "Version", software.Version),
            new XElement(Sf + "NumeroInstalacion", software.InstallationNumber),
            new XElement(Sf + "TipoUsoPosibleSoloVerifactu", YesNo(software.OnlyVerifactu)),
            new XElement(Sf + "TipoUsoPosibleMultiOT", YesNo(software.MultiTaxpayerCapable)),
            new XElement(Sf + "IndicadorMultiplesOT", YesNo(software.MultiTaxpayerInUse)));

    /// <summary>
    /// El destinatario: NIF si es español, <c>IDOtro</c> con su país si no.
    ///
    /// Se decide por el país y no por la forma del identificador: un VAT alemán puede parecer un
    /// NIF válido, y declararlo como tal lo manda al censo equivocado.
    /// </summary>
    private static XElement Recipient(VerifactuSubmission submission) =>
        string.Equals(submission.CustomerCountry, "ES", StringComparison.OrdinalIgnoreCase)
            ? new XElement(Sf + "NIF", submission.CustomerTaxId!)
            : new XElement(Sf + "IDOtro",
                new XElement(Sf + "CodigoPais", submission.CustomerCountry.ToUpperInvariant()),
                // 02: NIF-IVA. Es lo que se tiene de un cliente intracomunitario.
                new XElement(Sf + "IDType", "02"),
                new XElement(Sf + "ID", submission.CustomerTaxId!));

    private static XElement InvoiceId(
        string issuerTaxId, string seriesNumber, DateOnly issueDate, string name = "IDFactura") =>
        new(Sf + name,
            new XElement(Sf + "IDEmisorFactura", issuerTaxId),
            new XElement(Sf + "NumSerieFactura", seriesNumber),
            new XElement(Sf + "FechaExpedicionFactura", Date(issueDate)));

    /// <summary>
    /// Fecha en <c>dd-MM-yyyy</c>, que es el formato del tipo <c>sf:fecha</c> del esquema.
    ///
    /// No es ISO. Escribirla como <c>yyyy-MM-dd</c> —que es lo que sale solo— la rechaza el
    /// validador, y es el error más fácil de cometer aquí porque el resto del código usa ISO.
    /// </summary>
    private static string Date(DateOnly date) => date.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture);

    /// <summary>
    /// El instante CON su huso, literal. Tiene que ser exactamente el mismo texto que se metió
    /// en la huella: el mismo momento escrito como <c>Z</c> y como <c>+00:00</c> da huellas
    /// distintas, y entonces la que viaja no cuadra con la que se declara.
    /// </summary>
    private static string Moment(DateTimeOffset moment) =>
        moment.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture);

    private static string Cents(long cents) =>
        (cents / 100m).ToString("0.00", CultureInfo.InvariantCulture);

    /// <summary>
    /// Importes sin ceros de más: el ejemplo oficial escribe <c>4</c> y <c>0.4</c>, no
    /// <c>4.00</c>. Se usa para el tipo impositivo, donde el esquema admite hasta dos decimales.
    /// </summary>
    private static string Amount(decimal value) =>
        value.ToString("0.##", CultureInfo.InvariantCulture);

    private static string YesNo(bool value) => value ? "S" : "N";

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];
}
