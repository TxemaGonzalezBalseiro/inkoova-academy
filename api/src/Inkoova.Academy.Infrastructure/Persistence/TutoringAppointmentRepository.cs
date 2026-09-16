using Dapper;
using Inkoova.Academy.Application.Abstractions;
using Inkoova.Academy.Domain.Tutoring;

namespace Inkoova.Academy.Infrastructure.Persistence;

public sealed class TutoringAppointmentRepository(IDbConnectionFactory connections)
    : ITutoringAppointmentRepository
{
    private const string Columns = """
        id, grant_id, tutor_id, starts_at, minutes, topic, notes, location, status,
        session_id, ics_uid, ics_sequence, created_at, created_by, cancelled_reason
        """;

    public async Task<TutoringAppointment?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);

        var row = await connection.QuerySingleOrDefaultAsync<Row>(
            $"SELECT {Columns} FROM tutoring_appointment WHERE id = @id", new { id });

        return row?.ToDomain();
    }

    public async Task<IReadOnlyList<TutoringAppointment>> GetForGrantsAsync(
        IReadOnlyList<Guid> grantIds, CancellationToken ct)
    {
        if (grantIds.Count == 0)
        {
            return [];
        }

        using var connection = await connections.OpenAsync(ct);

        var rows = await connection.QueryAsync<Row>(
            $"SELECT {Columns} FROM tutoring_appointment WHERE grant_id = ANY(@ids) ORDER BY starts_at DESC",
            new { ids = grantIds.ToArray() });

        return [.. rows.Select(r => r.ToDomain())];
    }

    public async Task<IReadOnlyList<TutoringAppointment>> GetUpcomingAsync(
        DateTimeOffset from, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);

        var rows = await connection.QueryAsync<Row>(
            $"""
             SELECT {Columns} FROM tutoring_appointment
             WHERE status = 'scheduled' AND starts_at >= @from
             ORDER BY starts_at
             """,
            new { from = from.ToUniversalTime() });

        return [.. rows.Select(r => r.ToDomain())];
    }

    public async Task<TutoringAppointment?> GetBySessionAsync(Guid sessionId, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);

        var row = await connection.QuerySingleOrDefaultAsync<Row>(
            $"SELECT {Columns} FROM tutoring_appointment WHERE session_id = @sessionId",
            new { sessionId });

        return row?.ToDomain();
    }

    public async Task UpsertAsync(TutoringAppointment appointment, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);

        // El UID no está en el UPDATE: identifica el evento en la agenda de cada invitado y
        // cambiarlo dejaría el evento viejo colgado y crearía uno nuevo al lado.
        await connection.ExecuteAsync(
            """
            INSERT INTO tutoring_appointment
                (id, grant_id, tutor_id, starts_at, minutes, topic, notes, location, status,
                 session_id, ics_uid, ics_sequence, created_at, created_by, cancelled_reason)
            VALUES
                (@Id, @GrantId, @TutorId, @StartsAt, @Minutes, @Topic, @Notes, @Location, @Status,
                 @SessionId, @IcsUid, @IcsSequence, @CreatedAt, @CreatedBy, @CancelledReason)
            ON CONFLICT (id) DO UPDATE SET
                tutor_id = EXCLUDED.tutor_id,
                starts_at = EXCLUDED.starts_at,
                minutes = EXCLUDED.minutes,
                topic = EXCLUDED.topic,
                notes = EXCLUDED.notes,
                location = EXCLUDED.location,
                status = EXCLUDED.status,
                session_id = EXCLUDED.session_id,
                ics_sequence = EXCLUDED.ics_sequence,
                cancelled_reason = EXCLUDED.cancelled_reason,
                updated_at = now()
            """,
            new
            {
                appointment.Id,
                appointment.GrantId,
                appointment.TutorId,
                appointment.StartsAt,
                appointment.Minutes,
                appointment.Topic,
                appointment.Notes,
                appointment.Location,
                Status = StatusText(appointment.Status),
                appointment.SessionId,
                appointment.IcsUid,
                appointment.IcsSequence,
                appointment.CreatedAt,
                appointment.CreatedBy,
                appointment.CancelledReason
            });
    }

    /// <summary>
    /// El estado en texto, mapeado a mano. Igual que en el resto de tutorías: el nombre del enum
    /// y el valor de la base no tienen por qué coincidir, y dejar que coincidan por accidente
    /// convierte renombrar un enum en una migración silenciosa.
    /// </summary>
    private static string StatusText(AppointmentStatus status) => status switch
    {
        AppointmentStatus.Done => "done",
        AppointmentStatus.Cancelled => "cancelled",
        _ => "scheduled"
    };

    private static AppointmentStatus StatusFrom(string value) => value switch
    {
        "done" => AppointmentStatus.Done,
        "cancelled" => AppointmentStatus.Cancelled,
        _ => AppointmentStatus.Scheduled
    };

    private sealed record Row(
        Guid Id,
        Guid GrantId,
        Guid? TutorId,
        DateTimeOffset StartsAt,
        int Minutes,
        string Topic,
        string Notes,
        string Location,
        string Status,
        Guid? SessionId,
        string IcsUid,
        int IcsSequence,
        DateTimeOffset CreatedAt,
        Guid? CreatedBy,
        string CancelledReason)
    {
        public TutoringAppointment ToDomain() =>
            TutoringAppointment.Rehydrate(
                Id, GrantId, TutorId, StartsAt, Minutes, Topic, Notes, Location,
                StatusFrom(Status), SessionId, IcsUid, IcsSequence, CreatedAt, CreatedBy,
                CancelledReason);
    }
}
