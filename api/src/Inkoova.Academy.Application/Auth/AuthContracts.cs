using Inkoova.Academy.Domain.Common;

namespace Inkoova.Academy.Application.Auth;

public sealed record RegisterRequest(string Email, string Password, string DisplayName, string? QuizAnonymousKey);

public sealed record LoginRequest(string Email, string Password);

public sealed record AuthTokens(string AccessToken, DateTimeOffset AccessTokenExpiresAt, string RefreshToken);

/// <summary>
/// Quién dice el proveedor que es quien acaba de entrar.
///
/// <paramref name="Subject"/> es lo único estable: el email de una cuenta de Google puede
/// cambiar y el de Apple puede ser un alias de reenvío distinto por aplicación. La identidad se
/// ata al <c>sub</c>, nunca al correo.
///
/// <paramref name="EmailVerified"/> decide si se puede enlazar con una cuenta que ya existe en
/// la academia. Un proveedor que no garantiza el correo permitiría a cualquiera darse de alta
/// allí con el correo de otro y quedarse con su cuenta de aquí.
/// </summary>
public sealed record ExternalIdentity(
    string Provider,
    string Subject,
    string? Email,
    string? DisplayName,
    bool EmailVerified);

/// <summary>Una cuenta externa enlazada, para poder enseñarla en la pantalla de la cuenta.</summary>
public sealed record ExternalLoginDto(string Provider, string? Email, DateTimeOffset LinkedAt);

public sealed record MeDto(
    Guid Id,
    string Email,
    string DisplayName,
    bool EmailConfirmed,
    IReadOnlyList<string> Roles,
    bool IsAffiliate);

/// <summary>
/// Account lifecycle. Lives behind a port because the API layer must not know whether
/// credentials are stored locally or, one day, federated (ADR-004).
/// </summary>
public interface IAccountService
{
    Task<Result<Guid, Error>> RegisterAsync(RegisterRequest request, CancellationToken ct);

    Task<Result<AuthTokens, Error>> LoginAsync(LoginRequest request, CancellationToken ct);

    /// <summary>
    /// Entra —o se da de alta— con una cuenta de Google o de Apple. Devuelve los mismos tokens
    /// que un inicio de sesión con contraseña: a partir de aquí la sesión es idéntica, entre por
    /// donde entre el alumno.
    /// </summary>
    Task<Result<AuthTokens, Error>> SignInExternalAsync(ExternalIdentity identity, CancellationToken ct);

    Task<IReadOnlyList<ExternalLoginDto>> GetExternalLoginsAsync(Guid userId, CancellationToken ct);

    /// <summary>
    /// Desenlaza una cuenta externa. Se niega si es la única forma de entrar que le queda al
    /// alumno: dejarle sin contraseña y sin proveedor lo dejaría fuera de su propia cuenta.
    /// </summary>
    Task<Result<Unit, Error>> UnlinkExternalAsync(Guid userId, string provider, CancellationToken ct);

    Task<Result<AuthTokens, Error>> RefreshAsync(string refreshToken, CancellationToken ct);

    Task<Result<Unit, Error>> LogoutAsync(string refreshToken, CancellationToken ct);

    Task<Result<Unit, Error>> ConfirmEmailAsync(string token, CancellationToken ct);

    Task<Result<Unit, Error>> ResendConfirmationAsync(string email, CancellationToken ct);

    /// <summary>Always succeeds from the caller's point of view: an unknown email must not be
    /// distinguishable from a known one, or the endpoint becomes an account enumeration oracle.</summary>
    Task<Result<Unit, Error>> RequestPasswordResetAsync(string email, CancellationToken ct);

    Task<Result<Unit, Error>> ResetPasswordAsync(string token, string newPassword, CancellationToken ct);

    Task<Result<MeDto, Error>> GetMeAsync(Guid userId, CancellationToken ct);

    Task<Result<Unit, Error>> UpdateProfileAsync(Guid userId, string displayName, CancellationToken ct);

    Task<IReadOnlyList<string>> GetRolesAsync(Guid userId, CancellationToken ct);

    Task<Result<Unit, Error>> GrantRoleAsync(Guid userId, string role, CancellationToken ct);

    /// <summary>GDPR erasure (T-14): anonymises the account and revokes every session.</summary>
    Task<Result<Unit, Error>> DeleteAccountAsync(Guid userId, CancellationToken ct);
}
