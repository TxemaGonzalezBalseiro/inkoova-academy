using Inkoova.Academy.Domain.Common;

namespace Inkoova.Academy.Domain.Affiliates;

/// <summary>
/// Click attribution. Last click wins inside a 30-day window; an explicit discount code at
/// checkout beats the cookie (T-16 priority rule).
/// </summary>
public sealed class Referral
{
    public static readonly TimeSpan AttributionWindow = TimeSpan.FromDays(30);

    public Guid Id { get; private set; }

    /// <summary>Opaque first-party cookie value. Not an identifier of a person.</summary>
    public string VisitorId { get; private set; }

    public Guid AffiliateId { get; private set; }
    public DateTimeOffset FirstClickAt { get; private set; }
    public DateTimeOffset LastClickAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>Set when a purchase is attributed to this referral. One conversion per referral.</summary>
    public Guid? ConvertedPurchaseId { get; private set; }

    private Referral(
        Guid id,
        string visitorId,
        Guid affiliateId,
        DateTimeOffset firstClickAt,
        DateTimeOffset lastClickAt,
        DateTimeOffset expiresAt,
        Guid? convertedPurchaseId)
    {
        Id = id;
        VisitorId = visitorId;
        AffiliateId = affiliateId;
        FirstClickAt = firstClickAt;
        LastClickAt = lastClickAt;
        ExpiresAt = expiresAt;
        ConvertedPurchaseId = convertedPurchaseId;
    }

    public static Result<Referral, Error> Track(Guid id, string visitorId, Guid affiliateId, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(visitorId))
        {
            return Error.Validation("referral.visitor_empty", "Falta el identificador de visitante.");
        }

        return new Referral(id, visitorId, affiliateId, now, now, now + AttributionWindow, convertedPurchaseId: null);
    }

    public static Referral Rehydrate(
        Guid id,
        string visitorId,
        Guid affiliateId,
        DateTimeOffset firstClickAt,
        DateTimeOffset lastClickAt,
        DateTimeOffset expiresAt,
        Guid? convertedPurchaseId) =>
        new(id, visitorId, affiliateId, firstClickAt, lastClickAt, expiresAt, convertedPurchaseId);

    /// <summary>Last click wins: a new click extends the window and keeps the first-click date.</summary>
    public void RegisterClick(DateTimeOffset now)
    {
        LastClickAt = now;
        ExpiresAt = now + AttributionWindow;
    }

    public Result<Unit, Error> Convert(Guid purchaseId, DateTimeOffset now)
    {
        if (ConvertedPurchaseId is not null)
        {
            return Error.Conflict("referral.already_converted", "La referencia ya tiene una conversión asociada.");
        }

        if (now > ExpiresAt)
        {
            return Error.Conflict("referral.expired", "La ventana de atribución ha caducado.");
        }

        ConvertedPurchaseId = purchaseId;
        return Unit.Value;
    }

    public bool IsValidAt(DateTimeOffset instant) => ConvertedPurchaseId is null && instant <= ExpiresAt;
}
