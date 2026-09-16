using Inkoova.Academy.Application.Abstractions;
using Inkoova.Academy.Application.Access;
using Inkoova.Academy.Application.Affiliates;
using Inkoova.Academy.Application.Billing;
using Inkoova.Academy.Domain.Billing;

namespace Inkoova.Academy.Jobs;

/// <summary>
/// Once a day: withdraw expired plan access, approve matured commissions and reconcile
/// Discord roles. Every step is idempotent, so a double run or a restart mid-way changes
/// nothing that a single successful run would not have produced.
/// </summary>
public sealed class DailyMaintenanceJob(
    IServiceScopeFactory scopes,
    IClock clock,
    IConfiguration configuration,
    ILogger<DailyMaintenanceJob> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var runAtHourUtc = configuration.GetValue("Academy:Jobs:DailyHourUtc", 3);

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = TimeUntilNextRun(clock.UtcNow, runAtHourUtc);
            logger.LogInformation("Daily maintenance sleeping for {Delay}.", delay);

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
                await RunOnceAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                // Never let one bad night kill the loop: tomorrow's run picks up the backlog
                // because every step is driven by state, not by a cursor.
                logger.LogError(ex, "Daily maintenance failed.");
            }
        }
    }

    public async Task RunOnceAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var services = scope.ServiceProvider;

        var entitlements = services.GetRequiredService<EntitlementService>();
        var revoked = await entitlements.RevokeExpiredPlanEntitlementsAsync(ct);
        logger.LogInformation("Revoked {Count} expired plan entitlements.", revoked);

        var commissions = services.GetRequiredService<CommissionService>();
        var approved = await commissions.ApproveMaturedAsync(ct);
        logger.LogInformation("Approved {Count} matured commissions.", approved);

        // Lo que no se pudo remitir a la AEAT en su momento —red caída, servicio de la agencia
        // sin responder— se reintenta a diario. Sin esto, un registro pendiente se queda
        // pendiente para siempre, porque nada vuelve a intentarlo por su cuenta.
        //
        // Con el envío sin conectar no hace nada y no cuesta nada: los asientos nacen como «no
        // hay que remitir» y no aparecen en la lista de pendientes.
        var verifactu = services.GetRequiredService<VerifactuService>();
        var submitted = await verifactu.SubmitPendingAsync(ct);

        if (submitted > 0)
        {
            logger.LogInformation("Submitted {Count} pending Verifactu records.", submitted);
        }

        await SyncDiscordRolesAsync(services, ct);
    }

    /// <summary>
    /// Brings Discord in line with the current plan of every linked account. Doing it by
    /// reconciliation rather than by reacting to events means a missed webhook self-heals
    /// within a day (T-12 acceptance criteria: role removed within 24 h of expiry).
    /// </summary>
    private async Task SyncDiscordRolesAsync(IServiceProvider services, CancellationToken ct)
    {
        var links = services.GetRequiredService<IDiscordLinkRepository>();
        var subscriptions = services.GetRequiredService<ISubscriptionRepository>();
        var plans = services.GetRequiredService<IPlanRepository>();
        var discord = services.GetRequiredService<IDiscordGateway>();

        var changed = 0;

        foreach (var link in await links.GetAllAsync(ct))
        {
            var subscription = await subscriptions.GetActiveForUserAsync(link.UserId, ct);
            var plan = subscription is null ? null : await plans.GetByIdAsync(subscription.PlanId, ct);

            var shouldHave = subscription is not null
                             && subscription.GrantsAccessAt(clock.UtcNow, TimeSpan.Zero)
                             && subscription.Status == SubscriptionStatus.Active
                ? plan?.Code
                : null;

            if (shouldHave == link.LastAppliedRole)
            {
                continue;
            }

            if (link.LastAppliedRole is { } previous)
            {
                await discord.RemoveRoleAsync(link.DiscordUserId, previous, ct);
            }

            if (shouldHave is { } role)
            {
                await discord.AssignRoleAsync(link.DiscordUserId, role, ct);
            }

            await links.UpsertAsync(link with { LastAppliedRole = shouldHave }, ct);
            changed++;
        }

        logger.LogInformation("Discord roles reconciled for {Count} accounts.", changed);
    }

    private static TimeSpan TimeUntilNextRun(DateTimeOffset now, int hourUtc)
    {
        var today = new DateTimeOffset(now.Year, now.Month, now.Day, hourUtc, 0, 0, TimeSpan.Zero);
        return today > now ? today - now : today.AddDays(1) - now;
    }
}
