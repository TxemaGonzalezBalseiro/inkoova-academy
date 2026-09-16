using Inkoova.Academy.Domain.Billing;
using Inkoova.Academy.Domain.Catalog;
using Inkoova.Academy.Domain.Common;
using Inkoova.Academy.Domain.Learning;
using Inkoova.Academy.Domain.ValueObjects;

namespace Inkoova.Academy.Application.Abstractions;

/// <summary>
/// The only place in the system that answers "can this person open this?" (CLAUDE.md
/// convention 7). Endpoints call it; they never inspect subscriptions themselves.
/// </summary>
public interface IAccessPolicy
{
    Task<bool> CanAccessLessonAsync(Guid? userId, Lesson lesson, CancellationToken ct);

    Task<bool> CanAccessProductAsync(Guid? userId, Guid productId, CancellationToken ct);

    /// <summary>Products the user currently holds. Used to bulk-decorate a syllabus without N queries.</summary>
    Task<IReadOnlySet<Guid>> GetAccessibleProductIdsAsync(Guid userId, CancellationToken ct);
}

/// <summary>Reads and writes the imported course content. Local disk by default (ADR-003).</summary>
public interface IContentStorage
{
    Task<bool> ExistsAsync(string contentRef, CancellationToken ct);

    Task<Stream?> OpenReadAsync(string contentRef, CancellationToken ct);

    Task<long> GetSizeAsync(string contentRef, CancellationToken ct);

    Task WriteAsync(string contentRef, Stream content, CancellationToken ct);

    /// <summary>Content type inferred from the extension. The store never trusts client input.</summary>
    string GetContentType(string contentRef);
}

/// <summary>
/// Mints and validates the short-lived token that stands in for a content path (ADR-003).
/// Tokens are opaque to the SPA and carry the ref plus an expiry.
/// </summary>
public interface IContentTokenService
{
    string Issue(string contentRef, TimeSpan lifetime);

    Result<string, Error> Validate(string token);
}

public sealed record CheckoutRequest(
    Guid UserId,
    string UserEmail,
    string? StripeCustomerId,
    string PriceId,
    bool IsSubscription,
    string SuccessUrl,
    string CancelUrl,
    string? PromotionCodeId,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record CheckoutSessionResult(string SessionId, string Url, string CustomerId);

/// <summary>Stripe behind a port so the webhook tests and the settlement job do not need the SDK.</summary>
public interface IPaymentGateway
{
    Task<Result<CheckoutSessionResult, Error>> CreateCheckoutSessionAsync(CheckoutRequest request, CancellationToken ct);

    Task<Result<string, Error>> CreateBillingPortalSessionAsync(string customerId, string returnUrl, CancellationToken ct);

    Task<Result<string, Error>> EnsureCustomerAsync(Guid userId, string email, string displayName, CancellationToken ct);

    /// <summary>Idempotent product and price creation used by the seed script (T-04).</summary>
    Task<Result<string, Error>> EnsurePriceAsync(
        string productName,
        string lookupKey,
        Money price,
        BillingInterval? recurringInterval,
        CancellationToken ct);

    Task<Result<string, Error>> EnsurePromotionCodeAsync(
        string code,
        decimal percentOff,
        Guid? affiliateId,
        CancellationToken ct);
}

public sealed record EmailMessage(
    string To,
    string Subject,
    string HtmlBody,
    string TextBody,
    IReadOnlyList<(string FileName, byte[] Content, string ContentType)>? Attachments = null,
    /// <summary>
    /// Desde qué marca sale. <c>null</c> = la principal, que es lo correcto para todo lo que no
    /// pertenece a un curso concreto: confirmar la cuenta, restablecer la contraseña, avisar de
    /// un cobro fallido.
    ///
    /// Cuando el correo SÍ es de un curso, quien lo manda pasa la marca de ese curso, y el
    /// alumno lo recibe desde el buzón de la marca que está estudiando.
    /// </summary>
    Guid? IdentityId = null);

public interface IEmailSender
{
    Task<Result<Unit, Error>> SendAsync(EmailMessage message, CancellationToken ct);
}

/// <summary>Renders a template from <c>/emails</c> with the given model.</summary>
public interface IEmailTemplateRenderer
{
    /// <summary>
    /// Compone el correo. Es asíncrono porque la identidad de la academia —el nombre, el
    /// dominio y el correo de contacto que van en el asunto y en el pie— se lee de la base, que
    /// es donde la edita el panel. Con una versión síncrona habría que elegir entre bloquear un
    /// hilo o dejar la marca escrita a mano en las plantillas, que es de donde venimos.
    /// </summary>
    /// <param name="identityId">
    /// La marca cuyo texto se usa. <c>null</c> = la principal. Si esa marca tiene una plantilla
    /// propia se usa; si no, la del fichero, que sigue siendo el original.
    /// </param>
    Task<Result<(string Subject, string Html, string Text), Error>> RenderAsync(
        string templateName,
        IReadOnlyDictionary<string, string> model,
        CancellationToken ct,
        Guid? identityId = null);

    /// <summary>
    /// Compone un asunto y un cuerpo SUELTOS, sin buscarlos por nombre.
    ///
    /// Es lo que necesita la vista previa del panel: enseñar cómo queda lo que se está
    /// escribiendo, que todavía no está guardado en ningún sitio. Pasa por la misma sustitución
    /// y la misma marca que un correo de verdad, así que lo que se ve es lo que se manda.
    /// </summary>
    Task<(string Subject, string Html, string Text)> RenderContentAsync(
        string subject,
        string html,
        IReadOnlyDictionary<string, string> model,
        CancellationToken ct,
        Guid? identityId = null);

    /// <summary>
    /// La plantilla original de fichero, sin sustituir nada. La necesita el editor para
    /// arrancar de lo que hay hoy en vez de un cuadro vacío: reescribir un correo desde cero
    /// para cambiarle una frase es la forma más rápida de perder el pie legal o un enlace.
    /// </summary>
    Result<(string Subject, string Html), Error> GetOriginal(string templateName);
}

public interface ICatalogCache
{
    Task<T?> GetOrCreateAsync<T>(string key, Func<CancellationToken, Task<T>> factory, CancellationToken ct) where T : class;

    /// <summary>Called when the admin publishes; makes the change visible without a redeploy (T-11).</summary>
    void Invalidate();
}

/// <summary>
/// Lo único del certificado que se puede configurar: la cabecera, quién lo emite y los colores.
///
/// Todo lo demás —el sello, el QR, el código, el hash y el pie con la URL de verificación— lo
/// dibuja el generador y NO es configurable. Es a propósito: si el pie fuese editable, bastaría
/// con borrar un trozo para que ese certificado dejara de poder comprobarse, y el fallo no se
/// vería hasta que alguien intentara verificarlo, que es el peor momento.
///
/// Cada campo vacío cae en el valor de siempre, así que una marca sin estilo propio emite el
/// certificado que ya emitía.
/// </summary>
public sealed record CertificateStyle(
    string Heading,
    string Subheading,
    string IssuerName,
    string IssuerNote,
    string PrimaryColor,
    string AccentColor,
    string SupportColor)
{
    public static readonly CertificateStyle Default =
        new(string.Empty, string.Empty, string.Empty, string.Empty,
            string.Empty, string.Empty, string.Empty);

    /// <summary>El valor puesto, o el de siempre si está vacío.</summary>
    public string Or(string value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
}

public sealed record CertificatePdfModel(
    string StudentName,
    string CourseTitle,
    string Code,
    string Hash,
    DateOnly IssueDate,
    int Hours,
    string VerificationUrl,
    /// <summary>Cabecera, emisor y colores de la marca que emite. Ver <see cref="CertificateStyle"/>.</summary>
    CertificateStyle? Style = null);

public interface ICertificatePdfGenerator
{
    /// <summary>Deterministic: same model produces byte-identical output (T-10).</summary>
    byte[] Generate(CertificatePdfModel model);

    /// <summary>
    /// PNG del mismo certificado, para compartirlo donde un PDF no se ve: LinkedIn, un mensaje,
    /// una captura. Sale del mismo documento que el PDF, no de un diseño aparte, para que lo que
    /// alguien enseña sea exactamente lo que se puede verificar.
    /// </summary>
    byte[] GenerateImage(CertificatePdfModel model);
}

public sealed record AffiliateStatementModel(
    string AffiliateName,
    string AffiliateCode,
    string TaxId,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    IReadOnlyList<(DateOnly Date, string Product, decimal NetBase, decimal Percent, decimal Amount)> Lines,
    decimal CarriedOverIn,
    decimal Total,
    string Currency);

public interface IAffiliateStatementGenerator
{
    byte[] Generate(AffiliateStatementModel model);
}

/// <summary>Discord role sync (T-12). Failures are logged and retried; they never block billing.</summary>
public interface IDiscordGateway
{
    Task<Result<Unit, Error>> AssignRoleAsync(string discordUserId, string roleName, CancellationToken ct);

    Task<Result<Unit, Error>> RemoveRoleAsync(string discordUserId, string roleName, CancellationToken ct);

    Task<Result<string, Error>> ExchangeOAuthCodeAsync(string code, string redirectUri, CancellationToken ct);
}

/// <summary>
/// Todo lo que aparece impreso en una factura.
///
/// Lleva la huella YA CALCULADA y no la calcula el que imprime: la huella la manda el registro
/// de facturación (<c>VerifactuService</c>), y calcularla en dos sitios acabaría con un PDF que
/// dice una y un registro declarado con otra.
/// </summary>
public sealed record InvoiceDocument(
    InvoiceIssuer Issuer,
    string FullNumber,
    DateOnly IssueDate,
    string CustomerName,
    string? CustomerTaxId,
    string CustomerCountry,
    string Concept,
    Money Total,
    Money Tax,
    string Hash,
    string PreviousHash,
    bool IsRectification,
    string? RectifiedNumber,
    // Si el registro se remitió a la AEAT. Cambia lo que se imprime al pie.
    bool DeclaredToAeat);

/// <summary>
/// Imprime la factura. Solo eso: la numeración, el encadenamiento y la declaración son del
/// registro de facturación, no del documento.
/// </summary>
public interface IInvoiceDocumentRenderer
{
    byte[] Render(InvoiceDocument document);
}

/// <summary>Product resolution helpers shared by checkout, webhooks and admin.</summary>
public interface IProductCatalog
{
    Task<Product?> ResolveAsync(Slug slug, CancellationToken ct);

    /// <summary>Every pack product. Used when a yearly or lifetime plan grants them all.</summary>
    Task<IReadOnlyList<Product>> GetAllPackProductsAsync(CancellationToken ct);

    /// <summary>
    /// Products of every published course. A subscription grants the whole catalogue, so this
    /// is the set that gets plan-derived entitlements on each successful invoice.
    /// </summary>
    Task<IReadOnlyList<Product>> GetAllCourseProductsAsync(CancellationToken ct);
}
