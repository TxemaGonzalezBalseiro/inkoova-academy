using Inkoova.Academy.Domain.Common;
using Inkoova.Academy.Domain.ValueObjects;

namespace Inkoova.Academy.Domain.Tutoring;

/// <summary>
/// Cuándo dejan de poder gastarse unas horas.
///
/// No es una fecha porque la que de verdad se vende no lo es: las tutorías viven mientras el
/// alumno siga suscrito, y esa fecha se mueve sola cada vez que renueva. Guardar una fecha
/// obligaría a que algo la empujara en cada renovación, y ese algo puede fallar un día y
/// caducarle las horas a todo el mundo sin que nadie haya tocado nada.
/// </summary>
public enum TutoringExpiry
{
    /// <summary>Caduca en la fecha guardada. Para acuerdos sueltos que no dependen de un plan.</summary>
    FixedDate,

    /// <summary>
    /// Vive mientras la suscripción del alumno le dé acceso. Si renueva, siguen ahí; si deja de
    /// renovar, se pierden. Es lo que se vende en el catálogo.
    /// </summary>
    WithSubscription,

    /// <summary>No caduca nunca.</summary>
    Never
}

/// <summary>De dónde salieron estas horas. Se guarda para poder auditarlo, igual que en entitlement.</summary>
public enum TutoringGrantSource
{
    /// <summary>Pagadas.</summary>
    Purchase,

    /// <summary>Concedidas desde administración: beca, acuerdo con una empresa, cortesía.</summary>
    Manual
}

/// <summary>
/// Las horas de tutoría que tiene un alumno concreto, con lo que ya ha consumido de ellas.
///
/// El nombre y los minutos del paquete se COPIAN al conceder. Si mañana el paquete de 5 horas
/// pasa a 10, quien compró 5 sigue teniendo 5: sin la copia, editar el catálogo reescribiría
/// lo que ya compraron todos los alumnos.
///
/// El saldo no se guarda en ningún sitio. Es <see cref="MinutesTotal"/> menos la suma de las
/// sesiones, y se recalcula siempre. Un contador guardado se desincroniza en cuanto alguien
/// corrige una sesión; una resta no puede.
/// </summary>
public sealed class TutoringGrant
{
    private readonly List<TutoringSession> _sessions;

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }

    /// <summary>Paquete del que salió. <c>null</c> si fue una concesión suelta o si el paquete se borró.</summary>
    public Guid? PackageId { get; private set; }

    public string PackageName { get; private set; }
    public int MinutesTotal { get; private set; }
    public TutoringGrantSource Source { get; private set; }
    public Guid? PurchaseId { get; private set; }
    public DateTimeOffset GrantedAt { get; private set; }

    /// <summary>Cuándo dejan de poder gastarse. Ver <see cref="TutoringExpiry"/>.</summary>
    public TutoringExpiry ExpiryMode { get; private set; }

    /// <summary>
    /// La fecha, solo cuando el modo es <see cref="TutoringExpiry.FixedDate"/>. En los otros dos
    /// es <c>null</c>: en «con la suscripción» la fecha la pone la suscripción y cambia en cada
    /// renovación, así que guardarla aquí sería guardar una copia que envejece.
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public string? RevokedReason { get; private set; }
    public string Note { get; private set; }
    public Guid? GrantedBy { get; private set; }

    /// <summary>
    /// Lo que el alumno pagó por estas horas, copiado al conceder.
    ///
    /// Cero en las concesiones manuales, que es lo correcto y no un hueco: una beca no
    /// generó ingreso, y repartir un porcentaje de un ingreso que no existe es pagar de la
    /// propia caja sin enterarse.
    /// </summary>
    public Money Paid { get; private set; }

    public IReadOnlyList<TutoringSession> Sessions => _sessions;

    private TutoringGrant(
        Guid id,
        Guid userId,
        Guid? packageId,
        string packageName,
        int minutesTotal,
        TutoringGrantSource source,
        Guid? purchaseId,
        DateTimeOffset grantedAt,
        TutoringExpiry expiryMode,
        DateTimeOffset? expiresAt,
        DateTimeOffset? revokedAt,
        string? revokedReason,
        string note,
        Guid? grantedBy,
        Money paid,
        List<TutoringSession> sessions)
    {
        Paid = paid;
        Id = id;
        UserId = userId;
        PackageId = packageId;
        PackageName = packageName;
        MinutesTotal = minutesTotal;
        Source = source;
        PurchaseId = purchaseId;
        GrantedAt = grantedAt;
        ExpiryMode = expiryMode;
        ExpiresAt = expiresAt;
        RevokedAt = revokedAt;
        RevokedReason = revokedReason;
        Note = note;
        GrantedBy = grantedBy;
        _sessions = sessions;
    }

    /// <summary>
    /// Concesión a partir de un paquete del catálogo: copia su nombre, sus minutos y su forma de
    /// caducar. La fecha solo se calcula en el modo de fecha fija; en «con la suscripción» la
    /// pone la suscripción del alumno cada vez que se mira.
    /// </summary>
    public static Result<TutoringGrant, Error> FromPackage(
        Guid id,
        Guid userId,
        TutoringPackage package,
        TutoringGrantSource source,
        Guid? purchaseId,
        DateTimeOffset now,
        string note,
        Guid? grantedBy)
    {
        var expiresAt = package.ExpiryMode == TutoringExpiry.FixedDate && package.ValidityDays is { } days
            ? now.AddDays(days)
            : (DateTimeOffset?)null;

        return Create(
            id, userId, package.Id, package.Name, package.Minutes,
            source, purchaseId, now, package.ExpiryMode, expiresAt, note, grantedBy,
            // Una concesión manual no entró por caja aunque salga del mismo paquete.
            source == TutoringGrantSource.Purchase
                ? package.Price
                : Money.Zero(package.Price.Currency));
    }

    public static Result<TutoringGrant, Error> Create(
        Guid id,
        Guid userId,
        Guid? packageId,
        string packageName,
        int minutesTotal,
        TutoringGrantSource source,
        Guid? purchaseId,
        DateTimeOffset grantedAt,
        TutoringExpiry expiryMode,
        DateTimeOffset? expiresAt,
        string note,
        Guid? grantedBy,
        Money? paid = null)
    {
        if (string.IsNullOrWhiteSpace(packageName))
        {
            return Error.Validation("tutoring_grant.name_empty", "La concesión necesita un nombre.");
        }

        if (minutesTotal <= 0)
        {
            return Error.Validation("tutoring_grant.minutes_invalid", "Hay que conceder al menos un minuto.");
        }

        if (minutesTotal > TutoringPackage.MaximumMinutes)
        {
            return Error.Validation(
                "tutoring_grant.minutes_too_large",
                $"No se pueden conceder más de {TutoringPackage.MaximumMinutes / 60} horas de una vez.");
        }

        if (expiresAt is not null && expiresAt <= grantedAt)
        {
            return Error.Validation("tutoring_grant.expiry_invalid", "La caducidad tiene que ser posterior a la concesión.");
        }

        if (expiryMode == TutoringExpiry.FixedDate && expiresAt is null)
        {
            // Modo de fecha fija sin fecha sería una bolsa que no caduca nunca disfrazada de
            // una que sí. Quien quiera eso tiene el modo `Never`, que lo dice.
            return Error.Validation(
                "tutoring_grant.expiry_missing",
                "Una caducidad por fecha necesita una fecha.");
        }

        return new TutoringGrant(
            id, userId, packageId, packageName.Trim(), minutesTotal, source, purchaseId,
            grantedAt, expiryMode,
            // La fecha solo significa algo en el modo de fecha fija. Guardarla en los otros dos
            // dejaría un dato que nadie lee y que alguien acabará leyendo por error.
            expiryMode == TutoringExpiry.FixedDate ? expiresAt : null,
            revokedAt: null, revokedReason: null,
            (note ?? string.Empty).Trim(), grantedBy, paid ?? Money.Euros(0), []);
    }

    public static TutoringGrant Rehydrate(
        Guid id,
        Guid userId,
        Guid? packageId,
        string packageName,
        int minutesTotal,
        TutoringGrantSource source,
        Guid? purchaseId,
        DateTimeOffset grantedAt,
        TutoringExpiry expiryMode,
        DateTimeOffset? expiresAt,
        DateTimeOffset? revokedAt,
        string? revokedReason,
        string note,
        Guid? grantedBy,
        Money paid,
        IEnumerable<TutoringSession> sessions) =>
        new(id, userId, packageId, packageName, minutesTotal, source, purchaseId, grantedAt,
            expiryMode, expiresAt, revokedAt, revokedReason, note, grantedBy, paid,
            [.. sessions]);

    public int MinutesUsed => _sessions.Sum(s => s.Minutes);

    public int MinutesRemaining => MinutesTotal - MinutesUsed;

    public bool IsRevoked => RevokedAt is not null;

    /// <summary>
    /// Hasta cuándo valen estas horas, dado hasta cuándo le da acceso su suscripción al alumno.
    ///
    /// <paramref name="subscriptionValidUntil"/> es <c>null</c> cuando no hay suscripción que
    /// valga, y también cuando la que hay no caduca (un vitalicio). Los dos casos son
    /// legítimos y opuestos, así que el modo decide qué significa el nulo: en «con la
    /// suscripción» hay que decir además si esa suscripción da acceso ahora mismo, y eso es
    /// justo lo que responde <see cref="HasExpired"/>.
    /// </summary>
    public DateTimeOffset? EffectiveExpiry(DateTimeOffset? subscriptionValidUntil) => ExpiryMode switch
    {
        TutoringExpiry.FixedDate => ExpiresAt,
        TutoringExpiry.WithSubscription => subscriptionValidUntil,
        _ => null
    };

    /// <summary>
    /// Si las horas ya no se pueden gastar.
    ///
    /// En el modo «con la suscripción» la respuesta NO sale de una fecha guardada: sale de si
    /// el alumno tiene ahora mismo acceso por plan. Eso es lo que hace que renovar mantenga las
    /// horas vivas sin que nada tenga que ir a tocarlas, y que dejar de renovar las cierre el
    /// mismo día, sin esperar a que pase ningún proceso.
    /// </summary>
    /// <param name="hasPlanAccess">
    /// Si su plan le da acceso en <paramref name="now"/>. Solo se mira en el modo de
    /// suscripción; en los otros dos da igual lo que valga.
    /// </param>
    public bool HasExpired(DateTimeOffset now, bool hasPlanAccess = false) => ExpiryMode switch
    {
        TutoringExpiry.FixedDate => ExpiresAt is { } expiry && expiry <= now,
        TutoringExpiry.WithSubscription => !hasPlanAccess,
        _ => false
    };

    /// <summary>Concesión de la que todavía se puede gastar: ni revocada, ni caducada, ni agotada.</summary>
    public bool IsUsable(DateTimeOffset now, bool hasPlanAccess = false) =>
        !IsRevoked && !HasExpired(now, hasPlanAccess) && MinutesRemaining > 0;

    /// <summary>
    /// Apunta una tutoría dada contra esta concesión.
    ///
    /// No se permite gastar de más ni siquiera un minuto: si el saldo no llega, el error dice
    /// cuánto queda para que quien lo apunta parta la sesión o conceda más horas. Dejarlo pasar
    /// convertiría el saldo en negativo y la pantalla del alumno mentiría.
    /// </summary>
    public Result<TutoringSession, Error> Consume(
        Guid sessionId,
        int minutes,
        DateTimeOffset occurredAt,
        string topic,
        string notes,
        Guid? recordedBy,
        DateTimeOffset now,
        bool hasPlanAccess = false,
        Guid? tutorId = null)
    {
        if (IsRevoked)
        {
            return Error.Conflict("tutoring_grant.revoked", "Esa bolsa de tutorías está revocada.");
        }

        if (HasExpired(now, hasPlanAccess))
        {
            return Error.Conflict(
                "tutoring_grant.expired",
                ExpiryMode == TutoringExpiry.WithSubscription
                    // El motivo importa: «ha caducado» a secas manda a mirar una fecha que en
                    // este modo no existe, y lo que hay que hacer es renovar.
                    ? "Esas horas iban con la suscripción y ahora mismo no está activa."
                    : "Esa bolsa de tutorías ha caducado.");
        }

        var session = TutoringSession.Create(
            sessionId, Id, minutes, occurredAt, topic, notes, recordedBy, tutorId);
        if (session.IsFailure)
        {
            return session.Error;
        }

        if (minutes > MinutesRemaining)
        {
            return Error.Conflict(
                "tutoring_grant.insufficient_balance",
                $"No hay saldo suficiente: quedan {Format(MinutesRemaining)} y se intentan gastar {Format(minutes)}.");
        }

        _sessions.Add(session.Value);
        return session.Value;
    }

    /// <summary>
    /// Deshace una sesión mal apuntada y devuelve el tiempo al saldo. Es la única forma de
    /// recuperar minutos: corregir a la baja el total concedido dejaría sesiones sin cubrir.
    /// </summary>
    public Result<Unit, Error> RemoveSession(Guid sessionId)
    {
        var session = _sessions.FirstOrDefault(s => s.Id == sessionId);
        if (session is null)
        {
            return Error.NotFound("tutoring_session.not_found", "No existe esa tutoría.");
        }

        _sessions.Remove(session);
        return Unit.Value;
    }

    public Result<Unit, Error> Revoke(string reason, DateTimeOffset now)
    {
        if (IsRevoked)
        {
            return Error.Conflict("tutoring_grant.already_revoked", "Esa bolsa ya estaba revocada.");
        }

        RevokedAt = now;
        RevokedReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        return Unit.Value;
    }

    /// <summary>
    /// Ajusta el total concedido, para corregir un error al teclear o para añadir horas de
    /// cortesía sobre la misma bolsa. Nunca por debajo de lo ya consumido: eso dejaría un saldo
    /// negativo y tutorías dadas que la bolsa no cubre.
    /// </summary>
    public Result<Unit, Error> AdjustTotal(int minutesTotal)
    {
        if (minutesTotal < MinutesUsed)
        {
            return Error.Validation(
                "tutoring_grant.below_used",
                $"No se puede bajar de {Format(MinutesUsed)}, que es lo ya consumido.");
        }

        if (minutesTotal is <= 0 or > TutoringPackage.MaximumMinutes)
        {
            return Error.Validation("tutoring_grant.minutes_invalid", "El total concedido no es válido.");
        }

        MinutesTotal = minutesTotal;
        return Unit.Value;
    }

    /// <summary>"2 h 30 min", para que el error diga algo que un humano lee sin traducir minutos.</summary>
    private static string Format(int minutes) => (minutes / 60, minutes % 60) switch
    {
        (0, var m) => $"{m} min",
        (var h, 0) => $"{h} h",
        var (h, m) => $"{h} h {m} min"
    };
}
