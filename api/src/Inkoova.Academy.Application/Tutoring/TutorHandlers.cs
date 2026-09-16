using Inkoova.Academy.Application.Abstractions;
using Inkoova.Academy.Domain.Common;
using Inkoova.Academy.Domain.Tutoring;

namespace Inkoova.Academy.Application.Tutoring;

/// <summary>
/// Los nombres de los profesores por identificador, para poder enseñar «quién dio esta tutoría»
/// sin obligar a cada pantalla a cargarse la lista entera y cruzarla a mano.
///
/// No cachea: son unas pocas filas y una lista cacheada enseñaría durante minutos el nombre
/// antiguo de un profesor que acaba de cambiar.
/// </summary>
public sealed class TutorNames(ITutorRepository tutors)
{
    public async Task<IReadOnlyDictionary<Guid, string>> AllAsync(CancellationToken ct) =>
        (await tutors.GetAllAsync(ct)).ToDictionary(t => t.Id, t => t.DisplayName);
}

public sealed record TutorDto(
    Guid Id,
    Guid? UserId,
    string DisplayName,
    string Email,
    string Bio,
    decimal CommissionPercent,
    bool IsActive,
    // Lo devengado y todavía sin pagar, en céntimos. Es lo que se le debe hoy.
    long PendingCents,
    long PaidCents,
    string Currency,
    int SessionsGiven,
    int MinutesGiven);

public sealed record TutorEarningDto(
    Guid Id,
    Guid SessionId,
    string StudentName,
    DateTimeOffset OccurredAt,
    int Minutes,
    string Topic,
    long BaseCents,
    decimal Percent,
    long AmountCents,
    string Currency,
    bool IsPaid,
    DateTimeOffset? PaidAt,
    string PayoutNote);

public sealed record TutorDetailDto(TutorDto Tutor, IReadOnlyList<TutorEarningDto> Earnings);

public sealed record SaveTutorRequest(
    Guid? UserId,
    string DisplayName,
    string Email,
    string Bio,
    decimal CommissionPercent,
    bool IsActive);

internal static class TutorMapping
{
    public static TutorEarningDto ToDto(this TutorEarningRow r) =>
        new(r.Id, r.SessionId, r.StudentName, r.OccurredAt, r.Minutes, r.Topic,
            r.BaseCents, r.Percent, r.AmountCents, r.Currency, r.IsPaid, r.PaidAt, r.PayoutNote);

    /// <summary>
    /// La ficha con sus totales. Se calculan sobre lo devengado y no se guardan en ninguna
    /// columna: un contador guardado se desincroniza en cuanto se deshace una tutoría, y aquí
    /// el descuadre sería dinero.
    /// </summary>
    public static TutorDto ToDto(this Tutor t, IReadOnlyList<TutorEarningRow> earnings) =>
        new(t.Id, t.UserId, t.DisplayName, t.Email, t.Bio, t.CommissionPercent, t.IsActive,
            earnings.Where(e => !e.IsPaid).Sum(e => e.AmountCents),
            earnings.Where(e => e.IsPaid).Sum(e => e.AmountCents),
            // La moneda sale de lo devengado; si aún no ha dado nada, EUR, que es en lo que se
            // vende. Inventar otra sería peor que quedarse corto.
            earnings.Count > 0 ? earnings[0].Currency : "EUR",
            earnings.Count,
            earnings.Sum(e => e.Minutes));
}

public sealed class ListTutorsHandler(ITutorRepository tutors, ITutorEarningRepository earnings)
{
    public async Task<IReadOnlyList<TutorDto>> HandleAsync(CancellationToken ct)
    {
        var all = await tutors.GetAllAsync(ct);
        if (all.Count == 0)
        {
            return [];
        }

        // Lo devengado por TODOS en una consulta, no una por profesor: la lista del panel no debe
        // costar N viajes a la base. Y todo, no solo lo pendiente: la ficha enseña también lo ya
        // pagado, y con media lista ese total saldría en cero como si nunca se le hubiera pagado.
        var byTutor = (await earnings.GetAllAsync(ct)).ToLookup(e => e.TutorId);

        return [.. all.Select(t => t.ToDto([.. byTutor[t.Id]]))];
    }
}

public sealed class GetTutorHandler(ITutorRepository tutors, ITutorEarningRepository earnings)
{
    public async Task<Result<TutorDetailDto, Error>> HandleAsync(Guid id, CancellationToken ct)
    {
        var tutor = await tutors.GetByIdAsync(id, ct);
        if (tutor is null)
        {
            return Error.NotFound("tutor.not_found", "No existe ese profesor.");
        }

        var mine = await earnings.GetForTutorAsync(id, ct);

        return new TutorDetailDto(tutor.ToDto(mine), [.. mine.Select(e => e.ToDto())]);
    }
}

public sealed class SaveTutorHandler(ITutorRepository tutors, ITutorEarningRepository earnings)
{
    public async Task<Result<TutorDto, Error>> HandleAsync(
        Guid? id, SaveTutorRequest request, CancellationToken ct)
    {
        var email = request.Email ?? string.Empty;

        // El correo identifica al profesor: es por donde se le convoca y por donde se le liquida.
        // Dos fichas con el mismo correo repartirían sus horas entre dos liquidaciones distintas.
        var byEmail = await tutors.GetByEmailAsync(email, ct);
        if (byEmail is not null && byEmail.Id != id)
        {
            return Error.Conflict("tutor.email_taken", "Ya hay un profesor con ese correo.");
        }

        Tutor tutor;

        if (id is { } existingId)
        {
            var existing = await tutors.GetByIdAsync(existingId, ct);
            if (existing is null)
            {
                return Error.NotFound("tutor.not_found", "No existe ese profesor.");
            }

            var described = existing.Describe(
                request.DisplayName, email, request.Bio, request.CommissionPercent);

            if (described.IsFailure)
            {
                return described.Error;
            }

            existing.LinkUser(request.UserId);
            existing.SetActive(request.IsActive);
            tutor = existing;
        }
        else
        {
            var created = Tutor.Create(
                Guid.CreateVersion7(), request.UserId, request.DisplayName,
                email, request.Bio, request.CommissionPercent);

            if (created.IsFailure)
            {
                return created.Error;
            }

            tutor = created.Value;
            tutor.SetActive(request.IsActive);
        }

        await tutors.UpsertAsync(tutor, ct);

        return tutor.ToDto([.. await earnings.GetForTutorAsync(tutor.Id, ct)]);
    }
}

public sealed record PayTutorEarningsRequest(IReadOnlyList<Guid> EarningIds, string Note);

/// <summary>
/// Marca como pagado lo que se le ha liquidado a un profesor.
///
/// No mueve dinero: la transferencia se hace fuera y aquí solo se apunta que ya está hecha. Por
/// eso hace falta decir cuáles, y no «todo lo pendiente»: se paga lo que se pagó, y un botón de
/// «todo» acabaría marcando como saldado algo que la transferencia no incluyó.
/// </summary>
public sealed class PayTutorEarningsHandler(
    ITutorRepository tutors,
    ITutorEarningRepository earnings,
    IClock clock)
{
    public async Task<Result<TutorDetailDto, Error>> HandleAsync(
        Guid tutorId, PayTutorEarningsRequest request, CancellationToken ct)
    {
        var tutor = await tutors.GetByIdAsync(tutorId, ct);
        if (tutor is null)
        {
            return Error.NotFound("tutor.not_found", "No existe ese profesor.");
        }

        var now = clock.UtcNow;

        foreach (var earningId in request.EarningIds ?? [])
        {
            var earning = await earnings.GetByIdAsync(earningId, ct);
            if (earning is null)
            {
                return Error.NotFound("tutor_earning.not_found", "No existe uno de los devengos.");
            }

            // Que el devengo sea de OTRO profesor no es un detalle: marcarlo aquí saldaría lo de
            // un tercero con una transferencia que no era suya.
            if (earning.TutorId != tutorId)
            {
                return Error.Validation(
                    "tutor_earning.other_tutor",
                    "Uno de los devengos no es de este profesor.");
            }

            var paid = earning.MarkPaid(now, request.Note);
            if (paid.IsFailure)
            {
                return paid.Error;
            }

            await earnings.SaveAsync(earning, ct);
        }

        var mine = await earnings.GetForTutorAsync(tutorId, ct);

        return new TutorDetailDto(tutor.ToDto(mine), [.. mine.Select(e => e.ToDto())]);
    }
}
