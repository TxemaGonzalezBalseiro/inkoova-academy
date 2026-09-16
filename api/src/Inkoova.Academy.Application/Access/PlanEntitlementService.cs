using Inkoova.Academy.Application.Abstractions;
using Inkoova.Academy.Domain.Billing;
using Inkoova.Academy.Domain.Catalog;
using Inkoova.Academy.Domain.Common;

namespace Inkoova.Academy.Application.Access;

/// <summary>
/// Convierte «el plan está pagado hasta X» en derechos de acceso.
///
/// Lo que incluye el plan ya no está escrito aquí: sale del propio plan, que lleva dos
/// interruptores —todos los cursos, todos los packs— y una lista de productos sueltos. Antes
/// esto concedía el catálogo entero a cualquier plan, así que no había forma de vender uno
/// barato con dos cursos sin cambiar este fichero y desplegar.
/// </summary>
public sealed class PlanEntitlementService(
    EntitlementService entitlements,
    IProductCatalog productCatalog)
{
    /// <summary>
    /// Aplica el plan: concede lo que incluye y **retira lo que ya no**.
    ///
    /// Las dos mitades importan. Sin la segunda, quitarle un curso a un plan no tendría ningún
    /// efecto sobre quien ya lo tenía: el derecho seguiría ahí, renovándose con cada pago, y el
    /// panel diría una cosa mientras el alumno ve otra. Solo se retira lo derivado del plan; una
    /// compra suelta del mismo curso sobrevive, que es lo que hace <c>RevokePlanDerivedAsync</c>.
    /// </summary>
    public async Task<Result<Unit, Error>> ApplyAsync(
        Guid userId,
        Plan plan,
        DateTimeOffset validUntil,
        CancellationToken ct)
    {
        var (included, everything) = await ResolveAsync(plan, ct);

        foreach (var product in everything)
        {
            var result = included.Contains(product.Id)
                ? await entitlements.GrantFromPlanAsync(userId, product.Id, validUntil, ct)
                : await entitlements.RevokePlanDerivedAsync(
                    userId, product.Id, $"Ya no lo incluye el plan {plan.Code}.", ct);

            if (result.IsFailure)
            {
                return result.Error;
            }
        }

        return Unit.Value;
    }

    /// <summary>
    /// Acceso vitalicio: el mismo conjunto, sin caducidad. Se modela como compra para que el job
    /// diario de revocación no lo toque nunca.
    ///
    /// Aquí NO se retira nada: lo vitalicio ya cobrado no se estrecha porque después se edite el
    /// plan. Quien pagó por «los cuatro cursos para siempre» los conserva aunque mañana el plan
    /// se venda con tres.
    /// </summary>
    public async Task<Result<Unit, Error>> ApplyLifetimeAsync(
        Guid userId, Plan plan, CancellationToken ct)
    {
        var (included, everything) = await ResolveAsync(plan, ct);

        foreach (var product in everything.Where(p => included.Contains(p.Id)))
        {
            var granted = await entitlements.GrantFromPurchaseAsync(userId, product.Id, ct);
            if (granted.IsFailure)
            {
                return granted.Error;
            }
        }

        return Unit.Value;
    }

    /// <summary>
    /// Retira el acceso derivado del plan cuando se cancela la suscripción.
    ///
    /// Recorre TODO el catálogo y no solo lo que el plan incluye hoy: si el plan se editó
    /// después de concederse, lo que sobra son justo los productos que ya no están en la lista,
    /// y son los que se quedarían colgados para siempre.
    /// </summary>
    public async Task<Result<Unit, Error>> WithdrawAsync(Guid userId, string reason, CancellationToken ct)
    {
        foreach (var product in await AllProductsAsync(ct))
        {
            var revoked = await entitlements.RevokePlanDerivedAsync(userId, product.Id, reason, ct);
            if (revoked.IsFailure)
            {
                return revoked.Error;
            }
        }

        return Unit.Value;
    }

    /// <summary>
    /// Qué productos incluye el plan, y contra qué universo se compara.
    ///
    /// El universo es el catálogo entero porque hay que recorrerlo para saber qué **no** entra:
    /// mirar solo lo incluido no permite retirar lo que se quitó del plan.
    /// </summary>
    private async Task<(HashSet<Guid> Included, IReadOnlyList<Product> Everything)> ResolveAsync(
        Plan plan, CancellationToken ct)
    {
        var courses = await productCatalog.GetAllCourseProductsAsync(ct);
        var packs = await productCatalog.GetAllPackProductsAsync(ct);

        var included = new HashSet<Guid>();

        if (plan.IncludesAllCourses)
        {
            included.UnionWith(courses.Select(p => p.Id));
        }

        if (plan.IncludesAllPacks)
        {
            included.UnionWith(packs.Select(p => p.Id));
        }

        // La lista suelta se suma a los interruptores en vez de sustituirlos: un plan puede dar
        // todos los cursos y además un pack concreto, sin dar los seis.
        included.UnionWith(plan.IncludedProductIds);

        return (included, [.. courses, .. packs]);
    }

    private async Task<IReadOnlyList<Product>> AllProductsAsync(CancellationToken ct) =>
    [
        .. await productCatalog.GetAllCourseProductsAsync(ct),
        .. await productCatalog.GetAllPackProductsAsync(ct)
    ];
}
