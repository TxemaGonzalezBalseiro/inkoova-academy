using System.Globalization;
using System.Text;
using System.Xml.Linq;
using Inkoova.Academy.Application.Abstractions;
using Inkoova.Academy.Domain.Billing;
using Microsoft.Extensions.Logging;

namespace Inkoova.Academy.Infrastructure.Invoicing;

/// <summary>
/// Remite los registros al servicio <c>RegFactuSistemaFacturacion</c> de la AEAT.
///
/// SOAP 1.1, document/literal, UTF-8, sobre una conexión autenticada con certificado de cliente.
/// No firma cada registro: en modo Veri*Factu la firma no es exigible (documento de firma
/// v0.1.5, apartado 2). Lo que acredita quién envía es el certificado de la conexión.
///
/// El endpoint depende de DOS cosas independientes —el entorno y el tipo de certificado—, y
/// mandar un sello al host de representante da un rechazo de autenticación que parece un
/// problema del certificado. Lo resuelve <see cref="AeatEndpoints"/>.
///
/// **Un rechazo no es una excepción.** Que la AEAT diga que no a un registro es una respuesta
/// que hay que guardar con su motivo para poder corregirla; solo se propaga hacia arriba lo que
/// impide saber qué ha pasado con el lote: red caída, certificado inválido, respuesta ilegible.
/// </summary>
public sealed class AeatVerifactuSubmitter(
    HttpClient http,
    AeatSubmitterOptions options,
    ILogger<AeatVerifactuSubmitter> logger) : IVerifactuSubmitter
{
    private static readonly XNamespace Soap = "http://schemas.xmlsoap.org/soap/envelope/";

    private static readonly XNamespace Response =
        "https://www2.agenciatributaria.gob.es/static_files/common/internet/dep/aplicaciones/es/aeat/tike/cont/ws/RespuestaSuministro.xsd";

    public bool IsEnabled => true;

    public async Task<VerifactuBatchResult> SubmitAsync(
        IReadOnlyList<VerifactuSubmission> batch, CancellationToken ct)
    {
        var url = AeatEndpoints.Verifactu(options.Environment, options.UsesSealCertificate);
        var envelope = Wrap(VerifactuXmlBuilder.BuildSubmission(batch).Root!);

        // Sin declaración XML y sin sangrado: lo que se manda es un documento, no algo para
        // leer, y el sangrado añade texto dentro de elementos que el servicio compara literales.
        var body = envelope.ToString(SaveOptions.DisableFormatting);

        logger.LogInformation(
            "Remitiendo {Count} registros a {Url}.", batch.Count, url);

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(body, new UTF8Encoding(false), "text/xml")
        };

        // SOAP 1.1 exige la cabecera aunque vaya vacía: el WSDL declara soapAction="".
        request.Headers.Add("SOAPAction", "\"\"");

        using var answer = await http.SendAsync(request, ct);
        var text = await answer.Content.ReadAsStringAsync(ct);

        if (!answer.IsSuccessStatusCode)
        {
            // Un 500 con Fault SÍ trae motivo: la AEAT contesta los errores de validación del
            // sobre como Fault, y perderlo dejaría «error 500» como única pista.
            var fault = ReadFault(text);

            throw new InvalidOperationException(
                $"La AEAT ha respondido {(int)answer.StatusCode} al remitir {batch.Count} " +
                $"registros: {fault ?? Shorten(text)}");
        }

        return Parse(text, batch.Count);
    }

    private static XElement Wrap(XElement payload) =>
        new(Soap + "Envelope",
            new XAttribute(XNamespace.Xmlns + "soapenv", Soap.NamespaceName),
            new XElement(Soap + "Body", payload));

    /// <summary>
    /// Traduce la respuesta a un resultado por registro, EN EL MISMO ORDEN en que se mandaron.
    ///
    /// Si vinieran menos líneas de las mandadas, las que faltan se dan por no aceptadas y se
    /// quedan pendientes. Al revés —darlas por buenas— dejaría facturas sin declarar creyendo
    /// que lo están, que es el único fallo aquí del que no se sale solo.
    /// </summary>
    private VerifactuBatchResult Parse(string xml, int expected)
    {
        XDocument document;

        try
        {
            document = XDocument.Parse(xml);
        }
        catch (System.Xml.XmlException failure)
        {
            throw new InvalidOperationException(
                "La respuesta de la AEAT no es XML válido. No se puede saber qué registros " +
                "han entrado, así que el lote se queda pendiente.", failure);
        }

        var root = document.Descendants(Response + "RespuestaRegFactuSistemaFacturacion").FirstOrDefault()
                   ?? throw new InvalidOperationException(
                       "La respuesta de la AEAT no trae RespuestaRegFactuSistemaFacturacion: " +
                       Shorten(xml));

        var wait = int.TryParse(
            root.Element(Response + "TiempoEsperaEnvio")?.Value,
            NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds) && seconds > 0
            ? seconds
            : VerifactuLimits.InitialWaitSeconds;

        var estado = root.Element(Response + "EstadoEnvio")?.Value ?? string.Empty;

        var lines = root.Elements(Response + "RespuestaLinea").ToList();
        var results = new List<VerifactuSubmissionResult>(expected);

        foreach (var line in lines.Take(expected))
        {
            var registro = line.Element(Response + "EstadoRegistro")?.Value ?? string.Empty;

            // «AceptadoConErrores» es aceptado: el registro consta, con avisos. Tratarlo como
            // rechazo haría que se reintentara algo que ya está declarado, y el reenvío llega
            // como duplicado.
            var accepted = registro is "Correcto" or "AceptadoConErrores";

            var error = string.Join(' ', new[]
                {
                    line.Element(Response + "CodigoErrorRegistro")?.Value,
                    line.Element(Response + "DescripcionErrorRegistro")?.Value
                }
                .Where(v => !string.IsNullOrWhiteSpace(v)));

            results.Add(new VerifactuSubmissionResult(
                accepted,
                accepted ? line.Element(Response + "CSV")?.Value ?? root.Element(Response + "CSV")?.Value : null,
                accepted ? string.Empty : Fallback(error, registro)));
        }

        if (results.Count < expected)
        {
            logger.LogError(
                "La AEAT ha contestado {Got} líneas para {Sent} registros (estado {Estado}). " +
                "Los que faltan se quedan pendientes.",
                results.Count, expected, estado);

            while (results.Count < expected)
            {
                results.Add(new VerifactuSubmissionResult(
                    false, null, "La AEAT no ha contestado por este registro."));
            }
        }

        return new VerifactuBatchResult(results, wait);
    }

    private static string Fallback(string error, string estado) =>
        string.IsNullOrWhiteSpace(error)
            ? $"La AEAT ha rechazado el registro (estado «{estado}») sin detallar el motivo."
            : error;

    private static string? ReadFault(string xml)
    {
        try
        {
            return XDocument.Parse(xml)
                .Descendants(Soap + "Fault")
                .Select(f => f.Element("faultstring")?.Value ?? f.Value)
                .FirstOrDefault();
        }
        catch (System.Xml.XmlException)
        {
            return null;
        }
    }

    private static string Shorten(string text) =>
        text.Length <= 500 ? text : text[..500] + "…";
}

/// <summary>
/// Cómo se conecta con la AEAT. El certificado NO está aquí: lo monta el
/// <see cref="System.Net.Http.HttpClientHandler"/> que se registra en la inyección, para que la
/// clave privada no viaje por la aplicación.
/// </summary>
public sealed record AeatSubmitterOptions(AeatEnvironment Environment, bool UsesSealCertificate);
