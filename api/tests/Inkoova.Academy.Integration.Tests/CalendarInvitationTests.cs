using System.Text;
using FluentAssertions;
using Inkoova.Academy.Infrastructure.Documents;

namespace Inkoova.Academy.Integration.Tests;

/// <summary>
/// El fichero de invitación.
///
/// No hace falta base de datos, pero vive aquí porque el generador es de infraestructura y el
/// proyecto de dominio no la referencia. Lo que se comprueba es lo que rompe en silencio: una
/// línea demasiado larga o una coma sin escapar hacen que Outlook descarte el evento entero sin
/// decir nada, y el alumno se queda sin cita creyendo que la tiene.
/// </summary>
public class CalendarInvitationTests
{
    private static readonly DateTimeOffset Start = new(2026, 9, 14, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public void The_invitation_carries_the_moment_in_utc()
    {
        var ics = Text(Build());

        ics.Should().Contain("DTSTART:20260914T150000Z");
        ics.Should().Contain("DTEND:20260914T160000Z");
        ics.Should().Contain("METHOD:REQUEST");
        ics.Should().Contain("STATUS:CONFIRMED");
    }

    [Fact]
    public void Everyone_who_goes_is_an_attendee_and_can_answer()
    {
        // Sin plegar: la línea de un invitado pasa de 75 octetos y viene partida, que es lo
        // correcto. Buscar en el texto plegado no encontraría ni el correo entero.
        var ics = Unfold(Text(Build()));

        ics.Should().Contain("mailto:alumno@ejemplo.es");
        ics.Should().Contain("mailto:profe@ejemplo.es");
        // Sin RSVP no salen los botones de aceptar y rechazar.
        ics.Should().Contain("RSVP=TRUE");
        ics.Should().Contain("ORGANIZER;CN=Inkoova Academy:mailto:hola@ejemplo.es");
    }

    [Fact]
    public void Cancelling_sends_a_cancellation_and_not_another_invitation()
    {
        var ics = Text(Build(cancel: true, sequence: 2));

        ics.Should().Contain("METHOD:CANCEL");
        ics.Should().Contain("STATUS:CANCELLED");
        ics.Should().Contain("SEQUENCE:2");
        // Un aviso en una cita anulada haría sonar la alarma de algo que no va a pasar.
        ics.Should().NotContain("BEGIN:VALARM");
    }

    [Fact]
    public void Commas_and_semicolons_in_the_subject_are_escaped()
    {
        var ics = Text(Build(topic: "Arquitectura, agentes; y trazabilidad"));

        ics.Should().Contain("Arquitectura\\, agentes\\; y trazabilidad");
    }

    [Fact]
    public void No_line_goes_over_seventy_five_octets()
    {
        var ics = Text(Build(
            topic: "Revisión del caso Meridiana con métricas de trazabilidad y gobernanza europea",
            notes: "Repasamos el expediente completo, los indicadores del trimestre y las " +
                   "acciones pendientes con el equipo de riesgos y cumplimiento normativo."));

        foreach (var line in ics.Split("\r\n", StringSplitOptions.RemoveEmptyEntries))
        {
            // Octetos, no caracteres: en UTF-8 una tilde ocupa dos, y medir en caracteres deja
            // pasar líneas que el cliente rechaza.
            Encoding.UTF8.GetByteCount(line).Should().BeLessThanOrEqualTo(75);
        }
    }

    [Fact]
    public void A_folded_line_continues_with_a_space_and_survives_unfolding()
    {
        var largo = new string('a', 200);
        var ics = Text(Build(topic: largo));

        // Deshacer el plegado tiene que devolver el texto original. Si el corte partiera un
        // carácter multibyte, esto no cuadraría.
        Unfold(ics).Should().Contain($"SUMMARY:Tutoría · {largo}");
    }

    [Fact]
    public void The_uid_is_stable_for_the_same_meeting()
    {
        var id = Guid.CreateVersion7();

        CalendarInvitation.Uid(id, "academy.inkoova.com")
            .Should().Be(CalendarInvitation.Uid(id, "academy.inkoova.com"))
            .And.Contain("@academy.inkoova.com");
    }

    private static byte[] Build(
        bool cancel = false,
        int sequence = 0,
        string topic = "Arquitectura de agentes",
        string notes = "") =>
        CalendarInvitation.Build(new CalendarInvitationRequest(
            "cita@ejemplo.es",
            sequence,
            Start,
            Start.AddHours(1),
            $"Tutoría · {topic}",
            notes,
            "https://meet.ejemplo/abc",
            new CalendarAttendee("hola@ejemplo.es", "Inkoova Academy"),
            [
                new CalendarAttendee("alumno@ejemplo.es", "Alumno de prueba"),
                new CalendarAttendee("profe@ejemplo.es", "Profesora de prueba")
            ],
            Start.AddDays(-1),
            cancel));

    private static string Text(byte[] ics) => Encoding.UTF8.GetString(ics);

    /// <summary>Junta las líneas partidas, que es lo que hace el cliente de calendario al leerlas.</summary>
    private static string Unfold(string ics) => ics.Replace("\r\n ", "");
}
