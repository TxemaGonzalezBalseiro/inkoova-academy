namespace Inkoova.Academy.Application.Abstractions;

/// <summary>Node of the learning roadmap (T-09). Edited from admin, rendered by the SPA.</summary>
public sealed record RoadmapNode(
    Guid Id,
    string Key,
    string Title,
    string Description,
    Guid? CourseId,
    Guid? LessonId,
    int Order,
    IReadOnlyList<string> PrerequisiteKeys);

public interface IRoadmapRepository
{
    Task<IReadOnlyList<RoadmapNode>> GetAllAsync(CancellationToken ct);

    Task UpsertAsync(RoadmapNode node, CancellationToken ct);

    Task DeleteAsync(Guid id, CancellationToken ct);
}

/// <summary>Email captured on a coming-soon course page (T-06).</summary>
public sealed record WaitlistEntry(Guid Id, Guid CourseId, string Email, DateTimeOffset CreatedAt);

public interface IWaitlistRepository
{
    Task<bool> AddAsync(WaitlistEntry entry, CancellationToken ct);

    Task<IReadOnlyList<WaitlistEntry>> GetForCourseAsync(Guid courseId, CancellationToken ct);
}

/// <summary>One admin action. Append-only; nothing in the app deletes from this table (T-11).</summary>
public sealed record AuditEntry(
    Guid Id,
    Guid ActorUserId,
    string Action,
    string EntityType,
    string? EntityId,
    string? DetailsJson,
    DateTimeOffset OccurredAt);

public interface IAuditLogRepository
{
    Task AppendAsync(AuditEntry entry, CancellationToken ct);

    Task<IReadOnlyList<AuditEntry>> GetRecentAsync(int limit, CancellationToken ct);
}

/// <summary>A pack file download. Feeds metrics and the new-version notification list (T-07).</summary>
public sealed record DownloadLogEntry(
    Guid Id,
    Guid UserId,
    Guid PackFileId,
    string PackVersion,
    DateTimeOffset DownloadedAt);

public interface IDownloadLogRepository
{
    Task AppendAsync(DownloadLogEntry entry, CancellationToken ct);

    /// <summary>Users who downloaded any file of a pack. Recipients of the new-version email.</summary>
    Task<IReadOnlyList<Guid>> GetUserIdsWhoDownloadedPackAsync(Guid packId, CancellationToken ct);
}

/// <summary>Community session offered as a plan benefit (T-12).</summary>
public sealed record CommunitySession(
    Guid Id,
    string Title,
    string Description,
    DateTimeOffset StartsAt,
    int DurationMinutes,
    string JoinUrl,
    IReadOnlyList<string> RequiredPlanCodes);

public interface ICommunitySessionRepository
{
    Task<IReadOnlyList<CommunitySession>> GetUpcomingAsync(DateTimeOffset from, CancellationToken ct);

    Task UpsertAsync(CommunitySession session, CancellationToken ct);
}

/// <summary>Link between an academy account and a Discord identity (T-12).</summary>
public sealed record DiscordLink(Guid UserId, string DiscordUserId, DateTimeOffset LinkedAt, string? LastAppliedRole);

public interface IDiscordLinkRepository
{
    Task<DiscordLink?> GetByUserIdAsync(Guid userId, CancellationToken ct);

    Task<IReadOnlyList<DiscordLink>> GetAllAsync(CancellationToken ct);

    Task UpsertAsync(DiscordLink link, CancellationToken ct);

    Task DeleteAsync(Guid userId, CancellationToken ct);
}

/// <summary>Fiscal invoice issued for a Stripe payment and chained under Verifactu (T-14).</summary>
public sealed record FiscalInvoice(
    Guid Id,
    Guid UserId,
    string Series,
    int Number,
    DateOnly IssueDate,
    long TotalCents,
    long TaxCents,
    string Currency,
    string StripeInvoiceId,
    string PreviousHash,
    string Hash,
    string? PdfContentRef,
    bool IsRectification,
    Guid? RectifiesInvoiceId);

public interface IFiscalInvoiceRepository
{
    Task<FiscalInvoice?> GetLastInSeriesAsync(string series, CancellationToken ct);

    Task<FiscalInvoice?> GetByStripeInvoiceIdAsync(string stripeInvoiceId, CancellationToken ct);

    Task<IReadOnlyList<FiscalInvoice>> GetForUserAsync(Guid userId, CancellationToken ct);

    Task<int> GetNextNumberAsync(string series, CancellationToken ct);

    Task InsertAsync(FiscalInvoice invoice, CancellationToken ct);
}

/// <summary>Consent record for the cookie banner and the processing register (T-14).</summary>
public sealed record ConsentRecord(
    Guid Id,
    Guid? UserId,
    string VisitorId,
    bool Analytics,
    bool Marketing,
    string PolicyVersion,
    DateTimeOffset RecordedAt);

public interface IConsentRepository
{
    Task RecordAsync(ConsentRecord record, CancellationToken ct);

    Task<ConsentRecord?> GetLatestAsync(string visitorId, CancellationToken ct);
}
