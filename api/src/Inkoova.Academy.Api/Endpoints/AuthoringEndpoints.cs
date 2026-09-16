using System.Text.Json;
using Inkoova.Academy.Api.Common;
using Inkoova.Academy.Application.Abstractions;
using Inkoova.Academy.Application.Content;
using Inkoova.Academy.Domain.Users;

namespace Inkoova.Academy.Api.Endpoints;

/// <summary>
/// Gestor de contenidos (T-11): crear y editar cursos, secciones, clases y quizzes desde el
/// panel, sin pasar por el importador.
///
/// Van aparte de <see cref="AdminEndpoints"/> porque son otra cosa: aquello administra lo que
/// ya existe —publicar, ordenar, dar accesos— y esto escribe el catálogo. Todo queda en la
/// auditoría con quién y qué, que es lo que se mira cuando un curso amanece distinto.
/// </summary>
public static class AuthoringEndpoints
{
    public static void MapAuthoringEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/authoring")
            .WithTags("Gestor de contenidos")
            .RequireAuthorization("admin");

        // ── cursos ─────────────────────────────────────────────────────────────────────

        // El DTO público del catálogo no lleva identificadores —no le hacen falta al alumno y
        // exponerlos es superficie de más— pero el editor necesita saber a qué fila escribe.
        group.MapGet("/courses/{slug}", async (
                string slug,
                ICourseRepository courses,
                CancellationToken ct) =>
            {
                var parsed = Domain.ValueObjects.Slug.Create(slug);
                if (parsed.IsFailure)
                {
                    return parsed.Error.ToProblem();
                }

                var course = await courses.GetBySlugAsync(parsed.Value, ct);

                if (course is null)
                {
                    return Results.NotFound();
                }

                return Results.Ok(new
                {
                    course.Id,
                    Slug = course.Slug.Value,
                    course.Title,
                    course.ShortDescription,
                    course.LongDescription,
                    Level = course.Level.ToString().ToLowerInvariant(),
                    Status = course.Status.ToString().ToLowerInvariant(),
                    Sections = course.Sections.OrderBy(section => section.Order).Select(section => new
                    {
                        section.Id,
                        section.Title,
                        section.Order,
                        Lessons = section.Lessons.OrderBy(lesson => lesson.Order).Select(lesson => new
                        {
                            lesson.Id,
                            Slug = lesson.Slug.Value,
                            lesson.Title,
                            Type = lesson.Type.ToString().ToLowerInvariant(),
                            lesson.DurationMinutes,
                            lesson.ContentRef,
                            lesson.IsFreePreview,
                            lesson.IsRequired
                        })
                    })
                });
            })
            .WithSummary("Curso completo con identificadores, para el editor.");

        group.MapPost("/courses", async (
                CreateCourseRequest body,
                CourseAuthoringHandlers authoring,
                IAuditLogRepository audit,
                HttpContext context,
                IClock clock,
                CancellationToken ct) =>
            {
                var result = await authoring.CreateCourseAsync(body, ct);

                if (result.IsSuccess)
                {
                    await Audit(audit, context, clock, "course.create", "course", result.Value.ToString(),
                        JsonSerializer.Serialize(new { body.Slug, body.Title }), ct);
                }

                return result.ToHttp(id => Results.Ok(new { id }));
            })
            .WithSummary("Crea un curso y su producto.");

        group.MapPut("/courses/{courseId:guid}", async (
                Guid courseId,
                UpdateCourseRequest body,
                CourseAuthoringHandlers authoring,
                IAuditLogRepository audit,
                HttpContext context,
                IClock clock,
                CancellationToken ct) =>
            {
                var result = await authoring.UpdateCourseAsync(courseId, body, ct);

                if (result.IsSuccess)
                {
                    await Audit(audit, context, clock, "course.update", "course", courseId.ToString(),
                        JsonSerializer.Serialize(new { body.Title }), ct);
                }

                return result.ToNoContent();
            })
            .WithSummary("Edita la ficha de un curso. El slug no cambia nunca.");

        // ── secciones ──────────────────────────────────────────────────────────────────

        group.MapPost("/courses/{courseId:guid}/sections", async (
                Guid courseId,
                TitleBody body,
                CourseAuthoringHandlers authoring,
                IAuditLogRepository audit,
                HttpContext context,
                IClock clock,
                CancellationToken ct) =>
            {
                var result = await authoring.AddSectionAsync(courseId, body.Title, ct);

                if (result.IsSuccess)
                {
                    await Audit(audit, context, clock, "section.create", "section", result.Value.ToString(),
                        JsonSerializer.Serialize(new { courseId, body.Title }), ct);
                }

                return result.ToHttp(id => Results.Ok(new { id }));
            })
            .WithSummary("Añade una sección al final del temario.");

        group.MapPut("/courses/{courseId:guid}/sections/{sectionId:guid}", async (
                Guid courseId,
                Guid sectionId,
                TitleBody body,
                CourseAuthoringHandlers authoring,
                IAuditLogRepository audit,
                HttpContext context,
                IClock clock,
                CancellationToken ct) =>
            {
                var result = await authoring.RenameSectionAsync(courseId, sectionId, body.Title, ct);

                if (result.IsSuccess)
                {
                    await Audit(audit, context, clock, "section.rename", "section", sectionId.ToString(),
                        JsonSerializer.Serialize(new { body.Title }), ct);
                }

                return result.ToNoContent();
            })
            .WithSummary("Renombra una sección.");

        group.MapDelete("/sections/{sectionId:guid}", async (
                Guid sectionId,
                CourseAuthoringHandlers authoring,
                IAuditLogRepository audit,
                HttpContext context,
                IClock clock,
                CancellationToken ct) =>
            {
                var result = await authoring.DeleteSectionAsync(sectionId, ct);

                // Se audita antes de contestar: borrar una sección se lleva sus clases por
                // delante y esta línea es lo único que queda para saber quién lo hizo.
                await Audit(audit, context, clock, "section.delete", "section", sectionId.ToString(), null, ct);

                return result.ToNoContent();
            })
            .WithSummary("Borra una sección y sus clases.");

        // ── clases ─────────────────────────────────────────────────────────────────────

        group.MapPost("/courses/{courseId:guid}/sections/{sectionId:guid}/lessons", async (
                Guid courseId,
                Guid sectionId,
                UpsertLessonRequest body,
                CourseAuthoringHandlers authoring,
                IAuditLogRepository audit,
                HttpContext context,
                IClock clock,
                CancellationToken ct) =>
            {
                var result = await authoring.AddLessonAsync(courseId, sectionId, body, ct);

                if (result.IsSuccess)
                {
                    await Audit(audit, context, clock, "lesson.create", "lesson", result.Value.ToString(),
                        JsonSerializer.Serialize(new { sectionId, body.Title, body.IsFreePreview }), ct);
                }

                return result.ToHttp(id => Results.Ok(new { id }));
            })
            .WithSummary("Añade una clase al final de una sección.");

        group.MapPut("/courses/{courseId:guid}/lessons/{lessonId:guid}", async (
                Guid courseId,
                Guid lessonId,
                UpsertLessonRequest body,
                CourseAuthoringHandlers authoring,
                IAuditLogRepository audit,
                HttpContext context,
                IClock clock,
                CancellationToken ct) =>
            {
                var result = await authoring.UpdateLessonAsync(courseId, lessonId, body, ct);

                if (result.IsSuccess)
                {
                    // `isFreePreview` se registra siempre: es la diferencia entre una clase de
                    // pago y una gratis, y un cambio accidental regala contenido.
                    await Audit(audit, context, clock, "lesson.update", "lesson", lessonId.ToString(),
                        JsonSerializer.Serialize(new { body.Title, body.IsFreePreview, body.IsRequired }), ct);
                }

                return result.ToNoContent();
            })
            .WithSummary("Edita una clase. El slug no cambia nunca.");

        group.MapDelete("/lessons/{lessonId:guid}", async (
                Guid lessonId,
                CourseAuthoringHandlers authoring,
                IAuditLogRepository audit,
                HttpContext context,
                IClock clock,
                CancellationToken ct) =>
            {
                var result = await authoring.DeleteLessonAsync(lessonId, ct);
                await Audit(audit, context, clock, "lesson.delete", "lesson", lessonId.ToString(), null, ct);

                return result.ToNoContent();
            })
            .WithSummary("Borra una clase.");

        // ── quizzes ────────────────────────────────────────────────────────────────────

        group.MapPut("/quizzes", async (
                UpsertQuizRequest body,
                CourseAuthoringHandlers authoring,
                IAuditLogRepository audit,
                HttpContext context,
                IClock clock,
                CancellationToken ct) =>
            {
                var result = await authoring.SaveQuizAsync(body, ct);

                if (result.IsSuccess)
                {
                    await Audit(audit, context, clock, "quiz.save", "quiz", result.Value.ToString(),
                        JsonSerializer.Serialize(new { body.Slug, Questions = body.Questions.Count }), ct);
                }

                return result.ToHttp(id => Results.Ok(new { id }));
            })
            .WithSummary("Crea o reemplaza un quiz entero con sus preguntas.");

        group.MapGet("/quizzes/{slug}", async (
                string slug,
                IQuizRepository quizzes,
                CancellationToken ct) =>
            {
                var parsed = Domain.ValueObjects.Slug.Create(slug);
                if (parsed.IsFailure)
                {
                    return parsed.Error.ToProblem();
                }

                var quiz = await quizzes.GetBySlugAsync(parsed.Value, ct);

                if (quiz is null)
                {
                    return Results.NotFound();
                }

                // A diferencia del quiz que ve el alumno, aquí sí viaja `isCorrect`: es lo que
                // se está editando.
                return Results.Ok(new
                {
                    quiz.Id,
                    Slug = quiz.Slug.Value,
                    quiz.Title,
                    Kind = quiz.Kind.ToString().ToLowerInvariant(),
                    quiz.CourseId,
                    Questions = quiz.Questions.Select(question => new
                    {
                        question.Category,
                        question.Text,
                        question.Explanation,
                        Options = question.Options.Select(option => new { option.Text, option.IsCorrect })
                    })
                });
            })
            .WithSummary("Devuelve un quiz con sus respuestas correctas, para editarlo.");
    }

    public sealed record TitleBody(string Title);

    private static Task Audit(
        IAuditLogRepository audit,
        HttpContext context,
        IClock clock,
        string action,
        string entityType,
        string? entityId,
        string? detailsJson,
        CancellationToken ct) =>
        audit.AppendAsync(
            new AuditEntry(
                Guid.CreateVersion7(),
                context.User.RequireUserId(),
                action,
                entityType,
                entityId,
                detailsJson,
                clock.UtcNow),
            ct);
}
