using Inkoova.Academy.Domain.Common;

namespace Inkoova.Academy.Domain.Identities;

/// <summary>
/// Una marca de la academia: cómo se llama, con qué logo se presenta y desde qué buzón escribe.
///
/// Lo que separa y lo que no:
///
/// - **Separa** la presentación (nombre, logo, lema, dominio), el buzón de salida y, con él,
///   las plantillas de correo y la cabecera del certificado.
/// - **No separa** alumnos, catálogo ni cobros. Una persona tiene una sola cuenta aunque curse
///   cosas de dos marcas. Meter la identidad en `app_user` habría convertido esto en tres
///   plataformas distintas, que no es lo que se pidió.
///
/// La contraseña del buzón NO vive aquí en claro: el dominio maneja un valor ya cifrado y no
/// sabe descifrarlo. Es deliberado —una entidad de dominio no tiene por qué poder leer una
/// credencial— y quien la necesita es el remitente de correo, en infraestructura.
/// </summary>
public sealed class AcademyIdentity
{
    public Guid Id { get; private set; }
    public string Slug { get; private set; }
    public string Name { get; private set; }
    public string Tagline { get; private set; }
    public string LogoUrl { get; private set; }
    public string PublicDomain { get; private set; }
    public string SupportEmail { get; private set; }

    /// <summary>
    /// Si esta marca es la que corresponde a un nombre de host.
    ///
    /// Es lo que hace que el certificado, el correo y la cabecera salgan con la marca por la que
    /// el alumno entró, sin tener que guardar en ningún sitio «este alumno es de esta marca»:
    /// la marca la dice el dominio desde el que se está usando la academia.
    /// </summary>
    public bool MatchesHost(string? host) =>
        NormalizeHost(PublicDomain) is { Length: > 0 } propio
        && propio == NormalizeHost(host);

    /// <summary>
    /// Deja un host comparable: sin puerto, en minúsculas, sin el punto final y sin «www.».
    ///
    /// El puerto sobra porque `localhost:5080` y `localhost:5173` son la misma marca, y en
    /// producción la misma web se sirve por 80 y por 443. Y el «www.» se quita de los dos lados
    /// para que configurar `www.marca.com` no deje fuera a quien entra por `marca.com`.
    /// </summary>
    public static string NormalizeHost(string? host)
    {
        var limpio = (host ?? string.Empty).Trim().ToLowerInvariant().TrimEnd('.');

        // El puerto va detrás del último ':', pero una IPv6 está llena de ':' que no lo son.
        // Llega entre corchetes —`[::1]:5080`—, así que el puerto solo cuenta si va DESPUÉS del
        // cierre. Cortando por el último ':' a secas, `[::1]` se quedaría en `[:`.
        var cierre = limpio.LastIndexOf(']');
        var puerto = limpio.LastIndexOf(':');

        if (puerto > 0 && puerto > cierre)
        {
            limpio = limpio[..puerto];
        }

        return limpio.StartsWith("www.", StringComparison.Ordinal) ? limpio[4..] : limpio;
    }

    /// <summary>
    /// Los datos que el artículo 10 de la LSSI obliga a publicar, más los enlaces del pie.
    ///
    /// Van juntos porque se rellenan juntos y porque se leen juntos: el aviso legal los usa
    /// todos a la vez. Separarlos en siete propiedades sueltas del agregado solo haría más
    /// larga cada firma.
    /// </summary>
    public LegalDetails Legal { get; private set; } = LegalDetails.Empty;

    public MailboxSettings Mailbox { get; private set; }

    /// <summary>
    /// La que se usa cuando nada dice qué marca aplica: un curso sin asignar, la cabecera de la
    /// web, un correo de sistema. Hay exactamente una, garantizado por un índice único.
    /// </summary>
    public bool IsDefault { get; private set; }

    public bool IsActive { get; private set; }

    private AcademyIdentity(
        Guid id,
        string slug,
        string name,
        string tagline,
        string logoUrl,
        string publicDomain,
        string supportEmail,
        MailboxSettings mailbox,
        bool isDefault,
        bool isActive)
    {
        Id = id;
        Slug = slug;
        Name = name;
        Tagline = tagline;
        LogoUrl = logoUrl;
        PublicDomain = publicDomain;
        SupportEmail = supportEmail;
        Mailbox = mailbox;
        IsDefault = isDefault;
        IsActive = isActive;
    }

    public static Result<AcademyIdentity, Error> Create(
        Guid id,
        string slug,
        string name,
        string tagline,
        string logoUrl,
        string publicDomain,
        string supportEmail)
    {
        var parsedSlug = ValueObjects.Slug.Create(slug);
        if (parsedSlug.IsFailure)
        {
            return Error.Validation("identity.slug_invalid", "El identificador de la marca no es válido.");
        }

        var validated = Validate(name, tagline, logoUrl, publicDomain, supportEmail);
        if (validated is not null)
        {
            return validated;
        }

        return new AcademyIdentity(
            id, parsedSlug.Value.Value, name.Trim(), Clean(tagline), Clean(logoUrl),
            CleanDomain(publicDomain), Clean(supportEmail),
            MailboxSettings.Empty, isDefault: false, isActive: true);
    }

    public static AcademyIdentity Rehydrate(
        Guid id,
        string slug,
        string name,
        string tagline,
        string logoUrl,
        string publicDomain,
        string supportEmail,
        MailboxSettings mailbox,
        LegalDetails legal,
        bool isDefault,
        bool isActive) =>
        new(id, slug, name, tagline, logoUrl, publicDomain, supportEmail, mailbox, isDefault, isActive)
        {
            Legal = legal
        };

    public Result<Unit, Error> Describe(
        string name,
        string tagline,
        string logoUrl,
        string publicDomain,
        string supportEmail)
    {
        var validated = Validate(name, tagline, logoUrl, publicDomain, supportEmail);
        if (validated is not null)
        {
            return validated;
        }

        Name = name.Trim();
        Tagline = Clean(tagline);
        LogoUrl = Clean(logoUrl);
        PublicDomain = CleanDomain(publicDomain);
        SupportEmail = Clean(supportEmail);

        return Unit.Value;
    }

    /// <summary>
    /// Los datos del titular. Se guardan aunque estén incompletos: rellenarlos es un trámite que
    /// se hace por partes —el NIF hoy, los datos registrales cuando llegue la escritura— y
    /// exigirlos todos de golpe obligaría a inventarse los que faltan para poder guardar.
    /// </summary>
    public Result<Unit, Error> ConfigureLegal(LegalDetails legal)
    {
        var validated = legal.Validate();
        if (validated is not null)
        {
            return validated;
        }

        Legal = legal;
        return Unit.Value;
    }

    public Result<Unit, Error> ConfigureMailbox(MailboxSettings mailbox)
    {
        var validated = mailbox.Validate();
        if (validated is not null)
        {
            return validated;
        }

        Mailbox = mailbox;
        return Unit.Value;
    }

    /// <summary>
    /// La marca por defecto no se puede desactivar: sin ella, un curso sin asignar y los correos
    /// de sistema se quedarían sin remitente y sin nombre que poner.
    /// </summary>
    public Result<Unit, Error> SetActive(bool active)
    {
        if (!active && IsDefault)
        {
            return Error.Conflict(
                "identity.default_cannot_deactivate",
                "La marca principal no se puede desactivar. Marca otra como principal antes.");
        }

        IsActive = active;
        return Unit.Value;
    }

    /// <summary>
    /// La pone como principal. Quitársela a la anterior es cosa del repositorio, en la misma
    /// transacción: el índice único de la base impide que haya dos, y hacerlo en dos pasos
    /// dejaría un instante con ninguna o con dos.
    /// </summary>
    public Result<Unit, Error> MakeDefault()
    {
        if (!IsActive)
        {
            return Error.Conflict(
                "identity.inactive_cannot_be_default",
                "Una marca desactivada no puede ser la principal.");
        }

        IsDefault = true;
        return Unit.Value;
    }

    internal void ClearDefault() => IsDefault = false;

    private static Error? Validate(
        string name, string tagline, string logoUrl, string publicDomain, string supportEmail)
    {
        var trimmed = (name ?? string.Empty).Trim();

        if (trimmed.Length is < 2 or > 120)
        {
            return Error.Validation("identity.name_invalid", "El nombre debe tener entre 2 y 120 caracteres.");
        }

        if (Clean(tagline).Length > 200)
        {
            return Error.Validation("identity.tagline_too_long", "El lema no puede pasar de 200 caracteres.");
        }

        // El logo acaba en un `src` que ve cualquier visitante: solo http(s) o una ruta propia.
        // `javascript:` y `data:` quedan fuera, y no por gusto: es la única entrada de esta
        // pantalla que se sirve a todo el mundo.
        var logo = Clean(logoUrl);
        if (logo.Length > 0
            && !logo.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            && !logo.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !logo.StartsWith('/'))
        {
            return Error.Validation(
                "identity.logo_invalid",
                "El logo debe ser una dirección http(s) o una ruta que empiece por /.");
        }

        // Se exige que no lleve esquema, barras ni espacios, pero NO que tenga punto: `localhost`
        // es un host de una sola etiqueta perfectamente válido, es el que se usa en desarrollo y
        // es el que la propia migración siembra. Rechazarlo dejaba la marca principal en un
        // estado que el sistema crea y luego no deja volver a guardar.
        var domain = CleanDomain(publicDomain);
        if (domain.Length > 0 && (domain.Contains('/') || domain.Contains(' ')))
        {
            return Error.Validation(
                "identity.domain_invalid",
                "El dominio va sin https:// ni barras. Por ejemplo: academy.inkoova.com.");
        }

        var email = Clean(supportEmail);
        if (email.Length > 0 && (!email.Contains('@') || email.Contains(' ')))
        {
            return Error.Validation("identity.support_email_invalid", "El correo de contacto no es válido.");
        }

        return null;
    }

    private static string Clean(string? value) => (value ?? string.Empty).Trim();

    /// <summary>
    /// Quita lo que la gente pega de más al copiar un dominio de la barra del navegador. Es más
    /// amable rechazar menos y limpiar lo evidente que devolver un error por una barra final.
    /// </summary>
    private static string CleanDomain(string? value) =>
        Clean(value)
            .Replace("https://", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("http://", string.Empty, StringComparison.OrdinalIgnoreCase)
            .TrimEnd('/')
            .ToLowerInvariant();
}
