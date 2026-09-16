namespace Inkoova.Academy.Application.Content;

/// <summary>
/// Shape produced by <c>content/tools/importer.py</c>. Kept as plain records so the contract
/// between the importer and the platform is a file, not a database migration (ADR-008).
/// </summary>
public sealed record CourseManifest(
    int ManifestVersion,
    string CourseSlug,
    string Title,
    string ShortDescription,
    string LongDescription,
    string Level,
    IReadOnlyList<SectionManifest> Sections,
    IReadOnlyList<QuizManifest> Quizzes,
    IReadOnlyList<string> Assets);

public sealed record SectionManifest(string Title, int Order, IReadOnlyList<LessonManifest> Lessons);

public sealed record LessonManifest(
    string Slug,
    string Title,
    string Type,
    int DurationMinutes,
    string ContentRef,
    bool IsFreePreview,
    bool IsRequired,
    string? Sha256);

public sealed record QuizManifest(
    string Slug,
    string Title,
    string Kind,
    IReadOnlyList<QuestionManifest> Questions);

public sealed record QuestionManifest(
    string Category,
    string Text,
    string Explanation,
    IReadOnlyList<OptionManifest> Options);

public sealed record OptionManifest(int Index, string Text, bool IsCorrect);

/// <summary>What applying a manifest would change. Shown before writing anything (T-11).</summary>
public sealed record ImportDiff(
    string CourseSlug,
    bool CourseExists,
    IReadOnlyList<string> SectionsAdded,
    IReadOnlyList<string> SectionsRemoved,
    IReadOnlyList<string> LessonsAdded,
    IReadOnlyList<string> LessonsRemoved,
    IReadOnlyList<string> LessonsChanged,
    IReadOnlyList<string> QuizzesAffected,
    IReadOnlyList<string> MissingContentFiles,

    /// <summary>
    /// Ficheros con clases gratuitas y de pago mezcladas. El contenido se sirve por fichero
    /// entero —una clase es un ancla dentro de él—, así que la clase gratuita entrega también
    /// las de pago. Se avisa antes de aplicar porque después no se nota: el temario sigue
    /// enseñando el candado y el material ya está servido.
    /// </summary>
    IReadOnlyList<string> MixedAccessFiles)
{
    public bool HasChanges =>
        !CourseExists
        || SectionsAdded.Count > 0
        || SectionsRemoved.Count > 0
        || LessonsAdded.Count > 0
        || LessonsRemoved.Count > 0
        || LessonsChanged.Count > 0;
}
