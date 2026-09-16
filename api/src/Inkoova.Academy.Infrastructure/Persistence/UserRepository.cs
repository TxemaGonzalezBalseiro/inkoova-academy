using Dapper;
using Inkoova.Academy.Application.Abstractions;
using Inkoova.Academy.Domain.Users;

namespace Inkoova.Academy.Infrastructure.Persistence;

public sealed class UserRepository(IDbConnectionFactory connections) : IUserRepository
{
    private const string Columns =
        "id, email, display_name, email_confirmed, stripe_customer_id, created_at, deleted_at";

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        var row = await connection.QuerySingleOrDefaultAsync<UserRow>(
            $"SELECT {Columns} FROM app_user WHERE id = @id", new { id });

        return row?.ToDomain();
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        var row = await connection.QuerySingleOrDefaultAsync<UserRow>(
            $"SELECT {Columns} FROM app_user WHERE lower(email) = lower(@email)", new { email });

        return row?.ToDomain();
    }

    public async Task<User?> GetByStripeCustomerIdAsync(string stripeCustomerId, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        var row = await connection.QuerySingleOrDefaultAsync<UserRow>(
            $"SELECT {Columns} FROM app_user WHERE stripe_customer_id = @stripeCustomerId",
            new { stripeCustomerId });

        return row?.ToDomain();
    }

    /// <summary>
    /// Admin search over email and name. Deleted accounts are excluded: their fields are
    /// anonymised and matching them would only surface noise.
    /// </summary>
    public async Task<IReadOnlyList<User>> SearchAsync(string term, int limit, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        var rows = await connection.QueryAsync<UserRow>(
            $"""
             SELECT {Columns} FROM app_user
             WHERE deleted_at IS NULL
               AND (email ILIKE @pattern OR display_name ILIKE @pattern)
             ORDER BY created_at DESC
             LIMIT @limit
             """,
            new { pattern = $"%{term}%", limit });

        return rows.Select(r => r.ToDomain()).ToList();
    }

    public async Task UpsertAsync(User user, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        await connection.ExecuteAsync(
            """
            INSERT INTO app_user (id, email, display_name, email_confirmed, stripe_customer_id, created_at, deleted_at)
            VALUES (@Id, @Email, @DisplayName, @EmailConfirmed, @StripeCustomerId, @CreatedAt, @DeletedAt)
            ON CONFLICT (id) DO UPDATE SET
                email = EXCLUDED.email,
                display_name = EXCLUDED.display_name,
                email_confirmed = EXCLUDED.email_confirmed,
                stripe_customer_id = EXCLUDED.stripe_customer_id,
                deleted_at = EXCLUDED.deleted_at
            """,
            new
            {
                user.Id,
                user.Email,
                user.DisplayName,
                user.EmailConfirmed,
                user.StripeCustomerId,
                user.CreatedAt,
                user.DeletedAt
            });
    }

    private sealed record UserRow(
        Guid Id,
        string Email,
        string DisplayName,
        bool EmailConfirmed,
        string? StripeCustomerId,
        DateTimeOffset CreatedAt,
        DateTimeOffset? DeletedAt)
    {
        public User ToDomain() =>
            User.Rehydrate(Id, Email, DisplayName, EmailConfirmed, StripeCustomerId, CreatedAt, DeletedAt);
    }
}
