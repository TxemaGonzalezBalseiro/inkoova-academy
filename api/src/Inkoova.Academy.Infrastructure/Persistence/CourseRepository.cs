using Dapper;
using Inkoova.Academy.Application.Abstractions;
using Inkoova.Academy.Domain.Catalog;
using Inkoova.Academy.Domain.ValueObjects;

namespace Inkoova.Academy.Infrastructure.Persistence;

/// <summary>
/// Course aggregate persistence. Reads the whole tree in one round trip and rebuilds it in
/// memory: a syllabus is a few dozen rows, and one query beats N+1 for every lesson.
/// </summary>
public sealed class CourseRepository(IDbConnectionFactory connections) : ICourseRepository
{
    private const string SelectCourseColumns = """
        c.id, c.product_id, c.slug, c.title, c.short_description, c.long_description,
        c.cover_image_url, c.status, c.level, c.is_featured, c.is_new, c.created_at, c.published_at
        """;

    public async Task<Course?> GetBySlugAsync(Slug slug, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        var row = await connection.QuerySingleOrDefaultAsync<CourseRow>(
            $"SELECT {SelectCourseColumns} FROM course c WHERE c.slug = @slug",
            new { slug = slug.Value });

        return row is null ? null : await LoadAggregateAsync(row, ct);
    }

    public async Task<Course?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        var row = await connection.QuerySingleOrDefaultAsync<CourseRow>(
            $"SELECT {SelectCourseColumns} FROM course c WHERE c.id = @id",
            new { id });

        return row is null ? null : await LoadAggregateAsync(row, ct);
    }

    public async Task<Course?> GetByProductIdAsync(Guid productId, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        var row = await connection.QuerySingleOrDefaultAsync<CourseRow>(
            $"SELECT {SelectCourseColumns} FROM course c WHERE c.product_id = @productId",
            new { productId });

        return row is null ? null : await LoadAggregateAsync(row, ct);
    }

    public async Task<IReadOnlyList<Course>> GetPublicCatalogAsync(CancellationToken ct) =>
        await LoadManyAsync("WHERE c.status IN ('published', 'comingsoon')", ct);

    public async Task<IReadOnlyList<Course>> GetAllAsync(CancellationToken ct) =>
        await LoadManyAsync(string.Empty, ct);

    public async Task<Lesson?> GetLessonAsync(Guid lessonId, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        var row = await connection.QuerySingleOrDefaultAsync<LessonRow>(
            """
            SELECT id, section_id, sort_order, slug, title, type, duration_minutes,
                   content_ref, is_free_preview, is_required
            FROM lesson WHERE id = @lessonId
            """,
            new { lessonId });

        return row?.ToDomain();
    }

    public async Task<Guid?> GetProductIdForLessonAsync(Guid lessonId, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        return await connection.QuerySingleOrDefaultAsync<Guid?>(
            """
            SELECT c.product_id
            FROM lesson l
            JOIN section s ON s.id = l.section_id
            JOIN course  c ON c.id = s.course_id
            WHERE l.id = @lessonId
            """,
            new { lessonId });
    }

    /// <summary>
    /// Writes the aggregate as a whole. Sections and lessons are upserted; rows that no
    /// longer belong to the course are deleted, which is what makes the importer's
    /// "apply the manifest" operation converge instead of accumulating orphans.
    /// </summary>
    public async Task SaveAsync(Course course, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        using var transaction = connection.BeginTransaction();

        await connection.ExecuteAsync(
            """
            INSERT INTO course (id, product_id, slug, title, short_description, long_description,
                                cover_image_url, status, level, is_featured, is_new, created_at, published_at)
            VALUES (@Id, @ProductId, @Slug, @Title, @ShortDescription, @LongDescription,
                    @CoverImageUrl, @Status, @Level, @IsFeatured, @IsNew, @CreatedAt, @PublishedAt)
            ON CONFLICT (id) DO UPDATE SET
                slug = EXCLUDED.slug,
                title = EXCLUDED.title,
                short_description = EXCLUDED.short_description,
                long_description = EXCLUDED.long_description,
                cover_image_url = EXCLUDED.cover_image_url,
                status = EXCLUDED.status,
                level = EXCLUDED.level,
                is_featured = EXCLUDED.is_featured,
                is_new = EXCLUDED.is_new,
                published_at = EXCLUDED.published_at
            """,
            new
            {
                course.Id,
                course.ProductId,
                Slug = course.Slug.Value,
                course.Title,
                course.ShortDescription,
                course.LongDescription,
                course.CoverImageUrl,
                Status = EnumMapping.ToDb(course.Status),
                Level = EnumMapping.ToDb(course.Level),
                course.IsFeatured,
                course.IsNew,
                course.CreatedAt,
                course.PublishedAt
            },
            transaction);

        var sectionIds = course.Sections.Select(s => s.Id).ToArray();
        await connection.ExecuteAsync(
            "DELETE FROM section WHERE course_id = @courseId AND NOT (id = ANY(@sectionIds))",
            new { courseId = course.Id, sectionIds },
            transaction);

        foreach (var section in course.Sections)
        {
            await connection.ExecuteAsync(
                """
                INSERT INTO section (id, course_id, sort_order, title)
                VALUES (@Id, @CourseId, @Order, @Title)
                ON CONFLICT (id) DO UPDATE SET sort_order = EXCLUDED.sort_order, title = EXCLUDED.title
                """,
                new { section.Id, section.CourseId, section.Order, section.Title },
                transaction);

            var lessonIds = section.Lessons.Select(l => l.Id).ToArray();
            await connection.ExecuteAsync(
                "DELETE FROM lesson WHERE section_id = @sectionId AND NOT (id = ANY(@lessonIds))",
                new { sectionId = section.Id, lessonIds },
                transaction);

            foreach (var lesson in section.Lessons)
            {
                await connection.ExecuteAsync(
                    """
                    INSERT INTO lesson (id, section_id, sort_order, slug, title, type,
                                        duration_minutes, content_ref, is_free_preview, is_required)
                    VALUES (@Id, @SectionId, @Order, @Slug, @Title, @Type,
                            @DurationMinutes, @ContentRef, @IsFreePreview, @IsRequired)
                    ON CONFLICT (id) DO UPDATE SET
                        sort_order = EXCLUDED.sort_order,
                        slug = EXCLUDED.slug,
                        title = EXCLUDED.title,
                        type = EXCLUDED.type,
                        duration_minutes = EXCLUDED.duration_minutes,
                        content_ref = EXCLUDED.content_ref,
                        is_free_preview = EXCLUDED.is_free_preview,
                        is_required = EXCLUDED.is_required
                    """,
                    new
                    {
                        lesson.Id,
                        lesson.SectionId,
                        lesson.Order,
                        Slug = lesson.Slug.Value,
                        lesson.Title,
                        Type = EnumMapping.ToDb(lesson.Type),
                        lesson.DurationMinutes,
                        lesson.ContentRef,
                        lesson.IsFreePreview,
                        lesson.IsRequired
                    },
                    transaction);
            }
        }

        transaction.Commit();
    }

    public async Task DeleteSectionAsync(Guid sectionId, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        await connection.ExecuteAsync("DELETE FROM section WHERE id = @sectionId", new { sectionId });
    }

    public async Task DeleteLessonAsync(Guid lessonId, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        await connection.ExecuteAsync("DELETE FROM lesson WHERE id = @lessonId", new { lessonId });
    }

    private async Task<IReadOnlyList<Course>> LoadManyAsync(string whereClause, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);

        var courseRows = (await connection.QueryAsync<CourseRow>(
            $"SELECT {SelectCourseColumns} FROM course c {whereClause} ORDER BY c.created_at")).ToList();

        if (courseRows.Count == 0)
        {
            return [];
        }

        var courseIds = courseRows.Select(r => r.Id).ToArray();

        var sectionRows = (await connection.QueryAsync<SectionRow>(
            "SELECT id, course_id, sort_order, title FROM section WHERE course_id = ANY(@courseIds) ORDER BY sort_order",
            new { courseIds })).ToList();

        var sectionIds = sectionRows.Select(s => s.Id).ToArray();

        var lessonRows = (await connection.QueryAsync<LessonRow>(
            """
            SELECT id, section_id, sort_order, slug, title, type, duration_minutes,
                   content_ref, is_free_preview, is_required
            FROM lesson WHERE section_id = ANY(@sectionIds) ORDER BY sort_order
            """,
            new { sectionIds })).ToList();

        return courseRows.Select(row => Assemble(row, sectionRows, lessonRows)).ToList();
    }

    private async Task<Course> LoadAggregateAsync(CourseRow row, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);

        var sectionRows = (await connection.QueryAsync<SectionRow>(
            "SELECT id, course_id, sort_order, title FROM section WHERE course_id = @courseId ORDER BY sort_order",
            new { courseId = row.Id })).ToList();

        var sectionIds = sectionRows.Select(s => s.Id).ToArray();

        var lessonRows = (await connection.QueryAsync<LessonRow>(
            """
            SELECT id, section_id, sort_order, slug, title, type, duration_minutes,
                   content_ref, is_free_preview, is_required
            FROM lesson WHERE section_id = ANY(@sectionIds) ORDER BY sort_order
            """,
            new { sectionIds })).ToList();

        return Assemble(row, sectionRows, lessonRows);
    }

    private static Course Assemble(CourseRow row, List<SectionRow> allSections, List<LessonRow> allLessons)
    {
        var sections = allSections
            .Where(s => s.CourseId == row.Id)
            .OrderBy(s => s.SortOrder)
            .Select(s => Section.Rehydrate(
                s.Id, s.CourseId, s.SortOrder, s.Title,
                allLessons.Where(l => l.SectionId == s.Id).OrderBy(l => l.SortOrder).Select(l => l.ToDomain())))
            .ToList();

        return Course.Rehydrate(
            row.Id,
            row.ProductId,
            Slug.Create(row.Slug).Value,
            row.Title,
            row.ShortDescription,
            row.LongDescription,
            row.CoverImageUrl,
            EnumMapping.FromDb<PublicationStatus>(row.Status),
            EnumMapping.FromDb<CourseLevel>(row.Level),
            row.IsFeatured,
            row.IsNew,
            row.CreatedAt,
            row.PublishedAt,
            sections);
    }

    private sealed record CourseRow(
        Guid Id,
        Guid ProductId,
        string Slug,
        string Title,
        string ShortDescription,
        string LongDescription,
        string? CoverImageUrl,
        string Status,
        string Level,
        bool IsFeatured,
        bool IsNew,
        DateTimeOffset CreatedAt,
        DateTimeOffset? PublishedAt);

    private sealed record SectionRow(Guid Id, Guid CourseId, int SortOrder, string Title);

    private sealed record LessonRow(
        Guid Id,
        Guid SectionId,
        int SortOrder,
        string Slug,
        string Title,
        string Type,
        int DurationMinutes,
        string ContentRef,
        bool IsFreePreview,
        bool IsRequired)
    {
        // Qualified: the property named Slug shadows the value object of the same name here.
        public Lesson ToDomain() => Lesson.Rehydrate(
            Id, SectionId, SortOrder, Domain.ValueObjects.Slug.Create(Slug).Value, Title,
            EnumMapping.FromDb<LessonType>(Type), DurationMinutes, ContentRef, IsFreePreview, IsRequired);
    }
}
