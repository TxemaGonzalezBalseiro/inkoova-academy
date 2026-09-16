using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Inkoova.Academy.Application.Abstractions;
using Inkoova.Academy.Domain.Common;
using Microsoft.Extensions.Logging;

namespace Inkoova.Academy.Infrastructure.Community;

public sealed record DiscordOptions
{
    public required string BotToken { get; init; }

    public required string GuildId { get; init; }

    public required string ClientId { get; init; }

    public required string ClientSecret { get; init; }

    /// <summary>Plan code to Discord role id. Configured, not hardcoded: role ids are per guild.</summary>
    public IReadOnlyDictionary<string, string> RoleIdsByName { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Discord role sync (T-12). Every failure is logged and returned as an error rather than
/// thrown: losing Discord must never roll back a payment or a plan change.
/// </summary>
public sealed class DiscordGateway(
    HttpClient http,
    DiscordOptions options,
    ILogger<DiscordGateway> logger) : IDiscordGateway
{
    private const string ApiBase = "https://discord.com/api/v10";

    public async Task<Result<Unit, Error>> AssignRoleAsync(
        string discordUserId,
        string roleName,
        CancellationToken ct)
    {
        if (!options.RoleIdsByName.TryGetValue(roleName, out var roleId))
        {
            return Error.Validation("discord.unknown_role", $"El rol '{roleName}' no está configurado.");
        }

        return await SendAsync(
            HttpMethod.Put,
            $"{ApiBase}/guilds/{options.GuildId}/members/{discordUserId}/roles/{roleId}",
            $"asignar rol {roleName}",
            ct);
    }

    public async Task<Result<Unit, Error>> RemoveRoleAsync(
        string discordUserId,
        string roleName,
        CancellationToken ct)
    {
        if (!options.RoleIdsByName.TryGetValue(roleName, out var roleId))
        {
            return Error.Validation("discord.unknown_role", $"El rol '{roleName}' no está configurado.");
        }

        return await SendAsync(
            HttpMethod.Delete,
            $"{ApiBase}/guilds/{options.GuildId}/members/{discordUserId}/roles/{roleId}",
            $"retirar rol {roleName}",
            ct);
    }

    /// <summary>Exchanges the OAuth2 code for the Discord user id we store in <c>discord_link</c>.</summary>
    public async Task<Result<string, Error>> ExchangeOAuthCodeAsync(
        string code,
        string redirectUri,
        CancellationToken ct)
    {
        try
        {
            using var tokenRequest = new HttpRequestMessage(HttpMethod.Post, $"{ApiBase}/oauth2/token")
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["client_id"] = options.ClientId,
                    ["client_secret"] = options.ClientSecret,
                    ["grant_type"] = "authorization_code",
                    ["code"] = code,
                    ["redirect_uri"] = redirectUri
                })
            };

            using var tokenResponse = await http.SendAsync(tokenRequest, ct);
            if (!tokenResponse.IsSuccessStatusCode)
            {
                return Error.Unexpected("discord.oauth_failed", "No se ha podido vincular la cuenta de Discord.");
            }

            var payload = await tokenResponse.Content.ReadFromJsonAsync<JsonElement>(ct);
            var accessToken = payload.GetProperty("access_token").GetString();

            using var meRequest = new HttpRequestMessage(HttpMethod.Get, $"{ApiBase}/users/@me");
            meRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using var meResponse = await http.SendAsync(meRequest, ct);
            if (!meResponse.IsSuccessStatusCode)
            {
                return Error.Unexpected("discord.me_failed", "No se ha podido leer tu perfil de Discord.");
            }

            var me = await meResponse.Content.ReadFromJsonAsync<JsonElement>(ct);
            var id = me.GetProperty("id").GetString();

            return string.IsNullOrWhiteSpace(id)
                ? Error.Unexpected("discord.me_failed", "Discord no ha devuelto un identificador de usuario.")
                : id;
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Discord OAuth exchange failed.");
            return Error.Unexpected("discord.oauth_failed", "No se ha podido vincular la cuenta de Discord.");
        }
    }

    private async Task<Result<Unit, Error>> SendAsync(
        HttpMethod method,
        string url,
        string description,
        CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(method, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bot", options.BotToken);

            using var response = await http.SendAsync(request, ct);

            if (response.IsSuccessStatusCode)
            {
                return Unit.Value;
            }

            // 404 on a role removal means the member already left the guild: nothing to do.
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound && method == HttpMethod.Delete)
            {
                return Unit.Value;
            }

            logger.LogWarning("Discord {Description} failed with {Status}.", description, response.StatusCode);
            return Error.Unexpected("discord.request_failed", $"Discord ha rechazado la operación: {description}.");
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Discord {Description} failed.", description);
            return Error.Unexpected("discord.request_failed", $"No se ha podido contactar con Discord: {description}.");
        }
    }
}

/// <summary>Used when Discord is not configured. Keeps the jobs runnable in dev and in CI.</summary>
public sealed class NullDiscordGateway(ILogger<NullDiscordGateway> logger) : IDiscordGateway
{
    public Task<Result<Unit, Error>> AssignRoleAsync(string discordUserId, string roleName, CancellationToken ct)
    {
        logger.LogInformation("[discord:noop] assign {Role} to {User}", roleName, discordUserId);
        return Task.FromResult(Result.Ok(Unit.Value));
    }

    public Task<Result<Unit, Error>> RemoveRoleAsync(string discordUserId, string roleName, CancellationToken ct)
    {
        logger.LogInformation("[discord:noop] remove {Role} from {User}", roleName, discordUserId);
        return Task.FromResult(Result.Ok(Unit.Value));
    }

    public Task<Result<string, Error>> ExchangeOAuthCodeAsync(string code, string redirectUri, CancellationToken ct) =>
        Task.FromResult(Result.Fail<string>(
            Error.Conflict("discord.not_configured", "La integración con Discord no está configurada.")));
}
