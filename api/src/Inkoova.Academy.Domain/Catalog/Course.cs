using Inkoova.Academy.Domain.Common;
using Inkoova.Academy.Domain.ValueObjects;

namespace Inkoova.Academy.Domain.Catalog;

/// <summary>
/// Aggregate root of the syllabus. Owns its sections and lessons; access is decided
/// elsewhere, against the <see cref="ProductId"/> that represents this course (ADR-007).
/// </summary>
public sealed class Course
{
    private readonly List<Section> _sections = [];

    public Guid Id { get; private set; }
    public Guid ProductId { get; private set; }
    public Slug Slug { get; private set; }
    public string Title { get; private set; }
    public string ShortDescription { get; private set; }
    public string LongDescription { get; private set; }
    public string? CoverImageUrl { get; private set; }
    public PublicationStatus Status { get; private set; }
    public CourseLevel Level { get; private set; }
    public bool IsFeatured { get; private set; }
    public bool IsNew { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? PublishedAt { get; private set; }

    public IReadOnlyList<Section> Sections => _sections;

    private Course(
        Guid id,
        Guid productId,
        Slug slug,
        string title,
        string shortDescription,
        string longDescription,
        string? coverImageUrl,
        PublicationStatus status,
        CourseLevel level,
        bool isFeatured,
        bool isNew,
        DateTimeOffset createdAt,
        DateTimeOffset? publishedAt)
    {
        Id = id;
        ProductId = productId;
        Slug = slug;
        Title = title;
        ShortDescription = shortDescription;
        LongDescription = longDescription;
        CoverImageUrl = coverImageUrl;
        Status = status;
        Level = level;
        IsFeatured = isFeatured;
        IsNew = isNew;
        CreatedAt = createdAt;
        PublishedAt = publishedAt;
    }

    public static Result<Course, Error> Create(
        Guid id,
        Guid productId,
        Slug slug,
        string title,
        string shortDescription,
        string longDescription,
        CourseLevel level,
        DateTimeOffset createdAt)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return Error.Validation("course.title_empty", "El curso necesita un título.");
        }

        if (string.IsNullOrWhiteSpace(shortDescription))
        {
            return Error.Validation("course.short_description_empty", "El curso necesita una descripción corta.");
        }

        return new Course(
            id, productId, slug, title.Trim(), shortDescription.Trim(), longDescription?.Trim() ?? string.Empty,
            coverImageUrl: null, PublicationStatus.Draft, level,
            isFeatured: false, isNew: false, createdAt, publishedAt: null);
    }

    public static Course Rehydrate(
        Guid id,
        Guid productId,
        Slug slug,
        string title,
        string shortDescription,
        string longDescription,
        string? coverImageUrl,
        PublicationStatus status,
        CourseLevel level,
        bool isFeatured,
        bool isNew,
        DateTimeOffset createdAt,
        DateTimeOffset? publishedAt,
        IEnumerable<Section> sections)
    {
        var course = new Course(
            id, productId, slug, title, shortDescription, longDescription, coverImageUrl,
            status, level, isFeatured, isNew, createdAt, publishedAt);
        course._sections.AddRange(sections.OrderBy(s => s.Order));
        return course;
    }

    public Result<Unit, Error> AddSection(Section section)
    {
        if (section.CourseId != Id)
        {
            return Error.Validation("course.section_mismatch", "La sección pertenece a otro curso.");
        }

        _sections.Add(section);
        _sections.Sort((a, b) => a.Order.CompareTo(b.Order));
        return Unit.Value;
    }

    /// <summary>Invariant: a course cannot be published without at least one lesson (T-01).</summary>
    public Result<Unit, Error> Publish(DateTimeOffset now)
    {
        if (TotalLessons == 0)
        {
            return Error.Conflict(
                "course.publish_without_lessons",
                "No se puede publicar un curso sin al menos una lección.");
        }

        Status = PublicationStatus.Published;
        PublishedAt ??= now;
        return Unit.Value;
    }

    public void Unpublish() => Status = PublicationStatus.Draft;

    public Result<Unit, Error> MarkComingSoon()
    {
        if (Status == PublicationStatus.Published)
        {
            return Error.Conflict(
                "course.coming_soon_after_publish",
                "Un curso publicado no puede volver a 'próximamente'; despublícalo primero.");
        }

        Status = PublicationStatus.ComingSoon;
        return Unit.Value;
    }

    /// <summary>
    /// Edición de la ficha desde el panel. El slug NO se toca: es la URL pública del curso y
    /// cambiarlo rompería los enlaces que ya circulan y el progreso guardado que apunta a ellos.
    /// </summary>
    public Result<Unit, Error> Describe(
        string title,
        string shortDescription,
        string longDescription,
        CourseLevel level)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return Error.Validation("course.title_empty", "El curso necesita un título.");
        }

        if (string.IsNullOrWhiteSpace(shortDescription))
        {
            return Error.Validation("course.short_description_empty", "El curso necesita una descripción corta.");
        }

        Title = title.Trim();
        ShortDescription = shortDescription.Trim();
        LongDescription = longDescription?.Trim() ?? string.Empty;
        Level = level;

        return Unit.Value;
    }

    public void SetFeatured(bool value) => IsFeatured = value;

    public void SetNew(bool value) => IsNew = value;

    public void SetCoverImage(string? url) => CoverImageUrl = url;

    public int TotalSections => _sections.Count;

    public int TotalLessons => _sections.Sum(s => s.Lessons.Count);

    public int TotalDurationMinutes => _sections.Sum(s => s.TotalDurationMinutes);

    /// <summary>Hours shown on the catalogue card, rounded up: 91 min reads as 2 h, not 1 h.</summary>
    public int TotalHours => (int)Math.Ceiling(TotalDurationMinutes / 60.0);

    public IEnumerable<Lesson> AllLessons => _sections.SelectMany(s => s.Lessons);

    public IEnumerable<Lesson> RequiredLessons => AllLessons.Where(l => l.IsRequired);

    public bool IsVisibleToPublic => Status is PublicationStatus.Published or PublicationStatus.ComingSoon;
}
