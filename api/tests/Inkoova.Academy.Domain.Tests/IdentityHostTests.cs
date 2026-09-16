using FluentAssertions;
using Inkoova.Academy.Domain.Identities;

namespace Inkoova.Academy.Domain.Tests;

/// <summary>
/// Qué marca corresponde a un dominio.
///
/// De esto sale con qué cabecera, qué emisor y qué colores se genera un certificado, así que
/// fallar aquí es firmar con la marca de otro. Los casos que importan no son los evidentes:
/// son el puerto, el «www.» y las mayúsculas.
/// </summary>
public class IdentityHostTests
{
    [Fact]
    public void The_port_does_not_change_the_brand()
    {
        var marca = Marca("localhost");

        // En desarrollo la web va por 5173 y la API por 5080. Son la misma marca, y quien
        // configura el dominio escribe «localhost», no «localhost:5173».
        marca.MatchesHost("localhost").Should().BeTrue();
        marca.MatchesHost("localhost:5173").Should().BeTrue();
        marca.MatchesHost("localhost:5080").Should().BeTrue();
    }

    [Fact]
    public void Www_matches_the_bare_domain_in_both_directions()
    {
        // Configurar «www.marca.com» no puede dejar fuera a quien entra por «marca.com», ni al
        // revés: son la misma web y quien la configura no debería tener que saberlo.
        Marca("www.marca.com").MatchesHost("marca.com").Should().BeTrue();
        Marca("marca.com").MatchesHost("www.marca.com").Should().BeTrue();
    }

    [Fact]
    public void The_comparison_ignores_case_and_the_trailing_dot()
    {
        var marca = Marca("academy.inkoova.com");

        marca.MatchesHost("ACADEMY.INKOOVA.COM").Should().BeTrue();
        // El punto final es un nombre absoluto, y algunos clientes lo mandan.
        marca.MatchesHost("academy.inkoova.com.").Should().BeTrue();
    }

    [Fact]
    public void A_different_domain_is_a_different_brand()
    {
        var marca = Marca("academy.inkoova.com");

        marca.MatchesHost("otra-marca.com").Should().BeFalse();
        // Un subdominio distinto tampoco: «cursos.» y «academy.» son sitios distintos.
        marca.MatchesHost("cursos.inkoova.com").Should().BeFalse();
    }

    [Fact]
    public void A_brand_without_a_domain_never_matches()
    {
        // Es lo que impide que una marca a medio configurar se quede con todo el tráfico que no
        // encaja en ninguna otra. Ese tráfico tiene que caer en la principal.
        var sinDominio = Marca("");

        sinDominio.MatchesHost("marca.com").Should().BeFalse();
        sinDominio.MatchesHost("").Should().BeFalse();
        sinDominio.MatchesHost(null).Should().BeFalse();
    }

    [Fact]
    public void Nothing_matches_an_empty_host()
    {
        // Sin host —un job, una consola— no hay dominio del que tirar y manda la principal, que
        // lo decide el repositorio. Aquí lo único que importa es que no encaje por accidente.
        Marca("marca.com").MatchesHost(null).Should().BeFalse();
        Marca("marca.com").MatchesHost("   ").Should().BeFalse();
    }

    [Fact]
    public void An_ipv6_host_keeps_its_brackets_and_loses_its_port()
    {
        // Los dos puntos de una IPv6 no son un puerto. Cortar por el último ':' sin mirar los
        // corchetes convertiría «[::1]» en «[:», que no es de nadie.
        AcademyIdentity.NormalizeHost("[::1]").Should().Be("[::1]");
        AcademyIdentity.NormalizeHost("[::1]:5080").Should().Be("[::1]");
    }

    private static AcademyIdentity Marca(string dominio) =>
        AcademyIdentity.Create(
            Guid.CreateVersion7(), $"marca-{Guid.CreateVersion7():N}", "Marca",
            "Lema", logoUrl: "", publicDomain: dominio, supportEmail: "hola@marca.com").Value;
}
