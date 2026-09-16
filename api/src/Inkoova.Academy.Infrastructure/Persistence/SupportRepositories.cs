using System.Text.Json;
using Dapper;
using Inkoova.Academy.Application.Abstractions;
using Inkoova.Academy.Domain.Catalog;
using Inkoova.Academy.Domain.ValueObjects;

namespace Inkoova.Academy.Infrastructure.Persistence;

public sealed class PackRepository(IDbConnectionFactory connections) : IPackRepository
{
    private const string Columns = """
        id, product_id, slug, title, sector, version, changelog, status,
        regulatory_check_date, updated_at
        """;

    public async Task<Pack?> GetBySlugAsync(Slug slug, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        var row = await connection.QuerySingleOrDefaultAsync<PackRow>(
            $"SELECT {Columns} FROM pack WHERE slug = @slug", new { slug = slug.Value });

        return row is null ? null : await LoadAsync(row, ct);
    }

    public async Task<Pack?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        var row = await connection.QuerySingleOrDefaultAsync<PackRow>(
            $"SELECT {Columns} FROM pack WHERE id = @id", new { id });

        return row is null ? null : await LoadAsync(row, ct);
    }

    public async Task<IReadOnlyList<Pack>> GetPublishedAsync(CancellationToken ct) =>
        await LoadManyAsync("WHERE status = 'published'", ct);

    public async Task<IReadOnlyList<Pack>> GetAllAsync(CancellationToken ct) =>
        await LoadManyAsync(string.Empty, ct);

    public async Task SaveAsync(Pack pack, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        using var transaction = connection.BeginTransaction();

        await connection.ExecuteAsync(
            """
            INSERT INTO pack (id, product_id, slug, title, sector, version, changelog, status,
                              regulatory_check_date, updated_at)
            VALUES (@Id, @ProductId, @Slug, @Title, @Sector, @Version, @Changelog, @Status,
                    @RegulatoryCheckDate, @UpdatedAt)
            ON CONFLICT (id) DO UPDATE SET
                slug = EXCLUDED.slug, title = EXCLUDED.title, sector = EXCLUDED.sector,
                version = EXCLUDED.version, changelog = EXCLUDED.changelog, status = EXCLUDED.status,
                regulatory_check_date = EXCLUDED.regulatory_check_date, updated_at = EXCLUDED.updated_at
            """,
            new
            {
                pack.Id,
                pack.ProductId,
                Slug = pack.Slug.Value,
                pack.Title,
                pack.Sector,
                pack.Version,
                pack.Changelog,
                Status = EnumMapping.ToDb(pack.Status),
                pack.RegulatoryCheckDate,
                pack.UpdatedAt
            },
            transaction);

        var fileIds = pack.Files.Select(f => f.Id).ToArray();
        await connection.ExecuteAsync(
            "DELETE FROM pack_file WHERE pack_id = @packId AND NOT (id = ANY(@fileIds))",
            new { packId = pack.Id, fileIds },
            transaction);

        foreach (var file in pack.Files)
        {
            await connection.ExecuteAsync(
                """
                INSERT INTO pack_file (id, pack_id, file_name, content_ref, size_in_bytes, sha256, sort_order)
                VALUES (@Id, @PackId, @FileName, @ContentRef, @SizeInBytes, @Sha256, @Order)
                ON CONFLICT (id) DO UPDATE SET
                    file_name = EXCLUDED.file_name, content_ref = EXCLUDED.content_ref,
                    size_in_bytes = EXCLUDED.size_in_bytes, sha256 = EXCLUDED.sha256,
                    sort_order = EXCLUDED.sort_order
                """,
                new { file.Id, file.PackId, file.FileName, file.ContentRef, file.SizeInBytes, file.Sha256, file.Order },
                transaction);
        }

        transaction.Commit();
    }

    public async Task<PackFile?> GetFileAsync(Guid fileId, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        var row = await connection.QuerySingleOrDefaultAsync<PackFileRow>(
            """
            SELECT id, pack_id, file_name, content_ref, size_in_bytes, sha256, sort_order
            FROM pack_file WHERE id = @fileId
            """,
            new { fileId });

        return row?.ToDomain();
    }

    private async Task<Pack> LoadAsync(PackRow row, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        var files = await connection.QueryAsync<PackFileRow>(
            """
            SELECT id, pack_id, file_name, content_ref, size_in_bytes, sha256, sort_order
            FROM pack_file WHERE pack_id = @packId ORDER BY sort_order
            """,
            new { packId = row.Id });

        return row.ToDomain(files.Select(f => f.ToDomain()));
    }

    private async Task<IReadOnlyList<Pack>> LoadManyAsync(string whereClause, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        var rows = (await connection.QueryAsync<PackRow>(
            $"SELECT {Columns} FROM pack {whereClause} ORDER BY slug")).ToList();

        if (rows.Count == 0)
        {
            return [];
        }

        var packIds = rows.Select(r => r.Id).ToArray();
        var files = (await connection.QueryAsync<PackFileRow>(
            """
            SELECT id, pack_id, file_name, content_ref, size_in_bytes, sha256, sort_order
            FROM pack_file WHERE pack_id = ANY(@packIds) ORDER BY sort_order
            """,
            new { packIds })).ToList();

        return rows
            .Select(r => r.ToDomain(files.Where(f => f.PackId == r.Id).Select(f => f.ToDomain())))
            .ToList();
    }

    private sealed record PackRow(
        Guid Id,
        Guid ProductId,
        string Slug,
        string Title,
        string Sector,
        string Version,
        string? Changelog,
        string Status,
        DateOnly? RegulatoryCheckDate,
        DateTimeOffset UpdatedAt)
    {
        public Pack ToDomain(IEnumerable<PackFile> files) => Pack.Rehydrate(
            Id, ProductId, Domain.ValueObjects.Slug.Create(Slug).Value, Title, Sector, Version, Changelog,
            EnumMapping.FromDb<PublicationStatus>(Status), RegulatoryCheckDate, UpdatedAt, files);
    }

    private sealed record PackFileRow(
        Guid Id,
        Guid PackId,
        string FileName,
        string ContentRef,
        long SizeInBytes,
        string Sha256,
        int SortOrder)
    {
        public PackFile ToDomain() => new(Id, PackId, FileName, ContentRef, SizeInBytes, Sha256, SortOrder);
    }
}

public sealed class ProgramRepository(IDbConnectionFactory connections) : IProgramRepository
{
    public async Task<LearningProgram?> GetBySlugAsync(Slug slug, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        var row = await connection.QuerySingleOrDefaultAsync<ProgramRow>(
            """
            SELECT id, product_id, slug, title, plan_codes_including_packs
            FROM learning_program WHERE slug = @slug
            """,
            new { slug = slug.Value });

        return row is null ? null : await LoadAsync(row, ct);
    }

    /// <summary>
    /// The academy sells one program today. Returning the first by slug keeps the callers
    /// simple; if a second program appears this becomes an explicit lookup.
    /// </summary>
    public async Task<LearningProgram?> GetDefaultAsync(CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        var row = await connection.QueryFirstOrDefaultAsync<ProgramRow>(
            """
            SELECT id, product_id, slug, title, plan_codes_including_packs
            FROM learning_program ORDER BY slug LIMIT 1
            """);

        return row is null ? null : await LoadAsync(row, ct);
    }

    public async Task SaveAsync(LearningProgram program, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        using var transaction = connection.BeginTransaction();

        await connection.ExecuteAsync(
            """
            INSERT INTO learning_program (id, product_id, slug, title, plan_codes_including_packs)
            VALUES (@Id, @ProductId, @Slug, @Title, @PlanCodes)
            ON CONFLICT (id) DO UPDATE SET
                slug = EXCLUDED.slug, title = EXCLUDED.title,
                plan_codes_including_packs = EXCLUDED.plan_codes_including_packs
            """,
            new
            {
                program.Id,
                program.ProductId,
                Slug = program.Slug.Value,
                program.Title,
                PlanCodes = program.PlanCodesIncludingPacks.ToArray()
            },
            transaction);

        await connection.ExecuteAsync(
            "DELETE FROM program_item WHERE program_id = @programId", new { programId = program.Id }, transaction);

        foreach (var item in program.Items)
        {
            await connection.ExecuteAsync(
                """
                INSERT INTO program_item (program_id, course_id, sort_order, prerequisite_course_ids)
                VALUES (@ProgramId, @CourseId, @Order, @Prerequisites)
                """,
                new
                {
                    item.ProgramId,
                    item.CourseId,
                    item.Order,
                    Prerequisites = item.PrerequisiteCourseIds.ToArray()
                },
                transaction);
        }

        transaction.Commit();
    }

    private async Task<LearningProgram> LoadAsync(ProgramRow row, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        var items = await connection.QueryAsync<ItemRow>(
            """
            SELECT program_id, course_id, sort_order, prerequisite_course_ids
            FROM program_item WHERE program_id = @programId ORDER BY sort_order
            """,
            new { programId = row.Id });

        return LearningProgram.Rehydrate(
            row.Id, row.ProductId, Domain.ValueObjects.Slug.Create(row.Slug).Value, row.Title,
            row.PlanCodesIncludingPacks,
            items.Select(i => new ProgramItem(i.ProgramId, i.CourseId, i.SortOrder, i.PrerequisiteCourseIds)));
    }

    private sealed record ProgramRow(
        Guid Id,
        Guid ProductId,
        string Slug,
        string Title,
        string[] PlanCodesIncludingPacks);

    private sealed record ItemRow(Guid ProgramId, Guid CourseId, int SortOrder, Guid[] PrerequisiteCourseIds);
}

public sealed class RoadmapRepository(IDbConnectionFactory connections) : IRoadmapRepository
{
    public async Task<IReadOnlyList<RoadmapNode>> GetAllAsync(CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        var rows = await connection.QueryAsync<NodeRow>(
            """
            SELECT id, node_key, title, description, course_id, lesson_id, sort_order, prerequisite_keys
            FROM roadmap_node ORDER BY sort_order
            """);

        return rows
            .Select(r => new RoadmapNode(
                r.Id, r.NodeKey, r.Title, r.Description, r.CourseId, r.LessonId, r.SortOrder, r.PrerequisiteKeys))
            .ToList();
    }

    public async Task UpsertAsync(RoadmapNode node, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        await connection.ExecuteAsync(
            """
            INSERT INTO roadmap_node (id, node_key, title, description, course_id, lesson_id,
                                      sort_order, prerequisite_keys)
            VALUES (@Id, @Key, @Title, @Description, @CourseId, @LessonId, @Order, @Prerequisites)
            ON CONFLICT (id) DO UPDATE SET
                node_key = EXCLUDED.node_key, title = EXCLUDED.title, description = EXCLUDED.description,
                course_id = EXCLUDED.course_id, lesson_id = EXCLUDED.lesson_id,
                sort_order = EXCLUDED.sort_order, prerequisite_keys = EXCLUDED.prerequisite_keys
            """,
            new
            {
                node.Id,
                node.Key,
                node.Title,
                node.Description,
                node.CourseId,
                node.LessonId,
                node.Order,
                Prerequisites = node.PrerequisiteKeys.ToArray()
            });
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        await connection.ExecuteAsync("DELETE FROM roadmap_node WHERE id = @id", new { id });
    }

    private sealed record NodeRow(
        Guid Id,
        string NodeKey,
        string Title,
        string Description,
        Guid? CourseId,
        Guid? LessonId,
        int SortOrder,
        string[] PrerequisiteKeys);
}

public sealed class WaitlistRepository(IDbConnectionFactory connections) : IWaitlistRepository
{
    /// <summary>Returns false when the email was already on the list; that is not an error.</summary>
    public async Task<bool> AddAsync(WaitlistEntry entry, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        var inserted = await connection.ExecuteAsync(
            """
            INSERT INTO waitlist (id, course_id, email, created_at)
            VALUES (@Id, @CourseId, @Email, @CreatedAt)
            ON CONFLICT (course_id, lower(email)) DO NOTHING
            """,
            new { entry.Id, entry.CourseId, entry.Email, entry.CreatedAt });

        return inserted == 1;
    }

    public async Task<IReadOnlyList<WaitlistEntry>> GetForCourseAsync(Guid courseId, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        var rows = await connection.QueryAsync<WaitlistEntry>(
            "SELECT id, course_id, email, created_at FROM waitlist WHERE course_id = @courseId ORDER BY created_at",
            new { courseId });

        return rows.ToList();
    }
}

public sealed class AuditLogRepository(IDbConnectionFactory connections) : IAuditLogRepository
{
    public async Task AppendAsync(AuditEntry entry, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        await connection.ExecuteAsync(
            """
            INSERT INTO audit_log (id, actor_user_id, action, entity_type, entity_id, details_json, occurred_at)
            VALUES (@Id, @ActorUserId, @Action, @EntityType, @EntityId, @DetailsJson::jsonb, @OccurredAt)
            """,
            new
            {
                entry.Id,
                entry.ActorUserId,
                entry.Action,
                entry.EntityType,
                entry.EntityId,
                entry.DetailsJson,
                entry.OccurredAt
            });
    }

    public async Task<IReadOnlyList<AuditEntry>> GetRecentAsync(int limit, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        var rows = await connection.QueryAsync<AuditEntry>(
            """
            SELECT id, actor_user_id, action, entity_type, entity_id, details_json, occurred_at
            FROM audit_log ORDER BY occurred_at DESC LIMIT @limit
            """,
            new { limit });

        return rows.ToList();
    }
}

public sealed class DownloadLogRepository(IDbConnectionFactory connections) : IDownloadLogRepository
{
    public async Task AppendAsync(DownloadLogEntry entry, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        await connection.ExecuteAsync(
            """
            INSERT INTO download_log (id, user_id, pack_file_id, pack_version, downloaded_at)
            VALUES (@Id, @UserId, @PackFileId, @PackVersion, @DownloadedAt)
            """,
            new { entry.Id, entry.UserId, entry.PackFileId, entry.PackVersion, entry.DownloadedAt });
    }

    public async Task<IReadOnlyList<Guid>> GetUserIdsWhoDownloadedPackAsync(Guid packId, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        var ids = await connection.QueryAsync<Guid>(
            """
            SELECT DISTINCT d.user_id
            FROM download_log d
            JOIN pack_file f ON f.id = d.pack_file_id
            WHERE f.pack_id = @packId
            """,
            new { packId });

        return ids.ToList();
    }
}

public sealed class CommunitySessionRepository(IDbConnectionFactory connections) : ICommunitySessionRepository
{
    public async Task<IReadOnlyList<CommunitySession>> GetUpcomingAsync(DateTimeOffset from, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        var rows = await connection.QueryAsync<SessionRow>(
            """
            SELECT id, title, description, starts_at, duration_minutes, join_url, required_plan_codes
            FROM community_session WHERE starts_at >= @from ORDER BY starts_at
            """,
            new { from });

        return rows
            .Select(r => new CommunitySession(
                r.Id, r.Title, r.Description, r.StartsAt, r.DurationMinutes, r.JoinUrl, r.RequiredPlanCodes))
            .ToList();
    }

    public async Task UpsertAsync(CommunitySession session, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        await connection.ExecuteAsync(
            """
            INSERT INTO community_session (id, title, description, starts_at, duration_minutes,
                                           join_url, required_plan_codes)
            VALUES (@Id, @Title, @Description, @StartsAt, @DurationMinutes, @JoinUrl, @Plans)
            ON CONFLICT (id) DO UPDATE SET
                title = EXCLUDED.title, description = EXCLUDED.description,
                starts_at = EXCLUDED.starts_at, duration_minutes = EXCLUDED.duration_minutes,
                join_url = EXCLUDED.join_url, required_plan_codes = EXCLUDED.required_plan_codes
            """,
            new
            {
                session.Id,
                session.Title,
                session.Description,
                session.StartsAt,
                session.DurationMinutes,
                session.JoinUrl,
                Plans = session.RequiredPlanCodes.ToArray()
            });
    }

    private sealed record SessionRow(
        Guid Id,
        string Title,
        string Description,
        DateTimeOffset StartsAt,
        int DurationMinutes,
        string JoinUrl,
        string[] RequiredPlanCodes);
}

public sealed class DiscordLinkRepository(IDbConnectionFactory connections) : IDiscordLinkRepository
{
    public async Task<DiscordLink?> GetByUserIdAsync(Guid userId, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        return await connection.QuerySingleOrDefaultAsync<DiscordLink>(
            "SELECT user_id, discord_user_id, linked_at, last_applied_role FROM discord_link WHERE user_id = @userId",
            new { userId });
    }

    public async Task<IReadOnlyList<DiscordLink>> GetAllAsync(CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        var rows = await connection.QueryAsync<DiscordLink>(
            "SELECT user_id, discord_user_id, linked_at, last_applied_role FROM discord_link");

        return rows.ToList();
    }

    public async Task UpsertAsync(DiscordLink link, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        await connection.ExecuteAsync(
            """
            INSERT INTO discord_link (user_id, discord_user_id, linked_at, last_applied_role)
            VALUES (@UserId, @DiscordUserId, @LinkedAt, @LastAppliedRole)
            ON CONFLICT (user_id) DO UPDATE SET
                discord_user_id = EXCLUDED.discord_user_id,
                last_applied_role = EXCLUDED.last_applied_role
            """,
            link);
    }

    public async Task DeleteAsync(Guid userId, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        await connection.ExecuteAsync("DELETE FROM discord_link WHERE user_id = @userId", new { userId });
    }
}

public sealed class FiscalInvoiceRepository(IDbConnectionFactory connections) : IFiscalInvoiceRepository
{
    private const string Columns = """
        id, user_id, series, number, issue_date, total_cents, tax_cents, currency,
        stripe_invoice_id, previous_hash, hash, pdf_content_ref, is_rectification, rectifies_invoice_id
        """;

    public async Task<FiscalInvoice?> GetLastInSeriesAsync(string series, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        return await connection.QueryFirstOrDefaultAsync<FiscalInvoice>(
            $"SELECT {Columns} FROM fiscal_invoice WHERE series = @series ORDER BY number DESC LIMIT 1",
            new { series });
    }

    public async Task<FiscalInvoice?> GetByStripeInvoiceIdAsync(string stripeInvoiceId, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        return await connection.QueryFirstOrDefaultAsync<FiscalInvoice>(
            $"SELECT {Columns} FROM fiscal_invoice WHERE stripe_invoice_id = @stripeInvoiceId AND NOT is_rectification",
            new { stripeInvoiceId });
    }

    public async Task<IReadOnlyList<FiscalInvoice>> GetForUserAsync(Guid userId, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        var rows = await connection.QueryAsync<FiscalInvoice>(
            $"SELECT {Columns} FROM fiscal_invoice WHERE user_id = @userId ORDER BY issue_date DESC, number DESC",
            new { userId });

        return rows.ToList();
    }

    public async Task<int> GetNextNumberAsync(string series, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        return await connection.ExecuteScalarAsync<int>(
            "SELECT COALESCE(MAX(number), 0) + 1 FROM fiscal_invoice WHERE series = @series",
            new { series });
    }

    public async Task InsertAsync(FiscalInvoice invoice, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        await connection.ExecuteAsync(
            """
            INSERT INTO fiscal_invoice (id, user_id, series, number, issue_date, total_cents, tax_cents,
                                        currency, stripe_invoice_id, previous_hash, hash, pdf_content_ref,
                                        is_rectification, rectifies_invoice_id)
            VALUES (@Id, @UserId, @Series, @Number, @IssueDate, @TotalCents, @TaxCents,
                    @Currency, @StripeInvoiceId, @PreviousHash, @Hash, @PdfContentRef,
                    @IsRectification, @RectifiesInvoiceId)
            """,
            invoice);
    }
}

public sealed class ConsentRepository(IDbConnectionFactory connections) : IConsentRepository
{
    public async Task RecordAsync(ConsentRecord record, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        await connection.ExecuteAsync(
            """
            INSERT INTO consent_record (id, user_id, visitor_id, analytics, marketing, policy_version, recorded_at)
            VALUES (@Id, @UserId, @VisitorId, @Analytics, @Marketing, @PolicyVersion, @RecordedAt)
            """,
            record);
    }

    public async Task<ConsentRecord?> GetLatestAsync(string visitorId, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        return await connection.QueryFirstOrDefaultAsync<ConsentRecord>(
            """
            SELECT id, user_id, visitor_id, analytics, marketing, policy_version, recorded_at
            FROM consent_record WHERE visitor_id = @visitorId ORDER BY recorded_at DESC LIMIT 1
            """,
            new { visitorId });
    }
}

/// <summary>Serialisation options shared by the repositories that persist JSON columns.</summary>
internal static class SupportJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}
