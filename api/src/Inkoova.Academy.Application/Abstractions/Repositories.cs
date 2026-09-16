using Inkoova.Academy.Domain.Affiliates;
using Inkoova.Academy.Domain.Billing;
using Inkoova.Academy.Domain.Catalog;
using Inkoova.Academy.Domain.Learning;
using Inkoova.Academy.Domain.Users;
using Inkoova.Academy.Domain.ValueObjects;

namespace Inkoova.Academy.Application.Abstractions;

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(Guid id, CancellationToken ct);

    Task<Product?> GetBySlugAsync(Slug slug, CancellationToken ct);

    Task<IReadOnlyList<Product>> GetByTypeAsync(ProductType type, CancellationToken ct);

    Task UpsertAsync(Product product, CancellationToken ct);
}

public interface ICourseRepository
{
    /// <summary>Full aggregate, sections and lessons included. Used by the syllabus and the player.</summary>
    Task<Course?> GetBySlugAsync(Slug slug, CancellationToken ct);

    Task<Course?> GetByIdAsync(Guid id, CancellationToken ct);

    Task<Course?> GetByProductIdAsync(Guid productId, CancellationToken ct);

    /// <summary>Published and coming-soon courses, with metrics. Draft courses never leave here.</summary>
    Task<IReadOnlyList<Course>> GetPublicCatalogAsync(CancellationToken ct);

    Task<IReadOnlyList<Course>> GetAllAsync(CancellationToken ct);

    Task<Lesson?> GetLessonAsync(Guid lessonId, CancellationToken ct);

    /// <summary>Product that owns the lesson. The single question <c>IAccessPolicy</c> needs.</summary>
    Task<Guid?> GetProductIdForLessonAsync(Guid lessonId, CancellationToken ct);

    Task SaveAsync(Course course, CancellationToken ct);

    Task DeleteSectionAsync(Guid sectionId, CancellationToken ct);

    Task DeleteLessonAsync(Guid lessonId, CancellationToken ct);
}

public interface IPackRepository
{
    Task<Pack?> GetBySlugAsync(Slug slug, CancellationToken ct);

    Task<Pack?> GetByIdAsync(Guid id, CancellationToken ct);

    Task<IReadOnlyList<Pack>> GetPublishedAsync(CancellationToken ct);

    Task<IReadOnlyList<Pack>> GetAllAsync(CancellationToken ct);

    Task SaveAsync(Pack pack, CancellationToken ct);

    Task<PackFile?> GetFileAsync(Guid fileId, CancellationToken ct);
}

public interface IProgramRepository
{
    Task<LearningProgram?> GetBySlugAsync(Slug slug, CancellationToken ct);

    Task<LearningProgram?> GetDefaultAsync(CancellationToken ct);

    Task SaveAsync(LearningProgram program, CancellationToken ct);
}

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct);

    Task<User?> GetByEmailAsync(string email, CancellationToken ct);

    Task<User?> GetByStripeCustomerIdAsync(string stripeCustomerId, CancellationToken ct);

    Task<IReadOnlyList<User>> SearchAsync(string term, int limit, CancellationToken ct);

    Task UpsertAsync(User user, CancellationToken ct);
}

public interface IPlanRepository
{
    Task<IReadOnlyList<Plan>> GetActiveAsync(CancellationToken ct);

    /// <summary>Todos los planes, activos o no. Solo lo usa el panel.</summary>
    Task<IReadOnlyList<Plan>> GetAllAsync(CancellationToken ct);

    Task<Plan?> GetByCodeAsync(string code, CancellationToken ct);

    Task<Plan?> GetByIdAsync(Guid id, CancellationToken ct);

    Task<Plan?> GetByStripePriceIdAsync(string stripePriceId, CancellationToken ct);

    Task UpsertAsync(Plan plan, CancellationToken ct);
}

public interface ISubscriptionRepository
{
    Task<Subscription?> GetActiveForUserAsync(Guid userId, CancellationToken ct);

    Task<Subscription?> GetByStripeIdAsync(string stripeSubscriptionId, CancellationToken ct);

    Task<IReadOnlyList<Subscription>> GetExpiringBeforeAsync(DateTimeOffset instant, CancellationToken ct);

    /// <summary>
    /// Todas las suscripciones que hoy dan acceso, incluidas las que están en periodo de gracia
    /// por un recibo devuelto. Es la entrada de la reconciliación de derechos: cuando entra un
    /// curso o un pack nuevo, sus suscriptores tienen que recibirlo sin esperar a renovar.
    /// </summary>
    Task<IReadOnlyList<Subscription>> GetActiveAsync(CancellationToken ct);

    Task UpsertAsync(Subscription subscription, CancellationToken ct);
}

public interface IEntitlementRepository
{
    Task<IReadOnlyList<Entitlement>> GetForUserAsync(Guid userId, CancellationToken ct);

    Task<Entitlement?> GetForUserAndProductAsync(Guid userId, Guid productId, CancellationToken ct);

    Task<IReadOnlyList<Entitlement>> GetBySourceAsync(Guid userId, EntitlementSource source, CancellationToken ct);

    /// <summary>Plan-derived entitlements whose window has closed. Input of the daily revocation job.</summary>
    Task<IReadOnlyList<Entitlement>> GetExpiredPlanIncludedAsync(DateTimeOffset instant, CancellationToken ct);

    Task UpsertAsync(Entitlement entitlement, CancellationToken ct);
}

public interface IPurchaseRepository
{
    Task<Purchase?> GetByIdAsync(Guid id, CancellationToken ct);

    Task<Purchase?> GetByPaymentIntentAsync(string paymentIntentId, CancellationToken ct);

    Task<IReadOnlyList<Purchase>> GetForUserAsync(Guid userId, CancellationToken ct);

    Task UpsertAsync(Purchase purchase, CancellationToken ct);
}

public interface IProgressRepository
{
    Task<IReadOnlyList<LessonProgress>> GetForCourseAsync(Guid userId, Guid courseId, CancellationToken ct);

    Task<LessonProgress?> GetAsync(Guid userId, Guid lessonId, CancellationToken ct);

    Task<IReadOnlySet<Guid>> GetCompletedLessonIdsAsync(Guid userId, Guid courseId, CancellationToken ct);

    Task UpsertAsync(LessonProgress progress, CancellationToken ct);

    /// <summary>Lesson ids where students most often stop, for the admin metrics panel (T-11).</summary>
    Task<IReadOnlyList<(Guid LessonId, int DropOffCount)>> GetDropOffPointsAsync(Guid courseId, CancellationToken ct);
}

public interface IQuizRepository
{
    Task<Quiz?> GetBySlugAsync(Slug slug, CancellationToken ct);

    Task<Quiz?> GetByIdAsync(Guid id, CancellationToken ct);

    Task SaveAsync(Quiz quiz, CancellationToken ct);

    Task<IReadOnlyList<QuizAttempt>> GetAttemptsForUserAsync(Guid userId, CancellationToken ct);

    Task<QuizAttempt?> GetAttemptByAnonymousKeyAsync(string anonymousKey, CancellationToken ct);

    Task<IReadOnlyList<QuizAttempt>> GetAttemptsByAnonymousKeyAsync(string anonymousKey, CancellationToken ct);

    Task SaveAttemptAsync(QuizAttempt attempt, CancellationToken ct);
}

public interface ICertificateRepository
{
    Task<Certificate?> GetByCodeAsync(CertificateCode code, CancellationToken ct);

    Task<Certificate?> GetForUserAndSubjectAsync(Guid userId, Guid subjectId, CancellationToken ct);

    Task<IReadOnlyList<Certificate>> GetForUserAsync(Guid userId, CancellationToken ct);

    Task UpsertAsync(Certificate certificate, CancellationToken ct);
}

public interface IAffiliateRepository
{
    Task<Affiliate?> GetByIdAsync(Guid id, CancellationToken ct);

    Task<Affiliate?> GetByCodeAsync(string code, CancellationToken ct);

    Task<Affiliate?> GetByUserIdAsync(Guid userId, CancellationToken ct);

    Task<IReadOnlyList<Affiliate>> GetActiveAsync(CancellationToken ct);

    Task UpsertAsync(Affiliate affiliate, CancellationToken ct);
}

public interface IDiscountCodeRepository
{
    Task<DiscountCode?> GetByCodeAsync(string code, CancellationToken ct);

    Task<IReadOnlyList<DiscountCode>> GetAllAsync(CancellationToken ct);

    Task UpsertAsync(DiscountCode code, CancellationToken ct);
}

public interface IReferralRepository
{
    Task<Referral?> GetByVisitorAsync(string visitorId, CancellationToken ct);

    Task<int> CountClicksAsync(Guid affiliateId, DateOnly from, DateOnly to, CancellationToken ct);

    Task UpsertAsync(Referral referral, CancellationToken ct);
}

public interface ICommissionRepository
{
    Task<IReadOnlyList<Commission>> GetForAffiliateAsync(Guid affiliateId, CancellationToken ct);

    Task<IReadOnlyList<Commission>> GetByPurchaseAsync(Guid purchaseId, CancellationToken ct);

    /// <summary>Pending commissions whose withdrawal window has closed. Input of the settlement job.</summary>
    Task<IReadOnlyList<Commission>> GetApprovableAsync(DateTimeOffset instant, CancellationToken ct);

    Task<IReadOnlyList<Commission>> GetPayableAsync(Guid affiliateId, DateOnly periodEnd, CancellationToken ct);

    Task UpsertAsync(Commission commission, CancellationToken ct);
}

public interface IPayoutRepository
{
    Task<IReadOnlyList<Payout>> GetForAffiliateAsync(Guid affiliateId, CancellationToken ct);

    Task<Payout?> GetForPeriodAsync(Guid affiliateId, DateOnly periodStart, CancellationToken ct);

    Task<Payout?> GetByIdAsync(Guid id, CancellationToken ct);

    Task UpsertAsync(Payout payout, CancellationToken ct);
}

/// <summary>Stripe event ledger. Makes webhook processing idempotent (T-04).</summary>
public interface IStripeEventStore
{
    /// <summary>True when the event was recorded now, false when it had already been processed.</summary>
    Task<bool> TryRecordAsync(string eventId, string eventType, DateTimeOffset receivedAt, CancellationToken ct);

    Task MarkProcessedAsync(string eventId, DateTimeOffset processedAt, CancellationToken ct);

    Task MarkFailedAsync(string eventId, string error, CancellationToken ct);
}
