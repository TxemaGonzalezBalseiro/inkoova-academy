using Inkoova.Academy.Domain.Tutoring;

namespace Inkoova.Academy.Application.Abstractions;

public interface ITutorRepository
{
    Task<IReadOnlyList<Tutor>> GetAllAsync(CancellationToken ct);

    Task<Tutor?> GetByIdAsync(Guid id, CancellationToken ct);

    Task<Tutor?> GetByEmailAsync(string email, CancellationToken ct);

    Task UpsertAsync(Tutor tutor, CancellationToken ct);
}

/// <summary>
/// Lo devengado por cada tutoría, con lo que hace falta para explicarlo: de qué alumno, de qué
/// bolsa y cuándo. Sin ese contexto, una liquidación es una lista de importes que nadie puede
/// comprobar tres meses después.
/// </summary>
public sealed record TutorEarningRow(
    Guid Id,
    Guid TutorId,
    Guid SessionId,
    long BaseCents,
    decimal Percent,
    long AmountCents,
    string Currency,
    bool IsPaid,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PaidAt,
    string PayoutNote,
    DateTimeOffset OccurredAt,
    int Minutes,
    string Topic,
    string StudentName);

public interface ITutorEarningRepository
{
    Task<IReadOnlyList<TutorEarningRow>> GetForTutorAsync(Guid tutorId, CancellationToken ct);

    /// <summary>Lo devengado por todos, para pintar la lista del panel sin abrirlos uno por uno.</summary>
    Task<IReadOnlyList<TutorEarningRow>> GetAllAsync(CancellationToken ct);

    Task<TutorEarning?> GetByIdAsync(Guid id, CancellationToken ct);

    Task SaveAsync(TutorEarning earning, CancellationToken ct);

    /// <summary>
    /// Lo devengado por una tutoría, si lo hay. Se consulta antes de apuntar para no devengar
    /// dos veces la misma clase, y al deshacerla para saber si ya estaba pagada.
    /// </summary>
    Task<TutorEarning?> GetBySessionAsync(Guid sessionId, CancellationToken ct);
}
