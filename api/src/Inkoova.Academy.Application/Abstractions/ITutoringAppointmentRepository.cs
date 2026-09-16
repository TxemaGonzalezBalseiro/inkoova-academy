using Inkoova.Academy.Domain.Tutoring;

namespace Inkoova.Academy.Application.Abstractions;

public interface ITutoringAppointmentRepository
{
    Task<TutoringAppointment?> GetByIdAsync(Guid id, CancellationToken ct);

    Task<IReadOnlyList<TutoringAppointment>> GetForGrantsAsync(
        IReadOnlyList<Guid> grantIds, CancellationToken ct);

    /// <summary>Lo convocado y sin dar a partir de una fecha. Es la agenda del panel.</summary>
    Task<IReadOnlyList<TutoringAppointment>> GetUpcomingAsync(DateTimeOffset from, CancellationToken ct);

    /// <summary>La cita que salió en esta tutoría, si la hubo. Sirve para reabrirla al deshacerla.</summary>
    Task<TutoringAppointment?> GetBySessionAsync(Guid sessionId, CancellationToken ct);

    Task UpsertAsync(TutoringAppointment appointment, CancellationToken ct);
}

/// <summary>
/// A quién hay que avisar de una tutoría convocada, con lo que hace falta para escribirlo.
///
/// Lleva la marca porque el correo sale del buzón de la identidad del alumno, no de uno global:
/// quien estudia en una marca no debe recibir una convocatoria firmada por otra.
/// </summary>
public sealed record TutoringInvitation(
    Guid AppointmentId,
    string IcsUid,
    int Sequence,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    string Topic,
    string Notes,
    string Location,
    string StudentName,
    string StudentEmail,
    string? TutorName,
    string? TutorEmail,
    Guid? IdentityId,
    bool IsCancellation,
    string CancelledReason);

/// <summary>
/// Manda la invitación de calendario a quien va a la tutoría.
///
/// Es un puerto y no una llamada directa al correo porque lo que se manda es una invitación en
/// iCalendar, y eso —cómo se compone, cómo se adjunta— es un detalle de infraestructura que no
/// tiene por qué conocer un caso de uso.
/// </summary>
public interface ITutoringInvitationSender
{
    /// <summary>
    /// Avisa a los invitados. NO devuelve error: una convocatoria que se guardó bien no se
    /// deshace porque el servidor de correo esté caído un minuto, y quien la creó la ve en el
    /// panel igual. Los fallos se registran.
    /// </summary>
    Task SendAsync(TutoringInvitation invitation, CancellationToken ct);
}
