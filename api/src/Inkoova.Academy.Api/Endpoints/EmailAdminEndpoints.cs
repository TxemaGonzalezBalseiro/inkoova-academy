using Inkoova.Academy.Api.Common;
using Inkoova.Academy.Application.Abstractions;
using Inkoova.Academy.Domain.Common;
using Inkoova.Academy.Infrastructure.Messaging;

namespace Inkoova.Academy.Api.Endpoints;

/// <summary>
/// Configuración del correo saliente.
///
/// Los datos van en el entorno, no en la base: la contraseña del buzón es un secreto y no debe
/// acabar en una copia de seguridad ni en una captura de pantalla del panel. Aquí solo se ve
/// qué hay puesto y se puede mandar un correo de prueba, que es lo único que de verdad
/// demuestra que la configuración sirve.
/// </summary>
public static class EmailAdminEndpoints
{
    public static void MapEmailAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/email")
            .WithTags("Correo")
            .RequireAuthorization("admin");

        group.MapGet("/", (IConfiguration configuration, IEmailSender sender) =>
            {
                var host = configuration["Academy:Email:Host"];
                var port = configuration.GetValue("Academy:Email:Port", 587);
                var user = configuration["Academy:Email:Username"];

                return Results.Ok(new
                {
                    // Sin host, la API usa el sumidero que escribe en el log: los correos no
                    // salen, y conviene que se vea aquí y no al preguntarse por qué nadie
                    // recibe la confirmación de su cuenta.
                    configured = !string.IsNullOrWhiteSpace(host),
                    delivering = sender is SmtpEmailSender,
                    host,
                    port,
                    security = configuration["Academy:Email:Security"] ?? "auto",
                    effectiveSecurity = port == 465 ? "ssl" : "starttls",
                    username = Mask(user),
                    hasPassword = !string.IsNullOrWhiteSpace(configuration["Academy:Email:Password"]),
                    from = configuration["Academy:Email:From"],
                    fromName = configuration["Academy:Email:FromName"],
                    redirectAllTo = configuration["Academy:Email:RedirectAllTo"],
                    provider = ProviderOf(host)
                });
            })
            .WithSummary("Qué hay configurado para enviar correo. Nunca devuelve la contraseña.");

        group.MapPost("/test", async (
                TestEmailBody body,
                IEmailSender sender,
                HttpContext context,
                CancellationToken ct) =>
            {
                var to = string.IsNullOrWhiteSpace(body.To) ? context.User.FindFirst("email")?.Value : body.To;

                if (string.IsNullOrWhiteSpace(to))
                {
                    return Error.Validation("email.no_recipient", "Indica a qué dirección enviarlo.").ToProblem();
                }

                // Sin servidor, el sumidero de desarrollo escribe en el log y devuelve éxito.
                // Contestar "enviado" aquí sería lo peor que puede hacer esta pantalla: daría
                // por buena una configuración que no manda nada.
                if (sender is not SmtpEmailSender)
                {
                    return Error.Conflict(
                            "email.not_configured",
                            "No hay servidor de correo configurado: la API escribe los mensajes en el log " +
                            "en vez de enviarlos. Configura Academy__Email__Host y reinicia.")
                        .ToProblem();
                }

                var result = await sender.SendAsync(
                    new EmailMessage(
                        to,
                        "Prueba de configuración · Inkoova Academy",
                        "<p>Si lees esto, el correo saliente de la academia funciona.</p>",
                        "Si lees esto, el correo saliente de la academia funciona."),
                    ct);

                return result.ToHttp(_ => Results.Ok(new { sent = true, to }));
            })
            .WithSummary("Envía un correo de prueba con la configuración actual.");
    }

    public sealed record TestEmailBody(string? To);

    /// <summary>Usuario reconocible sin enseñarlo entero: <c>ho…@inkoova.com</c>.</summary>
    private static string? Mask(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var at = value.IndexOf('@', StringComparison.Ordinal);

        return at <= 2 ? value : $"{value[..2]}…{value[at..]}";
    }

    /// <summary>
    /// Reconoce el proveedor por el host para poder dar instrucciones concretas en el panel.
    /// Los tres de siempre tienen cada uno su trampa, y son distintas.
    /// </summary>
    private static string ProviderOf(string? host) => host?.ToLowerInvariant() switch
    {
        null or "" => "sin configurar",
        var h when h.Contains("office365") || h.Contains("outlook") => "office365",
        var h when h.Contains("gmail") || h.Contains("google") => "gmail",
        _ => "otro"
    };
}
