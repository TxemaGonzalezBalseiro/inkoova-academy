using Inkoova.Academy.Api.Common;
using Inkoova.Academy.Application.Auth;
using Inkoova.Academy.Domain.Common;

namespace Inkoova.Academy.Api.Endpoints;

public static class AuthEndpoints
{
    /// <summary>
    /// The refresh token never reaches JavaScript: it travels in an HttpOnly cookie, so an
    /// XSS in the SPA cannot walk away with a long-lived credential. The access token does
    /// go to JS, which is why it lasts 15 minutes (T-03).
    /// </summary>
    private const string RefreshCookie = "ink_rt";

    /// <summary>
    /// Marca legible por JavaScript que dice "hay una cookie de refresco". No contiene el
    /// token ni nada aprovechable: existe para que el SPA no llame a /auth/refresh cuando
    /// no hay sesión que renovar. Sin ella, cada visitante anónimo genera un 401 en cada
    /// carga de página. El servidor no confía en esta marca para nada: la autorización la
    /// sigue dando la cookie HttpOnly.
    /// </summary>
    private const string SessionHintCookie = "ink_session";

    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Autenticación");

        group.MapPost("/register", async (
                RegisterBody body,
                IAccountService accounts,
                HttpContext context,
                CancellationToken ct) =>
            {
                // An admission attempt taken before signing up is claimed by the new account.
                var anonymousKey = body.QuizAnonymousKey ?? VisitorCookie.Read(context);

                var result = await accounts.RegisterAsync(
                    new RegisterRequest(body.Email, body.Password, body.DisplayName, anonymousKey), ct);

                return result.ToHttp(_ => Results.Accepted());
            })
            .AllowAnonymous()
            .RequireRateLimiting("auth")
            .WithSummary("Alta de cuenta. Envía email de confirmación.");

        group.MapPost("/login", async (
                LoginBody body,
                IAccountService accounts,
                HttpContext context,
                CancellationToken ct) =>
            {
                var result = await accounts.LoginAsync(new LoginRequest(body.Email, body.Password), ct);

                return result.ToHttp(tokens =>
                {
                    SetRefreshCookie(context, tokens.RefreshToken);
                    return Results.Ok(new AccessTokenResponse(tokens.AccessToken, tokens.AccessTokenExpiresAt));
                });
            })
            .AllowAnonymous()
            .RequireRateLimiting("auth")
            .WithSummary("Inicio de sesión.");

        group.MapPost("/refresh", async (IAccountService accounts, HttpContext context, CancellationToken ct) =>
            {
                if (!context.Request.Cookies.TryGetValue(RefreshCookie, out var refreshToken))
                {
                    // La marca ha sobrevivido a la cookie real (caducidades distintas, borrado
                    // parcial). Se retira para que el SPA deje de pedir renovaciones inútiles.
                    ClearRefreshCookie(context);
                    return Error.Unauthorized("auth.no_refresh", "No hay sesión que renovar.").ToProblem();
                }

                var result = await accounts.RefreshAsync(refreshToken, ct);

                if (result.IsFailure)
                {
                    // Token caducado o familia revocada por reutilización: la sesión ya no
                    // existe y dejar las cookies puestas produciría un 401 en cada carga.
                    ClearRefreshCookie(context);
                    return result.Error.ToProblem();
                }

                SetRefreshCookie(context, result.Value.RefreshToken);

                return Results.Ok(
                    new AccessTokenResponse(result.Value.AccessToken, result.Value.AccessTokenExpiresAt));
            })
            .AllowAnonymous()
            .WithSummary("Renovación silenciosa del token de acceso.");

        group.MapPost("/logout", async (IAccountService accounts, HttpContext context, CancellationToken ct) =>
            {
                if (context.Request.Cookies.TryGetValue(RefreshCookie, out var refreshToken))
                {
                    await accounts.LogoutAsync(refreshToken, ct);
                }

                ClearRefreshCookie(context);
                return Results.NoContent();
            })
            .AllowAnonymous()
            .WithSummary("Cierra la sesión y revoca la familia de refresh tokens.");

        group.MapPost("/confirm-email", async (TokenBody body, IAccountService accounts, CancellationToken ct) =>
                (await accounts.ConfirmEmailAsync(body.Token, ct)).ToNoContent())
            .AllowAnonymous()
            .RequireRateLimiting("auth")
            .WithSummary("Confirma el email con el token del enlace.");

        group.MapPost("/resend-confirmation", async (
                EmailBody body,
                IAccountService accounts,
                CancellationToken ct) =>
            {
                await accounts.ResendConfirmationAsync(body.Email, ct);
                // Always 202: telling the caller whether the email exists would leak accounts.
                return Results.Accepted();
            })
            .AllowAnonymous()
            .RequireRateLimiting("auth")
            .WithSummary("Reenvía el email de confirmación.");

        group.MapPost("/forgot-password", async (EmailBody body, IAccountService accounts, CancellationToken ct) =>
            {
                await accounts.RequestPasswordResetAsync(body.Email, ct);
                return Results.Accepted();
            })
            .AllowAnonymous()
            .RequireRateLimiting("auth")
            .WithSummary("Solicita el enlace de recuperación de contraseña.");

        group.MapPost("/reset-password", async (
                ResetPasswordBody body,
                IAccountService accounts,
                CancellationToken ct) =>
                (await accounts.ResetPasswordAsync(body.Token, body.NewPassword, ct)).ToNoContent())
            .AllowAnonymous()
            .RequireRateLimiting("auth")
            .WithSummary("Establece una contraseña nueva con el token del enlace.");

        var me = app.MapGroup("/api/me").WithTags("Cuenta").RequireAuthorization();

        me.MapGet("/", async (IAccountService accounts, HttpContext context, CancellationToken ct) =>
                (await accounts.GetMeAsync(context.User.RequireUserId(), ct)).ToHttp())
            .WithSummary("Perfil del usuario autenticado.");

        me.MapPut("/profile", async (
                ProfileBody body,
                IAccountService accounts,
                HttpContext context,
                CancellationToken ct) =>
                (await accounts.UpdateProfileAsync(context.User.RequireUserId(), body.DisplayName, ct)).ToNoContent())
            .WithSummary("Actualiza el nombre visible.");

        me.MapDelete("/", async (IAccountService accounts, HttpContext context, CancellationToken ct) =>
            {
                var result = await accounts.DeleteAccountAsync(context.User.RequireUserId(), ct);
                ClearRefreshCookie(context);
                return result.ToNoContent();
            })
            .WithSummary("Borrado de cuenta (RGPD). Conserva solo lo fiscalmente obligatorio.");
    }

    private static void SetRefreshCookie(HttpContext context, string refreshToken)
    {
        context.Response.Cookies.Append(RefreshCookie, refreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            MaxAge = TimeSpan.FromDays(30),
            IsEssential = true,
            // Scoped to the refresh endpoints: the cookie is not sent with every API call.
            Path = "/api/auth"
        });

        // La marca acompaña siempre a la cookie real y con la misma vida, para que las dos
        // caduquen a la vez y el SPA no intente renovar una sesión que ya no existe.
        context.Response.Cookies.Append(SessionHintCookie, "1", new CookieOptions
        {
            HttpOnly = false,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            MaxAge = TimeSpan.FromDays(30),
            IsEssential = true,
            Path = "/"
        });
    }

    /// <summary>Borra las dos cookies a la vez: dejar la marca sola provocaría un 401 por carga.</summary>
    private static void ClearRefreshCookie(HttpContext context)
    {
        context.Response.Cookies.Delete(RefreshCookie, new CookieOptions { Path = "/api/auth" });
        context.Response.Cookies.Delete(SessionHintCookie, new CookieOptions { Path = "/" });
    }

    public sealed record RegisterBody(string Email, string Password, string DisplayName, string? QuizAnonymousKey);

    public sealed record LoginBody(string Email, string Password);

    public sealed record TokenBody(string Token);

    public sealed record EmailBody(string Email);

    public sealed record ResetPasswordBody(string Token, string NewPassword);

    public sealed record ProfileBody(string DisplayName);

    public sealed record AccessTokenResponse(string AccessToken, DateTimeOffset ExpiresAt);
}
