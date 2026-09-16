namespace Inkoova.Academy.Api.Auth;

/// <summary>
/// Construye las URLs de vuelta a la SPA después de un rodeo por un proveedor externo.
///
/// Existe por una razón concreta: el parámetro <c>returnUrl</c> viene del navegador y por tanto
/// de cualquiera. Si se redirigiera a lo que traiga, este endpoint sería un **redirector
/// abierto**: un enlace que empieza en el dominio de la academia —con su TLS y su aspecto de
/// confianza— y termina en el sitio del atacante. Es de los agujeros más fáciles de dejar
/// abiertos y de los más usados en phishing.
///
/// La regla es simple y no admite excepciones: solo se aceptan rutas relativas de este mismo
/// sitio. Cualquier otra cosa cae en la portada.
/// </summary>
public sealed class AcademyUrls(string publicBaseUrl)
{
    private readonly string _base = publicBaseUrl.TrimEnd('/');

    /// <summary>
    /// Deja el <c>returnUrl</c> en una ruta relativa segura, o <c>null</c> si no lo es.
    ///
    /// Se rechaza todo lo que no empiece por una sola barra. Eso descarta de golpe
    /// <c>https://otro.sitio</c>, <c>//otro.sitio</c> —que el navegador entiende como absoluta
    /// con el mismo esquema— y <c>javascript:</c>.
    /// </summary>
    public string? SafeReturnPath(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return null;
        }

        var value = returnUrl.Trim();

        if (!value.StartsWith('/') || value.StartsWith("//", StringComparison.Ordinal))
        {
            return null;
        }

        // Una barra invertida detrás de la primera barra también vale como esquema en algunos
        // navegadores (/\otro.sitio). Se descarta igual.
        return value.StartsWith("/\\", StringComparison.Ordinal) ? null : value;
    }

    /// <summary>A dónde va el alumno tras entrar: a donde quería ir, o a su cuenta.</summary>
    public string AfterLogin(string? returnPath) => _base + (SafeReturnPath(returnPath) ?? "/cuenta");

    /// <summary>
    /// Vuelta a la pantalla de acceso con el motivo. Se manda el <em>código</em> del error y no
    /// el mensaje: el texto es cosa de la SPA, que es quien sabe en qué idioma está la pantalla,
    /// y así el motivo no acaba escrito en la barra de direcciones en prosa.
    /// </summary>
    public string LoginWithError(string code) => $"{_base}/login?error={Uri.EscapeDataString(code)}";
}
