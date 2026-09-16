using Inkoova.Academy.Domain.Common;

namespace Inkoova.Academy.Domain.Identities;

/// <summary>
/// Quién es el titular del sitio, a efectos legales, y cómo se le contacta.
///
/// El artículo 10 de la LSSI obliga a publicar la denominación, el identificador fiscal, el
/// domicilio, los datos de inscripción registral cuando los haya y una vía de contacto. Son
/// justo el tipo de dato que NO se puede aproximar: un NIF inventado en un aviso legal es peor
/// que un hueco, porque el hueco se ve y el dato falso se cree.
///
/// Por eso todo empieza vacío y existe <see cref="Missing"/>: la página avisa de lo que falta
/// hasta que alguien lo escribe, en vez de enseñar un ejemplo con pinta de real.
/// </summary>
public sealed record LegalDetails(
    string LegalName,
    string TaxId,
    string Address,
    string RegistryDetails,
    string Email,
    string LinkedInUrl,
    string CompanyUrl,

    /// <summary>
    /// Clave de tipo de factura para una venta corriente, y para una rectificativa.
    ///
    /// Están aquí y no en el código porque qué clave corresponde a cada venta lo decide quien
    /// lleva la fiscalidad, y cambia con el negocio: una marca que empieza a pedir el NIF pasa
    /// de F2 a F1 sin que eso sea un despliegue. Los valores válidos los fija el esquema de la
    /// AEAT, no nosotros: ver <see cref="InvoiceTypes"/>.
    /// </summary>
    string InvoiceType = InvoiceTypes.DefaultOrdinary,
    string CorrectiveInvoiceType = InvoiceTypes.DefaultCorrective)
{
    public static readonly LegalDetails Empty = new(
        string.Empty, string.Empty, string.Empty, string.Empty,
        string.Empty, string.Empty, string.Empty);

    /// <summary>
    /// Lo que falta para poder publicar el aviso legal.
    ///
    /// Los datos registrales NO están en la lista: una persona física no está inscrita en
    /// ningún registro mercantil, así que exigírselos convertiría un aviso correcto en un aviso
    /// permanentemente «incompleto».
    /// </summary>
    public IReadOnlyList<string> Missing =>
        new[]
        {
            (Field: "razón social", Value: LegalName),
            (Field: "NIF", Value: TaxId),
            (Field: "domicilio", Value: Address),
            (Field: "correo de contacto", Value: Email),
        }
        .Where(x => string.IsNullOrWhiteSpace(x.Value))
        .Select(x => x.Field)
        .ToArray();

    public bool IsComplete => Missing.Count == 0;

    public Error? Validate()
    {
        if (LegalName.Length > 200)
        {
            return Error.Validation("legal.name_too_long", "La razón social no puede pasar de 200 caracteres.");
        }

        if (TaxId.Length > 40)
        {
            return Error.Validation("legal.tax_id_too_long", "El identificador fiscal no puede pasar de 40 caracteres.");
        }

        if (Email.Length > 0 && (!Email.Contains('@') || Email.Contains(' ')))
        {
            return Error.Validation("legal.email_invalid", "El correo de contacto legal no es válido.");
        }

        // Los enlaces del pie acaban en un `href` que ve cualquier visitante: solo http(s). Sin
        // esto, un `javascript:` guardado desde el panel se ejecutaría en el navegador de todo
        // el que entre en la web.
        foreach (var (label, url) in new[] { ("LinkedIn", LinkedInUrl), ("la web de la empresa", CompanyUrl) })
        {
            if (url.Length > 0
                && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                && !url.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            {
                return Error.Validation(
                    "legal.url_invalid",
                    $"El enlace de {label} debe empezar por https://.");
            }
        }

        // La AEAT rechaza el registro entero si la clave no está en su enumeración, y el rechazo
        // llega horas después por el reintento diario, no al facturar. Se para aquí.
        if (!InvoiceTypes.IsOrdinary(InvoiceType))
        {
            return Error.Validation(
                "legal.invoice_type_invalid",
                $"«{InvoiceType}» no es una clave de factura válida. Admitidas: " +
                string.Join(", ", InvoiceTypes.Ordinary.Select(t => t.Code)) + ".");
        }

        if (!InvoiceTypes.IsCorrective(CorrectiveInvoiceType))
        {
            return Error.Validation(
                "legal.corrective_invoice_type_invalid",
                $"«{CorrectiveInvoiceType}» no es una clave de rectificativa válida. Admitidas: " +
                string.Join(", ", InvoiceTypes.Corrective.Select(t => t.Code)) + ".");
        }

        return null;
    }

    /// <summary>Quita espacios sobrantes de todo, que es lo único que hay que normalizar aquí.</summary>
    public static LegalDetails From(
        string? legalName,
        string? taxId,
        string? address,
        string? registryDetails,
        string? email,
        string? linkedInUrl,
        string? companyUrl,
        string? invoiceType = null,
        string? correctiveInvoiceType = null) =>
        new(
            Clean(legalName), Clean(taxId), Clean(address), Clean(registryDetails),
            Clean(email), Clean(linkedInUrl), Clean(companyUrl),
            // Vacío significa «déjalo como está por defecto», no «bórralo»: una clave en blanco
            // en la petición no puede acabar en un registro que la AEAT rechace.
            Blank(invoiceType) ? InvoiceTypes.DefaultOrdinary : Clean(invoiceType).ToUpperInvariant(),
            Blank(correctiveInvoiceType)
                ? InvoiceTypes.DefaultCorrective
                : Clean(correctiveInvoiceType).ToUpperInvariant());

    private static bool Blank(string? value) => string.IsNullOrWhiteSpace(value);

    private static string Clean(string? value) => (value ?? string.Empty).Trim();
}
