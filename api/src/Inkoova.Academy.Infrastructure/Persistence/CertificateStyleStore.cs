using Dapper;
using Inkoova.Academy.Application.Abstractions;

namespace Inkoova.Academy.Infrastructure.Persistence;

/// <summary>
/// Cabecera, emisor y colores del certificado de cada marca.
///
/// Sin fila, la marca emite el certificado de siempre. Es lo que hace que crear una marca no
/// obligue a diseñar un certificado antes de poder emitir el primero.
/// </summary>
public sealed class CertificateStyleStore(IDbConnectionFactory connections) : ICertificateStyleStore
{
    private const string Columns = """
        heading, subheading, issuer_name AS IssuerName, issuer_note AS IssuerNote,
        primary_color AS PrimaryColor, accent_color AS AccentColor, support_color AS SupportColor
        """;

    public async Task<CertificateStyle> GetAsync(Guid identityId, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);

        var row = await connection.QuerySingleOrDefaultAsync<CertificateStyle>(
            $"SELECT {Columns} FROM certificate_style WHERE identity_id = @identityId",
            new { identityId });

        return row ?? CertificateStyle.Default;
    }

    public async Task SaveAsync(
        Guid identityId, CertificateStyle style, Guid actor, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);

        await connection.ExecuteAsync(
            """
            INSERT INTO certificate_style
                (identity_id, heading, subheading, issuer_name, issuer_note,
                 primary_color, accent_color, support_color, updated_at, updated_by)
            VALUES
                (@identityId, @Heading, @Subheading, @IssuerName, @IssuerNote,
                 @PrimaryColor, @AccentColor, @SupportColor, now(), @actor)
            ON CONFLICT (identity_id) DO UPDATE SET
                heading = EXCLUDED.heading,
                subheading = EXCLUDED.subheading,
                issuer_name = EXCLUDED.issuer_name,
                issuer_note = EXCLUDED.issuer_note,
                primary_color = EXCLUDED.primary_color,
                accent_color = EXCLUDED.accent_color,
                support_color = EXCLUDED.support_color,
                updated_at = EXCLUDED.updated_at,
                updated_by = EXCLUDED.updated_by
            """,
            new
            {
                identityId,
                style.Heading,
                style.Subheading,
                style.IssuerName,
                style.IssuerNote,
                style.PrimaryColor,
                style.AccentColor,
                style.SupportColor,
                actor
            });
    }
}
