using FluentAssertions;
using Inkoova.Academy.Application.Access;
using Inkoova.Academy.Domain.Billing;
using Inkoova.Academy.Domain.Catalog;
using Inkoova.Academy.Domain.ValueObjects;
using Dapper;
using Inkoova.Academy.Infrastructure.Persistence;

namespace Inkoova.Academy.Integration.Tests;

/// <summary>
/// Lo que un plan concede, contra la base de verdad.
///
/// Hasta que esto fue configurable, TODO plan daba TODOS los cursos, escrito en duro. Lo que se
/// fija aquí es la mitad que se olvida al hacer este cambio: que **estrechar** un plan retire el
/// acceso a quien ya lo tenía. Sin eso, quitar un curso del panel no haría nada —el derecho
/// seguiría vivo, renovándose con cada pago— y el panel diría una cosa mientras el alumno ve otra.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class PlanEntitlementTests(DatabaseFixture fixture)
{
    private static readonly DateTimeOffset Now = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Hasta = Now.AddDays(30);
    private static readonly CancellationToken Ct = CancellationToken.None;

    [Fact]
    public async Task Un_plan_de_catalogo_completo_concede_todos_los_cursos()
    {
        var (service, seed, entitlements) = Build();
        var uno = await seed.PublishedCourseAsync();
        var otro = await seed.PublishedCourseAsync();
        var user = await seed.UserAsync(fixture.ConnectionString);

        var plan = Plan(todoCursos: true, todoPacks: false, productos: []);

        (await service.ApplyAsync(user, plan, Hasta, Ct)).IsSuccess.Should().BeTrue();

        await AssertAccesoAsync(entitlements, user, uno.Id, esperado: true);
        await AssertAccesoAsync(entitlements, user, otro.Id, esperado: true);
    }

    [Fact]
    public async Task Un_plan_con_lista_cerrada_concede_solo_lo_marcado()
    {
        var (service, seed, entitlements) = Build();
        var dentro = await seed.PublishedCourseAsync();
        var fuera = await seed.PublishedCourseAsync();
        var user = await seed.UserAsync(fixture.ConnectionString);

        var plan = Plan(todoCursos: false, todoPacks: false, productos: [dentro.Id]);

        (await service.ApplyAsync(user, plan, Hasta, Ct)).IsSuccess.Should().BeTrue();

        await AssertAccesoAsync(entitlements, user, dentro.Id, esperado: true);
        await AssertAccesoAsync(entitlements, user, fuera.Id, esperado: false);
    }

    [Fact]
    public async Task Estrechar_el_plan_RETIRA_el_acceso_que_ya_estaba_concedido()
    {
        var (service, seed, entitlements) = Build();
        var sigue = await seed.PublishedCourseAsync();
        var quitado = await seed.PublishedCourseAsync();
        var user = await seed.UserAsync(fixture.ConnectionString);

        // Primero con el catálogo entero, como estaban todos los planes hasta ahora.
        (await service.ApplyAsync(user, Plan(true, false, []), Hasta, Ct)).IsSuccess.Should().BeTrue();
        await AssertAccesoAsync(entitlements, user, quitado.Id, esperado: true);

        // Y ahora estrechado a uno solo. Esta es la mitad que se olvida: sin ella, quitar un
        // curso del plan no tendría ningún efecto sobre quien ya lo tenía.
        (await service.ApplyAsync(user, Plan(false, false, [sigue.Id]), Hasta, Ct))
            .IsSuccess.Should().BeTrue();

        await AssertAccesoAsync(entitlements, user, sigue.Id, esperado: true);
        await AssertAccesoAsync(entitlements, user, quitado.Id, esperado: false);
    }

    [Fact]
    public async Task Estrechar_el_plan_NO_toca_lo_que_el_alumno_compro_aparte()
    {
        var (service, seed, entitlements) = Build();
        var comprado = await seed.PublishedCourseAsync();
        var otro = await seed.PublishedCourseAsync();
        var user = await seed.UserAsync(fixture.ConnectionString);

        var granted = Entitlement.Grant(
            Guid.CreateVersion7(), user, comprado.Id, EntitlementSource.Purchase, Now, null).Value;
        await entitlements.UpsertAsync(granted, Ct);

        // El plan pasa a no incluirlo. Una compra suelta no depende del plan y sobrevive: lo
        // contrario sería quitarle a alguien algo que pagó por su cuenta.
        (await service.ApplyAsync(user, Plan(false, false, [otro.Id]), Hasta, Ct))
            .IsSuccess.Should().BeTrue();

        await AssertAccesoAsync(entitlements, user, comprado.Id, esperado: true);
    }

    [Fact]
    public async Task Los_interruptores_y_la_lista_se_suman()
    {
        var (service, seed, entitlements) = Build();
        var curso = await seed.PublishedCourseAsync();
        var packDentro = await seed.PackAsync();
        var packFuera = await seed.PackAsync();
        var user = await seed.UserAsync(fixture.ConnectionString);

        // Todos los cursos Y un pack concreto, sin dar los seis.
        var plan = Plan(todoCursos: true, todoPacks: false, productos: [packDentro.Id]);

        (await service.ApplyAsync(user, plan, Hasta, Ct)).IsSuccess.Should().BeTrue();

        await AssertAccesoAsync(entitlements, user, curso.Id, esperado: true);
        await AssertAccesoAsync(entitlements, user, packDentro.Id, esperado: true);
        await AssertAccesoAsync(entitlements, user, packFuera.Id, esperado: false);
    }

    [Fact]
    public async Task Un_plan_estrechado_se_guarda_y_se_relee_con_su_lista()
    {
        var (_, seed, _) = Build();
        var plans = new PlanRepository(new NpgsqlConnectionFactory(fixture.ConnectionString));

        var uno = await seed.PublishedCourseAsync();
        var dos = await seed.PublishedCourseAsync();

        var codigo = $"plan-{Guid.CreateVersion7().ToString()[^8..]}";
        var plan = Domain.Billing.Plan.Create(
            Guid.CreateVersion7(), codigo, "Plan de prueba", BillingInterval.Monthly,
            Money.Euros(1_900), ["Comunidad Discord"],
            includesAllCourses: false, includesAllPacks: false,
            includedProductIds: [uno.Id, dos.Id], displayOrder: 9).Value;

        await plans.UpsertAsync(plan, Ct);

        var releido = await plans.GetByCodeAsync(codigo, Ct);

        releido.Should().NotBeNull();
        releido!.IncludesAllCourses.Should().BeFalse();
        releido.IncludedProductIds.Should().BeEquivalentTo([uno.Id, dos.Id]);

        // Guardar otra vez con menos: la lista se reemplaza, no se acumula. Si se acumulara,
        // quitar un curso del panel no serviría de nada.
        plan.Describe(
            "Plan de prueba", Money.Euros(1_900), ["Comunidad Discord"],
            includesAllCourses: false, includesAllPacks: false,
            includedProductIds: [uno.Id], displayOrder: 9);

        await plans.UpsertAsync(plan, Ct);

        (await plans.GetByCodeAsync(codigo, Ct))!
            .IncludedProductIds.Should().ContainSingle().Which.Should().Be(uno.Id);
    }

    // ── andamiaje ────────────────────────────────────────────────────────────────────────

    private (PlanEntitlementService Service, Seeder Seeder, EntitlementRepository Entitlements) Build()
    {
        DapperTypeHandlers.Register();
        var connections = new NpgsqlConnectionFactory(fixture.ConnectionString);

        var products = new ProductRepository(connections);
        var entitlements = new EntitlementRepository(connections);
        var clock = new FixedClock(Now);

        return (
            new PlanEntitlementService(new EntitlementService(entitlements, products, clock), products),
            new Seeder(products, new CourseRepository(connections)),
            entitlements);
    }

    /// <summary>
    /// Siembra productos de verdad.
    ///
    /// Un curso necesita su fila en <c>course</c> y estar PUBLICADO: el catálogo de productos de
    /// curso hace JOIN contra ella y filtra por estado, porque un borrador no puede abrirse solo
    /// porque alguien tenga suscripción. Un producto suelto sin curso detrás no lo ve nadie, y
    /// con eso el test no probaría lo que dice probar.
    /// </summary>
    private sealed class Seeder(ProductRepository products, CourseRepository courses)
    {
        public async Task<Product> PackAsync() => await ProductAsync(ProductType.Pack);

        public async Task<Product> PublishedCourseAsync()
        {
            var product = await ProductAsync(ProductType.Course);
            var slug = product.Slug;

            var course = Course.Create(
                Guid.CreateVersion7(), product.Id, slug, product.Title,
                "Descripción corta.", "Descripción larga.", CourseLevel.Advanced, Now).Value;

            var seccion = Section.Create(Guid.CreateVersion7(), course.Id, 0, "Bloque 0").Value;
            course.AddSection(seccion);
            seccion.AddLesson(Lesson.Create(
                Guid.CreateVersion7(), seccion.Id, 0, Slug.Create("b0-intro").Value, "Intro",
                LessonType.Slides, 30, $"{slug.Value}/B0.html", isFreePreview: false).Value);

            course.Publish(Now);
            await courses.SaveAsync(course, Ct);

            return product;
        }

        /// <summary>
        /// Un alumno de verdad. <c>entitlement.user_id</c> tiene clave ajena, así que un GUID
        /// inventado revienta el INSERT en vez de conceder nada.
        /// </summary>
        public async Task<Guid> UserAsync(string connectionString)
        {
            using var connection = await new NpgsqlConnectionFactory(connectionString).OpenAsync(Ct);

            var userId = Guid.CreateVersion7();
            var email = $"plan-{userId:N}@test.local";

            await connection.ExecuteAsync(
                """
                INSERT INTO identity_user (id, normalized_email, email, email_confirmed, password_hash,
                                           security_stamp, concurrency_stamp, lockout_end,
                                           lockout_enabled, access_failed_count, created_at)
                VALUES (@userId, @normalized, @email, true, 'hash', 'stamp', 'concurrency', null, true, 0, @now);

                INSERT INTO app_user (id, email, display_name, created_at)
                VALUES (@userId, @email, 'Alumno de prueba', @now);
                """,
                new { userId, normalized = email.ToUpperInvariant(), email, now = Now });

            return userId;
        }

        private async Task<Product> ProductAsync(ProductType type)
        {
            // La cola del GUID y no la cabeza: v7 empieza por marca de tiempo, así que dos
            // productos creados en el mismo instante compartirían prefijo y chocarían de slug.
            var slug = $"{type.ToString().ToLowerInvariant()}-{Guid.CreateVersion7().ToString()[^12..]}";

            var product = Product.Create(
                Guid.CreateVersion7(), type, Slug.Create(slug).Value,
                $"Producto {slug}", Money.Euros(9_900), Now).Value;

            await products.UpsertAsync(product, Ct);

            return product;
        }
    }

    private static Domain.Billing.Plan Plan(
        bool todoCursos, bool todoPacks, IReadOnlyList<Guid> productos) =>
        Domain.Billing.Plan.Create(
            Guid.CreateVersion7(), $"p-{Guid.CreateVersion7().ToString()[^8..]}", "Plan",
            BillingInterval.Monthly, Money.Euros(1_900), [],
            todoCursos, todoPacks, productos, displayOrder: 1).Value;

    private static async Task AssertAccesoAsync(
        EntitlementRepository entitlements, Guid userId, Guid productId, bool esperado)
    {
        var entitlement = await entitlements.GetForUserAndProductAsync(userId, productId, Ct);
        var vigente = entitlement is not null && entitlement.IsValidAt(Now);

        vigente.Should().Be(
            esperado,
            esperado
                ? "el plan lo incluye"
                : "el plan ya no lo incluye y el derecho derivado del plan tiene que retirarse");
    }

    private sealed class FixedClock(DateTimeOffset now) : Application.Abstractions.IClock
    {
        public DateTimeOffset UtcNow => now;
    }
}
