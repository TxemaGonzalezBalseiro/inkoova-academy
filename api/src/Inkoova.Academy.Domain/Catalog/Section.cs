using Inkoova.Academy.Domain.Common;

namespace Inkoova.Academy.Domain.Catalog;

public sealed class Section
{
    private readonly List<Lesson> _lessons = [];

    public Guid Id { get; private set; }
    public Guid CourseId { get; private set; }
    public int Order { get; private set; }
    public string Title { get; private set; }

    public IReadOnlyList<Lesson> Lessons => _lessons;

    private Section(Guid id, Guid courseId, int order, string title)
    {
        Id = id;
        CourseId = courseId;
        Order = order;
        Title = title;
    }

    public static Result<Section, Error> Create(Guid id, Guid courseId, int order, string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return Error.Validation("section.title_empty", "La sección necesita un título.");
        }

        if (order < 0)
        {
            return Error.Validation("section.order_negative", "El orden de la sección no puede ser negativo.");
        }

        return new Section(id, courseId, order, title.Trim());
    }

    public static Section Rehydrate(Guid id, Guid courseId, int order, string title, IEnumerable<Lesson> lessons)
    {
        var section = new Section(id, courseId, order, title);
        section._lessons.AddRange(lessons.OrderBy(l => l.Order));
        return section;
    }

    public Result<Unit, Error> AddLesson(Lesson lesson)
    {
        if (lesson.SectionId != Id)
        {
            return Error.Validation("section.lesson_mismatch", "La lección pertenece a otra sección.");
        }

        if (_lessons.Any(l => l.Slug.Value == lesson.Slug.Value))
        {
            return Error.Conflict("section.lesson_slug_duplicated", $"Ya existe una lección con el slug '{lesson.Slug}'.");
        }

        _lessons.Add(lesson);
        _lessons.Sort((a, b) => a.Order.CompareTo(b.Order));
        return Unit.Value;
    }

    public int TotalDurationMinutes => _lessons.Sum(l => l.DurationMinutes);

    public void Reorder(int newOrder) => Order = Math.Max(0, newOrder);

    public Result<Unit, Error> Rename(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return Error.Validation("section.title_empty", "La sección necesita un título.");
        }

        Title = title.Trim();
        return Unit.Value;
    }

    /// <summary>Quita una lección de la sección. El repositorio la borra de la base al guardar.</summary>
    public bool RemoveLesson(Guid lessonId)
    {
        var lesson = _lessons.FirstOrDefault(item => item.Id == lessonId);

        if (lesson is null)
        {
            return false;
        }

        _lessons.Remove(lesson);

        // El orden se compacta: un hueco en la numeración se ve como una clase que falta.
        for (var index = 0; index < _lessons.Count; index++)
        {
            _lessons[index].Reorder(index);
        }

        return true;
    }
}
