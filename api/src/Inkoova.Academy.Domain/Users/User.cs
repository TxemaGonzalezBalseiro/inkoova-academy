using Inkoova.Academy.Domain.Common;

namespace Inkoova.Academy.Domain.Users;

public static class Roles
{
    public const string Student = "student";
    public const string Admin = "admin";
    public const string Affiliate = "affiliate";
}

/// <summary>
/// Application-side user. Credentials live in the Identity tables; this is the identity the
/// rest of the domain refers to, and the name printed on certificates.
/// </summary>
public sealed class User
{
    public Guid Id { get; private set; }
    public string Email { get; private set; }
    public string DisplayName { get; private set; }
    public bool EmailConfirmed { get; private set; }
    public string? StripeCustomerId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Set when the account is erased under GDPR (T-14); personal fields are wiped.</summary>
    public DateTimeOffset? DeletedAt { get; private set; }

    private User(
        Guid id,
        string email,
        string displayName,
        bool emailConfirmed,
        string? stripeCustomerId,
        DateTimeOffset createdAt,
        DateTimeOffset? deletedAt)
    {
        Id = id;
        Email = email;
        DisplayName = displayName;
        EmailConfirmed = emailConfirmed;
        StripeCustomerId = stripeCustomerId;
        CreatedAt = createdAt;
        DeletedAt = deletedAt;
    }

    public static Result<User, Error> Create(Guid id, string email, string displayName, DateTimeOffset createdAt)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@', StringComparison.Ordinal))
        {
            return Error.Validation("user.email_invalid", "El email no es válido.");
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            return Error.Validation("user.display_name_empty", "El nombre no puede estar vacío.");
        }

        return new User(
            id, email.Trim().ToLowerInvariant(), displayName.Trim(),
            emailConfirmed: false, stripeCustomerId: null, createdAt, deletedAt: null);
    }

    public static User Rehydrate(
        Guid id,
        string email,
        string displayName,
        bool emailConfirmed,
        string? stripeCustomerId,
        DateTimeOffset createdAt,
        DateTimeOffset? deletedAt) =>
        new(id, email, displayName, emailConfirmed, stripeCustomerId, createdAt, deletedAt);

    public void ConfirmEmail() => EmailConfirmed = true;

    public void LinkStripeCustomer(string customerId) => StripeCustomerId = customerId;

    public Result<Unit, Error> UpdateProfile(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return Error.Validation("user.display_name_empty", "El nombre no puede estar vacío.");
        }

        DisplayName = displayName.Trim();
        return Unit.Value;
    }

    /// <summary>
    /// GDPR erasure. Keeps the row so that invoices and commissions stay referentially
    /// valid (fiscal retention, T-14) but leaves no personal data behind.
    /// </summary>
    public void Anonymize(DateTimeOffset now)
    {
        Email = $"deleted-{Id:N}@invalid.local";
        DisplayName = "Cuenta eliminada";
        EmailConfirmed = false;
        DeletedAt = now;
    }

    public bool IsActive => DeletedAt is null;
}
