using Dapper;
using Inkoova.Academy.Application.Abstractions;
using Inkoova.Academy.Domain.Catalog;
using Inkoova.Academy.Domain.ValueObjects;

namespace Inkoova.Academy.Infrastructure.Persistence;

public sealed class ProductRepository(IDbConnectionFactory connections) : IProductRepository, IProductCatalog
{
    private const string SelectColumns =
        "id, type, slug, title, one_off_price_cents, currency, stripe_price_id, created_at";

    public async Task<Product?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        var row = await connection.QuerySingleOrDefaultAsync<ProductRow>(
            $"SELECT {SelectColumns} FROM product WHERE id = @id", new { id });

        return row?.ToDomain();
    }

    public async Task<Product?> GetBySlugAsync(Slug slug, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        var row = await connection.QuerySingleOrDefaultAsync<ProductRow>(
            $"SELECT {SelectColumns} FROM product WHERE slug = @slug", new { slug = slug.Value });

        return row?.ToDomain();
    }

    public async Task<IReadOnlyList<Product>> GetByTypeAsync(ProductType type, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        var rows = await connection.QueryAsync<ProductRow>(
            $"SELECT {SelectColumns} FROM product WHERE type = @type ORDER BY created_at",
            new { type = EnumMapping.ToDb(type) });

        return rows.Select(r => r.ToDomain()).ToList();
    }

    public async Task UpsertAsync(Product product, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        await connection.ExecuteAsync(
            """
            INSERT INTO product (id, type, slug, title, one_off_price_cents, currency, stripe_price_id, created_at)
            VALUES (@Id, @Type, @Slug, @Title, @PriceCents, @Currency, @StripePriceId, @CreatedAt)
            ON CONFLICT (id) DO UPDATE SET
                slug = EXCLUDED.slug,
                title = EXCLUDED.title,
                one_off_price_cents = EXCLUDED.one_off_price_cents,
                currency = EXCLUDED.currency,
                stripe_price_id = EXCLUDED.stripe_price_id
            """,
            new
            {
                product.Id,
                Type = EnumMapping.ToDb(product.Type),
                Slug = product.Slug.Value,
                product.Title,
                PriceCents = product.OneOffPrice?.AmountInCents,
                Currency = product.OneOffPrice?.Currency ?? "EUR",
                product.StripePriceId,
                product.CreatedAt
            });
    }

    public Task<Product?> ResolveAsync(Slug slug, CancellationToken ct) => GetBySlugAsync(slug, ct);

    public Task<IReadOnlyList<Product>> GetAllPackProductsAsync(CancellationToken ct) =>
        GetByTypeAsync(ProductType.Pack, ct);

    /// <summary>
    /// Only products of published courses. A draft course must not become accessible just
    /// because someone holds a subscription.
    /// </summary>
    public async Task<IReadOnlyList<Product>> GetAllCourseProductsAsync(CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        var rows = await connection.QueryAsync<ProductRow>(
            $"""
             SELECT {string.Join(", ", SelectColumns.Split(", ").Select(c => "p." + c))}
             FROM product p
             JOIN course c ON c.product_id = p.id
             WHERE p.type = 'course' AND c.status = 'published'
             ORDER BY p.created_at
             """);

        return rows.Select(r => r.ToDomain()).ToList();
    }

    private sealed record ProductRow(
        Guid Id,
        string Type,
        string Slug,
        string Title,
        long? OneOffPriceCents,
        string Currency,
        string? StripePriceId,
        DateTimeOffset CreatedAt)
    {
        public Product ToDomain() => Product.Rehydrate(
            Id,
            EnumMapping.FromDb<ProductType>(Type),
            Domain.ValueObjects.Slug.Create(Slug).Value,
            Title,
            OneOffPriceCents is { } cents ? Money.Create(cents, Currency).Value : null,
            StripePriceId,
            CreatedAt);
    }
}
