# Veri*Factu · qué está hecho, qué falta y de dónde sale cada cosa

Especificaciones oficiales de la AEAT descargadas el 2026-09-01 del
[portal de desarrolladores](https://www.agenciatributaria.es/AEAT.desarrolladores/Desarrolladores/_menu_/Documentacion/Sistemas_Informaticos_de_Facturacion_y_Sistemas_VERI_FACTU/Sistemas_Informaticos_de_Facturacion_y_Sistemas_VERI_FACTU.html):

| Documento | Versión | Qué fija |
|---|---|---|
| `AEAT-especificaciones-huella-v0.1.2.pdf` | 0.1.2, 27/08/2024 | Los campos de la huella, su orden, su formato y tres ejemplos con resultado |
| `AEAT-especificaciones-codigo-QR-v0.5.0.pdf` | 0.5.0 | La URL de cotejo, sus cuatro parámetros y dónde va el QR en la factura |
| `esquemas/*.xsd` y `esquemas/SistemaFacturacion.wsdl` | preproducción | La estructura del registro y las direcciones del servicio |
| `AEAT-firma-electronica-v0.1.5.pdf` + `firma-anexos/` | 0.1.5 | La firma de los registros — y a quién NO le aplica |
| `AEAT-descripcion-servicios-web-v1.0.3.pdf` | 1.0.3 | El protocolo, la autenticación y los límites de envío |
| `AEAT-declaracion-responsable.pdf` | — | Ejemplos de la declaración responsable del software |
| `AEAT-faq-desarrolladores.pdf` | 04-12-2025 | Preguntas frecuentes |

Están aquí y no solo enlazados porque son la fuente de la que salen decisiones del código, y
un enlace se rompe.

## Qué implementa la plataforma, y contra qué se comprueba

**La huella** (`VerifactuRecord.BuildHashInput`). Los tres ejemplos del documento oficial están
clavados como test en `VerifactuHuellaOficialTests`. Si alguien cambia el cálculo y se desvía de
la norma, falla la compilación de los tests en vez de rechazarlo la AEAT meses después.

Detalles que costaron un fallo y que conviene no volver a perder:

- El primer registro lleva `Huella=` **vacío**, sin ceros ni relleno. Los 64 ceros de
  `GenesisHash` son marca interna nuestra y se traducen al vacío al firmar.
- El registro de **anulación lleva otros campos**: cinco, sin tipo ni importes, con los nombres
  terminados en «Anulada».
- La marca temporal se firma **literal**, con su huso. El mismo instante escrito como `Z` y como
  `+00:00` da huellas distintas, así que lo que se firma tiene que ser exactamente lo que viaje
  en el XML.
- Salida en hexadecimal **mayúsculas**, 64 caracteres.

**El QR** (`VerifactuQr`). Cuatro parámetros, codificados uno a uno, con la URL base que depende
del entorno *y* de si el sistema remite. Comprobado contra el ejemplo del documento en
`VerifactuQrTests`, incluido el caso del número de serie con `&` dentro.

**La presentación** (`InvoiceDocumentRenderer`). El QR va arriba y centrado, con «QR tributario:»
encima y la leyenda de verificable debajo, y 6 mm de margen blanco. Lo pide el apartado 3 del
documento del QR: tiene que ser lo primero de la factura, no una esquina del pie.

## Lo que pide el registro y de dónde sale

Campos obligatorios de `RegistroFacturacionAltaType`, según `esquemas/SuministroInformacion.xsd`:

| Campo | De dónde sale hoy |
|---|---|
| `IDVersion` | constante del esquema |
| `IDFactura` (emisor, nº serie, fecha) | `VerifactuRecord` |
| `NombreRazonEmisor` | datos legales de la marca |
| `TipoFactura` | datos legales de la marca (F1/F2/F3 y R1..R5, validados contra el esquema) |
| `DescripcionOperacion` | concepto de la factura |
| `Desglose` | se deduce del total y la cuota (`VerifactuService.Breakdown`) |
| `CuotaTotal`, `ImporteTotal` | `VerifactuRecord` |
| `Encadenamiento` | `VerifactuRecord.PreviousHash` |
| `SistemaInformatico` | **configuración, hoy vacía** |
| `FechaHoraHusoGenRegistro` | `VerifactuRecord.GeneratedAt` |
| `TipoHuella`, `Huella` | `VerifactuRecord` |

Todo eso viaja junto en `VerifactuSubmission`, para que quien implemente el envío no tenga que
ir a buscarlo a cuatro sitios.

## La firma: no aplica en modo VERI*FACTU

El documento de firma electrónica (v0.1.5, apartado 2) lo dice sin rodeos:

> «La firma electrónica de los registros de facturación **sólo será exigible para los sistemas
> no VERI\*FACTU**, al no estar incluidos en las excepciones de los sistemas de remisión de
> facturas verificables recogidas en el artículo 3.»

Es decir: **remitiendo los registros a la AEAT no hay que firmar cada uno**. La firma es la
alternativa para quien NO remite y los conserva en su sistema.

Esto quita de encima la parte más incómoda de implementar: no hace falta XAdES.

El certificado **sí** hace falta, pero para otra cosa: para **autenticar la conexión** con el
servicio web. Del documento de servicios web (apartado 4.3):

> «Certificado: Las aplicaciones que envían información a los servicios web deberán autenticarse
> con certificado electrónico cualificado reconocido.»

Es decir, certificado de cliente sobre HTTPS. En .NET eso es
`HttpClientHandler.ClientCertificates`, no una librería de firma.

Protocolo: **SOAP 1.1, modo document/literal, UTF-8**. Las incidencias vienen como `Fault`.

## Los límites de envío, que condicionan el diseño

Del documento de servicios web:

- **Máximo 1.000 registros por envío.**
- **Control de flujo obligatorio**: hay un tiempo de espera entre envíos que empieza en **60
  segundos**, y la AEAT devuelve el vigente en cada respuesta (`TiempoEsperaEnvio`). Hay que
  esperar ese tiempo desde el envío anterior antes de mandar el siguiente.

Esto **descarta remitir cada factura en cuanto se cobra**: dos ventas en el mismo minuto ya
incumplirían, y una plataforma de cursos vende sola a cualquier hora.

Por eso los registros se acumulan como pendientes y los remite `VerifactuSubmissionJob` por
lotes, consultando antes `verifactu_flow` —que guarda hasta cuándo hay que esperar—. Está en la
base y no en memoria: un reinicio del proceso volvería a empezar y podría enviar antes de tiempo.

La espera se guarda **tal y como la manda la agencia**. Si la sube, es que está pidiendo que se
afloje el ritmo; ignorarla y usar un valor propio hace que se tarde más en ponerse al día, no
menos.

## El envío, que ya está escrito

`AeatVerifactuSubmitter` compone el XML, abre la conexión con el certificado y traduce la
respuesta. Son dos piezas separadas a propósito:

- **`VerifactuXmlBuilder`** compone el `RegFactuSistemaFacturacion`. Es la parte que se puede
  comprobar sin red y sin certificado, y por eso está aparte: `VerifactuXmlSchemaTests` valida
  lo que sale **contra los XSD oficiales** de `esquemas/`. Un alta encadenada, el primer
  registro de un emisor, una rectificativa y una anulación.
- **`AeatVerifactuSubmitter`** hace el POST SOAP 1.1 y lee `RespuestaRegFactuSistemaFacturacion`.

Cosas que costaron y conviene no volver a perder:

- Las fechas del registro van en **`dd-MM-yyyy`**, no en ISO. El tipo `sf:fecha` del esquema es
  así, y como todo el resto del código usa ISO, es el error que sale solo.
- La marca temporal viaja **literal, con su huso**, porque es la que se firmó en la huella. El
  mismo instante como `Z` y como `+01:00` da huellas distintas.
- El `Encadenamiento` **no se conforma con la huella del anterior**: pide su número de serie y
  su fecha de expedición, que no están en el registro que encadena. De ahí
  `IVerifactuRepository.GetByHashAsync`. Si falta, se para: rellenarlo a ojo pasa la validación
  y declara una cadena que apunta a una factura que no existe.
- El NIF del obligado va en la **cabecera**, una por envío. Un lote con dos emisores lo acepta
  el esquema y declara las facturas de uno a nombre del otro, así que se rechaza al componerlo.
- `AceptadoConErrores` **es aceptado**. Tratarlo como rechazo hace que se reintente algo ya
  declarado, y el reenvío entra como duplicado.
- Si la AEAT contesta menos líneas de las mandadas, las que faltan se quedan **pendientes**.
  Darlas por buenas dejaría facturas sin declarar creyendo que lo están, que es el único fallo
  de aquí del que no se sale solo.

## Lo que falta

1. **El certificado.** Un sello electrónico de entidad en `.pfx`, montado como secreto y
   apuntado con `Academy:Verifactu:Certificate:Path`. Sin él sigue puesto
   `DisabledVerifactuSubmitter` y los asientos nacen como «no hay que remitir», que dice la
   verdad. Con un `Path` que apunte a un fichero que no existe, la API **no arranca**: seguir
   dejaría los asientos naciendo pendientes y acumulándose sin que nadie los remita.

2. **`SistemaInformatico`.** Siete campos que describen el programa, obligatorios en cada
   registro y hoy vacíos: nombre y NIF del desarrollador, nombre e identificador del sistema,
   versión, número de instalación y tres indicadores de uso. Salen de la **declaración
   responsable** del software ante la AEAT, no de una decisión técnica. No se pueden verificar
   contra nada porque todavía no existen. El panel de Facturación avisa mientras falten.

3. **La prueba contra preproducción.** Con el certificado puesto y `SistemaInformatico` relleno,
   el entorno de pruebas (`prewww10` para un sello) es lo único que confirma de punta a punta lo
   que aquí se comprueba por esquema.

4. **`Desglose` con varios tipos.** Hoy se deduce del total, lo que vale mientras cada factura
   lleve un solo tipo impositivo. El día que se mezclen, tiene que venir de las líneas reales:
   deducirlo de un total mezclado da un desglose que cuadra en el importe y miente en el
   reparto.

## Entornos y endpoints

Del `SistemaFacturacion.wsdl` oficial, en `esquemas/`. Son **cuatro direcciones por servicio**,
no una, y la que manda depende de dos cosas independientes: el entorno y **el tipo de
certificado**.

| Entorno | Certificado | Envío (VerifactuSOAP) |
|---|---|---|
| Pruebas | representante | `https://prewww1.aeat.es/wlpl/TIKE-CONT/ws/SistemaFacturacion/VerifactuSOAP` |
| Pruebas | **sello** | `https://prewww10.aeat.es/…/VerifactuSOAP` |
| Producción | representante | `https://www1.agenciatributaria.gob.es/…/VerifactuSOAP` |
| Producción | **sello** | `https://www10.agenciatributaria.gob.es/…/VerifactuSOAP` |

**El sello usa el host «10».** Es a lo que se refiere la FNMT cuando dice que los sellos sirven
para el servicio web «apuntando a un endpoint específico», y es justo el certificado que hace
falta aquí, porque el servidor firma sin nadie delante. Mandar un sello al host «1» no da un
error que se entienda: da un rechazo de autenticación que parece un problema del certificado.

Está en `AeatEndpoints`, con sus tests.

El QR va a otro host distinto (`prewww2` / `www2`), que sale del documento del código QR:

| Entorno | URL del QR |
|---|---|
| Pruebas | `https://prewww2.aeat.es/wlpl/TIKE-CONT/ValidarQR` |
| Producción | `https://www2.agenciatributaria.gob.es/wlpl/TIKE-CONT/ValidarQR` |

Las operaciones del servicio son dos: `RegFactuSistemaFacturacion` para remitir y
`ConsultaFactuSistemaFacturacion` para consultar lo remitido.

Un solo ajuste manda sobre los dos: `Academy:Invoicing:Environment` (`pruebas` por defecto,
`produccion` si alguien lo escribe). Son un ajuste y no dos a propósito: con dos, alguien acaba
declarando en real con el QR de pruebas impreso, y cada cliente que lo escanee llega a un portal
donde su factura no existe.

## Certificado

Para que el servidor firme solo hace falta un **sello electrónico de entidad** (tipos 4 y 8 en
@firma), que va ligado al NIF de la empresa y está pensado para firma desatendida. El
certificado de representante también sirve para el envío por servicio web, y además es el único
que sirve para los trámites a mano en la sede.

En producción el sistema corre en Docker sobre Linux, donde no hay almacén de certificados de
Windows: el certificado va como fichero `.pfx` montado como secreto, con la contraseña por
variable de entorno. **Nunca en la imagen ni en el repositorio**: es una clave que firma
declaraciones fiscales.
