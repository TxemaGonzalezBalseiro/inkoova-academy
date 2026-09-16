using Inkoova.Academy.Application.Abstractions;
using Inkoova.Academy.Application.Auth;
using Inkoova.Academy.Application.Learning;
using Inkoova.Academy.Domain.Common;
using Inkoova.Academy.Domain.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Inkoova.Academy.Infrastructure.Identity;

public sealed record AccountOptions
{
    /// <summary>Base of the SPA, used to build the links in confirmation and reset emails.</summary>
    public required string PublicBaseUrl { get; init; }

    public int MinimumPasswordLength { get; init; } = 10;

    public int MaxFailedAccessAttempts { get; init; } = 8;

    public TimeSpan LockoutDuration { get; init; } = TimeSpan.FromMinutes(15);

    public TimeSpan EmailConfirmationLifetime { get; init; } = TimeSpan.FromDays(3);

    public TimeSpan PasswordResetLifetime { get; init; } = TimeSpan.FromHours(2);

    /// <summary>
    /// Whether an unconfirmed account may sign in. False in production; true in development so
    /// a local run does not need working SMTP.
    /// </summary>
    public bool RequireConfirmedEmail { get; init; } = true;

    /// <summary>
    /// Administradores globales, por email. Son de configuración y no de base de datos a
    /// propósito: quien manda en la plataforma no debe poder cambiarse desde la propia
    /// plataforma, ni perderse al restaurar una copia. Reciben el rol de administrador al
    /// registrarse y en cada arranque, así que sobreviven a un borrado de la base.
    /// </summary>
    public IReadOnlyList<string> OwnerEmails { get; init; } = [];

    public bool IsOwner(string email) =>
        OwnerEmails.Any(owner => string.Equals(owner.Trim(), email, StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// Registration, sign-in and the account flows around them (T-03).
/// Uses Identity's <see cref="PasswordHasher{TUser}"/> for credential hashing and keeps the
/// rest local: the stores Identity would need are a large surface for features this platform
/// does not use, and the flows here are few and security-critical enough to be explicit.
/// </summary>
public sealed class AccountService(
    IdentityStore identity,
    IUserRepository users,
    IAffiliateRepository affiliates,
    JwtTokenService tokens,
    IEmailSender email,
    IEmailTemplateRenderer templates,
    ClaimQuizAttemptsHandler claimAttempts,
    IClock clock,
    AccountOptions options,
    ILogger<AccountService> logger) : IAccountService
{
    // PasswordHasher requires a reference type for its user parameter but never reads it:
// the default implementation is PBKDF2 over the password alone. This marker keeps the
// call sites honest about that.
private sealed class HashSubject;

private static readonly HashSubject Subject = new();

private readonly PasswordHasher<HashSubject> _hasher = new();

    public async Task<Result<Guid, Error>> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        var validation = ValidatePassword(request.Password);
        if (validation.IsFailure)
        {
            return validation.Error;
        }

        var userId = Guid.CreateVersion7();
        var appUser = User.Create(userId, request.Email, request.DisplayName, clock.UtcNow);
        if (appUser.IsFailure)
        {
            return appUser.Error;
        }

        var row = new IdentityUserRow(
            userId,
            IdentityStore.Normalize(request.Email),
            request.Email.Trim(),
            EmailConfirmed: false,
            _hasher.HashPassword(Subject, request.Password),
            SecurityStamp: Guid.CreateVersion7().ToString("N"),
            ConcurrencyStamp: Guid.CreateVersion7().ToString("N"),
            LockoutEnd: null,
            LockoutEnabled: true,
            AccessFailedCount: 0,
            clock.UtcNow);

        if (!await identity.TryCreateAsync(row, ct))
        {
            return Error.Conflict("account.email_taken", "Ya existe una cuenta con ese email.");
        }

        await users.UpsertAsync(appUser.Value, ct);
        await identity.AddRoleAsync(userId, Roles.Student, ct);

        // Un administrador global lo es desde el primer segundo. Si hubiera que concederle el
        // rol después haría falta ya un administrador, y tras un borrado de la base no hay
        // ninguno: la plataforma se quedaría sin nadie que pueda entrar en el panel.
        if (options.IsOwner(request.Email))
        {
            await identity.AddRoleAsync(userId, Roles.Admin, ct);
            logger.LogInformation("{Email} está en la lista de administradores globales.", request.Email);
        }

        // An admission attempt taken before signing up belongs to this account (T-08).
        if (!string.IsNullOrWhiteSpace(request.QuizAnonymousKey))
        {
            await claimAttempts.HandleAsync(userId, request.QuizAnonymousKey, ct);
        }

        await SendConfirmationEmailAsync(userId, request.Email, request.DisplayName, ct);

        return userId;
    }

    public async Task<Result<AuthTokens, Error>> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var row = await identity.FindByEmailAsync(request.Email, ct);

        if (row is null)
        {
            // Verify against a dummy hash anyway: returning immediately would make the
            // response time reveal whether the email exists.
            _hasher.VerifyHashedPassword(Subject, DummyHash, request.Password);
            return InvalidCredentials;
        }

        if (row.LockoutEnd is { } until && until > clock.UtcNow)
        {
            return Error.Forbidden(
                "account.locked",
                "Demasiados intentos fallidos. Inténtalo de nuevo en unos minutos.");
        }

        if (row.PasswordHash is null)
        {
            return InvalidCredentials;
        }

        var verification = _hasher.VerifyHashedPassword(Subject, row.PasswordHash, request.Password);

        if (verification == PasswordVerificationResult.Failed)
        {
            await RegisterFailedAttemptAsync(row, ct);
            return InvalidCredentials;
        }

        var updated = row with { AccessFailedCount = 0, LockoutEnd = null };

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            // Identity bumped its iteration count; take the opportunity to upgrade the hash.
            updated = updated with { PasswordHash = _hasher.HashPassword(Subject, request.Password) };
        }

        await identity.UpdateAsync(updated, ct);

        if (options.RequireConfirmedEmail && !row.EmailConfirmed)
        {
            return Error.Forbidden(
                "account.email_not_confirmed",
                "Confirma tu email antes de entrar. Te hemos reenviado el enlace.");
        }

        return await IssueTokensAsync(updated, familyId: Guid.CreateVersion7(), ct);
    }

    /// <summary>
    /// Entrada con Google o con Apple. Tres caminos, en este orden y no en otro:
    ///
    /// 1. **El enlace ya existe** → es esa persona, se le dan tokens. Ni se mira el correo.
    /// 2. **No hay enlace pero sí una cuenta con ese correo** → se enlaza, *pero solo si el
    ///    proveedor certifica el correo*. Sin esa comprobación, cualquiera podría abrir una
    ///    cuenta en un proveedor laxo poniendo el correo de otro y quedarse con su cuenta de la
    ///    academia, sus cursos y sus certificados. Es la parte de todo esto donde un atajo se
    ///    paga caro.
    /// 3. **No hay nada** → alta. Sin contraseña: se entra por el proveedor, y quien quiera una
    ///    contraseña la pide por «he olvidado la contraseña», que ya existe.
    /// </summary>
    public async Task<Result<AuthTokens, Error>> SignInExternalAsync(
        ExternalIdentity external, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var provider = external.Provider.Trim().ToLowerInvariant();

        // 1. Enlace conocido.
        var linked = await identity.FindByExternalLoginAsync(provider, external.Subject, ct);
        if (linked is not null)
        {
            await identity.LinkExternalLoginAsync(provider, external.Subject, linked.Id, external.Email, now, ct);
            return await IssueTokensAsync(linked, Guid.CreateVersion7(), ct);
        }

        if (string.IsNullOrWhiteSpace(external.Email))
        {
            // Sin correo no se puede ni enlazar ni dar de alta: toda la academia identifica al
            // alumno por su correo (facturas, certificados, avisos).
            return Error.Validation(
                "auth.external_no_email",
                "Tu proveedor no ha compartido un correo y sin él no se puede crear la cuenta.");
        }

        // 2. Cuenta existente con ese correo.
        var existing = await identity.FindByEmailAsync(external.Email, ct);
        if (existing is not null)
        {
            if (!external.EmailVerified)
            {
                return Error.Forbidden(
                    "auth.external_email_unverified",
                    "Ya hay una cuenta con ese correo y tu proveedor no lo ha verificado. " +
                    "Entra con tu contraseña y enlaza la cuenta desde tu perfil.");
            }

            await identity.LinkExternalLoginAsync(provider, external.Subject, existing.Id, external.Email, now, ct);

            // El proveedor acaba de certificar el correo: si la cuenta estaba pendiente de
            // confirmar, ya no lo está. Obligar a confirmar por email a quien ha entrado con
            // Google usando ese mismo correo es pedir dos veces la misma prueba.
            if (!existing.EmailConfirmed && external.EmailVerified)
            {
                await identity.UpdateAsync(existing with { EmailConfirmed = true }, ct);
                existing = existing with { EmailConfirmed = true };
            }

            return await IssueTokensAsync(existing, Guid.CreateVersion7(), ct);
        }

        // 3. Alta.
        var userId = Guid.CreateVersion7();
        var displayName = string.IsNullOrWhiteSpace(external.DisplayName)
            // Apple solo manda el nombre la PRIMERA vez que se autoriza la aplicación, y a veces
            // ni eso. La parte local del correo es un nombre provisional razonable, y el alumno
            // lo cambia en su cuenta.
            ? external.Email.Split('@')[0]
            : external.DisplayName.Trim();

        var appUser = User.Create(userId, external.Email, displayName, now);
        if (appUser.IsFailure)
        {
            return appUser.Error;
        }

        var row = new IdentityUserRow(
            userId,
            IdentityStore.Normalize(external.Email),
            external.Email.Trim(),
            // Confirmado solo si el proveedor lo certifica. Si no, la cuenta nace sin confirmar
            // y sigue el mismo camino que cualquier otra.
            EmailConfirmed: external.EmailVerified,
            // Sin contraseña. La columna admite NULL y LoginAsync ya rechaza ese caso, así que
            // una cuenta creada por aquí no se puede tomar adivinando una contraseña vacía.
            PasswordHash: null,
            SecurityStamp: Guid.CreateVersion7().ToString("N"),
            ConcurrencyStamp: Guid.CreateVersion7().ToString("N"),
            LockoutEnd: null,
            LockoutEnabled: true,
            AccessFailedCount: 0,
            now);

        if (!await identity.TryCreateAsync(row, ct))
        {
            // Carrera: alguien creó la cuenta entre la comprobación y el alta.
            return Error.Conflict("account.email_taken", "Ya existe una cuenta con ese email.");
        }

        await users.UpsertAsync(appUser.Value, ct);
        await identity.AddRoleAsync(userId, Roles.Student, ct);

        if (options.IsOwner(external.Email))
        {
            await identity.AddRoleAsync(userId, Roles.Admin, ct);
            logger.LogInformation("{Email} está en la lista de administradores globales.", external.Email);
        }

        await identity.LinkExternalLoginAsync(provider, external.Subject, userId, external.Email, now, ct);

        return await IssueTokensAsync(row, Guid.CreateVersion7(), ct);
    }

    public async Task<IReadOnlyList<ExternalLoginDto>> GetExternalLoginsAsync(Guid userId, CancellationToken ct)
    {
        var rows = await identity.GetExternalLoginsAsync(userId, ct);

        return [.. rows.Select(r => new ExternalLoginDto(r.Provider, r.Email, r.LinkedAt))];
    }

    public async Task<Result<Unit, Error>> UnlinkExternalAsync(
        Guid userId, string provider, CancellationToken ct)
    {
        var row = await identity.FindByIdAsync(userId, ct);
        if (row is null)
        {
            return Error.NotFound("user.not_found", "No existe ese usuario.");
        }

        var links = await identity.GetExternalLoginsAsync(userId, ct);

        // Quitar la última puerta con la que se puede entrar deja al alumno fuera de su propia
        // cuenta, con sus cursos comprados dentro. Se niega y se dice cómo salir del paso.
        if (row.PasswordHash is null && links.Count <= 1)
        {
            return Error.Conflict(
                "auth.external_last_login",
                "Es tu única forma de entrar. Pon primero una contraseña desde «he olvidado la contraseña».");
        }

        await identity.RemoveExternalLoginAsync(userId, provider.Trim().ToLowerInvariant(), ct);

        return Unit.Value;
    }

    public async Task<Result<AuthTokens, Error>> RefreshAsync(string refreshToken, CancellationToken ct)
    {
        var hash = JwtTokenService.Hash(refreshToken);
        var stored = await identity.FindRefreshTokenAsync(hash, ct);

        if (stored is null)
        {
            return Error.Unauthorized("auth.invalid_refresh", "Sesión no válida. Vuelve a iniciar sesión.");
        }

        if (stored.RevokedAt is not null)
        {
            // A revoked token being presented means the chain leaked (T-03 security note).
            logger.LogWarning("Refresh token reuse detected for family {FamilyId}.", stored.FamilyId);
            await identity.RevokeFamilyAsync(stored.FamilyId, clock.UtcNow, ct);
            return Error.Unauthorized("auth.refresh_reused", "Sesión no válida. Vuelve a iniciar sesión.");
        }

        if (stored.ExpiresAt <= clock.UtcNow)
        {
            return Error.Unauthorized("auth.refresh_expired", "Sesión caducada. Vuelve a iniciar sesión.");
        }

        var row = await identity.FindByIdAsync(stored.UserId, ct);
        if (row is null)
        {
            return Error.Unauthorized("auth.invalid_refresh", "Sesión no válida. Vuelve a iniciar sesión.");
        }

        await identity.RevokeRefreshTokenAsync(stored.Id, clock.UtcNow, ct);

        return await IssueTokensAsync(row, stored.FamilyId, ct);
    }

    public async Task<Result<Unit, Error>> LogoutAsync(string refreshToken, CancellationToken ct)
    {
        var stored = await identity.FindRefreshTokenAsync(JwtTokenService.Hash(refreshToken), ct);
        if (stored is not null)
        {
            await identity.RevokeFamilyAsync(stored.FamilyId, clock.UtcNow, ct);
        }

        return Unit.Value;
    }

    public async Task<Result<Unit, Error>> ConfirmEmailAsync(string token, CancellationToken ct)
    {
        var userId = await identity.ConsumeTokenAsync(
            JwtTokenService.Hash(token), TokenPurposes.EmailConfirmation, clock.UtcNow, ct);

        if (userId is not { } id)
        {
            return Error.Validation("account.invalid_token", "El enlace de confirmación no es válido o ha caducado.");
        }

        var row = await identity.FindByIdAsync(id, ct);
        if (row is null)
        {
            return Error.NotFound("user.not_found", "No existe esa cuenta.");
        }

        await identity.UpdateAsync(row with { EmailConfirmed = true }, ct);

        var appUser = await users.GetByIdAsync(id, ct);
        if (appUser is not null)
        {
            appUser.ConfirmEmail();
            await users.UpsertAsync(appUser, ct);
        }

        return Unit.Value;
    }

    public async Task<Result<Unit, Error>> ResendConfirmationAsync(string email, CancellationToken ct)
    {
        var row = await identity.FindByEmailAsync(email, ct);
        if (row is { EmailConfirmed: false })
        {
            var appUser = await users.GetByIdAsync(row.Id, ct);
            await SendConfirmationEmailAsync(row.Id, row.Email, appUser?.DisplayName ?? row.Email, ct);
        }

        // Same answer either way: this endpoint must not confirm whether an email is registered.
        return Unit.Value;
    }

    public async Task<Result<Unit, Error>> RequestPasswordResetAsync(string email, CancellationToken ct)
    {
        var row = await identity.FindByEmailAsync(email, ct);
        if (row is null)
        {
            return Unit.Value;
        }

        var token = JwtTokenService.GenerateRefreshToken();
        await identity.StoreTokenAsync(
            Guid.CreateVersion7(), row.Id, TokenPurposes.PasswordReset,
            JwtTokenService.Hash(token), clock.UtcNow + options.PasswordResetLifetime, ct);

        var appUser = await users.GetByIdAsync(row.Id, ct);

        await SendTemplateAsync(row.Email, "password-reset", new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["nombre"] = appUser?.DisplayName ?? row.Email,
            ["enlace"] = $"{options.PublicBaseUrl.TrimEnd('/')}/restablecer?token={Uri.EscapeDataString(token)}",
            ["horas"] = ((int)options.PasswordResetLifetime.TotalHours).ToString(
                System.Globalization.CultureInfo.InvariantCulture)
        }, ct);

        return Unit.Value;
    }

    public async Task<Result<Unit, Error>> ResetPasswordAsync(string token, string newPassword, CancellationToken ct)
    {
        var validation = ValidatePassword(newPassword);
        if (validation.IsFailure)
        {
            return validation.Error;
        }

        var userId = await identity.ConsumeTokenAsync(
            JwtTokenService.Hash(token), TokenPurposes.PasswordReset, clock.UtcNow, ct);

        if (userId is not { } id)
        {
            return Error.Validation("account.invalid_token", "El enlace de recuperación no es válido o ha caducado.");
        }

        var row = await identity.FindByIdAsync(id, ct);
        if (row is null)
        {
            return Error.NotFound("user.not_found", "No existe esa cuenta.");
        }

        await identity.UpdateAsync(row with
        {
            PasswordHash = _hasher.HashPassword(Subject, newPassword),
            // A new stamp invalidates access tokens issued before the reset.
            SecurityStamp = Guid.CreateVersion7().ToString("N"),
            AccessFailedCount = 0,
            LockoutEnd = null
        }, ct);

        await identity.RevokeAllForUserAsync(id, clock.UtcNow, ct);

        return Unit.Value;
    }

    public async Task<Result<MeDto, Error>> GetMeAsync(Guid userId, CancellationToken ct)
    {
        var user = await users.GetByIdAsync(userId, ct);
        if (user is null)
        {
            return Error.NotFound("user.not_found", "No existe esa cuenta.");
        }

        var roles = await identity.GetRolesAsync(userId, ct);
        var affiliate = await affiliates.GetByUserIdAsync(userId, ct);

        return new MeDto(user.Id, user.Email, user.DisplayName, user.EmailConfirmed, roles, affiliate is not null);
    }

    public async Task<Result<Unit, Error>> UpdateProfileAsync(Guid userId, string displayName, CancellationToken ct)
    {
        var user = await users.GetByIdAsync(userId, ct);
        if (user is null)
        {
            return Error.NotFound("user.not_found", "No existe esa cuenta.");
        }

        var updated = user.UpdateProfile(displayName);
        if (updated.IsFailure)
        {
            return updated.Error;
        }

        await users.UpsertAsync(user, ct);
        return Unit.Value;
    }

    public Task<IReadOnlyList<string>> GetRolesAsync(Guid userId, CancellationToken ct) =>
        identity.GetRolesAsync(userId, ct);

    public async Task<Result<Unit, Error>> GrantRoleAsync(Guid userId, string role, CancellationToken ct)
    {
        if (role is not (Roles.Student or Roles.Admin or Roles.Affiliate))
        {
            return Error.Validation("account.unknown_role", $"El rol '{role}' no existe.");
        }

        await identity.AddRoleAsync(userId, role, ct);
        return Unit.Value;
    }

    public async Task<Result<Unit, Error>> DeleteAccountAsync(Guid userId, CancellationToken ct)
    {
        var user = await users.GetByIdAsync(userId, ct);
        if (user is null)
        {
            return Error.NotFound("user.not_found", "No existe esa cuenta.");
        }

        user.Anonymize(clock.UtcNow);
        await users.UpsertAsync(user, ct);

        var row = await identity.FindByIdAsync(userId, ct);
        if (row is not null)
        {
            // Credentials are destroyed; the row stays so invoices keep a valid foreign key
            // for the retention the tax rules require (T-14).
            await identity.UpdateAsync(row with
            {
                Email = user.Email,
                NormalizedEmail = IdentityStore.Normalize(user.Email),
                PasswordHash = null,
                EmailConfirmed = false,
                SecurityStamp = Guid.CreateVersion7().ToString("N")
            }, ct);
        }

        await identity.RevokeAllForUserAsync(userId, clock.UtcNow, ct);
        return Unit.Value;
    }

    // ── helpers ────────────────────────────────────────────────────────────────────────

    private static readonly Error InvalidCredentials =
        Error.Unauthorized("auth.invalid_credentials", "Email o contraseña incorrectos.");

    /// <summary>Hash of a throwaway password, used to keep failed logins constant-time.</summary>
    private static readonly string DummyHash = new PasswordHasher<HashSubject>()
        .HashPassword(Subject, "dummy-password-for-timing-equalisation");

    private Result<Unit, Error> ValidatePassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < options.MinimumPasswordLength)
        {
            return Error.Validation(
                "account.password_too_short",
                $"La contraseña debe tener al menos {options.MinimumPasswordLength} caracteres.");
        }

        if (password.Length > 256)
        {
            // Bound the input: PBKDF2 cost is linear in length and this is an unauthenticated endpoint.
            return Error.Validation("account.password_too_long", "La contraseña es demasiado larga.");
        }

        return Unit.Value;
    }

    private async Task RegisterFailedAttemptAsync(IdentityUserRow row, CancellationToken ct)
    {
        var failed = row.AccessFailedCount + 1;
        var lockout = failed >= options.MaxFailedAccessAttempts
            ? clock.UtcNow + options.LockoutDuration
            : (DateTimeOffset?)null;

        await identity.UpdateAsync(
            row with { AccessFailedCount = lockout is null ? failed : 0, LockoutEnd = lockout }, ct);
    }

    private async Task<AuthTokens> IssueTokensAsync(IdentityUserRow row, Guid familyId, CancellationToken ct)
    {
        var roles = await identity.GetRolesAsync(row.Id, ct);
        var now = clock.UtcNow;
        var (accessToken, expiresAt) = tokens.IssueAccessToken(row.Id, row.Email, roles, row.SecurityStamp, now);

        var refreshToken = JwtTokenService.GenerateRefreshToken();
        await identity.StoreRefreshTokenAsync(
            Guid.CreateVersion7(), row.Id, familyId, JwtTokenService.Hash(refreshToken),
            now + tokens.RefreshLifetime, ct);

        return new AuthTokens(accessToken, expiresAt, refreshToken);
    }

    private async Task SendConfirmationEmailAsync(
        Guid userId,
        string email,
        string displayName,
        CancellationToken ct)
    {
        var token = JwtTokenService.GenerateRefreshToken();

        await identity.StoreTokenAsync(
            Guid.CreateVersion7(), userId, TokenPurposes.EmailConfirmation,
            JwtTokenService.Hash(token), clock.UtcNow + options.EmailConfirmationLifetime, ct);

        await SendTemplateAsync(email, "confirm-email", new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["nombre"] = displayName,
            ["enlace"] = $"{options.PublicBaseUrl.TrimEnd('/')}/confirmar?token={Uri.EscapeDataString(token)}"
        }, ct);
    }

    private async Task SendTemplateAsync(
        string to,
        string template,
        IReadOnlyDictionary<string, string> model,
        CancellationToken ct)
    {
        var rendered = await templates.RenderAsync(template, model, ct);
        if (rendered.IsFailure)
        {
            logger.LogWarning("Email template {Template} failed: {Error}", template, rendered.Error);
            return;
        }

        var sent = await email.SendAsync(
            new EmailMessage(to, rendered.Value.Subject, rendered.Value.Html, rendered.Value.Text), ct);

        if (sent.IsFailure)
        {
            logger.LogWarning("Could not send {Template} to {To}: {Error}", template, to, sent.Error);
        }
    }

    /// <summary>
    /// Concede el rol de administrador a las cuentas de <see cref="AccountOptions.OwnerEmails"/>
    /// que ya existan. Se llama en cada arranque: si alguien pierde el rol —restaurando una
    /// copia, o por error desde el panel— vuelve solo, sin tocar la base a mano.
    /// </summary>
    public async Task<IReadOnlyList<string>> PromoteOwnersAsync(CancellationToken ct)
    {
        var promoted = new List<string>();

        foreach (var email in options.OwnerEmails.Select(value => value.Trim()).Where(value => value.Length > 0))
        {
            var row = await identity.FindByEmailAsync(email, ct);

            if (row is null)
            {
                // Todavía no se ha registrado. RegisterAsync le dará el rol cuando lo haga.
                logger.LogInformation("Administrador global {Email}: aún sin cuenta.", email);
                continue;
            }

            await identity.AddRoleAsync(row.Id, Roles.Admin, ct);
            promoted.Add(email);
        }

        return promoted;
    }

    /// <summary>Seed helper for the bootstrap admin. Not exposed through the port.</summary>
    public async Task<bool> EnsureAdminAsync(string email, string password, string displayName, CancellationToken ct)
    {
        var existing = await identity.FindByEmailAsync(email, ct);

        if (existing is not null)
        {
            await identity.AddRoleAsync(existing.Id, Roles.Admin, ct);
            return false;
        }

        var registered = await RegisterAsync(new RegisterRequest(email, password, displayName, null), ct);
        if (registered.IsFailure)
        {
            return false;
        }

        var row = await identity.FindByIdAsync(registered.Value, ct);
        if (row is not null)
        {
            await identity.UpdateAsync(row with { EmailConfirmed = true }, ct);
        }

        var appUser = await users.GetByIdAsync(registered.Value, ct);
        if (appUser is not null)
        {
            appUser.ConfirmEmail();
            await users.UpsertAsync(appUser, ct);
        }

        await identity.AddRoleAsync(registered.Value, Roles.Admin, ct);
        return true;
    }
}
