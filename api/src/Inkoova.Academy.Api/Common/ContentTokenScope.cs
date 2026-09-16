namespace Inkoova.Academy.Api.Common;

/// <summary>
/// Qué alcanza un token de contenido. Vive aparte de los endpoints porque ahora hay dos
/// caminos que lo consultan —el que sirve los bytes y el que solo autoriza para que los
/// sirva Caddy (ADR-012)— y dos copias de esta regla acabarían discrepando en algo.
/// </summary>
public static class ContentTokenScope
{
    /// <summary>Carpeta con los datos propios de un documento, creada por el importador.</summary>
    private const string SidecarSuffix = ".data";

    /// <summary>Chrome compartido del curso: hojas de estilo, visor, imágenes. No es material.</summary>
    private const string SharedAssets = "assets";

    /// <summary>
    /// Qué puede pedir un token. Tres cosas y ninguna más:
    ///
    /// 1. Su propio documento.
    /// 2. El chrome compartido del curso, <c>assets/</c>: el CSS, el visor y las imágenes que
    ///    el HTML importado carga por su cuenta. Es presentación, no material del curso.
    /// 3. Su propia carpeta de datos, <c>&lt;documento&gt;.data/</c>, donde el importador deja
    ///    los quizzes y los ejercicios de esa lección y solo de esa.
    ///
    /// Lo demás se rechaza, incluido cualquier cosa del mismo curso. Antes el criterio era
    /// pertenecer al curso, y con eso el token de una lección gratuita alcanzaba el banco de
    /// quizzes entero: las preguntas y los <c>model_answer</c> de los bloques de pago quedaban
    /// a la vista. Atar los datos al documento es lo que cierra eso.
    /// </summary>
    public static bool Covers(string contentRef, string requestedPath)
    {
        var normalised = Normalise(requestedPath);
        if (normalised is null)
        {
            return false;
        }

        // El contentRef lleva el ancla de la slide (…/B0.html#slide-7). El navegador nunca la
        // envía, así que se descarta antes de comparar o el documento no casaría consigo mismo.
        var document = Normalise(DocumentOf(contentRef));
        if (document is null)
        {
            return false;
        }

        if (string.Equals(normalised, document, StringComparison.Ordinal))
        {
            return true;
        }

        // Un documento nunca es un accesorio de otro. Sin esto, una lección con nombre bien
        // elegido podría colarse como si fuera un dato.
        if (IsDocument(normalised))
        {
            return false;
        }

        var course = CourseRootOf(document);

        if (course.Length > 0
            && normalised.StartsWith($"{course}/{SharedAssets}/", StringComparison.Ordinal))
        {
            return true;
        }

        return normalised.StartsWith(SidecarOf(document), StringComparison.Ordinal);
    }

    /// <summary>Ruta del fichero sin el ancla de slide: es del navegador, no del disco.</summary>
    public static string DocumentOf(string contentRef)
    {
        var hash = contentRef.IndexOf('#', StringComparison.Ordinal);
        return hash < 0 ? contentRef : contentRef[..hash];
    }

    /// <summary>
    /// Ruta relativa, con barras hacia delante y sin ningún segmento capaz de salirse.
    /// Devuelve <c>null</c> si no se puede normalizar a algo servible.
    ///
    /// El rechazo explícito de <c>..</c> no es redundante. Cuando los bytes los mandaba la API,
    /// <c>LocalDiskContentStorage</c> colapsaba la ruta y comprobaba que siguiera dentro del
    /// volumen, así que <c>curso/assets/../../etc/passwd</c> pasaba este alcance y moría
    /// después, en el disco. Ahora la ruta que se aprueba aquí viaja en una cabecera hasta el
    /// <c>file_server</c> de Caddy: lo que se aprueba es lo que se sirve, y el segundo cerrojo
    /// ya no está detrás.
    /// </summary>
    public static string? Normalise(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var normalised = path.Replace('\\', '/').Trim('/');

        if (normalised.Length == 0 || normalised.Contains('\0', StringComparison.Ordinal))
        {
            return null;
        }

        foreach (var segment in normalised.Split('/'))
        {
            if (segment.Length == 0 || segment is "." or "..")
            {
                return null;
            }
        }

        return normalised;
    }

    private static bool IsDocument(string path) =>
        path.EndsWith(".html", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith(".htm", StringComparison.OrdinalIgnoreCase);

    /// <summary>Primer segmento del contentRef: la carpeta del curso dentro del volumen.</summary>
    private static string CourseRootOf(string document)
    {
        var separator = document.IndexOf('/', StringComparison.Ordinal);
        return separator <= 0 ? string.Empty : document[..separator];
    }

    /// <summary>
    /// <c>curso/bloques/B3-multi-agente.html</c> → <c>curso/bloques/B3-multi-agente.data/</c>.
    /// El nombre sale del documento, así que el permiso no depende de conocer el contenido.
    /// </summary>
    private static string SidecarOf(string document)
    {
        var dot = document.LastIndexOf('.');
        var stem = dot <= 0 ? document : document[..dot];
        return $"{stem}{SidecarSuffix}/";
    }
}
