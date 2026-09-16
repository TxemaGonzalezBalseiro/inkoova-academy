using System.Data;
using System.Text.Json;
using Dapper;
using Inkoova.Academy.Api.Common;
using Inkoova.Academy.Application.Abstractions;
using Inkoova.Academy.Domain.Common;
using Inkoova.Academy.Infrastructure.Persistence;
using Microsoft.Extensions.Caching.Memory;

namespace Inkoova.Academy.Api.Endpoints;

/// <summary>
/// Identidad de la academia: nombre, lema, logo, dominio público y correo de contacto.
///
/// Al contrario que las claves de Stripe o la contraseña del buzón, esto no son secretos: son
/// datos de presentación que el negocio cambia sin desplegar. Por eso viven en la base
/// (<c>academy_setting</c>) y no en el entorno, y por eso hay un endpoint anónimo que los sirve.
///
/// <para>
/// <b>El dominio no manda sobre los enlaces ya emitidos.</b> <c>publicDomain</c> es una etiqueta
/// para enseñar en la web; <c>Academy:PublicBaseUrl</c> sigue siendo lo que construye la URL de
/// verificación que va impresa en el PDF de cada certificado y el enlace de confirmación de
/// correo. Son cosas distintas a propósito: el segundo se pone junto al DNS y al TLS que hacen
/// que ese dominio conteste, y cambiarlo desde un panel dejaría los certificados ya descargados
/// apuntando a un sitio que quizá ya no responde —y un PDF firmado no se puede reescribir—.
/// Aquí solo se avisa cuando ambos discrepan.
/// </para>
/// </summary>
public static class BrandingEndpoints
{
    /// <summary>
    /// Los cinco ajustes. La lista es cerrada: un PUT no puede crear claves nuevas, así que
    /// nadie usa esta tabla como cajón de sastre ni cuela aquí un secreto.
    /// </summary>
    private const string KeyName = "academyName";
    private const string KeyTagline = "academyTagline";
    private const string KeyLogoUrl = "logoUrl";
    private const string KeyPublicDomain = "publicDomain";
    private const string KeySupportEmail = "supportEmail";

    /// <summary>
    /// Una sola entrada de caché para los cinco ajustes: se leen siempre juntos. El TTL es corto
    /// porque el guardado ya invalida; solo cubre el caso de varias instancias de la API, donde
    /// la que no ha guardado no se entera.
    /// </summary>
    private const string CacheKey = "academy:branding";

    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    public static void MapBrandingEndpoints(this IEndpointRouteBuilder app)
    {
        // ── público ────────────────────────────────────────────────────────────────────
        //
        // Lo consume la cabecera de la SPA antes de que nadie inicie sesión, así que es
        // anónimo. Devuelve exactamente lo que se pinta y nada más: ni quién lo cambió, ni
        // cuándo, ni la configuración del despliegue. Si algún día se añade un ajuste que no
        // sea de escaparate, no entra en esta respuesta.
        app.MapGet("/api/branding", async (
                IDbConnectionFactory connections,
                IAcademyIdentityRepository identities,
                IMemoryCache cache,
                IConfiguration configuration,
                HttpContext context,
                CancellationToken ct) =>
            {
                var settings = await LoadAsync(connections, cache, ct);

                // Los datos del titular son públicos POR OBLIGACIÓN: el artículo 10 de la LSSI
                // exige publicarlos, y el aviso legal y el pie los necesitan sin sesión. No es
                // una fuga: es literalmente lo que la ley manda enseñar.
                var identity = await identities.GetDefaultAsync(ct);

                // Cacheable: cambia como mucho unas veces al año y lo pide todo visitante en
                // la primera carga.
                context.Response.Headers.CacheControl = "public, max-age=60";

                return Results.Ok(new
                {
                    settings.AcademyName,
                    settings.AcademyTagline,
                    // Cadena vacía significa "sin logo": el front dibuja su marca de siempre.
                    LogoUrl = NullIfEmpty(settings.LogoUrl),
                    SupportEmail = NullIfEmpty(settings.SupportEmail),
                    // Público por definición —es el dominio desde el que se sirve la web— y
                    // útil para enlaces canónicos. Mientras esté sin poner, el host del
                    // despliegue, que es lo que de verdad responde.
                    PublicDomain = NullIfEmpty(settings.PublicDomain)
                                   ?? HostOf(configuration["Academy:PublicBaseUrl"]),

                    legal = new
                    {
                        legalName = NullIfEmpty(identity.Legal.LegalName),
                        taxId = NullIfEmpty(identity.Legal.TaxId),
                        address = NullIfEmpty(identity.Legal.Address),
                        registryDetails = NullIfEmpty(identity.Legal.RegistryDetails),
                        email = NullIfEmpty(identity.Legal.Email),
                        linkedInUrl = NullIfEmpty(identity.Legal.LinkedInUrl),
                        companyUrl = NullIfEmpty(identity.Legal.CompanyUrl),
                        // Qué falta. El aviso legal lo enseña en vez de callarse un hueco: un
                        // dato identificativo que falta es un incumplimiento, no un detalle.
                        missing = identity.Legal.Missing
                    }
                });
            })
            .AllowAnonymous()
            .WithTags("Identidad")
            .WithSummary("Identidad pública de la academia. Solo datos de presentación.");

        // ── administración ─────────────────────────────────────────────────────────────

        var group = app.MapGroup("/api/admin/branding")
            .WithTags("Identidad")
            .RequireAuthorization("admin");

        group.MapGet("/", async (
                IDbConnectionFactory connections,
                IMemoryCache cache,
                IConfiguration configuration,
                CancellationToken ct) =>
            {
                var settings = await LoadAsync(connections, cache, ct);
                var deployment = await DescribeDeploymentAsync(connections, configuration, settings, ct);

                return Results.Ok(new
                {
                    settings.AcademyName,
                    settings.AcademyTagline,
                    settings.LogoUrl,
                    settings.PublicDomain,
                    settings.SupportEmail,
                    Deployment = deployment
                });
            })
            .WithSummary("Identidad actual y cómo encaja con el dominio del despliegue.");

        group.MapPut("/", async (
                BrandingBody body,
                IDbConnectionFactory connections,
                IMemoryCache cache,
                IConfiguration configuration,
                IAuditLogRepository audit,
                IClock clock,
                HttpContext context,
                CancellationToken ct) =>
            {
                var validated = Validate(body);

                if (!validated.IsSuccess)
                {
                    return validated.Error.ToProblem();
                }

                var settings = validated.Value;
                var previous = await LoadAsync(connections, cache, ct);
                var actor = context.User.RequireUserId();

                await SaveAsync(connections, cache, settings, actor, clock.UtcNow, ct);

                // Se registra qué cambió, no el estado entero: la auditoría se lee para
                // contestar "quién tocó el dominio", y una lista de cinco campos iguales
                // menos uno lo esconde.
                await audit.AppendAsync(
                    new AuditEntry(
                        Guid.CreateVersion7(),
                        actor,
                        "branding.update",
                        "branding",
                        null,
                        JsonSerializer.Serialize(Changes(previous, settings)),
                        clock.UtcNow),
                    ct);

                var deployment = await DescribeDeploymentAsync(connections, configuration, settings, ct);

                return Results.Ok(new
                {
                    settings.AcademyName,
                    settings.AcademyTagline,
                    settings.LogoUrl,
                    settings.PublicDomain,
                    settings.SupportEmail,
                    Deployment = deployment
                });
            })
            .WithSummary("Guarda la identidad. No toca el dominio del despliegue ni los certificados.");
    }

    /// <summary>Lo que llega del panel. Todo opcional: un campo ausente se trata como vacío.</summary>
    public sealed record BrandingBody(
        string? AcademyName,
        string? AcademyTagline,
        string? LogoUrl,
        string? PublicDomain,
        string? SupportEmail);

    /// <summary>Fila cruda de <c>academy_setting</c>.</summary>
    private sealed record SettingRow(string Key, string Value);

    /// <summary>Los cinco ajustes ya validados y normalizados. Nunca null: vacío es "sin poner".</summary>
    private sealed record BrandingSettings(
        string AcademyName,
        string AcademyTagline,
        string LogoUrl,
        string PublicDomain,
        string SupportEmail);

    // ── validación ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Reglas de negocio de la pantalla, en el sitio donde se pueden devolver como
    /// <c>Result</c> en vez de como excepción (CLAUDE.md, convención 3).
    /// </summary>
    private static Result<BrandingSettings, Error> Validate(BrandingBody body)
    {
        var name = (body.AcademyName ?? string.Empty).Trim();

        // El nombre es lo único obligatorio: sale en la cabecera, en los correos y en el
        // certificado, y no hay valor por defecto razonable que no sea inventárselo.
        if (name.Length < 2)
        {
            return Error.Validation("branding.name_required", "El nombre de la academia es obligatorio.");
        }

        if (name.Length > 80)
        {
            return Error.Validation("branding.name_too_long", "El nombre no puede pasar de 80 caracteres.");
        }

        var tagline = (body.AcademyTagline ?? string.Empty).Trim();

        if (tagline.Length > 160)
        {
            return Error.Validation("branding.tagline_too_long", "El lema no puede pasar de 160 caracteres.");
        }

        var logo = (body.LogoUrl ?? string.Empty).Trim();

        if (logo.Length > 0)
        {
            if (logo.Length > 500)
            {
                return Error.Validation("branding.logo_too_long", "La dirección del logo es demasiado larga.");
            }

            // Solo http(s) o una ruta del propio sitio. `javascript:` y `data:` acabarían en el
            // atributo src de una etiqueta que pinta todo el mundo, incluidos los visitantes
            // anónimos: es la única entrada de esta pantalla que llega al navegador de otros.
            var absolute = Uri.TryCreate(logo, UriKind.Absolute, out var uri)
                           && uri.Scheme is "http" or "https";

            var siteRelative = logo.StartsWith('/') && !logo.StartsWith("//", StringComparison.Ordinal);

            if (!absolute && !siteRelative)
            {
                return Error.Validation(
                    "branding.logo_invalid",
                    "El logo debe ser una dirección http(s) o una ruta del propio sitio que empiece por «/».");
            }
        }

        var domain = (body.PublicDomain ?? string.Empty).Trim().ToLowerInvariant();

        if (domain.Length > 0)
        {
            // Un dominio, no una URL: sin esquema, sin ruta y sin puerto. Aceptar
            // "https://x.com/" aquí produciría enlaces con doble esquema en cuanto alguien lo
            // concatene.
            if (!IsHostName(domain))
            {
                return Error.Validation(
                    "branding.domain_invalid",
                    "Escribe solo el dominio, sin «https://» ni barras. Por ejemplo: academy.inkoova.com");
            }
        }

        var email = (body.SupportEmail ?? string.Empty).Trim();

        if (email.Length > 0 && !IsEmail(email))
        {
            return Error.Validation("branding.email_invalid", "El correo de contacto no es una dirección válida.");
        }

        return new BrandingSettings(name, tagline, logo, domain, email);
    }

    /// <summary>
    /// Nombre de host con puntos: etiquetas alfanuméricas separadas por puntos, con guiones
    /// dentro pero no en los extremos, y un TLD de letras. No pretende cubrir el RFC entero,
    /// solo rechazar lo que claramente no es un dominio.
    /// </summary>
    private static bool IsHostName(string value)
    {
        if (value.Length is 0 or > 253 || !value.Contains('.'))
        {
            return false;
        }

        var labels = value.Split('.');

        if (labels.Any(label =>
                label.Length is 0 or > 63
                || label.StartsWith('-')
                || label.EndsWith('-')
                || !label.All(c => char.IsAsciiLetterOrDigit(c) || c == '-')))
        {
            return false;
        }

        return labels[^1].Length >= 2 && labels[^1].All(char.IsAsciiLetter);
    }

    /// <summary>Una arroba, algo a cada lado y un punto a la derecha. Lo demás lo dice el envío.</summary>
    private static bool IsEmail(string value)
    {
        if (value.Length > 254 || value.Any(char.IsWhiteSpace))
        {
            return false;
        }

        var at = value.IndexOf('@', StringComparison.Ordinal);

        return at > 0
               && at == value.LastIndexOf('@')
               && IsHostName(value[(at + 1)..]);
    }

    // ── persistencia ───────────────────────────────────────────────────────────────────

    private static async Task<BrandingSettings> LoadAsync(
        IDbConnectionFactory connections,
        IMemoryCache cache,
        CancellationToken ct)
    {
        if (cache.TryGetValue(CacheKey, out var cached) && cached is BrandingSettings hit)
        {
            return hit;
        }

        using var connection = await connections.OpenAsync(ct);

        // Desde V012 la marca vive en `academy_identity`: esta pantalla edita la marca PRINCIPAL.
        // Mientras estuvo en `academy_setting` había un único juego de valores; ahora hay uno
        // por marca, y dejar los dos sitios vivos significaría que guardar aquí no cambia lo que
        // leen los correos ni la cabecera.
        var row = await connection.QuerySingleOrDefaultAsync<BrandingSettings>(
            """
            SELECT name AS AcademyName, tagline AS AcademyTagline, logo_url AS LogoUrl,
                   public_domain AS PublicDomain, support_email AS SupportEmail
            FROM academy_identity
            ORDER BY is_default DESC, created_at
            LIMIT 1
            """);

        // Sin ninguna marca (base recién creada, migración a medias) se usa la de siempre en vez
        // de dejar la cabecera sin nombre.
        var settings = row ?? new BrandingSettings(
            "Inkoova Academy", string.Empty, string.Empty, string.Empty, string.Empty);

        cache.Set(CacheKey, settings, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = CacheTtl,
            // La caché del proceso tiene límite de tamaño; sin esto, Set lanza.
            Size = 1
        });

        return settings;
    }

    private static async Task SaveAsync(
        IDbConnectionFactory connections,
        IMemoryCache cache,
        BrandingSettings settings,
        Guid actor,
        DateTimeOffset now,
        CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);

        // Una sola sentencia sobre la marca principal: los cinco campos entran juntos, así que
        // ya no hace falta transacción para que no quede una identidad a medias.
        var updated = await connection.ExecuteAsync(
            """
            UPDATE academy_identity SET
                name = @AcademyName,
                tagline = @AcademyTagline,
                logo_url = @LogoUrl,
                public_domain = @PublicDomain,
                support_email = @SupportEmail,
                updated_at = @Now
            WHERE id = (SELECT id FROM academy_identity ORDER BY is_default DESC, created_at LIMIT 1)
            """,
            new
            {
                settings.AcademyName,
                settings.AcademyTagline,
                settings.LogoUrl,
                settings.PublicDomain,
                settings.SupportEmail,
                Now = now
            });

        if (updated == 0)
        {
            throw new InvalidOperationException(
                "No hay ninguna identidad que actualizar. ¿Se ha aplicado la migración V012?");
        }

        // `actor` ya no se escribe en la fila: `academy_identity` no lleva `updated_by`. Quién
        // cambió qué sigue estando en audit_log, que es donde de verdad se consulta.
        _ = actor;

        // Después del commit: si el commit falla, la caché sigue teniendo lo que hay en la base.
        cache.Remove(CacheKey);

        // Y la del lector que usan los correos. Son dos entradas porque guardan tipos distintos,
        // pero salen de la misma tabla: invalidar solo una dejaría los correos con el nombre
        // viejo mientras la web ya enseña el nuevo.
        cache.Remove(Infrastructure.Persistence.AcademyBrandingReader.CacheKey);
    }

    // ── dominio del despliegue ─────────────────────────────────────────────────────────

    /// <summary>
    /// Lo que la pantalla necesita para avisar sin asustar: cuál es el dominio que de verdad
    /// sirve la web, si coincide con el que se ha escrito aquí, y cuántos certificados llevan
    /// ya impresa una URL construida con el primero.
    /// </summary>
    private static async Task<object> DescribeDeploymentAsync(
        IDbConnectionFactory connections,
        IConfiguration configuration,
        BrandingSettings settings,
        CancellationToken ct)
    {
        var baseUrl = configuration["Academy:PublicBaseUrl"];
        var baseHost = HostOf(baseUrl);

        using var connection = await connections.OpenAsync(ct);

        var issued = await connection.ExecuteScalarAsync<long>(
            "SELECT count(*) FROM certificate WHERE revoked_at IS NULL");

        return new
        {
            PublicBaseUrl = baseUrl,
            PublicBaseUrlHost = baseHost,
            // Sin dominio escrito no hay nada que avisar: se hereda el del despliegue.
            Matches = settings.PublicDomain.Length == 0
                      || string.Equals(settings.PublicDomain, baseHost, StringComparison.OrdinalIgnoreCase),
            IssuedCertificates = issued,
            VerificationSample = $"{(baseUrl ?? string.Empty).TrimEnd('/')}/check-certificate/…"
        };
    }

    private static string? HostOf(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host : null;

    // ── auxiliares ─────────────────────────────────────────────────────────────────────

    private static string Read(IReadOnlyDictionary<string, string> values, string key, string fallback) =>
        values.TryGetValue(key, out var value) && value.Length > 0 ? value : fallback;

    private static string? NullIfEmpty(string value) => value.Length == 0 ? null : value;

    /// <summary>Solo los campos que cambian, para que la auditoría se pueda leer de un vistazo.</summary>
    private static Dictionary<string, object> Changes(BrandingSettings before, BrandingSettings after)
    {
        var changes = new Dictionary<string, object>(StringComparer.Ordinal);

        Add(KeyName, before.AcademyName, after.AcademyName);
        Add(KeyTagline, before.AcademyTagline, after.AcademyTagline);
        Add(KeyLogoUrl, before.LogoUrl, after.LogoUrl);
        Add(KeyPublicDomain, before.PublicDomain, after.PublicDomain);
        Add(KeySupportEmail, before.SupportEmail, after.SupportEmail);

        return changes;

        void Add(string key, string from, string to)
        {
            if (!string.Equals(from, to, StringComparison.Ordinal))
            {
                changes[key] = new { from, to };
            }
        }
    }
}
