# C-05 · B4 · Costes y rendimiento

> Curso: `agentes-en-produccion` · bloque `B4`

## Objetivo

Saber lo que cuesta el sistema por caso y por mes, y qué pasa el día que llegan seiscientos siniestros en veinticuatro horas.

## Guion de slides

28 slides de contenido. Una idea por slide, con un ejemplo real o del caso Meridiana. Nada de relleno.

### 1. El coste por caso: cómo se calcula de verdad

Todo este bloque usa **precios de ejemplo**. No son los de ningún proveedor: son números redondos elegidos para que las cuentas se sigan a mano. Pon los tuyos y el razonamiento no cambia.

```
PRECIO_ENTRADA = 3 €  por millón de tokens
PRECIO_SALIDA  = 15 € por millón de tokens
```

El error de casi todo el mundo es calcular el coste de **una llamada** y llamarlo coste por caso. Un FNOL de Meridiana no es una llamada: son tres, más lo que se rompe por el camino.

```
coste_caso = (Σ tokens_entrada × PRECIO_ENTRADA
            + Σ tokens_salida  × PRECIO_SALIDA)
            × (1 + tasa_reintento)
            ÷ tasa_de_casos_que_terminan_bien
```

Las tres llamadas de un FNOL típico suman **25.500 tokens de entrada y 1.600 de salida**. Con los precios de arriba: 0,0765 € de entrada más 0,024 € de salida, **0,10 € brutos por caso**.

Aplica el resto de la fórmula: un 8 % de las llamadas se reintenta y un 4 % de los casos se abandona y se relanza. `0,10 × 1,08 ÷ 0,96 = 0,11 €`.

Ese **0,11 €** es el número que vas a ver el resto del bloque. Multiplica por los 88 siniestros diarios y tienes 9,68 € al día; por los 32.000 anuales, 3.520 € al año.

Guarda el numerador y el denominador por separado. El día que el coste por caso suba un 30 %, necesitas saber si es porque el prompt engordó o porque el sistema falla más.

### 2. Tokens de entrada y de salida: la asimetría de precio

La salida cuesta cinco veces más que la entrada por token, con los precios de ejemplo. La intuición que salta sola —«hay que acortar las respuestas»— es casi siempre la optimización equivocada.

Mira el reparto real del FNOL de Meridiana:

| | Tokens | % de tokens | % de la factura |
|---|---|---|---|
| Entrada | 25.500 | 94 % | 76 % |
| Salida | 1.600 | 6 % | 24 % |

La entrada es cara **porque hay muchísima**, no porque cada token lo sea. Tres cuartas partes de la factura son instrucciones, schemas de tools, catálogo de documentos y resultados de consultas que tú metes en el contexto.

Eso reordena la lista de qué tocar primero:

- **Entrada.** Se ataca con caching (slide 5), con contexto más corto (slide 24) y quitando lo que no se usa. Es donde está el 76 %.
- **Salida.** Se ataca pidiendo el JSON justo, sin campos decorativos y sin razonamiento libre que nadie lee. Es donde está el 24 %.

Hay una excepción, y en Meridiana existe: la propuesta de resolución con su motivación escrita. Ahí la salida se acerca a los 1.500 tokens y el reparto se invierte. Por eso el perfil se mide **por tipo de tarea**, no una vez para todo el sistema.

### 3. El coste escondido: los reintentos

Un reintento no cuesta «un poco más». Cuesta **la llamada entera otra vez**, incluida toda la entrada, aunque el fallo llegue en el token 3 de la salida.

Piensa en la tercera llamada del FNOL: 11.000 tokens de entrada, 700 de salida. Si el proveedor devuelve un 529 a mitad de la generación, has pagado 11.000 tokens de entrada por nada y vuelves a pagarlos.

Las cuatro fuentes de reintento en Meridiana, por orden de frecuencia:

- **Errores transitorios del proveedor** (429, 529, cortes de red). Inevitables. Se reintentan.
- **Salida que no valida contra el schema.** Un campo obligatorio ausente, un enum inventado. Se reintenta con el error como pista.
- **Timeout del orquestador.** El peor de todos: puede que la llamada haya terminado bien del otro lado y la estés pagando dos veces sin saberlo.
- **Fallo de una tool que obliga a rehacer la vuelta.**

Con un 8 % de reintentos, el sobrecoste es del 8 %. Con un 40 % —que es lo que pasa cuando el schema es demasiado estricto y el modelo no lo cumple— el sobrecoste es del 40 % y nadie lo mira, porque el sistema *funciona*.

Por eso la tasa de reintento va en el panel al lado del coste, no escondida en los logs. Un reintento es un fallo que has decidido pagar; si sube, alguien tiene que enterarse.

### 4. El coste escondido: el contexto que se arrastra entre iteraciones

Si en cada vuelta del bucle mandas todo lo anterior más lo nuevo, el coste no crece con el número de vueltas: crece con **el cuadrado** del número de vueltas.

La aritmética, con los números del FNOL. Base de 6.000 tokens y 2.500 nuevos por vuelta:

- 3 vueltas: 6.000+8.500+11.000 = **25.500 tokens**. 0,077 € de entrada.
- 10 vueltas: 60.000 + 2.500 × (0+1+…+9) = **172.500 tokens**. 0,52 € de entrada.

Triplicar las vueltas multiplica la entrada por siete. Y las vueltas se triplican solas: basta con que una tool devuelva un error que el modelo no sepa resolver y lo intente de otra forma tres veces.

Lo que dispara esto en Meridiana son los **resultados de tools sin resumir**. `consultar_poliza` devuelve 4 KB de JSON; la vuelta siguiente lo lleva entero, y la siguiente también. De esos 4 KB, el bucle usa tres campos.

Dos defensas, las dos baratas:

1. **Resumir el resultado de la tool antes de meterlo en el contexto.** No con el modelo: con código, quedándote con los campos que usan tus reglas.
2. **Un tope de vueltas.** Un bucle que llega a diez vueltas no está resolviendo el caso, está dando tumbos. Se corta y se deriva (slide 14).

El crecimiento cuadrático es la razón número uno de facturas que se disparan sin que nadie haya cambiado un precio.

### 5. Prompt caching: mecánica y ahorro real

El proveedor puede guardar el resultado de procesar un **prefijo** de tu prompt y reutilizarlo en la llamada siguiente. Lo que compras es que esos tokens de entrada dejen de pagarse a precio pleno.

Con precios de ejemplo, la mecánica típica tiene tres tarifas: escribir en la caché sale algo **más caro** que la entrada normal (pongamos 1,25×), leer de ella sale **mucho más barato** (pongamos 0,1×), y hay un tiempo de vida corto tras el cual el prefijo se pierde. Los multiplicadores y el TTL reales están en el tarifario de tu proveedor, y cambian incluso entre modelos del mismo proveedor. Los de aquí son un ejemplo para que se vea la mecánica, no cifras que puedas usar.

En Meridiana el prefijo estable son unos **4.000 tokens**: instrucciones del sistema, schemas de las cinco tools y catálogo de documentos. Se repiten en las tres llamadas del caso.

Dentro de un solo caso, escribiendo la caché una vez y leyéndola dos:

```
entrada equivalente = 13.500 (sin cachear)
                    +  5.000 (4.000 escritos × 1,25)
                    +    800 (8.000 leídos × 0,1)
                    = 19.300  frente a 25.500
```

**Un 24 % menos de entrada, un 18 % de la factura total.** Está bien, no es espectacular.

El salto de verdad viene si el prefijo sobrevive **entre casos**: entonces la escritura se amortiza sobre muchos y la entrada equivalente baja a 14.700 tokens, un **32 %** de ahorro. De ahí la slide siguiente.

### 6. Diseñar el prompt para que el caching sirva

El caching solo funciona sobre un prefijo **idéntico byte a byte**. Un solo carácter distinto al principio y no hay acierto: pagas la escritura y no lees nunca.

De ahí la única regla de diseño que importa: **ordena el prompt de lo más estable a lo más volátil**.

```
1. Instrucciones del sistema        estable durante semanas
2. Schemas de las 5 tools           estable hasta el próximo despliegue
3. Catálogo de documentos por vía   estable, cambia dos veces al año
------------------------------- frontera de caché
4. Datos de la póliza               por caso
5. Relato del asegurado             por caso
6. Resultados de tools              por vuelta
```

Los tres asesinos silenciosos del acierto de caché, y los tres los he visto en producción:

- **La fecha de hoy en la primera línea.** Invalida la caché cada día, o cada segundo si lleva hora.
- **El `claim_id` en la cabecera del prompt**, «para trazar». Invalida la caché en cada caso. La traza va en los metadatos de la llamada, no en el texto.
- **Ejemplos elegidos dinámicamente** y colocados antes de las instrucciones.

Y un cálculo específico de Meridiana. A 88 siniestros al día llega uno cada 16 minutos de media, así que un TTL corto se agota entre casos y el acierto entre casos es bajo. **En el episodio de granizo pasa lo contrario**: con 600 en 24 horas y ráfagas de 80 en diez minutos, la caché está siempre caliente. El caching abarata más justo el día que más volumen hay. Eso es una propiedad afortunada, no un plan de capacidad.

### 7. Elegir modelo por tarea: cuándo el pequeño basta

«Qué modelo usamos» está mal planteado. La pregunta es qué modelo usa **cada tarea**, porque el sistema hace cosas de dificultad muy distinta.

Las tareas de Meridiana, ordenadas por lo que exigen:

- **Decidir la vía y la derivación por lesiones.** Ningún modelo. Es código, desde B1. Coste cero y reproducible.
- **Extraer campos del relato del asegurado.** Entrada corta, salida con schema estrecho, criterio de acierto binario. Es el caso de libro para el modelo pequeño.
- **Redactar la petición de documentación.** Texto de plantilla con huecos. Pequeño, o directamente plantilla determinista.
- **Motivar una propuesta de resolución de 6.000 €.** Razonamiento sobre coberturas, franquicia y hechos contradictorios, y el texto lo leerá un tramitador que aprueba dinero. Aquí el modelo grande se paga solo.

El criterio para bajar de modelo no es la intuición ni una tabla comparativa de un blog: es **tu conjunto de evals de B3**. Pasas los 31 casos sintéticos con el pequeño y comparas contra la línea base del grande, métrica a métrica.

Y hay un umbral que no se negocia: en los casos con lesiones, la extracción tiene que marcar `hay_lesiones_incierto` con la misma sensibilidad que el grande. Si el pequeño ahorra un 90 % y falla uno de esos, no ahorra nada.

### 8. Cascada de modelos: barato primero, caro si hace falta

La cascada es simple: resuelve con el modelo barato, comprueba el resultado con **código determinista**, y solo si la comprobación falla repite con el caro.

La pieza que decide si funciona no es el modelo: es el verificador. En la extracción de FNOL de Meridiana el verificador ya existe, porque son las validaciones de B1.

```python
extraccion = await llm_pequeno.extraer_fnol(relato)

if (extraccion.confianza < UMBRAL
        or extraccion.hay_lesiones_incierto
        or extraccion.campos_criticos_ausentes()
        or not extraccion.fecha_dentro_de_vigencia(poliza)):
    extraccion = await llm_grande.extraer_fnol(relato)   # escalada
    metricas.escalada("fnol", motivo=extraccion.motivo)
```

Con precios de ejemplo y un modelo pequeño a **una décima parte**: el grande cuesta 0,10 € por caso, el pequeño 0,010 €.

Si escala el 25 % de los casos, el coste esperado es `0,010 + 0,25 × 0,10 = 0,035 €`. Un **65 % menos** que ir siempre con el grande.

Tres condiciones para que la cascada sea honesta, y las tres son obligatorias:

- El verificador es **determinista y barato**. Si para verificar llamas a otro modelo, has añadido coste en vez de quitarlo.
- La escalada se **cuenta y se alerta**. Es la métrica que mantiene viva la cascada.
- El caso escalado **paga las dos latencias**. Si el 25 % de tus FNOL tarda el doble, eso se decide con conocimiento, no por sorpresa.

### 9. El coste de la cascada cuando el barato falla mucho

La cascada tiene un punto de indiferencia y conviene saber calcularlo antes de construirla.

```
coste_cascada = C_pequeno + e × C_grande        (e = tasa de escalada)
```

Iguala a `C_grande` y despeja:

```
e* = 1 − C_pequeno / C_grande
```

Con el pequeño a una décima del grande, `e* = 0,90`. En dinero puro, la cascada solo pierde si escala **más del 90 %** de los casos. Suena a margen enorme, y es la trampa.

El umbral real está mucho más abajo, porque el dinero no es lo único que pagas:

- **Latencia.** Cada caso escalado suma la latencia del pequeño a la del grande. Con un 40 % de escalada, el percentil 90 de tu FNOL es el del grande *más* el del pequeño.
- **Dos suites de evals.** Dos modelos que evaluar, dos líneas base que mantener, dos deprecaciones de las que enterarte.
- **Una rama más de comportamiento.** Cuando un tramitador pregunte por qué este caso salió distinto, la respuesta incluye «lo resolvió el otro modelo».

Mi criterio práctico: por debajo de un **20 % de escalada** la cascada compensa con claridad; entre el 20 % y el 40 % hay que mirar la latencia; **por encima del 50 % se quita**, aunque los euros digan que sigue ganando. Un sistema con dos modelos y ahorro del 15 % es un mal negocio.

Y vigila la deriva: la tasa de escalada sube sola cuando cambia la mezcla de casos. En el granizo, todos los relatos se parecen y la escalada baja; en enero, con más siniestros complejos, sube.

### 10. Batching: qué se puede agrupar en Meridiana y qué no

Muchos proveedores ofrecen un modo por lotes: mandas N peticiones, aceptas que tarden horas, y pagas menos por token. El descuento y la ventana de entrega los publica cada proveedor y no son comparables entre sí. La ventana es el dato que hay que mirar primero: si es de horas, el modo por lotes queda descartado para cualquier cosa que un tramitador esté esperando en pantalla, por barato que salga.

El criterio para usarlo es de producto, no técnico: **¿hay alguien esperando la respuesta?**

Lo que en Meridiana **sí** se agrupa:

- **La reevaluación nocturna de expedientes abiertos.** Miles de casos, nadie mirando, tolera doce horas.
- **La reclasificación tras cambiar un prompt.** Se relanzan los expedientes en curso para ver si alguna decisión cambia. Es trabajo de fondo.
- **La regeneración del conjunto de evals** cuando crece el corpus de casos.

Lo que **no** se agrupa nunca:

- **El FNOL del portal.** El asegurado está delante de la pantalla y ocho segundos ya es lo que aguanta.
- **El FNOL telefónico transcrito**, por la misma razón con un operador delante.
- **El pico de granizo.** Tentador —600 casos de golpe parecen un lote— y equivocado: son 600 personas con el coche abollado esperando respuesta, y el objetivo del proyecto es bajar de 11 días a 4. Un lote de 24 horas se come una cuarta parte del presupuesto de tiempo.

El pico se resuelve con colas propias y contrapresión (slide 17), no comprando descuento a cambio de latencia.

### 11. Latencia percibida frente a latencia real

Un FNOL tarda ocho segundos de reloj. Esa es la latencia real y es la que sale en tus métricas. La percibida es otra cosa, y es la que decide si el asegurado abandona el formulario.

Tres tiempos distintos, y cada uno tiene su remedio:

- **Tiempo hasta el primer feedback.** Si la pantalla se queda en blanco ocho segundos, se perciben como veinte. Un acuse inmediato —«hemos recibido tu aviso, expediente SIN-2026-0007»— convierte una espera en una confirmación, y no requiere que el modelo haya terminado.
- **Tiempo hasta la respuesta completa.** Los ocho segundos.
- **Tiempo hasta que el caso está resuelto.** En Meridiana, **once días**, con objetivo de cuatro.

Compara el segundo con el tercero. Bajar el FNOL de ocho segundos a cinco mejora el tiempo total de tramitación en un 0,0003 %. **Es ruido.** El proyecto no se gana ahí y optimizar esos tres segundos es tiempo de ingeniería tirado.

Dónde sí importa la latencia real en este caso:

- **En el portal**, porque hay abandono medible antes de que el expediente exista.
- **En la herramienta del tramitador**, que revisa decenas de casos al día. Ahí dos segundos de más se multiplican por 24 personas y por todo el año.
- **Como techo de rendimiento en el pico**, porque latencia alta con concurrencia limitada significa cola.

La latencia es un problema de capacidad y de abandono. Casi nunca es un problema de plazo.

### 12. Streaming: cuándo mejora la experiencia y cuándo solo la complica

El streaming reduce el tiempo hasta el primer token, no el coste ni el tiempo total. Pagas exactamente lo mismo.

Sirve cuando se cumplen las dos condiciones a la vez: **hay un humano leyendo** y **el texto es útil parcialmente**.

- **Sirve:** la motivación de una propuesta de resolución que lee el tramitador antes de aprobar. Empieza a leer al segundo en vez de al octavo.
- **No sirve:** la extracción del FNOL. Es un JSON que hay que validar entero contra el schema antes de tocarlo. Un JSON a medias no es medio JSON: es basura.
- **No sirve:** cualquier llamada intermedia del bucle cuyo resultado consume otra llamada. Nadie lo lee.

Lo que complica, y hay que ponerlo en la balanza:

- **Errores a mitad de flujo.** Ya has pintado tres párrafos cuando llega el fallo. ¿Los borras? ¿Reintentas y repites texto?
- **Contabilidad de tokens.** El recuento llega al final del flujo. Si el flujo se corta, tienes una llamada pagada sin métrica; hay que estimarla o perderás coste en las cuentas de la slide 21.
- **Cancelación.** El usuario cierra la pestaña. Si no propagas la cancelación, sigues generando y pagando.

En Meridiana: streaming en la revisión del tramitador, nunca en el FNOL del portal, donde lo que hace falta es el acuse inmediato de la slide anterior.

### 13. Paralelizar tools independientes

En el FNOL de Meridiana el bucle hace, en fila india: extraer, consultar póliza, consultar coberturas, consultar recibos, consultar siniestros previos, decidir. Cada consulta tarda entre 200 y 600 ms y la cadena entera suma casi dos segundos que no hacen falta.

Dos formas de recortarlos, con riesgos distintos:

**Paralelizar entre sí las tools independientes.** Póliza, recibos y siniestros previos solo necesitan el identificador de póliza; se lanzan a la vez y esperas a las tres. Coberturas depende de póliza, así que va después. Pasas de la suma al máximo.

**Adelantar trabajo mientras el modelo piensa.** El identificador de póliza llega en el formulario del portal, no lo extrae el modelo. Así que la consulta de póliza puede lanzarse **en paralelo con la llamada de extracción** y estar lista cuando el modelo termine. Es el segundo más barato de todo el sistema.

```python
async with asyncio.TaskGroup() as tg:
    extraccion = tg.create_task(llm.extraer_fnol(relato))
    poliza     = tg.create_task(tools.consultar_poliza(poliza_id))
    recibos    = tg.create_task(tools.consultar_recibos(poliza_id))
```

Dos límites que respetar:

- **Solo lecturas.** Las escrituras se paralelizan solo si son idempotentes, y ya vimos en B1 que `crear_peticion_documentacion` no lo es.
- **La concurrencia se acota.** Un `TaskGroup` sin techo, multiplicado por 80 casos en diez minutos durante el granizo, tumba la base de datos antes que el modelo.

Paralelizar no ahorra un céntimo. Compra capacidad en el pico, que es otra cosa.

### 14. Presupuesto por caso: cortar antes de gastar de más

Sin un tope, un bucle que se atasca gasta hasta que alguien lo mire. En un sistema que corre de noche, eso son ocho horas.

Un presupuesto por caso son tres topes y una salida:

```python
@dataclass(frozen=True)
class PresupuestoCaso:
    max_llamadas: int = 8          # el FNOL normal usa 3
    max_tokens_entrada: int = 60_000
    max_euros: float = 0.50        # ~5× el coste típico de 0,11 €
    max_segundos: int = 45
```

Los números salen de medir, no de inventar: se toma el percentil 95 de producción y se multiplica por tres. Un tope que salta en el 2 % de los casos no es un tope, es una avería.

Lo importante es **qué pasa al agotarse**, y solo hay una respuesta aceptable en Meridiana:

```python
if presupuesto.agotado():
    traza.marcar("presupuesto_agotado", gastado=presupuesto.consumido())
    return Triaje.derivar(motivo="presupuesto de caso agotado")
```

No se lanza una excepción, no se devuelve un 500 y **no se pierde el expediente**. Se deriva a la cola humana, que es exactamente lo que pasaba antes de que existiera el agente. El caso se resuelve más despacio y ya está.

Un presupuesto por caso convierte un bucle infinito en un incidente acotado: una línea en el panel y un tramitador con un caso de más. Sin él, lo mismo es una factura sorpresa y un expediente a medias.

Y mide la distancia al tope, no solo los cortes. Si el percentil 95 del gasto por caso se acerca al límite, algo está creciendo.

### 15. Presupuesto por tenant y por día

Meridiana es una sola compañía y no tiene inquilinos, pero sí tiene **tres vías de entrada**: el portal del asegurado, la app del tramitador y el teléfono transcrito. Cada una es una fuente de gasto con un perfil distinto, y cada una necesita su cuota diaria.

Por qué importa aquí y no solo en un SaaS multi-cliente: en B1 vimos que un bucle mal escrito en el portal puede meter 4.000 FNOL en un minuto. A 0,11 € el caso son **440 € en sesenta segundos**, más de un mes de operación normal.

La cuota diaria es un **control de seguridad**, no una medida de ahorro. Se dimensiona con el pico esperado y un margen, nunca con la media:

- Portal: 88/día de media, 600/día en granizo → cuota de **1.200 casos/día**.
- App del tramitador: acotada por 24 personas trabajando → cuota generosa, se agota sola.
- Teléfono: acotado por el número de operadores.

Y la parte que la gente olvida: **qué se hace al agotar la cuota**. Si el portal deja de aceptar siniestros, has convertido un ahorro de 400 € en una interrupción de un canal regulado de atención al cliente.

La respuesta correcta es la de siempre: se acepta el FNOL, se guarda el relato en crudo y se marca pendiente de extracción. Sube una alerta. La cuota frena el gasto del modelo, no el servicio.

### 16. El pico de granizo: 600 siniestros en 24 horas

El escenario del caso: una granizada deja **600 siniestros en 24 horas** frente a los 88 de un día normal. Casi siete veces la media, y no repartidos: ráfagas de 80 en diez minutos.

Lo primero, para quitarlo de en medio. **El coste en euros no es el problema.**

```
600 casos × 0,11 € = 66 €
```

Sesenta y seis euros, contra los ~290 € de un mes entero. El día más duro del año cuesta lo que dos días normales. Cualquiera que te diga que el pico es un problema de factura no ha hecho la cuenta.

Lo que sí revienta, en orden de aparición:

- **Los límites de tasa del proveedor.** 80 casos en diez minutos son 240 llamadas y unos 2 millones de tokens de entrada: alrededor de **200.000 tokens por minuto**. Ese es el número que hay que comparar con tu cuota (slide 18).
- **La cuota diaria mal dimensionada.** Si la pusiste en 300 casos/día mirando la media, corta el servicio justo el día que más falta hace.
- **La cola humana.** 600 siniestros entre 24 tramitadores son **25 casos por cabeza** encima de los 3,7 diarios habituales. Aunque el agente vaya perfecto, todo lo que derive cae en una cola que ya está desbordada.
- **Las conexiones a base de datos**, si paralelizaste sin techo.

La tercera es la de verdad. Un agente que en el pico deriva el 20 % genera 120 derivaciones en un día, cinco por tramitador. Ese día el umbral de derivación debería ser más estricto en lesiones y más permisivo en el resto, y esa es una decisión de negocio que se toma antes, no durante.

### 17. Colas, contrapresión y degradación ordenada

En el pico hay más trabajo entrando del que el sistema puede procesar. Solo hay tres respuestas posibles, y dos son inaceptables: caerse, o aceptar todo y morir despacio con timeouts en cascada.

La tercera es **encolar con contrapresión y degradar por prioridad**.

El principio que ordena Meridiana: **la admisión nunca se degrada**. El FNOL se acepta siempre —es un canal regulado de atención—, se persiste el relato en crudo y se devuelve el número de expediente. Lo que se encola es la extracción, que es lo caro.

La cola no es FIFO. Se prioriza por consecuencia:

1. **Indicios de lesiones en el texto en crudo.** Detección por palabras clave, sin modelo, en el momento de admitir. Pasa por delante de todo.
2. **Siniestros con contrario y parte amistoso**, que tienen plazos de gestión con la contraria.
3. **El resto**, por orden de llegada.

Y una lista de degradaciones escritas de antemano, que se activan por conmutador cuando la cola pasa de un umbral:

- Se apaga la **redacción** de la petición de documentación: se manda la plantilla determinista por vía. Ahorra una llamada de las tres.
- Se apaga la **reevaluación nocturna** de expedientes abiertos. Libera capacidad entera.
- Se sube el umbral de vueltas del bucle a 5: lo que no sale rápido, se deriva.

Cada degradación es un conmutador con nombre, probado, y un aviso en el panel de que está activo. Improvisar esto a las tres de la mañana con la cola en 4.000 no sale bien.

### 18. Límites de tasa del proveedor y qué hacer con ellos

Tu proveedor te limita por dos ejes a la vez —peticiones por minuto y tokens por minuto— y en un sistema con contextos grandes **el que salta primero es el de tokens**. En Meridiana, 88 casos al día son 24 llamadas por hora: ridículo en peticiones. Pero cada una arrastra 8.500 tokens de entrada de media, y en el pico eso son ~200.000 tokens por minuto. Los límites concretos son de tu cuenta y de tu nivel de acceso, no del modelo: los ves en tu panel y suelen subirse pidiéndolo. Lo que te llevas de aquí no es la cifra, es cuál de los dos ejes te va a saltar primero.

Lo que **no** funciona cuando llega el 429:

- **Espera exponencial ciega.** Todos los trabajadores esperan lo mismo, vuelven a la vez y provocan el siguiente 429. Se llama *thundering herd* y es lo que convierte un límite en un incidente.
- **Reintentar sin mirar la cabecera.** Muchas APIs te dicen cuánto queda de cuota y cuánto esperar. Ignorarlo es reintentar a ciegas.

Lo que funciona:

- **Concurrencia adaptativa.** Un único regulador que baja el número de llamadas simultáneas al ver un 429 y lo sube despacio cuando no los hay. Un limitador de tokens del lado del cliente, alimentado con las cabeceras de cuota restante.
- **Aleatorizar la espera.** La misma exponencial, con ruido, para no sincronizar a la flota.
- **Un presupuesto de tasa por vía**, para que la reevaluación nocturna no se coma la cuota del portal.
- **Pedir la subida de nivel antes de necesitarla.** Se tramita en días, no en minutos, y el granizo no avisa. Los datos que hay que llevar están en la slide 27.

### 19. Modelar el coste mensual con números de Meridiana

Un modelo de coste útil cabe en una hoja y tiene seis variables. Cinco las mides tú; la sexta la pone el proveedor.

```
casos_mes            = 88 × 30           = 2.640
tokens_entrada_caso  =                     25.500
tokens_salida_caso   =                      1.600
sobrecoste_fallos    = 1,08 ÷ 0,96      = 1,125
PRECIO_ENTRADA       = 3 €/M            (ejemplo)
PRECIO_SALIDA        = 15 €/M           (ejemplo)
```

De ahí sale todo:

```
producción FNOL       2.640 × 0,11 €      =   290 €/mes
evals en CI           440 pasadas × 3,41 € = 1.500 €/mes   (slide 22)
reevaluación nocturna                                       partida propia
```

La regla que hace útil el modelo: **no des un número, da tres escenarios.**

- **Base.** 2.640 casos, tasas de fallo actuales: ~290 €/mes de producción.
- **Malo.** La tasa de reintento sube al 30 % y el contexto crece un 40 % porque alguien añadió ejemplos al prompt: **~440 €/mes**. Ni un cambio de precio, solo dos descuidos.
- **Pico.** Dos episodios de granizo al año: +66 € cada uno. **Irrelevante.**

La conclusión incómoda de este bloque, y conviene decirla pronto: con estos precios de ejemplo, la factura del modelo en Meridiana es de miles de euros al año, no de cientos de miles. El caso de negocio no se juega ahí. Se juega en no perder expedientes y en no quedarse sin servicio (slide 28).

Pon el modelo en el repositorio y recalcúlalo con los tokens medidos reales cada mes. Un modelo que no se contrasta con la factura es ficción.

### 20. Alertas de coste que avisan antes de la factura, no después

La alerta de gasto acumulado del mes es la que todo el mundo pone y la que nunca sirve: cuando salta, ya has gastado el dinero. Es un recibo con retraso, no una alarma.

Las alertas que avisan a tiempo miran **razones**, no totales:

- **Coste medio por caso, media móvil de una hora.** Es el indicador principal. Si pasa de 0,11 € a 0,15 € sin que hayas desplegado nada, algo se está arrastrando en el contexto o los reintentos han subido.
- **Percentil 95 del coste por caso.** Detecta la minoría de casos que se descontrolan mucho antes que la media.
- **Casos que agotan el presupuesto de la slide 14**, en número absoluto. Cero es lo normal. Tres en una hora es un incidente.
- **Tasa de reintento y tasa de escalada de la cascada.** Las dos preceden a la subida del coste; son la causa, no el efecto.
- **Tokens de entrada por caso**, comparados con la línea base del despliegue anterior. Un pull request que engorda el prompt del sistema sale aquí el mismo día.

Dos detalles que las hacen accionables:

**Compara con el despliegue, no con ayer.** «El coste por caso subió un 22 % desde `v1.14.0`» apunta a un diff. «Subió un 22 % desde ayer» no apunta a nada.

**Alerta también si baja mucho.** Una caída brusca del coste por caso casi nunca es una optimización: suele ser que un fallo silencioso está saltándose una de las tres llamadas.

### 21. Atribuir coste por cliente, por vía y por versión de prompt

Un total mensual no responde a ninguna pregunta útil. Para responderlas hay que **etiquetar cada llamada** y guardar los tokens en la traza de B2.

El mínimo indispensable en cada registro de llamada:

```sql
CREATE TABLE llm_call (
    id               uuid PRIMARY KEY,
    claim_id         text NOT NULL,       -- el caso
    canal            text NOT NULL,       -- portal | app | telefono
    tarea            text NOT NULL,       -- extraccion | redaccion | resolucion
    modelo           text NOT NULL,
    prompt_version   text NOT NULL,       -- v1.14.0
    tokens_entrada   int  NOT NULL,
    tokens_cacheados int  NOT NULL,
    tokens_salida    int  NOT NULL,
    reintento_de     uuid NULL,           -- enlaza el reintento con el original
    creado_en        timestamptz NOT NULL
);
```

Fíjate en dos columnas. `tokens_cacheados` es lo único que te dice si el caching funciona de verdad. Y `reintento_de` es lo que permite separar el coste útil del coste desperdiciado, que es la distinción más importante de todo el bloque.

Con eso, preguntas que antes no tenían respuesta:

- «¿Cuánto cuesta un FNOL del portal frente a uno telefónico?» — agrupa por `canal`.
- «¿El prompt nuevo salió más caro?» — compara `prompt_version`.
- «¿Cuánto del gasto es reproceso?» — suma donde `reintento_de` no es nulo.
- «¿Qué acierto de caché tenemos en el pico?» — `tokens_cacheados / tokens_entrada` por hora.

**El coste se calcula fuera, con una tabla de precios versionada por fecha.** Nunca guardes euros en esta tabla: el día que cambien las tarifas, tu histórico deja de poder recalcularse.

### 22. El coste de los evals en CI y cuándo empieza a doler

Aquí llega la sorpresa del bloque. Los evals de B3 casi siempre cuestan más que la producción, y nadie los mira porque no salen en el panel de operación.

La cuenta, con el conjunto sintético de Meridiana y los precios de ejemplo:

```
31 casos × 0,11 €            =  3,41 €  por pasada completa
20 pasadas/día × 22 días     =  440 pasadas/mes
                             =  1.500 €/mes
```

**Mil quinientos euros al mes contra los 290 € de producción.** Cinco veces. Y eso con un conjunto pequeño y un equipo modesto.

Cuando el conjunto crezca a 300 casos —que es lo que pasa cuando empiezas a añadir cada incidente como caso de regresión— la misma cadencia son **14.500 €/mes**. Ahí es donde duele.

Lo que se hace, por orden de eficacia:

- **Estratificar.** En cada push, un subconjunto de 30 casos que cubra las clases críticas (lesiones, fuera de vigencia, inyección, duplicados). El conjunto completo, en `main` y una vez por la noche.
- **Cachear por hash.** La clave es `(prompt_version, modelo, caso_id)`. Si nada de eso cambió, el resultado ya lo tienes. En una rama que toca solo el gateway, el ahorro es total.
- **Usar el caching de prompt también en CI.** Las 300 llamadas seguidas comparten prefijo y la caché está caliente todo el rato: es el escenario ideal de la slide 6.
- **No evaluar con el modelo grande lo que produce el pequeño.** Un juez LLM multiplica el coste por dos. Donde haya comprobación determinista, se usa.

Y una advertencia: no recortes los evals para ahorrar. Un fallo en producción de Meridiana cuesta más que un año de CI. Recorta la **frecuencia** y la **redundancia**, nunca la cobertura de las clases críticas.

### 23. Contexto largo frente a recuperación: el cálculo honesto

Un expediente maduro de Meridiana tiene doce adjuntos: parte amistoso, fotos con su descripción, presupuestos de taller, informes. Unos **40.000 tokens** si lo metes entero.

La opción perezosa es meterlo todo en cada llamada. La cuenta, con los precios de ejemplo:

```
40.000 × 3 llamadas × 3 €/M  =  0,36 €/caso   frente a 0,10 €
```

Multiplicado por 32.000 siniestros al año: **11.500 €** contra 3.200 €. Ocho mil euros de diferencia.

La alternativa es recuperar solo lo que hace falta: cinco fragmentos, unos 3.000 tokens, `+0,027 €` por caso. Contra eso pagas indexar 32.000 expedientes una vez, el almacenamiento del índice y el mantenimiento de una pieza más.

Con esos números la recuperación gana con holgura. Pero el cálculo honesto incluye lo que casi nadie pone en la hoja:

- **La recuperación introduce un modo de fallo nuevo.** No recuperar el fragmento que decide. En Meridiana eso puede ser el informe médico que menciona una lesión, y ese fallo es exactamente el que no puedes permitirte.
- **Ese fallo hay que evaluarlo**, y su suite de evals cuesta dinero y tiempo de ingeniería.
- **El contexto largo también degrada la calidad**, no solo el precio: 40.000 tokens de ruido diluyen las instrucciones.

La regla que uso: si el documento **completo** cabe holgadamente y se usa entero, mételo. Si vas a usar el 5 %, recupera. Y si el 5 % que necesitas es el que decide una derivación por lesiones, recupera **y** aplica una comprobación determinista por palabras clave sobre el documento completo, que cuesta cero.

### 24. Comprimir contexto sin perder lo que decide

Comprimir es decidir qué tiras. En un sistema donde una decisión se puede acabar defendiendo ante una reclamación, eso hay que hacerlo con una lista escrita, no con criterio del momento.

**Lo que nunca se comprime:**

- **El relato original del asegurado, literal.** Es la prueba de qué dijo el cliente y es la entrada de todas las reglas. Cuesta 600 tokens. No se toca.
- **Los campos que leen las reglas deterministas de triaje.** Si el código mira `hay_lesiones`, `fecha_siniestro` y `vigencia_poliza`, esos tres campos viajan siempre.

**Lo que se resume con código:**

- **Resultados de tools.** `consultar_poliza` devuelve 4 KB; el bucle usa cobertura, franquicia y estado de recibos. Se proyectan tres campos y se tiran los otros cuarenta. De 1.000 tokens a 60.
- **El historial de vueltas anteriores.** Qué tool se llamó y si fue bien, no el JSON entero de vuelta.

**Lo que se recalcula en vez de arrastrarse:**

Cualquier dato derivado. Qué documentos faltan es una función de la vía y de lo ya adjunto; se recalcula en cada vuelta. Ya vimos en B1 por qué: un derivado arrastrado se queda viejo dentro de la misma ejecución.

Y la prohibición que ahorra disgustos: **no comprimas con el modelo lo que va a decidir el modelo.** Resumir el expediente con una llamada extra para meterlo en la siguiente cuesta tokens, añade latencia e introduce una pérdida de información que no puedes auditar. Comprime proyectando campos con código, que es determinista, gratis y se revisa en un pull request.

### 25. Medir antes de optimizar: el perfil de una ejecución

Nadie acierta de intuición dónde se va el dinero. El perfil de una ejecución es una tabla de veinte filas que se saca de la traza de B2 y que decide en qué trabajas la semana que viene.

Un FNOL real de Meridiana, desglosado por lo que ocupa la entrada:

| Componente | Tokens | % entrada |
|---|---|---|
| Catálogo de documentos por vía | 2.400 | 28 % |
| Schemas de las 5 tools | 1.100 | 13 % |
| Instrucciones del sistema | 500 | 6 % |
| Resultado de `consultar_poliza` sin resumir | 1.000 | 12 % |
| Historial de vueltas | 3.000 | 35 % |
| Relato del asegurado | 600 | 7 % |

Dos hallazgos que solo aparecen mirando la tabla:

**El catálogo de documentos es el 28 % de la entrada y solo lo usa la tercera llamada.** Quitarlo de las dos primeras es un cambio de una línea que recorta casi un 20 % del coste del caso.

**El relato del asegurado, lo único que aporta información de este siniestro concreto, es el 7 %.** El otro 93 % es andamiaje. Eso es normal, y es exactamente por lo que el caching y la proyección de campos funcionan.

Haz lo mismo con la latencia: milisegundos por llamada al modelo, por tool y por espera. En Meridiana el reparto típico deja el 80 % en las tres llamadas al modelo y el 20 % en tools secuenciales, lo que dice que paralelizar (slide 13) recorta como mucho ese 20 %.

Perfila **antes** de optimizar y **después**, con el mismo caso. Sin el después, no sabes si mejoraste o solo cambiaste cosas de sitio.

### 26. La optimización que no compensa: cuándo parar

Con los números de Meridiana, el modelo cuesta del orden de **4.500 € al año** contando producción y evals. Una semana de un ingeniero cuesta más que eso. Casi cualquier optimización que lleve más de tres días pierde dinero.

Mi criterio, en dos preguntas:

1. **¿El ahorro anual supera tres veces el coste de conseguirlo?** Si no, no se hace. Ahorrar un 15 % de 4.500 € son 675 € al año; si te lleva dos semanas, has perdido dinero y has añadido código que mantener.
2. **¿Elimina un riesgo, aunque no ahorre?** Entonces sí se hace, aunque falle la primera. El presupuesto por caso de la slide 14 no ahorra nada en operación normal: evita un incidente. La concurrencia adaptativa de la slide 18 tampoco: evita quedarte sin servicio en el granizo.

Lo que **siempre** compensa, porque es barato y estructural:

- Ordenar el prompt para el caching. Media hora.
- Proyectar los resultados de tools a los campos que se usan. Una tarde.
- Poner topes por caso. Una tarde.
- Quitar del contexto lo que la tabla de la slide 25 dice que no se usa.

Lo que casi nunca compensa en un sistema de este tamaño:

- Cuantizar y autoalojar un modelo para ahorrar unos miles al año. El coste real es operar una GPU y una guardia más.
- Un enrutador de modelos con siete niveles para un sistema con dos tareas.
- Recortar 200 tokens del prompt del sistema a mano, cada semana.

Escribe el número anual en la pizarra antes de empezar. La mitad de las optimizaciones se caen solas.

### 27. Negociar volumen con el proveedor: qué datos hacen falta

Con un gasto de miles de euros al año, Meridiana no negocia nada. La conversación empieza a existir con compromisos serios de volumen, y en ese momento el que llega con datos consigue condiciones y el que llega con un «gastamos bastante» consigue la tarifa de lista. A partir de qué volumen hay conversación, y con qué programa, lo pone cada proveedor y lo cambia. No lo busques en un curso: pregúntalo a tu comercial con el consumo de los últimos seis meses delante, que es lo que convierte la pregunta en una negociación.

Lo que hay que llevar, y todo sale de las slides 19 y 21:

- **Tokens al mes por modelo y por tarea**, separando entrada, entrada cacheada y salida. Los tres se tarifan distinto, así que un total agregado no sirve para nada.
- **Perfil horario y estacional.** Que tu carga sea plana de 9 a 19 tiene valor para quien vende capacidad. Y el pico documentado: 600 casos en 24 horas, ráfagas de 80 en diez minutos, dos episodios al año.
- **Previsión a 12 meses con su supuesto.** «32.000 siniestros al año, cobertura del agente del 60 % hoy y del 90 % en un año».
- **Qué parte es interrumpible.** La reevaluación nocturna tolera horas; el FNOL no. Eso es una moneda de cambio.

Y lo que hay que pedir **además** del precio, que a menudo vale más:

- **Límites de tasa mayores**, dimensionados con el pico y no con la media.
- **Retención cero de datos**, o el compromiso documentado que necesites. En Meridiana entran datos personales de siniestros, y en el curso 4 esto vuelve como parte del expediente técnico.
- **Aviso de deprecación con plazo suficiente.** El día que retiren tu modelo tienes que reevaluar el sistema entero. Sin aviso, eso es una urgencia.

### 28. Coste de la alternativa: cuánto costaría hacerlo a mano

El bloque cierra donde debía empezar la conversación. La pregunta no es si 0,11 € por caso es caro: es si es caro **comparado con no tener el agente**.

La capacidad actual de Meridiana, calculada solo con datos del caso:

```
24 tramitadores × ~220 jornadas/año  = 5.280 jornadas/año
5.280 ÷ 32.000 siniestros            = 0,165 jornadas por siniestro
con jornada de 7,5 h                 ≈ 1 h 15 min por siniestro
```

Y el caso dice que **la mayor parte de ese tiempo se va en leer, clasificar y pedir documentos**, no en decidir. Pongamos la mitad: unos 37 minutos por siniestro de trabajo que el agente puede absorber.

El coste hora cargado de un tramitador lo tiene recursos humanos, no este curso. Y para lo que se decide aquí no hace falta la cifra exacta: con cualquier valor imaginable en España, 37 minutos de una persona son **dos o tres órdenes de magnitud** más que 0,11 €. El precio por token no está ni cerca de ser la variable que decide.

Lo que sí decide, y por eso este bloque va después de arquitectura, observabilidad y evals:

- **Que el sistema no se caiga el día del granizo**, cuando la alternativa manual son 25 casos extra por tramitador.
- **Que ninguna decisión con consecuencias se tome sin traza**, porque un siniestro mal pagado cuesta del orden de 1.850 € y una reclamación cuesta más.
- **Que bajar de 11 días a 4** libere capacidad real en vez de mover el cuello de botella a la cola de derivaciones.

El coste por caso se mide, se presupuesta y se vigila. Pero el proyecto se gana o se pierde en el tiempo de tramitación y en la fiabilidad, no en la factura de tokens.

### 29. Ejercicio práctico 1: el modelo de coste de Meridiana en veinte líneas {ejercicio:B4-ej1}

**Objetivo.** Construir el modelo de coste del FNOL y averiguar qué variable domina la factura, para no optimizar la equivocada.

**Punto de partida.** El perfil de la slide 1: tres llamadas, 25.500 tokens de entrada y 1.600 de salida por caso. Precios de ejemplo `PRECIO_ENTRADA = 3 €/M` y `PRECIO_SALIDA = 15 €/M`.

**Qué hacer.**

1. Escribe `coste.py` con una función `coste_caso(tokens_entrada, tokens_salida, tasa_reintento, tasa_exito, precio_entrada, precio_salida)`. Ningún número dentro: todo son parámetros.
2. Reproduce el caso base y comprueba que te da **0,11 €**. Si no, revisa la fórmula antes de seguir.
3. Calcula el mes (2.640 casos) y el año (32.000 casos).
4. Haz un análisis de sensibilidad: sube **un 20 % cada variable por separado** y anota cuánto sube el total. Ordena las seis por impacto.
5. Añade dos escenarios: el conjunto de evals de la slide 22 con 31 casos y 20 pasadas al día, y un episodio de granizo de 600 casos.

**Qué tienes que poder responder al terminar:**

- ¿Qué variable, subida un 20 %, hace más daño? ¿Por qué no es el precio de salida?
- ¿Cuánto tendría que caer el precio de entrada para igualar el efecto de aplicar caching sobre los 4.000 tokens de prefijo?
- ¿Cuánto pesan los evals en CI frente a la producción?

**Criterio de acierto.** El caso base sale 0,11 € por caso y ~290 € al mes; la variable dominante son los **tokens de entrada**; y los evals salen varias veces por encima de la producción. Si tu modelo dice otra cosa, encuentra el error antes de mirar la solución.

### 30. Ejercicio práctico 2: presupuesto por caso y simulación del pico {ejercicio:B4-ej2}

**Objetivo.** Que un caso descontrolado sea un incidente acotado y que el pico de granizo no tumbe el sistema.

**Punto de partida.** El orquestador de `meridiana-agent` con el proveedor stub determinista, sin clave de API.

**Qué hacer.**

1. Implementa `PresupuestoCaso` con los cuatro topes de la slide 14: llamadas, tokens de entrada, euros y segundos. Se instancia por caso y el orquestador lo consulta **antes** de cada llamada al modelo, nunca después.
2. Al agotarse, el caso **deriva** con motivo `presupuesto_agotado` y deja traza con lo consumido. No lanza excepción y no pierde el expediente.
3. Añade un caso al conjunto sintético que provoque un bucle: una tool que devuelve siempre el mismo error. Sin presupuesto tiene que colgarse; con presupuesto tiene que derivar en menos de 8 llamadas.
4. Instrumenta cada llamada con las columnas de la slide 21, incluida `reintento_de`.
5. Simula el pico: lanza **600 casos con ráfagas de 80 en diez minutos** contra el stub, con un limitador de concurrencia adaptativo. El stub devuelve un 429 simulado por encima de N llamadas simultáneas.

**Criterios de aceptación.**

- Los 31 casos originales siguen dando **31 de 31**. El presupuesto no cambia ninguna decisión existente.
- El caso del bucle deriva, y la traza dice cuántas llamadas y cuántos euros se gastaron antes de cortar.
- Con 600 casos y 429 simulados, **ninguno se pierde**: todos terminan resueltos o derivados, y el número de 429 baja tras los primeros minutos porque la concurrencia se ha adaptado.
- El informe final imprime coste total, coste medio por caso, tasa de reintento y acierto de caché.
- Todo corre **sin clave de API** y en menos de 60 minutos.

### 31. Mini-quiz de comprensión — B4 {quiz:B4}

Tres preguntas sobre lo que decide de verdad la factura y el comportamiento en el pico: dónde colocar lo estable del prompt, cuándo una cascada deja de compensar, y qué hacer cuando el proveedor limita la tasa justo el día del granizo.

Antes de responder, comprueba que puedes decir en voz alta estas tres frases con los números del caso:

- El coste por caso de un FNOL es **0,11 €** y el 76 % de eso es entrada, no salida.
- Los 600 siniestros del granizo cuestan **66 €**: el problema del pico nunca fue el dinero.
- Los evals en CI cuestan hoy **más que la producción**, y crecen con el tamaño del conjunto.

Si alguna se te resiste, vuelve a las slides 1, 16 y 22 antes del quiz.

## Qué te llevas

- El coste por caso es una métrica de producto, no un detalle de infraestructura.
- Un presupuesto por caso convierte un bucle infinito en un incidente acotado.
- El pico se diseña antes de que llegue; durante, solo se puede degradar.

## Mini-quiz (3 preguntas)

Una sola opción correcta por pregunta, con explicación. Se generan con el mismo formato que `assets/js/quizzes-data.js` para que el importador las recoja.

1. **Tema:** Qué parte del prompt conviene fijar para aprovechar el caching
   - **Enunciado:** El prompt del FNOL empieza con «Hoy es {fecha}. Expediente {claim_id}.» y sigue con las instrucciones del sistema, los schemas de las cinco tools y el catálogo de documentos. El acierto de caché es prácticamente cero. ¿Qué hay que cambiar?
   - **Opciones:**
     - a) Acortar las instrucciones del sistema, porque el prefijo es demasiado largo para cachearse.
     - b) **Mover la fecha y el `claim_id` detrás del bloque estable, dejando delante instrucciones, schemas y catálogo.** ✅
     - c) Subir el TTL de la caché desde el cliente, para que sobreviva entre casos.
     - d) Nada: con 88 siniestros al día no hay volumen suficiente para que el caching sirva.
   - **Explicación:** La caché exige un prefijo idéntico byte a byte, y la fecha y el identificador cambian en cada llamada, así que invalidan todo lo que va detrás. Moverlos deja 4.000 tokens estables cacheables. La (a) confunde el problema: el prefijo largo es precisamente lo que hace rentable el caching. La (c) no depende del cliente. La (d) es falsa: incluso dentro de un solo caso, con tres llamadas que comparten prefijo, el ahorro ya es del orden del 18 % de la factura.

2. **Tema:** Cuándo una cascada de modelos sale más cara que el modelo grande
   - **Enunciado:** El modelo pequeño cuesta la décima parte que el grande. Montas una cascada con verificación determinista. ¿A partir de qué tasa de escalada la cascada deja de ahorrar dinero?
   - **Opciones:**
     - a) A partir del 10 %, porque es la proporción de precio entre los dos modelos.
     - b) A partir del 50 %, porque a partir de ahí más de la mitad de los casos pasan dos veces.
     - c) **A partir del 90 %: el coste esperado es `C_pequeño + e × C_grande`, que iguala a `C_grande` cuando `e = 1 − C_pequeño/C_grande`.** ✅
     - d) Nunca: como el pequeño siempre es más barato, la cascada siempre ahorra algo.
   - **Explicación:** La fórmula da el punto de indiferencia exacto, y con una relación de 1 a 10 son el 90 %. Ojo: eso es solo en euros. La latencia de los casos escalados, mantener dos suites de evals y una rama más de comportamiento hacen que en la práctica se quite la cascada bastante antes, alrededor del 50 %. La (a) y la (b) son intuiciones sin cuenta detrás. La (d) ignora que el caso escalado paga las dos llamadas, no solo la grande.

3. **Tema:** Qué hacer cuando llegan 600 siniestros y el proveedor limita la tasa
   - **Enunciado:** Episodio de granizo: ráfagas de 80 siniestros en diez minutos y el proveedor empieza a devolver 429 por límite de tokens por minuto. ¿Cuál es la respuesta correcta?
   - **Opciones:**
     - a) Cerrar temporalmente la admisión de FNOL en el portal hasta que baje la ráfaga.
     - b) Reintentar cada 429 con espera exponencial en todos los trabajadores a la vez.
     - c) Pasar los 600 casos al modo por lotes del proveedor, que además sale más barato.
     - d) **Aceptar y persistir todos los FNOL, encolar la extracción con prioridad para los indicios de lesiones, y bajar la concurrencia de forma adaptativa mientras haya 429.** ✅
   - **Explicación:** La admisión nunca se degrada, porque es un canal regulado de atención; lo que se encola es la parte cara. La prioridad por lesiones asegura que lo que tiene consecuencias sale primero, y la concurrencia adaptativa respeta el límite sin sincronizar la flota. La (a) rompe el servicio justo cuando más se usa. La (b) provoca que todos vuelvan a la vez y genera el siguiente 429. La (c) cambia dinero —66 € en total, irrelevante— por horas de latencia, en contra del objetivo de bajar la tramitación de 11 días a 4.

## Lab

Instrumentar el coste por caso en Meridiana, aplicar caching y medir el ahorro sobre el conjunto de evaluación.

**Enunciado.** Partes de `meridiana-agent` con la estructura que dejó B1 y la traza que dejó B2. Al terminar, cada ejecución dirá lo que cuesta, el prompt estará ordenado para el caching y tendrás medido el ahorro real sobre los 31 casos del conjunto sintético, no estimado.

Usa los precios de ejemplo del bloque (`PRECIO_ENTRADA = 3 €/M`, `PRECIO_SALIDA = 15 €/M`) en un fichero de tarifas versionado por fecha. Todo el lab corre contra el stub determinista, sin clave de API.

**Pasos:**

1. **Contabilidad.** Añade la tabla `llm_call` de la slide 21 y rellénala en la implementación de `ILlmClient`, no repartida por el código. Incluye `tokens_cacheados` y `reintento_de`.
2. **Perfil.** Saca la tabla de la slide 25 para un FNOL: cuántos tokens aporta cada componente de la entrada. Guárdala como artefacto de la ejecución.
3. **Reordena el prompt.** Estable delante, volátil detrás. Mueve fuera del prefijo cualquier fecha o identificador. Deja el catálogo de documentos solo en la llamada que lo usa.
4. **Mide el antes y el después** sobre los 31 casos: coste total, coste medio por caso, tokens de entrada por caso y acierto de caché.
5. **Presupuesto y alertas.** Integra el `PresupuestoCaso` del ejercicio 2 y añade dos comprobaciones en CI: coste medio por caso y tokens de entrada por caso frente a la línea base guardada.

**Criterios de aceptación:**

- `meridiana-agent --all --check` sigue dando **31 de 31**. Ninguna optimización cambia una decisión.
- El informe imprime **coste por caso, tokens de entrada, tokens cacheados, tasa de reintento y latencia p95**, y la suma de los 31 casos cuadra con la suma de la tabla `llm_call`.
- Los tokens de entrada por caso bajan **al menos un 20 %** respecto a la línea base, y la reducción se explica fila a fila con la tabla del paso 2.
- Un caso preparado para bucle **deriva** por presupuesto agotado en menos de 8 llamadas, con la traza indicando lo gastado.
- CI **falla** si el coste medio por caso sube más de un 15 % sobre la línea base. Compruébalo añadiendo 3.000 tokens al prompt del sistema a propósito.
- Reproducible en menos de 60 minutos, sin servicios de pago y sin clave de API.

**Solución de referencia:** en `content/caso/soluciones/B4/`, con el fichero de tarifas, el informe de perfil antes y después, y la comprobación de línea base para CI.

## Cierre

- Recapitulación en los tres puntos de arriba.
- Mini-quiz.
- Enlace al siguiente bloque.
