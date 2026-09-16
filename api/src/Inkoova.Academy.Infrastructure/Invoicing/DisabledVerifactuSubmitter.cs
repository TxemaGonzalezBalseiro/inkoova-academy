using Inkoova.Academy.Application.Abstractions;
using Inkoova.Academy.Domain.Billing;

namespace Inkoova.Academy.Infrastructure.Invoicing;

/// <summary>
/// El envío a la AEAT, apagado.
///
/// Es lo que hay mientras no se conecte la librería Veri*Factu que ya existe fuera de este
/// repositorio, que es la que sabe componer el XML del registro, firmarlo con el certificado
/// cualificado y hablar con el servicio de la AEAT. Nada de eso se puede escribir de memoria:
/// una factura declarada con un formato inventado la rechaza Hacienda, y una que se dé por
/// declarada sin haberlo sido es peor todavía.
///
/// Con esto puesto, todo lo demás funciona y es comprobable —numeración, encadenamiento,
/// verificación de la cadena, PDF, panel— y los asientos nacen como «no hay que remitir», que
/// dice la verdad: hoy no hay a dónde. El día que se conecte la librería, se sustituye esta
/// clase y los asientos empiezan a nacer pendientes.
/// </summary>
public sealed class DisabledVerifactuSubmitter : IVerifactuSubmitter
{
    public bool IsEnabled => false;

    public Task<VerifactuBatchResult> SubmitAsync(
        IReadOnlyList<VerifactuSubmission> batch, CancellationToken ct) =>
        Task.FromResult(new VerifactuBatchResult(
            [.. batch.Select(_ => new VerifactuSubmissionResult(
                Accepted: false,
                SubmissionId: null,
                "El envío a la AEAT no está configurado. Falta conectar la librería Veri*Factu " +
                "y el certificado."))],
            VerifactuLimits.InitialWaitSeconds));
}
