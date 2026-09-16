using System.Text.Json;
using Dapper;
using Inkoova.Academy.Api.Common;
using Inkoova.Academy.Application.Abstractions;
using Inkoova.Academy.Domain.Common;
using Inkoova.Academy.Infrastructure.Persistence;
using Microsoft.Extensions.Caching.Memory;

namespace Inkoova.Academy.Api.Endpoints;

/// <summary>
/// La página «Sobre mí», editable desde el panel.
///
/// Estaba escrita a mano en la SPA, con el nombre del instructor y su biografía dentro del
/// código: cambiar una línea exigía desplegar. Ahora vive en <c>academy_setting</c>, junto al
/// nombre y el lema de la academia, porque es lo mismo: identidad que el negocio cambia solo.
///
/// El cuerpo se guarda como TEXTO PLANO con un marcado mínimo, nunca como HTML. La SPA lo pinta
/// escapando el contenido; guardar HTML aquí sería dejar que una sesión de administración
/// robada inyectara scripts en una página que ve todo el mundo.
/// </summary>
public static class AboutEndpoints
{
    private const string CacheKey = "academy:about";

    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    /// <summary>Tope del cuerpo. Suficiente para una biografía larga y lejos de un pegado accidental.</summary>
    private const int MaximumBodyLength = 8000;

    public static void MapAboutEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/about", async (
                IDbConnectionFactory connections,
                IMemoryCache cache,
                HttpContext context,
                CancellationToken ct) =>
            {
                var about = await LoadAsync(connections, cache, ct);

                // Un minuto de caché de navegador: la página cambia muy de vez en cuando y así
                // no se pide en cada navegación de la SPA.
                context.Response.Headers.CacheControl = "public, max-age=60";

                return Results.Ok(about);
            })
            .AllowAnonymous()
            .WithTags("Identidad")
            .WithSummary("Contenido de la página «Sobre mí».");

        var group = app.MapGroup("/api/admin/about")
            .WithTags("Identidad")
            .RequireAuthorization("admin");

        group.MapGet("/", async (
                IDbConnectionFactory connections,
                IMemoryCache cache,
                CancellationToken ct) =>
                Results.Ok(await LoadAsync(connections, cache, ct)))
            .WithSummary("Contenido actual, para editarlo.");

        group.MapPut("/", async (
                AboutBody body,
                IDbConnectionFactory connections,
                IMemoryCache cache,
                IAuditLogRepository audit,
                IClock clock,
                HttpContext context,
                CancellationToken ct) =>
            {
                var validated = Validate(body);
                if (validated.IsFailure)
                {
                    return validated.Error.ToProblem();
                }

                await SaveAsync(connections, cache, validated.Value, context.User.RequireUserId(), clock.UtcNow, ct);

                await audit.AppendAsync(
                    new AuditEntry(
                        Guid.CreateVersion7(),
                        context.User.RequireUserId(),
                        "about.update",
                        "academy_setting",
                        "about",
                        // Solo el tamaño, no el texto: la auditoría se lee para saber quién
                        // cambió qué y cuándo, no para guardar una copia de cada versión.
                        JsonSerializer.Serialize(new { bodyLength = validated.Value.Body.Length }),
                        clock.UtcNow),
                    ct);

                return Results.NoContent();
            })
            .WithSummary("Guarda la página «Sobre mí».");
    }

    private sealed record AboutContent(string Name, string Headline, string Body, string PhotoUrl);

    public sealed record AboutBody(string? Name, string? Headline, string? Body, string? PhotoUrl);

    private static Result<AboutContent, Error> Validate(AboutBody body)
    {
        var name = (body.Name ?? string.Empty).Trim();
        var headline = (body.Headline ?? string.Empty).Trim();
        var text = (body.Body ?? string.Empty).Trim();
        var photo = (body.PhotoUrl ?? string.Empty).Trim();

        if (name.Length > 120)
        {
            return Error.Validation("about.name_too_long", "El nombre no puede pasar de 120 caracteres.");
        }

        if (headline.Length > 400)
        {
            return Error.Validation("about.headline_too_long", "La entradilla no puede pasar de 400 caracteres.");
        }

        if (text.Length > MaximumBodyLength)
        {
            return Error.Validation(
                "about.body_too_long",
                $"El texto no puede pasar de {MaximumBodyLength} caracteres.");
        }

        // La foto acaba en un `src` que ve cualquier visitante. Se aceptan solo direcciones
        // http(s) o rutas del propio sitio: `javascript:` y `data:` quedan fuera.
        if (photo.Length > 0
            && !photo.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            && !photo.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !photo.StartsWith('/'))
        {
            return Error.Validation(
                "about.photo_invalid",
                "La foto debe ser una dirección http(s) o una ruta que empiece por /.");
        }

        return new AboutContent(name, headline, text, photo);
    }

    private static async Task<AboutContent> LoadAsync(
        IDbConnectionFactory connections, IMemoryCache cache, CancellationToken ct)
    {
        if (cache.TryGetValue<AboutContent>(CacheKey, out var cached) && cached is not null)
        {
            return cached;
        }

        using var connection = await connections.OpenAsync(ct);
        var rows = await connection.QueryAsync<(string Key, string Value)>(
            "SELECT key, value FROM academy_setting WHERE key LIKE 'about%'");

        var settings = rows.ToDictionary(r => r.Key, r => r.Value, StringComparer.OrdinalIgnoreCase);

        var about = new AboutContent(
            Read(settings, "aboutName"),
            Read(settings, "aboutHeadline"),
            Read(settings, "aboutBody"),
            Read(settings, "aboutPhotoUrl"));

        cache.Set(CacheKey, about, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = CacheTtl,
            // El contenedor tiene límite de tamaño; sin esto la entrada se rechaza en silencio.
            Size = 1
        });

        return about;
    }

    private static async Task SaveAsync(
        IDbConnectionFactory connections,
        IMemoryCache cache,
        AboutContent about,
        Guid actor,
        DateTimeOffset now,
        CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        using var transaction = connection.BeginTransaction();

        var values = new (string Key, string Value)[]
        {
            ("aboutName", about.Name),
            ("aboutHeadline", about.Headline),
            ("aboutBody", about.Body),
            ("aboutPhotoUrl", about.PhotoUrl)
        };

        foreach (var (key, value) in values)
        {
            await connection.ExecuteAsync(
                """
                INSERT INTO academy_setting (key, value, updated_at, updated_by)
                VALUES (@key, @value, @now, @actor)
                ON CONFLICT (key) DO UPDATE SET
                    value = EXCLUDED.value,
                    updated_at = EXCLUDED.updated_at,
                    updated_by = EXCLUDED.updated_by
                """,
                new { key, value, now, actor },
                transaction);
        }

        transaction.Commit();

        // Después del commit: si el commit falla, la caché sigue teniendo lo que hay en la base.
        cache.Remove(CacheKey);
    }

    private static string Read(IReadOnlyDictionary<string, string> settings, string key) =>
        settings.TryGetValue(key, out var value) ? value : string.Empty;
}
