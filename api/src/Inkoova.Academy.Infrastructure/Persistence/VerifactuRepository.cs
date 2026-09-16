using Dapper;
using Inkoova.Academy.Application.Abstractions;
using Inkoova.Academy.Domain.Billing;

namespace Inkoova.Academy.Infrastructure.Persistence;

public sealed class VerifactuRepository(IDbConnectionFactory connections) : IVerifactuRepository
{
    private const string Columns = """
        id, invoice_id, kind, issuer_tax_id, issuer_name, invoice_type, series_number,
        issue_date, description, total_cents, tax_cents, currency, rectifies,
        previous_hash, hash, hash_input, generated_at, submission_state,
        aeat_submission_id, submitted_at, submission_error, submission_attempts
        """;

    public async Task<VerifactuRecord?> GetLastAsync(string issuerTaxId, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);

        // Por marca temporal de generación y, a igualdad, por identificador: los identificadores
        // son UUID v7 y crecen con el tiempo, así que dos asientos del mismo instante siguen
        // teniendo un orden estable. Sin desempate, la cadena se leería distinta cada vez.
        var row = await connection.QuerySingleOrDefaultAsync<Row>(
            $"""
             SELECT {Columns} FROM verifactu_record
             WHERE issuer_tax_id = @issuerTaxId
             ORDER BY generated_at DESC, id DESC
             LIMIT 1
             """,
            new { issuerTaxId });

        return row?.ToDomain();
    }

    public async Task<IReadOnlyList<VerifactuRecord>> GetChainAsync(string issuerTaxId, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);

        var rows = await connection.QueryAsync<Row>(
            $"""
             SELECT {Columns} FROM verifactu_record
             WHERE issuer_tax_id = @issuerTaxId
             ORDER BY generated_at, id
             """,
            new { issuerTaxId });

        return [.. rows.Select(r => r.ToDomain())];
    }

    public async Task<IReadOnlyList<string>> GetIssuersAsync(CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);

        var rows = await connection.QueryAsync<string>(
            "SELECT DISTINCT issuer_tax_id FROM verifactu_record ORDER BY issuer_tax_id");

        return [.. rows];
    }

    /// <summary>
    /// El asiento por su huella, para poder identificar al anterior en el encadenamiento del
    /// XML. Por emisor además de por huella: cada NIF tiene su cadena, y aunque una colisión de
    /// SHA-256 no es cosa de este siglo, buscar por la clave entera cuesta lo mismo.
    /// </summary>
    public async Task<VerifactuRecord?> GetByHashAsync(
        string issuerTaxId, string hash, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);

        var row = await connection.QuerySingleOrDefaultAsync<Row>(
            $"SELECT {Columns} FROM verifactu_record WHERE issuer_tax_id = @issuerTaxId AND hash = @hash",
            new { issuerTaxId, hash });

        return row?.ToDomain();
    }

    public async Task<VerifactuRecord?> GetByInvoiceAsync(
        Guid invoiceId, VerifactuKind kind, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);

        var row = await connection.QuerySingleOrDefaultAsync<Row>(
            $"SELECT {Columns} FROM verifactu_record WHERE invoice_id = @invoiceId AND kind = @kind",
            new { invoiceId, kind = KindText(kind) });

        return row?.ToDomain();
    }

    public async Task<IReadOnlyList<VerifactuRecord>> GetUnsubmittedAsync(
        string issuerTaxId, int limit, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);

        // Los más antiguos primero, con el mismo desempate que la cadena: el lote se remite en
        // el orden en que se encadenó. Mandar un registro antes que su predecesor es pedirle a
        // la AEAT que valide un encadenamiento que todavía no le consta.
        var rows = await connection.QueryAsync<Row>(
            $"""
             SELECT {Columns} FROM verifactu_record
             WHERE issuer_tax_id = @issuerTaxId
               AND submission_state IN ('pending', 'rejected')
             ORDER BY generated_at, id
             LIMIT @limit
             """,
            new { issuerTaxId, limit });

        return [.. rows.Select(r => r.ToDomain())];
    }

    public async Task<int> CountUnsubmittedAsync(CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);

        return await connection.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM verifactu_record WHERE submission_state IN ('pending', 'rejected')");
    }

    public async Task<VerifactuFlow?> GetFlowAsync(string issuerTaxId, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);

        return await connection.QuerySingleOrDefaultAsync<VerifactuFlow>(
            """
            SELECT issuer_tax_id AS IssuerTaxId, wait_seconds AS WaitSeconds,
                   next_allowed_at AS NextAllowedAt
            FROM verifactu_flow WHERE issuer_tax_id = @issuerTaxId
            """,
            new { issuerTaxId });
    }

    public async Task SaveFlowAsync(VerifactuFlow flow, DateTimeOffset sentAt, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);

        await connection.ExecuteAsync(
            """
            INSERT INTO verifactu_flow (issuer_tax_id, wait_seconds, next_allowed_at, last_sent_at)
            VALUES (@IssuerTaxId, @WaitSeconds, @NextAllowedAt, @sentAt)
            ON CONFLICT (issuer_tax_id) DO UPDATE SET
                wait_seconds = EXCLUDED.wait_seconds,
                next_allowed_at = EXCLUDED.next_allowed_at,
                last_sent_at = EXCLUDED.last_sent_at,
                updated_at = now()
            """,
            new { flow.IssuerTaxId, flow.WaitSeconds, flow.NextAllowedAt, sentAt });
    }

    public async Task InsertAsync(VerifactuRecord record, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);

        // Sin ON CONFLICT: un asiento no se reescribe nunca. Si dos altas llegaran para la misma
        // factura, el índice único tiene que fallar y no dejar la última en silencio.
        await connection.ExecuteAsync(
            """
            INSERT INTO verifactu_record
                (id, invoice_id, kind, issuer_tax_id, issuer_name, invoice_type, series_number,
                 issue_date, description, total_cents, tax_cents, currency, rectifies,
                 previous_hash, hash, hash_input, generated_at, submission_state,
                 aeat_submission_id, submitted_at, submission_error, submission_attempts)
            VALUES
                (@Id, @InvoiceId, @Kind, @IssuerTaxId, @IssuerName, @InvoiceType, @SeriesNumber,
                 @IssueDate, @Description, @TotalCents, @TaxCents, @Currency, @Rectifies,
                 @PreviousHash, @Hash, @HashInput, @GeneratedAt, @SubmissionState,
                 @AeatSubmissionId, @SubmittedAt, @SubmissionError, @SubmissionAttempts)
            """,
            new
            {
                record.Id,
                record.InvoiceId,
                Kind = KindText(record.Kind),
                record.IssuerTaxId,
                record.IssuerName,
                record.InvoiceType,
                record.SeriesNumber,
                record.IssueDate,
                record.Description,
                record.TotalCents,
                record.TaxCents,
                record.Currency,
                record.Rectifies,
                record.PreviousHash,
                record.Hash,
                record.HashInput,
                record.GeneratedAt,
                SubmissionState = StateText(record.State),
                record.AeatSubmissionId,
                record.SubmittedAt,
                record.SubmissionError,
                record.SubmissionAttempts
            });
    }

    public async Task UpdateSubmissionAsync(VerifactuRecord record, CancellationToken ct)
    {
        using var connection = await connections.OpenAsync(ct);

        // Solo las columnas del envío. Lo declarado no se toca: cambiarlo invalidaría la huella
        // y con ella toda la cadena posterior.
        await connection.ExecuteAsync(
            """
            UPDATE verifactu_record SET
                submission_state = @SubmissionState,
                aeat_submission_id = @AeatSubmissionId,
                submitted_at = @SubmittedAt,
                submission_error = @SubmissionError,
                submission_attempts = @SubmissionAttempts
            WHERE id = @Id
            """,
            new
            {
                record.Id,
                SubmissionState = StateText(record.State),
                record.AeatSubmissionId,
                record.SubmittedAt,
                record.SubmissionError,
                record.SubmissionAttempts
            });
    }

    private static string KindText(VerifactuKind kind) =>
        kind == VerifactuKind.Anulacion ? "anulacion" : "alta";

    private static string StateText(SubmissionState state) => state switch
    {
        SubmissionState.Sent => "sent",
        SubmissionState.Rejected => "rejected",
        SubmissionState.NotRequired => "not_required",
        _ => "pending"
    };

    private sealed record Row(
        Guid Id,
        Guid InvoiceId,
        string Kind,
        string IssuerTaxId,
        string IssuerName,
        string InvoiceType,
        string SeriesNumber,
        DateOnly IssueDate,
        string Description,
        long TotalCents,
        long TaxCents,
        string Currency,
        string? Rectifies,
        string PreviousHash,
        string Hash,
        string HashInput,
        DateTimeOffset GeneratedAt,
        string SubmissionState,
        string? AeatSubmissionId,
        DateTimeOffset? SubmittedAt,
        string SubmissionError,
        int SubmissionAttempts)
    {
        public VerifactuRecord ToDomain() =>
            VerifactuRecord.Rehydrate(
                Id, InvoiceId,
                Kind == "anulacion" ? VerifactuKind.Anulacion : VerifactuKind.Alta,
                IssuerTaxId, IssuerName, InvoiceType, SeriesNumber, IssueDate, Description,
                TotalCents, TaxCents, Currency, Rectifies, PreviousHash, Hash, HashInput,
                GeneratedAt, StateFrom(SubmissionState), AeatSubmissionId, SubmittedAt,
                SubmissionError, SubmissionAttempts);

        private static SubmissionState StateFrom(string value) => value switch
        {
            "sent" => Domain.Billing.SubmissionState.Sent,
            "rejected" => Domain.Billing.SubmissionState.Rejected,
            "not_required" => Domain.Billing.SubmissionState.NotRequired,
            _ => Domain.Billing.SubmissionState.Pending
        };
    }
}
