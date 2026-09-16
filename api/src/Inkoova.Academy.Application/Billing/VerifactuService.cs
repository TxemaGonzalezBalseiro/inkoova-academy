using Inkoova.Academy.Application.Abstractions;
using Inkoova.Academy.Domain.Billing;
using Inkoova.Academy.Domain.Common;
using Microsoft.Extensions.Logging;

namespace Inkoova.Academy.Application.Billing;

/// <summary>
/// El registro de facturación de Veri*Factu: crearlo al emitir una factura, encadenarlo con el
/// anterior de su emisor y remitirlo a la AEAT cuando haya con qué.
///
/// Se llama desde la emisión de facturas, que a su vez cuelga del webhook de Stripe, así que
/// aplica la misma regla: <b>nunca hace fallar a quien lo llama</b>. El cobro ya ocurrió; si el
/// asiento no se puede crear, se registra el fallo y se rehace desde el panel. Tirar el webhook
/// haría que Stripe reintentase el evento entero y duplicara accesos.
/// </summary>
public sealed class VerifactuService(
    IVerifactuRepository records,
    IVerifactuSubmitter submitter,
    IAcademyIdentityRepository identities,
    VerifactuSoftware software,
    IClock clock,
    ILogger<VerifactuService> logger)
{
    /// <summary>
    /// Si el sistema está en condiciones de remitir: hay envío conectado y el bloque que
    /// describe el software está relleno.
    ///
    /// Se pregunta antes de emitir y se enseña en el panel. Un registro al que le falte el
    /// bloque del sistema informático lo rechaza la AEAT entero, y enterarse en el primer cobro
    /// real es tarde.
    /// </summary>
    public bool CanSubmit => submitter.IsEnabled && software.IsComplete;

    public VerifactuSoftware Software => software;

    /// <summary>
    /// Quién emite, a partir de la marca. Sale de sus datos legales y NO de la configuración:
    /// con varias marcas, el emisor es la que vendió, con su razón social y su NIF.
    ///
    /// Devuelve error en vez de rellenar huecos cuando faltan datos. Un NIF inventado en una
    /// factura no es un hueco que se vea: es un dato falso que alguien se cree.
    /// </summary>
    public async Task<Result<InvoiceIssuer, Error>> ResolveIssuerAsync(
        Guid? identityId, CancellationToken ct)
    {
        var identity = identityId is { } id
            ? await identities.GetByIdAsync(id, ct) ?? await identities.GetDefaultAsync(ct)
            : await identities.GetDefaultAsync(ct);

        var legal = identity.Legal;

        if (string.IsNullOrWhiteSpace(legal.TaxId) || string.IsNullOrWhiteSpace(legal.LegalName))
        {
            return Error.Validation(
                "verifactu.issuer_incomplete",
                $"Faltan los datos fiscales de «{identity.Name}»: completa razón social y NIF " +
                "en Marcas → Datos legales antes de facturar.");
        }

        return new InvoiceIssuer(
            identity.Id,
            legal.LegalName,
            legal.TaxId,
            legal.Address,
            legal.InvoiceType,
            legal.CorrectiveInvoiceType);
    }

    /// <summary>
    /// Compone el asiento de alta y lo encadena con el último del emisor, SIN guardarlo.
    ///
    /// Va en dos pasos —componer y guardar— porque el asiento referencia a su factura por clave
    /// ajena, y la factura no se puede insertar antes: necesita la huella, que sale de aquí. El
    /// orden es componer → imprimir → guardar la factura → guardar el asiento.
    /// </summary>
    public async Task<Result<VerifactuRecord, Error>> PrepareAltaAsync(
        Guid invoiceId,
        InvoiceIssuer issuer,
        string seriesNumber,
        DateOnly issueDate,
        string description,
        long totalCents,
        long taxCents,
        string currency,
        string? rectifies,
        CancellationToken ct)
    {
        var previous = await records.GetLastAsync(issuer.TaxId, ct);

        var record = VerifactuRecord.Create(
            Guid.CreateVersion7(),
            invoiceId,
            VerifactuKind.Alta,
            issuer.TaxId,
            issuer.Name,
            // La clave sale de la marca (Marcas → Datos legales), no del código: qué tipo
            // corresponde a cada venta lo decide quien lleva la fiscalidad y cambia con el
            // negocio —empezar a pedir el NIF al cliente mueve la venta de F2 a F1—, así que
            // no puede depender de un despliegue. Los valores admitidos los fija el esquema
            // de la AEAT y los valida `LegalDetails`.
            rectifies is null ? issuer.InvoiceType : issuer.CorrectiveInvoiceType,
            seriesNumber,
            issueDate,
            description,
            totalCents,
            taxCents,
            currency,
            rectifies,
            previous?.Hash ?? VerifactuRecord.GenesisHash,
            clock.UtcNow,
            submitter.IsEnabled);

        return record;
    }

    /// <summary>
    /// Guarda el asiento y, si hay envío configurado, lo remite.
    ///
    /// El índice único de la base impide que dos asientos del mismo emisor encadenen desde la
    /// misma huella: si dos cobros se procesan a la vez, el segundo falla aquí en vez de partir
    /// la cadena en dos ramas que nadie vería hasta ir a verificarla.
    /// </summary>
    public async Task<Result<VerifactuRecord, Error>> SaveAsync(VerifactuRecord record, CancellationToken ct)
    {
        try
        {
            await records.InsertAsync(record, ct);
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            logger.LogError(
                failure,
                "No se ha podido guardar el registro de {Number}. Puede que otro cobro haya " +
                "encadenado antes desde la misma huella.",
                record.SeriesNumber);

            return Error.Conflict(
                "verifactu.chain_conflict",
                "No se ha podido encadenar el registro. Vuelve a emitir la factura.");
        }

        // Aquí NO se remite. Queda pendiente y lo recoge el proceso por lotes.
        //
        // La AEAT impone un tiempo de espera entre envíos —60 segundos de partida—, así que
        // remitir en cuanto se cobra incumpliría el control de flujo en cuanto entren dos ventas
        // en el mismo minuto. Y una plataforma de cursos vende sola a cualquier hora.
        return record;
    }

    /// <summary>El asiento de alta de una factura, si ya lo tiene.</summary>
    public Task<VerifactuRecord?> GetAltaAsync(Guid invoiceId, CancellationToken ct) =>
        records.GetByInvoiceAsync(invoiceId, VerifactuKind.Alta, ct);

    /// <summary>
    /// El desglose de IVA de una factura.
    ///
    /// Sale de restar: la base es el total menos la cuota, y el tipo se deduce de las dos. Vale
    /// mientras cada factura lleve UN solo tipo impositivo, que es el caso hoy.
    ///
    /// TODO(T-14): cuando una factura pueda mezclar tipos —un pack con algo exento, por
    /// ejemplo— esto tiene que venir de las líneas reales. Deducir el tipo de un total mezclado
    /// da un desglose que cuadra en el importe y miente en el reparto.
    /// </summary>
    public static IReadOnlyList<TaxBreakdownLine> Breakdown(long totalCents, long taxCents)
    {
        var baseCents = totalCents - taxCents;

        if (baseCents <= 0)
        {
            // Sin base no hay tipo que deducir: una línea al 0 % es lo único que se puede decir
            // con verdad. Pasa en una factura exenta o a importe cero.
            return [new TaxBreakdownLine(0m, Math.Max(baseCents, 0), taxCents)];
        }

        // A dos decimales, que es la precisión con la que se declaran los tipos.
        var rate = Math.Round(taxCents * 100m / baseCents, 2, MidpointRounding.AwayFromZero);

        return [new TaxBreakdownLine(rate, baseCents, taxCents)];
    }

    /// <summary>
    /// Remite un lote de pendientes de un emisor, respetando el control de flujo de la AEAT.
    ///
    /// Devuelve cuántos aceptó. Cero puede significar tres cosas distintas y todas normales: que
    /// no había nada pendiente, que todavía no toca enviar, o que la agencia rechazó el lote; el
    /// registro dice cuál.
    ///
    /// Un rechazo NO es una excepción: es una respuesta que se guarda con su motivo para poder
    /// corregirla. Lo que sí se traga es un fallo de red, porque los asientos ya están guardados
    /// y el siguiente pase los recoge.
    /// </summary>
    public async Task<int> SubmitPendingAsync(string issuerTaxId, CancellationToken ct)
    {
        if (!submitter.IsEnabled || !software.IsComplete)
        {
            return 0;
        }

        var now = clock.UtcNow;
        var flow = await records.GetFlowAsync(issuerTaxId, ct);

        if (flow is not null && flow.NextAllowedAt > now)
        {
            // Todavía no toca. Enviar antes de tiempo es lo que el control de flujo prohíbe, y
            // la AEAT responde subiendo la espera, con lo que se tarda más en ponerse al día.
            return 0;
        }

        var pending = await records.GetUnsubmittedAsync(
            issuerTaxId, VerifactuLimits.MaxRecordsPerSubmission, ct);

        if (pending.Count == 0)
        {
            return 0;
        }

        VerifactuBatchResult answer;

        try
        {
            answer = await submitter.SubmitAsync(await ComposeBatchAsync(pending, ct), ct);
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            logger.LogError(
                failure,
                "No se ha podido remitir el lote de {Count} registros de {Nif}. Quedan pendientes.",
                pending.Count, issuerTaxId);

            // Se respeta la espera igualmente: si la agencia no contesta, insistir cada segundo
            // no la va a hacer contestar antes.
            await SaveFlowAsync(issuerTaxId, VerifactuLimits.InitialWaitSeconds, now, ct);
            return 0;
        }

        var accepted = 0;

        // Una respuesta por registro, en el mismo orden. Si vinieran menos de las mandadas, los
        // que sobran se quedan pendientes en vez de darse por buenos.
        foreach (var (record, result) in pending.Zip(answer.Results))
        {
            if (result.Accepted)
            {
                var marked = record.MarkSent(result.SubmissionId ?? string.Empty, now);

                if (marked.IsFailure)
                {
                    // Aceptado pero sin CSV: no se puede volver a preguntar por él, así que se
                    // deja como rechazado para que alguien lo mire.
                    record.MarkRejected(
                        "La AEAT aceptó el registro pero no devolvió el CSV.", now);
                }
                else
                {
                    accepted++;
                }
            }
            else
            {
                record.MarkRejected(result.Error, now);
                logger.LogError(
                    "La AEAT ha rechazado el registro {Number}: {Error}",
                    record.SeriesNumber, result.Error);
            }

            await records.UpdateSubmissionAsync(record, ct);
        }

        await SaveFlowAsync(issuerTaxId, answer.WaitSeconds, now, ct);

        logger.LogInformation(
            "Remitidos {Accepted} de {Total} registros de {Nif}. Próximo envío en {Wait} s.",
            accepted, pending.Count, issuerTaxId, answer.WaitSeconds);

        return accepted;
    }

    /// <summary>Remite lo pendiente de todos los emisores que tengan cadena.</summary>
    public async Task<int> SubmitPendingAsync(CancellationToken ct)
    {
        if (!submitter.IsEnabled || !software.IsComplete)
        {
            return 0;
        }

        var total = 0;

        foreach (var issuer in await records.GetIssuersAsync(ct))
        {
            total += await SubmitPendingAsync(issuer, ct);
        }

        return total;
    }

    /// <summary>
    /// Todo lo que necesita el XML de cada registro del lote, junto.
    ///
    /// La parte que cuesta es el <c>Encadenamiento</c>: pide identificar el asiento anterior por
    /// número de serie y fecha, no solo por su huella. Casi siempre ese anterior está en el
    /// mismo lote —van en orden de cadena—, así que se resuelve con un índice en memoria y solo
    /// se va a la base por el primero, cuyo anterior ya se remitió en un envío previo.
    /// </summary>
    private async Task<IReadOnlyList<VerifactuSubmission>> ComposeBatchAsync(
        IReadOnlyList<VerifactuRecord> batch, CancellationToken ct)
    {
        var byHash = batch.ToDictionary(r => r.Hash, StringComparer.Ordinal);
        var composed = new List<VerifactuSubmission>(batch.Count);

        foreach (var record in batch)
        {
            VerifactuRecord? previous = null;

            if (record.PreviousHash != VerifactuRecord.GenesisHash
                && !byHash.TryGetValue(record.PreviousHash, out previous))
            {
                previous = await records.GetByHashAsync(record.IssuerTaxId, record.PreviousHash, ct);
            }

            composed.Add(new VerifactuSubmission(
                record,
                new InvoiceIssuer(Guid.Empty, record.IssuerName, record.IssuerTaxId, string.Empty),
                software,
                Breakdown(record.TotalCents, record.TaxCents),
                // El destinatario solo va en las facturas con destinatario identificado. Hoy se
                // emiten simplificadas, así que no hay ninguno que informar.
                CustomerName: null,
                CustomerTaxId: null,
                CustomerCountry: "ES",
                PreviousSeriesNumber: previous?.SeriesNumber,
                PreviousIssueDate: previous?.IssueDate));
        }

        return composed;
    }

    /// <summary>
    /// Apunta hasta cuándo hay que esperar. Un valor raro —cero o negativo— se trata como el de
    /// partida: dar por bueno un cero convertiría el control de flujo en ninguno.
    /// </summary>
    private async Task SaveFlowAsync(
        string issuerTaxId, int waitSeconds, DateTimeOffset sentAt, CancellationToken ct)
    {
        var wait = waitSeconds > 0 ? waitSeconds : VerifactuLimits.InitialWaitSeconds;

        await records.SaveFlowAsync(
            new VerifactuFlow(issuerTaxId, wait, sentAt.AddSeconds(wait)), sentAt, ct);
    }
}

/// <summary>Qué le pasa a un eslabón concreto de la cadena.</summary>
public sealed record ChainLinkDto(
    Guid RecordId,
    string SeriesNumber,
    DateOnly IssueDate,
    DateTimeOffset GeneratedAt,
    string Kind,
    long TotalCents,
    string Currency,
    string Hash,
    string PreviousHash,
    string SubmissionState,
    string SubmissionError,
    // Su huella cuadra con lo que dice haber firmado.
    bool HashIsIntact,
    // Y engancha con el eslabón anterior.
    bool ChainsFromPrevious);

public sealed record ChainReportDto(
    string IssuerTaxId,
    int Records,
    bool IsIntact,
    int Pending,
    int Rejected,
    IReadOnlyList<ChainLinkDto> Broken,
    IReadOnlyList<ChainLinkDto> Links);

/// <summary>
/// Verifica una cadena entera: que cada asiento conserve su huella y que enganche con el
/// anterior.
///
/// Es lo que convierte el encadenamiento en algo comprobable en vez de en un campo que nadie
/// mira. Si alguien tocara una fila a mano en la base de datos, esto lo enseña y dice dónde.
/// </summary>
public sealed class VerifyVerifactuChainHandler(IVerifactuRepository records)
{
    public async Task<IReadOnlyList<ChainReportDto>> HandleAsync(CancellationToken ct)
    {
        var issuers = await records.GetIssuersAsync(ct);
        var reports = new List<ChainReportDto>(issuers.Count);

        foreach (var issuer in issuers)
        {
            reports.Add(await VerifyAsync(issuer, ct));
        }

        return reports;
    }

    public async Task<ChainReportDto> VerifyAsync(string issuerTaxId, CancellationToken ct)
    {
        var chain = await records.GetChainAsync(issuerTaxId, ct);
        var links = new List<ChainLinkDto>(chain.Count);

        VerifactuRecord? previous = null;

        foreach (var record in chain)
        {
            links.Add(new ChainLinkDto(
                record.Id,
                record.SeriesNumber,
                record.IssueDate,
                record.GeneratedAt,
                record.Kind == VerifactuKind.Anulacion ? "anulacion" : "alta",
                record.TotalCents,
                record.Currency,
                record.Hash,
                record.PreviousHash,
                StateText(record.State),
                record.SubmissionError,
                record.HashIsIntact(),
                record.ChainsFrom(previous)));

            previous = record;
        }

        var broken = links.Where(l => !l.HashIsIntact || !l.ChainsFromPrevious).ToList();

        return new ChainReportDto(
            issuerTaxId,
            links.Count,
            broken.Count == 0,
            links.Count(l => l.SubmissionState == "pending"),
            links.Count(l => l.SubmissionState == "rejected"),
            broken,
            links);
    }

    private static string StateText(SubmissionState state) => state switch
    {
        SubmissionState.Sent => "sent",
        SubmissionState.Rejected => "rejected",
        SubmissionState.NotRequired => "not_required",
        _ => "pending"
    };
}
