namespace Inkoova.Academy.Domain.Billing;

/// <summary>
/// Las direcciones del servicio de remisión de la AEAT.
///
/// Fuente: <c>SistemaFacturacion.wsdl</c> publicado por la AEAT. Son CUATRO por servicio, no
/// una, y elegir mal no da un error claro: da un rechazo de autenticación que parece un problema
/// del certificado.
///
/// La combinación que manda son dos cosas independientes:
///
/// · <b>Entorno</b>: pruebas o producción.
/// · <b>Tipo de certificado</b>: un certificado de SELLO de entidad usa un host distinto
///   —el «10»— que uno de representante. Es lo que avisa la FNMT cuando dice que los sellos
///   sirven para el servicio web «apuntando a un endpoint específico».
///
/// Como el sello es justamente el que se usa para firmar sin intervención humana, que es lo que
/// hace un servidor que factura solo, esta distinción es la que más fácil se pasa por alto.
/// </summary>
public static class AeatEndpoints
{
    /// <summary>
    /// Remisión de registros de un sistema que emite facturas verificables. Es la nuestra.
    /// </summary>
    public static string Verifactu(AeatEnvironment environment, bool sealCertificate) =>
        $"{Host(environment, sealCertificate)}/wlpl/TIKE-CONT/ws/SistemaFacturacion/VerifactuSOAP";

    /// <summary>
    /// Remisión bajo requerimiento, para sistemas que NO emiten facturas verificables. No se usa
    /// mientras la academia remita en modo Veri*Factu; está por completitud del WSDL.
    /// </summary>
    public static string Requerimiento(AeatEnvironment environment, bool sealCertificate) =>
        $"{Host(environment, sealCertificate)}/wlpl/TIKE-CONT/ws/SistemaFacturacion/RequerimientoSOAP";

    private static string Host(AeatEnvironment environment, bool sealCertificate) =>
        (environment, sealCertificate) switch
        {
            (AeatEnvironment.Produccion, false) => "https://www1.agenciatributaria.gob.es",
            (AeatEnvironment.Produccion, true) => "https://www10.agenciatributaria.gob.es",
            (_, false) => "https://prewww1.aeat.es",
            (_, true) => "https://prewww10.aeat.es"
        };
}
