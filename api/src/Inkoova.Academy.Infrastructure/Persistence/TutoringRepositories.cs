using Dapper;
using Inkoova.Academy.Application.Abstractions;
using Inkoova.Academy.Domain.Tutoring;
using Inkoova.Academy.Domain.ValueObjects;

namespace Inkoova.Academy.Infrastructure.Persistence;

public sealed class TutoringPackageRepository(IDbConnectionFactory connections) : ITutoringPackageRepository
{
    /// <summary>
    /// El modo de caducidad, en texto para la base. Se mapea a mano y no con el nombre del
    /// enum: `TutoringExpiry.WithSubscription` se llama distinto que `subscription`, y dejar
    /// que coincidan por casualidad ata el esquema al nombre de un símbolo de C#.
    /// </summary>
    internal static string ExpiryText(TutoringExpiry mode) => mode switch
    {
        TutoringExpiry.WithSubscription => "subscription",
        TutoringExpiry.Never => "never",
        _ => "fixed"
    };

    internal static TutoringExpiry ExpiryFrom(string value) => value switch
    {
        "subscription" => TutoringExpiry.WithSubscription,
        "never" => TutoringExpiry.Never,
        _ => TutoringExpiry.FixedDate
    };

    private const string Columns = """
        id, slug, name, description, minutes, price_cents, currency,
        expiry_mode, validity_days, display_order, is_active, stripe_price_id
        """;

    public async Task<IReadOnlyList<TutoringPackage>> GetActiveAsync(CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        var rows = await connection.QueryAsync<PackageRow>(
            $"SELECT {Columns} FROM tutoring_package WHERE is_active ORDER BY display_order, name");

        return [.. rows.Select(r => r.ToDomain())];
    }

    public async Task<IReadOnlyList<TutoringPackage>> GetAllAsync(CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        var rows = await connection.QueryAsync<PackageRow>(
            $"SELECT {Columns} FROM tutoring_package ORDER BY display_order, name");

        return [.. rows.Select(r => r.ToDomain())];
    }

    public async Task<TutoringPackage?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        var row = await connection.QuerySingleOrDefaultAsync<PackageRow>(
            $"SELECT {Columns} FROM tutoring_package WHERE id = @id", new { id });

        return row?.ToDomain();
    }

    public async Task<TutoringPackage?> GetBySlugAsync(string slug, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        var row = await connection.QuerySingleOrDefaultAsync<PackageRow>(
            $"SELECT {Columns} FROM tutoring_package WHERE slug = @slug", new { slug });

        return row?.ToDomain();
    }

    public async Task UpsertAsync(TutoringPackage package, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        await connection.ExecuteAsync(
            """
            INSERT INTO tutoring_package
                (id, slug, name, description, minutes, price_cents, currency,
                 expiry_mode, validity_days, display_order, is_active, stripe_price_id)
            VALUES
                (@Id, @Slug, @Name, @Description, @Minutes, @PriceCents, @Currency,
                 @ExpiryMode, @ValidityDays, @DisplayOrder, @IsActive, @StripePriceId)
            ON CONFLICT (id) DO UPDATE SET
                slug = EXCLUDED.slug,
                name = EXCLUDED.name,
                description = EXCLUDED.description,
                minutes = EXCLUDED.minutes,
                price_cents = EXCLUDED.price_cents,
                currency = EXCLUDED.currency,
                expiry_mode = EXCLUDED.expiry_mode,
                validity_days = EXCLUDED.validity_days,
                display_order = EXCLUDED.display_order,
                is_active = EXCLUDED.is_active,
                stripe_price_id = EXCLUDED.stripe_price_id,
                updated_at = now()
            """,
            new
            {
                package.Id,
                package.Slug,
                package.Name,
                package.Description,
                package.Minutes,
                PriceCents = package.Price.AmountInCents,
                Currency = package.Price.Currency,
                ExpiryMode = ExpiryText(package.ExpiryMode),
                package.ValidityDays,
                package.DisplayOrder,
                package.IsActive,
                package.StripePriceId
            });
    }

    private sealed record PackageRow(
        Guid Id,
        string Slug,
        string Name,
        string Description,
        int Minutes,
        long PriceCents,
        string Currency,
        string ExpiryMode,
        int? ValidityDays,
        int DisplayOrder,
        bool IsActive,
        string? StripePriceId)
    {
        public TutoringPackage ToDomain() =>
            TutoringPackage.Rehydrate(
                Id, Slug, Name, Description, Minutes,
                Money.Create(PriceCents, Currency).Value,
                ExpiryFrom(ExpiryMode), ValidityDays, DisplayOrder, IsActive, StripePriceId);
    }
}

public sealed class TutoringGrantRepository(IDbConnectionFactory connections) : ITutoringGrantRepository
{
    private static string ExpiryText(TutoringExpiry mode) => TutoringPackageRepository.ExpiryText(mode);

    private static TutoringExpiry ExpiryFrom(string value) => TutoringPackageRepository.ExpiryFrom(value);

    private const string GrantColumns = """
        id, user_id, package_id, package_name, minutes_total, source, purchase_id,
        granted_at, expiry_mode, expires_at, revoked_at, revoked_reason, note, granted_by,
        paid_cents, paid_currency
        """;

    private const string SessionColumns = """
        id, grant_id, minutes, occurred_at, topic, notes, recorded_by, tutor_id
        """;

    public async Task<TutoringGrant?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);

        var row = await connection.QuerySingleOrDefaultAsync<GrantRow>(
            $"SELECT {GrantColumns} FROM tutoring_grant WHERE id = @id", new { id });

        if (row is null)
        {
            return null;
        }

        var sessions = await connection.QueryAsync<SessionRow>(
            $"SELECT {SessionColumns} FROM tutoring_session WHERE grant_id = @id ORDER BY occurred_at",
            new { id });

        return row.ToDomain(sessions);
    }

    /// <summary>
    /// Las bolsas del alumno con sus sesiones en DOS consultas, no en una por bolsa: pintar la
    /// pantalla de la cuenta no debe costar N+1 viajes a la base.
    /// </summary>
    public async Task<IReadOnlyList<TutoringGrant>> GetForUserAsync(Guid userId, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);

        var rows = (await connection.QueryAsync<GrantRow>(
            $"SELECT {GrantColumns} FROM tutoring_grant WHERE user_id = @userId ORDER BY granted_at DESC",
            new { userId })).ToList();

        if (rows.Count == 0)
        {
            return [];
        }

        var ids = rows.Select(r => r.Id).ToArray();
        var sessions = (await connection.QueryAsync<SessionRow>(
            $"SELECT {SessionColumns} FROM tutoring_session WHERE grant_id = ANY(@ids) ORDER BY occurred_at",
            new { ids })).ToLookup(s => s.GrantId);

        return [.. rows.Select(r => r.ToDomain(sessions[r.Id]))];
    }

    public async Task SaveAsync(TutoringGrant grant, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        using var transaction = connection.BeginTransaction();

        await connection.ExecuteAsync(
            """
            INSERT INTO tutoring_grant
                (id, user_id, package_id, package_name, minutes_total, source, purchase_id,
                 granted_at, expiry_mode, expires_at, revoked_at, revoked_reason, note, granted_by,
                 paid_cents, paid_currency)
            VALUES
                (@Id, @UserId, @PackageId, @PackageName, @MinutesTotal, @Source, @PurchaseId,
                 @GrantedAt, @ExpiryMode, @ExpiresAt, @RevokedAt, @RevokedReason, @Note, @GrantedBy,
                 @PaidCents, @PaidCurrency)
            ON CONFLICT (id) DO UPDATE SET
                package_id = EXCLUDED.package_id,
                package_name = EXCLUDED.package_name,
                minutes_total = EXCLUDED.minutes_total,
                expiry_mode = EXCLUDED.expiry_mode,
                expires_at = EXCLUDED.expires_at,
                revoked_at = EXCLUDED.revoked_at,
                revoked_reason = EXCLUDED.revoked_reason,
                note = EXCLUDED.note
            """,
            new
            {
                grant.Id,
                grant.UserId,
                grant.PackageId,
                grant.PackageName,
                grant.MinutesTotal,
                Source = grant.Source == TutoringGrantSource.Purchase ? "purchase" : "manual",
                grant.PurchaseId,
                grant.GrantedAt,
                ExpiryMode = ExpiryText(grant.ExpiryMode),
                grant.ExpiresAt,
                grant.RevokedAt,
                grant.RevokedReason,
                grant.Note,
                grant.GrantedBy,
                PaidCents = grant.Paid.AmountInCents,
                PaidCurrency = grant.Paid.Currency
            },
            transaction);

        // Las sesiones que la entidad ya no tiene se borran: es como se deshace una tutoría mal
        // apuntada. Va dentro de la misma transacción que el alta, porque un borrado sin su
        // inserción dejaría el saldo mal durante el hueco entre las dos.
        var sessionIds = grant.Sessions.Select(s => s.Id).ToArray();
        await connection.ExecuteAsync(
            "DELETE FROM tutoring_session WHERE grant_id = @grantId AND NOT (id = ANY(@sessionIds))",
            new { grantId = grant.Id, sessionIds },
            transaction);

        foreach (var session in grant.Sessions)
        {
            await connection.ExecuteAsync(
                """
                INSERT INTO tutoring_session
                    (id, grant_id, minutes, occurred_at, topic, notes, recorded_by, tutor_id)
                VALUES
                    (@Id, @GrantId, @Minutes, @OccurredAt, @Topic, @Notes, @RecordedBy, @TutorId)
                ON CONFLICT (id) DO UPDATE SET
                    minutes = EXCLUDED.minutes,
                    occurred_at = EXCLUDED.occurred_at,
                    topic = EXCLUDED.topic,
                    notes = EXCLUDED.notes,
                    tutor_id = EXCLUDED.tutor_id
                """,
                new
                {
                    session.Id,
                    session.GrantId,
                    session.Minutes,
                    session.OccurredAt,
                    session.Topic,
                    session.Notes,
                    session.RecordedBy,
                    session.TutorId
                },
                transaction);
        }

        transaction.Commit();
    }

    /// <summary>
    /// El resumen del panel en UNA consulta. Las sesiones se agregan en un subselect y no con
    /// un JOIN directo: al unir bolsas con sesiones, <c>SUM(minutes_total)</c> contaría el
    /// total de la bolsa una vez por cada sesión que tenga.
    ///
    /// Las bolsas revocadas quedan fuera del total pero su consumo se conserva, que es lo que
    /// de verdad pasó: esas tutorías se dieron.
    /// </summary>
    public async Task<IReadOnlyList<TutoringBalanceRow>> GetBalancesAsync(CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);

        var rows = await connection.QueryAsync<TutoringBalanceRow>(
            """
            SELECT
                u.id                                                    AS user_id,
                u.display_name                                          AS display_name,
                u.email                                                 AS email,
                COALESCE(SUM(g.minutes_total) FILTER (
                    WHERE g.revoked_at IS NULL), 0)::int                 AS minutes_total,
                COALESCE(SUM(s.used) FILTER (
                    WHERE g.revoked_at IS NULL), 0)::int                 AS minutes_used,
                COUNT(*) FILTER (
                    WHERE g.revoked_at IS NULL
                      -- Una bolsa atada a la suscripción no tiene fecha: no caduca por
                      -- aquí, y si el plan se cae deja de contarse al leerla, no en
                      -- esta consulta. Este resumen cuenta bolsas con saldo, no acceso.
                      AND (g.expires_at IS NULL OR g.expires_at > now())
                      AND g.minutes_total > COALESCE(s.used, 0))::int    AS active_grants
            FROM tutoring_grant g
            JOIN app_user u ON u.id = g.user_id
            LEFT JOIN LATERAL (
                SELECT COALESCE(SUM(ts.minutes), 0) AS used
                FROM tutoring_session ts
                WHERE ts.grant_id = g.id
            ) s ON true
            GROUP BY u.id, u.display_name, u.email
            ORDER BY u.display_name
            """);

        return [.. rows];
    }

    private sealed record GrantRow(
        Guid Id,
        Guid UserId,
        Guid? PackageId,
        string PackageName,
        int MinutesTotal,
        string Source,
        Guid? PurchaseId,
        DateTimeOffset GrantedAt,
        string ExpiryMode,
        DateTimeOffset? ExpiresAt,
        DateTimeOffset? RevokedAt,
        string? RevokedReason,
        string Note,
        Guid? GrantedBy,
        long PaidCents,
        string PaidCurrency)
    {
        public TutoringGrant ToDomain(IEnumerable<SessionRow> sessions) =>
            TutoringGrant.Rehydrate(
                Id, UserId, PackageId, PackageName, MinutesTotal,
                Source == "purchase" ? TutoringGrantSource.Purchase : TutoringGrantSource.Manual,
                PurchaseId, GrantedAt, ExpiryFrom(ExpiryMode), ExpiresAt, RevokedAt,
                RevokedReason, Note, GrantedBy,
                Money.Create(PaidCents, PaidCurrency).Value,
                sessions.Select(s => s.ToDomain()));
    }

    private sealed record SessionRow(
        Guid Id,
        Guid GrantId,
        int Minutes,
        DateTimeOffset OccurredAt,
        string Topic,
        string Notes,
        Guid? RecordedBy,
        Guid? TutorId)
    {
        public TutoringSession ToDomain() =>
            TutoringSession.Rehydrate(
                Id, GrantId, Minutes, OccurredAt, Topic, Notes, RecordedBy, TutorId);
    }
}
