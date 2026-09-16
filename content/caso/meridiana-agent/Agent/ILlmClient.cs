using Meridiana.Agent.Data;

namespace Meridiana.Agent.Agent;

/// <summary>Lo que el agente le pide al modelo: extraer del FNOL y clasificar.</summary>
public sealed record FnolExtraction(
    string? PlateNumber,
    DateOnly? OccurredOn,
    string? Location,
    bool MentionsThirdParty,
    bool MentionsInjury,
    string DamageSummary,
    string SuggestedCoverage,
    /// <summary>Qué campos el modelo no ha podido determinar. Van vacíos, no inventados.</summary>
    IReadOnlyList<string> MissingFields);

/// <summary>
/// Puerto del modelo. El agente depende de esta interfaz, nunca de un SDK: cambiar de
/// proveedor no debe tocar el loop, y los evals del curso 3 sustituyen la implementación
/// por una grabación.
/// </summary>
public interface ILlmClient
{
    Task<FnolExtraction> ExtractAsync(Claim claim, CancellationToken ct);

    Task<string> DraftDocumentRequestAsync(Claim claim, IReadOnlyList<string> missingDocuments, CancellationToken ct);
}

/// <summary>
/// Implementación determinista por reglas. No es un modelo: imita lo que uno devolvería.
///
/// Existe por tres motivos, y ninguno es la pereza:
///  1. El repositorio arranca sin API key, sin cuenta y sin gasto.
///  2. Los evals del curso 3 necesitan un baseline reproducible con el que comparar.
///  3. Obliga a que tools, validación y política estén bien: el "modelo" no tapa sus fallos.
/// </summary>
public sealed class StubLlmClient : ILlmClient
{
    private static readonly (string Marker, string Coverage)[] CoverageHints =
    [
        ("luna", "LUNAS"),
        ("parabrisas", "LUNAS"),
        ("cristal", "LUNAS"),
        ("robaron", "ROBO"),
        ("robo", "ROBO"),
        ("abrieron el coche", "ROBO"),
        ("grúa", "ASISTENCIA"),
        ("no arranca", "ASISTENCIA"),
        ("pinch", "ASISTENCIA"),
        ("batería", "ASISTENCIA"),
        ("no respetó", "RC_OBLIGATORIA"),
        ("le di", "RC_OBLIGATORIA"),
        ("marcha atrás", "RC_OBLIGATORIA")
    ];

    public Task<FnolExtraction> ExtractAsync(Claim claim, CancellationToken ct)
    {
        var narrative = claim.Narrative;
        var missing = new List<string>();

        if (string.IsNullOrWhiteSpace(claim.PlateNumber))
        {
            missing.Add("matricula");
        }

        var location = ExtractLocation(narrative);
        if (location is null)
        {
            missing.Add("lugar");
        }

        var coverage = CoverageHints
            .FirstOrDefault(hint => narrative.Contains(hint.Marker, StringComparison.OrdinalIgnoreCase))
            .Coverage ?? "DANOS_PROPIOS";

        // Un relato que parece una orden no se clasifica: se marca como indeterminado y la
        // política lo derivará. El "modelo" no intenta ser listo con esto.
        if (Policy.ClaimPolicy.LooksLikeInjection(narrative))
        {
            coverage = "INDETERMINADO";
            missing.Add("relato_valido");
        }

        return Task.FromResult(new FnolExtraction(
            claim.PlateNumber,
            claim.OccurredOn,
            location,
            MentionsThirdParty: narrative.Contains("otro", StringComparison.OrdinalIgnoreCase)
                                || narrative.Contains("contrario", StringComparison.OrdinalIgnoreCase)
                                || narrative.Contains("camión", StringComparison.OrdinalIgnoreCase),
            MentionsInjury: Policy.ClaimPolicy.MentionsInjury(narrative),
            DamageSummary: Summarise(narrative),
            SuggestedCoverage: coverage,
            MissingFields: missing));
    }

    public Task<string> DraftDocumentRequestAsync(
        Claim claim,
        IReadOnlyList<string> missingDocuments,
        CancellationToken ct)
    {
        if (missingDocuments.Count == 0)
        {
            return Task.FromResult(
                $"Hemos recibido toda la documentación del siniestro {claim.Id}. No necesitamos nada más.");
        }

        var list = string.Join("\n", missingDocuments.Select(document => $"  · {document}"));

        return Task.FromResult(
            $"""
             Para seguir con el siniestro {claim.Id} necesitamos:

             {list}

             Puedes adjuntarlo desde el área de cliente. En cuanto lo tengamos, continuamos.
             """);
    }

    /// <summary>Primera frase del relato, acotada. Suficiente para el resumen del expediente.</summary>
    private static string Summarise(string narrative)
    {
        var firstSentence = narrative.Split('.', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()
                            ?? narrative;

        var trimmed = firstSentence.Trim();
        return trimmed.Length <= 120 ? trimmed : trimmed[..117] + "…";
    }

    private static string? ExtractLocation(string narrative)
    {
        string[] markers = ["M-30", "A-2", "A-6", "N-401", "rotonda", "parking", "garaje", "túnel", "parquin"];

        return markers.FirstOrDefault(marker => narrative.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }
}
