using Inkoova.Academy.Application.Abstractions;
using Inkoova.Academy.Domain.Catalog;
using Inkoova.Academy.Domain.Common;
using Inkoova.Academy.Domain.Learning;
using Inkoova.Academy.Domain.ValueObjects;

namespace Inkoova.Academy.Application.Content;

/// <summary>
/// Autoría de cursos desde el panel (T-11).
///
/// Existe junto al importador, no en su lugar. El importador trae cursos que ya están escritos
/// en HTML; esto sirve para lo que no viene de ahí: crear un curso a mano, corregir un título,
/// meter una clase suelta, escribir un quiz. Los dos escriben en el mismo sitio y pasan por las
/// mismas invariantes del dominio.
///
/// Lo que NO se puede cambiar es el slug, ni de un curso ni de una clase. Es la URL pública y
/// el progreso de cada alumno apunta a ella: renombrarlo rompería enlaces ya repartidos y
/// dejaría el avance colgando de una clase que ya no existe. Para cambiarlo, se crea otra.
/// </summary>
public sealed record CreateCourseRequest(
    string Slug,
    string Title,
    string ShortDescription,
    string LongDescription,
    string Level,
    long PriceCents);

public sealed record UpdateCourseRequest(
    string Title,
    string ShortDescription,
    string LongDescription,
    string Level);

public sealed record UpsertLessonRequest(
    string Title,
    string Type,
    int DurationMinutes,
    string ContentRef,
    bool IsFreePreview,
    bool IsRequired);

public sealed record UpsertQuizRequest(
    string Slug,
    string Title,
    string Kind,
    Guid? CourseId,
    IReadOnlyList<UpsertQuestionRequest> Questions);

public sealed record UpsertQuestionRequest(
    string Category,
    string Text,
    string Explanation,
    IReadOnlyList<UpsertOptionRequest> Options);

public sealed record UpsertOptionRequest(string Text, bool IsCorrect);

public sealed class CourseAuthoringHandlers(
    ICourseRepository courses,
    IProductRepository products,
    IQuizRepository quizzes,
    ICatalogCache cache,
    IClock clock)
{
    /// <summary>
    /// Crea el curso y su producto a la vez. Un curso sin producto no se puede vender ni dar de
    /// alta como acceso, porque todo derecho de acceso apunta a un producto (ADR-007); crearlos
    /// por separado dejaría cursos invisibles para el catálogo hasta que alguien se acordara.
    /// </summary>
    public async Task<Result<Guid, Error>> CreateCourseAsync(CreateCourseRequest request, CancellationToken ct)
    {
        var slug = Slug.Create(request.Slug);
        if (slug.IsFailure)
        {
            return slug.Error;
        }

        if (await courses.GetBySlugAsync(slug.Value, ct) is not null)
        {
            return Error.Conflict("course.slug_taken", "Ya existe un curso con ese identificador.");
        }

        if (!TryParseLevel(request.Level, out var level))
        {
            return Error.Validation("course.level_unknown", $"Nivel desconocido: {request.Level}.");
        }

        if (request.PriceCents < 0)
        {
            return Error.Validation("course.price_negative", "El precio no puede ser negativo.");
        }

        var now = clock.UtcNow;

        var product = Product.Create(
            Guid.CreateVersion7(), ProductType.Course, slug.Value, request.Title,
            Money.Euros(request.PriceCents), now);

        if (product.IsFailure)
        {
            return product.Error;
        }

        var course = Course.Create(
            Guid.CreateVersion7(), product.Value.Id, slug.Value, request.Title,
            request.ShortDescription, request.LongDescription, level, now);

        if (course.IsFailure)
        {
            return course.Error;
        }

        await products.UpsertAsync(product.Value, ct);
        await courses.SaveAsync(course.Value, ct);
        cache.Invalidate();

        return course.Value.Id;
    }

    public async Task<Result<Unit, Error>> UpdateCourseAsync(
        Guid courseId,
        UpdateCourseRequest request,
        CancellationToken ct)
    {
        var course = await courses.GetByIdAsync(courseId, ct);
        if (course is null)
        {
            return NotFound;
        }

        if (!TryParseLevel(request.Level, out var level))
        {
            return Error.Validation("course.level_unknown", $"Nivel desconocido: {request.Level}.");
        }

        var described = course.Describe(
            request.Title, request.ShortDescription, request.LongDescription, level);

        if (described.IsFailure)
        {
            return described.Error;
        }

        await courses.SaveAsync(course, ct);
        cache.Invalidate();

        return Unit.Value;
    }

    public async Task<Result<Guid, Error>> AddSectionAsync(Guid courseId, string title, CancellationToken ct)
    {
        var course = await courses.GetByIdAsync(courseId, ct);
        if (course is null)
        {
            return NotFound;
        }

        // Al final del temario: una sección nueva es lo último que se ha escrito.
        var section = Section.Create(Guid.CreateVersion7(), course.Id, course.Sections.Count, title);
        if (section.IsFailure)
        {
            return section.Error;
        }

        var added = course.AddSection(section.Value);
        if (added.IsFailure)
        {
            return added.Error;
        }

        await courses.SaveAsync(course, ct);
        cache.Invalidate();

        return section.Value.Id;
    }

    public async Task<Result<Unit, Error>> RenameSectionAsync(
        Guid courseId,
        Guid sectionId,
        string title,
        CancellationToken ct)
    {
        var course = await courses.GetByIdAsync(courseId, ct);
        var section = course?.Sections.FirstOrDefault(item => item.Id == sectionId);

        if (course is null || section is null)
        {
            return NotFound;
        }

        var renamed = section.Rename(title);
        if (renamed.IsFailure)
        {
            return renamed.Error;
        }

        await courses.SaveAsync(course, ct);
        cache.Invalidate();

        return Unit.Value;
    }

    public async Task<Result<Unit, Error>> DeleteSectionAsync(Guid sectionId, CancellationToken ct)
    {
        await courses.DeleteSectionAsync(sectionId, ct);
        cache.Invalidate();

        return Unit.Value;
    }

    public async Task<Result<Guid, Error>> AddLessonAsync(
        Guid courseId,
        Guid sectionId,
        UpsertLessonRequest request,
        CancellationToken ct)
    {
        var course = await courses.GetByIdAsync(courseId, ct);
        var section = course?.Sections.FirstOrDefault(item => item.Id == sectionId);

        if (course is null || section is null)
        {
            return NotFound;
        }

        if (!TryParseType(request.Type, out var type))
        {
            return Error.Validation("lesson.type_unknown", $"Tipo de clase desconocido: {request.Type}.");
        }

        var uniform = EnsureUniformAccess(course, request.ContentRef, request.IsFreePreview);
        if (uniform.IsFailure)
        {
            return uniform.Error;
        }

        // El slug sale del título y es único dentro del curso, que es el alcance de la URL
        // /aprender/{curso}/{clase}. Se numera si choca en vez de fallar: dos clases llamadas
        // "Ejercicio 1" en secciones distintas es normal y no debería impedir guardarlas.
        var slug = UniqueLessonSlug(course, request.Title);
        if (slug.IsFailure)
        {
            return slug.Error;
        }

        var lesson = Lesson.Create(
            Guid.CreateVersion7(), section.Id, section.Lessons.Count, slug.Value, request.Title,
            type, request.DurationMinutes, request.ContentRef, request.IsFreePreview, request.IsRequired);

        if (lesson.IsFailure)
        {
            return lesson.Error;
        }

        var added = section.AddLesson(lesson.Value);
        if (added.IsFailure)
        {
            return added.Error;
        }

        await courses.SaveAsync(course, ct);
        cache.Invalidate();

        return lesson.Value.Id;
    }

    public async Task<Result<Unit, Error>> UpdateLessonAsync(
        Guid courseId,
        Guid lessonId,
        UpsertLessonRequest request,
        CancellationToken ct)
    {
        var course = await courses.GetByIdAsync(courseId, ct);
        var lesson = course?.AllLessons.FirstOrDefault(item => item.Id == lessonId);

        if (course is null || lesson is null)
        {
            return NotFound;
        }

        if (!TryParseType(request.Type, out var type))
        {
            return Error.Validation("lesson.type_unknown", $"Tipo de clase desconocido: {request.Type}.");
        }

        var uniform = EnsureUniformAccess(course, request.ContentRef, request.IsFreePreview, lesson.Id);
        if (uniform.IsFailure)
        {
            return uniform.Error;
        }

        var updated = lesson.Update(
            request.Title, type, request.DurationMinutes, request.ContentRef,
            request.IsFreePreview, request.IsRequired);

        if (updated.IsFailure)
        {
            return updated.Error;
        }

        await courses.SaveAsync(course, ct);
        cache.Invalidate();

        return Unit.Value;
    }

    public async Task<Result<Unit, Error>> DeleteLessonAsync(Guid lessonId, CancellationToken ct)
    {
        await courses.DeleteLessonAsync(lessonId, ct);
        cache.Invalidate();

        return Unit.Value;
    }

    /// <summary>
    /// Crea o reemplaza un quiz entero, con sus preguntas. Se reemplaza en bloque y no pregunta
    /// a pregunta porque un quiz a medio guardar es un examen distinto del que se quiso escribir,
    /// y el resultado de un alumno dependería de cuándo lo abriera.
    /// </summary>
    public async Task<Result<Guid, Error>> SaveQuizAsync(UpsertQuizRequest request, CancellationToken ct)
    {
        var slug = Slug.Create(request.Slug);
        if (slug.IsFailure)
        {
            return slug.Error;
        }

        if (!Enum.TryParse<QuizKind>(request.Kind, ignoreCase: true, out var kind))
        {
            return Error.Validation("quiz.kind_unknown", $"Tipo de quiz desconocido: {request.Kind}.");
        }

        if (request.Questions.Count == 0)
        {
            return Error.Validation("quiz.no_questions", "Un quiz necesita al menos una pregunta.");
        }

        // Se conserva el id si el quiz ya existía: los intentos de los alumnos apuntan a él.
        var existing = await quizzes.GetBySlugAsync(slug.Value, ct);

        var quiz = Quiz.Create(existing?.Id ?? Guid.CreateVersion7(), slug.Value, request.Title, kind, request.CourseId);
        if (quiz.IsFailure)
        {
            return quiz.Error;
        }

        for (var index = 0; index < request.Questions.Count; index++)
        {
            var source = request.Questions[index];

            var options = source.Options
                .Select((option, position) => new QuizOption(position, option.Text, option.IsCorrect))
                .ToList();

            var question = Question.Create(
                Guid.CreateVersion7(), quiz.Value.Id, index, source.Category, source.Text,
                source.Explanation, options);

            if (question.IsFailure)
            {
                // Se aborta: guardar un quiz al que le falta una pregunta produce un examen
                // que parece correcto y puntúa mal.
                return question.Error;
            }

            var added = quiz.Value.AddQuestion(question.Value);
            if (added.IsFailure)
            {
                return added.Error;
            }
        }

        await quizzes.SaveAsync(quiz.Value, ct);
        cache.Invalidate();

        return quiz.Value.Id;
    }

    /// <summary>
    /// Una clase es un ancla dentro de un documento, y el documento se sirve entero: abrir una
    /// clase de B0 carga las 25 slides del fichero de B0. Mientras "gratis" o "de pago" sea del
    /// bloque completo, eso cuadra.
    ///
    /// Deja de cuadrar en cuanto alguien marca como gratis unas clases de un documento y no
    /// otras: la clase gratuita entrega el fichero, y con él las de pago. No es un fallo que se
    /// vea —la lista de clases sigue enseñando su candado— pero el material está servido.
    ///
    /// Por eso el acceso se comprueba por documento y no por clase. Lo natural sería impedirlo
    /// al servir, pero entonces el error saldría en la cara del alumno por algo que decidió el
    /// panel; se rechaza aquí, donde todavía hay a quien decírselo.
    /// </summary>
    private static Result<Unit, Error> EnsureUniformAccess(
        Course course,
        string contentRef,
        bool isFreePreview,
        Guid? excludingLessonId = null)
    {
        var document = DocumentOf(contentRef);

        var conflict = course.AllLessons.FirstOrDefault(other =>
            other.Id != excludingLessonId
            && DocumentOf(other.ContentRef) == document
            && other.IsFreePreview != isFreePreview);

        if (conflict is null)
        {
            return Unit.Value;
        }

        return Error.Validation(
            "lesson.mixed_access",
            $"«{conflict.Title}» comparte fichero con esta clase y {(conflict.IsFreePreview ? "es gratuita" : "es de pago")}. " +
            "El contenido se sirve por fichero entero, así que una clase gratuita en un fichero " +
            "de pago regala el resto. Cambia todas las clases de ese fichero a la vez.");
    }

    /// <summary>Ruta del fichero, sin el ancla de la slide.</summary>
    private static string DocumentOf(string contentRef)
    {
        var hash = contentRef.IndexOf('#', StringComparison.Ordinal);
        return hash < 0 ? contentRef : contentRef[..hash];
    }

    private static readonly Error NotFound = Error.NotFound("catalog.not_found", "No existe ese elemento.");

    private static bool TryParseLevel(string value, out CourseLevel level) =>
        Enum.TryParse(value, ignoreCase: true, out level);

    private static bool TryParseType(string value, out LessonType type) =>
        Enum.TryParse(value, ignoreCase: true, out type);

    private static Result<Slug, Error> UniqueLessonSlug(Course course, string title)
    {
        var baseSlug = Slug.FromTitle(title);
        if (baseSlug.IsFailure)
        {
            return baseSlug.Error;
        }

        var taken = course.AllLessons.Select(lesson => lesson.Slug.Value).ToHashSet(StringComparer.Ordinal);

        if (!taken.Contains(baseSlug.Value.Value))
        {
            return baseSlug.Value;
        }

        for (var suffix = 2; suffix < 1000; suffix++)
        {
            var candidate = Slug.Create($"{baseSlug.Value.Value}-{suffix}");

            if (candidate.IsSuccess && !taken.Contains(candidate.Value.Value))
            {
                return candidate.Value;
            }
        }

        return Error.Conflict("lesson.slug_exhausted", "Demasiadas clases con ese mismo título.");
    }
}
