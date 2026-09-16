using Dapper;
using Inkoova.Academy.Application.Abstractions;
using Inkoova.Academy.Domain.Identities;

namespace Inkoova.Academy.Infrastructure.Persistence;

public sealed class AcademyIdentityRepository(IDbConnectionFactory connections) : IAcademyIdentityRepository
{
    private const string Columns = """
        id, slug, name, tagline, logo_url, public_domain, support_email,
        smtp_host, smtp_port, smtp_username, smtp_password, smtp_security,
        from_address, from_name, is_default, is_active,
        legal_name AS LegalName, tax_id AS TaxId, legal_address AS LegalAddress,
        registry_details AS RegistryDetails, legal_email AS LegalEmail,
        linkedin_url AS LinkedInUrl, company_url AS CompanyUrl,
        invoice_type AS InvoiceType, corrective_invoice_type AS CorrectiveInvoiceType
        """;

    public async Task<IReadOnlyList<AcademyIdentity>> GetAllAsync(CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        var rows = await connection.QueryAsync<IdentityRow>(
            $"SELECT {Columns} FROM academy_identity ORDER BY is_default DESC, name");

        return [.. rows.Select(r => r.ToDomain())];
    }

    public async Task<AcademyIdentity?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        var row = await connection.QuerySingleOrDefaultAsync<IdentityRow>(
            $"SELECT {Columns} FROM academy_identity WHERE id = @id", new { id });

        return row?.ToDomain();
    }

    public async Task<AcademyIdentity?> GetBySlugAsync(string slug, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        var row = await connection.QuerySingleOrDefaultAsync<IdentityRow>(
            $"SELECT {Columns} FROM academy_identity WHERE slug = @slug", new { slug });

        return row?.ToDomain();
    }

    public async Task<AcademyIdentity> GetDefaultAsync(CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);

        // El ORDER BY es un cinturón: el índice único garantiza que no hay dos principales, y
        // si por lo que sea no hubiera ninguna, se coge la más antigua antes que reventar. Una
        // academia sin marca no puede mandar un correo ni pintar su cabecera.
        var row = await connection.QuerySingleOrDefaultAsync<IdentityRow>(
            $"SELECT {Columns} FROM academy_identity ORDER BY is_default DESC, created_at LIMIT 1");

        return row?.ToDomain()
               ?? throw new InvalidOperationException(
                   "No hay ninguna identidad. ¿Se ha aplicado la migración V012?");
    }

    public async Task<AcademyIdentity> GetForHostAsync(string? host, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return await GetDefaultAsync(ct);
        }

        // Se comparan en memoria y no en SQL a propósito: la regla de qué hosts son el mismo
        // —puerto, «www.», mayúsculas— vive en el dominio, y son un puñado de filas. Con un
        // LIKE aquí habría dos reglas y un día dirían cosas distintas.
        var todas = await GetAllAsync(ct);
        var suya = todas.FirstOrDefault(i => i.IsActive && i.MatchesHost(host));

        // Un dominio que no es de nadie cae en la principal en vez de fallar: es lo que pasa al
        // entrar por una IP, por un dominio de vista previa o desde un job sin petición.
        return suya ?? await GetDefaultAsync(ct);
    }

    public async Task UpsertAsync(AcademyIdentity identity, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        await connection.ExecuteAsync(
            """
            INSERT INTO academy_identity
                (id, slug, name, tagline, logo_url, public_domain, support_email,
                 smtp_host, smtp_port, smtp_username, smtp_password, smtp_security,
                 from_address, from_name, is_default, is_active,
                 legal_name, tax_id, legal_address, registry_details, legal_email,
                 linkedin_url, company_url, invoice_type, corrective_invoice_type)
            VALUES
                (@Id, @Slug, @Name, @Tagline, @LogoUrl, @PublicDomain, @SupportEmail,
                 @SmtpHost, @SmtpPort, @SmtpUsername, @SmtpPassword, @SmtpSecurity,
                 @FromAddress, @FromName, @IsDefault, @IsActive,
                 @LegalName, @TaxId, @LegalAddress, @RegistryDetails, @LegalEmail,
                 @LinkedInUrl, @CompanyUrl, @InvoiceType, @CorrectiveInvoiceType)
            ON CONFLICT (id) DO UPDATE SET
                slug = EXCLUDED.slug,
                name = EXCLUDED.name,
                tagline = EXCLUDED.tagline,
                logo_url = EXCLUDED.logo_url,
                public_domain = EXCLUDED.public_domain,
                support_email = EXCLUDED.support_email,
                smtp_host = EXCLUDED.smtp_host,
                smtp_port = EXCLUDED.smtp_port,
                smtp_username = EXCLUDED.smtp_username,
                smtp_password = EXCLUDED.smtp_password,
                smtp_security = EXCLUDED.smtp_security,
                from_address = EXCLUDED.from_address,
                from_name = EXCLUDED.from_name,
                is_active = EXCLUDED.is_active,
                legal_name = EXCLUDED.legal_name,
                tax_id = EXCLUDED.tax_id,
                legal_address = EXCLUDED.legal_address,
                registry_details = EXCLUDED.registry_details,
                legal_email = EXCLUDED.legal_email,
                linkedin_url = EXCLUDED.linkedin_url,
                company_url = EXCLUDED.company_url,
                invoice_type = EXCLUDED.invoice_type,
                corrective_invoice_type = EXCLUDED.corrective_invoice_type,
                updated_at = now()
            """,
            new
            {
                identity.Id,
                identity.Slug,
                identity.Name,
                identity.Tagline,
                identity.LogoUrl,
                identity.PublicDomain,
                identity.SupportEmail,
                SmtpHost = identity.Mailbox.Host,
                SmtpPort = identity.Mailbox.Port,
                SmtpUsername = identity.Mailbox.Username,
                SmtpPassword = identity.Mailbox.EncryptedPassword,
                SmtpSecurity = identity.Mailbox.Security,
                FromAddress = identity.Mailbox.FromAddress,
                FromName = identity.Mailbox.FromName,
                identity.IsDefault,
                identity.IsActive,
                LegalName = identity.Legal.LegalName,
                TaxId = identity.Legal.TaxId,
                LegalAddress = identity.Legal.Address,
                RegistryDetails = identity.Legal.RegistryDetails,
                LegalEmail = identity.Legal.Email,
                LinkedInUrl = identity.Legal.LinkedInUrl,
                CompanyUrl = identity.Legal.CompanyUrl,
                InvoiceType = identity.Legal.InvoiceType,
                CorrectiveInvoiceType = identity.Legal.CorrectiveInvoiceType
            });
    }

    /// <summary>
    /// El cambio de principal, en una transacción. Se quita primero y se pone después: al revés,
    /// el índice único rechazaría el segundo UPDATE por haber dos principales a la vez.
    /// </summary>
    public async Task<bool> MakeDefaultAsync(Guid id, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);
        using var transaction = connection.BeginTransaction();

        await connection.ExecuteAsync(
            "UPDATE academy_identity SET is_default = false WHERE is_default",
            transaction: transaction);

        var updated = await connection.ExecuteAsync(
            "UPDATE academy_identity SET is_default = true, updated_at = now() WHERE id = @id AND is_active",
            new { id },
            transaction);

        if (updated == 0)
        {
            // Ni existe ni está activa: se deshace para no dejar la academia sin principal.
            transaction.Rollback();
            return false;
        }

        transaction.Commit();
        return true;
    }

    // Con JOIN hay que cualificar: `name` y `id` existen en las dos tablas.
    private const string PrefixedColumns = """
        i.id, i.slug, i.name, i.tagline, i.logo_url, i.public_domain, i.support_email,
        i.smtp_host, i.smtp_port, i.smtp_username, i.smtp_password, i.smtp_security,
        i.from_address, i.from_name, i.is_default, i.is_active,
        i.legal_name AS LegalName, i.tax_id AS TaxId, i.legal_address AS LegalAddress,
        i.registry_details AS RegistryDetails, i.legal_email AS LegalEmail,
        i.linkedin_url AS LinkedInUrl, i.company_url AS CompanyUrl,
        i.invoice_type AS InvoiceType, i.corrective_invoice_type AS CorrectiveInvoiceType
        """;

    private sealed record IdentityRow(
        Guid Id,
        string Slug,
        string Name,
        string Tagline,
        string LogoUrl,
        string PublicDomain,
        string SupportEmail,
        string SmtpHost,
        int SmtpPort,
        string SmtpUsername,
        string SmtpPassword,
        string SmtpSecurity,
        string FromAddress,
        string FromName,
        bool IsDefault,
        bool IsActive,
        string LegalName,
        string TaxId,
        string LegalAddress,
        string RegistryDetails,
        string LegalEmail,
        string LinkedInUrl,
        string CompanyUrl,
        string InvoiceType,
        string CorrectiveInvoiceType)
    {
        public AcademyIdentity ToDomain() =>
            AcademyIdentity.Rehydrate(
                Id, Slug, Name, Tagline, LogoUrl, PublicDomain, SupportEmail,
                new MailboxSettings(
                    SmtpHost, SmtpPort, SmtpUsername, SmtpPassword, SmtpSecurity,
                    FromAddress, FromName),
                new LegalDetails(
                    LegalName, TaxId, LegalAddress, RegistryDetails, LegalEmail,
                    LinkedInUrl, CompanyUrl, InvoiceType, CorrectiveInvoiceType),
                IsDefault, IsActive);
    }
}
