namespace Inkoova.Academy.Domain.Common;

/// <summary>
/// Kind of failure. Maps 1:1 to an HTTP status at the edge, so handlers never
/// pick status codes themselves.
/// </summary>
public enum ErrorKind
{
    Validation,
    NotFound,
    Conflict,
    Forbidden,
    Unauthorized,
    Unexpected
}

/// <summary>
/// A business failure. Carries a stable machine code (used by the SPA and by tests)
/// and a human message in Spanish (shown to the student).
/// </summary>
public sealed record Error(ErrorKind Kind, string Code, string Message)
{
    public static Error Validation(string code, string message) => new(ErrorKind.Validation, code, message);
    public static Error NotFound(string code, string message) => new(ErrorKind.NotFound, code, message);
    public static Error Conflict(string code, string message) => new(ErrorKind.Conflict, code, message);
    public static Error Forbidden(string code, string message) => new(ErrorKind.Forbidden, code, message);
    public static Error Unauthorized(string code, string message) => new(ErrorKind.Unauthorized, code, message);
    public static Error Unexpected(string code, string message) => new(ErrorKind.Unexpected, code, message);

    public override string ToString() => $"{Kind}:{Code} — {Message}";
}
