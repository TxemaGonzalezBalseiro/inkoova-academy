using System.Security.Claims;
using Inkoova.Academy.Domain.Common;

namespace Inkoova.Academy.Api.Common;

/// <summary>
/// Translates a domain <see cref="Result{TValue,TError}"/> into an HTTP response. Status
/// codes are decided here and only here, so no endpoint has to remember whether a given
/// failure is a 404 or a 409 (CLAUDE.md convention 3).
/// </summary>
public static class ResultExtensions
{
    public static IResult ToHttp<T>(this Result<T, Error> result) =>
        result.Match(Results.Ok, ToProblem);

    public static IResult ToHttp<T>(this Result<T, Error> result, Func<T, IResult> onSuccess) =>
        result.Match(onSuccess, ToProblem);

    public static IResult ToNoContent(this Result<Unit, Error> result) =>
        result.Match(_ => Results.NoContent(), ToProblem);

    public static IResult ToProblem(this Error error) => Results.Problem(
        title: TitleFor(error.Kind),
        detail: error.Message,
        statusCode: StatusFor(error.Kind),
        extensions: new Dictionary<string, object?> { ["code"] = error.Code });

    private static int StatusFor(ErrorKind kind) => kind switch
    {
        ErrorKind.Validation => StatusCodes.Status400BadRequest,
        ErrorKind.Unauthorized => StatusCodes.Status401Unauthorized,
        ErrorKind.Forbidden => StatusCodes.Status403Forbidden,
        ErrorKind.NotFound => StatusCodes.Status404NotFound,
        ErrorKind.Conflict => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status500InternalServerError
    };

    private static string TitleFor(ErrorKind kind) => kind switch
    {
        ErrorKind.Validation => "Petición no válida",
        ErrorKind.Unauthorized => "No autenticado",
        ErrorKind.Forbidden => "Sin acceso",
        ErrorKind.NotFound => "No encontrado",
        ErrorKind.Conflict => "Conflicto",
        _ => "Error inesperado"
    };
}

public static class ClaimsPrincipalExtensions
{
    /// <summary>Authenticated user id, or null for an anonymous caller.</summary>
    public static Guid? UserId(this ClaimsPrincipal principal)
    {
        var raw = principal.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? principal.FindFirstValue("sub");

        return Guid.TryParse(raw, out var id) ? id : null;
    }

    /// <summary>
    /// User id of a caller the endpoint already required to be authenticated. Throwing here
    /// would mean the authorization policy was missing, which is a bug, not a request error.
    /// </summary>
    public static Guid RequireUserId(this ClaimsPrincipal principal) =>
        principal.UserId() ?? throw new InvalidOperationException(
            "Endpoint autenticado sin identificador de usuario en el token.");
}
