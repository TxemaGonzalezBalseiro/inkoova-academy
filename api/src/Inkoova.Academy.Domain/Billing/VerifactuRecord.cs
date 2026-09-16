using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Inkoova.Academy.Domain.Common;

namespace Inkoova.Academy.Domain.Billing;

/// <summary>Alta al emitir, anulación al anular. Una anulación es un asiento nuevo, no un borrado.</summary>
public enum VerifactuKind
{
    Alta,
    Anulacion
}

/// <summary>Dónde está el envío a la AEAT de un registro.</summary>
public enum SubmissionState
{
    /// <summary>Generado y sin remitir. Es lo que reintenta el job.</summary>
    Pending,

    /// <summary>Aceptado por la AEAT.</summary>
    Sent,

    /// <summary>Rechazado. El motivo dice qué hay que corregir.</summary>
    Rejected,

    /// <summary>
    /// No hay que remitirlo: el envío está apagado. Distinto de <see cref="Pending"/> a
    /// propósito: «no enviado» es un problema y «no hacía falta» no lo es, y confundirlos deja
    /// una lista de pendientes que nadie mira porque siempre tiene cosas.
    /// </summary>
    NotRequired
}

/// <summary>
/// Un registro de facturación de Veri*Factu (RD 1007/2023).
///
/// Lo que se declara no es el PDF: es este asiento, encadenado con el anterior por su huella y
/// remitido a la AEAT. Por eso todo lo que lleva está COPIADO —el NIF del emisor, su razón
/// social, el importe— y no se lee de ningún sitio al consultarlo: lo declarado es lo que se
/// declaró, y cambiar mañana el domicilio fiscal no puede reescribir lo de ayer.
///
/// Un registro NO SE MODIFICA nunca después de crearse, salvo el estado de su envío. Cambiar
/// cualquier otra cosa invalidaría la huella y, con ella, toda la cadena posterior.
/// </summary>
public sealed class VerifactuRecord
{
    /// <summary>El primero de una cadena encadena con esto.</summary>
    public const string GenesisHash = "0000000000000000000000000000000000000000000000000000000000000000";

    public const string Algorithm = "SHA-256";

    public Guid Id { get; private set; }
    public Guid InvoiceId { get; private set; }
    public VerifactuKind Kind { get; private set; }

    public string IssuerTaxId { get; private set; }
    public string IssuerName { get; private set; }

    /// <summary>
    /// Tipo de factura según la norma: F1 con destinatario identificado, F2 simplificada,
    /// F3 en sustitución de simplificadas, R1..R5 rectificativas.
    ///
    /// Qué clave corresponde a cada venta NO se decide aquí ni en ningún otro punto del código:
    /// es una decisión fiscal que cambia con el negocio —empezar a pedir el NIF al cliente
    /// mueve esa venta de F2 a F1— y se configura por marca en <c>LegalDetails</c>. Los valores
    /// admitidos son los de <c>ClaveTipoFacturaType</c> del esquema oficial, y los valida el
    /// dominio antes de guardar: la AEAT rechaza el registro entero si la clave no está en su
    /// enumeración, y ese rechazo llega con el reintento diario, no al facturar.
    /// </summary>
    public string InvoiceType { get; private set; }

    public string SeriesNumber { get; private set; }
    public DateOnly IssueDate { get; private set; }
    public string Description { get; private set; }
    public long TotalCents { get; private set; }
    public long TaxCents { get; private set; }
    public string Currency { get; private set; }
    public string? Rectifies { get; private set; }

    public string PreviousHash { get; private set; }

    /// <summary>
    /// La huella del registro, calculada según las especificaciones de la AEAT. Es a la vez el
    /// dato fiscal que viaja en el registro y el sello que permite comprobar aquí que la fila no
    /// se ha tocado desde que se emitió.
    /// </summary>
    public string Hash { get; private set; }

    /// <summary>
    /// El texto exacto sobre el que se calculó la huella. Ocupa poco y es lo único que permite
    /// explicar una huella años después sin reconstruir el código de entonces.
    /// </summary>
    public string HashInput { get; private set; }

    /// <summary>
    /// Cuándo se generó el asiento, con huso. No es la fecha de expedición: una factura del
    /// día 30 puede generar su registro el 31, y la norma pide las dos.
    /// </summary>
    public DateTimeOffset GeneratedAt { get; private set; }

    public SubmissionState State { get; private set; }
    public string? AeatSubmissionId { get; private set; }
    public DateTimeOffset? SubmittedAt { get; private set; }
    public string SubmissionError { get; private set; }
    public int SubmissionAttempts { get; private set; }

    private VerifactuRecord(
        Guid id,
        Guid invoiceId,
        VerifactuKind kind,
        string issuerTaxId,
        string issuerName,
        string invoiceType,
        string seriesNumber,
        DateOnly issueDate,
        string description,
        long totalCents,
        long taxCents,
        string currency,
        string? rectifies,
        string previousHash,
        string hash,
        string hashInput,
        DateTimeOffset generatedAt,
        SubmissionState state,
        string? aeatSubmissionId,
        DateTimeOffset? submittedAt,
        string submissionError,
        int submissionAttempts)
    {
        Id = id;
        InvoiceId = invoiceId;
        Kind = kind;
        IssuerTaxId = issuerTaxId;
        IssuerName = issuerName;
        InvoiceType = invoiceType;
        SeriesNumber = seriesNumber;
        IssueDate = issueDate;
        Description = description;
        TotalCents = totalCents;
        TaxCents = taxCents;
        Currency = currency;
        Rectifies = rectifies;
        PreviousHash = previousHash;
        Hash = hash;
        HashInput = hashInput;
        GeneratedAt = generatedAt;
        State = state;
        AeatSubmissionId = aeatSubmissionId;
        SubmittedAt = submittedAt;
        SubmissionError = submissionError;
        SubmissionAttempts = submissionAttempts;
    }

    /// <summary>
    /// Crea el asiento y calcula su huella a partir del anterior de la cadena.
    /// </summary>
    /// <param name="submissionRequired">
    /// Si hay que remitirlo. Cuando el envío está apagado nace como <c>NotRequired</c> y no
    /// como pendiente: así la lista de pendientes solo contiene lo que de verdad falta.
    /// </param>
    public static Result<VerifactuRecord, Error> Create(
        Guid id,
        Guid invoiceId,
        VerifactuKind kind,
        string issuerTaxId,
        string issuerName,
        string invoiceType,
        string seriesNumber,
        DateOnly issueDate,
        string description,
        long totalCents,
        long taxCents,
        string currency,
        string? rectifies,
        string previousHash,
        DateTimeOffset generatedAt,
        bool submissionRequired)
    {
        if (string.IsNullOrWhiteSpace(issuerTaxId))
        {
            // Sin NIF del emisor no hay registro que valga: es lo primero que identifica quién
            // declara. Es preferible no emitir a emitir algo que la AEAT rechazará.
            return Error.Validation(
                "verifactu.issuer_tax_id_missing",
                "Falta el NIF del emisor. Complétalo en los datos legales de la marca.");
        }

        if (string.IsNullOrWhiteSpace(seriesNumber))
        {
            return Error.Validation("verifactu.number_missing", "El registro necesita serie y número.");
        }

        if (previousHash.Length != 64)
        {
            return Error.Validation("verifactu.previous_hash_invalid", "La huella anterior no es válida.");
        }

        var utc = generatedAt.ToUniversalTime();

        var input = BuildHashInput(
            kind, issuerTaxId, seriesNumber, issueDate, invoiceType,
            taxCents, totalCents, previousHash, utc);

        return new VerifactuRecord(
            id, invoiceId, kind, issuerTaxId.Trim(), issuerName.Trim(), invoiceType,
            seriesNumber, issueDate, (description ?? string.Empty).Trim(), totalCents, taxCents,
            currency, rectifies, previousHash, Hash256(input), input, utc,
            submissionRequired ? SubmissionState.Pending : SubmissionState.NotRequired,
            aeatSubmissionId: null, submittedAt: null, string.Empty, submissionAttempts: 0);
    }

    public static VerifactuRecord Rehydrate(
        Guid id,
        Guid invoiceId,
        VerifactuKind kind,
        string issuerTaxId,
        string issuerName,
        string invoiceType,
        string seriesNumber,
        DateOnly issueDate,
        string description,
        long totalCents,
        long taxCents,
        string currency,
        string? rectifies,
        string previousHash,
        string hash,
        string hashInput,
        DateTimeOffset generatedAt,
        SubmissionState state,
        string? aeatSubmissionId,
        DateTimeOffset? submittedAt,
        string submissionError,
        int submissionAttempts) =>
        new(id, invoiceId, kind, issuerTaxId, issuerName, invoiceType, seriesNumber, issueDate,
            description, totalCents, taxCents, currency, rectifies, previousHash, hash, hashInput,
            generatedAt, state, aeatSubmissionId, submittedAt, submissionError, submissionAttempts);

    /// <summary>
    /// El texto sobre el que se calcula la huella, según las
    /// «Especificaciones técnicas para generación de la huella o hash de los registros de
    /// facturación» de la AEAT (v0.1.2, 27/08/2024), apartado 3.
    ///
    /// Reglas que trae el documento y que es fácil incumplir sin enterarse:
    ///
    /// · Los campos van EN ESTE ORDEN, que es el de su aparición en el diseño de registro, y
    ///   con el nombre exacto del XML. Concatenados como
    ///   <c>nombre1=valor1&amp;nombre2=valor2&amp;…</c>.
    /// · Los valores son los del XML sin espacios al principio ni al final.
    /// · El registro de ALTA y el de ANULACIÓN llevan campos DISTINTOS. La anulación no lleva
    ///   ni tipo ni importes, y sus campos se llaman «…Anulada».
    /// · En el primer registro del sistema, <c>Huella=</c> va VACÍO. No lleva ceros ni ningún
    ///   relleno: el nombre del campo, el igual, y nada detrás.
    /// · La cadena se codifica en UTF-8 antes de aplicar SHA-256.
    ///
    /// Los ejemplos oficiales del documento están fijados como test, así que cualquier cambio
    /// aquí que se desvíe de la norma rompe la compilación en vez de rechazarse en la AEAT
    /// meses después.
    /// </summary>
    /// <param name="previousHash">
    /// La huella del registro anterior, o <see cref="GenesisHash"/> si es el primero. Los ceros
    /// son NUESTRA marca interna de «no hay anterior»; en la cadena que se firma se traducen al
    /// vacío que pide la norma.
    /// </param>
    public static string BuildHashInput(
        VerifactuKind kind,
        string issuerTaxId,
        string seriesNumber,
        DateOnly issueDate,
        string invoiceType,
        long taxCents,
        long totalCents,
        string previousHash,
        DateTimeOffset generatedAt)
    {
        var chained = previousHash == GenesisHash ? string.Empty : previousHash;

        // Anulación: apartado 3.b. Cinco campos y con otros nombres. Usar los del alta aquí
        // daría una huella que la AEAT marca como «Aceptado con errores».
        if (kind == VerifactuKind.Anulacion)
        {
            return string.Join('&',
                $"IDEmisorFacturaAnulada={issuerTaxId.Trim()}",
                $"NumSerieFacturaAnulada={seriesNumber.Trim()}",
                $"FechaExpedicionFacturaAnulada={Date(issueDate)}",
                $"Huella={chained}",
                $"FechaHoraHusoGenRegistro={Moment(generatedAt)}");
        }

        // Alta: apartado 3.a. Ocho campos.
        return string.Join('&',
            $"IDEmisorFactura={issuerTaxId.Trim()}",
            $"NumSerieFactura={seriesNumber.Trim()}",
            $"FechaExpedicionFactura={Date(issueDate)}",
            $"TipoFactura={invoiceType.Trim()}",
            $"CuotaTotal={Amount(taxCents)}",
            $"ImporteTotal={Amount(totalCents)}",
            $"Huella={chained}",
            $"FechaHoraHusoGenRegistro={Moment(generatedAt)}");
    }

    /// <summary>Fecha con guiones, <c>DD-MM-AAAA</c>, como en los ejemplos de la AEAT.</summary>
    private static string Date(DateOnly date) =>
        date.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture);

    /// <summary>
    /// La marca temporal en ISO-8601 con huso explícito, tal y como la escriben los ejemplos
    /// del documento (<c>2024-01-01T19:20:30+01:00</c>).
    ///
    /// NO se normaliza a UTC aquí, y es deliberado: la huella se calcula sobre la cadena
    /// LITERAL, así que el mismo instante escrito con distinto huso da huellas distintas. La
    /// AEAT recalcula sobre lo que recibe, de modo que lo que se firma tiene que ser
    /// exactamente lo que viaje en el XML, carácter a carácter.
    ///
    /// Quién decide el huso es <see cref="Create"/>, que pasa el instante ya en UTC. Que esta
    /// función no lo imponga es lo que permite reproducir los ejemplos oficiales de la AEAT
    /// —escritos en <c>+01:00</c>— y comprobar así que el cálculo es el suyo.
    /// </summary>
    private static string Moment(DateTimeOffset moment) =>
        moment.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture);

    /// <summary>
    /// Vuelve a calcular la huella sobre el texto GUARDADO y la compara con la guardada.
    ///
    /// Sobre el texto guardado y no sobre los campos: si un día cambia el formato de la huella,
    /// recalcularlo desde los campos daría por rota toda la cadena anterior, que está bien.
    /// </summary>
    public bool HashIsIntact() => Hash256(HashInput) == Hash;

    /// <summary>Si encadena con el registro que dice ser el anterior.</summary>
    public bool ChainsFrom(VerifactuRecord? previous) =>
        PreviousHash == (previous?.Hash ?? GenesisHash);

    /// <summary>
    /// Lo aceptó la AEAT.
    /// </summary>
    /// <param name="submissionId">
    /// El CSV —código seguro de verificación— que devuelve la AEAT al aceptar el registro. Es
    /// lo que permite volver a preguntar por él, así que sin CSV no se da por enviado.
    /// </param>
    public Result<Unit, Error> MarkSent(string submissionId, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(submissionId))
        {
            return Error.Validation(
                "verifactu.submission_id_missing",
                "Un envío aceptado tiene que traer su identificador.");
        }

        State = SubmissionState.Sent;
        AeatSubmissionId = submissionId.Trim();
        SubmittedAt = now;
        SubmissionError = string.Empty;
        SubmissionAttempts++;

        return Unit.Value;
    }

    /// <summary>
    /// Si la factura se puede imprimir con la leyenda de verificable.
    ///
    /// Solo cuando la AEAT la ha aceptado: imprimir «verificable en la sede electrónica» en una
    /// factura que Hacienda no tiene manda al cliente a comprobar algo que no va a encontrar.
    /// </summary>
    public bool IsDeclared => State == SubmissionState.Sent;

    /// <summary>
    /// Lo rechazó la AEAT. Se guarda el motivo y se cuenta el intento: un registro rechazado
    /// hay que corregirlo y reenviarlo, y sin el motivo nadie sabe qué corregir.
    /// </summary>
    public void MarkRejected(string reason, DateTimeOffset now)
    {
        State = SubmissionState.Rejected;
        SubmissionError = (reason ?? string.Empty).Trim();
        SubmittedAt = now;
        SubmissionAttempts++;
    }

    private static string Hash256(string input) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input)));

    /// <summary>Importe con dos decimales y punto, en cultura invariante. La coma variaría la huella.</summary>
    private static string Amount(long cents) =>
        (cents / 100m).ToString("0.00", CultureInfo.InvariantCulture);
}
