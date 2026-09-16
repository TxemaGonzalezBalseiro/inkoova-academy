using Inkoova.Academy.Domain.Common;
using Inkoova.Academy.Domain.ValueObjects;

namespace Inkoova.Academy.Domain.Catalog;

/// <summary>
/// The unit of entitlement. A course, a sector pack and the full program are all products,
/// so <c>IAccessPolicy</c> asks one question instead of one per sales channel (ADR-007).
/// </summary>
public sealed class Product
{
    public Guid Id { get; private set; }
    public ProductType Type { get; private set; }
    public Slug Slug { get; private set; }
    public string Title { get; private set; }

    /// <summary>One-off price. Null when the product is only reachable through a plan.</summary>
    public Money? OneOffPrice { get; private set; }

    /// <summary>Stripe price for the one-off purchase. Null until the seed script creates it.</summary>
    public string? StripePriceId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    private Product(
        Guid id,
        ProductType type,
        Slug slug,
        string title,
        Money? oneOffPrice,
        string? stripePriceId,
        DateTimeOffset createdAt)
    {
        Id = id;
        Type = type;
        Slug = slug;
        Title = title;
        OneOffPrice = oneOffPrice;
        StripePriceId = stripePriceId;
        CreatedAt = createdAt;
    }

    public static Result<Product, Error> Create(
        Guid id,
        ProductType type,
        Slug slug,
        string title,
        Money? oneOffPrice,
        DateTimeOffset createdAt)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return Error.Validation("product.title_empty", "El producto necesita un título.");
        }

        return new Product(id, type, slug, title.Trim(), oneOffPrice, stripePriceId: null, createdAt);
    }

    /// <summary>
    /// Rehydration from the database. Bypasses invariants on purpose: what is already
    /// persisted was valid when it was written, and a failed load must not hide a data bug.
    /// </summary>
    public static Product Rehydrate(
        Guid id,
        ProductType type,
        Slug slug,
        string title,
        Money? oneOffPrice,
        string? stripePriceId,
        DateTimeOffset createdAt) =>
        new(id, type, slug, title, oneOffPrice, stripePriceId, createdAt);

    /// <summary>
    /// Renombra el producto. El slug NO se toca: es la URL pública y la referencia con la que
    /// se reconoce este mismo producto en cada reimportación, así que cambiarlo dejaría la
    /// ficha anterior en 404 y crearía un producto duplicado en el catálogo.
    ///
    /// Hace falta porque el título vive en dos sitios —el curso y su producto— y solo el del
    /// curso se actualizaba al reimportar. El del producto es el que se ve al elegir qué
    /// incluye un plan y al conceder un acceso a mano, así que los dos acababan diciendo
    /// nombres distintos de la misma cosa.
    /// </summary>
    public Result<Unit, Error> Rename(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return Error.Validation("product.title_empty", "El producto necesita un título.");
        }

        Title = title.Trim();
        return Unit.Value;
    }

    public Result<Unit, Error> SetOneOffPrice(Money price)
    {
        if (price.AmountInCents == 0)
        {
            return Error.Validation("product.price_zero", "Un precio de venta única no puede ser 0.");
        }

        OneOffPrice = price;
        return Unit.Value;
    }

    public void LinkStripePrice(string stripePriceId) => StripePriceId = stripePriceId;

    /// <summary>A product without a Stripe price cannot be checked out one-off.</summary>
    public bool IsPurchasableOneOff => OneOffPrice is not null && !string.IsNullOrWhiteSpace(StripePriceId);
}
