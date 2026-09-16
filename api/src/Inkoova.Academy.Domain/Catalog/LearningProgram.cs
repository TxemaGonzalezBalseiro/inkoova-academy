using Inkoova.Academy.Domain.Common;
using Inkoova.Academy.Domain.ValueObjects;

namespace Inkoova.Academy.Domain.Catalog;

/// <summary>Course inside a program, with its position and its prerequisite courses.</summary>
public sealed record ProgramItem(Guid ProgramId, Guid CourseId, int Order, IReadOnlyList<Guid> PrerequisiteCourseIds);

/// <summary>
/// Ordered set of courses sold together, with a program certificate on completion (C-21).
/// Named <c>LearningProgram</c> because <c>Program</c> collides with the entry-point class.
/// </summary>
public sealed class LearningProgram
{
    private readonly List<ProgramItem> _items = [];

    public Guid Id { get; private set; }
    public Guid ProductId { get; private set; }
    public Slug Slug { get; private set; }
    public string Title { get; private set; }

    /// <summary>Plans whose subscribers get every sector pack included (T-04 addendum).</summary>
    public IReadOnlySet<string> PlanCodesIncludingPacks { get; private set; }

    public IReadOnlyList<ProgramItem> Items => _items;

    private LearningProgram(
        Guid id,
        Guid productId,
        Slug slug,
        string title,
        IReadOnlySet<string> planCodesIncludingPacks)
    {
        Id = id;
        ProductId = productId;
        Slug = slug;
        Title = title;
        PlanCodesIncludingPacks = planCodesIncludingPacks;
    }

    public static Result<LearningProgram, Error> Create(
        Guid id,
        Guid productId,
        Slug slug,
        string title,
        IEnumerable<string> planCodesIncludingPacks)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return Error.Validation("program.title_empty", "El programa necesita un título.");
        }

        return new LearningProgram(id, productId, slug, title.Trim(), planCodesIncludingPacks.ToHashSet());
    }

    public static LearningProgram Rehydrate(
        Guid id,
        Guid productId,
        Slug slug,
        string title,
        IEnumerable<string> planCodesIncludingPacks,
        IEnumerable<ProgramItem> items)
    {
        var program = new LearningProgram(id, productId, slug, title, planCodesIncludingPacks.ToHashSet());
        program._items.AddRange(items.OrderBy(i => i.Order));
        return program;
    }

    public Result<Unit, Error> AddCourse(Guid courseId, int order, IReadOnlyList<Guid>? prerequisites = null)
    {
        if (_items.Any(i => i.CourseId == courseId))
        {
            return Error.Conflict("program.course_duplicated", "El curso ya forma parte del programa.");
        }

        var prereqs = prerequisites ?? [];

        if (prereqs.Contains(courseId))
        {
            return Error.Validation("program.self_prerequisite", "Un curso no puede ser prerequisito de sí mismo.");
        }

        var unknown = prereqs.Where(p => _items.All(i => i.CourseId != p)).ToArray();
        if (unknown.Length > 0)
        {
            return Error.Validation(
                "program.unknown_prerequisite",
                "Los prerequisitos deben ser cursos ya añadidos al programa.");
        }

        _items.Add(new ProgramItem(Id, courseId, order, prereqs));
        _items.Sort((a, b) => a.Order.CompareTo(b.Order));
        return Unit.Value;
    }

    /// <summary>A course unlocks when every prerequisite of its item is completed.</summary>
    public bool IsUnlocked(Guid courseId, IReadOnlySet<Guid> completedCourseIds)
    {
        var item = _items.FirstOrDefault(i => i.CourseId == courseId);
        return item is null || item.PrerequisiteCourseIds.All(completedCourseIds.Contains);
    }

    public bool IsComplete(IReadOnlySet<Guid> completedCourseIds) =>
        _items.Count > 0 && _items.All(i => completedCourseIds.Contains(i.CourseId));
}
