using Inkoova.Academy.Application.Abstractions;
using Inkoova.Academy.Domain.Catalog;
using Inkoova.Academy.Domain.Common;
using Inkoova.Academy.Domain.Learning;
using Inkoova.Academy.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Inkoova.Academy.Application.Content;

/// <summary>
/// Applies a course manifest (T-07). Idempotent and convergent: running it twice on the same
/// manifest leaves the same rows, and a block removed from the manifest is removed from the
/// syllabus. Existing lesson ids are preserved by slug so student progress survives a re-import.
/// </summary>
public sealed class ImportManifestHandler(
    ICourseRepository courses,
    IProductRepository products,
    IQuizRepository quizzes,
    IContentStorage storage,
    ICatalogCache cache,
    IClock clock,
    ILogger<ImportManifestHandler> logger)
{
    /// <summary>Computes what would change without writing anything.</summary>
    public async Task<Result<ImportDiff, Error>> PreviewAsync(CourseManifest manifest, CancellationToken ct)
    {
        var slug = Slug.Create(manifest.CourseSlug);
        if (slug.IsFailure)
        {
            return slug.Error;
        }

        var existing = await courses.GetBySlugAsync(slug.Value, ct);

        var manifestSections = manifest.Sections.Select(s => s.Title).ToHashSet(StringComparer.Ordinal);
        var manifestLessons = manifest.Sections
            .SelectMany(s => s.Lessons)
            .ToDictionary(l => l.Slug, StringComparer.Ordinal);

        var existingSections = existing?.Sections.Select(s => s.Title).ToHashSet(StringComparer.Ordinal) ?? [];
        var existingLessons = existing?.AllLessons.ToDictionary(l => l.Slug.Value, StringComparer.Ordinal) ?? [];

        var changed = new List<string>();
        foreach (var (lessonSlug, incoming) in manifestLessons)
        {
            if (!existingLessons.TryGetValue(lessonSlug, out var current))
            {
                continue;
            }

            if (current.Title != incoming.Title
                || current.ContentRef != incoming.ContentRef
                || current.DurationMinutes != incoming.DurationMinutes
                || current.IsFreePreview != incoming.IsFreePreview
                || current.IsRequired != incoming.IsRequired)
            {
                changed.Add(lessonSlug);
            }
        }

        // A manifest that points at files the volume does not have would publish a course
        // whose lessons 404. Better to say so before applying than to find out from a student.
        var missing = new List<string>();
        foreach (var lesson in manifestLessons.Values)
        {
            if (!await storage.ExistsAsync(lesson.ContentRef, ct))
            {
                missing.Add(lesson.ContentRef);
            }
        }

        // El contenido se sirve por fichero entero: una clase es un ancla dentro de él. Si un
        // fichero mezcla clases gratuitas y de pago, la gratuita entrega también las de pago, y
        // no se nota porque el temario sigue enseñando su candado.
        var mixedAccess = manifestLessons.Values
            .GroupBy(lesson => DocumentOf(lesson.ContentRef))
            .Where(file => file.Any(l => l.IsFreePreview) && file.Any(l => !l.IsFreePreview))
            .Select(file => $"{file.Key} ({file.Count(l => l.IsFreePreview)} gratis de {file.Count()})")
            .ToList();

        return new ImportDiff(
            manifest.CourseSlug,
            existing is not null,
            manifestSections.Except(existingSections).ToList(),
            existingSections.Except(manifestSections).ToList(),
            manifestLessons.Keys.Except(existingLessons.Keys).ToList(),
            existingLessons.Keys.Except(manifestLessons.Keys).ToList(),
            changed,
            manifest.Quizzes.Select(q => q.Slug).ToList(),
            missing,
            mixedAccess);
    }

    public async Task<Result<ImportDiff, Error>> ApplyAsync(CourseManifest manifest, CancellationToken ct)
    {
        var diff = await PreviewAsync(manifest, ct);
        if (diff.IsFailure)
        {
            return diff.Error;
        }

        var slug = Slug.Create(manifest.CourseSlug).Value;
        var existing = await courses.GetBySlugAsync(slug, ct);

        var product = await EnsureProductAsync(manifest, slug, ct);
        if (product.IsFailure)
        {
            return product.Error;
        }

        // Reusing ids keyed by slug is what keeps lesson_progress pointing at the same rows
        // after a re-import. Losing them would silently reset every student's progress.
        var lessonIdsBySlug = existing?.AllLessons.ToDictionary(l => l.Slug.Value, l => l.Id, StringComparer.Ordinal)
                              ?? new Dictionary<string, Guid>(StringComparer.Ordinal);

        var sectionIdsByTitle = existing?.Sections.ToDictionary(s => s.Title, s => s.Id, StringComparer.Ordinal)
                                ?? new Dictionary<string, Guid>(StringComparer.Ordinal);

        var courseId = existing?.Id ?? Guid.CreateVersion7();
        var sections = new List<Section>();

        foreach (var sectionManifest in manifest.Sections.OrderBy(s => s.Order))
        {
            var sectionId = sectionIdsByTitle.TryGetValue(sectionManifest.Title, out var knownSection)
                ? knownSection
                : Guid.CreateVersion7();

            var section = Section.Create(sectionId, courseId, sectionManifest.Order, sectionManifest.Title);
            if (section.IsFailure)
            {
                return section.Error;
            }

            for (var order = 0; order < sectionManifest.Lessons.Count; order++)
            {
                var lessonManifest = sectionManifest.Lessons[order];

                var lessonSlug = Slug.Create(lessonManifest.Slug);
                if (lessonSlug.IsFailure)
                {
                    return lessonSlug.Error;
                }

                if (!Enum.TryParse<LessonType>(lessonManifest.Type, ignoreCase: true, out var type))
                {
                    return Error.Validation(
                        "manifest.unknown_lesson_type",
                        $"Tipo de lección desconocido: '{lessonManifest.Type}'.");
                }

                var lessonId = lessonIdsBySlug.TryGetValue(lessonManifest.Slug, out var knownLesson)
                    ? knownLesson
                    : Guid.CreateVersion7();

                var lesson = Lesson.Create(
                    lessonId, sectionId, order, lessonSlug.Value, lessonManifest.Title, type,
                    lessonManifest.DurationMinutes, lessonManifest.ContentRef,
                    lessonManifest.IsFreePreview, lessonManifest.IsRequired);

                if (lesson.IsFailure)
                {
                    return lesson.Error;
                }

                var added = section.Value.AddLesson(lesson.Value);
                if (added.IsFailure)
                {
                    return added.Error;
                }
            }

            sections.Add(section.Value);
        }

        if (!Enum.TryParse<CourseLevel>(manifest.Level, ignoreCase: true, out var level))
        {
            return Error.Validation("manifest.unknown_level", $"Nivel desconocido: '{manifest.Level}'.");
        }

        var course = Course.Rehydrate(
            courseId,
            product.Value.Id,
            slug,
            manifest.Title,
            manifest.ShortDescription,
            manifest.LongDescription,
            existing?.CoverImageUrl,
            // Importing never publishes: that is a deliberate admin action (T-11).
            existing?.Status ?? PublicationStatus.Draft,
            level,
            existing?.IsFeatured ?? false,
            existing?.IsNew ?? false,
            existing?.CreatedAt ?? clock.UtcNow,
            existing?.PublishedAt,
            sections);

        await courses.SaveAsync(course, ct);

        var importedQuizzes = await ImportQuizzesAsync(manifest, courseId, ct);
        if (importedQuizzes.IsFailure)
        {
            return importedQuizzes.Error;
        }

        cache.Invalidate();

        logger.LogInformation(
            "Imported {Slug}: {Sections} sections, {Lessons} lessons, {Quizzes} quizzes.",
            manifest.CourseSlug, course.TotalSections, course.TotalLessons, manifest.Quizzes.Count);

        return diff.Value;
    }

    private async Task<Result<Product, Error>> EnsureProductAsync(
        CourseManifest manifest,
        Slug slug,
        CancellationToken ct)
    {
        var existing = await products.GetBySlugAsync(slug, ct);
        if (existing is not null)
        {
            // El título del producto sigue al del manifest. Antes se devolvía tal cual y se
            // quedaba congelado desde que se creó: renombrar un curso cambiaba la ficha pública
            // y dejaba el producto con el nombre viejo, que es el que se ve al elegir qué
            // incluye un plan y al conceder un acceso desde el panel. Dos nombres para la misma
            // cosa, y ninguna pantalla decía cuál era el bueno.
            if (!string.Equals(existing.Title, manifest.Title.Trim(), StringComparison.Ordinal))
            {
                var renamed = existing.Rename(manifest.Title);
                if (renamed.IsFailure)
                {
                    return renamed.Error;
                }

                await products.UpsertAsync(existing, ct);
            }

            return existing;
        }

        var created = Product.Create(
            Guid.CreateVersion7(), ProductType.Course, slug, manifest.Title,
            // TODO(T-11): the one-off price is set from the admin panel, not by the importer.
            oneOffPrice: null, clock.UtcNow);

        if (created.IsFailure)
        {
            return created.Error;
        }

        await products.UpsertAsync(created.Value, ct);
        return created.Value;
    }

    private async Task<Result<Unit, Error>> ImportQuizzesAsync(CourseManifest manifest, Guid courseId, CancellationToken ct)
    {
        foreach (var quizManifest in manifest.Quizzes)
        {
            var quizSlug = Slug.Create(quizManifest.Slug);
            if (quizSlug.IsFailure)
            {
                logger.LogWarning("Skipping quiz with invalid slug '{Slug}'.", quizManifest.Slug);
                continue;
            }

            if (!Enum.TryParse<QuizKind>(quizManifest.Kind, ignoreCase: true, out var kind))
            {
                logger.LogWarning("Skipping quiz '{Slug}' with unknown kind '{Kind}'.", quizManifest.Slug, quizManifest.Kind);
                continue;
            }

            var existing = await quizzes.GetBySlugAsync(quizSlug.Value, ct);
            var quizId = existing?.Id ?? Guid.CreateVersion7();

            var quiz = Quiz.Create(quizId, quizSlug.Value, quizManifest.Title, kind, courseId);
            if (quiz.IsFailure)
            {
                logger.LogWarning("Skipping quiz '{Slug}': {Error}", quizManifest.Slug, quiz.Error);
                continue;
            }

            // Question ids are reused by position so a re-import does not orphan the answers
            // stored inside past attempts.
            var existingQuestionIds = existing?.Questions.Select(q => q.Id).ToList() ?? [];

            for (var order = 0; order < quizManifest.Questions.Count; order++)
            {
                var questionManifest = quizManifest.Questions[order];

                var options = questionManifest.Options
                    .Select(o => new QuizOption(o.Index, o.Text, o.IsCorrect))
                    .ToList();

                var questionId = order < existingQuestionIds.Count
                    ? existingQuestionIds[order]
                    : Guid.CreateVersion7();

                var question = Question.Create(
                    questionId, quizId, order, questionManifest.Category,
                    questionManifest.Text, questionManifest.Explanation, options);

                if (question.IsFailure)
                {
                    // Se aborta en vez de saltar la pregunta. Un test de nivel al que le
                    // faltan preguntas sigue funcionando pero con los umbrales invalidados:
                    // el alumno recibiría un veredicto calculado sobre otro examen. Es peor
                    // que no importarlo, y en silencio no se detecta hasta que alguien
                    // cuenta las preguntas.
                    return Error.Validation(
                        "manifest.invalid_question",
                        $"La pregunta {order + 1} del cuestionario '{quizManifest.Slug}' no es válida: "
                        + $"{question.Error.Message} No se importa nada para no dejar el test a medias.");
                }

                quiz.Value.AddQuestion(question.Value);
            }

            await quizzes.SaveAsync(quiz.Value, ct);
        }

        return Unit.Value;
    }

    /// <summary>Ruta del fichero, sin el ancla de la slide.</summary>
    private static string DocumentOf(string contentRef)
    {
        var hash = contentRef.IndexOf('#', StringComparison.Ordinal);
        return hash < 0 ? contentRef : contentRef[..hash];
    }
}
