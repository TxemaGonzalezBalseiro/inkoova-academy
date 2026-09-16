using FluentAssertions;
using Inkoova.Academy.Domain.Tutoring;
using Inkoova.Academy.Domain.ValueObjects;

namespace Inkoova.Academy.Domain.Tests;

/// <summary>
/// Tutorías convocadas. Lo que se comprueba aquí es sobre todo que convocar NO sea dar: el
/// saldo no se toca hasta que la clase ocurre, y una cita mal cerrada no puede descontar horas
/// que el alumno todavía tiene.
/// </summary>
public class AppointmentTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 31, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Scheduling_does_not_touch_the_balance()
    {
        var grant = Grant(minutes: 300);
        var appointment = Appointment(grant.Id, minutes: 60);

        // La cita existe y el saldo sigue entero: reservarlo dejaría horas muertas si alguien
        // olvida anular una convocatoria.
        appointment.Status.Should().Be(AppointmentStatus.Scheduled);
        grant.MinutesRemaining.Should().Be(300);
    }

    [Fact]
    public void Moving_a_meeting_raises_the_calendar_sequence_but_keeps_its_identity()
    {
        var appointment = Appointment(Guid.CreateVersion7(), minutes: 60);
        var uid = appointment.IcsUid;

        appointment.Reschedule(null, Now.AddDays(3), 90, "Otra cosa", "", "")
            .IsSuccess.Should().BeTrue();

        // El UID no cambia: si cambiara, el evento viejo se quedaría colgado en la agenda de
        // los invitados y aparecería otro al lado.
        appointment.IcsUid.Should().Be(uid);
        // Y la secuencia sube, o los clientes de calendario ignoran la actualización.
        appointment.IcsSequence.Should().Be(1);
        appointment.Minutes.Should().Be(90);
    }

    [Fact]
    public void A_meeting_that_already_happened_cannot_be_moved()
    {
        var appointment = Appointment(Guid.CreateVersion7(), minutes: 60);
        appointment.MarkGiven(Guid.CreateVersion7()).IsSuccess.Should().BeTrue();

        var moved = appointment.Reschedule(null, Now.AddDays(1), 60, "Tarde", "", "");

        moved.IsFailure.Should().BeTrue();
        moved.Error.Code.Should().Be("tutoring_appointment.not_scheduled");
    }

    [Fact]
    public void A_cancelled_meeting_cannot_be_quietly_marked_as_given()
    {
        var appointment = Appointment(Guid.CreateVersion7(), minutes: 60);
        appointment.Cancel("El alumno no puede").IsSuccess.Should().BeTrue();

        var given = appointment.MarkGiven(Guid.CreateVersion7());

        // Darla por buena escondería que se anuló. Si la clase se dio igualmente, se convoca de
        // nuevo o se apunta suelta, y entonces queda dicho.
        given.IsFailure.Should().BeTrue();
        given.Error.Code.Should().Be("tutoring_appointment.cancelled");
    }

    [Fact]
    public void Cancelling_also_raises_the_sequence_so_the_calendars_free_the_slot()
    {
        var appointment = Appointment(Guid.CreateVersion7(), minutes: 60);

        appointment.Cancel("Se pospone").IsSuccess.Should().BeTrue();

        appointment.Status.Should().Be(AppointmentStatus.Cancelled);
        appointment.IcsSequence.Should().Be(1);
        appointment.CancelledReason.Should().Be("Se pospone");

        appointment.Cancel("Otra vez").Error.Code.Should().Be("tutoring_appointment.already_cancelled");
    }

    [Fact]
    public void Undoing_the_session_puts_the_meeting_back_as_pending()
    {
        var appointment = Appointment(Guid.CreateVersion7(), minutes: 60);
        appointment.MarkGiven(Guid.CreateVersion7());

        appointment.Reopen();

        // Es la verdad: la clase se quedó sin dar. Desaparecerla de la agenda sería peor, porque
        // nadie decidió anularla.
        appointment.Status.Should().Be(AppointmentStatus.Scheduled);
        appointment.SessionId.Should().BeNull();
    }

    [Fact]
    public void A_meeting_needs_a_topic_because_it_goes_in_the_calendar_invitation()
    {
        var blank = TutoringAppointment.Create(
            Guid.CreateVersion7(), Guid.CreateVersion7(), null, Now.AddDays(1), 60,
            "   ", "", "", "uid@ejemplo", Now, null);

        blank.Error.Code.Should().Be("tutoring_appointment.topic_empty");

        var tooLong = TutoringAppointment.Create(
            Guid.CreateVersion7(), Guid.CreateVersion7(), null, Now.AddDays(1), 9 * 60,
            "Maratón", "", "", "uid@ejemplo", Now, null);

        tooLong.Error.Code.Should().Be("tutoring_appointment.minutes_invalid");
    }

    [Fact]
    public void The_start_is_kept_in_utc_whatever_offset_it_arrived_in()
    {
        var madrid = new DateTimeOffset(2026, 9, 14, 17, 0, 0, TimeSpan.FromHours(2));

        var appointment = TutoringAppointment.Create(
            Guid.CreateVersion7(), Guid.CreateVersion7(), null, madrid, 60,
            "Arquitectura", "", "", "uid@ejemplo", Now, null).Value;

        // Postgres rechaza un timestamptz con desplazamiento, y el calendario necesita la Z:
        // sin ella, alumno y profesor verían horas distintas.
        appointment.StartsAt.Offset.Should().Be(TimeSpan.Zero);
        appointment.StartsAt.Hour.Should().Be(15);
        appointment.EndsAt.Should().Be(appointment.StartsAt.AddMinutes(60));
    }

    private static TutoringGrant Grant(int minutes) =>
        TutoringGrant.Create(
            Guid.CreateVersion7(), Guid.CreateVersion7(), packageId: null, "Tutorías",
            minutes, TutoringGrantSource.Purchase, purchaseId: null, Now,
            TutoringExpiry.Never, expiresAt: null, "", null, Money.Euros(49900)).Value;

    private static TutoringAppointment Appointment(Guid grantId, int minutes) =>
        TutoringAppointment.Create(
            Guid.CreateVersion7(), grantId, null, Now.AddDays(2), minutes,
            "Arquitectura", "", "https://meet.ejemplo/abc", "uid@ejemplo", Now, null).Value;
}
