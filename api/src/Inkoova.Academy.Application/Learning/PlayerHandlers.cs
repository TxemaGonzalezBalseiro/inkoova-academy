using Inkoova.Academy.Application.Abstractions;
using Inkoova.Academy.Domain.Catalog;
using Inkoova.Academy.Domain.Common;
using Inkoova.Academy.Domain.ValueObjects;

namespace Inkoova.Academy.Application.Learning;

public sealed record PlayerLessonDto(
    Guid LessonId,
    string Slug,
    string Title,
    string Type,
    int DurationMinutes,

    /// <summary>Short-lived token that stands in for the content path (ADR-003).</summary>
    string ContentToken,

    int ContentTokenSecondsToLive,

    /// <summary>
    /// Ancla dentro del documento, sin la almohadilla. Los cursos que viven en un único HTML
    /// referencian cada lección con un fragmento; el player lo añade al src del iframe.
    /// Null cuando la lección es un fichero completo.
    /// </summary>
    string? ContentFragment,
    string? PreviousLessonSlug,
    string? NextLessonSlug,
    string? LastPositionRef,
    bool Completed);

/// <summary>
/// Serves one lesson to the player. Issues the content token here, at the moment access is
/// checked, so a token can never outlive the decision that produced it by more than its TTL.
/// </summary>
public sealed class GetPlayerLessonHandler(
    ICourseRepository courses,
    IProgressRepository progress,
    IAccessPolicy accessPolicy,
    IContentTokenService tokens)
{
    /// <summary>
    /// 60 seconds is enough for the browser to start the request and short enough that a
    /// leaked token is worthless. The player refreshes on 403 (T-07).
    /// </summary>
    public static readonly TimeSpan TokenLifetime = TimeSpan.FromSeconds(60);

    public async Task<Result<PlayerLessonDto, Error>> HandleAsync(
        string courseSlug,
        string lessonSlug,
        Guid? userId,
        CancellationToken ct)
    {
        var parsedCourse = Slug.Create(courseSlug);
        if (parsedCourse.IsFailure)
        {
            return parsedCourse.Error;
        }

        var course = await courses.GetBySlugAsync(parsedCourse.Value, ct);
        if (course is null || !course.IsVisibleToPublic)
        {
            return Error.NotFound("course.not_found", "No existe un curso con ese identificador.");
        }

        var ordered = course.AllLessons.ToList();
        var index = ordered.FindIndex(l => l.Slug.Value == lessonSlug);
        if (index < 0)
        {
            return Error.NotFound("lesson.not_found", "No existe esa lección en el curso.");
        }

        var lesson = ordered[index];

        if (!await accessPolicy.CanAccessLessonAsync(userId, lesson, ct))
        {
            return Error.Forbidden(
                "lesson.no_access",
                "Esta lección es solo para suscriptores. Elige un plan para continuar.");
        }

        var token = tokens.Issue(lesson.ContentRef, TokenLifetime);

        string? lastPosition = null;
        var completed = false;
        if (userId is { } uid)
        {
            var row = await progress.GetAsync(uid, lesson.Id, ct);
            lastPosition = row?.LastPositionRef;
            completed = row?.IsCompleted ?? false;
        }

        var hash = lesson.ContentRef.IndexOf('#', StringComparison.Ordinal);

        return new PlayerLessonDto(
            lesson.Id,
            lesson.Slug.Value,
            lesson.Title,
            lesson.Type.ToString().ToLowerInvariant(),
            lesson.DurationMinutes,
            token,
            (int)TokenLifetime.TotalSeconds,
            hash >= 0 ? lesson.ContentRef[(hash + 1)..] : null,
            index > 0 ? ordered[index - 1].Slug.Value : null,
            index < ordered.Count - 1 ? ordered[index + 1].Slug.Value : null,
            lastPosition,
            completed);
    }
}

public sealed record DownloadableFileDto(Guid FileId, string FileName, long SizeInBytes, string Version);

public sealed record PackDownloadDto(string PackSlug, string Version, IReadOnlyList<DownloadableFileDto> Files);

/// <summary>Lists the files of a pack the student may download (T-07 addendum).</summary>
public sealed class GetPackDownloadsHandler(
    IPackRepository packs,
    IAccessPolicy accessPolicy)
{
    public async Task<Result<PackDownloadDto, Error>> HandleAsync(string packSlug, Guid userId, CancellationToken ct)
    {
        var slug = Slug.Create(packSlug);
        if (slug.IsFailure)
        {
            return slug.Error;
        }

        var pack = await packs.GetBySlugAsync(slug.Value, ct);
        if (pack is null || pack.Status != PublicationStatus.Published)
        {
            return Error.NotFound("pack.not_found", "No existe ese pack.");
        }

        if (!await accessPolicy.CanAccessProductAsync(userId, pack.ProductId, ct))
        {
            return Error.Forbidden("pack.no_access", "Este pack no está incluido en tu plan.");
        }

        var files = pack.Files
            .Select(f => new DownloadableFileDto(f.Id, f.FileName, f.SizeInBytes, pack.Version))
            .ToList();

        return new PackDownloadDto(pack.Slug.Value, pack.Version, files);
    }
}

/// <summary>
/// Issues a 60-second token for one pack file and records the download. The log is what
/// makes the "new version available" email possible (T-07 addendum, T-11).
/// </summary>
public sealed class RequestPackFileDownloadHandler(
    IPackRepository packs,
    IAccessPolicy accessPolicy,
    IDownloadLogRepository downloads,
    IContentTokenService tokens,
    IClock clock)
{
    public async Task<Result<(string Token, string FileName), Error>> HandleAsync(
        Guid userId,
        Guid fileId,
        CancellationToken ct)
    {
        var file = await packs.GetFileAsync(fileId, ct);
        if (file is null)
        {
            return Error.NotFound("pack_file.not_found", "No existe ese fichero.");
        }

        var pack = await packs.GetByIdAsync(file.PackId, ct);
        if (pack is null)
        {
            return Error.NotFound("pack.not_found", "No existe ese pack.");
        }

        if (!await accessPolicy.CanAccessProductAsync(userId, pack.ProductId, ct))
        {
            return Error.Forbidden("pack.no_access", "Este pack no está incluido en tu plan.");
        }

        await downloads.AppendAsync(
            new DownloadLogEntry(Guid.CreateVersion7(), userId, file.Id, pack.Version, clock.UtcNow), ct);

        return (tokens.Issue(file.ContentRef, TimeSpan.FromSeconds(60)), file.FileName);
    }
}
