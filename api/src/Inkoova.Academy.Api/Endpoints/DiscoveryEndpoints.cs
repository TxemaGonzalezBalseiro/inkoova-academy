using System.Text;
using System.Xml;
using Inkoova.Academy.Application.Abstractions;
using Inkoova.Academy.Application.Billing;
using Inkoova.Academy.Application.Catalog;
using Inkoova.Academy.Domain.Catalog;

namespace Inkoova.Academy.Api.Endpoints;

/// <summary>
/// Lo que leen los rastreadores: <c>sitemap.xml</c> y <c>llms.txt</c>.
///
/// Van en la API y no como fichero estático porque su contenido depende de qué está publicado
/// en cada momento: publicar un curso desde el panel tiene que salir en el sitemap sin volver a
/// desplegar el frontal. Un sitemap generado en el build queda obsoleto la primera vez que se
/// publica algo, y un sitemap que anuncia URLs que dan 404 es peor que no tenerlo.
///
/// Se sirven en la raíz —no bajo <c>/api</c>— porque <c>robots.txt</c> las excluye y porque
/// <c>llms.txt</c> solo se busca en la raíz del dominio por convención (llmstxt.org).
/// </summary>
public static class DiscoveryEndpoints
{
    /// <summary>
    /// Rutas públicas sin parámetros. Las de cuenta, panel, afiliado y aprendizaje se quedan
    /// fuera a propósito: exigen sesión, así que un rastreador solo obtendría la cáscara.
    /// </summary>
    private static readonly string[] StaticPaths =
    [
        "/",
        "/cursos",
        "/precios",
        "/packs",
        "/roadmap",
        "/empresas",
        "/sobre-mi",
        "/faq",
        "/soporte",
        "/check-certificate",
        "/legal/aviso-legal",
        "/legal/privacidad",
        "/legal/cookies",
        "/legal/condiciones"
    ];

    public static void MapDiscoveryEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/sitemap.xml", async (
                HttpContext context,
                GetCatalogHandler catalog,
                IPackRepository packs,
                CancellationToken ct) =>
            {
                var origin = OriginOf(context);
                var courses = await catalog.HandleAsync(ct);
                var published = await packs.GetPublishedAsync(ct);

                var urls = new List<string>(StaticPaths.Length + courses.Count + published.Count);
                urls.AddRange(StaticPaths);

                // «Próximamente» entra igual: su ficha existe, es pública y tiene lista de aviso.
                urls.AddRange(courses.Select(c => $"/curso/{c.Slug}"));
                urls.AddRange(published.Select(p => $"/pack/{p.Slug.Value}"));

                return Results.Text(BuildSitemap(origin, urls), "application/xml", Encoding.UTF8);
            })
            .AllowAnonymous()
            .ExcludeFromDescription();

        app.MapGet("/llms.txt", async (
                HttpContext context,
                GetCatalogHandler catalog,
                GetPlansHandler plans,
                IPackRepository packs,
                IAcademyIdentityRepository identities,
                CancellationToken ct) =>
            {
                var origin = OriginOf(context);
                // GetForHostAsync ya cae a la marca principal cuando el host no es de nadie.
                var brand = await identities.GetForHostAsync(context.Request.Host.Host, ct);

                var text = BuildLlmsTxt(
                    origin,
                    brand.Name,
                    await catalog.HandleAsync(ct),
                    await packs.GetPublishedAsync(ct),
                    await plans.HandleAsync(ct));

                return Results.Text(text, "text/plain", Encoding.UTF8);
            })
            .AllowAnonymous()
            .ExcludeFromDescription();
    }

    /// <summary>
    /// El origen sale de la petición, no de la configuración: cada marca tiene su dominio y el
    /// sitemap de una no puede listar URLs de otra —Search Console lo rechaza por dominio
    /// cruzado— y el rastreador que pide <c>llms.txt</c> espera enlaces al sitio que visitó.
    /// </summary>
    private static string OriginOf(HttpContext context) =>
        $"{context.Request.Scheme}://{context.Request.Host.Value}";

    private static string BuildSitemap(string origin, IEnumerable<string> paths)
    {
        var buffer = new StringBuilder();

        using (var writer = XmlWriter.Create(buffer, new XmlWriterSettings
        {
            Indent = true,
            Encoding = Encoding.UTF8,
            OmitXmlDeclaration = false
        }))
        {
            writer.WriteStartDocument();
            writer.WriteStartElement("urlset", "http://www.sitemaps.org/schemas/sitemap/0.9");

            foreach (var path in paths.Distinct(StringComparer.Ordinal))
            {
                writer.WriteStartElement("url");
                writer.WriteElementString("loc", origin + path);
                writer.WriteEndElement();
            }

            writer.WriteEndElement();
            writer.WriteEndDocument();
        }

        // XmlWriter declara UTF-16 al escribir sobre un StringBuilder: lo que sale por HTTP es
        // UTF-8, y una declaración que miente hace que algunos validadores rechacen el fichero.
        return buffer.ToString().Replace("utf-16", "utf-8", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Formato de llmstxt.org: Markdown plano, un H1, una cita de resumen y listas de enlaces.
    /// Es lo único de esta clase que existe para que un modelo lo lea entero de una vez, así que
    /// lleva las cifras reales del catálogo y ninguna afirmación que no salga de la base.
    /// </summary>
    private static string BuildLlmsTxt(
        string origin,
        string brandName,
        IReadOnlyList<CourseCardDto> courses,
        IReadOnlyList<Pack> packs,
        IReadOnlyList<PlanDto> plans)
    {
        var text = new StringBuilder();

        text.AppendLine($"# {brandName}");
        text.AppendLine();
        text.AppendLine(
            "> Formación en ingeniería de agentes y prompt engineering para perfiles técnicos que " +
            "llevan sistemas con LLM a producción en entornos regulados de la Unión Europea.");
        text.AppendLine();
        text.AppendLine(
            "Los cursos parten de un caso conductor único y acaban en un sistema desplegado con " +
            "sus trazas, sus evaluaciones y su expediente de cumplimiento. El contenido está en " +
            "español. Cada curso emite certificado verificable públicamente por su código.");
        text.AppendLine();

        var publicados = courses.Where(c => !string.Equals(c.Status, "comingsoon", StringComparison.OrdinalIgnoreCase)).ToList();
        var proximos = courses.Except(publicados).ToList();

        if (publicados.Count > 0)
        {
            text.AppendLine("## Cursos");
            text.AppendLine();

            foreach (var course in publicados)
            {
                text.AppendLine(
                    $"- [{course.Title}]({origin}/curso/{course.Slug}): {course.ShortDescription} " +
                    $"Nivel {course.Level.ToLowerInvariant()}, {course.Hours} h, {course.LessonCount} clases.");
            }

            text.AppendLine();
        }

        if (proximos.Count > 0)
        {
            text.AppendLine("## Cursos en preparación");
            text.AppendLine();

            foreach (var course in proximos)
            {
                text.AppendLine($"- [{course.Title}]({origin}/curso/{course.Slug}): {course.ShortDescription}");
            }

            text.AppendLine();
        }

        if (packs.Count > 0)
        {
            text.AppendLine("## Packs sectoriales");
            text.AppendLine();

            foreach (var pack in packs)
            {
                // La fecha de verificación normativa se dice cuando la hay y se calla cuando no:
                // es el dato que hace citable un material de cumplimiento.
                var verificado = pack.RegulatoryCheckDate is { } fecha
                    ? $" Normativa verificada el {fecha:yyyy-MM-dd}."
                    : string.Empty;

                text.AppendLine(
                    $"- [{pack.Title}]({origin}/pack/{pack.Slug.Value}): materiales para {pack.Sector}. " +
                    $"Versión {pack.Version}.{verificado}");
            }

            text.AppendLine();
        }

        if (plans.Count > 0)
        {
            text.AppendLine("## Planes");
            text.AppendLine();

            foreach (var plan in plans.OrderBy(p => p.DisplayOrder))
            {
                text.AppendLine(
                    $"- {plan.Name}: {plan.Price:0.##} {plan.Currency} por {Interval(plan.Interval)}. " +
                    string.Join(' ', plan.Benefits.Select(b => b.TrimEnd('.') + '.')));
            }

            text.AppendLine();
        }

        text.AppendLine("## Páginas");
        text.AppendLine();
        text.AppendLine($"- [Catálogo completo]({origin}/cursos): todos los cursos con su temario.");
        text.AppendLine($"- [Precios]({origin}/precios): planes, compra suelta y qué incluye cada uno.");
        text.AppendLine($"- [Itinerario]({origin}/roadmap): en qué orden se hacen los cursos y qué exige cada uno.");
        text.AppendLine($"- [Para empresas]({origin}/empresas): formación de equipos y facturación a empresa.");
        text.AppendLine($"- [Preguntas frecuentes]({origin}/faq): acceso, certificados, devoluciones y requisitos.");
        text.AppendLine($"- [Verificar un certificado]({origin}/check-certificate): comprobación pública por código.");
        text.AppendLine($"- [Quién lo imparte]({origin}/sobre-mi).");
        text.AppendLine();
        text.AppendLine("## Opcional");
        text.AppendLine();
        text.AppendLine($"- [Condiciones de contratación]({origin}/legal/condiciones).");
        text.AppendLine($"- [Política de privacidad]({origin}/legal/privacidad).");

        return text.ToString();
    }

    private static string Interval(string interval) => interval.ToLowerInvariant() switch
    {
        "monthly" => "mes",
        "quarterly" => "trimestre",
        "biannual" => "semestre",
        "yearly" => "año",
        "lifetime" => "acceso vitalicio, pago único",
        _ => interval
    };
}
