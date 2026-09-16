using System.Text.Json;
using System.Text.Json.Serialization;

namespace Inkoova.Academy.Api.Common;

/// <summary>
/// Pasa a UTC toda fecha que entre por el cuerpo de una petición.
///
/// Los instantes se guardan en columnas <c>timestamptz</c> y Npgsql se niega a escribir un
/// <see cref="DateTimeOffset"/> cuyo desplazamiento no sea cero:
///
/// <code>
/// Cannot write DateTimeOffset with Offset=01:00:00 to PostgreSQL type
/// 'timestamp with time zone', only offset 0 (UTC) is supported.
/// </code>
///
/// Un cliente que mandaba <c>2026-09-14T17:00:00+02:00</c> —ISO-8601 perfectamente válido—
/// se llevaba un 500 con «Ha ocurrido un error», no un error de validación. La SPA no lo
/// disparaba porque siempre manda <c>toISOString()</c>, pero la API es pública y nada en el
/// contrato obligaba a mandar UTC.
///
/// La conversión está en la frontera y no en cada handler porque el instante es el mismo con
/// cualquier desplazamiento: normalizarlo no pierde información, y hacerlo aquí cubre los
/// endpoints de hoy y los de mañana sin que nadie tenga que acordarse. El tipo anulable lo
/// cubre <c>System.Text.Json</c> solo: envuelve este convertidor para <c>DateTimeOffset?</c>.
///
/// Una fecha sin desplazamiento (<c>2026-09-14T17:00:00</c>) sigue interpretándose como hora
/// del servidor, que es lo que hacía antes. Los contenedores corren en UTC, así que en la
/// práctica se lee como UTC.
/// </summary>
public sealed class UtcDateTimeOffsetConverter : JsonConverter<DateTimeOffset>
{
    /// <summary>Lo que OpenAPI cuenta de cada campo de fecha. Ver <c>Program.cs</c>.</summary>
    public const string SchemaNote =
        "Instante ISO-8601. Se acepta cualquier desplazamiento horario " +
        "(2026-09-14T17:00:00+02:00); se guarda y se devuelve siempre en UTC.";

    public override DateTimeOffset Read(
        ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.GetDateTimeOffset().ToUniversalTime();

    /// <summary>
    /// De salida también va en UTC, para que un mismo instante se lea igual venga de donde
    /// venga. Se escribe con el formateador nativo del escritor —y no con <c>"O"</c>— para no
    /// cambiarle el formato a la SPA, que ya consume lo que emite por defecto.
    /// </summary>
    public override void Write(
        Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToUniversalTime());
}
