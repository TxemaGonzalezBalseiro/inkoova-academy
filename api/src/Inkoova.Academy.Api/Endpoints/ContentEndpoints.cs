using Inkoova.Academy.Api.Common;
using Inkoova.Academy.Application.Abstractions;
using Inkoova.Academy.Domain.Common;

namespace Inkoova.Academy.Api.Endpoints;

public static class ContentEndpoints
{
    /// <summary>
    /// Cabecera con la que la API le dice a Caddy qué fichero servir. Va con la ruta ya
    /// aprobada y escapada, no con la que pidió el navegador: lo que decide el fichero es el
    /// resultado de la comprobación, nunca la URL de entrada.
    /// </summary>
    private const string ContentFileHeader = "X-Content-File";

    /// <summary>URI original de la petición, tal y como la manda `forward_auth` de Caddy.</summary>
    private const string ForwardedUriHeader = "X-Forwarded-Uri";

    private const string RoutePrefix = "/api/content/";

    public static void MapContentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/content").WithTags("Contenido");

        // Autorización sin bytes: Caddy pregunta aquí por cada fichero del curso y, si la
        // respuesta es 204, lo sirve él desde el volumen (ADR-012). Esta ruta no toca el disco
        // ni la base de datos: valida un HMAC y compara cadenas. Es lo que hace que 10.000
        // alumnos en el player no se traduzcan en 10.000 flujos de fichero por Kestrel.
        //
        // El segmento literal gana al de parámetro en el enrutado, así que esta ruta no la
        // captura /{token}. Un token tampoco puede valer «authorize»: lleva un punto.
        group.MapGet("/authorize", (
                HttpRequest request,
                IContentTokenService tokens) =>
            {
                var forwarded = request.Headers[ForwardedUriHeader].ToString();
                if (string.IsNullOrEmpty(forwarded))
                {
                    return Error.Forbidden("content.no_forwarded_uri", "Petición no reenviada.")
                        .ToContentProblem(request);
                }

                if (!TrySplitForwardedUri(forwarded, out var token, out var path))
                {
                    return Error.Forbidden("content.malformed_uri", "Ruta de contenido inválida.")
                        .ToContentProblem(request);
                }

                var contentRef = tokens.Validate(token);
                if (contentRef.IsFailure)
                {
                    return contentRef.Error.ToContentProblem(request);
                }

                if (!ContentTokenScope.Covers(contentRef.Value, path))
                {
                    return Error.Forbidden("content.out_of_scope", "El token no ampara ese recurso.")
                        .ToContentProblem(request);
                }

                request.HttpContext.Response.Headers[ContentFileHeader] = ToUriPath(path);
                return Results.NoContent();
            })
            .AllowAnonymous()
            .ExcludeFromDescription();

        // El token es la autorización: se emitió solo después de que IAccessPolicy dijera que
        // sí y caduca en 60 segundos (ADR-003). El endpoint es anónimo porque ni el iframe ni
        // las peticiones de assets que salen de dentro llevan cabecera Authorization.
        //
        // Esta ruta corta no sirve nada: redirige a la ruta canónica, que incluye la ruta real
        // del fichero. El motivo es la resolución de rutas relativas del navegador. Un bloque
        // pide `../assets/css/curso.css`; servido en /api/content/{token} el navegador resuelve
        // ese `..` contra /api/ y se sale del token, así que el CSS y el JS del curso daban 404
        // y la lección aparecía sin estilos. Servido en
        // /api/content/{token}/curso/bloques/B0.html, el `..` consume `bloques` y cae dentro.
        //
        // Se mantiene porque el fragmento (#slide-7) sobrevive a la redirección y el player
        // puede seguir construyendo la URL con solo el token.
        group.MapGet("/{token}", (string token, IContentTokenService tokens, HttpRequest request) =>
            {
                var contentRef = tokens.Validate(token);
                if (contentRef.IsFailure)
                {
                    return contentRef.Error.ToContentProblem(request);
                }

                // La query sobrevive a la redirección, igual que el fragmento. El player manda
                // ahí el tema (?theme=dark) para que el documento pinte del color correcto a la
                // primera; perderla en el salto significaría un fogonazo claro en cada lección
                // de quien tiene la academia en oscuro.
                //
                // Y la query va ANTES del ancla. El contentRef puede traer el ancla dentro
                // (…/B0.html#slide-0), y pegarle la query detrás daba #slide-0?theme=dark: para
                // el navegador todo eso es el ancla, y el visor moría en
                // querySelector('#slide-0?theme=dark') dejando la slide sin restaurar.
                var document = ContentTokenScope.DocumentOf(contentRef.Value);
                var fragment = contentRef.Value[document.Length..];

                return Results.Redirect(
                    $"/api/content/{token}/{document}{request.QueryString}{fragment}",
                    permanent: false);
            })
            .AllowAnonymous()
            .WithSummary("Redirige a la ruta canónica del contenido que ampara el token.");

        // La ruta es la del fichero dentro del volumen de contenido, no una ruta relativa a la
        // lección: así el árbol servido tiene la misma forma que el que el HTML importado
        // espera, que es lo único que hace que sus enlaces relativos funcionen sin reescribir
        // el contenido (ADR-008).
        //
        // En producción esta ruta no la pisa nadie: Caddy resuelve el fichero por su cuenta
        // tras preguntar en /authorize. Se conserva porque en desarrollo no hay proxy delante
        // —`dev.ps1 up` levanta la API pelada— y porque es la que ejercitan los tests.
        group.MapGet("/{token}/{**path}", async (
                string token,
                string path,
                IContentTokenService tokens,
                IContentStorage storage,
                HttpRequest request,
                CancellationToken ct) =>
            {
                var contentRef = tokens.Validate(token);
                if (contentRef.IsFailure)
                {
                    return contentRef.Error.ToContentProblem(request);
                }

                if (!ContentTokenScope.Covers(contentRef.Value, path))
                {
                    return Error.Forbidden("content.out_of_scope", "El token no ampara ese recurso.")
                        .ToContentProblem(request);
                }

                // El almacenamiento rechaza cualquier cosa que se salga de la raíz de
                // contenido, así que una ruta manipulada no puede alcanzar el volumen entero.
                var stream = await storage.OpenReadAsync(path, ct);
                if (stream is null)
                {
                    return Error.NotFound("content.not_found", "El recurso no está disponible.")
                        .ToContentProblem(request);
                }

                return Results.Stream(stream, storage.GetContentType(path), enableRangeProcessing: true);
            })
            .AllowAnonymous()
            .WithSummary("Sirve el documento del token, o un asset compartido de su mismo curso.");
    }

    /// <summary>
    /// Parte <c>/api/content/{token}/{ruta}?theme=dark</c> en token y ruta ya decodificada.
    ///
    /// La decodificación es por segmento a propósito. Desescapar la ruta entera de una vez
    /// convertiría un <c>%2F</c> en una barra y crearía segmentos que no estaban en la URL,
    /// que es justo como se cuela un <c>..</c> por debajo de la comprobación de alcance.
    /// </summary>
    private static bool TrySplitForwardedUri(string forwardedUri, out string token, out string path)
    {
        token = string.Empty;
        path = string.Empty;

        var withoutQuery = forwardedUri.AsSpan();
        var query = withoutQuery.IndexOfAny('?', '#');
        if (query >= 0)
        {
            withoutQuery = withoutQuery[..query];
        }

        if (!withoutQuery.StartsWith(RoutePrefix, StringComparison.Ordinal))
        {
            return false;
        }

        var rest = withoutQuery[RoutePrefix.Length..].ToString();
        var separator = rest.IndexOf('/', StringComparison.Ordinal);
        if (separator <= 0 || separator == rest.Length - 1)
        {
            return false;
        }

        token = rest[..separator];

        var segments = rest[(separator + 1)..].Split('/');
        for (var i = 0; i < segments.Length; i++)
        {
            var decoded = Uri.UnescapeDataString(segments[i]);

            // Un segmento que trae barras después de desescapar no es un segmento.
            if (decoded.Contains('/', StringComparison.Ordinal)
                || decoded.Contains('\\', StringComparison.Ordinal))
            {
                return false;
            }

            segments[i] = decoded;
        }

        path = string.Join('/', segments);
        return true;
    }

    /// <summary>
    /// Ruta absoluta y escapada para el <c>rewrite</c> de Caddy. Se escapa segmento a segmento
    /// porque <see cref="Uri.EscapeDataString"/> también escaparía las barras y dejaría el
    /// árbol en un solo nombre de fichero.
    /// </summary>
    private static string ToUriPath(string path) =>
        "/" + string.Join('/', path.Split('/').Select(Uri.EscapeDataString));
}
