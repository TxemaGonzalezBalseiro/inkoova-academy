using Inkoova.Academy.Domain.Common;
using Inkoova.Academy.Domain.ValueObjects;

namespace Inkoova.Academy.Domain.Catalog;

public sealed class Lesson
{
    public Guid Id { get; private set; }
    public Guid SectionId { get; private set; }
    public int Order { get; private set; }
    public Slug Slug { get; private set; }
    public string Title { get; private set; }
    public LessonType Type { get; private set; }
    public int DurationMinutes { get; private set; }

    /// <summary>
    /// Path of the imported asset relative to the content root, e.g. <c>agent-engineering-v3/B0-fundamentos.html</c>.
    /// Never leaves the API unless <c>IAccessPolicy</c> allows it (T-02, T-06).
    /// </summary>
    public string ContentRef { get; private set; }

    public bool IsFreePreview { get; private set; }

    /// <summary>Optional lessons do not block certificate emission (T-10).</summary>
    public bool IsRequired { get; private set; }

    private Lesson(
        Guid id,
        Guid sectionId,
        int order,
        Slug slug,
        string title,
        LessonType type,
        int durationMinutes,
        string contentRef,
        bool isFreePreview,
        bool isRequired)
    {
        Id = id;
        SectionId = sectionId;
        Order = order;
        Slug = slug;
        Title = title;
        Type = type;
        DurationMinutes = durationMinutes;
        ContentRef = contentRef;
        IsFreePreview = isFreePreview;
        IsRequired = isRequired;
    }

    public static Result<Lesson, Error> Create(
        Guid id,
        Guid sectionId,
        int order,
        Slug slug,
        string title,
        LessonType type,
        int durationMinutes,
        string contentRef,
        bool isFreePreview,
        bool isRequired = true)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return Error.Validation("lesson.title_empty", "La lección necesita un título.");
        }

        if (order < 0)
        {
            return Error.Validation("lesson.order_negative", "El orden de la lección no puede ser negativo.");
        }

        if (durationMinutes < 0)
        {
            return Error.Validation("lesson.duration_negative", "La duración no puede ser negativa.");
        }

        if (string.IsNullOrWhiteSpace(contentRef))
        {
            return Error.Validation("lesson.content_ref_empty", "La lección necesita una referencia de contenido.");
        }

        return new Lesson(
            id, sectionId, order, slug, title.Trim(), type, durationMinutes,
            contentRef.Trim(), isFreePreview, isRequired);
    }

    public static Lesson Rehydrate(
        Guid id,
        Guid sectionId,
        int order,
        Slug slug,
        string title,
        LessonType type,
        int durationMinutes,
        string contentRef,
        bool isFreePreview,
        bool isRequired) =>
        new(id, sectionId, order, slug, title, type, durationMinutes, contentRef, isFreePreview, isRequired);

    public void Reorder(int newOrder) => Order = Math.Max(0, newOrder);

    /// <summary>
    /// Edición desde el panel. Como en el curso, el slug no se toca: es la URL de la clase y
    /// el progreso del alumno apunta a ella.
    /// </summary>
    public Result<Unit, Error> Update(
        string title,
        LessonType type,
        int durationMinutes,
        string contentRef,
        bool isFreePreview,
        bool isRequired)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return Error.Validation("lesson.title_empty", "La lección necesita un título.");
        }

        if (durationMinutes < 0)
        {
            return Error.Validation("lesson.duration_negative", "La duración no puede ser negativa.");
        }

        if (string.IsNullOrWhiteSpace(contentRef))
        {
            return Error.Validation("lesson.content_ref_empty", "La lección necesita una referencia de contenido.");
        }

        Title = title.Trim();
        Type = type;
        DurationMinutes = durationMinutes;
        ContentRef = contentRef.Trim();
        IsFreePreview = isFreePreview;
        IsRequired = isRequired;

        return Unit.Value;
    }
}
