using Inkoova.Academy.Application.Abstractions;
using Inkoova.Academy.Application.Billing;
using Inkoova.Academy.Domain.Billing;

namespace Inkoova.Academy.Application.Tutoring;

/// <summary>
/// Responde una sola pregunta: ¿el plan de este alumno le da acceso ahora mismo?
///
/// Existe porque las tutorías vendidas se apagan y se encienden con la suscripción, y esa
/// pregunta hay que hacerla en cuatro sitios distintos (su cuenta, la ficha del panel, apuntar
/// una tutoría, el resumen). Repetirla a mano en cada uno acabaría con cuatro reglas
/// ligeramente distintas y con un alumno al que la cuenta le dice una cosa y el panel otra.
///
/// Dos casos, y los dos importan:
///
/// - **Suscripción.** Se usa <see cref="Subscription.GrantsAccessAt"/>, la MISMA regla que
///   abre los cursos, con el mismo periodo de gracia. Si la plataforma sigue dejando estudiar
///   durante los días de cortesía de un recibo devuelto, las tutorías no se pueden apagar
///   antes: sería cortar por un lado lo que se mantiene por el otro.
/// - **Vitalicio.** No es una suscripción, es una compra: no hay fila en `subscription` y sí un
///   entitlement sin fecha de fin. Sin este caso, quien compró el programa para siempre vería
///   sus horas caducadas al día siguiente.
/// </summary>
public sealed class PlanAccessProbe(
    ISubscriptionRepository subscriptions,
    IEntitlementRepository entitlements,
    BillingOptions billing)
{
    public async Task<PlanAccess> GetAsync(Guid userId, DateTimeOffset now, CancellationToken ct)
    {
        var subscription = await subscriptions.GetActiveForUserAsync(userId, ct);

        if (subscription is not null)
        {
            return new PlanAccess(
                subscription.GrantsAccessAt(now, billing.GracePeriod),
                subscription.AccessValidUntil(billing.GracePeriod));
        }

        // Sin suscripción: solo un derecho comprado y sin caducidad mantiene vivas las horas.
        // Los entitlements de plan (`plan_included`) no cuentan aquí: si existieran sin
        // suscripción sería porque el job de retirada aún no ha pasado, y eso es un retraso del
        // sistema, no un derecho del alumno.
        var owned = await entitlements.GetBySourceAsync(userId, EntitlementSource.Purchase, ct);
        var lifetime = owned.Any(e => e.ValidUntil is null && e.IsValidAt(now));

        // Vitalicio: da acceso y no tiene fecha de fin que enseñar.
        return new PlanAccess(lifetime, null);
    }
}

/// <summary>
/// Si el plan da acceso ahora y hasta cuándo lo garantiza.
///
/// <paramref name="ValidUntil"/> es <c>null</c> tanto cuando no hay nada como cuando no caduca,
/// así que nunca se lee solo: se lee junto a <paramref name="HasAccess"/>.
/// </summary>
public sealed record PlanAccess(bool HasAccess, DateTimeOffset? ValidUntil);
