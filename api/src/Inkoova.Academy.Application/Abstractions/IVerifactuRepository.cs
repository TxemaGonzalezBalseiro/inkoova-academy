using Inkoova.Academy.Domain.Billing;

namespace Inkoova.Academy.Application.Abstractions;

public interface IVerifactuRepository
{
    /// <summary>
    /// El último asiento de la cadena de un emisor, que es con el que encadena el siguiente.
    ///
    /// Por emisor y no global: cada NIF tiene su propia cadena, y mezclarlas haría que la
    /// factura de una marca dependiera de la de otra.
    /// </summary>
    Task<VerifactuRecord?> GetLastAsync(string issuerTaxId, CancellationToken ct);

    /// <summary>La cadena entera de un emisor, en orden de generación. Es lo que se verifica.</summary>
    Task<IReadOnlyList<VerifactuRecord>> GetChainAsync(string issuerTaxId, CancellationToken ct);

    /// <summary>Los NIF que tienen cadena. Una lista corta: hay tantas como marcas que facturan.</summary>
    Task<IReadOnlyList<string>> GetIssuersAsync(CancellationToken ct);

    Task<VerifactuRecord?> GetByInvoiceAsync(Guid invoiceId, VerifactuKind kind, CancellationToken ct);

    /// <summary>
    /// El asiento cuya huella es la dada. Hace falta para componer el <c>Encadenamiento</c> del
    /// XML: la AEAT no pide solo la huella del anterior, sino su identificación completa —NIF,
    /// número de serie y fecha de expedición—, y eso no está en el registro que encadena.
    /// </summary>
    Task<VerifactuRecord?> GetByHashAsync(string issuerTaxId, string hash, CancellationToken ct);

    /// <summary>
    /// Lo que falta por remitir o fue rechazado, de un emisor, en orden de encadenamiento.
    ///
    /// Por emisor porque el control de flujo es por emisor, y en orden porque el lote se remite
    /// tal cual: mandar un registro antes que el que lo precede en la cadena es pedirle a la
    /// AEAT que valide un encadenamiento que todavía no le consta.
    /// </summary>
    Task<IReadOnlyList<VerifactuRecord>> GetUnsubmittedAsync(
        string issuerTaxId, int limit, CancellationToken ct);

    /// <summary>Cuántos quedan sin remitir, para enseñarlo sin traérselos todos.</summary>
    Task<int> CountUnsubmittedAsync(CancellationToken ct);

    Task<VerifactuFlow?> GetFlowAsync(string issuerTaxId, CancellationToken ct);

    Task SaveFlowAsync(VerifactuFlow flow, DateTimeOffset sentAt, CancellationToken ct);

    Task InsertAsync(VerifactuRecord record, CancellationToken ct);

    /// <summary>
    /// Actualiza SOLO el estado del envío. Lo demás no se toca nunca: cambiarlo invalidaría la
    /// huella y con ella toda la cadena posterior.
    /// </summary>
    Task UpdateSubmissionAsync(VerifactuRecord record, CancellationToken ct);
}

/// <summary>
/// Quién emite una factura, a efectos fiscales. Sale de los datos legales de la marca.
/// </summary>
public sealed record InvoiceIssuer(
    Guid IdentityId,
    string Name,
    string TaxId,
    string Address,

    // Claves de tipo de factura de la marca: la ordinaria y la de rectificativa. Viajan con el
    // emisor porque se deciden en el mismo sitio y por la misma persona que su NIF.
    string InvoiceType = Domain.Identities.InvoiceTypes.DefaultOrdinary,
    string CorrectiveInvoiceType = Domain.Identities.InvoiceTypes.DefaultCorrective);

/// <summary>
/// Quién es el SISTEMA que factura, que el registro exige identificar aparte del emisor.
///
/// El bloque <c>SistemaInformatico</c> del XML es obligatorio y son siete campos que no salen
/// de ninguna factura: describen el programa. Van en configuración porque son constantes de
/// esta instalación, y la AEAT los cruza con la declaración responsable del sistema.
///
/// Los valores concretos —el identificador del sistema, el número de instalación y los tres
/// indicadores de uso— NO son verificables desde aquí porque todavía no existen: los fija la
/// declaración responsable del software cuando se presente, y la AEAT cruza el registro contra
/// ella. Inventarlos daría registros que se rechazan enteros, así que van vacíos por defecto y
/// sin ellos no se remite nada: el panel de Facturación lo dice mientras falten.
/// </summary>
public sealed record VerifactuSoftware(
    string DeveloperName,
    string DeveloperTaxId,
    string SystemName,
    string SystemId,
    string Version,
    string InstallationNumber,
    bool OnlyVerifactu,
    bool MultiTaxpayerCapable,
    bool MultiTaxpayerInUse)
{
    /// <summary>Si está relleno lo mínimo para poder componer el bloque.</summary>
    public bool IsComplete =>
        !string.IsNullOrWhiteSpace(DeveloperTaxId)
        && !string.IsNullOrWhiteSpace(SystemName)
        && !string.IsNullOrWhiteSpace(SystemId)
        && !string.IsNullOrWhiteSpace(Version)
        && !string.IsNullOrWhiteSpace(InstallationNumber);
}

/// <summary>
/// Un desglose de IVA: una línea por tipo impositivo.
///
/// El registro pide el desglose, no solo el total. Hoy la academia vende a un único tipo, así
/// que sale una línea; el día que haya productos a tipos distintos en la misma factura, esto
/// tiene que venir de las líneas reales y no calculado a partir del total.
/// </summary>
public sealed record TaxBreakdownLine(decimal Rate, long BaseCents, long TaxCents);

/// <summary>
/// Todo lo que hace falta para componer el registro que se remite.
///
/// El <see cref="VerifactuRecord"/> no basta por sí solo: guarda lo que se declara y su huella,
/// pero el XML pide además quién es el sistema informático, el desglose de IVA y —en las
/// facturas con destinatario identificado— sus datos. Se pasa junto para que quien implemente
/// el envío no tenga que ir a buscarlo a cuatro sitios.
/// </summary>
public sealed record VerifactuSubmission(
    VerifactuRecord Record,
    InvoiceIssuer Issuer,
    VerifactuSoftware Software,
    IReadOnlyList<TaxBreakdownLine> Breakdown,
    string? CustomerName,
    string? CustomerTaxId,
    string CustomerCountry,

    /// <summary>
    /// Identificación del asiento anterior de la cadena. Nulos en el primero de un emisor, que
    /// viaja como <c>PrimerRegistro</c>.
    ///
    /// El <c>Encadenamiento</c> del XML no se conforma con la huella del anterior: pide también
    /// su número de serie y su fecha de expedición, y eso no está en el registro que encadena.
    /// </summary>
    string? PreviousSeriesNumber = null,
    DateOnly? PreviousIssueDate = null);

/// <summary>
/// El envío del registro a la AEAT.
///
/// Es un puerto y no una implementación porque lo que hay detrás —el XML del registro, la firma
/// con el certificado y la llamada al servicio— vive en la librería Veri*Factu que ya existe
/// fuera de este repositorio. Mantenerlo como puerto permite que todo lo demás —numeración,
/// encadenamiento, huella, QR, verificación, panel— esté hecho y probado sin ella.
///
/// Quien lo implemente NO tiene que calcular la huella ni encadenar: eso ya viene resuelto y
/// comprobado contra los ejemplos oficiales de la AEAT. Su trabajo es componer el XML con esos
/// datos, firmarlo y mandarlo al endpoint que corresponda —ojo, hay uno distinto para los
/// certificados de sello, ver <see cref="Domain.Billing.AeatEndpoints"/>—.
/// </summary>
public interface IVerifactuSubmitter
{
    /// <summary>Si hay algo detrás. Con <c>false</c>, los asientos nacen como «no hay que remitir».</summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Remite un LOTE de registros. En lote y no de uno en uno porque la AEAT impone un tiempo
    /// de espera entre envíos: mandar factura a factura incumpliría el control de flujo en
    /// cuanto entren dos ventas seguidas.
    /// </summary>
    /// <param name="batch">
    /// Como mucho <see cref="VerifactuLimits.MaxRecordsPerSubmission"/> registros, en el orden
    /// en que se encadenaron. Quien implemente esto NO tiene que trocearlo ni ordenarlo.
    /// </param>
    Task<VerifactuBatchResult> SubmitAsync(
        IReadOnlyList<VerifactuSubmission> batch, CancellationToken ct);
}

/// <summary>Los límites que impone la AEAT al envío. No son recomendaciones.</summary>
public static class VerifactuLimits
{
    /// <summary>Máximo de registros por envío, según la descripción de servicios web (v1.0.3).</summary>
    public const int MaxRecordsPerSubmission = 1000;

    /// <summary>
    /// Espera inicial entre envíos, en segundos. La AEAT devuelve el valor vigente en cada
    /// respuesta; este es solo el de partida mientras no haya contestado ninguna vez.
    /// </summary>
    public const int InitialWaitSeconds = 60;
}

/// <summary>
/// Lo que contesta la AEAT a un lote.
/// </summary>
/// <param name="Results">
/// Una respuesta por registro, en el mismo orden en que se mandaron. La AEAT puede aceptar unos
/// y rechazar otros dentro del mismo envío, así que un resultado único para todo el lote
/// perdería justo la información que hace falta para corregir.
/// </param>
/// <param name="WaitSeconds">
/// El <c>TiempoEsperaEnvio</c> que devuelve la AEAT: cuántos segundos hay que esperar desde
/// este envío antes del siguiente. Subirlo es su forma de pedir que se afloje el ritmo, así que
/// se respeta el que venga y no uno fijo nuestro.
/// </param>
public sealed record VerifactuBatchResult(
    IReadOnlyList<VerifactuSubmissionResult> Results,
    int WaitSeconds);

/// <summary>Hasta cuándo no se puede volver a enviar, y qué espera pidió la AEAT.</summary>
public sealed record VerifactuFlow(string IssuerTaxId, int WaitSeconds, DateTimeOffset NextAllowedAt);

/// <summary>
/// Lo que contesta la AEAT. No es un <c>Result</c> a propósito: un rechazo NO es un fallo del
/// programa, es una respuesta que hay que guardar con su motivo para poder corregirla.
/// </summary>
/// <param name="SubmissionId">
/// El CSV —código seguro de verificación— que devuelve la AEAT al aceptar el registro.
/// </param>
public sealed record VerifactuSubmissionResult(bool Accepted, string? SubmissionId, string Error);
