using Inkoova.Academy.Api.Common;
using Inkoova.Academy.Application.Abstractions;
using Inkoova.Academy.Application.Billing;
using Inkoova.Academy.Application.Catalog;
using Inkoova.Academy.Domain.Catalog;
using Inkoova.Academy.Domain.Common;
using Inkoova.Academy.Domain.ValueObjects;

namespace Inkoova.Academy.Api.Endpoints;

public static class CatalogEndpoints
{
    public static void MapCatalogEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api").WithTags("Catálogo");

        group.MapGet("/courses", async (GetCatalogHandler handler, CancellationToken ct) =>
                Results.Ok(await handler.HandleAsync(ct)))
            .AllowAnonymous()
            .WithSummary("Cursos publicados y próximos, con métricas para las tarjetas.");

        // Anonymous is allowed on purpose: the syllabus is public, only contentRef is gated.
        group.MapGet("/courses/{slug}", async (
                    string slug,
                    GetCourseDetailHandler handler,
                    HttpContext context,
                    CancellationToken ct) =>
                (await handler.HandleAsync(slug, context.User.UserId(), ct)).ToHttp())
            .AllowAnonymous()
            .WithSummary("Ficha completa del curso. contentRef solo si hay acceso.");

        group.MapGet("/plans", async (GetPlansHandler handler, CancellationToken ct) =>
                Results.Ok(await handler.HandleAsync(ct)))
            .AllowAnonymous()
            .WithSummary("Planes activos con sus beneficios.");

        group.MapGet("/packs", async (
                IPackRepository packs,
                IProductRepository products,
                IAccessPolicy accessPolicy,
                HttpContext context,
                CancellationToken ct) =>
            {
                var userId = context.User.UserId();
                var published = await packs.GetPublishedAsync(ct);
                var result = new List<PackDto>(published.Count);

                foreach (var pack in published)
                {
                    var product = await products.GetByIdAsync(pack.ProductId, ct);
                    var hasAccess = await accessPolicy.CanAccessProductAsync(userId, pack.ProductId, ct);

                    result.Add(ToDto(pack, product, hasAccess, includeFiles: hasAccess));
                }

                return Results.Ok(result);
            })
            .AllowAnonymous()
            .WithSummary("Packs sectoriales publicados.");

        group.MapGet("/packs/{slug}", async (
                string slug,
                IPackRepository packs,
                IProductRepository products,
                IAccessPolicy accessPolicy,
                HttpContext context,
                CancellationToken ct) =>
            {
                var parsed = Slug.Create(slug);
                if (parsed.IsFailure)
                {
                    return parsed.Error.ToProblem();
                }

                var pack = await packs.GetBySlugAsync(parsed.Value, ct);
                if (pack is null || pack.Status != PublicationStatus.Published)
                {
                    return Error.NotFound("pack.not_found", "No existe ese pack.").ToProblem();
                }

                var userId = context.User.UserId();
                var product = await products.GetByIdAsync(pack.ProductId, ct);
                var hasAccess = await accessPolicy.CanAccessProductAsync(userId, pack.ProductId, ct);

                return Results.Ok(ToDto(pack, product, hasAccess, includeFiles: hasAccess));
            })
            .AllowAnonymous()
            .WithSummary("Ficha de un pack: qué incluye, versión y changelog.");

        group.MapPost("/courses/{slug}/waitlist", async (
                string slug,
                WaitlistRequest request,
                ICourseRepository courses,
                IWaitlistRepository waitlist,
                IClock clock,
                CancellationToken ct) =>
            {
                if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains('@', StringComparison.Ordinal))
                {
                    return Error.Validation("waitlist.email_invalid", "El email no es válido.").ToProblem();
                }

                var parsed = Slug.Create(slug);
                if (parsed.IsFailure)
                {
                    return parsed.Error.ToProblem();
                }

                var course = await courses.GetBySlugAsync(parsed.Value, ct);
                if (course is null || course.Status != PublicationStatus.ComingSoon)
                {
                    return Error.NotFound("course.not_coming_soon", "Ese curso no admite avisos.").ToProblem();
                }

                await waitlist.AddAsync(
                    new WaitlistEntry(Guid.CreateVersion7(), course.Id, request.Email.Trim(), clock.UtcNow), ct);

                // A duplicate signup answers the same as a first one: the visitor gets no
                // signal about who else is on the list.
                return Results.Accepted();
            })
            .AllowAnonymous()
            .WithSummary("Avísame cuando salga este curso.");
    }

    private static PackDto ToDto(Pack pack, Product? product, bool hasAccess, bool includeFiles) => new(
        pack.Slug.Value,
        pack.Title,
        pack.Sector,
        pack.Version,
        pack.Changelog,
        pack.RegulatoryCheckDate,
        product?.OneOffPrice?.ToDecimal(),
        product?.OneOffPrice?.Currency,
        hasAccess,
        hasAccess ? "incluido" : "compra",
        includeFiles
            ? pack.Files.Select(f => new PackFileDto(f.Id, f.FileName, f.SizeInBytes, f.Order)).ToList()
            : []);

    public sealed record WaitlistRequest(string Email);
}
