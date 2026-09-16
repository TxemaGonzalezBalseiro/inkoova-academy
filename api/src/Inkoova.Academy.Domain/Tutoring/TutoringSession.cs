using Inkoova.Academy.Domain.Common;

namespace Inkoova.Academy.Domain.Tutoring;

/// <summary>
/// Una tutoría ya dada, apuntada contra una bolsa. Es el libro mayor del consumo: el saldo del
/// alumno sale de restar estas filas, así que cada una tiene que decir cuándo fue y de qué,
/// no solo cuánto tiempo se gastó.
///
/// Se crea únicamente desde <see cref="TutoringGrant.Consume"/>, que es quien comprueba que
/// hay saldo. Instanciarla suelta permitiría apuntar tiempo que nadie tiene.
/// </summary>
public sealed class TutoringSession
{
    /// <summary>Ocho horas. Una tutoría más larga es un día entero: se apuntan varias.</summary>
    public const int MaximumMinutes = 8 * 60;

    public Guid Id { get; private set; }
    public Guid GrantId { get; private set; }
    public int Minutes { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public string Topic { get; private set; }
    public string Notes { get; private set; }
    /// <summary>Quién la apuntó en el panel. No es quien la dio.</summary>
    public Guid? RecordedBy { get; private set; }

    /// <summary>
    /// El profesor que la impartió, y a quien por tanto se le liquida.
    ///
    /// Anulable porque las tutorías apuntadas antes de que existieran los profesores no
    /// tienen a nadie asignado. Adivinarlo sería inventar a quién se le debe dinero.
    /// </summary>
    public Guid? TutorId { get; private set; }

    private TutoringSession(
        Guid id,
        Guid grantId,
        int minutes,
        DateTimeOffset occurredAt,
        string topic,
        string notes,
        Guid? recordedBy,
        Guid? tutorId)
    {
        TutorId = tutorId;
        Id = id;
        GrantId = grantId;
        Minutes = minutes;
        OccurredAt = occurredAt;
        Topic = topic;
        Notes = notes;
        RecordedBy = recordedBy;
    }

    internal static Result<TutoringSession, Error> Create(
        Guid id,
        Guid grantId,
        int minutes,
        DateTimeOffset occurredAt,
        string topic,
        string notes,
        Guid? recordedBy,
        Guid? tutorId)
    {
        if (minutes <= 0)
        {
            return Error.Validation("tutoring_session.minutes_invalid", "Una tutoría dura al menos un minuto.");
        }

        if (minutes > MaximumMinutes)
        {
            return Error.Validation(
                "tutoring_session.minutes_too_large",
                $"Una tutoría no puede pasar de {MaximumMinutes / 60} horas. Apunta varias.");
        }

        if (string.IsNullOrWhiteSpace(topic))
        {
            // Sin tema, el histórico del alumno es una lista de restas sin explicación.
            return Error.Validation("tutoring_session.topic_empty", "Indica de qué fue la tutoría.");
        }

        if (topic.Trim().Length > 200)
        {
            return Error.Validation("tutoring_session.topic_too_long", "El tema no puede pasar de 200 caracteres.");
        }

        return new TutoringSession(
            id, grantId, minutes, occurredAt, topic.Trim(), (notes ?? string.Empty).Trim(),
            recordedBy, tutorId);
    }

    public static TutoringSession Rehydrate(
        Guid id,
        Guid grantId,
        int minutes,
        DateTimeOffset occurredAt,
        string topic,
        string notes,
        Guid? recordedBy,
        Guid? tutorId) =>
        new(id, grantId, minutes, occurredAt, topic, notes, recordedBy, tutorId);
}
