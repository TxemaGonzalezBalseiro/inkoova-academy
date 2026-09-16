using Meridiana.Agent.Agent;
using Meridiana.Agent.Data;

// Agente de siniestros de Meridiana (C-00). Punto de llegada del curso 2 y de partida del 3.
//
//   dotnet run -- --claim SIN-2026-0007
//   dotnet run -- --all
//   dotnet run -- --all --check     comprueba las derivaciones contra lo esperado

var claimId = ValueOf(args, "--claim");
var all = args.Contains("--all");
var check = args.Contains("--check");

if (!all && claimId is null)
{
    Console.Error.WriteLine(
        """
        Uso:
          dotnet run -- --claim SIN-2026-0007    resuelve un siniestro
          dotnet run -- --all                    resuelve los 31 siniestros sintéticos
          dotnet run -- --all --check            además, compara con lo esperado
        """);
    return 2;
}

CaseRepository repository;

try
{
    repository = CaseRepository.Load();
}
catch (Exception ex) when (ex is InvalidOperationException or FileNotFoundException)
{
    Console.Error.WriteLine(ex.Message);
    return 2;
}

// Proveedor por defecto: stub determinista. Ver el README para usar un modelo real.
var provider = Environment.GetEnvironmentVariable("MERIDIANA_PROVIDER") ?? "stub";

ILlmClient llm = provider.ToLowerInvariant() switch
{
    "stub" => new StubLlmClient(),
    _ => throw new NotSupportedException(
        $"El proveedor '{provider}' aún no está implementado. Se escribe en el bloque B1 del curso 2.")
};

var agent = new ClaimAgent(repository, llm);
var claims = all
    ? repository.Claims
    : [repository.FindClaim(claimId!) ?? throw new ArgumentException($"No existe el siniestro '{claimId}'.")];

var mismatches = 0;

foreach (var claim in claims)
{
    var outcome = await agent.ResolveAsync(claim, CancellationToken.None);

    Console.WriteLine();
    Console.WriteLine($"═══ {outcome.ClaimId} · póliza {claim.PolicyNumber} ═══");

    if (all)
    {
        // En modo lote solo interesa el veredicto; la traza completa satura la consola.
        Console.WriteLine(
            outcome.Escalated
                ? $"  DERIVADO · {outcome.Reason}"
                : $"  {outcome.Route} · {outcome.ProposedAmount:0.00} €"
                  + (outcome.RequiresFullReview ? " · revisión completa" : " · aprobación de un clic"));
    }
    else
    {
        foreach (var step in outcome.Trace)
        {
            Console.WriteLine($"  {step}");
        }

        Console.WriteLine();
        Console.WriteLine("  Tools invocadas:");
        foreach (var call in outcome.ToolCalls)
        {
            Console.WriteLine($"    {call}");
        }

        Console.WriteLine();
        Console.WriteLine("  Mensaje al asegurado:");
        foreach (var line in outcome.CustomerMessage.Split('\n'))
        {
            Console.WriteLine($"    {line}");
        }
    }

    if (check && outcome.Escalated != claim.ShouldEscalate)
    {
        mismatches++;
        Console.WriteLine(
            $"  ✕ ESPERADO {(claim.ShouldEscalate ? "derivar" : "tramitar")}, "
            + $"OBTENIDO {(outcome.Escalated ? "derivar" : "tramitar")}"
            + (claim.EscalationReason is null ? string.Empty : $" · {claim.EscalationReason}"));
    }
}

if (check)
{
    Console.WriteLine();
    Console.WriteLine(
        mismatches == 0
            ? $"✓ Las {claims.Count} decisiones de derivación coinciden con lo esperado."
            : $"✕ {mismatches} de {claims.Count} decisiones de derivación no coinciden.");

    // Código de salida distinto de cero: así esto puede ser un paso de CI en el curso 3.
    return mismatches == 0 ? 0 : 1;
}

return 0;

static string? ValueOf(string[] args, string name)
{
    var index = Array.IndexOf(args, name);
    return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
}
