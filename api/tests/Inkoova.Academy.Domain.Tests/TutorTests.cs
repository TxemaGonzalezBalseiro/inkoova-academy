using FluentAssertions;
using Inkoova.Academy.Domain.Tutoring;
using Inkoova.Academy.Domain.ValueObjects;

namespace Inkoova.Academy.Domain.Tests;

/// <summary>
/// Lo que se le paga a un profesor por una tutoría.
///
/// Todo lo de aquí es dinero de un tercero, así que lo que se comprueba no es que «funcione»
/// sino que no pueda pagarse de más, ni dos veces, ni sobre un ingreso que no existió.
/// </summary>
public class TutorTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 31, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void What_a_tutor_earns_is_prorated_by_the_minutes_of_the_class()
    {
        var tutor = Tutor.Create(Guid.CreateVersion7(), null, "Ana", "ana@ejemplo.es", "", 50).Value;

        // Bolsa de 5 h por 499 €, clase de 1 h: la base es un quinto, no los 499 enteros.
        var earning = TutorEarning.Accrue(
            Guid.CreateVersion7(), tutor, Guid.CreateVersion7(),
            Money.Euros(49900), bagMinutes: 300, sessionMinutes: 60, Now);

        earning.Base.AmountInCents.Should().Be(9980);
        earning.Amount.AmountInCents.Should().Be(4990);
        earning.Percent.Should().Be(50);
    }

    [Fact]
    public void A_bag_that_was_never_paid_for_earns_nothing()
    {
        var tutor = Tutor.Create(Guid.CreateVersion7(), null, "Ana", "ana@ejemplo.es", "", 50).Value;

        // Una beca o una cortesía no entró por caja. Repartir un porcentaje de un ingreso que no
        // existe es pagar de la propia caja sin enterarse.
        var earning = TutorEarning.Accrue(
            Guid.CreateVersion7(), tutor, Guid.CreateVersion7(),
            Money.Euros(0), bagMinutes: 300, sessionMinutes: 60, Now);

        earning.Base.AmountInCents.Should().Be(0);
        earning.Amount.AmountInCents.Should().Be(0);
    }

    [Fact]
    public void Raising_the_commission_does_not_rewrite_what_was_already_owed()
    {
        var tutor = Tutor.Create(Guid.CreateVersion7(), null, "Ana", "ana@ejemplo.es", "", 40).Value;

        var earning = TutorEarning.Accrue(
            Guid.CreateVersion7(), tutor, Guid.CreateVersion7(),
            Money.Euros(10000), bagMinutes: 60, sessionMinutes: 60, Now);

        tutor.Describe("Ana", "ana@ejemplo.es", "", 80).IsSuccess.Should().BeTrue();

        // Lo devengado guardó su porcentaje: el acuerdo nuevo vale de aquí en adelante, no
        // sobre lo del mes pasado.
        earning.Percent.Should().Be(40);
        earning.Amount.AmountInCents.Should().Be(4000);
    }

    [Fact]
    public void The_same_earning_cannot_be_paid_twice()
    {
        var tutor = Tutor.Create(Guid.CreateVersion7(), null, "Ana", "ana@ejemplo.es", "", 50).Value;

        var earning = TutorEarning.Accrue(
            Guid.CreateVersion7(), tutor, Guid.CreateVersion7(),
            Money.Euros(9900), bagMinutes: 60, sessionMinutes: 60, Now);

        earning.MarkPaid(Now, "Transferencia 1").IsSuccess.Should().BeTrue();

        var again = earning.MarkPaid(Now.AddDays(1), "Transferencia 2");

        again.IsFailure.Should().BeTrue();
        again.Error.Code.Should().Be("tutor_earning.already_paid");
        // Ni la fecha ni la referencia se pisan: así es como se pierde el rastro de un pago
        // duplicado.
        earning.PaidAt.Should().Be(Now);
        earning.PayoutNote.Should().Be("Transferencia 1");
    }

    [Fact]
    public void Rounding_goes_half_away_from_zero_like_the_affiliate_commissions()
    {
        var tutor = Tutor.Create(Guid.CreateVersion7(), null, "Ana", "ana@ejemplo.es", "", 33.33m).Value;

        // 10,01 € al 33,33 % son 333,6333 céntimos: 334, no 333. Si dos partes del sistema
        // redondearan distinto, dos cifras que deberían cuadrar se separarían por un céntimo.
        tutor.Earn(Money.Euros(1001)).AmountInCents.Should().Be(334);
    }

    [Fact]
    public void A_retired_tutor_keeps_his_history_and_what_he_is_owed()
    {
        var tutor = Tutor.Create(Guid.CreateVersion7(), null, "Ana", "ana@ejemplo.es", "", 50).Value;

        var earning = TutorEarning.Accrue(
            Guid.CreateVersion7(), tutor, Guid.CreateVersion7(),
            Money.Euros(9900), bagMinutes: 60, sessionMinutes: 60, Now);

        tutor.SetActive(false);

        tutor.IsActive.Should().BeFalse();
        earning.IsPaid.Should().BeFalse();
        earning.Amount.AmountInCents.Should().Be(4950);
    }

    [Fact]
    public void A_tutor_needs_a_valid_email_because_that_is_how_he_gets_paid()
    {
        Tutor.Create(Guid.CreateVersion7(), null, "Ana", "sin-arroba", "", 50)
            .Error.Code.Should().Be("tutor.email_invalid");

        Tutor.Create(Guid.CreateVersion7(), null, "Ana", "ana@ejemplo.es", "", 120)
            .Error.Code.Should().Be("tutor.percent_invalid");

        Tutor.Create(Guid.CreateVersion7(), null, "   ", "ana@ejemplo.es", "", 50)
            .Error.Code.Should().Be("tutor.name_invalid");
    }

    [Fact]
    public void A_session_records_who_gave_it_apart_from_who_typed_it_in()
    {
        var admin = Guid.CreateVersion7();
        var tutorId = Guid.CreateVersion7();

        var grant = TutoringGrant.Create(
            Guid.CreateVersion7(), Guid.CreateVersion7(), packageId: null, "Tutorías",
            300, TutoringGrantSource.Purchase, purchaseId: null, Now,
            TutoringExpiry.Never, expiresAt: null, "", null, Money.Euros(49900)).Value;

        var session = grant.Consume(
            Guid.CreateVersion7(), 60, Now, "Arquitectura", "", admin, Now,
            hasPlanAccess: false, tutorId).Value;

        // Quien lo apuntó en el panel no es quien dio la clase, y pagarle al primero sería
        // pagarle al administrativo.
        session.RecordedBy.Should().Be(admin);
        session.TutorId.Should().Be(tutorId);
    }

    [Fact]
    public void A_bag_remembers_what_was_paid_for_it_so_the_split_survives_a_price_change()
    {
        var package = TutoringPackage.Create(
            Guid.CreateVersion7(), "pack-5", "Pack de 5", "", 300, Money.Euros(49900),
            TutoringExpiry.WithSubscription, validityDays: null, displayOrder: 1).Value;

        var bought = TutoringGrant.FromPackage(
            Guid.CreateVersion7(), Guid.CreateVersion7(), package,
            TutoringGrantSource.Purchase, purchaseId: null, Now, "", null).Value;

        // Una concesión manual del mismo paquete no entró por caja.
        var given = TutoringGrant.FromPackage(
            Guid.CreateVersion7(), Guid.CreateVersion7(), package,
            TutoringGrantSource.Manual, purchaseId: null, Now, "Beca", null).Value;

        bought.Paid.AmountInCents.Should().Be(49900);
        given.Paid.AmountInCents.Should().Be(0);

        // Y si mañana el paquete sube, la bolsa ya vendida sigue repartiendo sobre lo que costó.
        package.Describe("Pack de 5", "", 300, Money.Euros(89900), TutoringExpiry.WithSubscription, null, 1)
            .IsSuccess.Should().BeTrue();

        bought.Paid.AmountInCents.Should().Be(49900);
    }
}
