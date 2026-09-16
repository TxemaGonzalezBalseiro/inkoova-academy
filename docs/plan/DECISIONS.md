# DECISIONS · Registro de decisiones de arquitectura (ADR)

Formato corto: Contexto → Decisión → Alternativas → Consecuencias.
Una decisión no se edita: se supersede con una ADR nueva que la referencia.

| # | Decisión | Estado | Fecha |
|---|---|---|---|
| ADR-001 | Monorepo `/api /web /content /infra /db` | Aceptada | 2026-08-30 |
| ADR-002 | Academia como app independiente en `academy.inkoova.com` | Aceptada | 2026-08-30 |
| ADR-003 | Contenido servido por la API con token firmado de vida corta | Aceptada | 2026-08-30 |
| ADR-004 | ASP.NET Core Identity en vez de Keycloak | Aceptada | 2026-08-30 |
| ADR-005 | `IMemoryCache` en vez de Valkey/Redis | Aceptada | 2026-08-30 |
| ADR-006 | Dapper + migraciones SQL versionadas (DbUp), nunca EF Core | Aceptada | 2026-08-30 |
| ADR-007 | Acceso unificado por `Entitlement` en vez de "suscripción activa" | Aceptada | 2026-08-30 |
| ADR-008 | El contenido HTML se importa, no se reescribe (salvo tres transformaciones de la copia servida) | Aceptada | 2026-08-31 |
| ADR-009 | La tutoría convocada es una tabla distinta de la tutoría dada | Aceptada | 2026-08-31 |
| ADR-010 | Invitaciones de calendario por iCalendar adjunto, no por API de calendario | Aceptada | 2026-08-31 |
| ADR-011 | El registro Veri*Factu es tabla propia; el envío a la AEAT, un puerto sin implementar | Aceptada | 2026-08-31 |
| ADR-012 | La API autoriza el contenido; los bytes los sirve Caddy (`forward_auth`) | Aceptada | 2026-09-04 |

---

## ADR-001 · Monorepo

**Contexto.** T-00 pide decidir entre monorepo y repos separados. El sistema tiene cuatro
artefactos que se despliegan juntos en una única VPS (T-13): API .NET, SPA React, contenido
importado y definición de infraestructura. El equipo es de una persona.

**Decisión.** Monorepo único con raíces `api/`, `web/`, `content/`, `infra/`, `db/`, `docs/`.
Un pipeline de CI, una versión, un `docker compose`.

**Alternativas.** Repos separados por artefacto: aporta aislamiento de permisos y releases
independientes; a cambio obliga a coordinar versiones entre API y SPA en cada cambio de
contrato, que es el trabajo que más se repite en fases 0 y 1.

**Consecuencias.**
- El contrato API↔SPA cambia en un solo commit y CI valida ambos lados a la vez.
- Despliegue atómico: imágenes del mismo SHA.
- Coste: checkout de CI más pesado. Mitigado con filtros `paths` en los workflows.

---

## ADR-002 · Academia como aplicación independiente

**Contexto.** Existe una app React de Inkoova desplegada en Azure (rediseño "control room").
T-00 pide decidir si la academia vive dentro de esa app o aparte.

**Decisión.** App independiente en `academy.inkoova.com`, con su propio Vite, router y ciclo de
despliegue. Comparte con la app corporativa **solo los design tokens** (Manrope, azul `#1E3A8A`,
magenta `#DB2777`, teal `#0891B2`), copiados en `web/src/styles/tokens.css`.

**Alternativas.** Nueva sección dentro de la app existente: ahorra un dominio y un despliegue,
pero acopla la academia al ciclo de release de la web corporativa y a su infraestructura Azure,
en contra del objetivo de coste de T-13 (< 10 €/mes en VPS propia).

**Consecuencias.**
- La academia se despliega sin tocar la web corporativa y viceversa.
- Los tokens se duplican. Si divergen se corrigen copiando desde la fuente corporativa; no se
  introduce un paquete npm compartido hasta que haya un tercer consumidor.
- Login propio (ADR-004); no hay SSO con la web corporativa. Backlog si aparece B2B.

---

## ADR-003 · Cómo se sirve el contenido del curso

**Contexto.** El contenido son HTML planos con assets (`assets/css`, `assets/js`). Debe ser
inaccesible sin derecho de acceso y a la vez seguir funcionando con doble clic fuera de la
plataforma (criterio de aceptación de T-07).

**Decisión.** Los ficheros viven en un volumen privado montado en la API (`/srv/content`).
La API expone `GET /api/content/{token}/{ruta}`, donde `token` es un token opaco de 60 s
firmado con HMAC-SHA256 que se emite solo tras pasar `IAccessPolicy`, y `ruta` es la ruta real
del fichero dentro del volumen. `GET /api/content/{token}` a secas redirige a la ruta canónica,
que es lo que el player construye. La abstracción es `IContentStorage`, con implementación
`LocalDiskContentStorage` (por defecto) y `AzureBlobContentStorage` (opcional, misma interfaz,
se activa por configuración).

**Por qué la ruta va en la URL además del token.** Sin ella no funciona ningún enlace relativo
del contenido importado. Un bloque pide `../assets/css/curso.css`; servido en
`/api/content/{token}`, el navegador resuelve ese `..` contra `/api/` y se sale del token, así
que el CSS y el JS daban 404 y la lección salía sin estilos con un 200 en el player. Con la ruta
real el árbol servido tiene la misma forma que el que el HTML espera, que es la única manera de
que sus enlaces funcionen sin reescribir el contenido (ADR-008).

**Qué ampara un token.** Tres cosas y ninguna más:

1. Su propio documento.
2. El chrome compartido del curso (`assets/`): CSS, visor, imágenes. Es presentación.
3. Su propia carpeta de datos, `<documento>.data/`, donde el importador deja los quizzes y los
   ejercicios de esa lección y solo de esa.

Los documentos ajenos quedan fuera aunque compartan curso, y los datos de otra lección también.

**Alternativas.**
- Servir los ficheros directamente por Caddy: gratis y rápido, pero no hay forma de aplicar
  `IAccessPolicy` por lección sin duplicar la autorización en el proxy.
- Azure Blob con SAS, como proponía T-07: funciona, pero añade dependencia de Azure y coste de
  egress, en contra de T-13. Queda detrás de la interfaz como implementación alternativa.

**Consecuencias.**
- El contenido no es enlazable ni cacheable públicamente; un token caducado devuelve 403 y el
  player lo renueva (criterio de aceptación de T-07).
- La API sirve bytes: hay que usar `SendFileAsync` y limitar tamaño para no cargar en memoria.
- Los HTML originales no se modifican, así que siguen abriéndose por doble clic.
- El árbol copiado debe conservar las rutas relativas del origen. Por eso el pre-curso vive en
  `agent-engineering-v3/pre-curso/` aunque sea un producto aparte del catálogo: sacarlo a una
  carpeta propia dejaba su `../assets` apuntando fuera.
- **El banco de quizzes se reparte al importar.** `assets/js/quizzes-data.js` trae los quizzes y
  los ejercicios de B0 a B7 en un solo fichero de 73 KB, y cada bloque lo cargaba entero: un
  token gratuito alcanzaba las preguntas y los `model_answer` de los bloques de pago. El
  importador lo parte en `<documento>.data/quizzes.js` y reescribe el `<script>` de cada
  documento. Ver ADR-008: es la excepción a "el contenido no se reescribe", y solo afecta a la
  copia servida.

---

## ADR-004 · ASP.NET Core Identity en vez de Keycloak

**Contexto.** T-00 planteaba Keycloak; T-03 y T-13 lo revierten por coste y por número de piezas.

**Decisión.** ASP.NET Core Identity sobre la misma base Postgres. Las tablas de Identity se crean
por migración SQL versionada como el resto del esquema; la lectura de la aplicación es Dapper.
JWT de acceso de 15 minutos + refresh token rotatorio persistido y hasheado.

**Alternativas.** Keycloak: aporta OIDC completo, federación y SSO listo para B2B; a cambio son
cientos de MB de RAM en una VPS de 4 GB y una pieza más que mantener.

**Consecuencias.**
- Un contenedor menos y coste adicional cero.
- Federación empresarial (SAML/OIDC) queda en backlog; si aparece B2B se pone Keycloak delante de
  Identity, no en su lugar.
- Hay que implementar a mano verificación de email, reset de contraseña y rotación de refresh.

---

## ADR-005 · `IMemoryCache` en vez de Valkey

**Contexto.** T-02 pedía Valkey con TTL de 5 minutos. T-13 lo elimina para bajar coste y piezas.

**Decisión.** `IMemoryCache` en proceso, TTL 5 minutos, invalidación explícita
(`ICatalogCache.Invalidate()`) desde el admin al publicar.

**Alternativas.** Valkey/Redis: necesario si hubiera más de una instancia de API. Hoy hay una.

**Consecuencias.**
- Si se escala a N réplicas la invalidación deja de ser global. La interfaz `ICatalogCache` está
  aislada para poder cambiar la implementación sin tocar los handlers.

---

## ADR-006 · Dapper + DbUp, nunca EF Core

**Contexto.** Convención fijada en el README del plan.

**Decisión.** Todo acceso a datos por Dapper con SQL explícito. Todo cambio de esquema por fichero
`db/migrations/VNNN__nombre.sql` aplicado con DbUp desde un ejecutable dedicado
(`Inkoova.Academy.Migrator`), que corre como paso previo al arranque de la API.

**Alternativas.** EF Core: migraciones automáticas y menos SQL a mano; excluido por decisión de
proyecto.

**Consecuencias.**
- El SQL es visible y revisable en el diff; los índices son deliberados.
- Las migraciones son de solo avance (no hay `Down`). Un error se corrige con otra migración.
- DbUp registra lo aplicado en `schema_versions`, lo que la hace idempotente en CI.

---

## ADR-007 · `Entitlement` como única puerta de acceso

**Contexto.** El añadido de T-01/T-04 introduce compras únicas, packs y programa vitalicio junto a
las suscripciones. Si `IAccessPolicy` preguntara "¿hay suscripción activa?" habría que añadir un
`or` por cada nueva forma de vender.

**Decisión.** Toda forma de acceso materializa filas en
`entitlement (user_id, product_id, source, valid_from, valid_until)`. `IAccessPolicy` responde una
sola pregunta: ¿existe un entitlement vigente del usuario sobre el producto que contiene esta
lección, o la lección es preview?

**Alternativas.** Evaluar suscripción + compras + inclusión en plan en tiempo de consulta: más
normalizado, pero convierte la comprobación en un join de cuatro tablas por cada lección de cada
temario.

**Consecuencias.**
- Añadir una forma de vender es escribir entitlements en el webhook; la autorización no cambia.
- Los entitlements derivados de un plan (`source = 'plan_included'`) hay que retirarlos al
  expirar: lo hace un job diario idempotente, y además llevan `valid_until` como red de seguridad.

---

## ADR-008 · El contenido se importa, no se reescribe

**Contexto.** El curso Agent Engineering v3.0 existe como HTML generado por `content.py`, con
`IntersectionObserver`, hash routing y theme switcher propios. Reescribirlo a componentes React
sería tirar el pipeline de autoría con el que se producen los cuatro cursos del programa.

**Decisión.** Un importador (`content/tools/importer.py`) lee la carpeta del curso y produce
`course.manifest.json`. La plataforma consume el manifest y sirve el HTML dentro de un `iframe`
con `sandbox`, comunicándose por `postMessage` para progreso y navegación. El array `BLOQUES`
hardcodeado del `index.html` original pasa a leerse del manifest.

**Alternativas.** Portar los slides a MDX: mejor integración y SEO, pero descarta el pipeline
`content.py`.

**Consecuencias.**
- El HTML original sigue siendo la fuente de verdad y sigue abriéndose en local.
- El acoplamiento plataforma↔contenido es un contrato `postMessage` versionado
  (`content/PROTOCOL.md`), no una dependencia de código.
- El `sandbox` del iframe impide que el contenido acceda a la sesión del alumno.

**Segunda excepción: el curso que vive en un solo fichero.** El curso de prompt engineering es
un HTML con sus 54 lecciones dentro y su propio índice lateral. Servirlo tal cual tenía dos
consecuencias, y la segunda es la grave:

1. Dentro del player se veían **dos índices de curso**, uno al lado del otro: el «Temario» de
   la plataforma y el `<nav id="rail">` del documento.
2. **El muro de pago no existía.** El token de una lección ampara «su propio documento», y su
   documento era el fichero entero: un alumno sin plan que abría cualquiera de las 15 lecciones
   gratuitas recibía las 54 en una sola respuesta —177 KB con el curso de pago dentro— y el
   índice del documento navegaba a ellas sin volver a pedir nada.

`split_single_file_course` escribe un documento por lección en `panes/`, con la cabecera
original —los estilos son del curso y no se tocan—, solo su pane y sin índice. El manifest
apunta a esos documentos en vez de a `index.html#pane-m0-l0`. Mismos tres límites que arriba:
solo la copia servida, y si el formato del original cambia, la importación **falla** en vez de
producir un curso incompleto que parece correcto.

**Tercera excepción: el tema.** Ver `normalise_theme`. El visor original trae un selector con
los nombres de otra marca; dentro del player el tema se hereda de la academia.

**Primera excepción: el banco de quizzes.** El origen trae los quizzes y los ejercicios de los
ocho bloques en un solo `assets/js/quizzes-data.js` de 73 KB, y cada bloque lo carga entero. Al
ser un fichero compartido del curso, el token de una lección gratuita lo alcanzaba: las
preguntas y los `model_answer` de los bloques de pago quedaban a la vista en la pestaña de red.
No hay forma de cerrarlo desde la API, porque el fichero es legítimamente necesario para el
bloque que sí se ha pagado.

`split_quiz_bundles` lo parte en `<documento>.data/quizzes.js` y reescribe el `<script>` de cada
documento. Tres límites que mantienen viva la decisión de arriba:

- Solo se toca **la copia servida**. El origen no se modifica, así que sigue abriéndose con
  doble clic (criterio de T-07).
- El reparto lo manda el documento: se le da lo que sus `data-quiz` y `data-exercise` piden, no
  lo que el nombre de la clave sugiera. Si pide algo que no está, o si sobra material que nadie
  pide, la importación **falla**; perderlo en silencio produce un curso incompleto que parece
  correcto.
- De paso arregla un desajuste del origen: el pre-curso pide `data-quiz="P0"` y su banco dice
  `"PRE-P0"`, así que sus cuatro mini-quiz nunca llegaron a renderizarse. Al reindexar por lo
  que pide el documento, aparecen.

---

## ADR-009 · La tutoría convocada es distinta de la tutoría dada

**Contexto.** T-17 dejó `tutoring_session`: lo que YA ocurrió, apuntado a posteriori para
descontar tiempo del saldo. Cuando en T-18 hubo que convocar a alguien —mandarle una invitación
de calendario al alumno y al profesor— esa tabla no servía: cuando la fila existe, la clase ya
pasó.

**Decisión.** Dos conceptos y dos tablas. `tutoring_appointment` es una promesa —alumno,
profesor, día, hora— y no toca el saldo. `tutoring_session` es el hecho, y es lo único que
descuenta y lo único que devenga al profesor. Confirmar una cita crea su sesión y las ata;
deshacer la sesión devuelve la cita a «convocada», que es lo que de verdad pasó.

El saldo disponible para convocar sí descuenta lo ya citado y sin dar: sin eso se podrían
convocar cinco tutorías contra una bolsa de una hora, y solo la primera podría apuntarse.

**Alternativas.** Una sola tabla con un estado y una fecha anulable: menos tablas, pero el saldo
tendría que preguntar por el estado en cada suma, y el día que alguien olvide el filtro convocar
descontaría horas. Y reservar el tiempo al convocar: una cancelación olvidada dejaría horas
muertas que el alumno pagó y no puede gastar.

**Consecuencias.**
- Una tutoría dada sin convocatoria previa sigue siendo posible y es un camino de primera: se
  apunta suelta, como antes.
- La cita guarda su `ics_uid` de por vida y un contador de secuencia. El UID no puede cambiar
  —el evento viejo se quedaría colgado en la agenda de los invitados— y la secuencia tiene que
  subir en cada cambio, o los clientes de calendario ignoran la actualización.

---

## ADR-010 · Las invitaciones de calendario van por iCalendar, no por la API de ningún calendario

**Contexto.** Las tutorías hay que convocarlas en el calendario del alumno y en el del profesor.
El alumno es de fuera y puede tener Outlook, Gmail o Apple; el profesor puede ser externo y no
tener siquiera cuenta en la academia.

**Decisión.** Un adjunto `text/calendar` (RFC 5545) en el correo de convocatoria, con
`METHOD:REQUEST` al convocar y al mover, y `METHOD:CANCEL` al anular. Nadie conecta ninguna
cuenta ni concede permisos.

**Alternativas.** Microsoft Graph y Google Calendar API escriben directamente en la agenda y
permiten leer disponibilidad. A cambio: OAuth por cada persona, tokens que caducan y hay que
renovar, y dos integraciones que mantener para cubrir a la mitad de la gente. Nada de eso
alcanza al alumno que use Apple Calendar.

**Consecuencias.**
- Funciona con todos desde el primer día, y no hay ningún token que se pueda caer justo el día
  que haya que mover una tutoría.
- No se puede consultar la disponibilidad de nadie: quien convoca elige la hora.
- El fichero se compone a mano (`CalendarInvitation`). Lo que rompe en silencio —una línea de
  más de 75 octetos, una coma sin escapar— está cubierto por tests, porque un cliente que
  descarta el evento no avisa: el alumno se queda sin cita creyendo que la tiene.
- Si algún día hace falta escribir en la agenda de la casa, Graph se añade encima; esto seguirá
  sirviendo para el alumno y para el profesor externo.

---

## ADR-011 · El registro de facturación es una tabla, y el envío a la AEAT un puerto vacío

**Contexto.** Veri*Factu (RD 1007/2023) no regula el PDF: regula el **registro de facturación**,
un asiento por cada factura emitida y por cada una anulada, encadenado con el anterior por su
huella y remitido a la AEAT. Lo que había era una columna `hash` en `fiscal_invoice`, que sirve
para imprimir pero no para declarar: faltan el tipo de factura, la marca temporal de generación
y —sobre todo— el estado del envío.

**Decisión.** `verifactu_record` como tabla propia, con lo declarado COPIADO (NIF y razón social
del emisor incluidos) y un estado de remisión con cuatro valores: pendiente, remitido, rechazado
y «no hay que remitirlo». Un asiento no se modifica nunca salvo ese estado.

El envío se queda como puerto (`IVerifactuSubmitter`) con una implementación que dice que no
está conectada. El XML del registro, la firma XAdES con el certificado cualificado y la llamada
al servicio los fija la AEAT, y **no se pueden escribir de memoria**: una factura declarada con
un formato inventado la rechaza Hacienda, y una que se dé por declarada sin haberlo sido es
peor. Detrás va la librería Veri*Factu que ya existe fuera de este repositorio.

**Alternativas.** Seguir con la columna `hash` en la factura: no cabe el estado del envío, y sin
estado no se puede reintentar lo que no salió; lo que no se puede reintentar acaba sin
declararse. Escribir el adaptador contra la especificación de memoria: produciría algo que
*parece* correcto, que es exactamente lo que la convención 6 de `CLAUDE.md` prohíbe.

**Consecuencias.**
- Todo lo que sí puede estar bien lo está y es comprobable: numeración, encadenamiento, huella,
  marca temporal, PDF con QR y verificación de la cadena entera desde el panel.
- Los cuatro estados separan «no enviado» de «no hacía falta enviarlo». Sin esa distinción, la
  lista de pendientes siempre tendría cosas y nadie la miraría.
- Un índice único sobre `(issuer_tax_id, previous_hash)` impide que dos cobros simultáneos
  encadenen desde la misma huella. Sin él la cadena se bifurca en Y y no se nota hasta ir a
  verificarla, que es demasiado tarde.
- Cada asiento guarda el **texto exacto** sobre el que calculó su huella. Es lo que permite
  explicar una huella años después, y lo que hará que confirmar el formato definitivo contra la
  norma no dé por rota toda la cadena anterior.
- El emisor sale de los datos legales de la marca y no de la configuración: es donde ya se
  editan y de donde los lee el aviso legal. Sin razón social y NIF **no se emite**, porque una
  factura a medias consume un número de serie —que no se reutiliza— para un documento inválido.
- Los tres puntos que dependían de la norma están cerrados contra las especificaciones
  oficiales, descargadas en `docs/verifactu/`: el orden y formato de los campos de la huella
  (fijado por los tres ejemplos oficiales en `VerifactuHuellaOficialTests`), la URL de
  validación del QR (`VerifactuQrTests`) y el tipo de factura, que ha dejado de ser una
  constante del código para configurarse por marca contra la enumeración del esquema.

---

## ADR-012 · La API autoriza el contenido; los bytes los sirve Caddy

**Contexto.** ADR-003 dejó a la API sirviendo cada fichero del curso con `Results.Stream`. Con
un curso de 321 lecciones, abrir una clase son varias peticiones —el documento, el CSS, el
visor, las imágenes, el fichero de datos de esa lección—, y todas atraviesan Kestrel. En una
CX22 de 2 vCPU eso convierte la concurrencia de alumnos en concurrencia de flujos de fichero
abiertos, que es el primer recurso que se agota: mucho antes que la CPU o la base de datos.

La lista de alternativas de ADR-003 descartaba servir desde Caddy porque «no hay forma de
aplicar `IAccessPolicy` por lección sin duplicar la autorización en el proxy». Eso es cierto de
*decidir* en el proxy. No lo es de *preguntar* desde el proxy.

**Decisión.** La autorización se queda entera en la API; el transporte se va a Caddy.

- `GET /api/content/authorize` valida el HMAC del token y comprueba el alcance. No toca disco
  ni base de datos: es un `HMACSHA256` y unas comparaciones de cadenas. Responde `204` con
  `X-Content-File: <ruta aprobada>`, o el mismo `403` de siempre.
- Caddy usa `forward_auth` contra esa ruta y, si dice que sí, sirve el fichero con
  `file_server` desde el volumen de contenido, montado en solo lectura.
- El fichero servido es **el que aprobó la API**, no el que pidió el navegador: la ruta viaja
  en la cabecera ya normalizada y escapada.
- La ruta que sirve bytes desde la API se mantiene. Es la que se usa en desarrollo, donde no
  hay proxy delante, y la que ejercitan los tests de integración.

**Por qué esto no duplica la autorización.** Caddy no sabe qué es un token, ni un entitlement,
ni una lección. Solo sabe reenviar una petición y mirar el código de respuesta. La regla de
alcance vive en un único sitio, `ContentTokenScope`, y los dos caminos —el que sirve y el que
autoriza— la llaman. `ContentScopeTests` recorre los dos con los mismos casos, porque un muro
de pago que dice cosas distintas según quién sirva el fichero es peor que no tenerlo.

**Alternativas.**
- Dejarlo como estaba y agrandar la máquina: pagar cada mes por mover ficheros estáticos con un
  runtime que sabe hacer cosas mucho más caras.
- CDN con URLs firmadas: quita el problema del todo, pero mete un tercero, coste de egress y
  una segunda copia del contenido que hay que invalidar. Sigue siendo el paso siguiente si el
  disco de la máquina deja de dar abasto; el modelo de token no lo estorba.
- Servir `assets/` sin autorizar, por ser presentación y no material: ahorraría una petición de
  cada tres a cambio de que el CSS y el visor del curso pasen a ser públicos. No compensa
  cambiar la superficie de lo que se regala por una optimización.

**Consecuencias.**
- La API deja de mover bytes. Sigue habiendo una petición por fichero, pero cuesta un HMAC en
  vez de un flujo abierto: la diferencia entre microsegundos y el tiempo que tarde el cliente
  en leer.
- `file_server` aporta `ETag`, `Last-Modified`, rangos y `sendfile`. Las respuestas llevan
  `Cache-Control: private, no-store`: el contenido es de pago y su URL cambia con cada token,
  así que cachearlo no ahorraría nada y sí dejaría copias donde no toca.
- **El `..` deja de tener red debajo.** Antes, una ruta que se salía del volumen pasaba el
  alcance y la paraba `LocalDiskContentStorage` al resolverla contra la raíz. Ahora lo que se
  aprueba es lo que se sirve, así que `ContentTokenScope.Normalise` rechaza por su cuenta
  cualquier segmento vacío, `.` o `..`, y la ruta se desescapa **segmento a segmento**: hacerlo
  de una vez convertiría un `%2F` en barra y crearía separadores que no venían en la URL.
- Caddy monta el volumen de contenido en solo lectura. Es el único sitio del sistema, aparte
  del importador, que ve ese árbol.
- Comprobado sobre la pila de producción levantada entera, no sobre el papel: el documento, el
  chrome del curso y los datos de la lección salen con `200` y con las cabeceras de
  `file_server`; el bloque de pago del mismo curso, otro curso y un token manipulado siguen
  dando `403` con el mismo `problem+json`; un fichero amparado que no existe da el `404` de
  Caddy; y las cabeceras de seguridad del sitio siguen puestas en todas esas respuestas.
