using FluentAssertions;
using Inkoova.Academy.Domain.Billing;
using Inkoova.Academy.Domain.ValueObjects;

namespace Inkoova.Academy.Domain.Tests;

/// <summary>
/// Qué incluye un plan.
///
/// Hasta que esto fue configurable, TODO plan daba TODOS los cursos, escrito en duro en
/// <c>PlanEntitlementService</c>. Estos tests fijan las reglas que sustituyen a esa constante.
/// </summary>
public sealed class PlanInclusionTests
{
    [Fact]
    public void Un_plan_que_no_incluye_nada_no_se_crea()
    {
        var creado = Crear(todoCursos: false, todoPacks: false, productos: []);

        // Un plan sin nada dentro es un cobro sin contraprestación. Se para en el dominio y no
        // solo en el panel, porque el panel no es el único que crea planes —está la siembra— y
        // porque el fallo no se ve hasta que alguien paga y se queda sin acceso.
        creado.IsFailure.Should().BeTrue();
        creado.Error.Code.Should().Be("plan.includes_nothing");
    }

    [Fact]
    public void Con_un_solo_producto_marcado_ya_es_un_plan_valido()
    {
        var curso = Guid.CreateVersion7();

        var creado = Crear(todoCursos: false, todoPacks: false, productos: [curso]);

        creado.IsSuccess.Should().BeTrue();
        creado.Value.IncludedProductIds.Should().ContainSingle().Which.Should().Be(curso);
    }

    [Fact]
    public void Los_interruptores_y_la_lista_se_suman_en_vez_de_excluirse()
    {
        var pack = Guid.CreateVersion7();

        // Un plan puede dar todos los cursos y ADEMÁS un pack concreto, sin dar los seis. Si la
        // lista sustituyera al interruptor en vez de sumarse, eso no se podría expresar.
        var creado = Crear(todoCursos: true, todoPacks: false, productos: [pack]);

        creado.IsSuccess.Should().BeTrue();
        creado.Value.IncludesAllCourses.Should().BeTrue();
        creado.Value.IncludesAllPacks.Should().BeFalse();
        creado.Value.IncludedProductIds.Should().ContainSingle().Which.Should().Be(pack);
    }

    [Fact]
    public void La_lista_se_normaliza_sin_repetidos_ni_ids_vacios()
    {
        var curso = Guid.CreateVersion7();

        var creado = Crear(
            todoCursos: false, todoPacks: false,
            productos: [curso, curso, Guid.Empty]);

        // Guid.Empty es lo que llega de una casilla sin resolver, y un repetido rompería la
        // clave primaria de plan_product al guardar.
        creado.IsSuccess.Should().BeTrue();
        creado.Value.IncludedProductIds.Should().ContainSingle().Which.Should().Be(curso);
    }

    [Fact]
    public void Editar_un_plan_para_dejarlo_sin_nada_se_rechaza_y_no_lo_toca()
    {
        var curso = Guid.CreateVersion7();
        var plan = Crear(todoCursos: true, todoPacks: true, productos: [curso]).Value;

        var descrito = plan.Describe(
            "Pro", Money.Euros(4_900), ["Comunidad Discord"],
            includesAllCourses: false, includesAllPacks: false, includedProductIds: [],
            displayOrder: 2);

        descrito.IsFailure.Should().BeTrue();

        // Y el plan sigue entero: un rechazo no puede dejarlo a medio editar, porque lo que
        // quedaría vivo es justo el plan que no da nada.
        plan.IncludesAllCourses.Should().BeTrue();
        plan.IncludesAllPacks.Should().BeTrue();
        plan.IncludedProductIds.Should().ContainSingle();
    }

    [Fact]
    public void Estrechar_un_plan_reemplaza_la_lista_en_vez_de_acumularla()
    {
        var viejo = Guid.CreateVersion7();
        var nuevo = Guid.CreateVersion7();

        var plan = Crear(todoCursos: false, todoPacks: false, productos: [viejo]).Value;

        var descrito = plan.Describe(
            "Starter", Money.Euros(1_900), [],
            includesAllCourses: false, includesAllPacks: false, includedProductIds: [nuevo],
            displayOrder: 1);

        descrito.IsSuccess.Should().BeTrue();

        // Acumular haría que quitar un curso del plan no tuviera ningún efecto, que es
        // exactamente el fallo que este modelo viene a evitar.
        plan.IncludedProductIds.Should().ContainSingle().Which.Should().Be(nuevo);
    }

    private static Domain.Common.Result<Plan, Domain.Common.Error> Crear(
        bool todoCursos, bool todoPacks, IReadOnlyList<Guid> productos) =>
        Plan.Create(
            Guid.CreateVersion7(), "pro", "Pro", BillingInterval.Quarterly, Money.Euros(4_900),
            ["Comunidad Discord"], todoCursos, todoPacks, productos, displayOrder: 2);
}
