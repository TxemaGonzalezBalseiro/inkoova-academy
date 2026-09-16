using Inkoova.Academy.Domain.Tutoring;

namespace Inkoova.Academy.Application.Abstractions;

public interface ITutoringPackageRepository
{
    /// <summary>Solo los que están a la venta. Es lo que ve el alumno.</summary>
    Task<IReadOnlyList<TutoringPackage>> GetActiveAsync(CancellationToken ct);

    /// <summary>Todos, retirados incluidos: el panel tiene que poder reactivarlos.</summary>
    Task<IReadOnlyList<TutoringPackage>> GetAllAsync(CancellationToken ct);

    Task<TutoringPackage?> GetByIdAsync(Guid id, CancellationToken ct);

    Task<TutoringPackage?> GetBySlugAsync(string slug, CancellationToken ct);

    Task UpsertAsync(TutoringPackage package, CancellationToken ct);
}

public interface ITutoringGrantRepository
{
    /// <summary>Una bolsa con TODAS sus sesiones cargadas: sin ellas el saldo no se puede calcular.</summary>
    Task<TutoringGrant?> GetByIdAsync(Guid id, CancellationToken ct);

    /// <summary>Las bolsas de un alumno, de la más reciente a la más antigua, con sus sesiones.</summary>
    Task<IReadOnlyList<TutoringGrant>> GetForUserAsync(Guid userId, CancellationToken ct);

    /// <summary>
    /// Guarda la bolsa y sincroniza sus sesiones en la misma transacción. Si se guardara la
    /// bolsa sin sus sesiones, el saldo saldría mal en la siguiente lectura.
    /// </summary>
    Task SaveAsync(TutoringGrant grant, CancellationToken ct);

    /// <summary>
    /// Resumen por alumno para la lista del panel: quién tiene tutorías y cuántas le quedan,
    /// sin traerse todas las sesiones de todos. Recorrer bolsa a bolsa desde la aplicación
    /// serían N+1 consultas para pintar una tabla.
    /// </summary>
    Task<IReadOnlyList<TutoringBalanceRow>> GetBalancesAsync(CancellationToken ct);
}

/// <summary>
/// Fila del resumen del panel. Vive en Abstractions y no en Domain porque es una proyección de
/// lectura, no una entidad: no tiene invariantes que proteger.
/// </summary>
public sealed record TutoringBalanceRow(
    Guid UserId,
    string DisplayName,
    string Email,
    int MinutesTotal,
    int MinutesUsed,
    int ActiveGrants);
