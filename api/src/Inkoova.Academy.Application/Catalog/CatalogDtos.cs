namespace Inkoova.Academy.Application.Catalog;

/// <summary>Catalogue card. Everything the landing grid needs, nothing it does not (T-05).</summary>
public sealed record CourseCardDto(
    string Slug,
    string Title,
    string ShortDescription,
    string? CoverImageUrl,
    string Status,
    string Level,
    bool IsFeatured,
    bool IsNew,
    int Hours,
    int LessonCount,
    int SectionCount,
    decimal? Rating,
    int? RatingCount,
    bool MembersOnly);

public sealed record LessonDto(
    string Slug,
    string Title,
    string Type,
    int DurationMinutes,
    bool IsFreePreview,
    bool IsRequired,
    bool HasAccess,

    /// <summary>Null unless <see cref="HasAccess"/>; never serialised otherwise (T-02, T-06).</summary>
    string? ContentRef);

public sealed record SectionDto(string Title, int Order, IReadOnlyList<LessonDto> Lessons);

public sealed record CourseDetailDto(
    string Slug,
    string Title,
    string ShortDescription,
    string LongDescription,
    string? CoverImageUrl,
    string Status,
    string Level,
    int Hours,
    int LessonCount,
    int SectionCount,
    bool IsNew,
    bool IsFeatured,
    IReadOnlyList<SectionDto> Sections,

    /// <summary>Slug of the admission quiz gating this course, when it has one (T-08).</summary>
    string? AdmissionQuizSlug,

    /// <summary>Only for signed-in students: how far they are and where to resume (T-06).</summary>
    CourseProgressDto? Progress);

public sealed record CourseProgressDto(
    int CompletedLessons,
    int TotalRequiredLessons,
    int PercentComplete,
    string? NextLessonSlug,
    string? NextLessonTitle,
    bool IsComplete,
    string? CertificateCode,
    /// <summary>
    /// Si el alumno puede emitir ya su certificado: curso completo, sin certificado todavía y
    /// con acceso a todo lo que ha dado por completado.
    /// </summary>
    bool CanIssueCertificate);

public sealed record PlanDto(
    string Code,
    string Name,
    string Interval,
    decimal Price,
    string Currency,

    /// <summary>
    /// Ventajas escritas a mano: Discord, sesiones grupales, prioridad en soporte. Lo que NO es
    /// un producto. Los cursos y los packs ya no se teclean aquí —salen de lo que incluye el
    /// plan— porque una lista escrita a mano se desincroniza del acceso real y entonces la
    /// página de precios promete una cosa y la plataforma da otra.
    /// </summary>
    IReadOnlyList<string> Benefits,

    bool IncludesPacks,
    int DisplayOrder,

    /// <summary>Si el plan sigue el catálogo entero, incluido lo que se publique después.</summary>
    bool IncludesAllCourses,
    bool IncludesAllPacks,

    /// <summary>Los productos marcados uno a uno, para pintarlos con su nombre.</summary>
    IReadOnlyList<PlanProductDto> IncludedProducts);

/// <summary>Un producto que entra en un plan, con lo justo para enseñarlo y enlazarlo.</summary>
public sealed record PlanProductDto(string Slug, string Title, string Kind);

public sealed record PackFileDto(Guid Id, string FileName, long SizeInBytes, int Order);

public sealed record PackDto(
    string Slug,
    string Title,
    string Sector,
    string Version,
    string? Changelog,
    DateOnly? RegulatoryCheckDate,
    decimal? Price,
    string? Currency,
    bool HasAccess,
    string AccessReason,
    IReadOnlyList<PackFileDto> Files);

public sealed record CertificateVerificationDto(
    bool IsValid,
    string? StudentName,
    string? Subject,
    DateOnly? IssuedOn,
    string? RevocationReason);
