using Inkoova.Academy.Domain.Common;

namespace Inkoova.Academy.Domain.Learning;

/// <summary>
/// One row per (user, lesson). <see cref="LastPositionRef"/> is opaque to the platform:
/// the imported deck decides what it means (slide hash, scroll anchor), which keeps the
/// postMessage contract from leaking content structure into the database.
/// </summary>
public sealed class LessonProgress
{
    public Guid UserId { get; private set; }
    public Guid LessonId { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public string? LastPositionRef { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private LessonProgress(
        Guid userId,
        Guid lessonId,
        DateTimeOffset? completedAt,
        string? lastPositionRef,
        DateTimeOffset updatedAt)
    {
        UserId = userId;
        LessonId = lessonId;
        CompletedAt = completedAt;
        LastPositionRef = lastPositionRef;
        UpdatedAt = updatedAt;
    }

    public static LessonProgress Start(Guid userId, Guid lessonId, DateTimeOffset now) =>
        new(userId, lessonId, completedAt: null, lastPositionRef: null, now);

    public static LessonProgress Rehydrate(
        Guid userId,
        Guid lessonId,
        DateTimeOffset? completedAt,
        string? lastPositionRef,
        DateTimeOffset updatedAt) =>
        new(userId, lessonId, completedAt, lastPositionRef, updatedAt);

    /// <summary>Idempotent: re-completing keeps the original timestamp so streaks stay honest.</summary>
    public void Complete(DateTimeOffset now)
    {
        CompletedAt ??= now;
        UpdatedAt = now;
    }

    public void Reopen(DateTimeOffset now)
    {
        CompletedAt = null;
        UpdatedAt = now;
    }

    public Result<Unit, Error> UpdatePosition(string positionRef, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(positionRef))
        {
            return Error.Validation("progress.position_empty", "La posición no puede estar vacía.");
        }

        if (positionRef.Length > 200)
        {
            return Error.Validation("progress.position_too_long", "La posición supera los 200 caracteres permitidos.");
        }

        LastPositionRef = positionRef;
        UpdatedAt = now;
        return Unit.Value;
    }

    public bool IsCompleted => CompletedAt is not null;
}
