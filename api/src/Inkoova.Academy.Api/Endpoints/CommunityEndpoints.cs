using Inkoova.Academy.Api.Common;
using Inkoova.Academy.Application.Abstractions;
using Inkoova.Academy.Domain.Common;

namespace Inkoova.Academy.Api.Endpoints;

public static class CommunityEndpoints
{
    public static void MapCommunityEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/community").WithTags("Comunidad").RequireAuthorization();

        group.MapGet("/sessions", async (
                ICommunitySessionRepository sessions,
                ISubscriptionRepository subscriptions,
                IPlanRepository plans,
                IClock clock,
                HttpContext context,
                CancellationToken ct) =>
            {
                var userId = context.User.RequireUserId();
                var subscription = await subscriptions.GetActiveForUserAsync(userId, ct);
                var plan = subscription is null ? null : await plans.GetByIdAsync(subscription.PlanId, ct);

                var upcoming = await sessions.GetUpcomingAsync(clock.UtcNow, ct);

                // Sessions the plan does not include are hidden entirely rather than shown
                // greyed out: the calendar is the benefit, not a teaser (T-12).
                var visible = upcoming
                    .Where(s => s.RequiredPlanCodes.Count == 0
                                || (plan is not null && s.RequiredPlanCodes.Contains(plan.Code)))
                    .Select(s => new
                    {
                        s.Id,
                        s.Title,
                        s.Description,
                        s.StartsAt,
                        s.DurationMinutes,
                        s.JoinUrl
                    });

                return Results.Ok(visible);
            })
            .WithSummary("Sesiones grupales visibles para el plan del alumno.");

        group.MapGet("/discord/link", (
                IConfiguration configuration,
                HttpContext context) =>
            {
                var clientId = configuration["Academy:Discord:ClientId"];
                var redirect = configuration["Academy:Discord:RedirectUri"];

                if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(redirect))
                {
                    return Error.Conflict(
                        "discord.not_configured", "La integración con Discord no está configurada.").ToProblem();
                }

                // state carries the user id so the callback knows whose account to link.
                var state = context.User.RequireUserId().ToString();

                var url = $"https://discord.com/oauth2/authorize?client_id={Uri.EscapeDataString(clientId)}"
                          + $"&redirect_uri={Uri.EscapeDataString(redirect)}"
                          + "&response_type=code&scope=identify%20guilds.join"
                          + $"&state={Uri.EscapeDataString(state)}";

                return Results.Ok(new { url });
            })
            .WithSummary("URL de vinculación OAuth2 con Discord.");

        group.MapPost("/discord/callback", async (
                DiscordCallbackBody body,
                IDiscordGateway discord,
                IDiscordLinkRepository links,
                ISubscriptionRepository subscriptions,
                IPlanRepository plans,
                IConfiguration configuration,
                IClock clock,
                HttpContext context,
                CancellationToken ct) =>
            {
                var userId = context.User.RequireUserId();

                // The state must match the caller: otherwise a leaked code could attach
                // someone else's Discord account to this one.
                if (!Guid.TryParse(body.State, out var stateUserId) || stateUserId != userId)
                {
                    return Error.Forbidden("discord.state_mismatch", "La vinculación no es válida.").ToProblem();
                }

                var redirect = configuration["Academy:Discord:RedirectUri"] ?? string.Empty;
                var exchanged = await discord.ExchangeOAuthCodeAsync(body.Code, redirect, ct);

                if (exchanged.IsFailure)
                {
                    return exchanged.Error.ToProblem();
                }

                var subscription = await subscriptions.GetActiveForUserAsync(userId, ct);
                var plan = subscription is null ? null : await plans.GetByIdAsync(subscription.PlanId, ct);

                await links.UpsertAsync(
                    new DiscordLink(userId, exchanged.Value, clock.UtcNow, plan?.Code), ct);

                if (plan is not null)
                {
                    await discord.AssignRoleAsync(exchanged.Value, plan.Code, ct);
                }

                return Results.NoContent();
            })
            .WithSummary("Completa la vinculación con Discord y aplica el rol del plan.");

        group.MapDelete("/discord/link", async (
                IDiscordLinkRepository links,
                IDiscordGateway discord,
                HttpContext context,
                CancellationToken ct) =>
            {
                var userId = context.User.RequireUserId();
                var link = await links.GetByUserIdAsync(userId, ct);

                if (link is not null)
                {
                    if (link.LastAppliedRole is { } role)
                    {
                        await discord.RemoveRoleAsync(link.DiscordUserId, role, ct);
                    }

                    await links.DeleteAsync(userId, ct);
                }

                return Results.NoContent();
            })
            .WithSummary("Desvincula Discord y retira el rol.");
    }

    public sealed record DiscordCallbackBody(string Code, string State);
}
