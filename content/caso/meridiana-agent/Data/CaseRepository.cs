using System.Text.Json;
using System.Text.Json.Serialization;

namespace Meridiana.Agent.Data;

public sealed record Coverage(
    [property: JsonPropertyName("codigo")] string Code,
    [property: JsonPropertyName("nombre")] string Name,
    [property: JsonPropertyName("descripcion")] string Description,
    [property: JsonPropertyName("franquicia")] int DeductibleEuros,
    [property: JsonPropertyName("limite")] int? LimitEuros);

public sealed record Validity(
    [property: JsonPropertyName("desde")] DateOnly From,
    [property: JsonPropertyName("hasta")] DateOnly To);

public sealed record Policy(
    [property: JsonPropertyName("numero")] string Number,
    [property: JsonPropertyName("tomador")] string Holder,
    [property: JsonPropertyName("matricula")] string PlateNumber,
    [property: JsonPropertyName("vehiculo")] string Vehicle,
    [property: JsonPropertyName("modalidad")] string Kind,
    [property: JsonPropertyName("vigencia")] Validity Validity,
    [property: JsonPropertyName("coberturas")] IReadOnlyList<string> Coverages,
    [property: JsonPropertyName("franquicia_danos_propios")] int? OwnDamageDeductible);

public sealed record Claim(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("poliza")] string PolicyNumber,
    [property: JsonPropertyName("matricula")] string? PlateNumber,
    [property: JsonPropertyName("fecha_ocurrencia")] DateOnly OccurredOn,
    [property: JsonPropertyName("fecha_aviso")] DateOnly ReportedOn,
    [property: JsonPropertyName("canal")] string Channel,
    [property: JsonPropertyName("relato_fnol")] string Narrative,
    [property: JsonPropertyName("clasificacion_esperada")] string ExpectedClassification,
    [property: JsonPropertyName("requiere_derivacion")] bool ShouldEscalate,
    [property: JsonPropertyName("motivo_derivacion")] string? EscalationReason,
    [property: JsonPropertyName("duplicado_de")] string? DuplicateOf = null);

/// <summary>
/// Lee los datos sintéticos del caso. En un sistema real esto sería el core de pólizas;
/// aquí son tres ficheros JSON, y eso basta para que el agente tenga tools de verdad contra
/// las que trabajar.
/// </summary>
public sealed class CaseRepository
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public IReadOnlyList<Coverage> Coverages { get; }
    public IReadOnlyList<Policy> Policies { get; }
    public IReadOnlyList<Claim> Claims { get; }

    private CaseRepository(
        IReadOnlyList<Coverage> coverages,
        IReadOnlyList<Policy> policies,
        IReadOnlyList<Claim> claims)
    {
        Coverages = coverages;
        Policies = policies;
        Claims = claims;
    }

    public static CaseRepository Load(string? dataDirectory = null)
    {
        var directory = dataDirectory ?? FindDataDirectory()
            ?? throw new InvalidOperationException(
                "No se encuentra la carpeta de datos. Ejecuta primero: python ../generar_datos.py");

        return new CaseRepository(
            Read<Coverage>(Path.Combine(directory, "coberturas.json")),
            Read<Policy>(Path.Combine(directory, "polizas.json")),
            Read<Claim>(Path.Combine(directory, "siniestros.json")));
    }

    public Policy? FindPolicy(string number) =>
        Policies.FirstOrDefault(p => p.Number == number);

    public Claim? FindClaim(string id) =>
        Claims.FirstOrDefault(c => c.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    public IReadOnlyList<Coverage> CoveragesOf(Policy policy) =>
        Coverages.Where(c => policy.Coverages.Contains(c.Code)).ToList();

    private static List<T> Read<T>(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Falta {Path.GetFileName(path)}. Genera los datos primero.", path);
        }

        return JsonSerializer.Deserialize<List<T>>(File.ReadAllText(path), Options) ?? [];
    }

    /// <summary>
    /// Sube desde el binario buscando <c>caso/datos</c>. Así el repo funciona igual desde el
    /// directorio del proyecto, desde la raíz o publicado.
    /// </summary>
    private static string? FindDataDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            foreach (var candidate in new[]
                     {
                         Path.Combine(directory.FullName, "datos"),
                         Path.Combine(directory.FullName, "caso", "datos"),
                         Path.Combine(directory.FullName, "content", "caso", "datos")
                     })
            {
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }
            }

            directory = directory.Parent;
        }

        return null;
    }
}
