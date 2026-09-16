using Inkoova.Academy.Application.Abstractions;
using Inkoova.Academy.Domain.Catalog;

namespace Inkoova.Academy.Application.Access;

/// <summary>
/// One question, one answer (ADR-007): the lesson is a free preview, or the student holds a
/// valid entitlement over the product that contains it. Subscriptions, purchases and manual
/// grants all reach here as entitlements, so this class never grows an <c>or</c> per channel.
/// </summary>
public sealed class AccessPolicy(
    IEntitlementRepository entitlements,
    ICourseRepository courses,
    IClock clock) : IAccessPolicy
{
    public async Task<bool> CanAccessLessonAsync(Guid? userId, Lesson lesson, CancellationToken ct)
    {
        if (lesson.IsFreePreview)
        {
            return true;
        }

        if (userId is not { } uid)
        {
            return false;
        }

        var productId = await courses.GetProductIdForLessonAsync(lesson.Id, ct);
        return productId is { } pid && await CanAccessProductAsync(uid, pid, ct);
    }

    public async Task<bool> CanAccessProductAsync(Guid? userId, Guid productId, CancellationToken ct)
    {
        if (userId is not { } uid)
        {
            return false;
        }

        var entitlement = await entitlements.GetForUserAndProductAsync(uid, productId, ct);
        return entitlement is not null && entitlement.IsValidAt(clock.UtcNow);
    }

    public async Task<IReadOnlySet<Guid>> GetAccessibleProductIdsAsync(Guid userId, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var all = await entitlements.GetForUserAsync(userId, ct);
        return all.Where(e => e.IsValidAt(now)).Select(e => e.ProductId).ToHashSet();
    }
}
