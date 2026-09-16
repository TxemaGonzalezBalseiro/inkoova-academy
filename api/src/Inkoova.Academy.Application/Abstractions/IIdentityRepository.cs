using Inkoova.Academy.Domain.Identities;

namespace Inkoova.Academy.Application.Abstractions;

public interface IAcademyIdentityRepository
{
    Task<IReadOnlyList<AcademyIdentity>> GetAllAsync(CancellationToken ct);

    Task<AcademyIdentity?> GetByIdAsync(Guid id, CancellationToken ct);

    Task<AcademyIdentity?> GetBySlugAsync(string slug, CancellationToken ct);

    /// <summary>
    /// La marca principal. Nunca devuelve null en una base migrada: la migración crea una a
    /// partir de la identidad que ya había, y el dominio impide desactivarla o quedarse sin
    /// ninguna.
    /// </summary>
    Task<AcademyIdentity> GetDefaultAsync(CancellationToken ct);

    /// <summary>
    /// La marca que se está usando, a partir del nombre de host de la petición.
    ///
    /// Es así como se decide con qué marca sale un certificado o un correo: no se guarda a qué
    /// marca pertenece un alumno —el catálogo y los alumnos son comunes—, sino que manda el
    /// dominio por el que está entrando.
    ///
    /// Sin host, o con uno que no es de nadie, devuelve la principal: en desarrollo y en los
    /// procesos que no atienden peticiones no hay dominio del que tirar.
    /// </summary>
    Task<AcademyIdentity> GetForHostAsync(string? host, CancellationToken ct);

    Task UpsertAsync(AcademyIdentity identity, CancellationToken ct);

    /// <summary>
    /// Marca una como principal y se la quita a la anterior EN LA MISMA TRANSACCIÓN. Hacerlo en
    /// dos pasos dejaría un instante con dos principales —que el índice único rechaza— o con
    /// ninguna, que deja los correos de sistema sin remitente.
    /// </summary>
    Task<bool> MakeDefaultAsync(Guid id, CancellationToken ct);
}
