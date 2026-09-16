using System.Net;
using System.Text;
using Inkoova.Academy.Domain.Common;

namespace Inkoova.Academy.Api.Common;

/// <summary>
/// Los errores de <c>/api/content</c> se ven DENTRO del iframe del player, no en una consola.
/// Devolver `application/problem+json` ahí le enseña al alumno el JSON crudo en mitad de la
/// clase. Estos endpoints responden con una página, y el JSON queda para quien lo pida.
///
/// La página es autónoma —estilos inline, cero peticiones— porque el iframe está en un sandbox
/// sin acceso a nada más, y avisa al player por <c>postMessage</c> para que él decida: renovar
/// el token si solo ha caducado, o enseñar su propio aviso si el problema es de acceso.
/// </summary>
public static class ContentProblem
{
    /// <summary>Versión del aviso que viaja al player. Ver content/PROTOCOL.md.</summary>
    private const int ProtocolVersion = 1;

    public static IResult ToContentProblem(this Error error, HttpRequest request) =>
        PrefersHtml(request) ? Html(error) : error.ToProblem();

    /// <summary>
    /// Un iframe pide el documento con <c>Accept: text/html…</c>; un `fetch` o un test piden
    /// JSON o cualquier cosa. Solo el primero necesita una página.
    /// </summary>
    private static bool PrefersHtml(HttpRequest request) =>
        request.Headers.Accept.Any(value => value?.Contains("text/html", StringComparison.OrdinalIgnoreCase) == true);

    private static IResult Html(Error error)
    {
        var (heading, body, recoverable) = Explain(error);

        var page = $$"""
            <!doctype html>
            <html lang="es">
            <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <title>{{Escape(heading)}}</title>
            <style>
              :root { color-scheme: light dark; }
              body {
                margin: 0; min-height: 100vh; display: grid; place-items: center;
                padding: 2rem; box-sizing: border-box;
                font-family: 'Manrope', -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
                background: #f8fafc; color: #0f172a;
              }
              .card { max-width: 26rem; text-align: center; }
              .mark {
                width: 3rem; height: 3rem; margin: 0 auto 1.25rem;
                border-radius: 999px; background: #eff4ff; color: #1e3a8a;
                display: grid; place-items: center; font-size: 1.5rem; font-weight: 700;
              }
              h1 { font-size: 1.25rem; margin: 0 0 .5rem; }
              p { margin: 0; color: #64748b; line-height: 1.6; }
              button {
                margin-top: 1.5rem; padding: .625rem 1.25rem; border: 0; border-radius: .5rem;
                background: #1e3a8a; color: #fff; font: inherit; font-weight: 600; cursor: pointer;
              }
              button:hover { background: #1d4ed8; }
              @media (prefers-color-scheme: dark) {
                body { background: #0f172a; color: #f8fafc; }
                .mark { background: #1e293b; color: #bfdbfe; }
                p { color: #94a3b8; }
              }
            </style>
            </head>
            <body>
              <div class="card">
                <div class="mark" aria-hidden="true">!</div>
                <h1>{{Escape(heading)}}</h1>
                <p>{{Escape(body)}}</p>
                {{(recoverable ? """<button type="button" id="retry">Reintentar</button>""" : string.Empty)}}
              </div>
            <script>
            (function () {
              var aviso = {
                type: 'inkoova:content-error',
                version: {{ProtocolVersion}},
                code: {{Quote(error.Code)}},
                recoverable: {{(recoverable ? "true" : "false")}}
              };

              // El player decide qué hacer. Si no hay player (alguien abrió la URL suelta) esto
              // no hace nada y la página se explica sola.
              try { parent.postMessage(aviso, '*'); } catch (e) {}

              var boton = document.getElementById('retry');
              if (boton) {
                boton.addEventListener('click', function () {
                  try { parent.postMessage(aviso, '*'); } catch (e) {}
                  location.reload();
                });
              }
            })();
            </script>
            </body>
            </html>
            """;

        return Results.Content(page, "text/html; charset=utf-8", Encoding.UTF8, StatusFor(error.Kind));
    }

    /// <summary>
    /// Traduce el error a algo que un alumno entienda, y dice si vale la pena reintentar.
    ///
    /// Un token caducado es lo normal cuando alguien deja la clase abierta un rato: se
    /// reintenta y ya está. Uno fuera de alcance o mal firmado no se arregla reintentando, y
    /// ofrecer un botón que no puede funcionar es peor que no ofrecer ninguno.
    /// </summary>
    private static (string Heading, string Body, bool Recoverable) Explain(Error error) => error.Code switch
    {
        "content_token.expired" => (
            "La sesión de esta clase ha caducado",
            "Pasa cuando la clase se queda abierta un rato. Recárgala y sigues donde estabas.",
            true),

        "content.not_found" => (
            "Este material no está disponible",
            "El fichero de la clase no se encuentra en el servidor. Avisa a soporte indicando qué clase es.",
            false),

        "content.out_of_scope" => (
            "Esta clase no entra en tu acceso",
            "Vuelve al temario y ábrela desde ahí. Si es de un bloque de pago, necesitas un plan que lo incluya.",
            false),

        _ => (
            "No hemos podido abrir esta clase",
            "Vuelve al temario y ábrela otra vez. Si sigue igual, avísanos desde soporte.",
            false)
    };

    private static int StatusFor(ErrorKind kind) => kind switch
    {
        ErrorKind.NotFound => StatusCodes.Status404NotFound,
        ErrorKind.Unauthorized => StatusCodes.Status401Unauthorized,
        _ => StatusCodes.Status403Forbidden
    };

    private static string Escape(string value) => WebUtility.HtmlEncode(value);

    /// <summary>Literal JS seguro: el código del error acaba dentro de un `<script>`.</summary>
    private static string Quote(string value) =>
        System.Text.Json.JsonSerializer.Serialize(value);
}
