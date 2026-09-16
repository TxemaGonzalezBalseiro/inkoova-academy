using Inkoova.Academy.Application.Abstractions;
using Inkoova.Academy.Application.Catalog;
using Inkoova.Academy.Domain.Common;
using Inkoova.Academy.Domain.Learning;
using Inkoova.Academy.Domain.ValueObjects;

namespace Inkoova.Academy.Application.Learning;

public sealed record CertificateOptions
{
    /// <summary>Secret that makes the certificate hash unforgeable. Injected from configuration.</summary>
    public required string SigningSecret { get; init; }

    /// <summary>Public base of the verification URL printed as a QR code.</summary>
    public required string PublicBaseUrl { get; init; }
}

/// <summary>
/// Issues the certificate once every required lesson is complete. Idempotent: a second call
/// returns the existing certificate instead of minting a new code (T-10).
///
/// **No se guarda ningún PDF.** El documento se dibuja al descargarlo, así que lo que ve el
/// alumno lleva siempre el diseño y la marca de HOY. Un fichero escrito al emitir se quedaba
/// con el estilo de aquel día: el volumen y la descarga acababan diciendo cosas distintas del
/// mismo certificado.
///
/// Emitir sigue significando lo mismo que antes: nace la fila, con su código y su hash. Eso es
/// lo que se verifica; el PDF es solo una forma de enseñarlo.
/// </summary>
public sealed class IssueCertificateHandler(
    ICourseRepository courses,
    IProgressRepository progress,
    ICertificateRepository certificates,
    IUserRepository users,
    IClock clock,
    CertificateOptions options)
{
    public async Task<Result<string, Error>> HandleAsync(Guid userId, Guid courseId, CancellationToken ct)
    {
        var existing = await certificates.GetForUserAndSubjectAsync(userId, courseId, ct);
        if (existing is not null)
        {
            return existing.Code.Value;
        }

        var course = await courses.GetByIdAsync(courseId, ct);
        if (course is null)
        {
            return Error.NotFound("course.not_found", "No existe ese curso.");
        }

        var user = await users.GetByIdAsync(userId, ct);
        if (user is null)
        {
            return Error.NotFound("user.not_found", "No existe ese usuario.");
        }

        var required = course.RequiredLessons.Select(l => l.Id).ToHashSet();
        var completed = await progress.GetCompletedLessonIdsAsync(userId, courseId, ct);

        var issuedAt = clock.UtcNow;
        var certificate = Certificate.Issue(
            Guid.CreateVersion7(), CertificateScope.Course, userId, courseId,
            required, completed, issuedAt, options.SigningSecret);

        if (certificate.IsFailure)
        {
            return certificate.Error;
        }

        await certificates.UpsertAsync(certificate.Value, ct);

        return certificate.Value.Code.Value;
    }
}

/// <summary>
/// Public verification. Reveals the student name, the course and the date, and nothing else:
/// the endpoint is unauthenticated and rate-limited (T-10).
/// </summary>
/// <summary>
/// PNG del certificado, para enseñarlo donde un PDF no se ve: LinkedIn, un mensaje, una
/// captura. Sale del mismo documento que el PDF, así que lo que se enseña es exactamente lo que
/// se puede verificar.
///
/// No se guarda en disco: una segunda copia del certificado es una copia que puede quedarse
/// vieja, y generarla cuesta menos que mantenerla sincronizada.
/// </summary>
public sealed class RenderCertificateImageHandler(
    ICertificateRepository certificates,
    ICourseRepository courses,
    IProgramRepository programs,
    IUserRepository users,
    IAcademyIdentityRepository identities,
    ICertificateStyleStore styles,
    ICertificatePdfGenerator generator,
    CertificateOptions options)
{
    /// <summary>PNG para compartir.</summary>
    /// <param name="host">
    /// El dominio por el que se está pidiendo. Es lo que decide con qué marca sale el
    /// certificado: cabecera, emisor y colores. Sin él, la principal.
    /// </param>
    public Task<Result<byte[], Error>> HandleAsync(string code, string? host, CancellationToken ct) =>
        RenderAsync(code, host, generator.GenerateImage, ct);

    /// <summary>
    /// PDF para descargar, regenerado a partir del modelo.
    ///
    /// El PDF se guarda en el volumen al emitir, pero servir ESE fichero congela el diseño del
    /// día de la emisión: rediseñar el certificado dejaba fuera a todo el que ya lo tuviera. Se
    /// regenera, que además es barato y no cambia nada comprobable —el hash se calcula sobre
    /// alumno, asunto y fecha, no sobre los bytes del PDF—.
    /// </summary>
    public Task<Result<byte[], Error>> RenderPdfAsync(string code, string? host, CancellationToken ct) =>
        RenderAsync(code, host, generator.Generate, ct);

    private async Task<Result<byte[], Error>> RenderAsync(
        string code,
        string? host,
        Func<CertificatePdfModel, byte[]> render,
        CancellationToken ct)
    {
        var parsed = CertificateCode.Create(code);
        if (parsed.IsFailure)
        {
            return NotFound;
        }

        var certificate = await certificates.GetByCodeAsync(parsed.Value, ct);

        // La firma se comprueba igual que en la verificación pública: sin ella, una fila
        // manipulada en la base produciría una imagen con aspecto de certificado válido.
        if (certificate is null || !certificate.Verify(options.SigningSecret) || !certificate.IsValid)
        {
            return NotFound;
        }

        var user = await users.GetByIdAsync(certificate.UserId, ct);
        if (user is null)
        {
            return NotFound;
        }

        var course = certificate.Scope == CertificateScope.Course
            ? await courses.GetByIdAsync(certificate.SubjectId, ct)
            : null;

        var subject = course?.Title ?? (await programs.GetDefaultAsync(ct))?.Title ?? "Programa";

        // La marca la pone el DOMINIO por el que se pide, no el curso: el catálogo es común a
        // todas las marcas, y quien entra por el dominio de una espera su certificado, con su
        // cabecera, su emisor y sus colores.
        var identity = await identities.GetForHostAsync(host, ct);

        var style = await styles.GetAsync(identity.Id, ct);

        return render(new CertificatePdfModel(
            user.DisplayName,
            subject,
            certificate.Code.Value,
            certificate.Hash,
            DateOnly.FromDateTime(certificate.IssuedAt.UtcDateTime),
            course?.TotalHours ?? 0,
            // La MISMA ruta en el PDF y en la imagen. Con otra, el QR llevaría a un 404 y el
            // certificado que alguien enseña no se podría comprobar.
            $"{options.PublicBaseUrl.TrimEnd('/')}/check-certificate/{certificate.Code.Value}",
            style));
    }

    /// <summary>
    /// Mismo error para "no existe", "firma inválida" y "revocado": distinguirlos diría a
    /// cualquiera si un código existe, que es justo lo que no debe filtrar un endpoint público.
    /// </summary>
    private static readonly Error NotFound =
        Error.NotFound("certificate.not_found", "No existe ese certificado.");
}

public sealed class VerifyCertificateHandler(
    ICertificateRepository certificates,
    ICourseRepository courses,
    IProgramRepository programs,
    IUserRepository users,
    CertificateOptions options)
{
    public async Task<CertificateVerificationDto> HandleAsync(string code, CancellationToken ct)
    {
        var parsed = CertificateCode.Create(code);
        if (parsed.IsFailure)
        {
            return new CertificateVerificationDto(false, null, null, null, null);
        }

        var certificate = await certificates.GetByCodeAsync(parsed.Value, ct);
        if (certificate is null || !certificate.Verify(options.SigningSecret))
        {
            return new CertificateVerificationDto(false, null, null, null, null);
        }

        if (!certificate.IsValid)
        {
            return new CertificateVerificationDto(false, null, null, null, certificate.RevocationReason);
        }

        var user = await users.GetByIdAsync(certificate.UserId, ct);

        var subject = certificate.Scope == CertificateScope.Course
            ? (await courses.GetByIdAsync(certificate.SubjectId, ct))?.Title
            : (await programs.GetDefaultAsync(ct))?.Title;

        return new CertificateVerificationDto(
            true,
            user?.DisplayName,
            subject,
            DateOnly.FromDateTime(certificate.IssuedAt.UtcDateTime),
            null);
    }
}

public sealed record MyCertificateDto(string Code, string Subject, DateOnly IssuedOn, bool IsValid, string ShareUrl);

public sealed class GetMyCertificatesHandler(
    ICertificateRepository certificates,
    ICourseRepository courses,
    IProgramRepository programs,
    CertificateOptions options)
{
    public async Task<IReadOnlyList<MyCertificateDto>> HandleAsync(Guid userId, CancellationToken ct)
    {
        var mine = await certificates.GetForUserAsync(userId, ct);
        var result = new List<MyCertificateDto>(mine.Count);

        foreach (var certificate in mine)
        {
            var subject = certificate.Scope == CertificateScope.Course
                ? (await courses.GetByIdAsync(certificate.SubjectId, ct))?.Title
                : (await programs.GetDefaultAsync(ct))?.Title;

            result.Add(new MyCertificateDto(
                certificate.Code.Value,
                subject ?? "Curso",
                DateOnly.FromDateTime(certificate.IssuedAt.UtcDateTime),
                certificate.IsValid,
                $"{options.PublicBaseUrl.TrimEnd('/')}/check-certificate/{certificate.Code.Value}"));
        }

        return result;
    }
}

/// <summary>
/// Issues the program certificate when every course of the program is complete (C-21).
/// Completion of a course is defined by that course already having a certificate.
/// </summary>
public sealed class IssueProgramCertificateHandler(
    IProgramRepository programs,
    ICertificateRepository certificates,
    ICourseRepository courses,
    IProgressRepository progress,
    IUserRepository users,
    IClock clock,
    CertificateOptions options)
{
    public async Task<Result<string, Error>> HandleAsync(Guid userId, CancellationToken ct)
    {
        var program = await programs.GetDefaultAsync(ct);
        if (program is null)
        {
            return Error.NotFound("program.not_found", "No hay programa configurado.");
        }

        var existing = await certificates.GetForUserAndSubjectAsync(userId, program.Id, ct);
        if (existing is not null)
        {
            return existing.Code.Value;
        }

        // The program is complete when every one of its courses has all required lessons done.
        var requiredAcrossProgram = new HashSet<Guid>();
        var completedAcrossProgram = new HashSet<Guid>();

        foreach (var item in program.Items)
        {
            var course = await courses.GetByIdAsync(item.CourseId, ct);
            if (course is null)
            {
                continue;
            }

            foreach (var lesson in course.RequiredLessons)
            {
                requiredAcrossProgram.Add(lesson.Id);
            }

            foreach (var lessonId in await progress.GetCompletedLessonIdsAsync(userId, course.Id, ct))
            {
                completedAcrossProgram.Add(lessonId);
            }
        }

        var user = await users.GetByIdAsync(userId, ct);
        if (user is null)
        {
            return Error.NotFound("user.not_found", "No existe ese usuario.");
        }

        var issuedAt = clock.UtcNow;
        var certificate = Certificate.Issue(
            Guid.CreateVersion7(), CertificateScope.Program, userId, program.Id,
            requiredAcrossProgram, completedAcrossProgram, issuedAt, options.SigningSecret);

        if (certificate.IsFailure)
        {
            return certificate.Error;
        }

        await certificates.UpsertAsync(certificate.Value, ct);

        return certificate.Value.Code.Value;
    }
}
