using Dapper;
using FluentAssertions;
using Inkoova.Academy.Application.Abstractions;
using Inkoova.Academy.Infrastructure.Persistence;

namespace Inkoova.Academy.Integration.Tests;

/// <summary>
/// El control de flujo del envío a la AEAT.
///
/// La agencia impone un tiempo de espera entre envíos —60 segundos de partida— y devuelve el
/// vigente en cada respuesta. Enviar antes de tiempo es lo que el control prohíbe, y su
/// respuesta es subir la espera: incumplirlo hace que se tarde MÁS en ponerse al día, no menos.
///
/// Se guarda en la base y no en memoria porque un reinicio del proceso volvería a empezar de
/// cero y podría enviar antes de tiempo sin enterarse.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class VerifactuFlowTests(DatabaseFixture fixture)
{
    private static readonly DateTimeOffset Now = new(2026, 9, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly CancellationToken Ct = CancellationToken.None;

    [Fact]
    public async Task An_issuer_with_no_history_can_send_right_away()
    {
        var (records, nif) = Arrange();

        // Sin fila no hay espera pendiente: el primer envío no tiene nada que respetar.
        (await records.GetFlowAsync(nif, Ct)).Should().BeNull();
    }

    [Fact]
    public async Task The_wait_the_agency_asks_for_is_what_gets_stored()
    {
        var (records, nif) = Arrange();

        await records.SaveFlowAsync(new VerifactuFlow(nif, 120, Now.AddSeconds(120)), Now, Ct);

        var flow = await records.GetFlowAsync(nif, Ct);

        // 120 y no los 60 de partida: si la AEAT sube el tiempo es que está pidiendo que se
        // afloje, y usar un valor propio sería ignorarla.
        flow!.WaitSeconds.Should().Be(120);
        flow.NextAllowedAt.Should().Be(Now.AddSeconds(120));
    }

    [Fact]
    public async Task A_later_answer_replaces_the_previous_wait()
    {
        var (records, nif) = Arrange();

        await records.SaveFlowAsync(new VerifactuFlow(nif, 60, Now.AddSeconds(60)), Now, Ct);

        var later = Now.AddMinutes(5);
        await records.SaveFlowAsync(new VerifactuFlow(nif, 30, later.AddSeconds(30)), later, Ct);

        var flow = await records.GetFlowAsync(nif, Ct);

        flow!.WaitSeconds.Should().Be(30);
        flow.NextAllowedAt.Should().Be(later.AddSeconds(30));
    }

    [Fact]
    public async Task Each_issuer_keeps_its_own_pace()
    {
        var (records, nif) = Arrange();
        var other = $"B{Random.Shared.Next(10_000_000, 99_999_999)}";

        await records.SaveFlowAsync(new VerifactuFlow(nif, 60, Now.AddSeconds(60)), Now, Ct);

        // Cada NIF tiene su cadena y su ritmo: la espera de uno no puede frenar al otro.
        (await records.GetFlowAsync(other, Ct)).Should().BeNull();
        (await records.GetFlowAsync(nif, Ct))!.WaitSeconds.Should().Be(60);
    }

    [Fact]
    public async Task The_batch_never_goes_over_what_the_agency_admits()
    {
        // Mil por envío, según la descripción de servicios web. El límite está en una constante
        // y no repartido por el código, para que subirlo o bajarlo sea un solo cambio.
        VerifactuLimits.MaxRecordsPerSubmission.Should().Be(1000);
        VerifactuLimits.InitialWaitSeconds.Should().Be(60);

        var (records, nif) = Arrange();

        // Y la consulta respeta el límite que se le pida, que es con lo que se trocea.
        var lote = await records.GetUnsubmittedAsync(nif, VerifactuLimits.MaxRecordsPerSubmission, Ct);
        lote.Count.Should().BeLessThanOrEqualTo(VerifactuLimits.MaxRecordsPerSubmission);
    }

    [Fact]
    public async Task The_stored_pace_survives_a_restart()
    {
        var (records, nif) = Arrange();

        await records.SaveFlowAsync(new VerifactuFlow(nif, 90, Now.AddSeconds(90)), Now, Ct);

        // Un repositorio nuevo, como el que crearía un proceso recién arrancado: la espera sigue
        // ahí. Si viviera en memoria, reiniciar sería una forma de saltarse el control de flujo.
        var reiniciado = new VerifactuRepository(new NpgsqlConnectionFactory(fixture.ConnectionString));

        (await reiniciado.GetFlowAsync(nif, Ct))!.NextAllowedAt.Should().Be(Now.AddSeconds(90));
    }

    private (VerifactuRepository Records, string Nif) Arrange()
    {
        DapperTypeHandlers.Register();

        return (new VerifactuRepository(new NpgsqlConnectionFactory(fixture.ConnectionString)),
            $"B{Random.Shared.Next(10_000_000, 99_999_999)}");
    }
}
