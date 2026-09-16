using System.Text.Json;
using Dapper;
using Inkoova.Academy.Application.Abstractions;
using Inkoova.Academy.Domain.Learning;
using Inkoova.Academy.Domain.ValueObjects;

namespace Inkoova.Academy.Infrastructure.Persistence;

public sealed class ProgressRepository(IDbConnectionFactory connections) : IProgressRepository
{
    public async Task<IReadOnlyList<LessonProgress>> GetForCourseAsync(
        Guid userId,
        Guid courseId,
        CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        var rows = await connection.QueryAsync<ProgressRow>(
            """
            SELECT p.user_id, p.lesson_id, p.completed_at, p.last_position_ref, p.updated_at
            FROM lesson_progress p
            JOIN lesson  l ON l.id = p.lesson_id
            JOIN section s ON s.id = l.section_id
            WHERE p.user_id = @userId AND s.course_id = @courseId
            """,
            new { userId, courseId });

        return rows.Select(r => r.ToDomain()).ToList();
    }

    public async Task<LessonProgress?> GetAsync(Guid userId, Guid lessonId, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        var row = await connection.QuerySingleOrDefaultAsync<ProgressRow>(
            """
            SELECT user_id, lesson_id, completed_at, last_position_ref, updated_at
            FROM lesson_progress WHERE user_id = @userId AND lesson_id = @lessonId
            """,
            new { userId, lessonId });

        return row?.ToDomain();
    }

    public async Task<IReadOnlySet<Guid>> GetCompletedLessonIdsAsync(
        Guid userId,
        Guid courseId,
        CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        var ids = await connection.QueryAsync<Guid>(
            """
            SELECT p.lesson_id
            FROM lesson_progress p
            JOIN lesson  l ON l.id = p.lesson_id
            JOIN section s ON s.id = l.section_id
            WHERE p.user_id = @userId AND s.course_id = @courseId AND p.completed_at IS NOT NULL
            """,
            new { userId, courseId });

        return ids.ToHashSet();
    }

    public async Task UpsertAsync(LessonProgress progress, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        await connection.ExecuteAsync(
            """
            INSERT INTO lesson_progress (user_id, lesson_id, completed_at, last_position_ref, updated_at)
            VALUES (@UserId, @LessonId, @CompletedAt, @LastPositionRef, @UpdatedAt)
            ON CONFLICT (user_id, lesson_id) DO UPDATE SET
                completed_at = COALESCE(lesson_progress.completed_at, EXCLUDED.completed_at),
                last_position_ref = EXCLUDED.last_position_ref,
                updated_at = EXCLUDED.updated_at
            """,
            new
            {
                progress.UserId,
                progress.LessonId,
                progress.CompletedAt,
                progress.LastPositionRef,
                progress.UpdatedAt
            });
    }

    /// <summary>
    /// Lessons where students stop: opened (progress row exists) but not completed, and the
    /// next lesson never opened. Feeds the "where do we lose people" panel (T-11).
    /// </summary>
    public async Task<IReadOnlyList<(Guid LessonId, int DropOffCount)>> GetDropOffPointsAsync(
        Guid courseId,
        CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        var rows = await connection.QueryAsync<(Guid LessonId, int DropOffCount)>(
            """
            SELECT p.lesson_id, COUNT(*)::int AS drop_off_count
            FROM lesson_progress p
            JOIN lesson  l ON l.id = p.lesson_id
            JOIN section s ON s.id = l.section_id
            WHERE s.course_id = @courseId AND p.completed_at IS NULL
            GROUP BY p.lesson_id
            ORDER BY drop_off_count DESC
            LIMIT 20
            """,
            new { courseId });

        return rows.ToList();
    }

    private sealed record ProgressRow(
        Guid UserId,
        Guid LessonId,
        DateTimeOffset? CompletedAt,
        string? LastPositionRef,
        DateTimeOffset UpdatedAt)
    {
        public LessonProgress ToDomain() =>
            LessonProgress.Rehydrate(UserId, LessonId, CompletedAt, LastPositionRef, UpdatedAt);
    }
}

public sealed class QuizRepository(IDbConnectionFactory connections) : IQuizRepository
{
    public async Task<Quiz?> GetBySlugAsync(Slug slug, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        var row = await connection.QuerySingleOrDefaultAsync<QuizRow>(
            "SELECT id, slug, title, kind, course_id FROM quiz WHERE slug = @slug",
            new { slug = slug.Value });

        return row is null ? null : await LoadAsync(row, ct);
    }

    public async Task<Quiz?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        var row = await connection.QuerySingleOrDefaultAsync<QuizRow>(
            "SELECT id, slug, title, kind, course_id FROM quiz WHERE id = @id", new { id });

        return row is null ? null : await LoadAsync(row, ct);
    }

    public async Task SaveAsync(Quiz quiz, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        using var transaction = connection.BeginTransaction();

        await connection.ExecuteAsync(
            """
            INSERT INTO quiz (id, slug, title, kind, course_id)
            VALUES (@Id, @Slug, @Title, @Kind, @CourseId)
            ON CONFLICT (id) DO UPDATE SET
                slug = EXCLUDED.slug, title = EXCLUDED.title,
                kind = EXCLUDED.kind, course_id = EXCLUDED.course_id
            """,
            new
            {
                quiz.Id,
                Slug = quiz.Slug.Value,
                quiz.Title,
                Kind = EnumMapping.ToDb(quiz.Kind),
                quiz.CourseId
            },
            transaction);

        var questionIds = quiz.Questions.Select(q => q.Id).ToArray();
        await connection.ExecuteAsync(
            "DELETE FROM quiz_question WHERE quiz_id = @quizId AND NOT (id = ANY(@questionIds))",
            new { quizId = quiz.Id, questionIds },
            transaction);

        foreach (var question in quiz.Questions)
        {
            await connection.ExecuteAsync(
                """
                INSERT INTO quiz_question (id, quiz_id, sort_order, category, text, explanation, options_json)
                VALUES (@Id, @QuizId, @Order, @Category, @Text, @Explanation, @OptionsJson::jsonb)
                ON CONFLICT (id) DO UPDATE SET
                    sort_order = EXCLUDED.sort_order,
                    category = EXCLUDED.category,
                    text = EXCLUDED.text,
                    explanation = EXCLUDED.explanation,
                    options_json = EXCLUDED.options_json
                """,
                new
                {
                    question.Id,
                    question.QuizId,
                    question.Order,
                    question.Category,
                    question.Text,
                    question.Explanation,
                    OptionsJson = JsonSerializer.Serialize(question.Options, JsonHelpers.Options)
                },
                transaction);
        }

        transaction.Commit();
    }

    public async Task<IReadOnlyList<QuizAttempt>> GetAttemptsForUserAsync(Guid userId, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        var rows = await connection.QueryAsync<AttemptRow>(
            """
            SELECT id, quiz_id, user_id, anonymous_key, score, total_questions, verdict, answers_json, taken_at
            FROM quiz_attempt WHERE user_id = @userId ORDER BY taken_at DESC
            """,
            new { userId });

        return rows.Select(r => r.ToDomain()).ToList();
    }

    public async Task<QuizAttempt?> GetAttemptByAnonymousKeyAsync(string anonymousKey, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        var row = await connection.QuerySingleOrDefaultAsync<AttemptRow>(
            """
            SELECT id, quiz_id, user_id, anonymous_key, score, total_questions, verdict, answers_json, taken_at
            FROM quiz_attempt WHERE anonymous_key = @anonymousKey ORDER BY taken_at DESC LIMIT 1
            """,
            new { anonymousKey });

        return row?.ToDomain();
    }

    public async Task<IReadOnlyList<QuizAttempt>> GetAttemptsByAnonymousKeyAsync(
        string anonymousKey,
        CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        var rows = await connection.QueryAsync<AttemptRow>(
            """
            SELECT id, quiz_id, user_id, anonymous_key, score, total_questions, verdict, answers_json, taken_at
            FROM quiz_attempt WHERE anonymous_key = @anonymousKey AND user_id IS NULL
            """,
            new { anonymousKey });

        return rows.Select(r => r.ToDomain()).ToList();
    }

    public async Task SaveAttemptAsync(QuizAttempt attempt, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        await connection.ExecuteAsync(
            """
            INSERT INTO quiz_attempt (id, quiz_id, user_id, anonymous_key, score, total_questions,
                                      verdict, answers_json, taken_at)
            VALUES (@Id, @QuizId, @UserId, @AnonymousKey, @Score, @TotalQuestions,
                    @Verdict, @AnswersJson::jsonb, @TakenAt)
            ON CONFLICT (id) DO UPDATE SET
                user_id = EXCLUDED.user_id,
                anonymous_key = EXCLUDED.anonymous_key
            """,
            new
            {
                attempt.Id,
                attempt.QuizId,
                attempt.UserId,
                attempt.AnonymousKey,
                attempt.Score,
                attempt.TotalQuestions,
                Verdict = EnumMapping.ToDb(attempt.Verdict),
                attempt.AnswersJson,
                attempt.TakenAt
            });
    }

    private async Task<Quiz> LoadAsync(QuizRow row, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        var questionRows = await connection.QueryAsync<QuestionRow>(
            """
            SELECT id, quiz_id, sort_order, category, text, explanation, options_json
            FROM quiz_question WHERE quiz_id = @quizId ORDER BY sort_order
            """,
            new { quizId = row.Id });

        return Quiz.Rehydrate(
            row.Id,
            Domain.ValueObjects.Slug.Create(row.Slug).Value,
            row.Title,
            EnumMapping.FromDb<QuizKind>(row.Kind),
            row.CourseId,
            questionRows.Select(q => q.ToDomain()));
    }

    private sealed record QuizRow(Guid Id, string Slug, string Title, string Kind, Guid? CourseId);

    private sealed record QuestionRow(
        Guid Id,
        Guid QuizId,
        int SortOrder,
        string Category,
        string Text,
        string Explanation,
        string OptionsJson)
    {
        public Question ToDomain() => Question.Rehydrate(
            Id, QuizId, SortOrder, Category, Text, Explanation,
            JsonSerializer.Deserialize<List<QuizOption>>(OptionsJson, JsonHelpers.Options) ?? []);
    }

    private sealed record AttemptRow(
        Guid Id,
        Guid QuizId,
        Guid? UserId,
        string? AnonymousKey,
        int Score,
        int TotalQuestions,
        string Verdict,
        string AnswersJson,
        DateTimeOffset TakenAt)
    {
        public QuizAttempt ToDomain() => QuizAttempt.Rehydrate(
            Id, QuizId, UserId, AnonymousKey, Score, TotalQuestions,
            EnumMapping.FromDb<AdmissionVerdict>(Verdict), AnswersJson, TakenAt);
    }
}

public sealed class CertificateRepository(IDbConnectionFactory connections) : ICertificateRepository
{
    private const string Columns = """
        id, code, scope, user_id, subject_id, issued_at, hash,
        revoked_at, revocation_reason
        """;

    public async Task<Certificate?> GetByCodeAsync(CertificateCode code, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        var row = await connection.QuerySingleOrDefaultAsync<CertificateRow>(
            $"SELECT {Columns} FROM certificate WHERE code = @code", new { code = code.Value });

        return row?.ToDomain();
    }

    public async Task<Certificate?> GetForUserAndSubjectAsync(Guid userId, Guid subjectId, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        var row = await connection.QuerySingleOrDefaultAsync<CertificateRow>(
            $"SELECT {Columns} FROM certificate WHERE user_id = @userId AND subject_id = @subjectId",
            new { userId, subjectId });

        return row?.ToDomain();
    }

    public async Task<IReadOnlyList<Certificate>> GetForUserAsync(Guid userId, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        var rows = await connection.QueryAsync<CertificateRow>(
            $"SELECT {Columns} FROM certificate WHERE user_id = @userId ORDER BY issued_at DESC",
            new { userId });

        return rows.Select(r => r.ToDomain()).ToList();
    }

    public async Task UpsertAsync(Certificate certificate, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        await connection.ExecuteAsync(
            """
            INSERT INTO certificate (id, code, scope, user_id, subject_id, issued_at, hash,
                                     revoked_at, revocation_reason)
            VALUES (@Id, @Code, @Scope, @UserId, @SubjectId, @IssuedAt, @Hash,
                    @RevokedAt, @RevocationReason)
            ON CONFLICT (id) DO UPDATE SET
                revoked_at = EXCLUDED.revoked_at,
                revocation_reason = EXCLUDED.revocation_reason
            """,
            new
            {
                certificate.Id,
                Code = certificate.Code.Value,
                Scope = EnumMapping.ToDb(certificate.Scope),
                certificate.UserId,
                certificate.SubjectId,
                certificate.IssuedAt,
                certificate.Hash,
                certificate.RevokedAt,
                certificate.RevocationReason
            });
    }

    private sealed record CertificateRow(
        Guid Id,
        string Code,
        string Scope,
        Guid UserId,
        Guid SubjectId,
        DateTimeOffset IssuedAt,
        string Hash,
        DateTimeOffset? RevokedAt,
        string? RevocationReason)
    {
        public Certificate ToDomain() => Certificate.Rehydrate(
            Id, CertificateCode.Create(Code).Value, EnumMapping.FromDb<CertificateScope>(Scope),
            UserId, SubjectId, IssuedAt, Hash, RevokedAt, RevocationReason);
    }
}
