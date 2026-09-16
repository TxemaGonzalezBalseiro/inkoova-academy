using Inkoova.Academy.Domain.Common;
using Inkoova.Academy.Domain.ValueObjects;

namespace Inkoova.Academy.Domain.Tutoring;

/// <summary>
/// Un paquete de tutorías del catálogo: "5 tutorías de una hora, 450 €". Lo edita el negocio
/// desde administración, igual que los planes, así que nada de esto está escrito en el código.
///
/// El tiempo se cuenta en MINUTOS, no en horas. Una tutoría de hora y media es 90, un número
/// exacto que suma y resta sin error; en horas sería 1,5 y el saldo dependería de la coma
/// flotante. La interfaz enseña horas y aquí dentro siempre hay enteros.
/// </summary>
public sealed class TutoringPackage
{
    /// <summary>Tope defensivo: 200 horas por paquete. Más que eso es un cero de más al teclear.</summary>
    public const int MaximumMinutes = 200 * 60;

    public Guid Id { get; private set; }
    public string Slug { get; private set; }
    public string Name { get; private set; }
    public string Description { get; private set; }
    public int Minutes { get; private set; }
    public Money Price { get; private set; }

    /// <summary>Cómo caducan las horas que salgan de este paquete.</summary>
    public TutoringExpiry ExpiryMode { get; private set; }

    /// <summary>
    /// Días de validez desde la concesión. Solo se usa cuando <see cref="ExpiryMode"/> es
    /// <see cref="TutoringExpiry.FixedDate"/>; en los otros modos la fecha no la pone el
    /// paquete.
    /// </summary>
    public int? ValidityDays { get; private set; }

    public int DisplayOrder { get; private set; }
    public bool IsActive { get; private set; }

    /// <summary>
    /// Precio de Stripe con el que se cobra este paquete. <c>null</c> mientras no se haya
    /// sincronizado, y también en cuanto cambia el importe: en Stripe los precios son
    /// inmutables, así que un importe nuevo necesita un precio nuevo.
    /// </summary>
    public string? StripePriceId { get; private set; }

    private TutoringPackage(
        Guid id,
        string slug,
        string name,
        string description,
        int minutes,
        Money price,
        TutoringExpiry expiryMode,
        int? validityDays,
        int displayOrder,
        bool isActive,
        string? stripePriceId)
    {
        StripePriceId = stripePriceId;
        ExpiryMode = expiryMode;
        Id = id;
        Slug = slug;
        Name = name;
        Description = description;
        Minutes = minutes;
        Price = price;
        ValidityDays = validityDays;
        DisplayOrder = displayOrder;
        IsActive = isActive;
    }

    public static Result<TutoringPackage, Error> Create(
        Guid id,
        string slug,
        string name,
        string description,
        int minutes,
        Money price,
        TutoringExpiry expiryMode,
        int? validityDays,
        int displayOrder)
    {
        var parsedSlug = ValueObjects.Slug.Create(slug);
        if (parsedSlug.IsFailure)
        {
            return Error.Validation("tutoring_package.slug_invalid", "El identificador del paquete no es válido.");
        }

        var validated = Validate(name, minutes, price, expiryMode, validityDays);
        if (validated is not null)
        {
            return validated;
        }

        return new TutoringPackage(
            id, parsedSlug.Value.Value, name.Trim(), (description ?? string.Empty).Trim(),
            minutes, price, expiryMode, Normalise(expiryMode, validityDays), displayOrder,
            isActive: true, stripePriceId: null);
    }

    public static TutoringPackage Rehydrate(
        Guid id,
        string slug,
        string name,
        string description,
        int minutes,
        Money price,
        TutoringExpiry expiryMode,
        int? validityDays,
        int displayOrder,
        bool isActive,
        string? stripePriceId) =>
        new(id, slug, name, description, minutes, price, expiryMode, validityDays, displayOrder,
            isActive, stripePriceId);

    /// <summary>
    /// Los días de validez solo significan algo con caducidad por fecha. Guardarlos en los otros
    /// modos deja un número que nadie usa y que alguien acabará leyendo como si mandara.
    /// </summary>
    private static int? Normalise(TutoringExpiry expiryMode, int? validityDays) =>
        expiryMode == TutoringExpiry.FixedDate ? validityDays : null;

    public void LinkStripePrice(string stripePriceId) => StripePriceId = stripePriceId;

    /// <summary>
    /// Edición desde el panel. El slug no se toca: es la referencia estable del paquete.
    ///
    /// Cambiar los minutos o el precio NO afecta a lo ya concedido. Cada concesión copió su
    /// nombre y sus minutos al crearse, precisamente para que editar el catálogo no reescriba
    /// lo que un alumno compró el mes pasado.
    /// </summary>
    public Result<Unit, Error> Describe(
        string name,
        string description,
        int minutes,
        Money price,
        TutoringExpiry expiryMode,
        int? validityDays,
        int displayOrder)
    {
        var validated = Validate(name, minutes, price, expiryMode, validityDays);
        if (validated is not null)
        {
            return validated;
        }

        // El precio de Stripe es inmutable: no se puede editar el importe de uno ya creado. Al
        // cambiarlo aquí se suelta el enlace para que la sincronización cree el precio nuevo.
        // Quien ya compró conserva lo que pagó, que es un cobro cerrado en Stripe.
        if (price.AmountInCents != Price.AmountInCents || price.Currency != Price.Currency)
        {
            StripePriceId = null;
        }

        Name = name.Trim();
        Description = (description ?? string.Empty).Trim();
        Minutes = minutes;
        Price = price;
        ExpiryMode = expiryMode;
        ValidityDays = Normalise(expiryMode, validityDays);
        DisplayOrder = displayOrder;

        return Unit.Value;
    }

    public void Activate() => IsActive = true;

    /// <summary>
    /// Se retira del catálogo, no se borra. Las concesiones que salieron de él apuntan aquí
    /// para poder explicar de dónde vinieron esas horas.
    /// </summary>
    public void Deactivate() => IsActive = false;

    private static Error? Validate(
        string name,
        int minutes,
        Money price,
        TutoringExpiry expiryMode,
        int? validityDays)
    {
        // Un paquete que dice caducar por fecha y no trae días no caduca nunca, pero lo anuncia
        // como si caducara. Para eso está el modo `Never`, que lo dice de frente.
        if (expiryMode == TutoringExpiry.FixedDate && validityDays is null)
        {
            return Error.Validation(
                "tutoring_package.validity_required",
                "Con caducidad por fecha hay que decir cuántos días vale.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return Error.Validation("tutoring_package.name_empty", "El paquete necesita un nombre.");
        }

        if (name.Trim().Length > 160)
        {
            return Error.Validation("tutoring_package.name_too_long", "El nombre no puede pasar de 160 caracteres.");
        }

        if (minutes <= 0)
        {
            return Error.Validation("tutoring_package.minutes_invalid", "El paquete debe incluir tiempo.");
        }

        if (minutes > MaximumMinutes)
        {
            return Error.Validation(
                "tutoring_package.minutes_too_large",
                $"Un paquete no puede pasar de {MaximumMinutes / 60} horas.");
        }

        // Cero es válido a propósito: un paquete de cortesía tiene importe cero y horas reales.
        if (price.AmountInCents < 0)
        {
            return Error.Validation("tutoring_package.price_invalid", "El importe no puede ser negativo.");
        }

        if (validityDays is <= 0)
        {
            return Error.Validation("tutoring_package.validity_invalid", "La validez debe ser de al menos un día.");
        }

        return null;
    }
}
