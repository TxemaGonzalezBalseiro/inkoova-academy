using Inkoova.Academy.Domain.Common;
using Inkoova.Academy.Domain.ValueObjects;

namespace Inkoova.Academy.Domain.Tutoring;

/// <summary>
/// Quien da las tutorías.
///
/// No tiene por qué ser un usuario de la plataforma: se le puede pagar a alguien externo que
/// nunca entra en la academia. Por eso el usuario es opcional y lo que identifica al profesor
/// es su correo, que es además por donde se le convoca y se le liquida.
/// </summary>
public sealed class Tutor
{
    public Guid Id { get; private set; }

    /// <summary>Su cuenta en la plataforma, si la tiene. <c>null</c> para un profesor externo.</summary>
    public Guid? UserId { get; private set; }

    public string DisplayName { get; private set; }
    public string Email { get; private set; }
    public string Bio { get; private set; }

    /// <summary>
    /// Qué porcentaje se lleva de lo que el alumno pagó por las horas que imparte.
    ///
    /// Sobre lo PAGADO, no sobre el precio de catálogo: una bolsa concedida a mano —una beca,
    /// una cortesía— no generó ingreso, y repartir un porcentaje de un ingreso inexistente es
    /// pagar de la propia caja sin enterarse.
    /// </summary>
    public decimal CommissionPercent { get; private set; }

    public bool IsActive { get; private set; }

    private Tutor(
        Guid id,
        Guid? userId,
        string displayName,
        string email,
        string bio,
        decimal commissionPercent,
        bool isActive)
    {
        Id = id;
        UserId = userId;
        DisplayName = displayName;
        Email = email;
        Bio = bio;
        CommissionPercent = commissionPercent;
        IsActive = isActive;
    }

    public static Result<Tutor, Error> Create(
        Guid id,
        Guid? userId,
        string displayName,
        string email,
        string bio,
        decimal commissionPercent)
    {
        var validated = Validate(displayName, email, commissionPercent);
        if (validated is not null)
        {
            return validated;
        }

        return new Tutor(
            id, userId, displayName.Trim(), email.Trim().ToLowerInvariant(),
            (bio ?? string.Empty).Trim(), commissionPercent, isActive: true);
    }

    public static Tutor Rehydrate(
        Guid id,
        Guid? userId,
        string displayName,
        string email,
        string bio,
        decimal commissionPercent,
        bool isActive) =>
        new(id, userId, displayName, email, bio, commissionPercent, isActive);

    /// <summary>
    /// Edita la ficha. Cambiar el porcentaje NO toca lo ya devengado: cada tutoría guardó el
    /// porcentaje con el que se calculó, para que subir la comisión no reescriba lo que se debía
    /// del mes pasado.
    /// </summary>
    public Result<Unit, Error> Describe(
        string displayName, string email, string bio, decimal commissionPercent)
    {
        var validated = Validate(displayName, email, commissionPercent);
        if (validated is not null)
        {
            return validated;
        }

        DisplayName = displayName.Trim();
        Email = email.Trim().ToLowerInvariant();
        Bio = (bio ?? string.Empty).Trim();
        CommissionPercent = commissionPercent;

        return Unit.Value;
    }

    /// <summary>Se desactiva, no se borra: lo que dio y lo que se le debe tiene que seguir ahí.</summary>
    public void SetActive(bool active) => IsActive = active;

    public void LinkUser(Guid? userId) => UserId = userId;

    /// <summary>
    /// Lo que se lleva por una tutoría, dado lo que el alumno pagó por esas horas.
    ///
    /// Se redondea al céntimo hacia arriba desde la mitad, igual que las comisiones de
    /// afiliado: si dos partes del sistema redondearan distinto, dos cifras que deberían
    /// cuadrar dejarían de hacerlo por un céntimo, que es de lo más caro de perseguir.
    /// </summary>
    public Money Earn(Money paidForTheseMinutes)
    {
        var cents = (long)Math.Round(
            paidForTheseMinutes.AmountInCents * CommissionPercent / 100m,
            MidpointRounding.AwayFromZero);

        return Money.Create(cents, paidForTheseMinutes.Currency).Value;
    }

    private static Error? Validate(string displayName, string email, decimal commissionPercent)
    {
        if (string.IsNullOrWhiteSpace(displayName) || displayName.Trim().Length > 120)
        {
            return Error.Validation("tutor.name_invalid", "El profesor necesita un nombre de hasta 120 caracteres.");
        }

        var trimmed = (email ?? string.Empty).Trim();
        if (trimmed.Length == 0 || !trimmed.Contains('@') || trimmed.Contains(' '))
        {
            return Error.Validation("tutor.email_invalid", "El correo del profesor no es válido.");
        }

        if (commissionPercent is < 0 or > 100)
        {
            return Error.Validation("tutor.percent_invalid", "El porcentaje debe estar entre 0 y 100.");
        }

        return null;
    }
}

/// <summary>
/// Lo que se le debe a un profesor por una tutoría concreta.
///
/// Guarda la base y el porcentaje con los que se calculó, no solo el importe. Es lo que permite
/// explicar una liquidación tres meses después —«de dónde sale esta cifra»— y lo que hace que
/// cambiar la comisión de un profesor no reescriba lo que ya se le debía.
/// </summary>
public sealed class TutorEarning
{
    public Guid Id { get; private set; }
    public Guid TutorId { get; private set; }
    public Guid SessionId { get; private set; }

    /// <summary>La parte de lo pagado que corresponde a los minutos de esta tutoría.</summary>
    public Money Base { get; private set; }

    public decimal Percent { get; private set; }
    public Money Amount { get; private set; }
    public bool IsPaid { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? PaidAt { get; private set; }
    public string PayoutNote { get; private set; }

    private TutorEarning(
        Guid id,
        Guid tutorId,
        Guid sessionId,
        Money @base,
        decimal percent,
        Money amount,
        bool isPaid,
        DateTimeOffset createdAt,
        DateTimeOffset? paidAt,
        string payoutNote)
    {
        Id = id;
        TutorId = tutorId;
        SessionId = sessionId;
        Base = @base;
        Percent = percent;
        Amount = amount;
        IsPaid = isPaid;
        CreatedAt = createdAt;
        PaidAt = paidAt;
        PayoutNote = payoutNote;
    }

    /// <summary>
    /// Devenga lo de una tutoría.
    ///
    /// <paramref name="paidForTheBag"/> es lo que el alumno pagó por la bolsa entera, y el
    /// reparto es por minutos: una sesión de 60 minutos de una bolsa de 300 devenga sobre un
    /// quinto. Con una bolsa manual —pagada a cero— la base es cero y el importe también, que
    /// es lo correcto: no hubo ingreso del que repartir.
    /// </summary>
    public static TutorEarning Accrue(
        Guid id,
        Tutor tutor,
        Guid sessionId,
        Money paidForTheBag,
        int bagMinutes,
        int sessionMinutes,
        DateTimeOffset now)
    {
        var share = bagMinutes <= 0
            ? 0L
            : (long)Math.Round(
                paidForTheBag.AmountInCents * (decimal)sessionMinutes / bagMinutes,
                MidpointRounding.AwayFromZero);

        var @base = Money.Create(share, paidForTheBag.Currency).Value;

        return new TutorEarning(
            id, tutor.Id, sessionId, @base, tutor.CommissionPercent, tutor.Earn(@base),
            isPaid: false, now, paidAt: null, string.Empty);
    }

    public static TutorEarning Rehydrate(
        Guid id,
        Guid tutorId,
        Guid sessionId,
        Money @base,
        decimal percent,
        Money amount,
        bool isPaid,
        DateTimeOffset createdAt,
        DateTimeOffset? paidAt,
        string payoutNote) =>
        new(id, tutorId, sessionId, @base, percent, amount, isPaid, createdAt, paidAt, payoutNote);

    /// <summary>
    /// Marca lo devengado como pagado. No se puede pagar dos veces: la segunda llamada falla en
    /// vez de sobrescribir la fecha, que es como se pierde el rastro de un pago duplicado.
    /// </summary>
    public Result<Unit, Error> MarkPaid(DateTimeOffset now, string note)
    {
        if (IsPaid)
        {
            return Error.Conflict("tutor_earning.already_paid", "Eso ya estaba pagado.");
        }

        IsPaid = true;
        PaidAt = now;
        PayoutNote = (note ?? string.Empty).Trim();

        return Unit.Value;
    }
}
