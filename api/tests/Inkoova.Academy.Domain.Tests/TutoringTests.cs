using FluentAssertions;
using Inkoova.Academy.Domain.Tutoring;
using Inkoova.Academy.Domain.ValueObjects;

namespace Inkoova.Academy.Domain.Tests;

/// <summary>
/// El saldo de tutorías nunca se guarda: es lo concedido menos lo consumido. Estas pruebas
/// fijan las tres cosas que hacen que esa resta sea de fiar —no se puede gastar de más, no se
/// puede gastar de una bolsa muerta, y no se puede bajar el total por debajo de lo ya dado—,
/// porque cualquiera de las tres la convertiría en un número negativo que la pantalla del
/// alumno enseñaría como si fuese cierto.
/// </summary>
public class TutoringTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 31, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void The_balance_is_what_was_granted_minus_what_was_used()
    {
        var grant = Grant(minutes: 300);

        grant.Consume(Guid.CreateVersion7(), 90, Now, "Arquitectura", "", null, Now)
            .IsSuccess.Should().BeTrue();
        grant.Consume(Guid.CreateVersion7(), 60, Now, "Trazabilidad", "", null, Now)
            .IsSuccess.Should().BeTrue();

        grant.MinutesUsed.Should().Be(150);
        grant.MinutesRemaining.Should().Be(150);
    }

    [Fact]
    public void Time_that_is_not_there_cannot_be_spent()
    {
        var grant = Grant(minutes: 120);

        var tooMuch = grant.Consume(Guid.CreateVersion7(), 180, Now, "De más", "", null, Now);

        tooMuch.IsFailure.Should().BeTrue();
        tooMuch.Error.Code.Should().Be("tutoring_grant.insufficient_balance");

        // Y el intento fallido no deja rastro: el saldo sigue entero.
        grant.MinutesUsed.Should().Be(0);
        grant.MinutesRemaining.Should().Be(120);
    }

    [Fact]
    public void The_last_minute_can_be_spent_but_not_one_more()
    {
        var grant = Grant(minutes: 60);

        grant.Consume(Guid.CreateVersion7(), 60, Now, "Justo lo que queda", "", null, Now)
            .IsSuccess.Should().BeTrue();

        grant.MinutesRemaining.Should().Be(0);
        grant.IsUsable(Now).Should().BeFalse();

        grant.Consume(Guid.CreateVersion7(), 1, Now, "Uno de más", "", null, Now)
            .IsFailure.Should().BeTrue();
    }

    [Fact]
    public void An_expired_bag_cannot_be_spent_even_with_time_left()
    {
        var grant = Grant(minutes: 300, expiresAt: Now.AddDays(10));
        var later = Now.AddDays(11);

        var attempt = grant.Consume(Guid.CreateVersion7(), 60, later, "Tarde", "", null, later);

        attempt.IsFailure.Should().BeTrue();
        attempt.Error.Code.Should().Be("tutoring_grant.expired");
        grant.MinutesRemaining.Should().Be(300);
    }

    [Fact]
    public void A_revoked_bag_keeps_the_sessions_that_were_already_given()
    {
        var grant = Grant(minutes: 300);
        grant.Consume(Guid.CreateVersion7(), 90, Now, "Ya dada", "", null, Now);

        grant.Revoke("acuerdo cancelado", Now).IsSuccess.Should().BeTrue();

        // Esas tutorías ocurrieron: borrarlas al revocar dejaría el histórico mintiendo.
        grant.Sessions.Should().HaveCount(1);
        grant.MinutesUsed.Should().Be(90);
        grant.IsUsable(Now).Should().BeFalse();

        grant.Consume(Guid.CreateVersion7(), 30, Now, "Después de revocar", "", null, Now)
            .Error.Code.Should().Be("tutoring_grant.revoked");
    }

    [Fact]
    public void Undoing_a_session_gives_the_time_back()
    {
        var grant = Grant(minutes: 300);
        var sessionId = Guid.CreateVersion7();
        grant.Consume(sessionId, 90, Now, "Mal apuntada", "", null, Now);

        grant.RemoveSession(sessionId).IsSuccess.Should().BeTrue();

        grant.MinutesUsed.Should().Be(0);
        grant.MinutesRemaining.Should().Be(300);
    }

    [Fact]
    public void The_total_cannot_be_lowered_below_what_was_already_used()
    {
        var grant = Grant(minutes: 300);
        grant.Consume(Guid.CreateVersion7(), 120, Now, "Dada", "", null, Now);

        var tooLow = grant.AdjustTotal(60);

        tooLow.IsFailure.Should().BeTrue();
        tooLow.Error.Code.Should().Be("tutoring_grant.below_used");
        grant.MinutesTotal.Should().Be(300);

        // Justo hasta lo consumido sí, porque deja el saldo en cero y no en negativo.
        grant.AdjustTotal(120).IsSuccess.Should().BeTrue();
        grant.MinutesRemaining.Should().Be(0);
    }

    [Fact]
    public void A_session_needs_a_topic()
    {
        var grant = Grant(minutes: 300);

        var blank = grant.Consume(Guid.CreateVersion7(), 60, Now, "   ", "", null, Now);

        blank.IsFailure.Should().BeTrue();
        blank.Error.Code.Should().Be("tutoring_session.topic_empty");
        grant.MinutesUsed.Should().Be(0);
    }

    [Fact]
    public void A_grant_from_a_package_copies_its_hours_so_editing_the_catalogue_changes_nothing()
    {
        var package = Package(minutes: 300);
        var grant = TutoringGrant.FromPackage(
            Guid.CreateVersion7(), Guid.CreateVersion7(), package,
            TutoringGrantSource.Purchase, purchaseId: null, Now, "", null).Value;

        // El paquete pasa a valer el doble el mes que viene.
        package.Describe("Pack ampliado", "", 600, Money.Euros(90000), TutoringExpiry.FixedDate, 180, 1)
            .IsSuccess.Should().BeTrue();

        grant.MinutesTotal.Should().Be(300);
        grant.PackageName.Should().Be("Pack de 5 tutorías");
    }

    [Fact]
    public void A_package_with_validity_gives_the_bag_an_expiry_date()
    {
        var package = Package(minutes: 300, validityDays: 180);

        var grant = TutoringGrant.FromPackage(
            Guid.CreateVersion7(), Guid.CreateVersion7(), package,
            TutoringGrantSource.Purchase, purchaseId: null, Now, "", null).Value;

        grant.ExpiresAt.Should().Be(Now.AddDays(180));
        grant.HasExpired(Now.AddDays(179)).Should().BeFalse();
        grant.HasExpired(Now.AddDays(181)).Should().BeTrue();
    }

    // ── caducidad atada a la suscripción ──────────────────────────────────────────────────

    [Fact]
    public void Hours_tied_to_the_subscription_live_while_the_plan_gives_access()
    {
        var grant = SubscriptionGrant(minutes: 300);

        // Un año después sigue viva si el plan sigue dando acceso: renovar no tiene que tocar
        // nada, y ninguna fecha guardada la puede matar por su cuenta.
        grant.HasExpired(Now.AddYears(1), hasPlanAccess: true).Should().BeFalse();
        grant.IsUsable(Now.AddYears(1), hasPlanAccess: true).Should().BeTrue();
    }

    [Fact]
    public void Hours_tied_to_the_subscription_die_the_moment_the_plan_stops()
    {
        var grant = SubscriptionGrant(minutes: 300);

        grant.HasExpired(Now, hasPlanAccess: false).Should().BeTrue();
        grant.IsUsable(Now, hasPlanAccess: false).Should().BeFalse();

        var attempt = grant.Consume(
            Guid.CreateVersion7(), 60, Now, "Sin plan", "", null, Now, hasPlanAccess: false);

        attempt.IsFailure.Should().BeTrue();
        attempt.Error.Code.Should().Be("tutoring_grant.expired");
        // El motivo tiene que hablar de la suscripción: mandar a mirar una fecha que no existe
        // deja al alumno buscando algo que no va a encontrar.
        attempt.Error.Message.Should().Contain("suscripción");
    }

    [Fact]
    public void The_shown_expiry_of_subscription_hours_is_the_subscription_date()
    {
        var grant = SubscriptionGrant(minutes: 300);
        var renewal = Now.AddMonths(6);

        grant.EffectiveExpiry(renewal).Should().Be(renewal);

        // Y se mueve sola: al renovar, la fecha que se enseña es la nueva sin haber tocado la
        // bolsa. Es justo lo que un `expires_at` guardado no puede hacer.
        grant.EffectiveExpiry(renewal.AddYears(1)).Should().Be(renewal.AddYears(1));
    }

    [Fact]
    public void A_fixed_date_bag_ignores_the_subscription_entirely()
    {
        var grant = Grant(minutes: 300, expiresAt: Now.AddDays(10));

        // Sin plan pero dentro de su fecha: sigue viva. Son acuerdos que no dependen de ningún
        // plan, como el de una empresa.
        grant.HasExpired(Now.AddDays(5), hasPlanAccess: false).Should().BeFalse();

        // Y con plan pero fuera de fecha: caducada igual.
        grant.HasExpired(Now.AddDays(11), hasPlanAccess: true).Should().BeTrue();
    }

    [Fact]
    public void A_package_that_expires_by_date_needs_a_number_of_days()
    {
        var invalid = TutoringPackage.Create(
            Guid.CreateVersion7(), "pack", "Pack", "", 60, Money.Euros(9900),
            TutoringExpiry.FixedDate, validityDays: null, displayOrder: 1);

        invalid.IsFailure.Should().BeTrue();
        invalid.Error.Code.Should().Be("tutoring_package.validity_required");
    }

    [Fact]
    public void A_subscription_package_does_not_keep_a_number_of_days()
    {
        var package = TutoringPackage.Create(
            Guid.CreateVersion7(), "tutoria-1h", "1 tutoría", "", 60, Money.Euros(9900),
            TutoringExpiry.WithSubscription, validityDays: 180, displayOrder: 1).Value;

        // Los días se descartan: en este modo la fecha la pone la suscripción, y un número
        // guardado aquí sería un dato muerto que alguien acabaría leyendo como si mandara.
        package.ValidityDays.Should().BeNull();

        var grant = TutoringGrant.FromPackage(
            Guid.CreateVersion7(), Guid.CreateVersion7(), package,
            TutoringGrantSource.Purchase, purchaseId: null, Now, "", null).Value;

        grant.ExpiryMode.Should().Be(TutoringExpiry.WithSubscription);
        grant.ExpiresAt.Should().BeNull();
    }

    private static TutoringGrant Grant(int minutes, DateTimeOffset? expiresAt = null) =>
        TutoringGrant.Create(
            Guid.CreateVersion7(), Guid.CreateVersion7(), packageId: null, "Tutorías",
            minutes, TutoringGrantSource.Purchase, purchaseId: null, Now,
            expiresAt is null ? TutoringExpiry.Never : TutoringExpiry.FixedDate,
            expiresAt, "", null).Value;

    /// <summary>Una bolsa de las que se venden: vive mientras la suscripción dé acceso.</summary>
    private static TutoringGrant SubscriptionGrant(int minutes) =>
        TutoringGrant.Create(
            Guid.CreateVersion7(), Guid.CreateVersion7(), packageId: null, "Tutorías",
            minutes, TutoringGrantSource.Purchase, purchaseId: null, Now,
            TutoringExpiry.WithSubscription, expiresAt: null, "", null).Value;

    private static TutoringPackage Package(int minutes, int? validityDays = null) =>
        TutoringPackage.Create(
            Guid.CreateVersion7(), "pack-5-tutorias", "Pack de 5 tutorías", "",
            minutes, Money.Euros(45000),
            validityDays is null ? TutoringExpiry.Never : TutoringExpiry.FixedDate,
            validityDays, displayOrder: 1).Value;
}
