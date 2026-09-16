using FluentAssertions;
using Inkoova.Academy.Domain.Billing;

namespace Inkoova.Academy.Domain.Tests;

/// <summary>
/// El registro de facturación.
///
/// Es lo que se declara a Hacienda, así que lo que se comprueba no es que «funcione» sino que
/// no pueda mentir: que la huella cuadre con lo que dice haber firmado, que la cadena enganche
/// y que un dato tocado a mano se vea.
/// </summary>
public class VerifactuTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 31, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void The_first_record_of_a_chain_links_to_the_genesis_hash()
    {
        var record = Record(VerifactuRecord.GenesisHash);

        record.PreviousHash.Should().Be(VerifactuRecord.GenesisHash);
        record.ChainsFrom(null).Should().BeTrue();
        record.HashIsIntact().Should().BeTrue();
    }

    [Fact]
    public void Each_record_chains_to_the_previous_one()
    {
        var first = Record(VerifactuRecord.GenesisHash, number: "INK-000001");
        var second = Record(first.Hash, number: "INK-000002");

        second.ChainsFrom(first).Should().BeTrue();

        // Y no engancha con otro cualquiera: si enganchara, el encadenamiento no diría nada.
        var elsewhere = Record(VerifactuRecord.GenesisHash, number: "INK-000009");
        second.ChainsFrom(elsewhere).Should().BeFalse();
    }

    [Fact]
    public void Two_identical_invoices_get_different_hashes_because_the_chain_differs()
    {
        var first = Record(VerifactuRecord.GenesisHash, number: "INK-000001");
        var second = Record(first.Hash, number: "INK-000001");

        // Mismo número, mismo importe, misma fecha: la huella cambia porque la anterior cambia.
        // Es lo que hace que insertar una factura entre dos existentes sea detectable.
        second.Hash.Should().NotBe(first.Hash);
    }

    [Fact]
    public void The_hash_is_verifiable_against_what_it_says_it_signed()
    {
        var record = Record(VerifactuRecord.GenesisHash);

        record.HashIsIntact().Should().BeTrue();

        // Un registro con la huella cambiada a mano en la base de datos: el texto firmado sigue
        // ahí y ya no cuadra con la huella. Es lo que enseña el panel.
        var tampered = VerifactuRecord.Rehydrate(
            record.Id, record.InvoiceId, record.Kind, record.IssuerTaxId, record.IssuerName,
            record.InvoiceType, record.SeriesNumber, record.IssueDate, record.Description,
            record.TotalCents, record.TaxCents, record.Currency, record.Rectifies,
            record.PreviousHash,
            new string('A', 64),
            record.HashInput, record.GeneratedAt, SubmissionState.Pending, null, null, "", 0);

        tampered.HashIsIntact().Should().BeFalse();
    }

    [Fact]
    public void The_signed_text_carries_the_fields_the_regulation_names()
    {
        var record = Record(VerifactuRecord.GenesisHash);

        // Los campos y su orden salen de las especificaciones de la AEAT (v0.1.2, apartado 3),
        // y están fijados contra sus ejemplos oficiales en VerifactuHuellaOficialTests. El
        // importe va con punto: una coma cambiaría la huella según la cultura de la máquina.
        record.HashInput.Should().Contain("IDEmisorFactura=B12345678");
        record.HashInput.Should().Contain("NumSerieFactura=INK-000001");
        record.HashInput.Should().Contain("ImporteTotal=121.00");
        record.HashInput.Should().Contain("CuotaTotal=21.00");
        // El primer registro lleva `Huella=` VACÍO, no ceros: apartado 3 de las
        // especificaciones de la huella. Los ceros son solo NUESTRA marca interna.
        record.HashInput.Should().Contain("&Huella=&");
        // Con huso explícito, como los ejemplos de la AEAT. No con «Z».
        record.HashInput.Should().Contain("FechaHoraHusoGenRegistro=2026-08-31T10:00:00+00:00");
    }

    [Fact]
    public void A_record_without_the_issuer_tax_id_is_not_created()
    {
        var missing = VerifactuRecord.Create(
            Guid.CreateVersion7(), Guid.CreateVersion7(), VerifactuKind.Alta,
            "   ", "Academia", "F2", "INK-000001", new DateOnly(2026, 8, 31),
            "Suscripción", 12100, 2100, "EUR", null, VerifactuRecord.GenesisHash, Now, false);

        // Es preferible no emitir a emitir algo que la AEAT rechazará: sin NIF no hay quien
        // declare.
        missing.Error.Code.Should().Be("verifactu.issuer_tax_id_missing");
    }

    [Fact]
    public void With_submission_off_a_record_is_born_as_not_required_and_not_as_pending()
    {
        Record(VerifactuRecord.GenesisHash, submissionRequired: false)
            .State.Should().Be(SubmissionState.NotRequired);

        // «No enviado» es un problema y «no hacía falta enviarlo» no lo es. Confundirlos deja
        // una lista de pendientes que nadie mira porque siempre tiene cosas.
        Record(VerifactuRecord.GenesisHash, submissionRequired: true)
            .State.Should().Be(SubmissionState.Pending);
    }

    [Fact]
    public void An_accepted_submission_needs_the_id_that_lets_you_ask_about_it_again()
    {
        var record = Record(VerifactuRecord.GenesisHash, submissionRequired: true);

        record.MarkSent("   ", Now).Error.Code.Should().Be("verifactu.submission_id_missing");
        record.State.Should().Be(SubmissionState.Pending);

        record.MarkSent("AEAT-2026-0001", Now).IsSuccess.Should().BeTrue();
        record.State.Should().Be(SubmissionState.Sent);
        record.AeatSubmissionId.Should().Be("AEAT-2026-0001");
        // Un intento, no dos: el rechazado por falta de identificador no llegó a ningún sitio y
        // no toca nada. Contarlo inflaría el contador que sirve para ver qué se atasca.
        record.SubmissionAttempts.Should().Be(1);
    }

    [Fact]
    public void A_rejection_keeps_the_reason_because_it_says_what_to_fix()
    {
        var record = Record(VerifactuRecord.GenesisHash, submissionRequired: true);

        record.MarkRejected("Campo TipoFactura no válido", Now);

        record.State.Should().Be(SubmissionState.Rejected);
        record.SubmissionError.Should().Be("Campo TipoFactura no válido");
        record.SubmittedAt.Should().Be(Now);
    }

    [Fact]
    public void The_generation_timestamp_is_kept_in_utc()
    {
        var madrid = new DateTimeOffset(2026, 8, 31, 12, 0, 0, TimeSpan.FromHours(2));
        var record = Record(VerifactuRecord.GenesisHash, generatedAt: madrid);

        // La huella lleva la marca temporal: si dos máquinas con husos distintos la escribieran
        // diferente, la misma factura daría dos huellas.
        record.GeneratedAt.Offset.Should().Be(TimeSpan.Zero);
        // Con huso explícito, como los ejemplos de la AEAT. No con «Z».
        record.HashInput.Should().Contain("FechaHoraHusoGenRegistro=2026-08-31T10:00:00+00:00");
    }

    private static VerifactuRecord Record(
        string previousHash,
        string number = "INK-000001",
        bool submissionRequired = false,
        DateTimeOffset? generatedAt = null) =>
        VerifactuRecord.Create(
            Guid.CreateVersion7(), Guid.CreateVersion7(), VerifactuKind.Alta,
            "B12345678", "Academia de prueba", "F2", number, new DateOnly(2026, 8, 31),
            "Suscripción mensual", 12100, 2100, "EUR", null, previousHash,
            generatedAt ?? Now, submissionRequired).Value;
}
