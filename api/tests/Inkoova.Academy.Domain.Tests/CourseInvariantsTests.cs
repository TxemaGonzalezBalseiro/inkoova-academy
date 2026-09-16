using FluentAssertions;
using Inkoova.Academy.Domain.Catalog;
using Inkoova.Academy.Domain.ValueObjects;

namespace Inkoova.Academy.Domain.Tests;

public class CourseInvariantsTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 30, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Publishing_a_course_without_lessons_is_refused()
    {
        var course = BuildCourse();

        var result = course.Publish(Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("course.publish_without_lessons");
        course.Status.Should().Be(PublicationStatus.Draft);
    }

    [Fact]
    public void Publishing_a_course_with_at_least_one_lesson_succeeds()
    {
        var course = BuildCourse();
        AddSectionWithLesson(course, "b0", "Fundamentos", durationMinutes: 45);

        var result = course.Publish(Now);

        result.IsSuccess.Should().BeTrue();
        course.Status.Should().Be(PublicationStatus.Published);
        course.PublishedAt.Should().Be(Now);
    }

    [Fact]
    public void Publishing_twice_keeps_the_original_publication_date()
    {
        var course = BuildCourse();
        AddSectionWithLesson(course, "b0", "Fundamentos", 45);

        course.Publish(Now);
        course.Publish(Now.AddDays(10));

        course.PublishedAt.Should().Be(Now);
    }

    [Fact]
    public void A_published_course_cannot_go_back_to_coming_soon()
    {
        var course = BuildCourse();
        AddSectionWithLesson(course, "b0", "Fundamentos", 45);
        course.Publish(Now);

        var result = course.MarkComingSoon();

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("course.coming_soon_after_publish");
    }

    [Fact]
    public void Hours_round_up_so_a_ninety_one_minute_course_reads_as_two_hours()
    {
        var course = BuildCourse();
        AddSectionWithLesson(course, "b0", "Uno", 46);
        AddSectionWithLesson(course, "b1", "Dos", 45);

        course.TotalDurationMinutes.Should().Be(91);
        course.TotalHours.Should().Be(2);
    }

    [Fact]
    public void Two_lessons_with_the_same_slug_in_a_section_are_refused()
    {
        var course = BuildCourse();
        var section = Section.Create(Guid.CreateVersion7(), course.Id, 0, "Bloque 0").Value;
        course.AddSection(section);

        section.AddLesson(BuildLesson(section.Id, "intro", 0)).IsSuccess.Should().BeTrue();

        var duplicate = section.AddLesson(BuildLesson(section.Id, "intro", 1));

        duplicate.IsFailure.Should().BeTrue();
        duplicate.Error.Code.Should().Be("section.lesson_slug_duplicated");
    }

    [Fact]
    public void A_section_belonging_to_another_course_is_refused()
    {
        var course = BuildCourse();
        var foreign = Section.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), 0, "Ajena").Value;

        var result = course.AddSection(foreign);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("course.section_mismatch");
    }

    [Fact]
    public void Sections_and_lessons_are_returned_in_order_regardless_of_insertion_order()
    {
        var course = BuildCourse();

        var second = Section.Create(Guid.CreateVersion7(), course.Id, 1, "Segundo").Value;
        var first = Section.Create(Guid.CreateVersion7(), course.Id, 0, "Primero").Value;

        course.AddSection(second);
        course.AddSection(first);

        course.Sections.Select(s => s.Title).Should().ContainInOrder("Primero", "Segundo");
    }

    [Fact]
    public void A_course_where_every_lesson_is_preview_is_not_members_only()
    {
        var course = BuildCourse();
        var section = Section.Create(Guid.CreateVersion7(), course.Id, 0, "Pre-curso").Value;
        course.AddSection(section);
        section.AddLesson(BuildLesson(section.Id, "p0", 0, isFreePreview: true));

        course.AllLessons.All(l => l.IsFreePreview).Should().BeTrue();
    }

    private static Course BuildCourse() => Course.Create(
        Guid.CreateVersion7(),
        Guid.CreateVersion7(),
        Slug.Create("agent-engineering-v3").Value,
        "Agent Engineering v3.0",
        "Construye agentes que sobreviven en producción.",
        "Descripción larga.",
        CourseLevel.Advanced,
        Now).Value;

    private static void AddSectionWithLesson(Course course, string slug, string title, int durationMinutes)
    {
        var section = Section.Create(Guid.CreateVersion7(), course.Id, course.TotalSections, title).Value;
        course.AddSection(section);
        section.AddLesson(BuildLesson(section.Id, slug, 0, durationMinutes: durationMinutes));
    }

    private static Lesson BuildLesson(
        Guid sectionId,
        string slug,
        int order,
        bool isFreePreview = false,
        int durationMinutes = 30) =>
        Lesson.Create(
            Guid.CreateVersion7(), sectionId, order, Slug.Create(slug).Value, $"Lección {slug}",
            LessonType.Slides, durationMinutes, $"agent-engineering-v3/{slug}.html", isFreePreview).Value;
}
