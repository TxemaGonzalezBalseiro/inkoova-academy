using Inkoova.Academy.Application.Abstractions;
using Inkoova.Academy.Application.Catalog;
using Inkoova.Academy.Domain.Common;
using Inkoova.Academy.Domain.Learning;
using Inkoova.Academy.Domain.ValueObjects;

namespace Inkoova.Academy.Application.Learning;

public sealed record LessonProgressDto(string LessonSlug, bool Completed, string? LastPositionRef);

public sealed record CourseProgressDetailDto(
    string CourseSlug,
    int PercentComplete,
    int CompletedLessons,
    int TotalRequiredLessons,
    string? NextLessonSlug,
    IReadOnlyList<LessonProgressDto> Lessons);

public sealed class GetCourseProgressHandler(ICourseRepository courses, IProgressRepository progress)
{
    public async Task<Result<CourseProgressDetailDto, Error>> HandleAsync(
        string courseSlug,
        Guid userId,
        CancellationToken ct)
    {
        var slug = Slug.Create(courseSlug);
        if (slug.IsFailure)
        {
            return slug.Error;
        }

        var course = await courses.GetBySlugAsync(slug.Value, ct);
        if (course is null)
        {
            return Error.NotFound("course.not_found", "No existe un curso con ese identificador.");
        }

        var rows = await progress.GetForCourseAsync(userId, course.Id, ct);
        var byLesson = rows.ToDictionary(r => r.LessonId);

        var lessons = course.AllLessons
            .Select(lesson =>
            {
                byLesson.TryGetValue(lesson.Id, out var row);
                return new LessonProgressDto(lesson.Slug.Value, row?.IsCompleted ?? false, row?.LastPositionRef);
            })
            .ToList();

        var required = course.RequiredLessons.ToList();
        var completedRequired = required.Count(l => byLesson.TryGetValue(l.Id, out var r) && r.IsCompleted);
        var percent = required.Count == 0 ? 0 : (int)Math.Round(completedRequired * 100.0 / required.Count);
        var next = course.AllLessons.FirstOrDefault(l => !(byLesson.TryGetValue(l.Id, out var r) && r.IsCompleted));

        return new CourseProgressDetailDto(
            course.Slug.Value, percent, completedRequired, required.Count, next?.Slug.Value, lessons);
    }
}

/// <summary>
/// Marks a lesson complete. Requires access: otherwise a student without a subscription
/// could complete a course they never opened and claim a certificate (T-10).
/// </summary>
public sealed class CompleteLessonHandler(
    ICourseRepository courses,
    IProgressRepository progress,
    IAccessPolicy accessPolicy,
    IClock clock)
{
    public async Task<Result<Unit, Error>> HandleAsync(Guid userId, Guid lessonId, CancellationToken ct)
    {
        var lesson = await courses.GetLessonAsync(lessonId, ct);
        if (lesson is null)
        {
            return Error.NotFound("lesson.not_found", "No existe esa lección.");
        }

        if (!await accessPolicy.CanAccessLessonAsync(userId, lesson, ct))
        {
            return Error.Forbidden("lesson.no_access", "No tienes acceso a esta lección.");
        }

        var existing = await progress.GetAsync(userId, lessonId, ct)
                       ?? LessonProgress.Start(userId, lessonId, clock.UtcNow);

        existing.Complete(clock.UtcNow);
        await progress.UpsertAsync(existing, ct);
        return Unit.Value;
    }
}

/// <summary>Stores the resume point reported by the deck through postMessage (T-07).</summary>
public sealed class SaveLessonPositionHandler(
    ICourseRepository courses,
    IProgressRepository progress,
    IAccessPolicy accessPolicy,
    IClock clock)
{
    public async Task<Result<Unit, Error>> HandleAsync(
        Guid userId,
        Guid lessonId,
        string positionRef,
        CancellationToken ct)
    {
        var lesson = await courses.GetLessonAsync(lessonId, ct);
        if (lesson is null)
        {
            return Error.NotFound("lesson.not_found", "No existe esa lección.");
        }

        if (!await accessPolicy.CanAccessLessonAsync(userId, lesson, ct))
        {
            return Error.Forbidden("lesson.no_access", "No tienes acceso a esta lección.");
        }

        var existing = await progress.GetAsync(userId, lessonId, ct)
                       ?? LessonProgress.Start(userId, lessonId, clock.UtcNow);

        var updated = existing.UpdatePosition(positionRef, clock.UtcNow);
        if (updated.IsFailure)
        {
            return updated.Error;
        }

        await progress.UpsertAsync(existing, ct);
        return Unit.Value;
    }
}

/// <summary>Progress summary across every course the student has started (account page).</summary>
public sealed class GetMyCoursesHandler(
    ICourseRepository courses,
    IProgressRepository progress,
    ICertificateRepository certificates,
    IAccessPolicy accessPolicy)
{
    /// <summary>
    /// Los cursos del alumno: los que tiene comprados y también los que ha empezado.
    ///
    /// Antes solo salían los comprados, así que quien estaba estudiando el pre-curso gratuito
    /// —o el bloque gratis de un curso de pago— veía "todavía no tienes acceso a ningún curso"
    /// con la mitad hecha. El sitio le decía que no había empezado mientras avanzaba.
    ///
    /// `HasFullAccess` distingue los dos casos para que la pantalla no dé a entender que lo
    /// tiene entero cuando solo está viendo la muestra.
    /// </summary>
    public async Task<IReadOnlyList<MyCourseDto>> HandleAsync(
        Guid userId,
        CancellationToken ct)
    {
        var accessible = await accessPolicy.GetAccessibleProductIdsAsync(userId, ct);
        var catalog = await courses.GetPublicCatalogAsync(ct);
        var result = new List<MyCourseDto>();

        foreach (var course in catalog)
        {
            var completed = await progress.GetCompletedLessonIdsAsync(userId, course.Id, ct);
            var owned = accessible.Contains(course.ProductId);

            if (!owned && completed.Count == 0)
            {
                continue;
            }

            var required = course.RequiredLessons.ToList();
            var percent = required.Count == 0
                ? 0
                : (int)Math.Round(required.Count(l => completed.Contains(l.Id)) * 100.0 / required.Count);

            // El certificado ya emitido, si lo hay. Se consulta solo para los cursos que están
            // al 100 %: pedirlo para todos serían N consultas para responder que no en casi
            // todas, y por debajo del 100 % no puede existir.
            var certificate = percent == 100
                ? await certificates.GetForUserAndSubjectAsync(userId, course.Id, ct)
                : null;

            result.Add(new MyCourseDto(
                GetCatalogHandler.ToCard(course),
                course.Id,
                percent,
                owned,
                certificate?.Code.Value,
                // Emitible cuando está completo, no lo tiene ya y tenía derecho a abrir todo lo
                // que completó. Eso último no es lo mismo que haberlo comprado: un curso
                // enteramente gratuito —el pre-curso— tiene todas sus clases como muestra y se
                // cursa entero sin comprar nada. Lo que se sigue impidiendo es certificar un
                // curso de pago a quien solo ha visto la muestra.
                CanIssueCertificate: percent == 100
                                     && certificate is null
                                     && (owned || required.TrueForAll(lesson => lesson.IsFreePreview))));
        }

        return result;
    }
}

/// <summary>
/// Un curso en la pantalla de la cuenta, con lo que hace falta para decidir qué botón enseñar:
/// cuánto lleva, si es suyo del todo y en qué estado está su certificado.
/// </summary>
public sealed record MyCourseDto(
    CourseCardDto Course,
    Guid CourseId,
    int PercentComplete,
    bool HasFullAccess,
    string? CertificateCode,
    bool CanIssueCertificate);
