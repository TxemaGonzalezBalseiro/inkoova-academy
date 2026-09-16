using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Inkoova.Academy.Application.Abstractions;
using Inkoova.Academy.Domain.Common;

namespace Inkoova.Academy.Infrastructure.Messaging;

public sealed record EmailTemplateOptions
{
    /// <summary>
    /// Directory holding <c>{name}.html</c>. The subject travels in the first line of the
    /// same file as <c>&lt;!-- subject: ... --&gt;</c>: one file per email means the copy and
    /// its subject cannot drift apart. The plain-text part is derived from the markup.
    /// </summary>
    public required string TemplatesPath { get; init; }
}

/// <summary>
/// Deliberately not a template engine: transactional emails only ever substitute a handful
/// of named values, and pulling in Razor or Handlebars for <c>{{nombre}}</c> would add a
/// dependency with a much larger attack surface than the problem deserves.
/// </summary>
public sealed partial class FileEmailTemplateRenderer(
    EmailTemplateOptions options,
    IEmailTemplateStore store,
    IAcademyIdentityRepository identities) : IEmailTemplateRenderer
{
    private readonly ConcurrentDictionary<string, (string Subject, string Html, string Text)> _cache = new();

    public async Task<Result<(string Subject, string Html, string Text), Error>> RenderAsync(
        string templateName,
        IReadOnlyDictionary<string, string> model,
        CancellationToken ct,
        Guid? identityId = null)
    {
        var identity = identityId ?? (await identities.GetDefaultAsync(ct)).Id;

        // Primero la de la marca; si no la ha escrito, la del fichero. Las plantillas propias
        // NO se cachean en memoria: se editan desde el panel y hay que ver el cambio al
        // siguiente correo, no cinco minutos después. Las de fichero sí, porque solo cambian
        // con un despliegue.
        var own = await store.GetAsync(identity, templateName, ct);

        (string Subject, string Html, string Text) template = own is not null
            ? (own.Subject, own.Html, StripTags(own.Html))
            : TryLoad(templateName, out var fromFile)
                ? fromFile
                : default;

        if (string.IsNullOrEmpty(template.Html))
        {
            return Error.NotFound("email.template_not_found", $"No existe la plantilla de correo '{templateName}'.");
        }

        var full = await WithBrandingAsync(model, ct, identity);

        return (
            Substitute(template.Subject, full),
            Substitute(template.Html, full),
            Substitute(template.Text, full));
    }

    public async Task<(string Subject, string Html, string Text)> RenderContentAsync(
        string subject,
        string html,
        IReadOnlyDictionary<string, string> model,
        CancellationToken ct,
        Guid? identityId = null)
    {
        // Se ignora `identityId` para elegir plantilla —aquí el contenido lo trae quien llama—
        // pero NO para la marca: la vista previa tiene que enseñar el pie y el remitente de la
        // marca que se está editando, no los de la principal.
        var full = await WithBrandingAsync(model, ct, identityId);

        return (Substitute(subject, full), Substitute(html, full), Substitute(StripTags(html), full));
    }

    public Result<(string Subject, string Html), Error> GetOriginal(string templateName) =>
        TryLoad(templateName, out var template)
            ? (template.Subject, template.Html)
            : Error.NotFound("email.template_not_found", $"No existe la plantilla '{templateName}'.");

    /// <summary>
    /// Añade la identidad de la academia al modelo de cada correo, como
    /// <c>{{academyName}}</c>, <c>{{academyUrl}}</c> y <c>{{supportEmail}}</c>.
    ///
    /// Lo que traiga quien llama MANDA sobre esto: si un correo concreto quiere poner otro
    /// remitente o otra dirección de contacto, puede. Estos son valores por defecto, no una
    /// imposición.
    /// </summary>
    private async Task<IReadOnlyDictionary<string, string>> WithBrandingAsync(
        IReadOnlyDictionary<string, string> model,
        CancellationToken ct,
        Guid? identityId = null)
    {
        // La marca que corresponde a ESTE correo, no siempre la principal. Un alumno que
        // estudia con la marca B tiene que recibir el pie y el contacto de la marca B; si no,
        // la firma del correo y el sitio donde estudia dicen cosas distintas.
        var identity = identityId is { } id
            ? await identities.GetByIdAsync(id, ct) ?? await identities.GetDefaultAsync(ct)
            : await identities.GetDefaultAsync(ct);

        var merged = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["academyName"] = Or(identity.Name, "Inkoova Academy"),
            ["academyTagline"] = identity.Tagline,
            // Sin dominio configurado no se inventa uno: se deja vacío y la plantilla enseña
            // solo el nombre. Un enlace a un dominio que no responde es peor que ningún enlace.
            ["academyDomain"] = identity.PublicDomain,
            ["academyUrl"] = string.IsNullOrWhiteSpace(identity.PublicDomain)
                ? string.Empty
                : $"https://{identity.PublicDomain}",

            // El enlace del pie, ya montado. Va aquí y no en la plantilla porque incluye el
            // separador: sin dominio configurado no debe quedar un « · » suelto colgando del
            // nombre de la academia.
            ["academyLink"] = string.IsNullOrWhiteSpace(identity.PublicDomain)
                ? string.Empty
                : $" · <a href=\"https://{identity.PublicDomain}\" style=\"color:#64748B\">{identity.PublicDomain}</a>",

            ["supportEmail"] = identity.SupportEmail,
        };

        foreach (var pair in model)
        {
            merged[pair.Key] = pair.Value;
        }

        return merged;
    }

    private static string Or(string value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value;

    private bool TryLoad(string name, out (string Subject, string Html, string Text) template)
    {
        template = _cache.GetOrAdd(name, key =>
        {
            var htmlPath = Path.Combine(options.TemplatesPath, key + ".html");

            if (!File.Exists(htmlPath))
            {
                return (string.Empty, string.Empty, string.Empty);
            }

            var raw = File.ReadAllText(htmlPath);
            var subjectMatch = SubjectPattern().Match(raw);

            // Sin línea de asunto en la plantilla se usa el nombre de la academia, que se
            // sustituye después junto al resto del modelo.
            var subject = subjectMatch.Success ? subjectMatch.Groups[1].Value.Trim() : "{{academyName}}";
            var html = subjectMatch.Success ? raw[subjectMatch.Length..].TrimStart() : raw;

            return (subject, html, StripTags(html));
        });

        return !string.IsNullOrEmpty(template.Html);
    }

    /// <summary>
    /// Replaces <c>{{key}}</c> with the model value. Unknown placeholders are left in place
    /// rather than blanked, so a missing value is visible in a test instead of silent.
    /// </summary>
    private static string Substitute(string content, IReadOnlyDictionary<string, string> model) =>
        PlaceholderPattern().Replace(content, match =>
        {
            var key = match.Groups[1].Value.Trim();
            return model.TryGetValue(key, out var value) ? value : match.Value;
        });

    private static string StripTags(string html) =>
        System.Net.WebUtility.HtmlDecode(TagPattern().Replace(html, string.Empty)).Trim();

    [GeneratedRegex(@"\{\{\s*([a-zA-Z0-9_]+)\s*\}\}")]
    private static partial Regex PlaceholderPattern();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex TagPattern();

    [GeneratedRegex(@"\A<!--\s*subject:\s*(.+?)\s*-->\s*", RegexOptions.Singleline)]
    private static partial Regex SubjectPattern();
}
