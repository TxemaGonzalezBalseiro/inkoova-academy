# Estado por tarea

Actualizado: 2026-09-04.

Leyenda: **Hecho** · **Parcial** (queda algo señalado) · **Pendiente**.

## Plataforma

| Tarea | Estado | Qué hay | Qué falta |
|---|---|---|---|
| **T-00** Discovery y stack | Hecho | Monorepo, `CLAUDE.md`, `DECISIONS.md` con 8 ADR, esqueleto de solución, compose local | — |
| **T-01** Dominio y esquema | Hecho | Entidades con invariantes, VOs, 5 migraciones SQL, repos Dapper, 78 tests de dominio | — |
| **T-02** API de catálogo | Hecho | Catálogo, ficha, progreso, verificación pública, `IAccessPolicy`, caché, OpenAPI, OTel, health | Medición real de p95 con caché caliente |
| **T-03** Auth | Hecho | Registro, verificación de email, login, reset, JWT 15 min + refresh rotatorio, roles | Login social Google/GitHub (opcional en la tarea) |
| **T-04** Suscripciones Stripe | Hecho | Checkout de plan y de producto, portal, webhook idempotente, periodo de gracia, emails, **gestor de planes** y **pantalla de configuración de Stripe** con sincronización de precios | Claves reales de la cuenta y prueba de pago de punta a punta |
| **T-05** Landing pública | Hecho | Las 9 secciones, `/sobre-mi`, `/faq`, `/soporte`, SEO, sitemap, robots, **repaso de diseño de todo el front** | Medir Lighthouse contra el despliegue real; biografía extendida de B0 |
| **T-06** Catálogo y ficha | Hecho | `/cursos` con filtros, ficha con temario, previews, CTA contextual, waitlist | — |
| **T-07** Player e importación | Hecho | Importador Python (16 tests), manifest, player con iframe sandbox, `postMessage`, tokens de 60 s, descargas | Añadir el fragmento de `PROTOCOL.md` al generador `content.py` |
| **T-08** Quiz y pre-curso | Hecho | Quiz corregido en servidor, umbrales originales fijados por test, intento anónimo reclamable, Fundamentos de IA Engineer con P0 de muestra y P1–P3 de pago | — |
| **T-09** Roadmap | Hecho | Endpoint con estado por alumno, página accesible, itinerario sembrado con los 6 cursos y CRUD en el panel con guardas contra ciclos, claves repetidas y borrado de nodos de los que cuelgan otros | — |
| **T-10** Certificados | Hecho | Emisión con invariante de 100 %, hash reproducible, PDF enmarcado con QR, **imagen PNG para compartir**, verificación pública con rate limit y descarga desde ella, LinkedIn, revocación | — |
| **T-11** Panel admin | Hecho | Publicar, marcas, reordenar, usuarios, accesos manuales, métricas, auditoría, importador con diff, packs, afiliados, **gestor de contenidos**, **itinerario** y **marca por curso** | Churn necesita histórico; falta listado de packs en el panel (hoy se publican por API) |
| **T-12** Comunidad y Discord | Hecho | OAuth2, roles por plan, reconciliación diaria, sesiones por plan, página de empresas | Configurar guild y roles reales |
| **T-13** Infraestructura | Hecho | Compose de desarrollo y de producción, 4 Dockerfiles, Caddy con CSP, `bootstrap.sh`, backup y prueba de restauración, CI y despliegue, **contenido servido por Caddy (ADR-012), PgBouncer y ajuste de Postgres** | Ejecutarlo sobre una VPS real; prueba de carga que mida el techo nuevo |
| **T-14** Legal y Verifactu | Parcial | Cuatro páginas legales, banner con consentimiento real, exportación y borrado de datos, **registro de facturación con la huella y el QR ajustados a las especificaciones oficiales de la AEAT y fijados contra sus ejemplos**, rectificativas, emisor tomado de los datos legales de la marca, **panel de facturación que verifica la cadena entera**, reintento diario de lo pendiente | **Adaptador Verifactu de envío a AEAT** (librería propia + certificado cualificado), `TipoFactura` por confirmar con el gestor, NIF del cliente en el checkout para B2B |
| **T-15** Lanzamiento | Parcial | Analítica tras consentimiento, SEO, `LAUNCH.md` | Beta cerrada, dashboards de embudo, prueba de pago real |
| **T-16** Descuentos y afiliados | Hecho | Atribución por cookie y por código, comisiones con ventana de 14 días, reversión por reembolso, liquidación mensual reproducible, panel de afiliado, antifraude | Stripe Connect queda en backlog, como pide la tarea |
| **T-17** Tutorías | Hecho | Catálogo de paquetes (1 h, 2 h, 5 h, 10 h) con sincronización de Stripe, caducidad atada a la suscripción, concesión y consumo desde el panel, saldo calculado, vista del alumno | — |
| **T-18** Profesores | Hecho | Gestor de profesores con porcentaje, devengo por tutoría prorrateado sobre lo pagado, liquidación con referencia, agenda de tutorías convocadas e invitaciones de calendario (iCalendar) al alumno y al profesor | Pago real al profesor: sale de la plataforma, aquí solo se apunta |

## Contenido

| Tarea | Estado | Qué hay |
|---|---|---|
| **C-00** Caso conductor | Hecho | `caso/meridiana.md`, 31 siniestros sintéticos deterministas, 5 pólizas, catálogo de coberturas, repo `meridiana-agent` que resuelve el conjunto completo con `--all --check` en verde |
| **C-01** Extensión curso 1 | **Escrito** | 2 bloques (PE-A, PE-B), 59 clases, 6 preguntas de quiz. Falta decidir si van como curso aparte o dentro del de prompt engineering |
| **C-02 → C-08** Curso 3 | **Hecho** | 7 bloques escritos, **213 clases · 9 h · 21 preguntas de quiz**, 7 labs sobre Meridiana. Publicado en el catálogo como `agentes-en-produccion`, con B1 gratuito |
| **C-09 → C-14** Curso 4 | **Publicado** | 6 bloques, **179 clases · 8 h · 18 preguntas**, como `gobernanza-eu`. Cero marcadores sin cerrar: las afirmaciones normativas que quedan están en `VERIFICADO.md` con su fuente primaria abierta, y lo que no se pudo verificar no se menciona |
| **C-15 → C-20** Packs | **Publicados** | Los 6, versión 1.0.0, **30 documentos generados** (2 Word, 2 Excel y un PDF por pack). Contenido citado artículo por artículo contra el AI Act, DORA, el MDR, el Estatuto de los Trabajadores y la Ley 40/2015, con los textos abiertos en EUR-Lex y el BOE. Se regeneran con `python content/tools/build_packs.py` |
| **C-21** Proyecto final | Hecho | Enunciado y rúbrica completa de autoevaluación sobre los cuatro cursos |

### Los marcadores de verificación, cerrados

**No queda ningún `TODO(verificar)` en el contenido que se sirve.** Se comprueba con:

```bash
grep -ro "TODO(verificar)" content/dist content/generado content/manifests | wc -l
```

Los que quedaban se cerraron de dos formas distintas, y la diferencia importa:

**Abriendo la fuente**, donde el hecho era nuestro y era verificable:

| Dónde | Qué se verificó | Fuente |
|---|---|---|
| B2, referencias a salud en trazas | Categoría especial del art. 9.1 RGPD, definición amplia del art. 4.15, y que la excepción del **art. 9.2.f** ampara el expediente pero no la traza | EUR-Lex, CELEX 32016R0679 |
| B2, nombres de atributos | Las convenciones GenAI de OpenTelemetry están en **Development** y se movieron a `semantic-conventions-genai`; nombres vigentes `gen_ai.operation.name`, `gen_ai.provider.name`, `gen_ai.request.model`, `gen_ai.usage.input_tokens` | Repositorio oficial |
| B5 y B7, notificación de incidentes | Las **72 horas** del art. 33.1 RGPD, que cuentan desde que se tiene *constancia*, y la comunicación al afectado del art. 34.1 cuando el riesgo es alto | EUR-Lex, CELEX 32016R0679 |
| `TipoFactura` | La enumeración `ClaveTipoFacturaType`: F1, F2, F3, R1..R5 | `SuministroInformacion.xsd` |

**Reescribiendo la frase**, donde el marcador estaba mal puesto: la tarifa del proveedor del
alumno, el coste hora de su compañía, su política de retención, su asesoría jurídica. Eso no era
deber nuestro pendiente sino una decisión del lector, y `TODO(verificar)` lo hacía parecer lo
primero. Ahora se dice como lo que es —«esto lo pones tú, y esta es la razón»— sin marcador.
Donde había una cifra que no podíamos dar, se explica **por qué el número no está aquí** y qué
regla sí se lleva el alumno, en vez de dejar un hueco.

Lo que sigue sin poder verificarse es `SistemaInformatico`: sus siete campos no existen todavía,
los fija la declaración responsable del software. Va vacío, sin él no se remite nada, y el panel
de Facturación lo dice. No lleva marcador porque no es algo que se verifique: es algo que se
presenta.

### Por qué el contenido está en esqueleto y no escrito

La estructura pedagógica se puede decidir de antemano y está decidida: objetivo, guion slide
a slide, labs, «qué te llevas» y temas de quiz. Lo que no se puede generar es la prosa de los
cursos 3 y 4, porque el README del plan lo prohíbe explícitamente: *toda afirmación normativa
con artículo/fuente y fecha de verificación, nunca inventar plazos*.

Escribir «según el artículo 6.2, desde el 2 de agosto de 2026…» sin haber abierto el DOUE
produciría material que **parece** verificado. En un curso de cumplimiento normativo eso no
es un borrador incompleto: es un defecto que se propaga a los expedientes técnicos de quien
lo estudie. Cada bloque normativo lleva su lista de fuentes primarias y un
`TODO(YYYY-MM-DD)` de fecha de verificación que hay que rellenar abriéndolas.

## Capacidad

Tres cuellos de botella medidos sobre el compose de producción, no estimados. Los tres estaban
en el mismo sitio: la máquina hacía a mano trabajo que ya sabe hacer otra pieza.

| Qué era | Qué se ha hecho | Comprobado |
|---|---|---|
| Cada fichero del curso salía por `Results.Stream` de Kestrel. Con 321 lecciones, abrir una clase son varias peticiones, y todas ocupaban un flujo abierto en la API | La API solo autoriza (`GET /api/content/authorize`, un HMAC y unas comparaciones) y Caddy sirve el fichero con `file_server` (ADR-012) | Pila entera levantada: documento, chrome y datos con `200` y cabeceras de `file_server`; bloque de pago, otro curso y token manipulado con `403` y el mismo `problem+json`; fichero amparado inexistente con `404` de Caddy; cabeceras de seguridad presentes |
| Npgsql traía 100 conexiones por proceso. La API y los jobs sumaban 200 contra las 100 de Postgres: el primer pico se saldaba con «too many connections» en vez de con una cola | Pools acotados a 40 (API) y 8 (jobs), y PgBouncer en modo transacción delante. El migrador va directo a Postgres: su DDL no se lleva bien con el pooling por transacción | `pg_stat_activity` no ve ni una conexión de la API: los cinco backends vienen de PgBouncer. `SHOW POOLS` confirma `pool_mode transaction` |
| Postgres con los valores de arranque de la imagen: `shared_buffers` de 128 MB y planificador de disco giratorio | Ajuste para una CX22 compartida: 384 MB de buffers, `effective_cache_size` de 1200 MB, `random_page_cost` a 1.1, WAL más espaciado, `jit` apagado y `pg_stat_statements` cargado | `pg_settings` devuelve los once valores puestos; `pg_stat_statements` ya registra consultas |

Lo que **no** cambia: sigue habiendo una sola máquina, una sola instancia de API y una caché en
proceso (ADR-005). Estas tres cosas suben el techo dentro de la arquitectura actual; pasar de
ahí es otra decisión —Redis para caché y limitador compartidos, y N réplicas detrás de Caddy—
y hasta que no haya una prueba de carga real el techo nuevo es una expectativa, no un dato.

## Lo que hay que decidir o aportar antes de vender

1. **Datos identificativos de la empresa** (razón social, NIF, domicilio, datos registrales).
   Ya se editan en *Marcas → Datos legales*, y de ahí salen el aviso legal, el pie de la web y
   **el emisor de las facturas**. Mientras falten razón social o NIF **no se emite ninguna
   factura**: el panel de *Facturación* lo dice en grande, porque emitir con el emisor a medias
   consume un número de serie —que no se reutiliza— para un documento fiscal inválido.
2. **Certificado de la AEAT.** El adaptador de envío está escrito: `VerifactuXmlBuilder` compone
   el registro —validado **contra los XSD oficiales** en `VerifactuXmlSchemaTests`— y
   `AeatVerifactuSubmitter` lo remite por SOAP con certificado de cliente. No hace falta XAdES:
   en modo Veri\*Factu la firma de cada registro no es exigible, solo la autenticación de la
   conexión (documento de firma v0.1.5, apartado 2). Lo que falta es el **sello electrónico de
   entidad** en `.pfx` y apuntarlo con `Academy:Verifactu:Certificate:Path`. Sin él sigue puesto
   el emisor apagado y los asientos nacen como «no hay que remitir».
3. **`SistemaInformatico`.** Siete campos que describen el programa y que salen de la
   **declaración responsable** del software ante la AEAT: no se pueden verificar contra nada
   porque todavía no existen. Van vacíos y sin ellos no se remite; el panel de *Facturación* lo
   dice. La clave de tipo de factura ya **no** está aquí: se configura por marca en *Marcas →
   Datos legales*, validada contra la enumeración del esquema oficial.
4. **Precios definitivos.** Los iniciales son los propuestos en T-04 (19 / 49 / 79 / 129 / 249),
   y ya se editan desde *Planes y cobro* sin tocar código ni desplegar.
5. **Cuenta de Stripe** en modo real y prueba de pago y reembolso de punta a punta. La pantalla
   de configuración dice qué falta y el botón de sincronizar crea los precios.
6. **Buzón de correo saliente.** Sin él la API escribe los mensajes en el log y nadie recibe la
   confirmación de cuenta ni el enlace de recuperación. La pantalla *Correo* lo dice, explica
   qué poner según el proveedor y manda un correo de prueba.
7. **Guild de Discord** con los roles `monthly`, `quarterly`, `biannual`, `yearly`, `lifetime`.

## Verificación ejecutada

| Comprobación | Resultado |
|---|---|
| `dotnet build api/Inkoova.Academy.slnx` | Correcto, sin avisos (warnings como errores) |
| `dotnet test tests/Inkoova.Academy.Domain.Tests` | 165 de 165 correctos |
| Tests de integración (Testcontainers) | **70 de 70 correctos** |
| Pila de producción levantada entera (Caddy + API + PgBouncer + Postgres) | Contenido, alcance del token, ajuste de Postgres y enrutado por el pooler, comprobados |
| Huella y QR contra los ejemplos oficiales de la AEAT | **10 de 10 correctos** |
| `npm run typecheck` / `lint` / `build` en `web` | Correcto, cero avisos |
| Playwright | Escritos; **no ejecutados** |
| `python content/tools/test_importer.py` | 41 de 41 correctos |
| `meridiana-agent --all --check` | 31 de 31 decisiones de derivación coinciden |
| Generador de datos del caso | Determinista: mismo hash en dos ejecuciones |

### Recorrido completo ejecutado con `.\dev.ps1 up`

| Comprobación | Resultado |
|---|---|
| Arranque | Postgres, migraciones, API y seed de planes en verde |
| Importación de los 3 cursos | agent-engineering-v3 (8 bloques · **321 clases** · 14 h), pre-curso (4 · 29 · 2 h), prompt-engineering (8 módulos · 54 · 17 h) |
| Ficheros de contenido | 0 faltantes en los tres manifests |
| Publicación y catálogo público | Los 3 cursos visibles sin autenticar |
| **Contenido de pago no se filtra** | 296 de 321 lecciones sin `contentRef` para un anónimo |
| Player y token de contenido | Navegación slide a slide con ancla, incluido el salto entre bloques; token inválido → 403; lección de pago sin sesión → 403 |
| **Contenido importado tal y como se ve** | CSS (134 reglas) y los cuatro scripts del curso cargados dentro del iframe; `CursoViewer`, `QUIZZES` y `Progreso` definidos; consola sin errores en las dos formas de curso |
| Alcance de un token gratuito | Su documento 200, chrome del curso 200, sus datos 200; **datos de B3 y B7 403**, documento de pago 403, otro curso 403 |
| **Los quizzes de pago ya no se filtran** | Cada lección carga solo su bloque: en B0, `QUIZZES` tiene 1 clave y `EXERCISES` 2, no las 24 de antes |
| Mini-quiz del pre-curso | Renderiza sus 3 preguntas **por primera vez**: el origen pedía `P0` y su banco decía `PRE-P0` |
| Endpoints de admin | 401 sin sesión; login de admin devuelve roles `student,admin` |
| **Umbrales del test de nivel (T-08)** | 12 preguntas en 4 dimensiones; los 3 veredictos correctos en 9 puntos de corte |
| Administrador global | `txemabalseiro@hotmail.com` con roles `student,admin`, repuestos en cada arranque |
| Gestor de contenidos | Curso, sección, clase y quiz creados, editados y borrados de punta a punta; las seis acciones en la auditoría |
| Stripe | Endpoints en pie; con clave de marcador el checkout responde 409 `plan.no_stripe_price` en vez de fallar de forma opaca |
| **Certificado, de punta a punta** | Curso completado al 100 % → emitido `INK-WMSR-3YJG` → verificación pública sin sesión `isValid: true` → PDF 58 KB → PNG 1754×1239. Código inventado: `isValid: false` sin filtrar nada |
| Gestor de planes | Precio de Starter cambiado a 24 € y visible en `/api/plans` al instante, avisando de que hay que sincronizar con Stripe; restaurado a 19 € |
| Correo | Sin buzón configurado, la prueba de envío responde **409** en vez de decir «enviado»: el sumidero de log no debe pasar por un envío correcto |

### El repaso de diseño

Se hizo casi todo en el sistema, no página a página: así lo que se arregla una vez queda
arreglado en todas, y no aparecen nueve variantes de la misma tarjeta.

| Dónde | Qué cambia |
|---|---|
| Tokens | Anillo de foco de marca, retícula de fondo, curva de animación única, y sombras propias para el modo oscuro, donde las claras no se ven |
| Tarjetas | Elevación y borde encendido al pasar por encima o al recibir el foco, en vez de moverse: una rejilla entera de tarjetas saltando marea |
| Chips | Nivel, duración y número de clases con icono. El nivel estaba en los datos y no se enseñaba en ninguna parte |
| Cabeceras de sección | Filete de marca a la izquierda; cada sección tiene un principio visible sin subir el tamaño del titular |
| Iconos | Juego propio en SVG inline. Los emoji del temario los dibujaba cada sistema a su manera y en 321 filas la columna dejaba de alinearse |
| Portadas y acceso | Misma retícula y mismos degradados en portada, ficha de curso y entrar/registrarse |
| Precios | Los cinco planes en una fila en escritorio: partidos en 4 + 1, el último parecía de otra categoría. Botones alineados abajo para comparar la misma fila |
| Proceso | Lista numerada con hilo entre pasos, en vez de los dígitos sueltos del `<ol>` |
| Cuenta | Identidad con avatar y email en monoespaciada; las tres secciones pasan de botones sueltos a grupo segmentado |
| FAQ y legal | Cada pregunta en su tarjeta; con ocho seguidas no se veía dónde acababa una respuesta |
| Cifras | `tabular-nums` en precios, duraciones y estadísticas, que si no bailan al cambiar |

Tres defectos de maquetación que salieron al mirarlo a 375 px y con el ratón encima:

- **El consentimiento del registro se rompía en cuatro líneas** con un punto suelto al final. La
  etiqueta es un contenedor flex y cada trozo de texto era un elemento flex por su cuenta.
- **La barra del player no cabía en móvil.** El nombre del curso se apilaba palabra a palabra y
  empujaba los controles fuera. Ahora se recorta con puntos suspensivos, y por debajo de 560 px
  el título sale de la barra: la clase ya lo lleva escrito arriba.
- **Los planes se repartían en 3 + 2 con anchos distintos**, así que los dos de la segunda fila
  parecían otra cosa.

### Granularidad del contenido

La lección es la **slide**, no el fichero del bloque. Cada bloque de `curso-autodidacta` es un
HTML con `<section id="slide-N" data-slide-idx="N">` por slide, con su título, su duración
declarada y sus marcadores `data-checkpoint`, `data-exercise-slide` y `data-quiz-slide`. El
HTML no se trocea —su CSS y su JS son inline— sino que cada lección lo referencia con un ancla
que el player aplica al iframe.

| | Lecciones | Obligatorias | Notas |
|---|---:|---:|---|
| agent-engineering-v3 | 321 | 293 | 297 slides, 16 ejercicios, 8 quizzes; 28 checkpoints opcionales |
| pre-curso | 29 | 29 | Todas gratuitas (T-08) |
| prompt-engineering | 54 | 54 | Un pane por lección, agrupados por módulo |

Los checkpoints no son obligatorios a propósito: son una pausa para pensar, no producen
evidencia y no deben bloquear la emisión del certificado.

### El muro de pago decía dos cosas distintas

Reportado desde la aplicación: «puedo acceder a todo el curso desde el temario; desde el
continuar dice que no entra en mi acceso». Eran dos defectos que se veían como uno.

1. **«Continuar» llevaba a una clase bloqueada.** El siguiente era la primera clase sin
   completar del curso **entero**, sin mirar el acceso: quien terminaba el bloque gratuito
   recibía como siguiente la primera de pago. El temario, que sí filtra, le enseñaba lo suyo
   abierto. Ahora el siguiente se busca solo entre las que puede abrir, y cuando se acaban la
   ficha dice «has visto todas las clases de muestra» en vez de «has completado el curso»,
   que era falso.
2. **«Mis cursos» salía vacío mientras estudiabas.** Solo listaba los cursos cuyo producto
   tienes comprado, así que quien estaba haciendo el pre-curso gratuito veía «todavía no
   tienes acceso a ningún curso» con la mitad hecha. Ahora salen también los empezados, con
   una etiqueta que distingue muestra de curso completo.

La API nunca dejó pasar nada: una clase de pago sin plan sigue devolviendo 403. Lo que fallaba
era que dos pantallas contaban historias distintas sobre lo mismo.

### Seis defectos que solo aparecieron al usarlo

Todos con la aplicación delante, no en los tests.

1. **El botón "Temario" no hacía nada en escritorio.** El panel estaba fijo por CSS y el botón
   solo tenía efecto por debajo de 860 px. Ahora se pliega a cualquier ancho. Cerrarlo tampoco
   funcionaba a la primera: un elemento flex tiene `min-width: auto` y se negaba a bajar del
   ancho de su contenido, así que solo se estrechaba.
2. **El player enseñaba el `problem+json` en crudo.** Los errores de `/api/content` se ven
   dentro del iframe, no en una consola. Ahora esa ruta responde con una página que explica qué
   ha pasado y avisa al player por `postMessage`: si solo ha caducado el token, lo renueva sin
   que el alumno vea nada; si no, el player pinta su propio aviso.
3. **El player era un callejón sin salida.** Ocupa la pantalla entera y no había forma de volver
   a la plataforma ni de llegar a la cuenta. Ahora la barra lleva marca y acceso a la cuenta.
4. **`Mi cuenta` y `Salir` eran dos botones iguales.** Ahora hay iconos, y la jerarquía
   distingue lo que se usa a diario de lo que se pulsa por error: Admin en magenta porque no
   todos lo ven, cuenta con contorno, salir discreto y de solo icono.
5. **Un parámetro que faltaba salía como 500.** `GET /admin/users` declaraba `q` como no
   anulable; sin `?q=` reventaba en el binding, y el manejador de excepciones se tragaba el 400
   del framework y contestaba 500. Se arregla el parámetro y el manejador deja pasar los 400.
6. **El iframe se recargaba cada 50 segundos.** El token de contenido se renovaba por reloj y
   eso cambiaba el `src`, tirando por el camino lo que el alumno llevara respondido de un quiz.
   Ya no se renueva por reloj: cuando caduca de verdad, la página de aviso pide uno nuevo.

### Cuatro defectos que solo aparecieron al mirar la consola del navegador

Ninguno rompía una petición. Los tests miraban códigos de estado y todos daban 200, así que
ninguno podía verlos: el fallo estaba en cabeceras y en la forma de las URL.

1. **`X-Frame-Options: DENY` en toda la API.** Se aplicaba también a `/api/content`, que es lo
   único que el player mete en un iframe. El navegador rechazaba el documento y el player salía
   en blanco en **todas** las lecciones. Ahora esa ruta va con `SAMEORIGIN` y el resto sigue con
   `DENY`.
2. **El contenido se servía sin profundidad de ruta.** Un bloque pide `../assets/css/curso.css`;
   desde `/api/content/{token}` ese `..` se sale del token y daba 404. Las lecciones se veían
   sin estilos y sin JS. Ahora la URL canónica lleva la ruta real del fichero y la corta
   redirige a ella.
3. **El pre-curso se copiaba fuera de su curso padre.** Su HTML referencia `../assets`, que en
   el origen es la carpeta del curso principal; sacarlo a una carpeta propia dejaba ese `..`
   apuntando a la nada. Conserva su posición, `agent-engineering-v3/pre-curso/`, y solo el
   catálogo lo trata como producto aparte. De paso dejaba de publicar los `.py` del origen, que
   son las herramientas que generaron el HTML, no contenido del curso.
4. **`down` no paraba nada en Windows PowerShell 5.1.** En 5.1 `ConvertFrom-Json` no enumera el
   array: lo devuelve como un objeto, así que cada arranque anidaba el registro de PIDs un nivel
   más y acababa guardando `{"value":[...],"Count":n}`. `down` recorría ese envoltorio, no
   encontraba `.pid` y dejaba la API y Vite vivos bloqueando los binarios y el puerto. Además
   daba por parado lo que `taskkill` no había podido matar; ahora lo comprueba y lo dice.

### Cinco defectos que solo aparecieron al ejecutarlo

1. **Dapper y los records posicionales.** `MatchNamesWithUnderscores` solo se aplica al mapeo
   por propiedades, no al de constructores, así que ninguna consulta podía materializar sus
   filas (`product_id` no casa con `ProductId`). Afectaba a **todos** los repositorios.
   Resuelto con `SnakeCaseTypeMap`, en un solo sitio y sin tocar el SQL.
2. **El test de nivel perdía la mitad de las preguntas.** El dominio exigía "exactamente una
   opción correcta", pero el original marca varias como válidas cuando de verdad lo son.
   Se descartaban 6 de 12 **en silencio** y el veredicto salía de un examen que ya no era el
   original. Ahora se exige "al menos una", y el importador **aborta** en vez de saltarse una
   pregunta: un test a medias es peor que no importarlo.
3. **Postgres en el puerto 5432.** Si el bind falla, Docker arranca el contenedor igual pero
   sin publicar el puerto, y `pg_isready` dentro del contenedor sigue dando verde mientras la
   API se conecta a otra base de datos. Desarrollo usa ahora el 5433, y el arranque comprueba
   el puerto **desde el host** además de la salud interna.

4. **`WebApplicationFactory` no podía configurar la API.** Con top-level statements,
   `Program.cs` llama a `AddAcademyInfrastructure` mientras construye el builder, antes de
   que `ConfigureAppConfiguration` pueda inyectar nada: los 11 tests que usan la factory
   arrancaban con "Falta ConnectionStrings:Academy". La configuración va ahora por variables
   de entorno, que es el único canal que `CreateBuilder` lee a tiempo.
5. **Colisión de slugs en un test.** Usaba los *primeros* 8 caracteres de un GUID v7 como
   sufijo único, y v7 empieza por marca de tiempo: dos tests en el mismo instante generaban
   el mismo slug. Ahora usa la cola, que es la parte aleatoria.

Los dos primeros son exactamente lo que cazaron los tests de integración en cuanto pudieron
ejecutarse.
