using Inkoova.Academy.Domain.Common;
using Inkoova.Academy.Domain.ValueObjects;

namespace Inkoova.Academy.Domain.Learning;

public enum QuizKind
{
    /// <summary>Level test that gates the course, with three verdicts (T-08).</summary>
    Admission,

    /// <summary>Three-question check at the end of a block. Informational, never gating.</summary>
    BlockCheck
}

public enum AdmissionVerdict
{
    /// <summary>Ready for the course. Original threshold: 9 or more out of 12.</summary>
    Ready,

    /// <summary>Close. Do the pre-course first. Original threshold: 5 to 8.</summary>
    Almost,

    /// <summary>External resources first. Original threshold: below 5.</summary>
    ComeBackLater
}

public sealed record QuizOption(int Index, string Text, bool IsCorrect);

public sealed class Question
{
    public Guid Id { get; private set; }
    public Guid QuizId { get; private set; }
    public int Order { get; private set; }

    /// <summary>Dimension used for the per-category breakdown of the admission test.</summary>
    public string Category { get; private set; }

    public string Text { get; private set; }
    public string Explanation { get; private set; }

    public IReadOnlyList<QuizOption> Options { get; private set; }

    private Question(
        Guid id,
        Guid quizId,
        int order,
        string category,
        string text,
        string explanation,
        IReadOnlyList<QuizOption> options)
    {
        Id = id;
        QuizId = quizId;
        Order = order;
        Category = category;
        Text = text;
        Explanation = explanation;
        Options = options;
    }

    public static Result<Question, Error> Create(
        Guid id,
        Guid quizId,
        int order,
        string category,
        string text,
        string explanation,
        IReadOnlyList<QuizOption> options)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Error.Validation("question.text_empty", "La pregunta necesita enunciado.");
        }

        if (options.Count < 2)
        {
            return Error.Validation("question.too_few_options", "Una pregunta necesita al menos dos opciones.");
        }

        // Al menos una, no exactamente una. El test de nivel original marca varias opciones
        // como válidas cuando de verdad lo son: en "¿has llamado a una API de LLM?", tanto
        // "en producción" como "en prototipos" acreditan el nivel que el curso asume.
        // Exigir una sola descartaba la mitad de las preguntas e invalidaba los umbrales.
        if (!options.Any(o => o.IsCorrect))
        {
            return Error.Validation(
                "question.no_correct_option",
                "Una pregunta necesita al menos una opción correcta.");
        }

        // La categoría es una etiqueta, y la columna que la guarda mide 80. Sin esta
        // comprobación, una etiqueta larga no daba un error de validación sino una excepción
        // de la base al escribir, y el importador contestaba 500 sin decir qué pasaba: el
        // curso entraba con sus clases y sin ninguno de sus quizzes.
        if (category.Trim().Length > MaximumCategoryLength)
        {
            return Error.Validation(
                "question.category_too_long",
                $"La categoría de la pregunta no puede pasar de {MaximumCategoryLength} caracteres.");
        }

        return new Question(id, quizId, order, category.Trim(), text.Trim(), explanation.Trim(), options);
    }

    /// <summary>Lo que admite la columna `quiz_question.category`.</summary>
    public const int MaximumCategoryLength = 80;

    public static Question Rehydrate(
        Guid id,
        Guid quizId,
        int order,
        string category,
        string text,
        string explanation,
        IReadOnlyList<QuizOption> options) =>
        new(id, quizId, order, category, text, explanation, options);

    public bool IsCorrectAnswer(int selectedIndex) =>
        Options.FirstOrDefault(o => o.Index == selectedIndex)?.IsCorrect ?? false;
}

/// <summary>
/// Quiz imported from the original HTML. Grading lives here so the React component and the
/// standalone HTML cannot disagree about a verdict (T-08 acceptance criteria).
/// </summary>
public sealed class Quiz
{
    private readonly List<Question> _questions = [];

    public Guid Id { get; private set; }
    public Slug Slug { get; private set; }
    public string Title { get; private set; }
    public QuizKind Kind { get; private set; }

    /// <summary>Course this quiz gates or belongs to. Null for a standalone level test.</summary>
    public Guid? CourseId { get; private set; }

    public IReadOnlyList<Question> Questions => _questions;

    private Quiz(Guid id, Slug slug, string title, QuizKind kind, Guid? courseId)
    {
        Id = id;
        Slug = slug;
        Title = title;
        Kind = kind;
        CourseId = courseId;
    }

    public static Result<Quiz, Error> Create(Guid id, Slug slug, string title, QuizKind kind, Guid? courseId)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return Error.Validation("quiz.title_empty", "El cuestionario necesita un título.");
        }

        return new Quiz(id, slug, title.Trim(), kind, courseId);
    }

    public static Quiz Rehydrate(
        Guid id,
        Slug slug,
        string title,
        QuizKind kind,
        Guid? courseId,
        IEnumerable<Question> questions)
    {
        var quiz = new Quiz(id, slug, title, kind, courseId);
        quiz._questions.AddRange(questions.OrderBy(q => q.Order));
        return quiz;
    }

    public Result<Unit, Error> AddQuestion(Question question)
    {
        if (question.QuizId != Id)
        {
            return Error.Validation("quiz.question_mismatch", "La pregunta pertenece a otro cuestionario.");
        }

        _questions.Add(question);
        _questions.Sort((a, b) => a.Order.CompareTo(b.Order));
        return Unit.Value;
    }

    /// <summary>
    /// Grades a set of answers keyed by question id. A missing answer counts as wrong,
    /// which matches the original HTML: the student cannot advance without answering.
    /// </summary>
    public Result<QuizGrade, Error> Grade(IReadOnlyDictionary<Guid, int> answers)
    {
        if (_questions.Count == 0)
        {
            return Error.Conflict("quiz.no_questions", "El cuestionario no tiene preguntas.");
        }

        var unknown = answers.Keys.Where(k => _questions.All(q => q.Id != k)).ToArray();
        if (unknown.Length > 0)
        {
            return Error.Validation("quiz.unknown_question", "Se han enviado respuestas a preguntas inexistentes.");
        }

        var correct = 0;
        var byCategory = new Dictionary<string, CategoryScore>(StringComparer.Ordinal);

        foreach (var question in _questions)
        {
            var isCorrect = answers.TryGetValue(question.Id, out var selected) && question.IsCorrectAnswer(selected);
            if (isCorrect)
            {
                correct++;
            }

            var existing = byCategory.GetValueOrDefault(question.Category, new CategoryScore(0, 0));
            byCategory[question.Category] = existing with
            {
                Correct = existing.Correct + (isCorrect ? 1 : 0),
                Total = existing.Total + 1
            };
        }

        return new QuizGrade(correct, _questions.Count, byCategory, VerdictFor(correct));
    }

    /// <summary>
    /// Original thresholds from <c>nivel.html</c>: 9 or more APTO, 5 to 8 CASI, below 5
    /// VEN MÁS TARDE. Kept verbatim; changing them changes who is told they are ready.
    /// </summary>
    public static AdmissionVerdict VerdictFor(int correctCount) => correctCount switch
    {
        >= 9 => AdmissionVerdict.Ready,
        >= 5 => AdmissionVerdict.Almost,
        _ => AdmissionVerdict.ComeBackLater
    };
}

public sealed record CategoryScore(int Correct, int Total);

public sealed record QuizGrade(
    int CorrectCount,
    int TotalQuestions,
    IReadOnlyDictionary<string, CategoryScore> ByCategory,
    AdmissionVerdict Verdict);
