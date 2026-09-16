using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;

namespace Inkoova.Academy.Api.Auth;

/// <summary>
/// Credenciales de los proveedores de identidad. Van por configuración y NO por base de datos:
/// son secretos, y la pantalla de administración no debe poder leerlos ni enseñarlos. El panel
/// solo dice si están puestos, igual que con Stripe.
/// </summary>
public sealed class ExternalAuthOptions
{
    public GoogleOptions Google { get; init; } = new();

    public AppleOptions Apple { get; init; } = new();

    public sealed class GoogleOptions
    {
        public string? ClientId { get; init; }

        public string? ClientSecret { get; init; }

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);
    }

    /// <summary>
    /// Apple no da un «client secret» fijo: hay que firmar uno con la clave privada .p8 que
    /// descarga el desarrollador, y caduca. Por eso hacen falta cuatro datos y no dos.
    /// </summary>
    public sealed class AppleOptions
    {
        /// <summary>El <em>Services ID</em> (no el App ID): es el que actúa de client_id en la web.</summary>
        public string? ClientId { get; init; }

        public string? TeamId { get; init; }

        /// <summary>Identificador de la clave .p8, el que Apple enseña al crearla.</summary>
        public string? KeyId { get; init; }

        /// <summary>
        /// Contenido de la clave .p8. Se admite con o sin las líneas BEGIN/END y con saltos de
        /// línea reales o escapados: una clave pegada en una variable de entorno llega de las
        /// tres formas según quién la copie.
        /// </summary>
        public string? PrivateKey { get; init; }

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(ClientId)
            && !string.IsNullOrWhiteSpace(TeamId)
            && !string.IsNullOrWhiteSpace(KeyId)
            && !string.IsNullOrWhiteSpace(PrivateKey);
    }
}

/// <summary>
/// Genera el «client secret» de Apple: un JWT ES256 firmado con la clave .p8.
///
/// Se firma en cada intercambio de código en vez de guardarse. Apple admite hasta seis meses de
/// validez, pero un secreto guardado caduca un día cualquiera y el login se rompe sin que nadie
/// haya tocado nada; firmarlo al vuelo cuesta microsegundos y no caduca nunca en la práctica.
/// </summary>
public static class AppleClientSecret
{
    /// <summary>Cinco minutos: solo tiene que sobrevivir a la llamada al endpoint de token.</summary>
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    public static string Create(ExternalAuthOptions.AppleOptions options, DateTimeOffset now)
    {
        using var key = ECDsa.Create();
        key.ImportPkcs8PrivateKey(DecodePrivateKey(options.PrivateKey!), out _);

        var credentials = new SigningCredentials(
            new ECDsaSecurityKey(key) { KeyId = options.KeyId },
            SecurityAlgorithms.EcdsaSha256);

        var token = new JwtSecurityToken(
            issuer: options.TeamId,
            // La audiencia SIEMPRE es Apple, no nuestra API: el que valida este JWT es Apple.
            audience: "https://appleid.apple.com",
            claims: [new System.Security.Claims.Claim("sub", options.ClientId!)],
            notBefore: now.UtcDateTime,
            expires: now.Add(Lifetime).UtcDateTime,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Deja la clave en bytes DER a partir de lo que haya en la configuración. Se aceptan las
    /// tres formas en que llega una .p8 pegada a mano: con cabeceras PEM, sin ellas, y con los
    /// saltos de línea escapados como \n por un fichero .env.
    /// </summary>
    private static byte[] DecodePrivateKey(string raw)
    {
        var cleaned = raw
            .Replace("\\n", "\n")
            .Replace("-----BEGIN PRIVATE KEY-----", string.Empty)
            .Replace("-----END PRIVATE KEY-----", string.Empty)
            .Replace("\r", string.Empty)
            .Replace("\n", string.Empty)
            .Trim();

        return Convert.FromBase64String(cleaned);
    }
}
