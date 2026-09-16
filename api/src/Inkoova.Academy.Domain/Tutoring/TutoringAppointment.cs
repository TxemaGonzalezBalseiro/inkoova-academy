using Inkoova.Academy.Domain.Common;

namespace Inkoova.Academy.Domain.Tutoring;

public enum AppointmentStatus
{
    /// <summary>Convocada y todavía por dar.</summary>
    Scheduled,

    /// <summary>Dada. Tiene ya su <see cref="TutoringSession"/>, que es la que descontó tiempo.</summary>
    Done,

    /// <summary>Anulada. Se conserva: cancelar no es borrar, y el alumno tiene que poder verlo.</summary>
    Cancelled
}

/// <summary>
/// Una tutoría CONVOCADA: alumno, profesor, día y hora.
///
/// No descuenta saldo. El descuento ocurre cuando se da, y no antes, porque reservar el tiempo
/// al convocar haría que una cancelación olvidada dejase horas muertas que el alumno pagó y no
/// puede gastar. Mientras la cita vive, el saldo sigue siendo suyo.
/// </summary>
public sealed class TutoringAppointment
{
    /// <summary>Lo mismo que una tutoría: ocho horas. Más largo es un día entero.</summary>
    public const int MaximumMinutes = TutoringSession.MaximumMinutes;

    public Guid Id { get; private set; }
    public Guid GrantId { get; private set; }
    public Guid? TutorId { get; private set; }
    public DateTimeOffset StartsAt { get; private set; }
    public int Minutes { get; private set; }
    public string Topic { get; private set; }
    public string Notes { get; private set; }
    public string Location { get; private set; }
    public AppointmentStatus Status { get; private set; }

    /// <summary>La tutoría que nació de ella. <c>null</c> mientras no se ha dado.</summary>
    public Guid? SessionId { get; private set; }

    /// <summary>
    /// Lo que identifica el evento en el calendario de cada invitado. No cambia nunca: si
    /// cambiara, mover la hora crearía un evento nuevo y dejaría el viejo colgado en su agenda.
    /// </summary>
    public string IcsUid { get; private set; }

    /// <summary>
    /// El número de versión que exige el calendario. Los clientes ignoran una actualización
    /// cuya secuencia no supere a la que ya tienen, así que sube en cada cambio de hora.
    /// </summary>
    public int IcsSequence { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public string CancelledReason { get; private set; }

    public DateTimeOffset EndsAt => StartsAt.AddMinutes(Minutes);

    private TutoringAppointment(
        Guid id,
        Guid grantId,
        Guid? tutorId,
        DateTimeOffset startsAt,
        int minutes,
        string topic,
        string notes,
        string location,
        AppointmentStatus status,
        Guid? sessionId,
        string icsUid,
        int icsSequence,
        DateTimeOffset createdAt,
        Guid? createdBy,
        string cancelledReason)
    {
        Id = id;
        GrantId = grantId;
        TutorId = tutorId;
        StartsAt = startsAt;
        Minutes = minutes;
        Topic = topic;
        Notes = notes;
        Location = location;
        Status = status;
        SessionId = sessionId;
        IcsUid = icsUid;
        IcsSequence = icsSequence;
        CreatedAt = createdAt;
        CreatedBy = createdBy;
        CancelledReason = cancelledReason;
    }

    public static Result<TutoringAppointment, Error> Create(
        Guid id,
        Guid grantId,
        Guid? tutorId,
        DateTimeOffset startsAt,
        int minutes,
        string topic,
        string notes,
        string location,
        string icsUid,
        DateTimeOffset now,
        Guid? createdBy)
    {
        var invalid = Validate(minutes, topic);
        if (invalid is not null)
        {
            return invalid;
        }

        return new TutoringAppointment(
            id, grantId, tutorId, startsAt.ToUniversalTime(), minutes, topic.Trim(),
            (notes ?? string.Empty).Trim(), (location ?? string.Empty).Trim(),
            AppointmentStatus.Scheduled, sessionId: null, icsUid, icsSequence: 0,
            now, createdBy, string.Empty);
    }

    public static TutoringAppointment Rehydrate(
        Guid id,
        Guid grantId,
        Guid? tutorId,
        DateTimeOffset startsAt,
        int minutes,
        string topic,
        string notes,
        string location,
        AppointmentStatus status,
        Guid? sessionId,
        string icsUid,
        int icsSequence,
        DateTimeOffset createdAt,
        Guid? createdBy,
        string cancelledReason) =>
        new(id, grantId, tutorId, startsAt, minutes, topic, notes, location, status,
            sessionId, icsUid, icsSequence, createdAt, createdBy, cancelledReason);

    /// <summary>
    /// Cambia lo convocado y sube la secuencia para que los calendarios se enteren.
    ///
    /// Una cita ya dada no se mueve: su tutoría ya descontó tiempo y ya devengó, y cambiarle la
    /// hora dejaría el histórico diciendo una cosa y el calendario otra.
    /// </summary>
    public Result<Unit, Error> Reschedule(
        Guid? tutorId,
        DateTimeOffset startsAt,
        int minutes,
        string topic,
        string notes,
        string location)
    {
        if (Status != AppointmentStatus.Scheduled)
        {
            return Error.Conflict(
                "tutoring_appointment.not_scheduled",
                Status == AppointmentStatus.Done
                    ? "Esa tutoría ya se dio: no se puede cambiar la convocatoria."
                    : "Esa convocatoria está anulada.");
        }

        var invalid = Validate(minutes, topic);
        if (invalid is not null)
        {
            return invalid;
        }

        TutorId = tutorId;
        StartsAt = startsAt.ToUniversalTime();
        Minutes = minutes;
        Topic = topic.Trim();
        Notes = (notes ?? string.Empty).Trim();
        Location = (location ?? string.Empty).Trim();
        IcsSequence++;

        return Unit.Value;
    }

    public Result<Unit, Error> Cancel(string reason)
    {
        if (Status == AppointmentStatus.Done)
        {
            return Error.Conflict(
                "tutoring_appointment.already_given",
                "Esa tutoría ya se dio. Si hay que deshacerla, se deshace la tutoría.");
        }

        if (Status == AppointmentStatus.Cancelled)
        {
            return Error.Conflict("tutoring_appointment.already_cancelled", "Esa cita ya estaba anulada.");
        }

        Status = AppointmentStatus.Cancelled;
        CancelledReason = (reason ?? string.Empty).Trim();
        IcsSequence++;

        return Unit.Value;
    }

    /// <summary>
    /// La marca como dada y la ata a la tutoría que descontó el tiempo.
    ///
    /// Quien llama tiene que haber consumido ya de la bolsa: es ahí donde vive la comprobación
    /// de saldo, y duplicarla aquí daría dos sitios donde puede quedar mal.
    /// </summary>
    public Result<Unit, Error> MarkGiven(Guid sessionId)
    {
        if (Status == AppointmentStatus.Done)
        {
            return Error.Conflict("tutoring_appointment.already_given", "Esa tutoría ya estaba apuntada.");
        }

        if (Status == AppointmentStatus.Cancelled)
        {
            // Dar por buena una cita anulada escondería que se anuló: si la clase se dio de
            // todas formas, se convoca de nuevo o se apunta suelta.
            return Error.Conflict(
                "tutoring_appointment.cancelled",
                "Esa cita estaba anulada. Convócala otra vez o apunta la tutoría sin cita.");
        }

        Status = AppointmentStatus.Done;
        SessionId = sessionId;

        return Unit.Value;
    }

    /// <summary>
    /// Vuelve a «convocada» cuando se deshace la tutoría que salió de ella. Es la verdad: la
    /// clase se quedó sin dar.
    /// </summary>
    public void Reopen()
    {
        if (Status != AppointmentStatus.Done)
        {
            return;
        }

        Status = AppointmentStatus.Scheduled;
        SessionId = null;
    }

    private static Error? Validate(int minutes, string topic)
    {
        if (minutes <= 0 || minutes > MaximumMinutes)
        {
            return Error.Validation(
                "tutoring_appointment.minutes_invalid",
                $"Una tutoría dura entre un minuto y {MaximumMinutes / 60} horas.");
        }

        if (string.IsNullOrWhiteSpace(topic))
        {
            // El tema va en el asunto de la invitación: sin él, al alumno le llega una cita en
            // blanco en el calendario.
            return Error.Validation("tutoring_appointment.topic_empty", "Indica de qué va la tutoría.");
        }

        if (topic.Trim().Length > 200)
        {
            return Error.Validation("tutoring_appointment.topic_too_long", "El tema no puede pasar de 200 caracteres.");
        }

        return null;
    }
}
