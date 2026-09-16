using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Inkoova.Academy.Domain.Catalog;
using Inkoova.Academy.Domain.ValueObjects;
using Inkoova.Academy.Infrastructure.Persistence;

namespace Inkoova.Academy.Integration.Tests;

/// <summary>
/// The rule this file exists for: a visitor without access sees the syllabus but never the
/// content reference (T-02 and T-06 acceptance criteria). Asserting on the raw JSON rather
/// than a deserialised DTO is deliberate — the risk is the value being serialised at all.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class CourseAccessEndpointTests(DatabaseFixture fixture) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 8, 30, 12, 0, 0, TimeSpan.Zero);
    private static readonly CancellationToken Ct = CancellationToken.None;
    private const string CourseSlug = "acceso-endpoint";

    private AcademyApiFactory _factory = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        DapperTypeHandlers.Register();
        await SeedPublishedCourseAsync();

        _factory = new AcademyApiFactory(fixture.ConnectionString);
        _client = _factory.CreateClient();
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return _factory.DisposeAsync().AsTask();
    }

    [Fact]
    public async Task The_public_catalogue_lists_the_published_course()
    {
        var response = await _client.GetAsync("/api/courses", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var courses = await response.Content.ReadFromJsonAsync<List<JsonElement>>(Ct);
        courses!.Select(c => c.GetProperty("slug").GetString()).Should().Contain(CourseSlug);
    }

    [Fact]
    public async Task An_anonymous_visitor_sees_the_syllabus_without_any_content_reference()
    {
        var json = await _client.GetStringAsync($"/api/courses/{CourseSlug}", Ct);

        // The syllabus itself is public.
        json.Should().Contain("Bloque 0");
        json.Should().Contain("b0-loop");

        // The path to the real content is not, for the lesson that is not a preview.
        json.Should().NotContain("acceso-endpoint/B0b.html");
    }

    [Fact]
    public async Task A_free_preview_lesson_does_carry_its_content_reference()
    {
        var json = await _client.GetStringAsync($"/api/courses/{CourseSlug}", Ct);

        json.Should().Contain("acceso-endpoint/B0.html");
    }

    [Fact]
    public async Task The_player_serves_a_preview_lesson_to_an_anonymous_visitor()
    {
        var response = await _client.GetAsync($"/api/learn/{CourseSlug}/b0-intro", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        payload.GetProperty("contentToken").GetString().Should().NotBeNullOrWhiteSpace();
        payload.GetProperty("contentTokenSecondsToLive").GetInt32().Should().Be(60);
    }

    [Fact]
    public async Task The_player_refuses_a_members_only_lesson_to_an_anonymous_visitor()
    {
        var response = await _client.GetAsync($"/api/learn/{CourseSlug}/b0-loop", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        problem.GetProperty("code").GetString().Should().Be("lesson.no_access");
    }

    [Fact]
    public async Task A_course_that_does_not_exist_answers_404_and_not_500()
    {
        var response = await _client.GetAsync("/api/courses/no-existe-este-curso", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Progress_endpoints_require_authentication()
    {
        var response = await _client.PostAsync(
            $"/api/me/progress/lessons/{Guid.CreateVersion7()}/complete", content: null, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task An_invalid_content_token_is_refused()
    {
        var response = await _client.GetAsync("/api/content/no-es-un-token", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private async Task SeedPublishedCourseAsync()
    {
        var connections = new NpgsqlConnectionFactory(fixture.ConnectionString);
        var products = new ProductRepository(connections);
        var courses = new CourseRepository(connections);

        if (await courses.GetBySlugAsync(Slug.Create(CourseSlug).Value, Ct) is not null)
        {
            return;
        }

        var product = Product.Create(
            Guid.CreateVersion7(), ProductType.Course, Slug.Create(CourseSlug).Value,
            "Curso de acceso", Money.Euros(9_900), Now).Value;

        await products.UpsertAsync(product, Ct);

        var course = Course.Create(
            Guid.CreateVersion7(), product.Id, Slug.Create(CourseSlug).Value, "Curso de acceso",
            "Corta.", "Larga.", CourseLevel.Advanced, Now).Value;

        var section = Section.Create(Guid.CreateVersion7(), course.Id, 0, "Bloque 0").Value;
        course.AddSection(section);

        section.AddLesson(Lesson.Create(
            Guid.CreateVersion7(), section.Id, 0, Slug.Create("b0-intro").Value, "Intro",
            LessonType.Slides, 30, $"{CourseSlug}/B0.html", isFreePreview: true).Value);

        section.AddLesson(Lesson.Create(
            Guid.CreateVersion7(), section.Id, 1, Slug.Create("b0-loop").Value, "Loop",
            LessonType.Slides, 45, $"{CourseSlug}/B0b.html", isFreePreview: false).Value);

        course.Publish(Now);
        await courses.SaveAsync(course, Ct);
    }
}
