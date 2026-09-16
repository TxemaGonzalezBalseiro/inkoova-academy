using Inkoova.Academy.Application.Abstractions;
using Inkoova.Academy.Domain.Common;
using Inkoova.Academy.Domain.Tutoring;
using Inkoova.Academy.Domain.ValueObjects;

namespace Inkoova.Academy.Application.Tutoring;

/// <summary>
/// Traducción de entidad a DTO. Está en un sitio y no repartida por los handlers porque el
/// saldo se calcula aquí: si cada handler lo montara a su manera, uno acabaría restando mal.
/// </summary>
internal static class TutoringMapping
{
    /// <summary>
    /// El modo tal y como viaja por la API. Lo desconocido cae en «con la suscripción», que es
    /// lo que se vende: caer en «no caduca nunca» ante un valor raro regalaría horas para
    /// siempre por una errata.
    /// </summary>
    public static TutoringExpiry ParseExpiry(string? value) => value switch
    {
        "fixed" => TutoringExpiry.FixedDate,
        "never" => TutoringExpiry.Never,
        _ => TutoringExpiry.WithSubscription
    };

    public static string ExpiryText(TutoringExpiry mode) => mode switch
    {
        TutoringExpiry.FixedDate => "fixed",
        TutoringExpiry.Never => "never",
        _ => "subscription"
    };

    public static TutoringPackageDto ToDto(this TutoringPackage p) =>
        new(p.Id, p.Slug, p.Name, p.Description, p.Minutes, p.Price.AmountInCents,
            p.Price.Currency, ExpiryText(p.ExpiryMode), p.ValidityDays, p.DisplayOrder,
            p.IsActive, p.StripePriceId);

    /// <param name="plan">
    /// El acceso por plan del alumno. Es lo que decide si una bolsa atada a la suscripción está
    /// viva, así que no tiene valor por defecto: quien traduzca una bolsa tiene que haberlo
    /// mirado. Un `false` implícito habría dejado a todo el mundo con las horas caducadas.
    /// </param>
    /// <param name="tutorNames">
    /// Nombres de los profesores por identificador. Opcional: cuando no se pasa, el
    /// histórico sale sin nombres en vez de fallar, que es preferible a que una pantalla
    /// entera se caiga porque un profesor se borró.
    /// </param>
    public static TutoringGrantDto ToDto(
        this TutoringGrant g,
        DateTimeOffset now,
        PlanAccess plan,
        IReadOnlyDictionary<Guid, string>? tutorNames = null,
        IReadOnlyList<Domain.Tutoring.TutoringAppointment>? appointments = null) =>
        new(g.Id,
            g.PackageId,
            g.PackageName,
            g.MinutesTotal,
            g.MinutesUsed,
            g.MinutesRemaining,
            g.Source == TutoringGrantSource.Purchase ? "purchase" : "manual",
            g.GrantedAt,
            ExpiryText(g.ExpiryMode),
            g.EffectiveExpiry(plan.ValidUntil),
            g.HasExpired(now, plan.HasAccess),
            g.IsRevoked,
            g.RevokedReason,
            g.Note,
            [.. g.Sessions
                .OrderByDescending(s => s.OccurredAt)
                .Select(s => new TutoringSessionDto(
                    s.Id, s.Minutes, s.OccurredAt, s.Topic, s.Notes, s.TutorId,
                    s.TutorId is { } tutorId && tutorNames?.TryGetValue(tutorId, out var name) == true
                        ? name
                        : null))],
            [.. (appointments ?? [])
                .Where(a => a.GrantId == g.Id)
                .OrderBy(a => a.StartsAt)
                .Select(a => a.ToDto(tutorNames))]);
}

// ── catálogo (admin) ──────────────────────────────────────────────────────────────────────

public sealed record SaveTutoringPackageRequest(
    Guid? Id,
    string Slug,
    string Name,
    string Description,
    int Minutes,
    long PriceCents,
    string Currency,
    /// <summary>'fixed', 'subscription' o 'never'. Ver <see cref="TutoringExpiry"/>.</summary>
    string ExpiryMode,
    int? ValidityDays,
    int DisplayOrder);

public sealed class ListTutoringPackagesHandler(ITutoringPackageRepository packages)
{
    /// <param name="onlyActive">El alumno solo ve lo que está a la venta; el panel lo ve todo.</param>
    public async Task<IReadOnlyList<TutoringPackageDto>> HandleAsync(bool onlyActive, CancellationToken ct)
    {
        var all = onlyActive
            ? await packages.GetActiveAsync(ct)
            : await packages.GetAllAsync(ct);

        return [.. all.Select(p => p.ToDto())];
    }
}

public sealed class SaveTutoringPackageHandler(ITutoringPackageRepository packages)
{
    public async Task<Result<TutoringPackageDto, Error>> HandleAsync(
        SaveTutoringPackageRequest request, CancellationToken ct)
    {
        var price = Money.Create(request.PriceCents, string.IsNullOrWhiteSpace(request.Currency) ? "EUR" : request.Currency);
        if (price.IsFailure)
        {
            return price.Error;
        }

        if (request.Id is { } id)
        {
            var existing = await packages.GetByIdAsync(id, ct);
            if (existing is null)
            {
                return Error.NotFound("tutoring_package.not_found", "No existe ese paquete.");
            }

            var described = existing.Describe(
                request.Name, request.Description, request.Minutes,
                price.Value, TutoringMapping.ParseExpiry(request.ExpiryMode), request.ValidityDays, request.DisplayOrder);

            if (described.IsFailure)
            {
                return described.Error;
            }

            await packages.UpsertAsync(existing, ct);
            return existing.ToDto();
        }

        // El slug es único en la base. Comprobarlo antes convierte un 500 por violación de
        // índice en un mensaje que dice qué pasa.
        var clash = await packages.GetBySlugAsync(request.Slug, ct);
        if (clash is not null)
        {
            return Error.Conflict("tutoring_package.slug_taken", "Ya hay un paquete con ese identificador.");
        }

        var created = TutoringPackage.Create(
            Guid.CreateVersion7(), request.Slug, request.Name, request.Description,
            request.Minutes, price.Value, TutoringMapping.ParseExpiry(request.ExpiryMode),
            request.ValidityDays, request.DisplayOrder);

        if (created.IsFailure)
        {
            return created.Error;
        }

        await packages.UpsertAsync(created.Value, ct);
        return created.Value.ToDto();
    }
}

public sealed class SetTutoringPackageActiveHandler(ITutoringPackageRepository packages)
{
    public async Task<Result<Unit, Error>> HandleAsync(Guid id, bool active, CancellationToken ct)
    {
        var package = await packages.GetByIdAsync(id, ct);
        if (package is null)
        {
            return Error.NotFound("tutoring_package.not_found", "No existe ese paquete.");
        }

        if (active)
        {
            package.Activate();
        }
        else
        {
            package.Deactivate();
        }

        await packages.UpsertAsync(package, ct);
        return Unit.Value;
    }
}

/// <summary>
/// Crea en Stripe los precios de los paquetes que aún no tienen uno y los enlaza. Idempotente:
/// un paquete ya enlazado se devuelve tal cual, sin tocar Stripe.
///
/// El importe entra en la clave de búsqueda porque en Stripe un precio distinto es un objeto
/// distinto —los precios no se editan—. Cambiar el importe de un paquete suelta su enlace, y la
/// siguiente sincronización crea el precio nuevo. Lo ya cobrado no se toca: eso vive en Stripe.
/// </summary>
public sealed class SyncTutoringWithStripeHandler(
    ITutoringPackageRepository packages,
    IPaymentGateway? payments = null)
{
    public async Task<Result<IReadOnlyList<TutoringSyncResult>, Error>> HandleAsync(CancellationToken ct)
    {
        if (payments is null)
        {
            return Error.Conflict(
                "stripe.not_configured",
                "Stripe no está configurado. Falta Academy:Stripe:SecretKey.");
        }

        var results = new List<TutoringSyncResult>();

        foreach (var package in await packages.GetAllAsync(ct))
        {
            if (package.StripePriceId is not null)
            {
                results.Add(new TutoringSyncResult(package.Slug, Linked: true, package.StripePriceId, null));
                continue;
            }

            // Un paquete gratuito no tiene nada que cobrar. Crear un precio de cero en Stripe
            // solo añadiría un objeto que nadie va a usar.
            if (package.Price.AmountInCents == 0)
            {
                results.Add(new TutoringSyncResult(
                    package.Slug, Linked: false, null, "Importe cero: no hay nada que cobrar."));
                continue;
            }

            var price = await payments.EnsurePriceAsync(
                $"Inkoova Academy · {package.Name}",
                $"tutoring_{package.Slug}_{package.Price.AmountInCents}",
                package.Price,
                // Pago único: una bolsa de horas se compra, no se suscribe.
                recurringInterval: null,
                ct);

            if (price.IsFailure)
            {
                results.Add(new TutoringSyncResult(package.Slug, Linked: false, null, price.Error.Message));
                continue;
            }

            package.LinkStripePrice(price.Value);
            await packages.UpsertAsync(package, ct);

            results.Add(new TutoringSyncResult(package.Slug, Linked: true, price.Value, null));
        }

        return results;
    }
}

// ── bolsas por alumno (admin) ─────────────────────────────────────────────────────────────

public sealed class ListTutoringBalancesHandler(ITutoringGrantRepository grants)
{
    public async Task<IReadOnlyList<TutoringBalanceDto>> HandleAsync(CancellationToken ct)
    {
        var rows = await grants.GetBalancesAsync(ct);

        return [.. rows.Select(r => new TutoringBalanceDto(
            r.UserId, r.DisplayName, r.Email,
            r.MinutesTotal, r.MinutesUsed, r.MinutesTotal - r.MinutesUsed, r.ActiveGrants))];
    }
}

public sealed class GetStudentTutoringHandler(
    ITutoringGrantRepository grants,
    ITutoringAppointmentRepository appointments,
    IUserRepository users,
    TutorNames tutorNames,
    PlanAccessProbe planAccess,
    IClock clock)
{
    public async Task<Result<StudentTutoringDto, Error>> HandleAsync(Guid userId, CancellationToken ct)
    {
        var user = await users.GetByIdAsync(userId, ct);
        if (user is null)
        {
            return Error.NotFound("user.not_found", "No existe ese alumno.");
        }

        var now = clock.UtcNow;
        var plan = await planAccess.GetAsync(userId, now, ct);
        var mine = await grants.GetForUserAsync(userId, ct);
        var names = await tutorNames.AllAsync(ct);
        var citas = await appointments.GetForGrantsAsync([.. mine.Select(g => g.Id)], ct);

        return new StudentTutoringDto(
            user.Id,
            user.DisplayName,
            user.Email,
            mine.Where(g => g.IsUsable(now, plan.HasAccess)).Sum(g => g.MinutesRemaining),
            mine.Where(g => !g.IsRevoked).Sum(g => g.MinutesTotal),
            mine.Sum(g => g.MinutesUsed),
            [.. mine.Select(g => g.ToDto(now, plan, names, citas))],
            plan.HasAccess,
            plan.ValidUntil);
    }
}

/// <summary>
/// Concesión desde el panel. O se indica un paquete del catálogo, y se copian sus horas y su
/// validez, o se indican los minutos a mano para un acuerdo suelto.
/// </summary>
public sealed record GrantTutoringRequest(
    Guid UserId,
    Guid? PackageId,
    int? Minutes,
    string? Name,
    string Source,
    DateTimeOffset? ExpiresAt,
    string Note);

public sealed class GrantTutoringHandler(
    ITutoringGrantRepository grants,
    ITutoringPackageRepository packages,
    IUserRepository users,
    PlanAccessProbe planAccess,
    IClock clock)
{
    public async Task<Result<TutoringGrantDto, Error>> HandleAsync(
        GrantTutoringRequest request, Guid adminUserId, CancellationToken ct)
    {
        var user = await users.GetByIdAsync(request.UserId, ct);
        if (user is null)
        {
            return Error.NotFound("user.not_found", "No existe ese alumno.");
        }

        var now = clock.UtcNow;
        var source = request.Source == "purchase" ? TutoringGrantSource.Purchase : TutoringGrantSource.Manual;

        Result<TutoringGrant, Error> grant;

        if (request.PackageId is { } packageId)
        {
            var package = await packages.GetByIdAsync(packageId, ct);
            if (package is null)
            {
                return Error.NotFound("tutoring_package.not_found", "No existe ese paquete.");
            }

            grant = TutoringGrant.FromPackage(
                Guid.CreateVersion7(), request.UserId, package, source,
                purchaseId: null, now, request.Note, adminUserId);

            // Una fecha puesta a mano gana sobre el modo del paquete: el acuerdo concreto con un
            // alumno manda sobre el valor por defecto del catálogo, y poner fecha es decir
            // «esta bolsa caduca ese día», no «la que diga su suscripción».
            if (grant.IsSuccess && request.ExpiresAt is { } expiry)
            {
                grant = TutoringGrant.Create(
                    grant.Value.Id, request.UserId, package.Id, package.Name, package.Minutes,
                    source, purchaseId: null, now, TutoringExpiry.FixedDate, expiry,
                    request.Note, adminUserId,
                    // Rehacer la concesión no puede perder lo que costó: sin ese importe no
                    // habría con qué liquidarle al profesor las horas de esta bolsa.
                    source == TutoringGrantSource.Purchase
                        ? package.Price
                        : Money.Zero(package.Price.Currency));
            }
        }
        else
        {
            if (request.Minutes is not { } minutes)
            {
                return Error.Validation(
                    "tutoring_grant.minutes_required",
                    "Indica un paquete o el tiempo que se concede.");
            }

            // Tiempo suelto: con fecha caduca en esa fecha; sin fecha no caduca. Atarlo a una
            // suscripción sin que nadie lo haya pedido sería apagarle las horas a una empresa
            // que no tiene por qué tener plan.
            grant = TutoringGrant.Create(
                Guid.CreateVersion7(), request.UserId, packageId: null,
                string.IsNullOrWhiteSpace(request.Name) ? "Tutorías" : request.Name!,
                minutes, source, purchaseId: null, now,
                request.ExpiresAt is null ? TutoringExpiry.Never : TutoringExpiry.FixedDate,
                request.ExpiresAt, request.Note, adminUserId);
        }

        if (grant.IsFailure)
        {
            return grant.Error;
        }

        await grants.SaveAsync(grant.Value, ct);

        var plan = await planAccess.GetAsync(request.UserId, now, ct);
        return grant.Value.ToDto(now, plan);
    }
}

public sealed record RecordTutoringSessionRequest(
    int Minutes,
    DateTimeOffset OccurredAt,
    string Topic,
    string Notes,
    // Quién la dio. Opcional porque se puede apuntar una tutoría que dio la propia casa
    // y que no hay que liquidar a nadie; cuando se indica, se devenga su parte.
    Guid? TutorId = null);

/// <summary>
/// Apunta una tutoría dada contra una bolsa. La comprobación de saldo la hace la entidad, que
/// es donde vive la invariante; aquí solo se carga, se guarda y se traduce.
/// </summary>
public sealed class RecordTutoringSessionHandler(
    ITutoringGrantRepository grants,
    ITutorRepository tutors,
    ITutorEarningRepository earnings,
    TutorNames tutorNames,
    PlanAccessProbe planAccess,
    IClock clock)
{
    public async Task<Result<TutoringGrantDto, Error>> HandleAsync(
        Guid grantId, RecordTutoringSessionRequest request, Guid adminUserId, CancellationToken ct)
    {
        var grant = await grants.GetByIdAsync(grantId, ct);
        if (grant is null)
        {
            return Error.NotFound("tutoring_grant.not_found", "No existe esa bolsa de tutorías.");
        }

        var now = clock.UtcNow;

        // Se mira el plan del DUEÑO de la bolsa, no el de quien apunta: lo apunta un
        // administrador, y su suscripción no tiene nada que ver con estas horas.
        var plan = await planAccess.GetAsync(grant.UserId, now, ct);

        // El profesor se valida ANTES de apuntar nada: apuntar la tutoría y fallar luego al
        // devengar dejaría una clase dada sin nadie a quien pagarla, y nadie volvería a
        // mirarla.
        Tutor? tutor = null;
        if (request.TutorId is { } tutorId)
        {
            tutor = await tutors.GetByIdAsync(tutorId, ct);
            if (tutor is null)
            {
                return Error.NotFound("tutor.not_found", "No existe ese profesor.");
            }
        }

        var consumed = grant.Consume(
            Guid.CreateVersion7(), request.Minutes, request.OccurredAt,
            request.Topic, request.Notes, adminUserId, now, plan.HasAccess, tutor?.Id);

        if (consumed.IsFailure)
        {
            return consumed.Error;
        }

        await grants.SaveAsync(grant, ct);

        // El devengo va después de guardar la sesión porque tiene una clave ajena contra
        // ella. Se calcula sobre lo que el alumno pagó por ESTA bolsa, prorrateado por los
        // minutos de la clase: no sobre el precio de catálogo de hoy, que pudo cambiar.
        if (tutor is not null)
        {
            await earnings.SaveAsync(
                TutorEarning.Accrue(
                    Guid.CreateVersion7(), tutor, consumed.Value.Id,
                    grant.Paid, grant.MinutesTotal, consumed.Value.Minutes, now),
                ct);
        }

        return grant.ToDto(now, plan, await tutorNames.AllAsync(ct));
    }
}

/// <summary>Deshace una tutoría mal apuntada. Es la única forma de devolver minutos al saldo.</summary>
public sealed class DeleteTutoringSessionHandler(
    ITutoringGrantRepository grants,
    ITutorEarningRepository earnings,
    ITutoringAppointmentRepository appointments,
    PlanAccessProbe planAccess,
    IClock clock)
{
    public async Task<Result<TutoringGrantDto, Error>> HandleAsync(
        Guid grantId, Guid sessionId, CancellationToken ct)
    {
        var grant = await grants.GetByIdAsync(grantId, ct);
        if (grant is null)
        {
            return Error.NotFound("tutoring_grant.not_found", "No existe esa bolsa de tutorías.");
        }

        // Borrar la sesión se lleva por delante su devengo (clave ajena en cascada). Si ya
        // se pagó, eso borraría el rastro de un pago hecho de verdad y la liquidación del
        // mes dejaría de cuadrar. Que lo deshaga quien pueda revertir también el pago.
        var earning = await earnings.GetBySessionAsync(sessionId, ct);
        if (earning is { IsPaid: true })
        {
            return Error.Conflict(
                "tutoring_session.earning_paid",
                "Esa tutoría ya se le liquidó al profesor y no se puede borrar.");
        }

        var removed = grant.RemoveSession(sessionId);
        if (removed.IsFailure)
        {
            return removed.Error;
        }

        // Si la tutoría había salido de una cita, la cita vuelve a estar pendiente. Es la
        // verdad —la clase se quedó sin dar— y así no desaparece de la agenda sin que nadie
        // haya decidido anularla.
        var appointment = await appointments.GetBySessionAsync(sessionId, ct);
        if (appointment is not null)
        {
            appointment.Reopen();
            await appointments.UpsertAsync(appointment, ct);
        }

        await grants.SaveAsync(grant, ct);

        var now = clock.UtcNow;
        return grant.ToDto(now, await planAccess.GetAsync(grant.UserId, now, ct));
    }
}

public sealed class RevokeTutoringGrantHandler(
    ITutoringGrantRepository grants,
    PlanAccessProbe planAccess,
    IClock clock)
{
    public async Task<Result<TutoringGrantDto, Error>> HandleAsync(
        Guid grantId, string reason, CancellationToken ct)
    {
        var grant = await grants.GetByIdAsync(grantId, ct);
        if (grant is null)
        {
            return Error.NotFound("tutoring_grant.not_found", "No existe esa bolsa de tutorías.");
        }

        var now = clock.UtcNow;
        var revoked = grant.Revoke(reason, now);
        if (revoked.IsFailure)
        {
            return revoked.Error;
        }

        await grants.SaveAsync(grant, ct);
        return grant.ToDto(now, await planAccess.GetAsync(grant.UserId, now, ct));
    }
}

/// <summary>Corrige el total concedido: un cero de más al teclear, u horas extra de cortesía.</summary>
public sealed class AdjustTutoringGrantHandler(
    ITutoringGrantRepository grants,
    PlanAccessProbe planAccess,
    IClock clock)
{
    public async Task<Result<TutoringGrantDto, Error>> HandleAsync(
        Guid grantId, int minutesTotal, CancellationToken ct)
    {
        var grant = await grants.GetByIdAsync(grantId, ct);
        if (grant is null)
        {
            return Error.NotFound("tutoring_grant.not_found", "No existe esa bolsa de tutorías.");
        }

        var adjusted = grant.AdjustTotal(minutesTotal);
        if (adjusted.IsFailure)
        {
            return adjusted.Error;
        }

        await grants.SaveAsync(grant, ct);

        var now = clock.UtcNow;
        return grant.ToDto(now, await planAccess.GetAsync(grant.UserId, now, ct));
    }
}

// ── alumno ────────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Lo que el alumno ve en su cuenta: qué compró, cuánto ha consumido y cuánto le queda.
///
/// El titular suma solo las bolsas utilizables. Una caducada con horas sin gastar sigue
/// listada, porque el alumno tiene derecho a ver que las perdió, pero no cuenta en el saldo:
/// sumarla sería prometer un tiempo que ya no se puede pedir.
/// </summary>
public sealed class GetMyTutoringHandler(
    ITutoringGrantRepository grants,
    ITutoringPackageRepository packages,
    ITutoringAppointmentRepository appointments,
    TutorNames tutorNames,
    PlanAccessProbe planAccess,
    IClock clock)
{
    public async Task<MyTutoringDto> HandleAsync(Guid userId, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var plan = await planAccess.GetAsync(userId, now, ct);
        var mine = await grants.GetForUserAsync(userId, ct);
        var offer = await packages.GetActiveAsync(ct);

        // El alumno tiene que ver lo que tiene convocado sin ir a buscarlo a su correo.
        var citas = await appointments.GetForGrantsAsync([.. mine.Select(g => g.Id)], ct);
        var names = await tutorNames.AllAsync(ct);

        var usable = mine.Where(g => g.IsUsable(now, plan.HasAccess)).ToList();

        return new MyTutoringDto(
            usable.Sum(g => g.MinutesRemaining),
            mine.Where(g => !g.IsRevoked).Sum(g => g.MinutesTotal),
            mine.Sum(g => g.MinutesUsed),
            // La caducidad más próxima de lo que todavía se puede gastar. En las bolsas atadas
            // a la suscripción es la fecha de la propia suscripción, así que se recalcula en
            // cada lectura y se mueve sola cuando el alumno renueva.
            usable.Select(g => g.EffectiveExpiry(plan.ValidUntil)).Where(d => d is not null).Min(),
            [.. mine.Select(g => g.ToDto(now, plan, names, citas))],
            [.. offer.Select(p => p.ToDto())],
            plan.HasAccess,
            plan.ValidUntil);
    }
}
