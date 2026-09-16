using System.Globalization;
using Inkoova.Academy.Application.Abstractions;
using Inkoova.Academy.Infrastructure.Documents;
using Microsoft.Extensions.Logging;

namespace Inkoova.Academy.Infrastructure.Messaging;

/// <summary>
/// Manda la invitación de calendario de una tutoría al alumno y al profesor.
///
/// Va por correo con un adjunto iCalendar y no contra la API de ningún calendario a propósito:
/// así funciona con Outlook / Office 365, Gmail y Apple Calendar sin que nadie tenga que
/// conectar su cuenta ni conceder permisos, y sin depender de que un token siga vivo el día que
/// haya que mover una tutoría. Si algún día hace falta escribir directamente en la agenda de la
/// casa, se añade encima; esto seguiría sirviendo para el alumno, que es de fuera.
/// </summary>
public sealed class TutoringInvitationSender(
    IEmailSender email,
    IEmailTemplateRenderer templates,
    IAcademyIdentityRepository identities,
    IClock clock,
    ILogger<TutoringInvitationSender> logger) : ITutoringInvitationSender
{
    public async Task SendAsync(TutoringInvitation invitation, CancellationToken ct)
    {
        // Nada de lo que pase aquí puede tirar la operación: la cita ya está guardada y quien la
        // creó la ve en el panel. Perderla porque el correo falle sería cambiar un aviso que no
        // llega por una tutoría que no existe.
        try
        {
            await ComposeAndSendAsync(invitation, ct);
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            logger.LogError(
                failure,
                "No se ha podido avisar de la tutoría {Appointment}. La cita está guardada.",
                invitation.AppointmentId);
        }
    }

    private async Task ComposeAndSendAsync(TutoringInvitation invitation, CancellationToken ct)
    {
        var identity = invitation.IdentityId is { } identityId
            ? await identities.GetByIdAsync(identityId, ct) ?? await identities.GetDefaultAsync(ct)
            : await identities.GetDefaultAsync(ct);

        var organiser = new CalendarAttendee(
            string.IsNullOrWhiteSpace(identity.SupportEmail) ? "no-reply@inkoova.academy" : identity.SupportEmail,
            identity.Name);

        var guests = new List<CalendarAttendee>
        {
            new(invitation.StudentEmail, invitation.StudentName)
        };

        if (!string.IsNullOrWhiteSpace(invitation.TutorEmail))
        {
            guests.Add(new CalendarAttendee(invitation.TutorEmail!, invitation.TutorName ?? invitation.TutorEmail!));
        }

        var ics = CalendarInvitation.Build(new CalendarInvitationRequest(
            invitation.IcsUid,
            invitation.Sequence,
            invitation.StartsAt,
            invitation.EndsAt,
            $"Tutoría · {invitation.Topic}",
            Description(invitation),
            invitation.Location,
            organiser,
            guests,
            clock.UtcNow,
            invitation.IsCancellation));

        foreach (var guest in guests)
        {
            await SendToAsync(invitation, identity.Id, guest, ics, ct);
        }
    }

    private async Task SendToAsync(
        TutoringInvitation invitation,
        Guid identityId,
        CalendarAttendee guest,
        byte[] ics,
        CancellationToken ct)
    {
        var model = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["nombre"] = guest.Name,
            ["cuando"] = LongMoment(invitation.StartsAt),
            ["duracion"] = Duration(invitation.StartsAt, invitation.EndsAt),
            ["tema"] = invitation.Topic,
            ["profesor"] = invitation.TutorName ?? "Por asignar",
            ["donde"] = string.IsNullOrWhiteSpace(invitation.Location)
                ? "Se indicará antes de la sesión"
                : invitation.Location,
            // Un cambio de hora no es una convocatoria nueva, y decirle «se ha convocado» a
            // quien ya la tenía apuntada le hace pensar que son dos tutorías.
            ["intro"] = invitation.Sequence == 0
                ? "Tienes una tutoría convocada."
                : "Ha cambiado la tutoría que tenías convocada. Estos son los datos nuevos.",
            ["motivo"] = string.IsNullOrWhiteSpace(invitation.CancelledReason)
                ? "No se ha indicado el motivo."
                : invitation.CancelledReason
        };

        var template = invitation.IsCancellation ? "tutoring-cancelled" : "tutoring-invitation";

        var rendered = await templates.RenderAsync(template, model, ct, identityId);
        if (rendered.IsFailure)
        {
            logger.LogError(
                "No se ha podido componer la invitación de la tutoría {Appointment}: {Error}",
                invitation.AppointmentId, rendered.Error.Message);
            return;
        }

        var (subject, html, text) = rendered.Value;

        var sent = await email.SendAsync(
            new EmailMessage(
                guest.Email,
                subject,
                html,
                text,
                [("tutoria.ics", ics, CalendarInvitation.ContentType)],
                identityId),
            ct);

        if (sent.IsFailure)
        {
            // No se propaga: la cita quedó guardada y quien la creó la ve en el panel. Tirar la
            // operación entera porque el correo esté caído un minuto sería perder la cita.
            logger.LogError(
                "No se ha podido avisar a {Guest} de la tutoría {Appointment}: {Error}",
                guest.Email, invitation.AppointmentId, sent.Error.Message);
        }
    }

    private static string Description(TutoringInvitation invitation)
    {
        var parts = new List<string> { invitation.Topic };

        if (!string.IsNullOrWhiteSpace(invitation.TutorName))
        {
            parts.Add($"Profesor: {invitation.TutorName}");
        }

        if (!string.IsNullOrWhiteSpace(invitation.Notes))
        {
            parts.Add(invitation.Notes);
        }

        return string.Join("\n", parts);
    }

    private static readonly string[] Days =
        ["domingo", "lunes", "martes", "miércoles", "jueves", "viernes", "sábado"];

    private static readonly string[] Months =
    [
        "enero", "febrero", "marzo", "abril", "mayo", "junio",
        "julio", "agosto", "septiembre", "octubre", "noviembre", "diciembre"
    ];

    /// <summary>
    /// "lunes, 14 de septiembre de 2026, 17:00". En hora peninsular, que es donde están el
    /// alumno y el profesor; el adjunto va en UTC y cada calendario lo traduce solo.
    ///
    /// Los nombres van escritos y no salen de <c>CultureInfo("es-ES")</c>: la API se compila con
    /// <c>InvariantGlobalization</c>, donde pedir una cultura concreta lanza una excepción. Con
    /// la cultura invariante saldría «Monday, September», que es peor que tener doce cadenas.
    /// </summary>
    private static string LongMoment(DateTimeOffset moment)
    {
        var local = TimeZoneInfo.ConvertTime(moment, Spain);

        return $"{Days[(int)local.DayOfWeek]}, {local.Day} de {Months[local.Month - 1]} " +
               $"de {local.Year}, {local:HH\\:mm}";
    }

    private static string Duration(DateTimeOffset start, DateTimeOffset end)
    {
        var minutes = (int)(end - start).TotalMinutes;
        var hours = minutes / 60;
        var rest = minutes % 60;

        if (hours == 0) return $"{rest} min";
        if (rest == 0) return $"{hours} h";

        return $"{hours} h {rest} min";
    }

    /// <summary>
    /// El huso se resuelve por los dos identificadores: Linux usa el de la IANA y Windows el
    /// suyo, y el contenedor y la máquina de desarrollo no son el mismo sistema.
    /// </summary>
    private static readonly TimeZoneInfo Spain = Resolve();

    private static TimeZoneInfo Resolve()
    {
        foreach (var id in (string[])["Europe/Madrid", "Romance Standard Time"])
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
                // El otro identificador.
            }
        }

        return TimeZoneInfo.Utc;
    }
}
