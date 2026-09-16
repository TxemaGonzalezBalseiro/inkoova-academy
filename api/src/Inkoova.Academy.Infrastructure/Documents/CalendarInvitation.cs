using System.Globalization;
using System.Text;

namespace Inkoova.Academy.Infrastructure.Documents;

/// <summary>Un invitado de la cita: su correo y cómo llamarle en la agenda.</summary>
public sealed record CalendarAttendee(string Email, string Name);

public sealed record CalendarInvitationRequest(
    string Uid,
    int Sequence,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    string Summary,
    string Description,
    string Location,
    CalendarAttendee Organiser,
    IReadOnlyList<CalendarAttendee> Attendees,
    DateTimeOffset Stamp,
    /// <summary>Anular en vez de convocar. Manda <c>CANCEL</c>, que es lo que borra el hueco.</summary>
    bool IsCancellation = false);

/// <summary>
/// Invitaciones de calendario en iCalendar (RFC 5545), que es lo que entienden Outlook / Office
/// 365, Google Calendar y Apple Calendar sin pedirle a nadie que conecte ninguna cuenta.
///
/// Se escribe a mano y no con una biblioteca porque el formato que hace falta aquí cabe en una
/// pantalla, y a cambio no hay una dependencia más que auditar en algo que sale por correo.
///
/// Lo que NO es evidente y hay que respetar:
///
/// · Las líneas terminan en CRLF y no pueden pasar de 75 octetos: se parten con un espacio al
///   principio de la siguiente. Outlook descarta el evento entero si se le va la línea.
/// · Las comas, los puntos y coma y las barras invertidas van escapados dentro del texto; los
///   saltos de línea viajan como «\n» literal.
/// · Las fechas van en UTC con la Z al final. Sin ella, cada cliente la interpreta en su huso y
///   la tutoría aparece a horas distintas para el alumno y para el profesor.
/// · El UID identifica el evento para siempre y la secuencia dice qué versión es. Un cliente
///   ignora una actualización cuya secuencia no supere a la que ya tiene.
/// </summary>
public static class CalendarInvitation
{
    public const string ContentType = "text/calendar";

    public static byte[] Build(CalendarInvitationRequest request)
    {
        var method = request.IsCancellation ? "CANCEL" : "REQUEST";

        var lines = new List<string>
        {
            "BEGIN:VCALENDAR",
            "VERSION:2.0",
            "PRODID:-//Inkoova Academy//Tutorias//ES",
            "CALSCALE:GREGORIAN",
            $"METHOD:{method}",
            "BEGIN:VEVENT",
            $"UID:{request.Uid}",
            $"SEQUENCE:{request.Sequence}",
            $"DTSTAMP:{Stamp(request.Stamp)}",
            $"DTSTART:{Stamp(request.StartsAt)}",
            $"DTEND:{Stamp(request.EndsAt)}",
            $"SUMMARY:{Escape(request.Summary)}",
            $"ORGANIZER;CN={Escape(request.Organiser.Name)}:mailto:{request.Organiser.Email}",
        };

        if (!string.IsNullOrWhiteSpace(request.Description))
        {
            lines.Add($"DESCRIPTION:{Escape(request.Description)}");
        }

        if (!string.IsNullOrWhiteSpace(request.Location))
        {
            lines.Add($"LOCATION:{Escape(request.Location)}");
        }

        foreach (var attendee in request.Attendees)
        {
            // RSVP=TRUE es lo que hace que el cliente enseñe los botones de aceptar y rechazar.
            lines.Add(
                $"ATTENDEE;CUTYPE=INDIVIDUAL;ROLE=REQ-PARTICIPANT;PARTSTAT=NEEDS-ACTION;RSVP=TRUE;" +
                $"CN={Escape(attendee.Name)}:mailto:{attendee.Email}");
        }

        lines.Add(request.IsCancellation ? "STATUS:CANCELLED" : "STATUS:CONFIRMED");

        if (!request.IsCancellation)
        {
            // Aviso quince minutos antes. Va dentro del evento y no como ajuste del invitado
            // para que le salte también a quien no tenga recordatorios por defecto.
            lines.AddRange([
                "BEGIN:VALARM",
                "ACTION:DISPLAY",
                "TRIGGER:-PT15M",
                $"DESCRIPTION:{Escape(request.Summary)}",
                "END:VALARM"
            ]);
        }

        lines.Add("END:VEVENT");
        lines.Add("END:VCALENDAR");

        var text = new StringBuilder();
        foreach (var line in lines)
        {
            text.Append(Fold(line)).Append("\r\n");
        }

        return Encoding.UTF8.GetBytes(text.ToString());
    }

    /// <summary>
    /// El UID del evento. Sale del identificador de la cita, así que es estable de por vida: el
    /// mismo evento en la agenda de todos, actualización tras actualización.
    /// </summary>
    public static string Uid(Guid appointmentId, string domain) =>
        $"{appointmentId:N}@{(string.IsNullOrWhiteSpace(domain) ? "inkoova.academy" : domain.Trim())}";

    private static string Stamp(DateTimeOffset moment) =>
        moment.ToUniversalTime().ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);

    /// <summary>
    /// Escapa lo que en iCalendar es sintaxis. El orden importa: la barra invertida primero, o
    /// se acabarían escapando las barras que se acaban de meter.
    /// </summary>
    private static string Escape(string value) =>
        (value ?? string.Empty)
            .Replace("\\", "\\\\")
            .Replace(";", "\\;")
            .Replace(",", "\\,")
            .Replace("\r\n", "\\n")
            .Replace("\n", "\\n")
            .Replace("\r", "\\n");

    /// <summary>
    /// Parte la línea a 75 octetos —octetos, no caracteres: en UTF-8 una tilde ocupa dos— y
    /// continúa con un espacio delante, que es lo que manda el RFC. Cortar en medio de un
    /// carácter multibyte dejaría el fichero ilegible, así que se mide byte a byte.
    /// </summary>
    private static string Fold(string line)
    {
        if (Encoding.UTF8.GetByteCount(line) <= 75)
        {
            return line;
        }

        var folded = new StringBuilder();
        var bytesInLine = 0;
        var first = true;

        foreach (var rune in line.EnumerateRunes())
        {
            var size = Encoding.UTF8.GetByteCount(rune.ToString());

            // Las continuaciones llevan un espacio delante que también cuenta para el límite.
            if (bytesInLine + size > (first ? 75 : 74))
            {
                folded.Append("\r\n ");
                bytesInLine = 1;
                first = false;
            }

            folded.Append(rune);
            bytesInLine += size;
        }

        return folded.ToString();
    }
}
