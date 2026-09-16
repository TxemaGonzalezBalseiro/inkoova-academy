using Dapper;
using Inkoova.Academy.Application.Abstractions;
using Inkoova.Academy.Domain.Tutoring;
using Inkoova.Academy.Domain.ValueObjects;

namespace Inkoova.Academy.Infrastructure.Persistence;

public sealed class TutorRepository(IDbConnectionFactory connections) : ITutorRepository
{
    private const string Columns = """
        id, user_id, display_name, email, bio, commission_percent, is_active
        """;

    public async Task<IReadOnlyList<Tutor>> GetAllAsync(CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);

        // Los activos primero: es la lista que se abre para elegir quién dio una tutoría, y los
        // que ya no dan clase estorban arriba.
        var rows = await connection.QueryAsync<TutorRow>(
            $"SELECT {Columns} FROM tutor ORDER BY is_active DESC, display_name");

        return [.. rows.Select(r => r.ToDomain())];
    }

    public async Task<Tutor?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);

        var row = await connection.QuerySingleOrDefaultAsync<TutorRow>(
            $"SELECT {Columns} FROM tutor WHERE id = @id", new { id });

        return row?.ToDomain();
    }

    /// <summary>Por correo en minúsculas, que es como lo indexa la base: un profesor por correo.</summary>
    public async Task<Tutor?> GetByEmailAsync(string email, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);

        var row = await connection.QuerySingleOrDefaultAsync<TutorRow>(
            $"SELECT {Columns} FROM tutor WHERE lower(email) = lower(@email)", new { email });

        return row?.ToDomain();
    }

    public async Task UpsertAsync(Tutor tutor, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);

        await connection.ExecuteAsync(
            """
            INSERT INTO tutor
                (id, user_id, display_name, email, bio, commission_percent, is_active)
            VALUES
                (@Id, @UserId, @DisplayName, @Email, @Bio, @CommissionPercent, @IsActive)
            ON CONFLICT (id) DO UPDATE SET
                user_id = EXCLUDED.user_id,
                display_name = EXCLUDED.display_name,
                email = EXCLUDED.email,
                bio = EXCLUDED.bio,
                commission_percent = EXCLUDED.commission_percent,
                is_active = EXCLUDED.is_active,
                updated_at = now()
            """,
            new
            {
                tutor.Id,
                tutor.UserId,
                tutor.DisplayName,
                tutor.Email,
                tutor.Bio,
                tutor.CommissionPercent,
                tutor.IsActive
            });
    }

    private sealed record TutorRow(
        Guid Id,
        Guid? UserId,
        string DisplayName,
        string Email,
        string Bio,
        decimal CommissionPercent,
        bool IsActive)
    {
        public Tutor ToDomain() =>
            Tutor.Rehydrate(Id, UserId, DisplayName, Email, Bio, CommissionPercent, IsActive);
    }
}

public sealed class TutorEarningRepository(IDbConnectionFactory connections) : ITutorEarningRepository
{
    /// <summary>
    /// Lo devengado con el contexto que hace falta para explicarlo: de qué sesión, de qué alumno
    /// y cuándo fue. Se une aquí y no en la pantalla porque una liquidación sin ese contexto es
    /// una lista de importes que nadie puede comprobar tres meses después.
    /// </summary>
    private const string Detail = """
        SELECT
            e.id, e.tutor_id, e.session_id, e.base_cents, e.percent, e.amount_cents, e.currency,
            (e.status = 'paid') AS is_paid, e.created_at, e.paid_at, e.payout_note,
            s.occurred_at, s.minutes, s.topic,
            u.display_name AS student_name
        FROM tutor_earning e
        JOIN tutoring_session s ON s.id = e.session_id
        JOIN tutoring_grant g   ON g.id = s.grant_id
        JOIN app_user u         ON u.id = g.user_id
        """;

    public async Task<IReadOnlyList<TutorEarningRow>> GetForTutorAsync(Guid tutorId, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);

        var rows = await connection.QueryAsync<TutorEarningRow>(
            $"{Detail} WHERE e.tutor_id = @tutorId ORDER BY s.occurred_at DESC",
            new { tutorId });

        return [.. rows];
    }

    public async Task<IReadOnlyList<TutorEarningRow>> GetAllAsync(CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);

        var rows = await connection.QueryAsync<TutorEarningRow>(
            $"{Detail} ORDER BY s.occurred_at DESC");

        return [.. rows];
    }

    public async Task<TutorEarning?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);

        var row = await connection.QuerySingleOrDefaultAsync<EarningRow>(
            $"SELECT {EarningRow.Columns} FROM tutor_earning WHERE id = @id", new { id });

        return row?.ToDomain();
    }

    public async Task<TutorEarning?> GetBySessionAsync(Guid sessionId, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);

        var row = await connection.QuerySingleOrDefaultAsync<EarningRow>(
            $"SELECT {EarningRow.Columns} FROM tutor_earning WHERE session_id = @sessionId",
            new { sessionId });

        return row?.ToDomain();
    }

    public async Task SaveAsync(TutorEarning earning, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);

        // Solo se actualiza lo del pago. La base y el porcentaje se escriben una vez y no se
        // vuelven a tocar: son la explicación de la cifra, y reescribirlas al liquidar borraría
        // con qué acuerdo se calculó.
        await connection.ExecuteAsync(
            """
            INSERT INTO tutor_earning
                (id, tutor_id, session_id, base_cents, percent, amount_cents, currency,
                 status, created_at, paid_at, payout_note)
            VALUES
                (@Id, @TutorId, @SessionId, @BaseCents, @Percent, @AmountCents, @Currency,
                 @Status, @CreatedAt, @PaidAt, @PayoutNote)
            ON CONFLICT (id) DO UPDATE SET
                status = EXCLUDED.status,
                paid_at = EXCLUDED.paid_at,
                payout_note = EXCLUDED.payout_note
            """,
            new
            {
                earning.Id,
                earning.TutorId,
                earning.SessionId,
                BaseCents = earning.Base.AmountInCents,
                earning.Percent,
                AmountCents = earning.Amount.AmountInCents,
                Currency = earning.Amount.Currency,
                Status = earning.IsPaid ? "paid" : "pending",
                earning.CreatedAt,
                earning.PaidAt,
                earning.PayoutNote
            });
    }

    private sealed record EarningRow(
        Guid Id,
        Guid TutorId,
        Guid SessionId,
        long BaseCents,
        decimal Percent,
        long AmountCents,
        string Currency,
        string Status,
        DateTimeOffset CreatedAt,
        DateTimeOffset? PaidAt,
        string PayoutNote)
    {
        public const string Columns = """
            id, tutor_id, session_id, base_cents, percent, amount_cents, currency,
            status, created_at, paid_at, payout_note
            """;

        public TutorEarning ToDomain() =>
            TutorEarning.Rehydrate(
                Id, TutorId, SessionId,
                Money.Create(BaseCents, Currency).Value,
                Percent,
                Money.Create(AmountCents, Currency).Value,
                Status == "paid", CreatedAt, PaidAt, PayoutNote);
    }
}
