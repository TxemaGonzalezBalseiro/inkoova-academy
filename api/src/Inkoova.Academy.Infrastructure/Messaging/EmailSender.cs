using Inkoova.Academy.Application.Abstractions;
using Inkoova.Academy.Infrastructure.Identity;
using Inkoova.Academy.Domain.Common;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace Inkoova.Academy.Infrastructure.Messaging;

public sealed record EmailOptions
{
    public required string Host { get; init; }

    public int Port { get; init; } = 587;

    public required string Username { get; init; }

    public required string Password { get; init; }

    public required string FromAddress { get; init; }

    /// <summary>
    /// Nombre del remitente, si se quiere uno distinto del de la academia. Vacío —lo normal—
    /// significa «usa el nombre configurado en Identidad», que es lo que evita que la bandeja
    /// de entrada diga una cosa y la web otra.
    /// </summary>
    public string? FromNameOverride { get; init; }

    /// <summary>
    /// When set, every message is redirected here instead of the real recipient. Used in the
    /// closed beta so a test payment cannot email a real customer.
    /// </summary>
    public string? RedirectAllTo { get; init; }

    /// <summary>
    /// Cómo se cifra la conexión: <c>auto</c>, <c>starttls</c>, <c>ssl</c> o <c>none</c>.
    ///
    /// Estaba fijado a StartTLS, que es lo que usan Office 365 y Gmail en el 587. En el 465
    /// —el otro puerto que ofrece Gmail y muchos proveedores— el TLS se negocia al conectar, y
    /// pedir StartTLS ahí no conecta. Con <c>auto</c> se decide por el puerto.
    /// </summary>
    public string Security { get; init; } = "auto";

    public SecureSocketOptions SocketOptions => SocketOptionsFor(Security, Port);

    /// <summary>
    /// La misma regla, disponible para el buzón de cada marca. Está aquí y no duplicada allí
    /// porque duplicarla acabaría con dos reglas: una que sabe lo del 465 y otra que no.
    /// </summary>
    public static SecureSocketOptions SocketOptionsFor(string security, int port) =>
        (security ?? "auto").ToLowerInvariant() switch
        {
            "starttls" => SecureSocketOptions.StartTls,
            "ssl" or "tls" => SecureSocketOptions.SslOnConnect,
            "none" => SecureSocketOptions.None,
            // 465 es TLS implícito; 587 y 25 negocian con STARTTLS.
            _ => port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls
        };
}

public sealed class SmtpEmailSender(
    EmailOptions options,
    IAcademyIdentityRepository identities,
    SecretProtector secrets,
    ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task<Result<Unit, Error>> SendAsync(EmailMessage message, CancellationToken ct)
    {
        var recipient = options.RedirectAllTo ?? message.To;

        var identity = message.IdentityId is { } id
            ? await identities.GetByIdAsync(id, ct) ?? await identities.GetDefaultAsync(ct)
            : await identities.GetDefaultAsync(ct);

        var mailbox = Resolve(identity);

        if (string.IsNullOrWhiteSpace(mailbox.Host) || string.IsNullOrWhiteSpace(mailbox.From))
        {
            return Error.Unexpected(
                "email.not_configured",
                $"La marca «{identity.Name}» no tiene buzón configurado, así que no se puede enviar.");
        }

        try
        {
            var mime = new MimeMessage();
            mime.From.Add(new MailboxAddress(mailbox.FromName, mailbox.From));
            mime.To.Add(MailboxAddress.Parse(recipient));
            mime.Subject = message.Subject;

            var body = new BodyBuilder { HtmlBody = message.HtmlBody, TextBody = message.TextBody };

            foreach (var (fileName, content, contentType) in message.Attachments ?? [])
            {
                body.Attachments.Add(fileName, content, ContentType.Parse(contentType));
            }

            mime.Body = body.ToMessageBody();

            using var client = new SmtpClient();
            await client.ConnectAsync(mailbox.Host, mailbox.Port, mailbox.Socket, ct);

            // Un relay interno puede no pedir credenciales; autenticar sin ellas lo rechazaría.
            if (!string.IsNullOrWhiteSpace(mailbox.Username))
            {
                await client.AuthenticateAsync(mailbox.Username, mailbox.Password, ct);
            }

            await client.SendAsync(mime, ct);
            await client.DisconnectAsync(quit: true, ct);

            return Unit.Value;
        }
        catch (AuthenticationException ex)
        {
            // El fallo más común, y el que peor se diagnostica: Office 365 y Google tienen la
            // autenticación básica desactivada por defecto y hace falta una contraseña de
            // aplicación. Decirlo aquí ahorra buscarlo en los logs del proveedor.
            logger.LogError(ex, "SMTP authentication for {Username} failed.", mailbox.Username);

            return Error.Unexpected(
                "email.auth_failed",
                "El servidor de correo ha rechazado el usuario o la contraseña. Con Office 365 o " +
                "Gmail suele hacer falta una contraseña de aplicación, no la de la cuenta.");
        }
        catch (Exception ex) when (ex is SmtpCommandException or SmtpProtocolException or IOException
                                       or System.Net.Sockets.SocketException)
        {
            logger.LogError(ex, "SMTP send to {Recipient} failed.", recipient);
            return Error.Unexpected("email.send_failed", $"No se ha podido enviar el correo: {ex.Message}");
        }
    }

    /// <summary>
    /// El buzón que se va a usar: el de la marca si lo tiene, y si no el del entorno.
    ///
    /// El respaldo al entorno no es pereza: hasta hoy TODO el correo salía de ahí, y quitarlo de
    /// golpe dejaría la plataforma sin poder mandar ni una confirmación hasta que alguien
    /// rellenara el formulario. Con el respaldo, configurar el buzón por marca es una mejora que
    /// se adopta cuando se quiere, no un requisito para que siga funcionando lo que ya iba.
    /// </summary>
    private ResolvedMailbox Resolve(Domain.Identities.AcademyIdentity identity)
    {
        var box = identity.Mailbox;

        if (!box.CanSend)
        {
            return new ResolvedMailbox(
                options.Host, options.Port, options.Username, options.Password,
                options.SocketOptions, options.FromAddress,
                options.FromNameOverride is { Length: > 0 } name ? name : identity.Name);
        }

        // Si no se puede descifrar —clave cambiada, fila manipulada— se manda vacía en vez de
        // caer: con un relay sin autenticación eso funciona, y con uno que la pida el error dirá
        // «usuario o contraseña rechazados», que es exactamente lo que ha pasado.
        var password = secrets.Unprotect(box.EncryptedPassword) ?? string.Empty;

        return new ResolvedMailbox(
            box.Host, box.Port, box.Username, password,
            EmailOptions.SocketOptionsFor(box.Security, box.Port), box.FromAddress,
            box.FromName is { Length: > 0 } from ? from : identity.Name);
    }

    private sealed record ResolvedMailbox(
        string Host,
        int Port,
        string Username,
        string Password,
        SecureSocketOptions Socket,
        string From,
        string FromName);
}

/// <summary>
/// Development sink: writes the message to the log instead of sending it. Keeps local runs
/// from needing SMTP credentials.
/// </summary>
public sealed class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task<Result<Unit, Error>> SendAsync(EmailMessage message, CancellationToken ct)
    {
        logger.LogInformation(
            "[email] to={To} subject={Subject}\n{Body}", message.To, message.Subject, message.TextBody);

        return Task.FromResult(Result.Ok(Unit.Value));
    }
}
