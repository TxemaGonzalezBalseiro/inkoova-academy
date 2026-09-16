using FluentAssertions;
using Inkoova.Academy.Domain.Catalog;
using Inkoova.Academy.Domain.ValueObjects;

namespace Inkoova.Academy.Domain.Tests;

/// <summary>
/// Renombrar un producto.
///
/// El título de un curso vive en DOS sitios: el curso y su producto. Al reimportar solo se
/// actualizaba el del curso, así que renombrar un curso cambiaba la ficha pública y dejaba el
/// producto con el nombre viejo —que es el que se ve al elegir qué incluye un plan y al conceder
/// un acceso desde el panel—. Dos nombres para la misma cosa, y ninguna pantalla decía cuál era
/// el bueno.
/// </summary>
public sealed class ProductRenameTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Renombrar_cambia_el_titulo_y_deja_el_slug_intacto()
    {
        var product = Crear("pre-curso-agent-engineering", "Pre-curso: fundamentos para Agent Engineering");

        product.Rename("Fundamentos de IA Engineer").IsSuccess.Should().BeTrue();

        product.Title.Should().Be("Fundamentos de IA Engineer");

        // El slug es la URL pública y la referencia con la que el importador reconoce este mismo
        // producto en cada pasada. Cambiarlo dejaría la ficha anterior en 404 y crearía un
        // duplicado en el catálogo, con los accesos concedidos apuntando al viejo.
        product.Slug.Value.Should().Be("pre-curso-agent-engineering");
    }

    [Fact]
    public void Renombrar_recorta_los_espacios_sobrantes()
    {
        var product = Crear("curso", "Antiguo");

        product.Rename("  Fundamentos de IA Engineer  ").IsSuccess.Should().BeTrue();

        product.Title.Should().Be("Fundamentos de IA Engineer");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Un_titulo_vacio_no_renombra_nada(string vacio)
    {
        var product = Crear("curso", "Antiguo");

        var renamed = product.Rename(vacio);

        renamed.IsFailure.Should().BeTrue();
        renamed.Error.Code.Should().Be("product.title_empty");

        // Y el producto conserva el que tenía: un rechazo no puede dejarlo sin nombre, porque
        // un producto sin título sale en blanco en el selector de planes y no se puede elegir.
        product.Title.Should().Be("Antiguo");
    }

    private static Product Crear(string slug, string title) =>
        Product.Create(
            Guid.CreateVersion7(), ProductType.Course, Slug.Create(slug).Value,
            title, oneOffPrice: null, Now).Value;
}
