using Meridiana.Agent.Data;
using Meridiana.Agent.Policy;

namespace Meridiana.Agent.Agent;

public sealed record ClaimOutcome(
    string ClaimId,
    Route Route,
    bool Escalated,
    string Reason,
    decimal? ProposedAmount,
    bool RequiresFullReview,
    string CustomerMessage,
    IReadOnlyList<string> ToolCalls,
    IReadOnlyList<string> Trace);

/// <summary>
/// El loop del agente sobre las cuatro etapas del caso: FNOL, triaje, documentación y
/// propuesta.
///
/// Dos cosas que conviene no perder de vista al leerlo:
///
/// 1. El modelo <b>propone</b> y la política <b>dispone</b>. La salida del modelo entra en
///    <see cref="ClaimPolicy.Decide"/> como una sugerencia más, junto a la póliza y las
///    fechas. Nunca decide sola.
/// 2. Cada paso deja traza. En el curso 3 esa traza se convierte en spans de OpenTelemetry;
///    aquí es una lista de strings, pero la estructura ya está.
/// </summary>
public sealed class ClaimAgent(CaseRepository repository, ILlmClient llm)
{
    private readonly ClaimTools _tools = new(repository);

    public async Task<ClaimOutcome> ResolveAsync(Claim claim, CancellationToken ct)
    {
        var trace = new List<string>();

        // ── 1. FNOL: percibir ──────────────────────────────────────────────────────────
        trace.Add($"FNOL recibido por {claim.Channel}: «{Shorten(claim.Narrative)}»");

        var extraction = await llm.ExtractAsync(claim, ct);
        trace.Add(
            $"Extracción: cobertura sugerida {extraction.SuggestedCoverage}, "
            + $"terceros {(extraction.MentionsThirdParty ? "sí" : "no")}, "
            + $"campos ausentes [{string.Join(", ", extraction.MissingFields)}]");

        // ── 2. Triaje: consultar y decidir ─────────────────────────────────────────────
        var policyResult = _tools.GetPolicy(claim.PolicyNumber);
        trace.Add($"get_policy → {policyResult.Content}");

        var policy = repository.FindPolicy(claim.PolicyNumber);

        var coveragesResult = _tools.GetCoverages(claim.PolicyNumber);
        trace.Add($"get_coverages → {(coveragesResult.Ok ? "ok" : coveragesResult.Content)}");

        var decision = ClaimPolicy.Decide(claim, policy, extraction.SuggestedCoverage);
        trace.Add($"Política → {decision.Route}: {decision.Reason}");

        if (decision.Route == Route.Escalate || policy is null)
        {
            var escalation = _tools.Escalate(claim, decision.Reason);
            trace.Add(escalation.Content);

            return new ClaimOutcome(
                claim.Id,
                Route.Escalate,
                Escalated: true,
                decision.Reason,
                ProposedAmount: null,
                RequiresFullReview: true,
                CustomerMessage:
                    "Hemos recibido tu aviso y lo está revisando una persona del equipo. "
                    + "Te escribimos en cuanto tengamos novedades.",
                _tools.Invocations,
                trace);
        }

        // ── 3. Documentación ───────────────────────────────────────────────────────────
        var documentsResult = _tools.GetMissingDocuments(decision.Route, claim);
        trace.Add($"get_missing_documents → {documentsResult.Content.Replace('\n', ';')}");

        var missing = documentsResult.Content
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.TrimStart(' ', '·'))
            .Where(line => !line.StartsWith("Sin documentación", StringComparison.Ordinal))
            .ToList();

        var message = await llm.DraftDocumentRequestAsync(claim, missing, ct);

        // ── 4. Propuesta ───────────────────────────────────────────────────────────────
        var estimate = _tools.EstimateAmount(decision.Route, policy);
        trace.Add($"estimate_amount → {estimate.Content}");

        var amount = ParseNetAmount(estimate.Content);
        var fullReview = ClaimPolicy.RequiresFullReview(amount);

        trace.Add(
            fullReview
                ? $"Propuesta de {amount:0.00} €: supera {ClaimPolicy.FullReviewThresholdEuros} €, revisión completa."
                : $"Propuesta de {amount:0.00} €: aprobación de un clic.");

        // El agente nunca cierra el expediente: deja la propuesta lista para que alguien la
        // apruebe. Esa frontera es el criterio que el curso 4 tendrá que justificar por
        // escrito ante el AI Act.
        trace.Add("Propuesta preparada. Pendiente de aprobación humana.");

        return new ClaimOutcome(
            claim.Id,
            decision.Route,
            Escalated: false,
            decision.Reason,
            amount,
            fullReview,
            message,
            _tools.Invocations,
            trace);
    }

    private static decimal ParseNetAmount(string estimate)
    {
        // "Estimación: 1850,00 € · franquicia 300,00 € · a indemnizar 1550,00 €"
        var marker = estimate.LastIndexOf("a indemnizar ", StringComparison.Ordinal);

        if (marker < 0)
        {
            return 0m;
        }

        var tail = estimate[(marker + "a indemnizar ".Length)..].Replace("€", string.Empty).Trim();

        return decimal.TryParse(tail, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var value)
            ? value
            : 0m;
    }

    private static string Shorten(string text) =>
        text.Length <= 90 ? text : text[..87] + "…";
}
