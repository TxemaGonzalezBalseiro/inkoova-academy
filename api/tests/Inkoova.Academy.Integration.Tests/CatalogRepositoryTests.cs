using FluentAssertions;
using Inkoova.Academy.Domain.Catalog;
using Inkoova.Academy.Domain.ValueObjects;
using Inkoova.Academy.Infrastructure.Persistence;

namespace Inkoova.Academy.Integration.Tests;

[Collection(DatabaseCollection.Name)]
public class CatalogRepositoryTests(DatabaseFixture fixture)
{
    private static readonly DateTimeOffset Now = new(2026, 8, 30, 12, 0, 0, TimeSpan.Zero);

    /// <summary>These tests are not about cancellation; naming it once keeps the calls readable.</summary>
    private static readonly CancellationToken Ct = CancellationToken.None;

    [Fact]
    public async Task A_course_round_trips_with_its_sections_and_lessons()
    {
        var (products, courses) = Repositories();
        var (product, course) = await SeedCourseAsync(products, courses, "curso-ida-y-vuelta");

        var loaded = await courses.GetBySlugAsync(course.Slug, Ct);

        loaded.Should().NotBeNull();
        loaded!.ProductId.Should().Be(product.Id);
        loaded.Title.Should().Be(course.Title);
        loaded.TotalSections.Should().Be(2);
        loaded.TotalLessons.Should().Be(3);
        loaded.TotalDurationMinutes.Should().Be(30 + 45 + 60);
        loaded.Sections.Select(s => s.Title).Should().ContainInOrder("Bloque 0", "Bloque 1");
        loaded.Sections[0].Lessons.Select(l => l.Slug.Value).Should().ContainInOrder("b0-intro", "b0-loop");
    }

    [Fact]
    public async Task The_public_catalogue_hides_drafts_and_shows_coming_soon()
    {
        var (products, courses) = Repositories();

        var (_, published) = await SeedCourseAsync(products, courses, "catalogo-publicado");
        published.Publish(Now);
        await courses.SaveAsync(published, Ct);

        var (_, draft) = await SeedCourseAsync(products, courses, "catalogo-borrador");

        var (_, soon) = await SeedCourseAsync(products, courses, "catalogo-proximamente");
        soon.MarkComingSoon();
        await courses.SaveAsync(soon, Ct);

        var catalog = await courses.GetPublicCatalogAsync(Ct);
        var slugs = catalog.Select(c => c.Slug.Value).ToList();

        slugs.Should().Contain("catalogo-publicado");
        slugs.Should().Contain("catalogo-proximamente");
        slugs.Should().NotContain("catalogo-borrador");
    }

    [Fact]
    public async Task Saving_the_aggregate_removes_sections_that_are_no_longer_part_of_it()
    {
        var (products, courses) = Repositories();
        var (_, course) = await SeedCourseAsync(products, courses, "curso-convergente");

        // Rebuild the aggregate with a single section, the way the importer does when a
        // block disappears from the manifest.
        var trimmed = Course.Rehydrate(
            course.Id, course.ProductId, course.Slug, course.Title, course.ShortDescription,
            course.LongDescription, null, PublicationStatus.Draft, CourseLevel.Advanced,
            false, false, Now, null, [course.Sections[0]]);

        await courses.SaveAsync(trimmed, Ct);

        var loaded = await courses.GetBySlugAsync(course.Slug, Ct);
        loaded!.TotalSections.Should().Be(1);
        loaded.TotalLessons.Should().Be(2);
    }

    [Fact]
    public async Task The_product_of_a_lesson_resolves_through_its_section_and_course()
    {
        var (products, courses) = Repositories();
        var (product, course) = await SeedCourseAsync(products, courses, "curso-producto-de-leccion");

        var lessonId = course.AllLessons.First().Id;
        var resolved = await courses.GetProductIdForLessonAsync(lessonId, Ct);

        resolved.Should().Be(product.Id);
    }

    [Fact]
    public async Task Two_courses_cannot_share_a_slug()
    {
        var (products, courses) = Repositories();
        await SeedCourseAsync(products, courses, "slug-unico");

        var act = async () => await SeedCourseAsync(products, courses, "slug-unico");

        await act.Should().ThrowAsync<Npgsql.PostgresException>()
            .Where(e => e.SqlState == "23505");
    }

    [Fact]
    public async Task Only_published_courses_contribute_products_to_a_subscription()
    {
        var (products, courses) = Repositories();

        var (publishedProduct, published) = await SeedCourseAsync(products, courses, "incluido-en-plan");
        published.Publish(Now);
        await courses.SaveAsync(published, Ct);

        var (draftProduct, _) = await SeedCourseAsync(products, courses, "no-incluido-en-plan");

        var catalogProducts = await products.GetAllCourseProductsAsync(Ct);
        var ids = catalogProducts.Select(p => p.Id).ToList();

        ids.Should().Contain(publishedProduct.Id);
        ids.Should().NotContain(draftProduct.Id);
    }

    private (ProductRepository Products, CourseRepository Courses) Repositories()
    {
        DapperTypeHandlers.Register();
        var connections = new NpgsqlConnectionFactory(fixture.ConnectionString);
        return (new ProductRepository(connections), new CourseRepository(connections));
    }

    private static async Task<(Product Product, Course Course)> SeedCourseAsync(
        ProductRepository products,
        CourseRepository courses,
        string slug)
    {
        
        var product = Product.Create(
            Guid.CreateVersion7(), ProductType.Course, Slug.Create(slug).Value,
            $"Curso {slug}", Money.Euros(9_900), Now).Value;

        await products.UpsertAsync(product, Ct);

        var course = Course.Create(
            Guid.CreateVersion7(), product.Id, Slug.Create(slug).Value, $"Curso {slug}",
            "Descripción corta.", "Descripción larga.", CourseLevel.Advanced, Now).Value;

        var b0 = Section.Create(Guid.CreateVersion7(), course.Id, 0, "Bloque 0").Value;
        course.AddSection(b0);
        b0.AddLesson(Lesson.Create(
            Guid.CreateVersion7(), b0.Id, 0, Slug.Create("b0-intro").Value, "Intro",
            LessonType.Slides, 30, $"{slug}/B0.html", isFreePreview: true).Value);
        b0.AddLesson(Lesson.Create(
            Guid.CreateVersion7(), b0.Id, 1, Slug.Create("b0-loop").Value, "Loop",
            LessonType.Slides, 45, $"{slug}/B0b.html", isFreePreview: false).Value);

        var b1 = Section.Create(Guid.CreateVersion7(), course.Id, 1, "Bloque 1").Value;
        course.AddSection(b1);
        b1.AddLesson(Lesson.Create(
            Guid.CreateVersion7(), b1.Id, 0, Slug.Create("b1-lab").Value, "Lab",
            LessonType.Lab, 60, $"{slug}/lab1.md", isFreePreview: false).Value);

        await courses.SaveAsync(course, Ct);

        return (product, course);
    }
}
