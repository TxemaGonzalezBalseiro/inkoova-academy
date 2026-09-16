namespace Inkoova.Academy.Application.Abstractions;

/// <summary>Una plantilla de correo escrita para una marca concreta.</summary>
public sealed record StoredEmailTemplate(string Name, string Subject, string Html);

/// <summary>
/// Plantillas de correo propias de una marca.
///
/// Lo que NO está aquí cae en el fichero de <c>emails/</c>, que es el original. Es deliberado:
/// una marca recién creada manda correos desde el primer minuto sin que nadie tenga que
/// escribir ocho plantillas antes. Reescribir una es una decisión, no un trámite de alta.
/// </summary>
public interface IEmailTemplateStore
{
    /// <summary>La plantilla propia de esa marca, o <c>null</c> si usa la del fichero.</summary>
    Task<StoredEmailTemplate?> GetAsync(Guid identityId, string name, CancellationToken ct);

    Task<IReadOnlyList<StoredEmailTemplate>> GetAllAsync(Guid identityId, CancellationToken ct);

    Task SaveAsync(Guid identityId, StoredEmailTemplate template, Guid actor, CancellationToken ct);

    /// <summary>Borra la propia y vuelve a la del fichero. Es el «descartar mis cambios».</summary>
    Task RemoveAsync(Guid identityId, string name, CancellationToken ct);
}

/// <summary>
/// Cabecera, emisor y colores del certificado de una marca.
///
/// Nunca devuelve null: sin fila, la marca emite el certificado de siempre. Así crear una marca
/// no obliga a diseñar un certificado antes de poder emitir el primero.
/// </summary>
public interface ICertificateStyleStore
{
    Task<CertificateStyle> GetAsync(Guid identityId, CancellationToken ct);

    Task SaveAsync(Guid identityId, CertificateStyle style, Guid actor, CancellationToken ct);
}
