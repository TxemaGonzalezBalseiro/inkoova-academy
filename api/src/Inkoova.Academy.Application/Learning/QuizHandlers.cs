using Inkoova.Academy.Application.Abstractions;
using Inkoova.Academy.Domain.Common;
using Inkoova.Academy.Domain.Learning;
using Inkoova.Academy.Domain.ValueObjects;

namespace Inkoova.Academy.Application.Learning;

public sealed record QuizOptionDto(int Index, string Text);

public sealed record QuizQuestionDto(Guid Id, string Category, string Text, IReadOnlyList<QuizOptionDto> Options);

public sealed record QuizDto(string Slug, string Title, string Kind, IReadOnlyList<QuizQuestionDto> Questions);

public sealed record QuizAnswerDto(Guid QuestionId, int SelectedIndex);

public sealed record QuizResultDto(
    int Score,
    int Total,
    string Verdict,
    string Headline,
    string Recommendation,
    IReadOnlyList<QuizCategoryResultDto> ByCategory,
    IReadOnlyList<QuizFeedbackDto> Feedback);

public sealed record QuizCategoryResultDto(string Category, int Correct, int Total);

/// <summary>
/// <see cref="CorrectIndexes"/> es una lista porque una pregunta puede admitir varias
/// respuestas válidas: en el test de nivel, distintos grados de experiencia acreditan
/// igualmente el nivel que el curso asume.
/// </summary>
public sealed record QuizFeedbackDto(
    Guid QuestionId,
    bool WasCorrect,
    IReadOnlyList<int> CorrectIndexes,
    string Explanation);

/// <summary>
/// Serves a quiz without the answers. Correctness is decided server-side so the level test
/// cannot be read off the payload (the original HTML shipped answers to the client).
/// </summary>
public sealed class GetQuizHandler(IQuizRepository quizzes)
{
    public async Task<Result<QuizDto, Error>> HandleAsync(string slug, CancellationToken ct)
    {
        var parsed = Slug.Create(slug);
        if (parsed.IsFailure)
        {
            return parsed.Error;
        }

        var quiz = await quizzes.GetBySlugAsync(parsed.Value, ct);
        if (quiz is null)
        {
            return Error.NotFound("quiz.not_found", "No existe ese cuestionario.");
        }

        var questions = quiz.Questions
            .Select(q => new QuizQuestionDto(
                q.Id,
                q.Category,
                q.Text,
                q.Options.Select(o => new QuizOptionDto(o.Index, o.Text)).ToList()))
            .ToList();

        return new QuizDto(quiz.Slug.Value, quiz.Title, quiz.Kind.ToString().ToLowerInvariant(), questions);
    }
}

/// <summary>
/// Grades an attempt and stores it. Anonymous attempts carry a cookie key so they can be
/// claimed when the visitor registers minutes later (T-08 acceptance criteria).
/// </summary>
public sealed class SubmitQuizHandler(IQuizRepository quizzes, IClock clock)
{
    public async Task<Result<QuizResultDto, Error>> HandleAsync(
        string slug,
        IReadOnlyList<QuizAnswerDto> answers,
        Guid? userId,
        string? anonymousKey,
        CancellationToken ct)
    {
        var parsed = Slug.Create(slug);
        if (parsed.IsFailure)
        {
            return parsed.Error;
        }

        var quiz = await quizzes.GetBySlugAsync(parsed.Value, ct);
        if (quiz is null)
        {
            return Error.NotFound("quiz.not_found", "No existe ese cuestionario.");
        }

        var answerMap = answers
            .GroupBy(a => a.QuestionId)
            .ToDictionary(g => g.Key, g => g.Last().SelectedIndex);

        var grade = quiz.Grade(answerMap);
        if (grade.IsFailure)
        {
            return grade.Error;
        }

        var attempt = QuizAttempt.Record(
            Guid.CreateVersion7(), quiz.Id, userId, anonymousKey, grade.Value, answerMap, clock.UtcNow);

        if (attempt.IsFailure)
        {
            return attempt.Error;
        }

        await quizzes.SaveAttemptAsync(attempt.Value, ct);

        var feedback = quiz.Questions
            .Select(q => new QuizFeedbackDto(
                q.Id,
                answerMap.TryGetValue(q.Id, out var selected) && q.IsCorrectAnswer(selected),
                q.Options.Where(o => o.IsCorrect).Select(o => o.Index).ToList(),
                q.Explanation))
            .ToList();

        var (headline, recommendation) = CopyFor(grade.Value.Verdict);

        return new QuizResultDto(
            grade.Value.CorrectCount,
            grade.Value.TotalQuestions,
            grade.Value.Verdict.ToString().ToLowerInvariant(),
            headline,
            recommendation,
            grade.Value.ByCategory.Select(kv => new QuizCategoryResultDto(kv.Key, kv.Value.Correct, kv.Value.Total)).ToList(),
            feedback);
    }

    /// <summary>Verdict copy kept equivalent to the original <c>nivel.html</c>.</summary>
    private static (string Headline, string Recommendation) CopyFor(AdmissionVerdict verdict) => verdict switch
    {
        AdmissionVerdict.Ready => (
            "Apto",
            "Tienes el nivel que el curso asume. Empieza por el bloque B0. Si alguna dimensión te ha quedado floja, "
            + "pasa antes por el pre-curso: aprovecharás mucho mejor el curso principal."),
        AdmissionVerdict.Almost => (
            "Casi",
            "Te falta base en alguna dimensión. Haz el pre-curso (P0 a P3, gratis) y vuelve a hacer el test: "
            + "son unas pocas horas y te ahorran semanas de frustración."),
        _ => (
            "Ven más tarde",
            "Todavía no es tu momento. Construye durante unos meses con APIs de LLM y vuelve: "
            + "el curso da por sabidas cosas que ahora te harían ir a ciegas.")
    };
}

/// <summary>Attaches attempts taken before signup to the new account (T-08).</summary>
public sealed class ClaimQuizAttemptsHandler(IQuizRepository quizzes)
{
    public async Task<int> HandleAsync(Guid userId, string anonymousKey, CancellationToken ct)
    {
        var attempts = await quizzes.GetAttemptsByAnonymousKeyAsync(anonymousKey, ct);
        var claimed = 0;

        foreach (var attempt in attempts)
        {
            if (attempt.ClaimBy(userId).IsFailure)
            {
                continue;
            }

            await quizzes.SaveAttemptAsync(attempt, ct);
            claimed++;
        }

        return claimed;
    }
}

public sealed record QuizAttemptSummaryDto(string QuizSlug, int Score, int Total, string Verdict, DateTimeOffset TakenAt);

public sealed class GetMyQuizAttemptsHandler(IQuizRepository quizzes)
{
    public async Task<IReadOnlyList<QuizAttemptSummaryDto>> HandleAsync(Guid userId, CancellationToken ct)
    {
        var attempts = await quizzes.GetAttemptsForUserAsync(userId, ct);
        var result = new List<QuizAttemptSummaryDto>(attempts.Count);

        foreach (var attempt in attempts.OrderByDescending(a => a.TakenAt))
        {
            var quiz = await quizzes.GetByIdAsync(attempt.QuizId, ct);
            result.Add(new QuizAttemptSummaryDto(
                quiz?.Slug.Value ?? "desconocido",
                attempt.Score,
                attempt.TotalQuestions,
                attempt.Verdict.ToString().ToLowerInvariant(),
                attempt.TakenAt));
        }

        return result;
    }
}
