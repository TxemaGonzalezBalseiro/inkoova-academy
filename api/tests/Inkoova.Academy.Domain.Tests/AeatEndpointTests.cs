using FluentAssertions;
using Inkoova.Academy.Domain.Billing;

namespace Inkoova.Academy.Domain.Tests;

/// <summary>
/// Las direcciones del servicio de la AEAT, según su <c>SistemaFacturacion.wsdl</c>.
///
/// Son cuatro por servicio y no una. Equivocarse no da un error que se entienda: da un rechazo
/// de autenticación que parece un problema del certificado, y se pierde el día buscando donde
/// no es.
/// </summary>
public class AeatEndpointTests
{
    [Fact]
    public void A_seal_certificate_uses_a_different_host()
    {
        // Puertos `SistemaVerifactu` y `SistemaVerifactuSello` del WSDL. El sello es justo el
        // certificado que se usa para firmar sin intervención humana, que es lo que hace un
        // servidor que factura solo, así que ESTE es el caso normal aquí.
        AeatEndpoints.Verifactu(AeatEnvironment.Produccion, sealCertificate: false)
            .Should().Be("https://www1.agenciatributaria.gob.es/wlpl/TIKE-CONT/ws/SistemaFacturacion/VerifactuSOAP");

        AeatEndpoints.Verifactu(AeatEnvironment.Produccion, sealCertificate: true)
            .Should().Be("https://www10.agenciatributaria.gob.es/wlpl/TIKE-CONT/ws/SistemaFacturacion/VerifactuSOAP");
    }

    [Fact]
    public void The_test_environment_has_the_same_two_hosts_with_the_pre_prefix()
    {
        AeatEndpoints.Verifactu(AeatEnvironment.Pruebas, sealCertificate: false)
            .Should().Be("https://prewww1.aeat.es/wlpl/TIKE-CONT/ws/SistemaFacturacion/VerifactuSOAP");

        AeatEndpoints.Verifactu(AeatEnvironment.Pruebas, sealCertificate: true)
            .Should().Be("https://prewww10.aeat.es/wlpl/TIKE-CONT/ws/SistemaFacturacion/VerifactuSOAP");
    }

    [Fact]
    public void Production_never_points_at_the_test_portal()
    {
        foreach (var seal in new[] { true, false })
        {
            AeatEndpoints.Verifactu(AeatEnvironment.Produccion, seal).Should().NotContain("prewww");
            AeatEndpoints.Requerimiento(AeatEnvironment.Produccion, seal).Should().NotContain("prewww");
        }
    }

    [Fact]
    public void Submitting_and_answering_a_requirement_are_two_different_services()
    {
        // `sfVerifactu` y `sfRequerimiento` en el WSDL. El segundo es para sistemas que NO
        // emiten facturas verificables; mandar ahí un registro Veri*Factu no es lo mismo.
        AeatEndpoints.Verifactu(AeatEnvironment.Pruebas, false).Should().EndWith("/VerifactuSOAP");
        AeatEndpoints.Requerimiento(AeatEnvironment.Pruebas, false).Should().EndWith("/RequerimientoSOAP");
    }
}
