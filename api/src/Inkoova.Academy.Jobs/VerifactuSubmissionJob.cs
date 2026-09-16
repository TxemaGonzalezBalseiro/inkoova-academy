using Inkoova.Academy.Application.Abstractions;
using Inkoova.Academy.Application.Billing;

namespace Inkoova.Academy.Jobs;

/// <summary>
/// Remite a la AEAT lo que va quedando pendiente.
///
/// Es un proceso propio y no un paso del mantenimiento diario porque los registros hay que
/// remitirlos con prontitud, no una vez al día: una factura cobrada esta mañana no puede esperar
/// a las tres de la madrugada.
///
/// Y no se remite desde el cobro porque la AEAT impone un tiempo de espera ENTRE envíos —60
/// segundos de partida, y ella devuelve el vigente en cada respuesta—. Con envío inmediato, dos
/// ventas en el mismo minuto ya incumplen el control de flujo. Así que se acumulan y se mandan
/// por lotes de hasta mil.
///
/// El ritmo lo marca la propia agencia: <see cref="VerifactuService.SubmitPendingAsync(string,
/// CancellationToken)"/> mira hasta cuándo hay que esperar antes de cada lote, así que este
/// bucle puede despertarse a menudo sin riesgo de enviar antes de tiempo.
/// </summary>
public sealed class VerifactuSubmissionJob(
    IServiceScopeFactory scopes,
    IConfiguration configuration,
    ILogger<VerifactuSubmissionJob> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Con qué frecuencia se mira si toca enviar. No es el ritmo de envío —ese lo pone la
        // AEAT—, solo cada cuánto se comprueba.
        var pollSeconds = Math.Max(
            configuration.GetValue("Academy:Verifactu:PollSeconds", 30), 5);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(pollSeconds), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            try
            {
                using var scope = scopes.CreateScope();
                var verifactu = scope.ServiceProvider.GetRequiredService<VerifactuService>();

                if (!verifactu.CanSubmit)
                {
                    continue;
                }

                var sent = await verifactu.SubmitPendingAsync(stoppingToken);

                if (sent > 0)
                {
                    logger.LogInformation("Remitidos {Count} registros a la AEAT.", sent);
                }
            }
            catch (Exception ex)
            {
                // Igual que el mantenimiento diario: un fallo no puede matar el bucle. Lo que
                // no salga hoy sigue pendiente y lo recoge el pase siguiente, porque el estado
                // está en la base y no en una variable de este proceso.
                logger.LogError(ex, "El envío a la AEAT ha fallado. Se reintentará.");
            }
        }
    }
}
