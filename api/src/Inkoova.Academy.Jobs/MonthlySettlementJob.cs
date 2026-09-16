using Inkoova.Academy.Application.Abstractions;
using Inkoova.Academy.Application.Affiliates;

namespace Inkoova.Academy.Jobs;

/// <summary>
/// Runs on the first day of each month and settles the previous one (T-16). The period is
/// derived from the date rather than stored, so a re-run of the same day produces the same
/// period, and the settlement itself refuses to duplicate a payout for a period already closed.
/// </summary>
public sealed class MonthlySettlementJob(
    IServiceScopeFactory scopes,
    IClock clock,
    IConfiguration configuration,
    ILogger<MonthlySettlementJob> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var runAtHourUtc = configuration.GetValue("Academy:Jobs:SettlementHourUtc", 5);

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = TimeUntilNextFirstOfMonth(clock.UtcNow, runAtHourUtc);
            logger.LogInformation("Monthly settlement sleeping for {Delay}.", delay);

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            try
            {
                var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
                var periodStart = new DateOnly(today.Year, today.Month, 1).AddMonths(-1);
                var periodEnd = periodStart.AddMonths(1).AddDays(-1);

                await RunForPeriodAsync(periodStart, periodEnd, stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Monthly settlement failed.");
            }
        }
    }

    public async Task RunForPeriodAsync(DateOnly periodStart, DateOnly periodEnd, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var settlement = scope.ServiceProvider.GetRequiredService<SettlementService>();

        var payouts = await settlement.RunForPeriodAsync(periodStart, periodEnd, ct);

        logger.LogInformation(
            "Settled {Count} affiliate payouts for {Period}.", payouts.Count, $"{periodStart:yyyy-MM}");
    }

    private static TimeSpan TimeUntilNextFirstOfMonth(DateTimeOffset now, int hourUtc)
    {
        var thisMonth = new DateTimeOffset(now.Year, now.Month, 1, hourUtc, 0, 0, TimeSpan.Zero);
        return thisMonth > now ? thisMonth - now : thisMonth.AddMonths(1) - now;
    }
}
