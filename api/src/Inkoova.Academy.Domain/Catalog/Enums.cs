namespace Inkoova.Academy.Domain.Catalog;

/// <summary>Anything that can be sold or granted. See ADR-007.</summary>
public enum ProductType
{
    Course,
    Pack,
    Program
}

public enum PublicationStatus
{
    Draft,
    Published,
    ComingSoon
}

public enum CourseLevel
{
    Intro,
    Intermediate,
    Advanced
}

public enum LessonType
{
    /// <summary>Imported HTML deck rendered inside a sandboxed iframe.</summary>
    Slides,

    /// <summary>Reserved for recorded classes. Not produced yet; see BACKLOG.</summary>
    Video,

    /// <summary>Markdown lab rendered by the platform.</summary>
    Lab,

    /// <summary>Quiz rendered by the shared React quiz component.</summary>
    Quiz,

    /// <summary>Downloadable files (sector packs). Signed URL of 60 s per file.</summary>
    Download
}
