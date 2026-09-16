using Inkoova.Academy.Domain.Common;

namespace Inkoova.Academy.Domain.Identities;

/// <summary>
/// El buzón desde el que escribe una marca.
///
/// Cada identidad manda desde su propia dirección. No es un capricho de presentación: si la
/// marca B escribiera desde el dominio de la marca A, el SPF del dominio remitente no cuadraría
/// con el que firma, y esos correos acaban en spam. El remitente tiene que ser de la casa.
///
/// <see cref="EncryptedPassword"/> llega y sale CIFRADA. El dominio no sabe descifrarla y no
/// tiene por qué: la clave vive en el entorno y solo la usa el remitente, en infraestructura.
/// Así una entidad de dominio —que acaba en logs, en pruebas y en volcados— nunca contiene una
/// credencial legible.
/// </summary>
public sealed record MailboxSettings(
    string Host,
    int Port,
    string Username,
    string EncryptedPassword,
    string Security,
    string FromAddress,
    string FromName)
{
    /// <summary>Marca recién creada: todavía no manda correo. No es un error, es un estado.</summary>
    public static readonly MailboxSettings Empty =
        new(string.Empty, 587, string.Empty, string.Empty, "auto", string.Empty, string.Empty);

    /// <summary>
    /// Si está lo justo para poder enviar. Sin host o sin remitente no se manda nada, y es mejor
    /// saberlo al guardar que descubrirlo cuando un alumno no recibe su confirmación.
    /// </summary>
    public bool CanSend => Host.Length > 0 && FromAddress.Length > 0;

    public Error? Validate()
    {
        // Vacío del todo es válido: una marca puede existir antes de tener buzón.
        if (Host.Length == 0 && FromAddress.Length == 0 && Username.Length == 0)
        {
            return null;
        }

        if (Host.Length == 0)
        {
            return Error.Validation("mailbox.host_required", "Falta el servidor SMTP.");
        }

        if (Port is < 1 or > 65535)
        {
            return Error.Validation("mailbox.port_invalid", "El puerto no es válido.");
        }

        if (FromAddress.Length == 0 || !FromAddress.Contains('@'))
        {
            return Error.Validation("mailbox.from_invalid", "La dirección remitente no es válida.");
        }

        if (Security is not ("auto" or "starttls" or "ssl" or "tls" or "none"))
        {
            return Error.Validation(
                "mailbox.security_invalid",
                "El cifrado debe ser auto, starttls, ssl o none.");
        }

        return null;
    }
}
