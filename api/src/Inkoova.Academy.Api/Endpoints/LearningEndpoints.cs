using Inkoova.Academy.Api.Common;
using Inkoova.Academy.Application.Abstractions;
using Inkoova.Academy.Application.Learning;
using Inkoova.Academy.Domain.Common;

namespace Inkoova.Academy.Api.Endpoints;

public static class LearningEndpoints
{
    public static void MapLearningEndpoints(this IEndpointRouteBuilder app)
    {
        var me = app.MapGroup("/api/me").WithTags("Progreso").RequireAuthorization();

        me.MapGet("/courses", async (GetMyCoursesHandler handler, HttpContext context, CancellationToken ct) =>
            {
                var courses = await handler.HandleAsync(context.User.RequireUserId(), ct);

                return Results.Ok(courses.Select(c => new
                {
                    course = c.Course,
                    courseId = c.CourseId,
                    percentComplete = c.PercentComplete,
                    hasFullAccess = c.HasFullAccess,
                    certificateCode = c.CertificateCode,
                    canIssueCertificate = c.CanIssueCertificate
                }));
            })
            .WithSummary("Cursos comprados y también los empezados, con su porcentaje.");

        // Los packs NO salen en «Mis cursos» por la lista de arriba: un pack no es un curso, no
        // tiene progreso ni certificado, y meterlo ahí obligaría a que cada campo de esa lista
        // fuera anulable. Van por su lado y la pantalla los pinta en su propia sección.
        //
        // Sin esto, un alumno con plan Élite o vitalicio tiene los seis packs y no se entera:
        // están en /packs, que es el escaparate, y nada desde su cuenta lleva hasta allí.
        me.MapGet("/packs", async (
                IPackRepository packs,
                IAccessPolicy accessPolicy,
                HttpContext context,
                CancellationToken ct) =>
            {
                var userId = context.User.RequireUserId();
                var published = await packs.GetPublishedAsync(ct);
                var mine = new List<object>();

                foreach (var pack in published)
                {
                    if (!await accessPolicy.CanAccessProductAsync(userId, pack.ProductId, ct))
                    {
                        continue;
                    }

                    mine.Add(new
                    {
                        slug = pack.Slug.Value,
                        title = pack.Title,
                        sector = pack.Sector,
                        version = pack.Version,
                        // La fecha de verificación es lo que dice si el pack sigue vigente, así
                        // que va en la tarjeta y no escondida en el detalle.
                        regulatoryCheckDate = pack.RegulatoryCheckDate,
                        files = pack.Files.Count,
                        totalSizeInBytes = pack.TotalSizeInBytes
                    });
                }

                return Results.Ok(mine);
            })
            .WithSummary("Packs a los que el alumno tiene acceso, para su cuenta.");

        me.MapGet("/progress/{courseSlug}", async (
                string courseSlug,
                GetCourseProgressHandler handler,
                HttpContext context,
                CancellationToken ct) =>
                (await handler.HandleAsync(courseSlug, context.User.RequireUserId(), ct)).ToHttp())
            .WithSummary("Progreso detallado de un curso.");

        me.MapPost("/progress/lessons/{lessonId:guid}/complete", async (
                Guid lessonId,
                CompleteLessonHandler handler,
                HttpContext context,
                CancellationToken ct) =>
                (await handler.HandleAsync(context.User.RequireUserId(), lessonId, ct)).ToNoContent())
            .WithSummary("Marca una lección como completada.");

        me.MapPut("/progress/lessons/{lessonId:guid}/position", async (
                Guid lessonId,
                PositionBody body,
                SaveLessonPositionHandler handler,
                HttpContext context,
                CancellationToken ct) =>
                (await handler.HandleAsync(context.User.RequireUserId(), lessonId, body.PositionRef, ct)).ToNoContent())
            .WithSummary("Guarda la última posición reportada por el contenido.");

        me.MapGet("/quiz-attempts", async (
                GetMyQuizAttemptsHandler handler,
                HttpContext context,
                CancellationToken ct) =>
                Results.Ok(await handler.HandleAsync(context.User.RequireUserId(), ct)))
            .WithSummary("Histórico de intentos de test de nivel.");

        // ── player ─────────────────────────────────────────────────────────────────────

        var player = app.MapGroup("/api/learn").WithTags("Player");

        // Anonymous is allowed so a free preview lesson opens without an account (T-06).
        player.MapGet("/{courseSlug}/{lessonSlug}", async (
                string courseSlug,
                string lessonSlug,
                GetPlayerLessonHandler handler,
                HttpContext context,
                CancellationToken ct) =>
                (await handler.HandleAsync(courseSlug, lessonSlug, context.User.UserId(), ct)).ToHttp())
            .AllowAnonymous()
            .WithSummary("Lección lista para el player, con token de contenido de 60 s.");

        // ── packs and downloads ────────────────────────────────────────────────────────

        var packs = app.MapGroup("/api/packs").WithTags("Packs").RequireAuthorization();

        packs.MapGet("/{slug}/files", async (
                string slug,
                GetPackDownloadsHandler handler,
                HttpContext context,
                CancellationToken ct) =>
                (await handler.HandleAsync(slug, context.User.RequireUserId(), ct)).ToHttp())
            .WithSummary("Ficheros descargables de un pack al que se tiene acceso.");

        packs.MapPost("/files/{fileId:guid}/download", async (
                Guid fileId,
                RequestPackFileDownloadHandler handler,
                HttpContext context,
                CancellationToken ct) =>
            {
                var result = await handler.HandleAsync(context.User.RequireUserId(), fileId, ct);
                return result.ToHttp(x => Results.Ok(new { token = x.Token, fileName = x.FileName }));
            })
            .WithSummary("Emite un token de descarga de 60 s y registra la descarga.");

        // ── quizzes ────────────────────────────────────────────────────────────────────

        var quizzes = app.MapGroup("/api/quizzes").WithTags("Cuestionarios");

        quizzes.MapGet("/{slug}", async (string slug, GetQuizHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(slug, ct)).ToHttp())
            .AllowAnonymous()
            .WithSummary("Preguntas del cuestionario, sin las respuestas correctas.");

        quizzes.MapPost("/{slug}/submit", async (
                string slug,
                SubmitQuizBody body,
                SubmitQuizHandler handler,
                HttpContext context,
                CancellationToken ct) =>
            {
                var userId = context.User.UserId();

                // An anonymous visitor gets a key so the attempt can be claimed at signup (T-08).
                var anonymousKey = userId is null ? VisitorCookie.GetOrCreate(context) : null;

                var result = await handler.HandleAsync(slug, body.Answers, userId, anonymousKey, ct);
                return result.ToHttp();
            })
            .AllowAnonymous()
            .RequireRateLimiting("quiz")
            .WithSummary("Corrige el cuestionario en servidor y guarda el intento.");

        // ── certificates ───────────────────────────────────────────────────────────────

        var certificates = app.MapGroup("/api/certificates").WithTags("Certificados");

        certificates.MapGet("/{code}", async (
                string code,
                VerifyCertificateHandler handler,
                CancellationToken ct) =>
                Results.Ok(await handler.HandleAsync(code, ct)))
            .AllowAnonymous()
            .RequireRateLimiting("verification")
            .WithSummary("Verificación pública de un certificado.");

        // Se regenera en vez de servir el fichero guardado al emitir. Guardarlo congelaba el
        // diseño del día de la emisión: al rediseñar el certificado, quien ya lo tenía seguía
        // descargando el viejo para siempre. Regenerar es barato porque el documento es
        // determinista, y no cambia nada comprobable: el hash se calcula sobre alumno, asunto y
        // fecha, no sobre los bytes del PDF.
        certificates.MapGet("/{code}/pdf", async (
                string code,
                RenderCertificateImageHandler handler,
                HttpContext context,
                CancellationToken ct) =>
            {
                // El dominio por el que se pide decide con qué marca sale el certificado.
                var result = await handler.RenderPdfAsync(code, context.Request.Host.Host, ct);

                return result.ToHttp(pdf => Results.File(pdf, "application/pdf", $"{code}.pdf"));
            })
            .AllowAnonymous()
            .RequireRateLimiting("verification")
            .WithSummary("Descarga el PDF de un certificado válido.");

        // La imagen se genera al vuelo y no se guarda: sale del mismo documento que el PDF, así
        // que guardarla sería una segunda copia que puede quedarse vieja.
        certificates.MapGet("/{code}/imagen", async (
                string code,
                RenderCertificateImageHandler handler,
                HttpContext context,
                CancellationToken ct) =>
            {
                var result = await handler.HandleAsync(code, context.Request.Host.Host, ct);

                return result.ToHttp(png => Results.File(png, "image/png", $"{code}.png"));
            })
            .AllowAnonymous()
            .RequireRateLimiting("verification")
            .WithSummary("Imagen PNG del certificado, para compartirlo en redes.");

        app.MapGet("/api/me/certificates", async (
                GetMyCertificatesHandler handler,
                HttpContext context,
                CancellationToken ct) =>
                Results.Ok(await handler.HandleAsync(context.User.RequireUserId(), ct)))
            .RequireAuthorization()
            .WithTags("Certificados")
            .WithSummary("Certificados del alumno.");

        // Por slug y no por id: es lo que la SPA tiene a mano en todas sus pantallas. Con el id
        // haría falta arrastrar un GUID hasta la ficha del curso solo para este botón.
        app.MapPost("/api/me/certificates/courses/{courseSlug}", async (
                string courseSlug,
                IssueCertificateHandler handler,
                ICourseRepository courses,
                HttpContext context,
                CancellationToken ct) =>
            {
                var slug = Domain.ValueObjects.Slug.Create(courseSlug);
                if (slug.IsFailure)
                {
                    return slug.Error.ToProblem();
                }

                var course = await courses.GetBySlugAsync(slug.Value, ct);
                if (course is null)
                {
                    return Error.NotFound("course.not_found", "No existe ese curso.").ToProblem();
                }

                var result = await handler.HandleAsync(context.User.RequireUserId(), course.Id, ct);
                return result.ToHttp(code => Results.Ok(new { code }));
            })
            .RequireAuthorization()
            .WithTags("Certificados")
            .WithSummary("Emite el certificado del curso si está completo al 100 %.");

        // Emisión desde el panel, para cuando el proceso automático falló: un correo que no
        // salió, un fallo al generar el PDF, un alumno que completó el curso antes de que
        // existiera el certificado. NO salta la comprobación de progreso —eso lo sigue
        // decidiendo el dominio— así que no sirve para regalar un certificado sin cursar.
        app.MapPost("/api/admin/users/{userId:guid}/certificates", async (
                Guid userId,
                IssueCertificateBody body,
                IssueCertificateHandler handler,
                ICourseRepository courses,
                IAuditLogRepository audit,
                HttpContext context,
                IClock clock,
                CancellationToken ct) =>
            {
                // Por slug y no por id: es lo que el panel ya usa para conceder accesos, y un
                // identificador que hay que ir a buscar a la base no lo teclea nadie.
                var slug = Domain.ValueObjects.Slug.Create(body.CourseSlug);
                if (slug.IsFailure)
                {
                    return slug.Error.ToProblem();
                }

                var course = await courses.GetBySlugAsync(slug.Value, ct);
                if (course is null)
                {
                    return Error.NotFound("course.not_found", "No existe ese curso.").ToProblem();
                }

                var courseId = course.Id;
                var result = await handler.HandleAsync(userId, courseId, ct);

                if (result.IsSuccess)
                {
                    await audit.AppendAsync(
                        new AuditEntry(
                            Guid.CreateVersion7(),
                            context.User.RequireUserId(),
                            "certificate.issue_manual",
                            "certificate",
                            result.Value,
                            System.Text.Json.JsonSerializer.Serialize(new { userId, courseId }),
                            clock.UtcNow),
                        ct);
                }

                return result.ToHttp(code => Results.Ok(new { code }));
            })
            .RequireAuthorization("admin")
            .WithTags("Certificados")
            .WithSummary("Emite el certificado de un alumno. Exige el 100 % igual que la vía normal.");

        app.MapPost("/api/me/certificates/program", async (
                IssueProgramCertificateHandler handler,
                HttpContext context,
                CancellationToken ct) =>
            {
                var result = await handler.HandleAsync(context.User.RequireUserId(), ct);
                return result.ToHttp(code => Results.Ok(new { code }));
            })
            .RequireAuthorization()
            .WithTags("Certificados")
            .WithSummary("Emite el certificado de programa al completar los cuatro cursos.");
    }

    public sealed record IssueCertificateBody(string CourseSlug);

    public sealed record PositionBody(string PositionRef);

    public sealed record SubmitQuizBody(IReadOnlyList<QuizAnswerDto> Answers);
}
