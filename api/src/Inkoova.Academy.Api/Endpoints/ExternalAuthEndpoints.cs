using System.Security.Claims;
using Inkoova.Academy.Api.Auth;
using Inkoova.Academy.Api.Common;
using Inkoova.Academy.Application.Auth;
using Inkoova.Academy.Domain.Common;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;

namespace Inkoova.Academy.Api.Endpoints;

/// <summary>
/// Entrar con Google o con Apple.
///
/// El recorrido tiene tres saltos y conviene tenerlos claros:
///
/// 1. <c>GET /api/auth/external/{proveedor}/start</c> — la SPA manda aquí al navegador. Se
///    devuelve un desafío que redirige al proveedor.
/// 2. El proveedor vuelve a <c>/api/auth/external/{proveedor}/signin</c>, que **no es nuestro**:
///    lo atiende el handler de ASP.NET, que valida el estado, canjea el código y deja el
///    resultado en una cookie de un solo uso.
/// 3. <c>GET /api/auth/external/{proveedor}/callback</c> — nuestro. Lee esa cookie, crea o
///    enlaza la cuenta, emite la cookie de refresco y devuelve al alumno a la SPA.
///
/// El token de acceso NO viaja en la URL de vuelta. Se deja la cookie de refresco y la SPA pide
/// un token con ella, igual que al recargar: una URL con un token dentro acaba en el historial,
/// en el Referer y en los registros del servidor.
/// </summary>
public static class ExternalAuthEndpoints
{
    /// <summary>Cookie de un solo uso entre el handler del proveedor y nuestro callback.</summary>
    public const string ExternalScheme = "External";

    public const string AppleScheme = "apple";

    /// <summary>Claim donde se deja lo que el proveedor diga sobre si el correo está verificado.</summary>
    public const string EmailVerifiedClaim = "email_verified";

    private const string RefreshCookie = "ink_rt";
    private const string SessionHintCookie = "ink_session";

    public static void MapExternalAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth/external")
            .WithTags("Autenticación")
            .AllowAnonymous();

        // Qué proveedores puede ofrecer la SPA. Sin esto, la pantalla de acceso tendría que
        // adivinarlo y enseñaría un botón de Apple en un despliegue sin Apple configurado.
        group.MapGet("/providers", (ExternalAuthOptions options) =>
                Results.Ok(new
                {
                    google = options.Google.IsConfigured,
                    apple = options.Apple.IsConfigured
                }))
            .WithSummary("Qué proveedores de identidad están configurados.");

        group.MapGet("/{provider}/start", (
                string provider,
                string? returnUrl,
                ExternalAuthOptions options,
                AcademyUrls urls) =>
            {
                var scheme = SchemeFor(provider, options);
                if (scheme is null)
                {
                    return Error.NotFound("auth.provider_unknown", "Ese proveedor no está disponible.").ToProblem();
                }

                var properties = new AuthenticationProperties
                {
                    RedirectUri = $"/api/auth/external/{provider}/callback",
                };

                // A dónde vuelve el alumno dentro de la SPA. Se valida contra rutas relativas:
                // aceptar una URL absoluta convertiría este endpoint en un redirector abierto,
                // que es un regalo para el phishing.
                properties.Items["returnUrl"] = urls.SafeReturnPath(returnUrl);

                return Results.Challenge(properties, [scheme]);
            })
            .RequireRateLimiting("auth")
            .WithSummary("Empieza el acceso con un proveedor externo.");

        group.MapGet("/{provider}/callback", async (
                string provider,
                HttpContext context,
                IAccountService accounts,
                ExternalAuthOptions options,
                AcademyUrls urls,
                CancellationToken ct) =>
            {
                if (SchemeFor(provider, options) is null)
                {
                    return Error.NotFound("auth.provider_unknown", "Ese proveedor no está disponible.").ToProblem();
                }

                var result = await context.AuthenticateAsync(ExternalScheme);

                // La cookie se consume aquí pase lo que pase. Dejarla viva sería dejar una
                // identidad recién verificada rodando por el navegador sin necesidad.
                await context.SignOutAsync(ExternalScheme);

                if (!result.Succeeded || result.Principal is null)
                {
                    return Results.Redirect(urls.LoginWithError("auth.external_failed"));
                }

                var identity = Read(provider, result.Principal);
                if (identity is null)
                {
                    return Results.Redirect(urls.LoginWithError("auth.external_no_subject"));
                }

                var signIn = await accounts.SignInExternalAsync(identity, ct);

                if (signIn.IsFailure)
                {
                    return Results.Redirect(urls.LoginWithError(signIn.Error.Code));
                }

                SetRefreshCookie(context, signIn.Value.RefreshToken);

                var returnUrl = result.Properties?.Items.TryGetValue("returnUrl", out var stored) == true
                    ? stored
                    : null;

                return Results.Redirect(urls.AfterLogin(returnUrl));
            })
            .WithSummary("Vuelta del proveedor: crea o enlaza la cuenta y deja la sesión abierta.");

        // ── gestión desde la cuenta del alumno ─────────────────────────────────────────

        var me = app.MapGroup("/api/me/external-logins")
            .WithTags("Autenticación")
            .RequireAuthorization();

        me.MapGet("/", async (IAccountService accounts, HttpContext context, CancellationToken ct) =>
                Results.Ok(await accounts.GetExternalLoginsAsync(context.User.RequireUserId(), ct)))
            .WithSummary("Cuentas de Google o Apple enlazadas con esta cuenta.");

        me.MapDelete("/{provider}", async (
                string provider,
                IAccountService accounts,
                HttpContext context,
                CancellationToken ct) =>
                (await accounts.UnlinkExternalAsync(context.User.RequireUserId(), provider, ct)).ToNoContent())
            .WithSummary("Desenlaza un proveedor. Se niega si es la única forma de entrar.");
    }

    private static string? SchemeFor(string provider, ExternalAuthOptions options) =>
        provider.ToLowerInvariant() switch
        {
            "google" when options.Google.IsConfigured => GoogleDefaults.AuthenticationScheme,
            "apple" when options.Apple.IsConfigured => AppleScheme,
            _ => null
        };

    /// <summary>
    /// Traduce los claims del proveedor a lo único que la academia necesita saber.
    ///
    /// Google y Apple no coinciden en casi nada salvo en <c>sub</c>, que es justo el dato del que
    /// cuelga la identidad. El nombre de Apple llega solo la primera vez, y a veces ni eso.
    /// </summary>
    private static ExternalIdentity? Read(string provider, ClaimsPrincipal principal)
    {
        var subject = principal.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? principal.FindFirstValue("sub");

        if (string.IsNullOrWhiteSpace(subject))
        {
            return null;
        }

        var email = principal.FindFirstValue(ClaimTypes.Email) ?? principal.FindFirstValue("email");

        var verified = principal.FindFirstValue(EmailVerifiedClaim);

        // Apple manda "true" como cadena; Google, un booleano JSON que llega también como texto.
        // Ante la duda se toma como NO verificado: equivocarse hacia el lado prudente solo pide
        // una contraseña de más, mientras que el error contrario regala cuentas ajenas.
        var emailVerified = bool.TryParse(verified, out var parsed) && parsed;

        var name = principal.FindFirstValue(ClaimTypes.Name)
                   ?? Join(principal.FindFirstValue(ClaimTypes.GivenName), principal.FindFirstValue(ClaimTypes.Surname));

        return new ExternalIdentity(provider.ToLowerInvariant(), subject, email, name, emailVerified);
    }

    private static string? Join(string? given, string? surname) =>
        string.IsNullOrWhiteSpace(given) && string.IsNullOrWhiteSpace(surname)
            ? null
            : $"{given} {surname}".Trim();

    /// <summary>
    /// La misma cookie, con las mismas condiciones, que el login con contraseña. Si divergieran,
    /// una sesión abierta con Google se comportaría distinto que una abierta con contraseña.
    /// </summary>
    private static void SetRefreshCookie(HttpContext context, string refreshToken)
    {
        context.Response.Cookies.Append(RefreshCookie, refreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = context.Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Path = "/api/auth",
            Expires = DateTimeOffset.UtcNow.AddDays(30)
        });

        // Marca legible por JavaScript que solo dice "hay sesión". La cookie de verdad es
        // HttpOnly y el SPA no puede verla; sin esta marca pediría una renovación en cada carga
        // anónima y llenaría la consola de 401.
        context.Response.Cookies.Append(SessionHintCookie, "1", new CookieOptions
        {
            HttpOnly = false,
            Secure = context.Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            Expires = DateTimeOffset.UtcNow.AddDays(30)
        });
    }
}
