using Inkoova.Academy.Application.Abstractions;
using Inkoova.Academy.Domain.Catalog;

namespace Inkoova.Academy.Application.Catalog;

/// <summary>
/// Public catalogue. Cached because it is the most requested endpoint on the site and its
/// content only changes when an admin publishes (ADR-005).
/// </summary>
public sealed class GetCatalogHandler(ICourseRepository courses, ICatalogCache cache)
{
    private const string CacheKey = "catalog:courses:v1";

    public async Task<IReadOnlyList<CourseCardDto>> HandleAsync(CancellationToken ct)
    {
        var cached = await cache.GetOrCreateAsync(
            CacheKey,
            async token =>
            {
                var published = await courses.GetPublicCatalogAsync(token);
                return published.Select(ToCard).ToList();
            },
            ct);

        return cached ?? [];
    }

    internal static CourseCardDto ToCard(Course course) => new(
        course.Slug.Value,
        course.Title,
        course.ShortDescription,
        course.CoverImageUrl,
        course.Status.ToString().ToLowerInvariant(),
        course.Level.ToString().ToLowerInvariant(),
        course.IsFeatured,
        course.IsNew,
        course.TotalHours,
        course.TotalLessons,
        course.TotalSections,
        // TODO(T-15): ratings are collected in the closed beta; no invented numbers until then.
        Rating: null,
        RatingCount: null,
        // A course is members-only when it has at least one lesson that is not a free preview.
        MembersOnly: course.AllLessons.Any(l => !l.IsFreePreview));
}
