using Meridiana.Agent.Data;

namespace Meridiana.Agent.Policy;

public enum Route
{
    OwnDamage,
    ThirdParty,
    Glass,
    Theft,
    RoadsideAssistance,
    /// <summary>Fuera del flujo automático: lo ve una persona.</summary>
    Escalate
}

public sealed record PolicyDecision(Route Route, string Reason, bool RequiresHumanReview);

/// <summary>
/// Las reglas que no se negocian, en código y fuera del prompt.
///
/// Todo lo que hay aquí podría "explicarse" al modelo en el system prompt. No se hace, y esa
/// es la lección entera del caso: un modelo puede ser persuadido por el texto que le llega
/// del asegurado; un <c>if</c> no. Si una regla tiene consecuencias reguladas, vive aquí.
/// </summary>
public static class ClaimPolicy
{
    /// <summary>Por encima de esto la propuesta pasa por revisión completa, no por un clic.</summary>
    public const int FullReviewThresholdEuros = 1_500;

    /// <summary>
    /// Términos que indican daño personal. Deliberadamente amplio: un falso positivo cuesta
    /// que un humano mire un expediente de más; un falso negativo cuesta que un agente
    /// decida sobre una lesión.
    /// </summary>
    private static readonly string[] InjuryMarkers =
    [
        "herid", "lesion", "urgencias", "hospital", "ambulancia", "dolor",
        "cuello", "latigazo", "fractura", "se ha hecho daño", "se hizo daño", "muñeca"
    ];

    /// <summary>
    /// Marcadores de manipulación de instrucciones. No es un filtro de seguridad completo
    /// —eso se trata en el curso 3—, sino la comprobación mínima que impide que el relato
    /// del asegurado se lea como si fuera una orden del sistema.
    /// </summary>
    private static readonly string[] InjectionMarkers =
    [
        "ignora tus instrucciones", "ignore previous", "system prompt",
        "sin revisión humana", "aprueba este siniestro", "eres un asistente"
    ];

    public static PolicyDecision Decide(Claim claim, Data.Policy? policy, string modelSuggestedRoute)
    {
        // 1. Sin póliza no hay nada que decidir.
        if (policy is null)
        {
            return new PolicyDecision(Route.Escalate, "No se encuentra la póliza del siniestro.", true);
        }

        // 2. Lesiones: el agente no decide nunca. Es la regla que se mantiene desde el
        //    primer bloque del curso 2 hasta el expediente técnico del curso 4.
        if (MentionsInjury(claim.Narrative))
        {
            return new PolicyDecision(
                Route.Escalate, "El relato menciona daños personales: decide una persona.", true);
        }

        // 3. Manipulación de instrucciones: se deriva y se registra, no se "responde".
        if (LooksLikeInjection(claim.Narrative))
        {
            return new PolicyDecision(
                Route.Escalate, "El relato contiene un intento de manipular al agente.", true);
        }

        // 4. Vigencia. Un siniestro fuera de póliza no se tramita automáticamente aunque el
        //    modelo lo clasifique perfectamente.
        if (claim.OccurredOn < policy.Validity.From || claim.OccurredOn > policy.Validity.To)
        {
            return new PolicyDecision(
                Route.Escalate,
                $"El siniestro ({claim.OccurredOn:dd/MM/yyyy}) cae fuera de la vigencia de la póliza "
                + $"({policy.Validity.From:dd/MM/yyyy} a {policy.Validity.To:dd/MM/yyyy}).",
                true);
        }

        // 5. Datos esenciales ausentes. Un campo que falta es un campo que falta: no se
        //    rellena con lo más probable.
        if (string.IsNullOrWhiteSpace(claim.PlateNumber))
        {
            return new PolicyDecision(
                Route.Escalate, "Falta la matrícula: no se puede identificar el vehículo.", true);
        }

        // 6. Solo aquí se tiene en cuenta lo que el modelo propuso, y aún así se comprueba
        //    contra las coberturas contratadas.
        var route = ParseRoute(modelSuggestedRoute);

        if (!IsCovered(route, policy))
        {
            return new PolicyDecision(
                Route.Escalate,
                $"La vía propuesta ({route}) no está cubierta por la modalidad '{policy.Kind}'.",
                true);
        }

        return new PolicyDecision(route, "Vía determinada por coberturas y relato.", false);
    }

    public static bool MentionsInjury(string narrative) =>
        InjuryMarkers.Any(marker => narrative.Contains(marker, StringComparison.OrdinalIgnoreCase));

    public static bool LooksLikeInjection(string narrative) =>
        InjectionMarkers.Any(marker => narrative.Contains(marker, StringComparison.OrdinalIgnoreCase));

    public static bool RequiresFullReview(decimal proposedAmountEuros) =>
        proposedAmountEuros > FullReviewThresholdEuros;

    private static Route ParseRoute(string suggested) => suggested.ToUpperInvariant() switch
    {
        "LUNAS" => Route.Glass,
        "ROBO" => Route.Theft,
        "ASISTENCIA" => Route.RoadsideAssistance,
        "RC_OBLIGATORIA" => Route.ThirdParty,
        "DANOS_PROPIOS" => Route.OwnDamage,
        // Una clasificación que no se entiende no se interpreta: se deriva.
        _ => Route.Escalate
    };

    private static bool IsCovered(Route route, Data.Policy policy) => route switch
    {
        Route.Glass => policy.Coverages.Contains("LUNAS"),
        Route.Theft => policy.Coverages.Contains("ROBO"),
        Route.RoadsideAssistance => policy.Coverages.Contains("ASISTENCIA"),
        Route.OwnDamage => policy.Coverages.Contains("DANOS_PROPIOS"),
        Route.ThirdParty => policy.Coverages.Contains("RC_OBLIGATORIA"),
        _ => false
    };
}
