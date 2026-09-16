using Inkoova.Academy.Application.Abstractions;
using Inkoova.Academy.Domain.Catalog;
using Inkoova.Academy.Domain.Common;
using Inkoova.Academy.Domain.ValueObjects;

namespace Inkoova.Academy.Application.Catalog;

/// <summary>
/// Course page. The syllabus is public; <c>contentRef</c> is not. A lesson the student
/// cannot open is returned without its reference, so an unauthorised client never receives
/// the path even if it inspects the payload (T-02, T-06 acceptance criteria).
/// </summary>
public sealed class GetCourseDetailHandler(
    ICourseRepository courses,
    IProgressRepository progress,
    ICertificateRepository certificates,
    IAccessPolicy accessPolicy,
    IQuizRepository quizzes)
{
    public async Task<Result<CourseDetailDto, Error>> HandleAsync(string slug, Guid? userId, CancellationToken ct)
    {
        var parsedSlug = Slug.Create(slug);
        if (parsedSlug.IsFailure)
        {
            return parsedSlug.Error;
        }

        var course = await courses.GetBySlugAsync(parsedSlug.Value, ct);
        if (course is null || !course.IsVisibleToPublic)
        {
            return Error.NotFound("course.not_found", "No existe un curso con ese identificador.");
        }

        var hasProductAccess = await accessPolicy.CanAccessProductAsync(userId, course.ProductId, ct);

        var sections = course.Sections
            .Select(section => new SectionDto(
                section.Title,
                section.Order,
                section.Lessons.Select(lesson => ToLessonDto(lesson, hasProductAccess)).ToList()))
            .ToList();

        var admissionQuiz = await quizzes.GetBySlugAsync(AdmissionQuizSlugFor(course), ct);

        CourseProgressDto? progressDto = null;
        if (userId is { } uid)
        {
            progressDto = await BuildProgressAsync(course, uid, hasProductAccess, ct);
        }

        return new CourseDetailDto(
            course.Slug.Value,
            course.Title,
            course.ShortDescription,
            course.LongDescription,
            course.CoverImageUrl,
            course.Status.ToString().ToLowerInvariant(),
            course.Level.ToString().ToLowerInvariant(),
            course.TotalHours,
            course.TotalLessons,
            course.TotalSections,
            course.IsNew,
            course.IsFeatured,
            sections,
            admissionQuiz?.Slug.Value,
            progressDto);
    }

    private static LessonDto ToLessonDto(Lesson lesson, bool hasProductAccess)
    {
        var hasAccess = lesson.IsFreePreview || hasProductAccess;

        return new LessonDto(
            lesson.Slug.Value,
            lesson.Title,
            lesson.Type.ToString().ToLowerInvariant(),
            lesson.DurationMinutes,
            lesson.IsFreePreview,
            lesson.IsRequired,
            hasAccess,
            hasAccess ? lesson.ContentRef : null);
    }

    /// <summary>
    /// Si el alumno tenía derecho a abrir TODO lo que ha completado.
    ///
    /// Comprarlo no es la única forma: un curso enteramente gratuito —el pre-curso— tiene todas
    /// sus clases marcadas como muestra, así que nadie «lo compra» y aun así se cursa entero. La
    /// primera versión de esto exigía compra y dejaba al pre-curso sin certificado justo al
    /// terminarlo, que es cuando el alumno lo espera.
    ///
    /// Lo que sí se sigue impidiendo es lo que motivó la comprobación: certificar un curso de
    /// pago a quien solo ha visto sus clases de muestra.
    /// </summary>
    private static bool CoversEveryRequiredLesson(Course course, bool hasProductAccess) =>
        hasProductAccess || course.RequiredLessons.All(lesson => lesson.IsFreePreview);

    private async Task<CourseProgressDto> BuildProgressAsync(
        Course course,
        Guid userId,
        bool hasProductAccess,
        CancellationToken ct)
    {
        var completed = await progress.GetCompletedLessonIdsAsync(userId, course.Id, ct);
        var required = course.RequiredLessons.ToList();
        var completedRequired = required.Count(l => completed.Contains(l.Id));
        var percent = required.Count == 0 ? 0 : (int)Math.Round(completedRequired * 100.0 / required.Count);

        // "Continuar" tiene que llevar a una clase que el alumno pueda abrir. Sin filtrar por
        // acceso, quien terminaba el bloque gratuito recibía como siguiente la primera clase de
        // pago: el botón le mandaba a un 403 mientras el temario, que sí mira el acceso, le
        // enseñaba lo suyo abierto. Las dos vistas decían cosas distintas sobre lo mismo.
        var next = course.AllLessons.FirstOrDefault(lesson =>
            !completed.Contains(lesson.Id) && (lesson.IsFreePreview || hasProductAccess));
        var isComplete = required.Count > 0 && completedRequired == required.Count;

        string? certificateCode = null;
        if (isComplete)
        {
            var certificate = await certificates.GetForUserAndSubjectAsync(userId, course.Id, ct);
            certificateCode = certificate?.Code.Value;
        }

        return new CourseProgressDto(
            completedRequired,
            required.Count,
            percent,
            next?.Slug.Value,
            next?.Title,
            isComplete,
            certificateCode,
            isComplete && certificateCode is null && CoversEveryRequiredLesson(course, hasProductAccess));
    }

    /// <summary>
    /// Convention: the admission quiz of a course lives at <c>{course-slug}-nivel</c>.
    /// A course without such a quiz simply has none; the page hides the CTA.
    /// </summary>
    private static Slug AdmissionQuizSlugFor(Course course) =>
        Slug.Create($"{course.Slug.Value}-nivel").Value;
}
