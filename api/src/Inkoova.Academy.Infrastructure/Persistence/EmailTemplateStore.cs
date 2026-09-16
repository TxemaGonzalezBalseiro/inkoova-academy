using Dapper;
using Inkoova.Academy.Application.Abstractions;

namespace Inkoova.Academy.Infrastructure.Persistence;

public sealed class EmailTemplateStore(IDbConnectionFactory connections) : IEmailTemplateStore
{
    public async Task<StoredEmailTemplate?> GetAsync(Guid identityId, string name, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);

        return await connection.QuerySingleOrDefaultAsync<StoredEmailTemplate>(
            """
            SELECT name, subject, html
            FROM email_template
            WHERE identity_id = @identityId AND name = @name
            """,
            new { identityId, name });
    }

    public async Task<IReadOnlyList<StoredEmailTemplate>> GetAllAsync(Guid identityId, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);

        var rows = await connection.QueryAsync<StoredEmailTemplate>(
            """
            SELECT name, subject, html
            FROM email_template
            WHERE identity_id = @identityId
            ORDER BY name
            """,
            new { identityId });

        return [.. rows];
    }

    public async Task SaveAsync(
        Guid identityId, StoredEmailTemplate template, Guid actor, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);

        await connection.ExecuteAsync(
            """
            INSERT INTO email_template (identity_id, name, subject, html, updated_at, updated_by)
            VALUES (@identityId, @Name, @Subject, @Html, now(), @actor)
            ON CONFLICT (identity_id, name) DO UPDATE SET
                subject = EXCLUDED.subject,
                html = EXCLUDED.html,
                updated_at = EXCLUDED.updated_at,
                updated_by = EXCLUDED.updated_by
            """,
            new { identityId, template.Name, template.Subject, template.Html, actor });
    }

    public async Task RemoveAsync(Guid identityId, string name, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);

        await connection.ExecuteAsync(
            "DELETE FROM email_template WHERE identity_id = @identityId AND name = @name",
            new { identityId, name });
    }
}
