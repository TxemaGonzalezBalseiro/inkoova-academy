using System.Text.Json;
using Inkoova.Academy.Domain.Common;
using Inkoova.Academy.Domain.ValueObjects;

namespace Inkoova.Academy.Domain.Billing;

/// <summary>Billing cadence of a plan. Lifetime is a one-off payment, not a subscription.</summary>
public enum BillingInterval
{
    Monthly,
    Quarterly,
    Biannual,
    Yearly,
    Lifetime
}

/// <summary>
/// Subscription plan. Prices are editable from admin, so nothing here is hardcoded except
/// the codes, which are referenced by Discord roles and by the packs-included rule.
/// </summary>
public sealed class Plan
{
    public const string Monthly = "monthly";
    public const string Quarterly = "quarterly";
    public const string Biannual = "biannual";
    public const string Yearly = "yearly";
    public const string Lifetime = "lifetime";

    public Guid Id { get; private set; }
    public string Code { get; private set; }
    public string Name { get; private set; }
    public BillingInterval Interval { get; private set; }
    public Money Price { get; private set; }

    /// <summary>Free-form benefits rendered on the pricing card. JSON so admin can edit copy without a migration.</summary>
    public string BenefitsJson { get; private set; }

    public string? StripePriceId { get; private set; }

    /// <summary>
    /// Qué productos entra en el plan.
    ///
    /// Son dos interruptores y una lista, y no solo la lista, porque «todo el catálogo» y «estos
    /// tres cursos» se configuran igual pero se comportan distinto **el día que se publica algo
    /// nuevo**: el primero lo incluye solo, el segundo no. Con solo la lista, publicar un curso
    /// se lo estaría quitando en silencio a los suscriptores anuales hasta que alguien se
    /// acordara de repasar plan por plan, y nada avisaría de ello.
    ///
    /// Lo que concede el plan es la unión de los tres: todos los cursos si su interruptor está
    /// puesto, todos los packs si lo está el suyo, y además lo que haya marcado en la lista.
    /// </summary>
    public bool IncludesAllCourses { get; private set; }

    public bool IncludesAllPacks { get; private set; }

    /// <summary>
    /// Productos marcados uno a uno. Vacío en un plan que lo incluye todo por interruptor, y es
    /// lo único que se mira cuando el interruptor de esa familia está quitado.
    /// </summary>
    public IReadOnlyList<Guid> IncludedProductIds { get; private set; } = [];

    public int DisplayOrder { get; private set; }
    public bool IsActive { get; private set; }

    private Plan(
        Guid id,
        string code,
        string name,
        BillingInterval interval,
        Money price,
        string benefitsJson,
        string? stripePriceId,
        bool includesAllCourses,
        bool includesAllPacks,
        IReadOnlyList<Guid> includedProductIds,
        int displayOrder,
        bool isActive)
    {
        Id = id;
        Code = code;
        Name = name;
        Interval = interval;
        Price = price;
        BenefitsJson = benefitsJson;
        StripePriceId = stripePriceId;
        IncludesAllCourses = includesAllCourses;
        IncludesAllPacks = includesAllPacks;
        IncludedProductIds = includedProductIds;
        DisplayOrder = displayOrder;
        IsActive = isActive;
    }

    public static Result<Plan, Error> Create(
        Guid id,
        string code,
        string name,
        BillingInterval interval,
        Money price,
        IReadOnlyList<string> benefits,
        bool includesAllCourses,
        bool includesAllPacks,
        IReadOnlyList<Guid> includedProductIds,
        int displayOrder)
    {
        var slug = Slug.Create(code);
        if (slug.IsFailure)
        {
            return Error.Validation("plan.code_invalid", "El código del plan debe ser un slug válido.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return Error.Validation("plan.name_empty", "El plan necesita un nombre.");
        }

        if (price.AmountInCents <= 0)
        {
            return Error.Validation("plan.price_invalid", "El precio del plan debe ser mayor que cero.");
        }

        var inclusions = ValidateInclusions(includesAllCourses, includesAllPacks, includedProductIds);
        if (inclusions is { } problem)
        {
            return problem;
        }

        return new Plan(
            id, slug.Value.Value, name.Trim(), interval, price,
            JsonSerializer.Serialize(benefits), stripePriceId: null,
            includesAllCourses, includesAllPacks, Normalize(includedProductIds),
            displayOrder, isActive: true);
    }

    public static Plan Rehydrate(
        Guid id,
        string code,
        string name,
        BillingInterval interval,
        Money price,
        string benefitsJson,
        string? stripePriceId,
        bool includesAllCourses,
        bool includesAllPacks,
        IReadOnlyList<Guid> includedProductIds,
        int displayOrder,
        bool isActive) =>
        new(id, code, name, interval, price, benefitsJson, stripePriceId,
            includesAllCourses, includesAllPacks, includedProductIds, displayOrder, isActive);

    /// <summary>
    /// Un plan que no da nada no es un plan: es un cobro sin contraprestación. Se para aquí y no
    /// en el panel porque el panel no es el único que crea planes —está la siembra inicial— y
    /// porque el fallo no se ve hasta que alguien paga y se queda sin acceso.
    /// </summary>
    private static Error? ValidateInclusions(
        bool includesAllCourses, bool includesAllPacks, IReadOnlyList<Guid> includedProductIds) =>
        !includesAllCourses && !includesAllPacks && Normalize(includedProductIds).Count == 0
            ? Error.Validation(
                "plan.includes_nothing",
                "El plan no incluye nada: marca todo el catálogo, todos los packs, o al menos un producto.")
            : null;

    /// <summary>Sin repetidos y sin el id vacío, que es lo que llega de una casilla sin resolver.</summary>
    private static IReadOnlyList<Guid> Normalize(IReadOnlyList<Guid> productIds) =>
        [.. productIds.Where(id => id != Guid.Empty).Distinct()];

    public IReadOnlyList<string> Benefits =>
        JsonSerializer.Deserialize<List<string>>(BenefitsJson) ?? [];

    public void LinkStripePrice(string stripePriceId) => StripePriceId = stripePriceId;

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;

    /// <summary>
    /// Edición del plan desde el panel. El código y el intervalo NO se tocan: el código es la
    /// referencia que viaja en los metadatos de Stripe y en los roles de Discord, y el
    /// intervalo define el periodo de las suscripciones que ya están vivas.
    ///
    /// Cambiar el precio desuscribe el precio de Stripe: allí los precios son inmutables, así
    /// que hace falta uno nuevo. Devuelve <c>true</c> cuando eso ocurre, para que quien llama
    /// pueda avisar de que hay que sincronizar antes de vender al precio nuevo.
    /// </summary>
    public Result<bool, Error> Describe(
        string name,
        Money price,
        IReadOnlyList<string> benefits,
        bool includesAllCourses,
        bool includesAllPacks,
        IReadOnlyList<Guid> includedProductIds,
        int displayOrder)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Error.Validation("plan.name_empty", "El plan necesita un nombre.");
        }

        if (price.AmountInCents <= 0)
        {
            return Error.Validation("plan.price_invalid", "El precio del plan debe ser mayor que cero.");
        }

        if (benefits.Any(string.IsNullOrWhiteSpace))
        {
            return Error.Validation("plan.benefit_empty", "Un beneficio del plan no puede estar vacío.");
        }

        var inclusions = ValidateInclusions(includesAllCourses, includesAllPacks, includedProductIds);
        if (inclusions is { } problem)
        {
            return problem;
        }

        var priceChanged = price.AmountInCents != Price.AmountInCents;

        Name = name.Trim();
        Price = price;
        BenefitsJson = JsonSerializer.Serialize(benefits);
        IncludesAllCourses = includesAllCourses;
        IncludesAllPacks = includesAllPacks;
        IncludedProductIds = Normalize(includedProductIds);
        DisplayOrder = displayOrder;

        if (priceChanged)
        {
            // Quien ya está suscrito conserva el precio que contrató: eso lo decide Stripe con
            // el precio al que se suscribió, no este campo.
            StripePriceId = null;
        }

        return priceChanged;
    }

    /// <summary>How long one billing period lasts. Lifetime never expires, hence null.</summary>
    public TimeSpan? PeriodLength => Interval switch
    {
        BillingInterval.Monthly => TimeSpan.FromDays(30),
        BillingInterval.Quarterly => TimeSpan.FromDays(91),
        BillingInterval.Biannual => TimeSpan.FromDays(182),
        BillingInterval.Yearly => TimeSpan.FromDays(365),
        BillingInterval.Lifetime => null,
        _ => null
    };

    /// <summary>Lifetime is sold through a one-off Checkout, not a Stripe subscription.</summary>
    public bool IsRecurring => Interval != BillingInterval.Lifetime;
}
