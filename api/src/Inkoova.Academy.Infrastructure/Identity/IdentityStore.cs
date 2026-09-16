using Dapper;
using Inkoova.Academy.Infrastructure.Persistence;

namespace Inkoova.Academy.Infrastructure.Identity;

public sealed record IdentityUserRow(
    Guid Id,
    string NormalizedEmail,
    string Email,
    bool EmailConfirmed,
    string? PasswordHash,
    string SecurityStamp,
    string ConcurrencyStamp,
    DateTimeOffset? LockoutEnd,
    bool LockoutEnabled,
    int AccessFailedCount,
    DateTimeOffset CreatedAt);

public sealed record ExternalLoginRow(string Provider, string? Email, DateTimeOffset LinkedAt);

public sealed record TokenPurposes
{
    public const string EmailConfirmation = "email_confirmation";
    public const string PasswordReset = "password_reset";
}

/// <summary>
/// Dapper access to the credential tables. Kept separate from <c>UserRepository</c> because
/// the application user and the credentials are different concerns that happen to share an id.
/// </summary>
public sealed class IdentityStore(IDbConnectionFactory connections)
{
    private const string Columns = """
        id, normalized_email, email, email_confirmed, password_hash, security_stamp,
        concurrency_stamp, lockout_end, lockout_enabled, access_failed_count, created_at
        """;

    // Las mismas columnas con alias, para las consultas con JOIN: sin el prefijo, `email` es
    // ambiguo en cuanto se une con external_login, que también lo tiene.
    private const string ColumnsPrefixed = """
        u.id, u.normalized_email, u.email, u.email_confirmed, u.password_hash, u.security_stamp,
        u.concurrency_stamp, u.lockout_end, u.lockout_enabled, u.access_failed_count, u.created_at
        """;

    public async Task<IdentityUserRow?> FindByEmailAsync(string email, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        return await connection.QuerySingleOrDefaultAsync<IdentityUserRow>(
            $"SELECT {Columns} FROM identity_user WHERE normalized_email = @normalized",
            new { normalized = Normalize(email) });
    }

    public async Task<IdentityUserRow?> FindByIdAsync(Guid id, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        return await connection.QuerySingleOrDefaultAsync<IdentityUserRow>(
            $"SELECT {Columns} FROM identity_user WHERE id = @id", new { id });
    }

    // ── cuentas externas (Google, Apple) ───────────────────────────────────────────────

    /// <summary>
    /// El usuario de la academia enlazado con esa cuenta del proveedor, si lo hay. La búsqueda
    /// es por <c>sub</c> del proveedor y nunca por email: el correo puede cambiar y en Apple
    /// puede ser un alias distinto por aplicación.
    /// </summary>
    public async Task<IdentityUserRow?> FindByExternalLoginAsync(
        string provider, string providerKey, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        return await connection.QuerySingleOrDefaultAsync<IdentityUserRow>(
            $"""
            SELECT {ColumnsPrefixed}
            FROM external_login e
            JOIN identity_user u ON u.id = e.user_id
            WHERE e.provider = @provider AND e.provider_key = @providerKey
            """,
            new { provider, providerKey });
    }

    public async Task LinkExternalLoginAsync(
        string provider, string providerKey, Guid userId, string? email, DateTimeOffset now, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        await connection.ExecuteAsync(
            """
            INSERT INTO external_login (provider, provider_key, user_id, email, linked_at, last_login_at)
            VALUES (@provider, @providerKey, @userId, @email, @now, @now)
            ON CONFLICT (provider, provider_key) DO UPDATE SET
                email = EXCLUDED.email,
                last_login_at = EXCLUDED.last_login_at
            """,
            new { provider, providerKey, userId, email, now });
    }

    public async Task<IReadOnlyList<ExternalLoginRow>> GetExternalLoginsAsync(Guid userId, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        var rows = await connection.QueryAsync<ExternalLoginRow>(
            """
            SELECT provider, email, linked_at
            FROM external_login
            WHERE user_id = @userId
            ORDER BY linked_at
            """,
            new { userId });

        return [.. rows];
    }

    public async Task<int> RemoveExternalLoginAsync(Guid userId, string provider, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        return await connection.ExecuteAsync(
            "DELETE FROM external_login WHERE user_id = @userId AND provider = @provider",
            new { userId, provider });
    }

    /// <summary>Returns false when the email is already taken; the caller must not leak which.</summary>
    public async Task<bool> TryCreateAsync(IdentityUserRow user, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        var inserted = await connection.ExecuteAsync(
            """
            INSERT INTO identity_user (id, normalized_email, email, email_confirmed, password_hash,
                                       security_stamp, concurrency_stamp, lockout_end, lockout_enabled,
                                       access_failed_count, created_at)
            VALUES (@Id, @NormalizedEmail, @Email, @EmailConfirmed, @PasswordHash,
                    @SecurityStamp, @ConcurrencyStamp, @LockoutEnd, @LockoutEnabled,
                    @AccessFailedCount, @CreatedAt)
            ON CONFLICT (normalized_email) DO NOTHING
            """,
            user);

        return inserted == 1;
    }

    public async Task UpdateAsync(IdentityUserRow user, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        await connection.ExecuteAsync(
            """
            UPDATE identity_user SET
                normalized_email = @NormalizedEmail,
                email = @Email,
                email_confirmed = @EmailConfirmed,
                password_hash = @PasswordHash,
                security_stamp = @SecurityStamp,
                concurrency_stamp = @ConcurrencyStamp,
                lockout_end = @LockoutEnd,
                access_failed_count = @AccessFailedCount
            WHERE id = @Id
            """,
            user);
    }

    public async Task<IReadOnlyList<string>> GetRolesAsync(Guid userId, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        var roles = await connection.QueryAsync<string>(
            "SELECT role FROM identity_user_role WHERE user_id = @userId", new { userId });

        return roles.ToList();
    }

    public async Task AddRoleAsync(Guid userId, string role, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        await connection.ExecuteAsync(
            """
            INSERT INTO identity_user_role (user_id, role) VALUES (@userId, @role)
            ON CONFLICT (user_id, role) DO NOTHING
            """,
            new { userId, role });
    }

    // ── one-time tokens ────────────────────────────────────────────────────────────────

    public async Task StoreTokenAsync(
        Guid id,
        Guid userId,
        string purpose,
        string tokenHash,
        DateTimeOffset expiresAt,
        CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        await connection.ExecuteAsync(
            """
            INSERT INTO identity_user_token (id, user_id, purpose, token_hash, expires_at)
            VALUES (@id, @userId, @purpose, @tokenHash, @expiresAt)
            """,
            new { id, userId, purpose, tokenHash, expiresAt });
    }

    /// <summary>
    /// Consumes a one-time token atomically: the UPDATE only matches while the token is
    /// unconsumed, so two concurrent requests cannot both redeem it.
    /// </summary>
    public async Task<Guid?> ConsumeTokenAsync(
        string tokenHash,
        string purpose,
        DateTimeOffset now,
        CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        return await connection.QuerySingleOrDefaultAsync<Guid?>(
            """
            UPDATE identity_user_token
            SET consumed_at = @now
            WHERE token_hash = @tokenHash AND purpose = @purpose
              AND consumed_at IS NULL AND expires_at > @now
            RETURNING user_id
            """,
            new { tokenHash, purpose, now });
    }

    // ── refresh tokens ─────────────────────────────────────────────────────────────────

    public async Task StoreRefreshTokenAsync(
        Guid id,
        Guid userId,
        Guid familyId,
        string tokenHash,
        DateTimeOffset expiresAt,
        CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        await connection.ExecuteAsync(
            """
            INSERT INTO identity_refresh_token (id, user_id, family_id, token_hash, expires_at)
            VALUES (@id, @userId, @familyId, @tokenHash, @expiresAt)
            """,
            new { id, userId, familyId, tokenHash, expiresAt });
    }

    public async Task<RefreshTokenRow?> FindRefreshTokenAsync(string tokenHash, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        return await connection.QuerySingleOrDefaultAsync<RefreshTokenRow>(
            """
            SELECT id, user_id, family_id, token_hash, expires_at, revoked_at
            FROM identity_refresh_token WHERE token_hash = @tokenHash
            """,
            new { tokenHash });
    }

    public async Task RevokeRefreshTokenAsync(Guid id, DateTimeOffset now, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        await connection.ExecuteAsync(
            "UPDATE identity_refresh_token SET revoked_at = @now WHERE id = @id AND revoked_at IS NULL",
            new { id, now });
    }

    /// <summary>
    /// Reuse of an already-rotated token means the chain leaked: revoke the whole family so
    /// the attacker and the victim are both logged out rather than racing each other.
    /// </summary>
    public async Task RevokeFamilyAsync(Guid familyId, DateTimeOffset now, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        await connection.ExecuteAsync(
            "UPDATE identity_refresh_token SET revoked_at = @now WHERE family_id = @familyId AND revoked_at IS NULL",
            new { familyId, now });
    }

    public async Task RevokeAllForUserAsync(Guid userId, DateTimeOffset now, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        await connection.ExecuteAsync(
            "UPDATE identity_refresh_token SET revoked_at = @now WHERE user_id = @userId AND revoked_at IS NULL",
            new { userId, now });
    }

    public static string Normalize(string email) => email.Trim().ToUpperInvariant();
}

public sealed record RefreshTokenRow(
    Guid Id,
    Guid UserId,
    Guid FamilyId,
    string TokenHash,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? RevokedAt);
