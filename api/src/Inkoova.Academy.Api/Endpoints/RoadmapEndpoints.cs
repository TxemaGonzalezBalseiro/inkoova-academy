using Inkoova.Academy.Api.Common;
using Inkoova.Academy.Application.Abstractions;
using Inkoova.Academy.Domain.Common;

namespace Inkoova.Academy.Api.Endpoints;

public static class RoadmapEndpoints
{
    public static void MapRoadmapEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/roadmap", async (
                IRoadmapRepository roadmap,
                ICourseRepository courses,
                IProgressRepository progress,
                IAccessPolicy accessPolicy,
                HttpContext context,
                CancellationToken ct) =>
            {
                var nodes = await roadmap.GetAllAsync(ct);
                var userId = context.User.UserId();

                // Anonymous visitors see the full map with everything "available", which is
                // the CTA to register; nothing is marked locked to a person who has no state.
                var completedKeys = new HashSet<string>(StringComparer.Ordinal);
                var startedKeys = new HashSet<string>(StringComparer.Ordinal);

                if (userId is { } uid)
                {
                    foreach (var node in nodes.Where(n => n.CourseId is not null))
                    {
                        var course = await courses.GetByIdAsync(node.CourseId!.Value, ct);
                        if (course is null)
                        {
                            continue;
                        }

                        var completed = await progress.GetCompletedLessonIdsAsync(uid, course.Id, ct);
                        var required = course.RequiredLessons.ToList();

                        if (required.Count > 0 && required.All(l => completed.Contains(l.Id)))
                        {
                            completedKeys.Add(node.Key);
                        }
                        else if (completed.Count > 0)
                        {
                            startedKeys.Add(node.Key);
                        }
                    }
                }

                var result = new List<object>(nodes.Count);

                foreach (var node in nodes)
                {
                    string state;

                    if (completedKeys.Contains(node.Key))
                    {
                        state = "completed";
                    }
                    else if (startedKeys.Contains(node.Key))
                    {
                        state = "in_progress";
                    }
                    else if (userId is not null && node.PrerequisiteKeys.Any(k => !completedKeys.Contains(k)))
                    {
                        state = "locked";
                    }
                    else
                    {
                        state = "available";
                    }

                    string? courseSlug = null;
                    if (node.CourseId is { } courseId)
                    {
                        courseSlug = (await courses.GetByIdAsync(courseId, ct))?.Slug.Value;
                    }

                    result.Add(new
                    {
                        key = node.Key,
                        title = node.Title,
                        description = node.Description,
                        order = node.Order,
                        courseSlug,
                        prerequisites = node.PrerequisiteKeys,
                        state
                    });
                }

                return Results.Ok(result);
            })
            .AllowAnonymous()
            .WithTags("Roadmap")
            .WithSummary("Itinerario de aprendizaje con el estado por nodo del alumno.");

        MapAdminEndpoints(app);
    }

    /// <summary>
    /// Edición del itinerario desde el panel.
    ///
    /// Los nodos se enlazan por <c>key</c> y no por identificador: la clave sobrevive a
    /// renombrar el título, que es justo lo que más se hace. Por eso cambiar una clave que otros
    /// nodos usan como prerrequisito se rechaza en vez de dejar el itinerario partido.
    /// </summary>
    private static void MapAdminEndpoints(IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/admin/roadmap")
            .WithTags("Roadmap")
            .RequireAuthorization("admin");

        admin.MapGet("/", async (
                IRoadmapRepository roadmap,
                ICourseRepository courses,
                CancellationToken ct) =>
            {
                var nodes = await roadmap.GetAllAsync(ct);
                var result = new List<object>(nodes.Count);

                foreach (var node in nodes)
                {
                    result.Add(new
                    {
                        id = node.Id,
                        key = node.Key,
                        title = node.Title,
                        description = node.Description,
                        courseId = node.CourseId,
                        courseSlug = node.CourseId is { } id
                            ? (await courses.GetByIdAsync(id, ct))?.Slug.Value
                            : null,
                        order = node.Order,
                        prerequisites = node.PrerequisiteKeys
                    });
                }

                return Results.Ok(result);
            })
            .WithSummary("Nodos del itinerario, en orden.");

        admin.MapPost("/", async (
                RoadmapNodeBody body,
                IRoadmapRepository roadmap,
                IAuditLogRepository audit,
                HttpContext context,
                IClock clock,
                CancellationToken ct) =>
            {
                var nodes = await roadmap.GetAllAsync(ct);
                var existing = body.Id is { } id ? nodes.FirstOrDefault(n => n.Id == id) : null;

                if (body.Id is not null && existing is null)
                {
                    return Error.NotFound("roadmap.not_found", "No existe ese nodo.").ToProblem();
                }

                var problem = Validate(body, nodes, existing);
                if (problem is not null)
                {
                    return problem.ToProblem();
                }

                var node = new RoadmapNode(
                    existing?.Id ?? Guid.CreateVersion7(),
                    body.Key.Trim(),
                    body.Title.Trim(),
                    (body.Description ?? string.Empty).Trim(),
                    body.CourseId,
                    LessonId: null,
                    body.Order,
                    [.. (body.Prerequisites ?? []).Select(k => k.Trim()).Where(k => k.Length > 0)]);

                await roadmap.UpsertAsync(node, ct);

                await Audit(audit, context, clock,
                    body.Id is null ? "roadmap.create" : "roadmap.update", node.Id, node.Key, ct);

                return Results.Ok(new { id = node.Id, key = node.Key });
            })
            .WithSummary("Crea o edita un nodo. Sin Id crea; con Id edita.");

        admin.MapDelete("/{nodeId:guid}", async (
                Guid nodeId,
                IRoadmapRepository roadmap,
                IAuditLogRepository audit,
                HttpContext context,
                IClock clock,
                CancellationToken ct) =>
            {
                var nodes = await roadmap.GetAllAsync(ct);
                var node = nodes.FirstOrDefault(n => n.Id == nodeId);

                if (node is null)
                {
                    return Error.NotFound("roadmap.not_found", "No existe ese nodo.").ToProblem();
                }

                // Borrar un nodo del que cuelgan otros dejaría prerrequisitos apuntando al vacío,
                // y el itinerario los trataría como no cumplidos: todo lo de detrás quedaría
                // bloqueado para siempre sin forma de desbloquearlo.
                var dependants = nodes
                    .Where(n => n.PrerequisiteKeys.Contains(node.Key, StringComparer.Ordinal))
                    .Select(n => n.Title)
                    .ToList();

                if (dependants.Count > 0)
                {
                    return Error.Conflict(
                            "roadmap.has_dependants",
                            $"No se puede borrar: es prerrequisito de {string.Join(", ", dependants)}.")
                        .ToProblem();
                }

                await roadmap.DeleteAsync(nodeId, ct);
                await Audit(audit, context, clock, "roadmap.delete", nodeId, node.Key, ct);

                return Results.NoContent();
            })
            .WithSummary("Borra un nodo. Falla si otro lo tiene como prerrequisito.");
    }

    /// <summary>
    /// Lo que no puede pasar en un itinerario: claves repetidas, prerrequisitos que no existen y
    /// ciclos. Un ciclo no da error en ningún sitio: deja los nodos implicados bloqueados para
    /// siempre, porque cada uno espera al otro.
    /// </summary>
    private static Error? Validate(
        RoadmapNodeBody body, IReadOnlyList<RoadmapNode> nodes, RoadmapNode? existing)
    {
        var key = (body.Key ?? string.Empty).Trim();

        if (key.Length == 0 || key.Length > 60)
        {
            return Error.Validation("roadmap.key_invalid", "La clave del nodo es obligatoria.");
        }

        if (string.IsNullOrWhiteSpace(body.Title))
        {
            return Error.Validation("roadmap.title_invalid", "El nodo necesita un título.");
        }

        if (nodes.Any(n => n.Id != existing?.Id && string.Equals(n.Key, key, StringComparison.Ordinal)))
        {
            return Error.Conflict("roadmap.key_taken", "Ya hay un nodo con esa clave.");
        }

        // Cambiar la clave de un nodo del que cuelgan otros los dejaría apuntando al vacío.
        if (existing is not null && existing.Key != key)
        {
            var dependants = nodes
                .Where(n => n.PrerequisiteKeys.Contains(existing.Key, StringComparer.Ordinal))
                .Select(n => n.Title)
                .ToList();

            if (dependants.Count > 0)
            {
                return Error.Conflict(
                    "roadmap.key_in_use",
                    $"No se puede cambiar la clave: la usan como prerrequisito {string.Join(", ", dependants)}.");
            }
        }

        var prerequisites = (body.Prerequisites ?? [])
            .Select(k => k.Trim())
            .Where(k => k.Length > 0)
            .ToList();

        if (prerequisites.Contains(key, StringComparer.Ordinal))
        {
            return Error.Validation("roadmap.self_prerequisite", "Un nodo no puede ser prerrequisito de sí mismo.");
        }

        var known = nodes.Select(n => n.Key).ToHashSet(StringComparer.Ordinal);

        foreach (var missing in prerequisites.Where(k => !known.Contains(k)))
        {
            return Error.Validation(
                "roadmap.prerequisite_unknown",
                $"El prerrequisito «{missing}» no existe.");
        }

        return CycleThrough(key, prerequisites, nodes, existing?.Id);
    }

    /// <summary>
    /// Busca si los prerrequisitos nuevos crean un ciclo, siguiéndolos hacia atrás. Se hace con
    /// los datos que ya están cargados, así que no cuesta una consulta más.
    /// </summary>
    private static Error? CycleThrough(
        string key,
        IReadOnlyList<string> prerequisites,
        IReadOnlyList<RoadmapNode> nodes,
        Guid? editing)
    {
        var byKey = nodes
            .Where(n => n.Id != editing)
            .ToDictionary(n => n.Key, n => n.PrerequisiteKeys, StringComparer.Ordinal);

        byKey[key] = prerequisites;

        var visiting = new HashSet<string>(StringComparer.Ordinal);
        var settled = new HashSet<string>(StringComparer.Ordinal);

        bool HasCycle(string current)
        {
            if (settled.Contains(current))
            {
                return false;
            }

            if (!visiting.Add(current))
            {
                return true;
            }

            if (byKey.TryGetValue(current, out var parents))
            {
                foreach (var parent in parents)
                {
                    if (HasCycle(parent))
                    {
                        return true;
                    }
                }
            }

            visiting.Remove(current);
            settled.Add(current);

            return false;
        }

        return HasCycle(key)
            ? Error.Validation(
                "roadmap.cycle",
                "Esos prerrequisitos crean un ciclo: los nodos implicados quedarían bloqueados para siempre.")
            : null;
    }

    private static Task Audit(
        IAuditLogRepository audit,
        HttpContext context,
        IClock clock,
        string action,
        Guid nodeId,
        string key,
        CancellationToken ct) =>
        audit.AppendAsync(
            new AuditEntry(
                Guid.CreateVersion7(),
                context.User.RequireUserId(),
                action,
                "roadmap_node",
                nodeId.ToString(),
                System.Text.Json.JsonSerializer.Serialize(new { key }),
                clock.UtcNow),
            ct);

    public sealed record RoadmapNodeBody(
        Guid? Id,
        string Key,
        string Title,
        string? Description,
        Guid? CourseId,
        int Order,
        IReadOnlyList<string>? Prerequisites);
}
