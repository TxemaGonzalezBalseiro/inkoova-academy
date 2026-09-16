using Dapper;
using FluentAssertions;
using Inkoova.Academy.Application.Billing;
using Inkoova.Academy.Domain.Billing;
using Inkoova.Academy.Infrastructure.Persistence;

namespace Inkoova.Academy.Integration.Tests;

/// <summary>
/// La cadena de registros de facturación, contra Postgres de verdad.
///
/// Lo que se prueba aquí no se puede probar en memoria: que la base impide bifurcar la cadena
/// cuando dos cobros se procesan a la vez, y que la verificación detecta una fila tocada a mano.
/// Las dos cosas son las que hacen que el encadenamiento sirva para algo.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class VerifactuChainTests(DatabaseFixture fixture)
{
    private static readonly DateTimeOffset Now = new(2026, 8, 31, 10, 0, 0, TimeSpan.Zero);
    private static readonly CancellationToken Ct = CancellationToken.None;

    [Fact]
    public async Task A_chain_of_records_round_trips_and_verifies()
    {
        var (records, verifier, nif) = Arrange();

        var first = await SaveAsync(records, nif, "INK-000001", VerifactuRecord.GenesisHash, 12100, Now);
        var second = await SaveAsync(records, nif, "INK-000002", first.Hash, 24200, Now.AddMinutes(1));

        var report = await verifier.VerifyAsync(nif, Ct);

        report.Records.Should().Be(2);
        report.IsIntact.Should().BeTrue();
        report.Broken.Should().BeEmpty();
        report.Links[0].SeriesNumber.Should().Be("INK-000001");
        report.Links[1].PreviousHash.Should().Be(first.Hash);
        second.PreviousHash.Should().Be(first.Hash);
    }

    [Fact]
    public async Task Two_records_cannot_chain_from_the_same_predecessor()
    {
        var (records, _, nif) = Arrange();

        var first = await SaveAsync(records, nif, "INK-000001", VerifactuRecord.GenesisHash, 12100, Now);
        await SaveAsync(records, nif, "INK-000002", first.Hash, 12100, Now.AddMinutes(1));

        // Dos cobros a la vez leen el mismo «último registro». Sin el índice único, los dos
        // encadenarían desde ahí y la cadena quedaría en Y sin que nadie lo viera hasta ir a
        // verificarla.
        var fork = Build(await NewInvoiceAsync(), nif, "INK-000003", first.Hash, 12100, Now.AddMinutes(2));

        var attempt = async () => await records.InsertAsync(fork, Ct);

        await attempt.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task A_row_edited_by_hand_shows_up_as_broken_and_says_which_one()
    {
        var (records, verifier, nif) = Arrange();

        var first = await SaveAsync(records, nif, "INK-000001", VerifactuRecord.GenesisHash, 12100, Now);
        await SaveAsync(records, nif, "INK-000002", first.Hash, 24200, Now.AddMinutes(1));

        // Alguien cambia el importe directamente en la base, como quien "corrige" una factura.
        // El texto firmado sigue diciendo lo de antes, así que la huella deja de cuadrar.
        using (var connection = await new NpgsqlConnectionFactory(fixture.ConnectionString).OpenAsync(Ct))
        {
            await connection.ExecuteAsync(
                "UPDATE verifactu_record SET hash = @hash WHERE id = @id",
                new { hash = new string('A', 64), id = first.Id });
        }

        var report = await verifier.VerifyAsync(nif, Ct);

        report.IsIntact.Should().BeFalse();
        // Dos eslabones rotos: el tocado, cuya huella ya no cuadra con lo que firmó, y el
        // siguiente, que encadenaba con la huella vieja.
        report.Broken.Should().HaveCount(2);
        report.Broken[0].SeriesNumber.Should().Be("INK-000001");
        report.Broken[0].HashIsIntact.Should().BeFalse();
        report.Broken[1].SeriesNumber.Should().Be("INK-000002");
        report.Broken[1].ChainsFromPrevious.Should().BeFalse();
    }

    [Fact]
    public async Task Each_issuer_has_its_own_chain()
    {
        var (records, verifier, nif) = Arrange();
        var other = $"B{Random.Shared.Next(10_000_000, 99_999_999)}";

        await SaveAsync(records, nif, "INK-000001", VerifactuRecord.GenesisHash, 12100, Now);
        await SaveAsync(records, other, "OCC-000001", VerifactuRecord.GenesisHash, 5000, Now);

        // Las dos empiezan por el génesis y ninguna depende de la otra: si compartieran cadena,
        // la factura de una marca dependería de la de otra.
        (await verifier.VerifyAsync(nif, Ct)).IsIntact.Should().BeTrue();
        (await verifier.VerifyAsync(other, Ct)).Records.Should().Be(1);
    }

    [Fact]
    public async Task What_is_not_submitted_yet_is_listed_for_retry()
    {
        var (records, _, nif) = Arrange();

        await SaveAsync(records, nif, "INK-000001", VerifactuRecord.GenesisHash, 12100, Now,
            submissionRequired: true);

        var pending = await records.GetUnsubmittedAsync(nif, 10, Ct);

        pending.Should().Contain(r => r.SeriesNumber == "INK-000001");

        var mine = pending.First(r => r.SeriesNumber == "INK-000001");
        mine.MarkSent("AEAT-0001", Now).IsSuccess.Should().BeTrue();
        await records.UpdateSubmissionAsync(mine, Ct);

        (await records.GetUnsubmittedAsync(nif, 10, Ct))
            .Should().NotContain(r => r.SeriesNumber == "INK-000001");

        // Y lo declarado no se ha movido al actualizar el envío: solo cambian sus columnas.
        var reloaded = await records.GetLastAsync(nif, Ct);
        reloaded!.Hash.Should().Be(mine.Hash);
        reloaded.AeatSubmissionId.Should().Be("AEAT-0001");
    }

    private (VerifactuRepository Records, VerifyVerifactuChainHandler Verifier, string Nif)
        Arrange()
    {
        DapperTypeHandlers.Register();

        var connections = new NpgsqlConnectionFactory(fixture.ConnectionString);
        var records = new VerifactuRepository(connections);

        // Un NIF por test: los tests comparten la base del contenedor y las cadenas se separan
        // por emisor, así que uno fijo haría que un test viera los registros de otro.
        return (records, new VerifyVerifactuChainHandler(records),
            $"B{Random.Shared.Next(10_000_000, 99_999_999)}");
    }

    /// <summary>
    /// Una factura nueva, con su cliente.
    ///
    /// Una por registro y no una compartida: la base solo admite un alta por factura, que es lo
    /// correcto —un segundo alta declararía dos veces el mismo hecho imponible— y aquí hay que
    /// respetarlo igual que en producción.
    /// </summary>
    private async Task<Guid> NewInvoiceAsync()
    {
        using var connection = await new NpgsqlConnectionFactory(fixture.ConnectionString).OpenAsync(Ct);

        var userId = Guid.CreateVersion7();
        var invoiceId = Guid.CreateVersion7();
        var email = $"factura-{userId:N}@test.local";

        await connection.ExecuteAsync(
            """
            INSERT INTO identity_user (id, normalized_email, email, email_confirmed, password_hash,
                                       security_stamp, concurrency_stamp, lockout_end,
                                       lockout_enabled, access_failed_count, created_at)
            VALUES (@userId, @normalized, @email, true, 'hash', 'stamp', 'concurrency', null, true, 0, @now);

            INSERT INTO app_user (id, email, display_name, created_at)
            VALUES (@userId, @email, 'Cliente de prueba', @now);

            INSERT INTO fiscal_invoice (id, user_id, series, number, issue_date, total_cents,
                                        tax_cents, currency, stripe_invoice_id, previous_hash, hash)
            VALUES (@invoiceId, @userId, 'TEST', @number, @issueDate, 12100, 2100, 'EUR',
                    @stripeId, @zeros, @zeros);
            """,
            new
            {
                userId,
                normalized = email.ToUpperInvariant(),
                email,
                now = Now,
                invoiceId,
                // El número es único por serie: se usa el reloj para no chocar entre tests.
                number = (int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() % 2_000_000_000),
                issueDate = new DateOnly(2026, 8, 31),
                stripeId = $"in_test_{invoiceId:N}",
                zeros = VerifactuRecord.GenesisHash
            });

        return invoiceId;
    }

    private async Task<VerifactuRecord> SaveAsync(
        VerifactuRepository records,
        string nif,
        string number,
        string previousHash,
        long totalCents,
        DateTimeOffset generatedAt,
        bool submissionRequired = false)
    {
        var record = Build(
            await NewInvoiceAsync(), nif, number, previousHash, totalCents, generatedAt,
            submissionRequired);

        await records.InsertAsync(record, Ct);

        return record;
    }

    private static VerifactuRecord Build(
        Guid invoiceId,
        string nif,
        string number,
        string previousHash,
        long totalCents,
        DateTimeOffset generatedAt,
        bool submissionRequired = false) =>
        VerifactuRecord.Create(
            Guid.CreateVersion7(), invoiceId, VerifactuKind.Alta, nif, "Academia de prueba",
            "F2", number, new DateOnly(2026, 8, 31), "Suscripción mensual",
            totalCents, totalCents / 121 * 21, "EUR", null, previousHash,
            generatedAt, submissionRequired).Value;
}
