namespace Inkoova.Academy.Application.Abstractions;

/// <summary>
/// La identidad de la academia: cómo se llama, qué dice de sí misma y dónde se la contacta.
///
/// Nunca trae null. Un ajuste sin poner llega como cadena vacía, y quien lo use decide con qué
/// lo sustituye. Devolver null obligaría a comprobarlo en cada plantilla de correo.
/// </summary>
public sealed record AcademyBranding(
    string Name,
    string Tagline,
    string LogoUrl,
    string PublicDomain,
    string SupportEmail);

/// <summary>
/// Lee la identidad configurada desde el panel.
///
/// Existe como puerto porque los correos también la necesitan: el remitente, el pie y la
/// dirección de contacto de cada correo transaccional salen de aquí. Antes estaban escritos a
/// mano en las plantillas y en el remitente, así que cambiar el nombre de la academia en el
/// panel no cambiaba ni una línea de lo que recibía el alumno.
/// </summary>
public interface IAcademyBrandingReader
{
    Task<AcademyBranding> GetAsync(CancellationToken ct);
}
