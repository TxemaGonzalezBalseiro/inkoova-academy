using FluentAssertions;
using Inkoova.Academy.Domain.Learning;
using Inkoova.Academy.Domain.ValueObjects;

namespace Inkoova.Academy.Domain.Tests;

public class QuizTests
{
    /// <summary>
    /// Thresholds copied verbatim from the original nivel.html. Changing them changes who is
    /// told they are ready for the course, so they are pinned by test (T-08).
    /// </summary>
    [Theory]
    [InlineData(12, AdmissionVerdict.Ready)]
    [InlineData(9, AdmissionVerdict.Ready)]
    [InlineData(8, AdmissionVerdict.Almost)]
    [InlineData(5, AdmissionVerdict.Almost)]
    [InlineData(4, AdmissionVerdict.ComeBackLater)]
    [InlineData(0, AdmissionVerdict.ComeBackLater)]
    public void Verdict_thresholds_match_the_original_test(int correct, AdmissionVerdict expected) =>
        Quiz.VerdictFor(correct).Should().Be(expected);

    [Fact]
    public void Grading_counts_correct_answers_and_breaks_them_down_by_category()
    {
        var quiz = BuildQuiz(
            ("Fundamentos LLM", 0),
            ("Fundamentos LLM", 1),
            ("Producción", 2));

        var answers = new Dictionary<Guid, int>
        {
            [quiz.Questions[0].Id] = 0,
            [quiz.Questions[1].Id] = 0,
            [quiz.Questions[2].Id] = 2
        };

        var grade = quiz.Grade(answers);

        grade.IsSuccess.Should().BeTrue();
        grade.Value.CorrectCount.Should().Be(2);
        grade.Value.TotalQuestions.Should().Be(3);
        grade.Value.ByCategory["Fundamentos LLM"].Should().Be(new CategoryScore(1, 2));
        grade.Value.ByCategory["Producción"].Should().Be(new CategoryScore(1, 1));
    }

    [Fact]
    public void An_unanswered_question_counts_as_wrong()
    {
        var quiz = BuildQuiz(("A", 0), ("A", 0));

        var grade = quiz.Grade(new Dictionary<Guid, int> { [quiz.Questions[0].Id] = 0 });

        grade.Value.CorrectCount.Should().Be(1);
        grade.Value.TotalQuestions.Should().Be(2);
    }

    [Fact]
    public void Answers_to_questions_that_do_not_belong_to_the_quiz_are_refused()
    {
        var quiz = BuildQuiz(("A", 0));

        var grade = quiz.Grade(new Dictionary<Guid, int> { [Guid.CreateVersion7()] = 0 });

        grade.IsFailure.Should().BeTrue();
        grade.Error.Code.Should().Be("quiz.unknown_question");
    }

    [Fact]
    public void A_quiz_without_questions_cannot_be_graded()
    {
        var quiz = Quiz.Create(
            Guid.CreateVersion7(), Slug.Create("vacio").Value, "Vacío", QuizKind.Admission, null).Value;

        quiz.Grade(new Dictionary<Guid, int>()).Error.Code.Should().Be("quiz.no_questions");
    }

    [Fact]
    public void A_question_needs_at_least_one_correct_option()
    {
        var none = Question.Create(
            Guid.CreateVersion7(), Guid.CreateVersion7(), 0, "A", "¿Cuál?", "porque sí",
            [new QuizOption(0, "a", false), new QuizOption(1, "b", false)]);

        none.Error.Code.Should().Be("question.no_correct_option");
    }

    [Fact]
    public void A_question_may_have_several_valid_answers()
    {
        // El test de nivel original lo hace a propósito: en "¿has llamado a una API de LLM?",
        // tanto "en producción" como "en prototipos" acreditan el nivel que el curso asume.
        var question = Question.Create(
            Guid.CreateVersion7(), Guid.CreateVersion7(), 0, "Experiencia construyendo",
            "¿Has llamado a una API de LLM?", "Ambas cuentan.",
            [
                new QuizOption(0, "Sí, en producción.", true),
                new QuizOption(1, "Sí, en prototipos.", true),
                new QuizOption(2, "Solo he usado la interfaz web.", false),
                new QuizOption(3, "Nunca.", false)
            ]);

        question.IsSuccess.Should().BeTrue();
        question.Value.IsCorrectAnswer(0).Should().BeTrue();
        question.Value.IsCorrectAnswer(1).Should().BeTrue();
        question.Value.IsCorrectAnswer(2).Should().BeFalse();
    }

    [Fact]
    public void The_admission_quiz_keeps_all_twelve_questions_when_some_admit_several_answers()
    {
        // Regresión: con la regla de "exactamente una correcta" se descartaban 6 de las 12
        // preguntas, y el veredicto se calculaba sobre un examen que ya no era el original.
        var quiz = Quiz.Create(
            Guid.CreateVersion7(), Slug.Create("nivel").Value, "Test de nivel",
            QuizKind.Admission, null).Value;

        for (var i = 0; i < 12; i++)
        {
            var multipleValid = i % 2 == 0;

            var options = new List<QuizOption>
            {
                new(0, "a", true),
                new(1, "b", multipleValid),
                new(2, "c", false),
                new(3, "d", false)
            };

            quiz.AddQuestion(Question.Create(
                Guid.CreateVersion7(), quiz.Id, i, "Dimensión", $"Pregunta {i}", "…", options).Value);
        }

        quiz.Questions.Should().HaveCount(12);
    }

    [Fact]
    public void An_anonymous_attempt_can_be_claimed_once()
    {
        var quiz = BuildQuiz(("A", 0));
        var grade = quiz.Grade(new Dictionary<Guid, int> { [quiz.Questions[0].Id] = 0 }).Value;

        var attempt = QuizAttempt.Record(
            Guid.CreateVersion7(), quiz.Id, userId: null, anonymousKey: "cookie-value",
            grade, new Dictionary<Guid, int>(), DateTimeOffset.UtcNow).Value;

        var userId = Guid.CreateVersion7();
        attempt.ClaimBy(userId).IsSuccess.Should().BeTrue();
        attempt.UserId.Should().Be(userId);
        attempt.AnonymousKey.Should().BeNull();

        attempt.ClaimBy(Guid.CreateVersion7()).Error.Code.Should().Be("quiz_attempt.already_claimed");
    }

    [Fact]
    public void An_attempt_with_neither_user_nor_cookie_is_refused()
    {
        var quiz = BuildQuiz(("A", 0));
        var grade = quiz.Grade(new Dictionary<Guid, int> { [quiz.Questions[0].Id] = 0 }).Value;

        var attempt = QuizAttempt.Record(
            Guid.CreateVersion7(), quiz.Id, null, null, grade, new Dictionary<Guid, int>(), DateTimeOffset.UtcNow);

        attempt.Error.Code.Should().Be("quiz_attempt.no_owner");
    }

    private static Quiz BuildQuiz(params (string Category, int CorrectIndex)[] questions)
    {
        var quiz = Quiz.Create(
            Guid.CreateVersion7(), Slug.Create("nivel").Value, "Test de nivel",
            QuizKind.Admission, null).Value;

        for (var i = 0; i < questions.Length; i++)
        {
            var (category, correctIndex) = questions[i];

            var options = Enumerable.Range(0, 4)
                .Select(index => new QuizOption(index, $"Opción {index}", index == correctIndex))
                .ToList();

            quiz.AddQuestion(Question.Create(
                Guid.CreateVersion7(), quiz.Id, i, category, $"Pregunta {i}", "Explicación.", options).Value);
        }

        return quiz;
    }
}
