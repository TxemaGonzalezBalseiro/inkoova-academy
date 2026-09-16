using Meridiana.Agent.Data;

namespace Meridiana.Agent.Agent;

/// <summary>
/// Resultado de una tool. Un error es un dato que el agente puede leer y sobre el que puede
/// reaccionar, no una excepción: si una tool lanza, el loop se rompe y el modelo nunca se
/// entera de que su llamada estaba mal.
/// </summary>
public sealed record ToolResult(bool Ok, string Content)
{
    public static ToolResult Success(string content) => new(true, content);

    public static ToolResult Failure(string reason) => new(false, $"ERROR: {reason}");
}

/// <summary>
/// Las tools del agente. Cada una hace una cosa, su nombre es verbo + objeto y su descripción
/// dice <em>cuándo</em> usarla, no solo qué hace: es lo que separa una tool utilizable de una
/// que el modelo invoca al azar.
/// </summary>
public sealed class ClaimTools(CaseRepository repository)
{
    private readonly List<string> _invocations = [];

    /// <summary>Traza de lo invocado. En producción esto son spans; aquí, una lista.</summary>
    public IReadOnlyList<string> Invocations => _invocations;

    /// <summary>Consulta la póliza. Úsala antes de decidir cualquier cosa sobre coberturas.</summary>
    public ToolResult GetPolicy(string policyNumber)
    {
        _invocations.Add($"get_policy({policyNumber})");

        var policy = repository.FindPolicy(policyNumber);

        if (policy is null)
        {
            // El identificador se devuelve en el error: el modelo necesita saber qué buscó.
            return ToolResult.Failure($"no existe la póliza '{policyNumber}'");
        }

        return ToolResult.Success(
            $"{policy.Number} · {policy.Holder} · {policy.Vehicle} ({policy.PlateNumber}) · "
            + $"{policy.Kind} · vigente del {policy.Validity.From:dd/MM/yyyy} al {policy.Validity.To:dd/MM/yyyy}");
    }

    /// <summary>Lista las coberturas contratadas. Úsala para saber qué se puede tramitar.</summary>
    public ToolResult GetCoverages(string policyNumber)
    {
        _invocations.Add($"get_coverages({policyNumber})");

        var policy = repository.FindPolicy(policyNumber);

        if (policy is null)
        {
            return ToolResult.Failure($"no existe la póliza '{policyNumber}'");
        }

        var coverages = repository.CoveragesOf(policy);

        if (coverages.Count == 0)
        {
            return ToolResult.Success("La póliza no tiene coberturas registradas.");
        }

        return ToolResult.Success(string.Join("\n", coverages.Select(coverage =>
            $"  {coverage.Code}: {coverage.Name} · franquicia {coverage.DeductibleEuros} € · "
            + $"límite {(coverage.LimitEuros is null ? "sin límite" : coverage.LimitEuros + " €")}")));
    }

    /// <summary>
    /// Documentos que faltan según la vía. Úsala antes de escribir al asegurado: pedir dos
    /// veces lo mismo cuesta más confianza que tardar un día más.
    /// </summary>
    public ToolResult GetMissingDocuments(Policy.Route route, Claim claim)
    {
        _invocations.Add($"get_missing_documents({claim.Id}, {route})");

        var documents = route switch
        {
            Policy.Route.Glass => new[] { "Factura o presupuesto del taller de lunas" },
            Policy.Route.Theft => ["Denuncia policial", "Documentación del vehículo"],
            Policy.Route.RoadsideAssistance => ["Factura del servicio de grúa"],
            Policy.Route.ThirdParty => ["Parte amistoso firmado", "Datos del contrario", "Fotos de los daños"],
            Policy.Route.OwnDamage => ["Fotos de los daños", "Presupuesto de reparación"],
            _ => []
        };

        if (documents.Length == 0)
        {
            return ToolResult.Success("Sin documentación pendiente: el expediente no está en vía automática.");
        }

        return ToolResult.Success(string.Join("\n", documents.Select(document => $"  · {document}")));
    }

    /// <summary>
    /// Estimación del importe. Es una estimación de trabajo, no una tasación: la propuesta
    /// final la aprueba una persona.
    /// </summary>
    public ToolResult EstimateAmount(Policy.Route route, Data.Policy policy)
    {
        _invocations.Add($"estimate_amount({policy.Number}, {route})");

        // Importes de referencia del caso. En un sistema real esto sería el baremo.
        var gross = route switch
        {
            Policy.Route.Glass => 320m,
            Policy.Route.Theft => 4_200m,
            Policy.Route.RoadsideAssistance => 140m,
            Policy.Route.ThirdParty => 1_100m,
            Policy.Route.OwnDamage => 1_850m,
            _ => 0m
        };

        var deductible = route == Policy.Route.OwnDamage ? policy.OwnDamageDeductible ?? 0 : 0;
        var net = Math.Max(0, gross - deductible);

        return ToolResult.Success(
            $"Estimación: {gross:0.00} € · franquicia {deductible:0.00} € · a indemnizar {net:0.00} €");
    }

    /// <summary>
    /// Deriva el expediente a la cola humana. Es una tool y no un fallo del loop porque
    /// derivar es un resultado válido y frecuente, no un error.
    /// </summary>
    public ToolResult Escalate(Claim claim, string reason)
    {
        _invocations.Add($"escalate({claim.Id})");
        return ToolResult.Success($"Expediente {claim.Id} derivado a revisión humana. Motivo: {reason}");
    }
}
