namespace Inkoova.Academy.Application.Tutoring;

/// <summary>
/// Paquete tal y como lo ve el panel y, si está activo, el alumno.
///
/// Se envía el tiempo en minutos, no en horas: la conversión a "5 h 30 min" es presentación y
/// vive en la SPA. Mandar un decimal desde aquí obligaría a las dos partes a ponerse de acuerdo
/// sobre el redondeo.
/// </summary>
public sealed record TutoringPackageDto(
    Guid Id,
    string Slug,
    string Name,
    string Description,
    int Minutes,
    long PriceCents,
    string Currency,
    /// <summary>'fixed', 'subscription' o 'never'.</summary>
    string ExpiryMode,
    int? ValidityDays,
    int DisplayOrder,
    bool IsActive,
    string? StripePriceId);

/// <summary>
/// Resultado de sincronizar un paquete con Stripe. Se devuelve uno por paquete, enlazado o no:
/// una lista con solo los que fueron bien dejaría al panel sin poder decir cuál falló.
/// </summary>
public sealed record TutoringSyncResult(string Slug, bool Linked, string? PriceId, string? Problem);

/// <summary>Una tutoría ya dada, como aparece en el histórico del alumno y en el del panel.</summary>
public sealed record TutoringSessionDto(
    Guid Id,
    int Minutes,
    DateTimeOffset OccurredAt,
    string Topic,
    string Notes,
    Guid? TutorId,
    // El nombre viaja resuelto y no solo el identificador: quien lee el histórico —el
    // alumno incluido— no tiene la lista de profesores para traducirlo.
    string? TutorName);

/// <summary>
/// Una bolsa de horas con su saldo. <c>MinutesRemaining</c> se calcula, nunca se lee de una
/// columna: el saldo es la resta y así no puede desincronizarse.
/// </summary>
public sealed record TutoringGrantDto(
    Guid Id,
    Guid? PackageId,
    string PackageName,
    int MinutesTotal,
    int MinutesUsed,
    int MinutesRemaining,
    string Source,
    DateTimeOffset GrantedAt,
    /// <summary>'fixed', 'subscription' o 'never'.</summary>
    string ExpiryMode,
    /// <summary>
    /// La fecha que de verdad manda. En «con la suscripción» es hasta cuándo llega el plan del
    /// alumno, y cambia sola en cada renovación; en un vitalicio no hay ninguna.
    /// </summary>
    DateTimeOffset? ExpiresAt,
    bool IsExpired,
    bool IsRevoked,
    string? RevokedReason,
    string Note,
    IReadOnlyList<TutoringSessionDto> Sessions,
    // Lo convocado contra esta bolsa y todavía sin dar. No descuenta saldo: convocar no
    // es dar, y reservar el tiempo dejaría horas muertas si alguien olvida anular.
    IReadOnlyList<TutoringAppointmentDto> Appointments);

/// <summary>
/// Lo que ve el alumno en su cuenta: cuánto le queda en total y de dónde sale.
///
/// <c>MinutesRemaining</c> del resumen suma SOLO las bolsas utilizables. Una bolsa caducada con
/// horas sin gastar sigue en la lista para que se vea qué pasó, pero no se puede gastar, así
/// que sumarla en el titular sería prometer un tiempo que no existe.
/// </summary>
public sealed record MyTutoringDto(
    int MinutesRemaining,
    int MinutesTotal,
    int MinutesUsed,
    DateTimeOffset? NextExpiry,
    IReadOnlyList<TutoringGrantDto> Grants,
    IReadOnlyList<TutoringPackageDto> Offer,
    /// <summary>
    /// Si su plan le da acceso ahora mismo. Con las horas atadas a la suscripción es lo que
    /// separa «no te quedan» de «las tienes pero tu plan no está activo», que son dos mensajes
    /// muy distintos para el alumno.
    /// </summary>
    bool HasPlanAccess,
    DateTimeOffset? PlanValidUntil);

/// <summary>Fila de la tabla del panel: un alumno con tutorías y su saldo.</summary>
public sealed record TutoringBalanceDto(
    Guid UserId,
    string DisplayName,
    string Email,
    int MinutesTotal,
    int MinutesUsed,
    int MinutesRemaining,
    int ActiveGrants);

/// <summary>Ficha de un alumno en el panel de tutorías.</summary>
public sealed record StudentTutoringDto(
    Guid UserId,
    string DisplayName,
    string Email,
    int MinutesRemaining,
    int MinutesTotal,
    int MinutesUsed,
    IReadOnlyList<TutoringGrantDto> Grants,
    /// <summary>
    /// Si su plan le da acceso ahora. Con las horas atadas a la suscripción, es lo que explica
    /// por qué un alumno con saldo puede tenerlo todo parado: no es un fallo, es que no ha
    /// renovado.
    /// </summary>
    bool HasPlanAccess,
    DateTimeOffset? PlanValidUntil);
