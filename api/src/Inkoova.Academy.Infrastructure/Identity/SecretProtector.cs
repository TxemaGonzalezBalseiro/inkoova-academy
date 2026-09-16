using System.Security.Cryptography;
using System.Text;

namespace Inkoova.Academy.Infrastructure.Identity;

/// <summary>
/// Cifra y descifra las credenciales que esta plataforma guarda en su propia base.
///
/// Hoy solo hay una: la contraseña del buzón SMTP de cada marca. Todo lo demás —Stripe, la
/// clave del JWT, el secreto de los certificados— vive en el entorno y no pasa por aquí.
///
/// Por qué cifrar y no guardarla en claro: una copia de seguridad de la base sale de la máquina
/// —a un disco, a un correo, al portátil de alguien— mucho más a menudo que las variables de
/// entorno del servidor. Cifrada con una clave que NO está en la base, esa copia no entrega
/// ningún buzón.
///
/// AES-GCM y no AES-CBC: GCM autentica además de cifrar, así que un texto manipulado en la base
/// falla al descifrar en vez de producir basura que alguien intente usar como contraseña.
/// </summary>
public sealed class SecretProtector
{
    /// <summary>Marca de formato. Si algún día cambia el esquema, lo viejo se reconoce y se migra.</summary>
    private const string Prefix = "v1.";

    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly byte[] _key;

    /// <param name="secret">
    /// De configuración. Se deriva a 256 bits con SHA-256 para no exigir que quien la escribe
    /// acierte con la longitud exacta: una frase larga vale.
    /// </param>
    public SecretProtector(string secret)
    {
        if (string.IsNullOrWhiteSpace(secret) || secret.Trim().Length < 16)
        {
            throw new InvalidOperationException(
                "La clave de cifrado ('Academy:SecretsKey') debe tener al menos 16 caracteres.");
        }

        _key = SHA256.HashData(Encoding.UTF8.GetBytes(secret.Trim()));
    }

    /// <summary>Devuelve cadena vacía para entrada vacía: «sin contraseña» no se cifra.</summary>
    public string Protect(string? plaintext)
    {
        if (string.IsNullOrEmpty(plaintext))
        {
            return string.Empty;
        }

        // Nonce nuevo en cada cifrado. Repetirlo con la misma clave rompe GCM por completo, así
        // que se genera al azar y viaja junto al texto cifrado.
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var plain = Encoding.UTF8.GetBytes(plaintext);
        var cipher = new byte[plain.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plain, cipher, tag);

        return Prefix + Convert.ToBase64String([.. nonce, .. tag, .. cipher]);
    }

    /// <summary>
    /// Descifra. Devuelve <c>null</c> si el valor no es descifrable —clave cambiada, fila
    /// manipulada, formato desconocido— en vez de lanzar: quien llama tiene que poder decir
    /// «este buzón no se puede usar» sin tumbar el arranque de la aplicación.
    /// </summary>
    public string? Unprotect(string? stored)
    {
        if (string.IsNullOrEmpty(stored) || !stored.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return null;
        }

        try
        {
            var blob = Convert.FromBase64String(stored[Prefix.Length..]);

            if (blob.Length <= NonceSize + TagSize)
            {
                return null;
            }

            var nonce = blob.AsSpan(0, NonceSize);
            var tag = blob.AsSpan(NonceSize, TagSize);
            var cipher = blob.AsSpan(NonceSize + TagSize);
            var plain = new byte[cipher.Length];

            using var aes = new AesGcm(_key, TagSize);
            aes.Decrypt(nonce, cipher, tag, plain);

            return Encoding.UTF8.GetString(plain);
        }
        catch (CryptographicException)
        {
            // Clave distinta de la que cifró, o texto tocado. Las dos cosas significan lo mismo
            // para quien llama: esto no se puede usar.
            return null;
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
