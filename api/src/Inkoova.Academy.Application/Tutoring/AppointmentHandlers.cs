using Inkoova.Academy.Application.Abstractions;
using Inkoova.Academy.Domain.Common;
using Inkoova.Academy.Domain.Tutoring;

namespace Inkoova.Academy.Application.Tutoring;

public sealed record TutoringAppointmentDto(
    Guid Id,
    Guid GrantId,
    Guid? TutorId,
    string? TutorName,
    DateTimeOffset StartsAt,
    int Minutes,
    string Topic,
    string Notes,
    string Location,
    // 'scheduled', 'done' o 'cancelled'.
    string Status,
    Guid? SessionId,
    string CancelledReason);

public sealed record ScheduleTutoringRequest(
    Guid GrantId,
    Guid? TutorId,
    DateTimeOffset StartsAt,
    int Minutes,
    string Topic,
    string Notes,
    string Location);

internal static class AppointmentMapping
{
    public static string StatusText(AppointmentStatus status) => status switch
    {
        AppointmentStatus.Done => "done",
        AppointmentStatus.Cancelled => "cancelled",
        _ => "scheduled"
    };

    public static TutoringAppointmentDto ToDto(
        this TutoringAppointment a, IReadOnlyDictionary<Guid, string>? tutorNames = null) =>
        new(a.Id, a.GrantId, a.TutorId,
            a.TutorId is { } id && tutorNames?.TryGetValue(id, out var name) == true ? name : null,
            a.StartsAt, a.Minutes, a.Topic, a.Notes, a.Location,
            StatusText(a.Status), a.SessionId, a.CancelledReason);
}

/// <summary>
/// Convoca una tutoría y avisa por calendario a quien va.
///
/// No descuenta saldo: convocar no es dar. Sí comprueba que la bolsa esté viva y que quede
/// tiempo, porque convocar contra una bolsa agotada es citar a alguien a algo que no va a poder
/// apuntarse después.
/// </summary>
public sealed class ScheduleTutoringHandler(
    ITutoringGrantRepository grants,
    ITutoringAppointmentRepository appointments,
    ITutorRepository tutors,
    ITutoringInvitationSender invitations,
    IUserRepository users,
    IAcademyIdentityRepository identities,
    PlanAccessProbe planAccess,
    TutorNames tutorNames,
    IClock clock)
{
    public async Task<Result<TutoringAppointmentDto, Error>> HandleAsync(
        ScheduleTutoringRequest request, Guid adminUserId, CancellationToken ct)
    {
        var grant = await grants.GetByIdAsync(request.GrantId, ct);
        if (grant is null)
        {
            return Error.NotFound("tutoring_grant.not_found", "No existe esa bolsa de tutorías.");
        }

        var now = clock.UtcNow;
        var plan = await planAccess.GetAsync(grant.UserId, now, ct);

        if (!grant.IsUsable(now, plan.HasAccess))
        {
            return Error.Conflict(
                "tutoring_grant.not_usable",
                "Esa bolsa no está disponible: puede estar revocada, caducada o agotada.");
        }

        // Lo ya convocado y sin dar también cuenta: si no, se podrían citar cinco tutorías
        // contra una bolsa de una hora y solo la primera tendría con qué apuntarse.
        var booked = (await appointments.GetForGrantsAsync([grant.Id], ct))
            .Where(a => a.Status == AppointmentStatus.Scheduled)
            .Sum(a => a.Minutes);

        if (request.Minutes > grant.MinutesRemaining - booked)
        {
            return Error.Conflict(
                "tutoring_appointment.no_room",
                booked == 0
                    ? "No queda tiempo suficiente en la bolsa para esa tutoría."
                    : "No queda tiempo suficiente: hay más tutorías ya convocadas contra esa bolsa.");
        }

        Tutor? tutor = null;
        if (request.TutorId is { } tutorId)
        {
            tutor = await tutors.GetByIdAsync(tutorId, ct);
            if (tutor is null)
            {
                return Error.NotFound("tutor.not_found", "No existe ese profesor.");
            }
        }

        var identity = await identities.GetDefaultAsync(ct);
        var id = Guid.CreateVersion7();

        var appointment = TutoringAppointment.Create(
            id, grant.Id, tutor?.Id, request.StartsAt, request.Minutes,
            request.Topic, request.Notes, request.Location,
            CalendarUid(id, identity.PublicDomain), now, adminUserId);

        if (appointment.IsFailure)
        {
            return appointment.Error;
        }

        await appointments.UpsertAsync(appointment.Value, ct);
        await InviteAsync(appointment.Value, grant.UserId, tutor, users, identities, invitations, ct);

        return appointment.Value.ToDto(await tutorNames.AllAsync(ct));
    }

    /// <summary>
    /// El identificador del evento en los calendarios. Se compone aquí, en la capa de casos de
    /// uso, porque tiene que quedar guardado con la cita: generarlo al mandar cada correo daría
    /// uno distinto cada vez y cada actualización crearía un evento nuevo.
    /// </summary>
    internal static string CalendarUid(Guid appointmentId, string domain) =>
        $"{appointmentId:N}@{(string.IsNullOrWhiteSpace(domain) ? "inkoova.academy" : domain.Trim())}";

    /// <summary>
    /// Avisa a los invitados. Compartido por convocar, mover y anular: los tres mandan lo mismo
    /// con distinta secuencia, y tenerlo en un sitio evita que uno de los tres se olvide del
    /// profesor.
    /// </summary>
    internal static async Task InviteAsync(
        TutoringAppointment appointment,
        Guid studentId,
        Tutor? tutor,
        IUserRepository users,
        IAcademyIdentityRepository identities,
        ITutoringInvitationSender invitations,
        CancellationToken ct)
    {
        var student = await users.GetByIdAsync(studentId, ct);
        if (student is null)
        {
            return;
        }

        var identity = await identities.GetDefaultAsync(ct);

        await invitations.SendAsync(
            new TutoringInvitation(
                appointment.Id,
                appointment.IcsUid,
                appointment.IcsSequence,
                appointment.StartsAt,
                appointment.EndsAt,
                appointment.Topic,
                appointment.Notes,
                appointment.Location,
                student.DisplayName,
                student.Email,
                tutor?.DisplayName,
                tutor?.Email,
                identity.Id,
                appointment.Status == AppointmentStatus.Cancelled,
                appointment.CancelledReason),
            ct);
    }
}

public sealed class RescheduleTutoringHandler(
    ITutoringGrantRepository grants,
    ITutoringAppointmentRepository appointments,
    ITutorRepository tutors,
    ITutoringInvitationSender invitations,
    IUserRepository users,
    IAcademyIdentityRepository identities,
    TutorNames tutorNames)
{
    public async Task<Result<TutoringAppointmentDto, Error>> HandleAsync(
        Guid appointmentId, ScheduleTutoringRequest request, CancellationToken ct)
    {
        var appointment = await appointments.GetByIdAsync(appointmentId, ct);
        if (appointment is null)
        {
            return Error.NotFound("tutoring_appointment.not_found", "No existe esa convocatoria.");
        }

        var grant = await grants.GetByIdAsync(appointment.GrantId, ct);
        if (grant is null)
        {
            return Error.NotFound("tutoring_grant.not_found", "No existe esa bolsa de tutorías.");
        }

        Tutor? tutor = null;
        if (request.TutorId is { } tutorId)
        {
            tutor = await tutors.GetByIdAsync(tutorId, ct);
            if (tutor is null)
            {
                return Error.NotFound("tutor.not_found", "No existe ese profesor.");
            }
        }

        var moved = appointment.Reschedule(
            tutor?.Id, request.StartsAt, request.Minutes,
            request.Topic, request.Notes, request.Location);

        if (moved.IsFailure)
        {
            return moved.Error;
        }

        await appointments.UpsertAsync(appointment, ct);

        await ScheduleTutoringHandler.InviteAsync(
            appointment, grant.UserId, tutor, users, identities, invitations, ct);

        return appointment.ToDto(await tutorNames.AllAsync(ct));
    }
}

public sealed class CancelTutoringAppointmentHandler(
    ITutoringGrantRepository grants,
    ITutoringAppointmentRepository appointments,
    ITutorRepository tutors,
    ITutoringInvitationSender invitations,
    IUserRepository users,
    IAcademyIdentityRepository identities,
    TutorNames tutorNames)
{
    public async Task<Result<TutoringAppointmentDto, Error>> HandleAsync(
        Guid appointmentId, string reason, CancellationToken ct)
    {
        var appointment = await appointments.GetByIdAsync(appointmentId, ct);
        if (appointment is null)
        {
            return Error.NotFound("tutoring_appointment.not_found", "No existe esa convocatoria.");
        }

        var cancelled = appointment.Cancel(reason);
        if (cancelled.IsFailure)
        {
            return cancelled.Error;
        }

        await appointments.UpsertAsync(appointment, ct);

        var grant = await grants.GetByIdAsync(appointment.GrantId, ct);
        if (grant is not null)
        {
            var tutor = appointment.TutorId is { } tutorId
                ? await tutors.GetByIdAsync(tutorId, ct)
                : null;

            // La anulación se manda igual que la convocatoria: es lo que libera el hueco en la
            // agenda de los dos. Sin ella, la cita se queda ahí para siempre.
            await ScheduleTutoringHandler.InviteAsync(
                appointment, grant.UserId, tutor, users, identities, invitations, ct);
        }

        return appointment.ToDto(await tutorNames.AllAsync(ct));
    }
}

/// <summary>
/// Da por dada una tutoría convocada: descuenta el tiempo de la bolsa, devenga lo del profesor
/// y ata la cita a la tutoría que ha nacido.
///
/// Es lo mismo que apuntar una tutoría suelta, con la diferencia de que el profesor, la
/// duración y el tema ya estaban acordados. Se pueden corregir al confirmarla: la clase real
/// pudo durar más de lo previsto, y apuntar lo previsto en vez de lo ocurrido descuadraría el
/// saldo con lo que de verdad se dio.
/// </summary>
public sealed record ConfirmAppointmentRequest(int? Minutes, string? Notes);

public sealed class ConfirmTutoringAppointmentHandler(
    ITutoringGrantRepository grants,
    ITutoringAppointmentRepository appointments,
    ITutorRepository tutors,
    ITutorEarningRepository earnings,
    PlanAccessProbe planAccess,
    IClock clock)
{
    public async Task<Result<TutoringAppointmentDto, Error>> HandleAsync(
        Guid appointmentId, ConfirmAppointmentRequest request, Guid adminUserId, CancellationToken ct)
    {
        var appointment = await appointments.GetByIdAsync(appointmentId, ct);
        if (appointment is null)
        {
            return Error.NotFound("tutoring_appointment.not_found", "No existe esa convocatoria.");
        }

        if (appointment.Status != AppointmentStatus.Scheduled)
        {
            return Error.Conflict(
                "tutoring_appointment.not_scheduled",
                appointment.Status == AppointmentStatus.Done
                    ? "Esa tutoría ya estaba apuntada."
                    : "Esa cita está anulada. Convócala otra vez o apunta la tutoría sin cita.");
        }

        var grant = await grants.GetByIdAsync(appointment.GrantId, ct);
        if (grant is null)
        {
            return Error.NotFound("tutoring_grant.not_found", "No existe esa bolsa de tutorías.");
        }

        var now = clock.UtcNow;
        var plan = await planAccess.GetAsync(grant.UserId, now, ct);

        var tutor = appointment.TutorId is { } tutorId
            ? await tutors.GetByIdAsync(tutorId, ct)
            : null;

        var consumed = grant.Consume(
            Guid.CreateVersion7(),
            request.Minutes ?? appointment.Minutes,
            appointment.StartsAt,
            appointment.Topic,
            string.IsNullOrWhiteSpace(request.Notes) ? appointment.Notes : request.Notes!,
            adminUserId,
            now,
            plan.HasAccess,
            tutor?.Id);

        if (consumed.IsFailure)
        {
            return consumed.Error;
        }

        await grants.SaveAsync(grant, ct);

        var given = appointment.MarkGiven(consumed.Value.Id);
        if (given.IsFailure)
        {
            return given.Error;
        }

        await appointments.UpsertAsync(appointment, ct);

        if (tutor is not null)
        {
            await earnings.SaveAsync(
                TutorEarning.Accrue(
                    Guid.CreateVersion7(), tutor, consumed.Value.Id,
                    grant.Paid, grant.MinutesTotal, consumed.Value.Minutes, now),
                ct);
        }

        return appointment.ToDto(
            tutor is null ? null : new Dictionary<Guid, string> { [tutor.Id] = tutor.DisplayName });
    }
}

/// <summary>La agenda: lo convocado y sin dar, de todos los alumnos, por fecha.</summary>
public sealed record UpcomingAppointmentDto(
    TutoringAppointmentDto Appointment,
    // El identificador y no solo el nombre: la cita cuelga de la bolsa, y sin esto la agenda no
    // podría abrir la ficha de su alumno sin ponerse a buscarlo por el nombre.
    Guid StudentId,
    string StudentName,
    string StudentEmail,
    // Ya pasó su hora y sigue sin apuntarse. Lo decide el reloj del servidor y no el del
    // navegador: es el mismo reloj con el que se guardó la cita.
    bool IsOverdue);

public sealed class ListUpcomingAppointmentsHandler(
    ITutoringAppointmentRepository appointments,
    ITutoringGrantRepository grants,
    IUserRepository users,
    TutorNames tutorNames,
    IClock clock)
{
    public async Task<IReadOnlyList<UpcomingAppointmentDto>> HandleAsync(CancellationToken ct)
    {
        var now = clock.UtcNow;

        // Desde ayer y no desde ahora: una tutoría de esta mañana que todavía no se ha apuntado
        // tiene que seguir viéndose, que es justo cuando hay que acordarse de apuntarla.
        var upcoming = await appointments.GetUpcomingAsync(now.AddDays(-1), ct);
        if (upcoming.Count == 0)
        {
            return [];
        }

        var names = await tutorNames.AllAsync(ct);
        var result = new List<UpcomingAppointmentDto>(upcoming.Count);

        foreach (var appointment in upcoming)
        {
            var grant = await grants.GetByIdAsync(appointment.GrantId, ct);
            var student = grant is null ? null : await users.GetByIdAsync(grant.UserId, ct);

            result.Add(new UpcomingAppointmentDto(
                appointment.ToDto(names),
                grant?.UserId ?? Guid.Empty,
                student?.DisplayName ?? "—",
                student?.Email ?? "",
                appointment.EndsAt <= now));
        }

        return result;
    }
}
