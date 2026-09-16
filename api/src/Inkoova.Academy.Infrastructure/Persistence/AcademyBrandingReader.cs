using Dapper;
using Inkoova.Academy.Application.Abstractions;
using Microsoft.Extensions.Caching.Memory;

namespace Inkoova.Academy.Infrastructure.Persistence;

/// <summary>
/// Lee <c>academy_setting</c> y lo deja en caché.
///
/// Tiene clave propia porque el endpoint de identidad guarda su propio tipo bajo la suya, y
/// compartirla haría que ninguno de los dos acertara nunca en la caché. Lo que sí es común es la
/// invalidación: guardar desde el panel borra las dos. Si solo se borrara una, cambiar el nombre
/// de la academia lo cambiaría en la web y lo dejaría viejo en los correos durante cinco
/// minutos, que es la clase de incoherencia que nadie relaciona con su causa.
/// </summary>
public sealed class AcademyBrandingReader(IDbConnectionFactory connections, IMemoryCache cache)
    : IAcademyBrandingReader
{
    public const string CacheKey = "academy:branding:reader";

    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    public async Task<AcademyBranding> GetAsync(CancellationToken ct)
    {
        if (cache.TryGetValue<AcademyBranding>(CacheKey, out var cached) && cached is not null)
        {
            return cached;
        }

        using var connection = await connections.OpenAsync(ct);

        // De `academy_identity`, no de `academy_setting`. Desde que hay varias marcas, la
        // principal ES la marca de la academia, y dejar los dos sitios vivos significaría que
        // guardar en uno no cambia lo que lee el otro.
        var row = await connection.QuerySingleOrDefaultAsync<BrandingRow>(
            """
            SELECT name, tagline, logo_url, public_domain, support_email
            FROM academy_identity
            ORDER BY is_default DESC, created_at
            LIMIT 1
            """);

        var branding = row is null
            ? new AcademyBranding("Inkoova Academy", string.Empty, string.Empty, string.Empty, string.Empty)
            : new AcademyBranding(row.Name, row.Tagline, row.LogoUrl, row.PublicDomain, row.SupportEmail);

        // Size = 1 porque el contenedor de caché tiene límite de tamaño: sin él, la entrada se
        // rechaza en silencio y esto consultaría la base en cada correo.
        cache.Set(CacheKey, branding, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = CacheTtl,
            Size = 1
        });

        return branding;
    }

    private sealed record BrandingRow(
        string Name,
        string Tagline,
        string LogoUrl,
        string PublicDomain,
        string SupportEmail);
}
