using System.Text.Json;
using Inkoova.Academy.Domain.Common;

namespace Inkoova.Academy.Domain.Learning;

/// <summary>
/// One completed run of a quiz. An anonymous visitor can take the admission test before
/// registering, so <see cref="UserId"/> is null until the attempt is claimed (T-08).
/// </summary>
public sealed class QuizAttempt
{
    public Guid Id { get; private set; }
    public Guid QuizId { get; private set; }
    public Guid? UserId { get; private set; }

    /// <summary>Opaque cookie value that lets an anonymous attempt be claimed after signup.</summary>
    public string? AnonymousKey { get; private set; }

    public int Score { get; private set; }
    public int TotalQuestions { get; private set; }
    public AdmissionVerdict Verdict { get; private set; }

    /// <summary>Answers as {questionId: selectedIndex} plus the per-category breakdown.</summary>
    public string AnswersJson { get; private set; }

    public DateTimeOffset TakenAt { get; private set; }

    private QuizAttempt(
        Guid id,
        Guid quizId,
        Guid? userId,
        string? anonymousKey,
        int score,
        int totalQuestions,
        AdmissionVerdict verdict,
        string answersJson,
        DateTimeOffset takenAt)
    {
        Id = id;
        QuizId = quizId;
        UserId = userId;
        AnonymousKey = anonymousKey;
        Score = score;
        TotalQuestions = totalQuestions;
        Verdict = verdict;
        AnswersJson = answersJson;
        TakenAt = takenAt;
    }

    public static Result<QuizAttempt, Error> Record(
        Guid id,
        Guid quizId,
        Guid? userId,
        string? anonymousKey,
        QuizGrade grade,
        IReadOnlyDictionary<Guid, int> answers,
        DateTimeOffset takenAt)
    {
        if (userId is null && string.IsNullOrWhiteSpace(anonymousKey))
        {
            return Error.Validation(
                "quiz_attempt.no_owner",
                "Un intento necesita usuario o clave anónima para poder reclamarse después.");
        }

        var payload = JsonSerializer.Serialize(new
        {
            answers = answers.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value),
            byCategory = grade.ByCategory
        });

        return new QuizAttempt(
            id, quizId, userId, anonymousKey, grade.CorrectCount, grade.TotalQuestions,
            grade.Verdict, payload, takenAt);
    }

    public static QuizAttempt Rehydrate(
        Guid id,
        Guid quizId,
        Guid? userId,
        string? anonymousKey,
        int score,
        int totalQuestions,
        AdmissionVerdict verdict,
        string answersJson,
        DateTimeOffset takenAt) =>
        new(id, quizId, userId, anonymousKey, score, totalQuestions, verdict, answersJson, takenAt);

    /// <summary>Attaches an anonymous attempt to the account created right after it.</summary>
    public Result<Unit, Error> ClaimBy(Guid userId)
    {
        if (UserId is not null)
        {
            return Error.Conflict("quiz_attempt.already_claimed", "El intento ya pertenece a un usuario.");
        }

        UserId = userId;
        AnonymousKey = null;
        return Unit.Value;
    }
}
