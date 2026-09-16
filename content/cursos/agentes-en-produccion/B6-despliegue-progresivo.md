# C-07 · B6 · Despliegue progresivo

> Curso: `agentes-en-produccion` · bloque `B6`

## Objetivo

Cambiar el modelo o el prompt de un sistema en producción sin descubrir el problema por una reclamación.

## Guion de slides

26 slides de contenido. Una idea por slide, con un ejemplo real o del caso Meridiana. Nada de relleno.

### 1. Por qué desplegar un cambio de modelo no es desplegar código

Cambiar una frase del prompt o el identificador de versión del modelo pasa por el mismo sitio que cualquier otro commit: rama, pull request, CI en verde, despliegue. Ahí acaba el parecido.

Un cambio de código que rompe algo suele romperlo de forma ruidosa. No compila, un test falla, salta una excepción y aparece en la traza. Un cambio de comportamiento no rompe nada. La respuesta sigue siendo un JSON válido, con los campos que pedías y los tipos correctos. Lo único distinto es que ahora un puñado de siniestros que antes se derivaban se tramitan solos.

**No hay compilador que avise.** No hay stack trace, no hay error 500, no hay alerta de latencia. Si no has montado nada que lo detecte, el primer aviso llega semanas después y llega mal: un tramitador que comenta de pasada que «últimamente me entran menos derivaciones», o una reclamación con el expediente delante.

En Meridiana ese margen se paga caro. Son 88 siniestros al día, cada uno con una decisión de vía y a veces una propuesta de importe. Un desplazamiento de comportamiento del 3 % se convierte en dos o tres expedientes diarios que van por donde no deberían, todos los días, hasta que alguien mire.

De ahí la tesis del bloque: **el prompt y la versión del modelo son artefactos desplegables por derecho propio**, con su versión, su comparación previa y su botón de vuelta atrás. Tratarlos como una constante más dentro del código es lo que obliga a descubrir el problema por la vía cara.

### 2. Lo que cambia sin avisar: versiones del proveedor

El caso peor no es el despliegue que haces tú. Es el que hace el proveedor mientras tú no tocas nada.

Los proveedores exponen dos formas de pedir un modelo: un identificador exacto de una versión concreta, y un alias cómodo que apunta a «la más reciente de esa familia». El alias es lo que sale en la documentación, lo que copia todo el mundo el primer día, y lo que sigue en el fichero de configuración dos años después.

Con un alias, tu sistema cambia de comportamiento sin un solo commit. El diff de tu repositorio está vacío. La revisión no existe porque no hubo nada que revisar. Y el registro de despliegues, si lo tienes, no dice nada: para él ese día no pasó nada.

Hay una segunda cara del mismo problema. Las versiones concretas tampoco son eternas: se anuncian, se marcan como obsoletas y se retiran. El aviso llega por correo a la cuenta de facturación, que casi nunca es la de quien mantiene el sistema.

Para Meridiana esto es un riesgo operativo, no una molestia. La extracción de FNOL alimenta el triaje, y el triaje decide qué se deriva. Que la extracción cambie de criterio un martes por la mañana, sin que nadie del equipo haya hecho nada, es exactamente el escenario que este bloque intenta hacer imposible.

> Si no eliges tú cuándo cambia el modelo, lo elige el proveedor, y se entera tu tramitador antes que tú.

### 3. Fijar versión de modelo y qué implica

La respuesta a la slide anterior es corta: en el fichero de configuración va el **identificador exacto de una versión**, nunca el alias. Y esa configuración se revisa como cualquier otro cambio con consecuencias.

```yaml
# config/modelo.yaml — este fichero es un artefacto desplegable, no una constante.
extraccion_fnol:
  modelo: "<identificador-exacto-de-la-version>"   # nunca un alias del tipo "-latest"
  prompt: "fnol-extraccion@v7"
  max_tokens: 1200
  temperatura: 0
```

Fijar la versión compra una cosa concreta: **el cuándo**. A partir de ahí, el cambio de comportamiento ocurre el día que tú lo despliegas, con tu comparación hecha y tu gente mirando.

Lo que no compra es el *si*. Las versiones se retiran, y cuando eso pasa migras en el calendario del proveedor, no en el tuyo. Fijar la versión sin vigilar los avisos de retirada solo cambia el susto por otro: el día que la versión desaparece, el sistema deja de responder en vez de responder distinto. De los dos fallos, ese es el bueno —es ruidoso—, pero sigue siendo un fallo.

Dos consecuencias prácticas para Meridiana:

- La versión fijada se escribe **también en cada traza**, no solo en la configuración. Sin eso no se puede reconstruir con qué versión se decidió un expediente concreto.
- Alguien del equipo, no de facturación, recibe los avisos de retirada. Y el calendario de migración entra en la planificación como cualquier otro trabajo.

### 4. Banderas de funcionalidad aplicadas a prompts y modelos

Si el prompt y la versión del modelo son artefactos, tienen que poder desplegarse y retirarse **sin reconstruir la imagen del servicio**. Eso es una bandera de funcionalidad de toda la vida, aplicada a algo que normalmente no se piensa así.

La forma mínima: los prompts viven en el repositorio con un identificador de versión y un hash; la bandera es un dato en la base, no una constante compilada; y el orquestador resuelve la variante al empezar cada caso.

```python
# La bandera es dato: cambiarla es un UPDATE, no un despliegue.
variante = banderas.resolver("extraccion_fnol", clave_estable=expediente.id)

config = REGISTRO_PROMPTS[variante]        # {"prompt": "fnol-extraccion@v7", "modelo": "..."}
extraccion = await llm.extraer_fnol(relato, config)
```

Tres reglas que evitan que esto se convierta en otro problema:

- **Una bandera por artefacto, no una por idea.** `extraccion_fnol` tiene variantes; no hay banderas sueltas por cada frase del prompt.
- **Las variantes son un conjunto cerrado**, declaradas en el repositorio. La base guarda cuál está activa y con qué porcentaje, no el texto del prompt.
- **Toda bandera nace con fecha de retirada.** Una bandera que lleva ocho meses al 100 % ya no es una bandera: es código muerto que multiplica los caminos posibles.

Lo que compras con esto se ve en la slide 12: la reversión pasa de ser un despliegue a ser un cambio de valor.

### 5. Shadow mode: ejecutar el nuevo sin que afecte a nadie

Sombra es lo más barato que puedes hacer antes de arriesgar un solo expediente: para cada siniestro real, la versión anterior atiende el caso y decide, y la versión nueva lo procesa en paralelo **sin que su salida llegue a ninguna parte**. Se registran las dos y se comparan después.

La palabra importante es *ninguna*. En Meridiana el agente crea peticiones de documentación, adjunta documentos y avisa al tramitador. Si la ejecución en sombra puede llamar a esas tools, el asegurado recibe dos correos pidiéndole el mismo parte y la sombra deja de ser sombra.

```python
resultado = await agente.ejecutar(siniestro, config=CONFIG_ACTUAL)

if sombra.activa:
    # Sin efectos: las tools de escritura devuelven un resultado simulado.
    en_sombra = await agente.ejecutar(siniestro, config=CONFIG_NUEVA, solo_lectura=True)
    comparador.registrar(siniestro.id, resultado, en_sombra)

return resultado   # lo que sale al mundo es siempre lo de la versión que manda
```

El `solo_lectura=True` no es una promesa: es un doble de las tools de escritura que devuelve lo que devolvería la real y no toca nada. Si eso no existe, la sombra no se enciende.

El coste hay que decirlo claro. Sombra sobre el 100 % del tráfico **duplica el gasto de tokens** de la extracción: 88 llamadas extra al día en un día normal. Sale barato la primera vez que evita un despliegue malo, y es dinero tirado si nadie mira las comparaciones. Enciéndela con una fecha de apagado.

### 6. Comparar shadow contra producción: qué métricas y qué diferencias importan

El error típico al montar una comparación es medir el porcentaje de salidas idénticas. Ese número no sirve: en un flujo con texto libre casi nunca hay dos salidas iguales carácter a carácter, y el 12 % de diferencias que te salga mezcla cosas irrelevantes con cosas graves.

Lo que se compara es **la consecuencia**, y se clasifica por gravedad antes de mirar ningún número:

- **Bloqueante.** Un caso que la versión anterior derivaba por lesiones y la nueva no. Uno solo ya es motivo para no promocionar nada.
- **Grave.** Cambia la vía de tramitación. Cambia si la propuesta de importe queda por debajo o por encima del umbral de 1.500 €, porque eso cambia quién la aprueba y cuánto tarda.
- **Relevante.** Un campo que antes se extraía y ahora sale ausente, o al revés. Un documento que se pide y antes no.
- **Cosmético.** La redacción de la petición al asegurado, el orden de una lista, un espacio. No se cuenta.

Con esa escala, el informe de la comparación es corto y accionable: cuántos bloqueantes, cuántos graves, y la lista completa de los graves con el identificador del expediente para poder abrirlos uno a uno.

Y un aviso sobre la dirección de la diferencia: **distinto no es peor**. Si la versión nueva extrae una matrícula que la anterior daba por ausente, eso es una mejora. La comparación te dice dónde mirar; quién decide si el cambio es bueno es una persona leyendo esos casos.

### 7. Parallel run: los dos sistemas, un solo resultado

La sombra compara máquina contra máquina. El *parallel run* mete a una persona en medio, y es la única forma de saber cuál de las dos versiones es realmente mejor cuando las dos son plausibles.

La mecánica en Meridiana: durante un tramo acotado, en los expedientes que ya iban a pasar por un tramitador, la pantalla enseña **las dos propuestas** —la de la versión anterior y la de la nueva— sin decir cuál es cuál. El tramitador elige la que usaría, o ninguna, y sigue trabajando. Su elección es la etiqueta.

Lo que compras: pasas de «hay un 6 % de casos donde difieren» a «de esos casos, el tramitador prefirió la nueva en dos de cada tres». Eso ya es una decisión de promoción con base, no una lectura de tripas.

Lo que pagas, y hay que dimensionarlo antes de proponerlo:

- **Tiempo de 24 personas.** Leer dos propuestas en vez de una cuesta segundos por expediente, y esas personas ya tienen su cola. Se acota a una muestra y a unos días, no se deja abierto.
- **Sesgo de posición.** Si la nueva siempre sale a la derecha, la derecha gana. Se alterna el orden por expediente.
- **No sirve para todo.** Solo funciona donde ya había revisión humana. En lo que se tramita solo no hay quien etiquete, y ahí te quedas con la sombra.

### 8. Canary: el porcentaje y el criterio para subirlo

Después de la sombra viene el primer tráfico real: un porcentaje de los siniestros se atiende de verdad con la versión nueva. Y aquí es donde los números de Meridiana obligan a pensar.

Con 88 siniestros al día, **un canary del 5 % son cuatro casos al día**. Cuatro. Con cuatro casos no detectas un desplazamiento en la tasa de derivación, no ves un cambio en la distribución de vías y no distingues una regresión de un martes raro. Un canary al 1 %, que en un sistema con millones de peticiones es prudencia, aquí es teatro: un caso cada tres días.

De ahí la regla que ordena la escalera: **se sube por número de casos observados, no por horas transcurridas**.

Una escalera razonable para este volumen:

1. **5 %** hasta acumular 40 casos comparables. Sirve para detectar lo que revienta rápido: errores de esquema, latencias, tools que fallan.
2. **25 %** hasta acumular 200 casos. Aquí empiezan a leerse las distribuciones.
3. **50 %** durante una semana completa, para que entren los lunes y los viernes, que no se parecen.
4. **100 %**, con la versión anterior todavía cargada y la bandera intacta durante otra semana.

En cada peldaño, el mismo gesto: se mira la lista de criterios de la slide 10 y se decide subir, quedarse o revertir. No subir también es una decisión válida, y quedarse una semana más cuesta mucho menos que promocionar a ciegas.

### 9. Segmentar el canary: por qué no vale un porcentaje aleatorio en seguros

Un canary aleatorio uniforme parte del supuesto de que todos los casos valen lo mismo. En un flujo de siniestros eso es falso, y de tres maneras distintas.

**Primera: hay casos que no se experimentan.** Un siniestro con indicios de lesiones personales se deriva, y esa regla es determinista y vive en código. Ninguna variante del agente participa en esa decisión, así que ningún experimento la toca. Lo mismo con lo que ya está en revisión por reclamación. El canary se define sobre lo que queda, y esa exclusión se escribe.

**Segunda: la asignación tiene que ser pegajosa.** Si el sorteo es por petición, el mismo expediente puede pasar por la versión nueva al abrirse y por la anterior al recibir la documentación. Entonces tienes un expediente atendido por dos comportamientos distintos, imposible de atribuir y desagradable de explicar. La clave del sorteo es el **identificador del expediente**, y es estable durante toda su vida.

```python
# Pegajoso por expediente y reproducible: el mismo id siempre cae del mismo lado.
def en_canary(expediente_id: str, porcentaje: int) -> bool:
    if politica.excluido_de_experimentos(expediente_id):
        return False
    return int(hashlib.sha256(expediente_id.encode()).hexdigest(), 16) % 100 < porcentaje
```

**Tercera: el 5 % aleatorio puede no contener lo que quieres vigilar.** Los siniestros con propuesta cercana al umbral de 1.500 € son los que más importan y no son mayoría. Merece la pena reservar un cupo: además del porcentaje general, todos los casos de un tipo concreto que quieras observar de cerca.

### 10. Criterios de promoción escritos antes de empezar

Un criterio de promoción escrito **después** de ver los datos no es un criterio: es una justificación. Y siempre aparece alguien —a menudo tú mismo, a las siete de la tarde— capaz de explicar por qué ese número que salió un poco peor en realidad no importa.

Por eso la lista se escribe antes de encender el canary, se pega en el pull request del cambio y no se toca mientras el canary corre.

Cada criterio tiene cuatro partes, y si le falta alguna no es un criterio:

- **Qué se mide.** Una métrica concreta, con su definición. «Tasa de derivación» es ambiguo; «porcentaje de siniestros del canary marcados como derivados en el triaje» no lo es.
- **Qué umbral.** Un número. «No empeorar» no es un umbral porque no dice cuánto ruido se tolera.
- **Sobre cuántos casos.** La ventana mínima de la slide 8, en casos observados.
- **Quién firma.** Un nombre. Ingeniería sola no promociona un cambio que afecta a la carga de 24 tramitadores.

Un ejemplo del caso, con la forma completa:

> Se promociona de 25 % a 50 % si, sobre al menos 200 casos: cero diferencias bloqueantes; la tasa de derivación se mantiene dentro de ±2 puntos respecto a la versión anterior en la misma ventana; la proporción de propuestas que cruzan el umbral de 1.500 € se mantiene dentro de ±3 puntos; y la responsable de operaciones de siniestros firma que su equipo no ha reportado nada raro. Lo firman ella y el responsable técnico del agente.

### 11. Criterios de reversión, y quién puede pulsar el botón

Los criterios de promoción y los de reversión no son la misma lista con el signo cambiado. Promocionar admite deliberación; revertir, no. La lista de reversión es más corta, más dura y **cualquiera de las personas autorizadas la ejecuta sin pedir permiso a nadie**.

En Meridiana, revierte solo:

- Un caso en el que la versión nueva no derivó un siniestro con indicios de lesiones. **Uno basta.** Esta no espera ventana ni volumen.
- Cualquier propuesta de importe que llegue a un tramitador sin la validación de coberturas hecha.
- Errores de esquema por encima del umbral acordado, que dejan expedientes a medias.
- Un aumento de coste por caso que rompa el presupuesto del bloque de costes.
- La duda razonable de la responsable de operaciones. Sin métrica que la respalde. Sí, así escrito.

La lista de quién puede revertir se escribe con nombres, y tiene que incluir a **operaciones de siniestros, no solo a ingeniería**. Quien primero ve que algo va mal es la persona que lleva veinte expedientes raros esa mañana, y si tiene que abrir un ticket y esperar, la reversión llega tarde.

El acuerdo que hace que esto funcione en la práctica: **revertir nunca se cuestiona**. Se explica después, con calma, mirando los datos. Un equipo donde revertir cuesta una conversación incómoda es un equipo que revierte tarde.

### 12. Reversión en un minuto: qué hay que tener preparado

«Se puede revertir» es una frase que casi todo el mundo dice y casi nadie ha cronometrado. Si revertir implica abrir una rama, esperar a CI y desplegar, tu tiempo de reversión son veinte minutos en el mejor día, y a las tres de la mañana son cuarenta.

Lo que hace falta para que sean sesenta segundos:

- **La bandera es dato.** Revertir es cambiar un valor y que el orquestador lo lea en el siguiente caso. Si hay que reconstruir la imagen, no es reversión: es un despliegue de emergencia.
- **La versión anterior sigue cargada.** El prompt anterior está en el registro, la versión anterior del modelo sigue configurada y accesible. No se retira nada hasta que el cambio lleva semanas al 100 %.
- **La propagación es rápida y acotada.** Si el valor de la bandera se cachea en cada instancia durante cinco minutos, tu reversión tarda cinco minutos como mínimo. Elige ese número a sabiendas.
- **Los casos en vuelo terminan como empezaron.** Un expediente que ya está a medias con la versión nueva la termina con la versión nueva. Cambiarle la configuración a mitad produce un híbrido que no se parece a ninguna de las dos.
- **Está escrito y probado.** Un procedimiento de cuatro líneas que alguien que no lo escribió puede seguir. La slide 26 va sobre esto.

Y una métrica que conviene tener y casi nadie tiene: **el tiempo transcurrido entre la decisión de revertir y el primer caso atendido con la versión anterior**. Se mide en los ensayos.

### 13. El despliegue que no se puede revertir: datos ya escritos

Bajar la bandera detiene el daño futuro. No rebobina el pasado. Y en Meridiana el pasado del agente está lleno de efectos que ya salieron del sistema.

Durante una semana de canary al 25 %, la versión nueva ha atendido unos 150 siniestros. De ahí han salido correos al asegurado pidiendo documentación, documentos adjuntados a expedientes, avisos a tramitadores y propuestas de importe que alguien ya aprobó. Nada de eso se deshace con un `UPDATE`.

De ahí que la reversión tenga dos mitades, y la segunda se olvide siempre:

1. **Parar.** Bajar la bandera. Es la de un minuto.
2. **Reparar.** Sacar la lista de expedientes atendidos por la versión nueva, revisarlos y decidir qué hacer con cada grupo.

La segunda mitad **exige** lo de la slide 24: si no registraste qué versión atendió cada caso, la lista no se puede sacar y la reparación se convierte en revisar todo lo de la semana a mano.

La decisión de reparación no es técnica, así que no la toma ingeniería sola. Los grupos típicos: expedientes donde el fallo no cambió el resultado y se dejan; expedientes que fueron por una vía equivocada y hay que reconducir; y expedientes donde ya salió un correo al asegurado, que necesitan una segunda comunicación escrita por una persona.

Todo esto se decide mucho mejor un martes por la tarde, en frío, que durante el incidente.

### 14. Migraciones de esquema de memoria y contexto

Hay un cambio que parece de prompt y es de esquema: **cambiar la forma de lo que el modelo devuelve y tú guardas**. Partir `lugar` en `via` y `municipio`, añadir un campo de confianza a `hay_lesiones`, cambiar un valor del enumerado de vías.

Eso no se revierte bajando una bandera, porque en la base ya hay filas escritas con la forma nueva y código antiguo que no sabe leerlas. Es una migración con toda la letra, y el modelo no tiene nada que ver.

La secuencia que evita el problema es la de siempre, en tres despliegues separados:

1. **Ampliar.** El esquema admite las dos formas. El código sabe leer las dos. Todavía se escribe la vieja.
2. **Migrar.** Se empieza a escribir la nueva. Las filas antiguas se rellenan hacia atrás o se marcan. El código sigue leyendo las dos.
3. **Contraer.** Cuando no queda nada que lea la forma vieja, se retira. Este paso puede tardar semanas y no pasa nada.

Entre 1 y 3 puedes revertir el comportamiento cuando quieras, porque el esquema aguanta las dos versiones. Ese es el punto entero.

Y una precaución específica de estos sistemas: la traza y el contexto de la conversación también tienen forma. Si tu comparador de la slide 6 lee trazas de dos formatos distintos y solo entiende uno, tu comparación miente sin decirlo.

### 15. Comunicar el cambio a operaciones y a negocio

Un canary sin testigos humanos es media herramienta. Las métricas cogen lo que sabías medir; los 24 tramitadores cogen lo que no se te ocurrió.

Pero eso solo pasa si lo saben. Un tramitador que ve tres propuestas raras y no sabe que hay un cambio en curso asume que es martes y sigue trabajando. El mismo tramitador avisado escribe dos líneas y te ahorra una semana.

El aviso, antes de encender el canary, cabe en un párrafo y dice cuatro cosas:

- **Qué cambia**, en su idioma. «Cambiamos el motor que lee los relatos de siniestro. Puede que notes que la petición de documentación está redactada distinto.»
- **Cuándo y cuánto.** «A partir del martes, en aproximadamente uno de cada cuatro expedientes.»
- **Qué mirar.** Concreto: documentación pedida que no encaja, campos mal extraídos, propuestas de importe que chirrían.
- **Dónde decirlo.** Un canal, con el identificador del expediente. Que la respuesta llegue el mismo día, aunque sea para decir «visto, es esperado».

Lo que no se hace: pedirles que evalúen el cambio. Su trabajo es tramitar siniestros. Se les pide que digan cuando algo les parezca raro, que es lo que ya hacen, con un sitio donde decirlo.

Y al terminar, se cierra el círculo: qué se decidió y por qué. Un equipo al que le piden avisos y nunca le cuentan el final deja de avisar.

### 16. El plan de despliegue de un cambio de modelo en Meridiana

Todo lo anterior, junto, para un cambio de versión de modelo en la extracción de FNOL. Es el plan que se pega en el pull request antes de tocar nada.

| Fase | Qué se hace | Puerta para pasar a la siguiente |
|---|---|---|
| 0 · Eval | `--all --check` con la versión nueva | 31 de 31, con el caso de lesiones obligatorio |
| 1 · Sombra | 100 % del tráfico, sin efectos, 5 días | Cero bloqueantes; los graves revisados uno a uno |
| 2 · Canary 5 % | Tráfico real, pegajoso por expediente | 40 casos y ningún criterio de reversión |
| 3 · Canary 25 % | Aviso a los 24 tramitadores | 200 casos y firma de operaciones |
| 4 · Canary 50 % | Una semana natural completa | Métricas dentro de umbral, sin avisos abiertos |
| 5 · 100 % | Bandera al total, versión anterior cargada | Una semana estable antes de retirar nada |

Cuatro cosas que este plan hace y conviene ver:

- **El eval es una puerta, no un informe.** Si falla, no hay fase 1.
- **La sombra va antes que cualquier tráfico real.** Cuesta tokens y no cuesta expedientes.
- **La escalera sube por casos, no por días.** La fase 4 es la excepción, y por un motivo: hace falta una semana entera para que entren todos los días de la semana.
- **La fase 5 no es el final.** Hasta que la versión anterior se retira, el cambio sigue siendo reversible en un minuto.

### 17. Qué cambia de un despliegue de prompt frente a uno de modelo

Los dos son cambios de comportamiento y los dos pasan por el mismo plan. Pero no tienen el mismo perfil de riesgo, y tratarlos igual sale caro en las dos direcciones: o pones un plan de seis fases para arreglar una errata, o le das a un cambio de versión de modelo el mismo trámite que a un ajuste de redacción.

| | Cambio de prompt | Cambio de versión de modelo |
|---|---|---|
| Alcance | Lo que tocaste, más efectos laterales | Todo a la vez, incluido lo que no tocaste |
| Se lee el diff | Sí, palabra a palabra | No hay diff que leer |
| Frecuencia | Semanal o más | Pocas veces al año |
| Quién decide el momento | Tú | Tú, dentro del calendario del proveedor |

De ahí salen dos rutas distintas sobre el mismo mecanismo:

- **Prompt.** Eval completo siempre, sombra corta —un día basta si el diff es acotado—, y canary de dos peldaños. Lo que no cambia: el registro de versión y la bandera. Un prompt desplegado sin versión registrada es un cambio de comportamiento anónimo.
- **Modelo.** El plan de la slide 16 entero, sin recortes.

Y una advertencia sobre lo primero. «Solo he cambiado una frase» es la excusa con la que se cuelan la mitad de las regresiones. El alcance de un cambio de prompt **no** es proporcional al tamaño del diff: añadir una instrucción al final compite por atención con todo lo que ya estaba y puede degradar algo que no mencionaste.

### 18. Congelar cambios durante los picos de siniestralidad

Un episodio de granizo mete 600 siniestros en 24 horas frente a los 88 de un día normal. Ese día no se despliega nada, y no por superstición.

Tres razones que se sostienen solas:

- **No hay atención humana disponible.** Los 24 tramitadores están saturados y no van a detectar que las propuestas salen raras, porque están mirando el volumen. Pierdes el mejor sensor justo cuando más tráfico tienes.
- **Un canary del 25 % en día de pico son 150 casos**, no 22. La misma bandera con el mismo número expone siete veces más expedientes.
- **La reversión con carga es más difícil.** Las colas están llenas, hay expedientes en vuelo por todas partes y la lista de reparación de la slide 13 se multiplica.

Lo que funciona mejor que una regla de calendario: **una congelación disparada por volumen**. Si los siniestros de las últimas cuatro horas proyectan un día por encima de un umbral, el sistema de banderas rechaza cualquier cambio de variante y lo dice.

Con dos excepciones escritas: **revertir nunca está congelado** —el botón de vuelta atrás no se bloquea jamás— y bajar el porcentaje de un canary en curso tampoco. Congelas los cambios que añaden riesgo, no los que lo quitan.

Y cuando pase el pico, se descongela a mano, no automáticamente. Que alguien mire el estado antes de reabrir la puerta.

### 19. Ventanas de despliegue y por qué existen aquí

«No se despliega en viernes» suele ser folclore. En este sistema, la ventana tiene un motivo que se puede explicar en una frase: **el canary necesita testigos**.

Un cambio de comportamiento no lo detecta una alarma. Lo detecta un tramitador leyendo una propuesta que no encaja. Si enciendes el canary a las ocho de la tarde, acumulas una noche entera de expedientes atendidos por la versión nueva sin que nadie los haya mirado. Y con 88 siniestros al día, eso son decenas de casos con los que luego habrá que hacer algo.

La ventana buena para Meridiana es **martes o miércoles por la mañana**, y sale de tres condiciones:

- El equipo de siniestros está trabajando a pleno rendimiento durante las horas siguientes.
- Quien despliega está disponible el resto del día, no a punto de irse.
- Quedan días laborables por delante antes del fin de semana, para el siguiente peldaño de la escalera.

El lunes se descarta por la acumulación del fin de semana; el jueves y el viernes, porque el peldaño siguiente caería en sábado.

Y un matiz sobre la sombra: **la sombra sí se puede encender fuera de ventana**, porque no toca ningún expediente. Es una de las ventajas de tenerla: el trabajo de comparación avanza sin consumir ventanas.

### 20. El eval de preproducción como puerta previa al canary

Antes de cualquier tráfico, `meridiana-agent --all --check` con la configuración nueva. Los 31 siniestros sintéticos, el mismo conjunto de semilla fija que llevas usando desde el curso 2, y el resultado tiene que ser 31 de 31.

Su papel exacto es **puerta**, no garantía. Y conviene tener claras las dos mitades de esa frase.

Lo que la puerta atrapa: el cambio que rompe algo evidente. Un esquema de salida que ya no valida. La extracción que deja de encontrar la matrícula en el caso donde está mal escrita. El intento de inyección que antes se ignoraba y ahora no. El caso con lesiones que deja de derivar. Todo eso sale gratis, en segundos, sin gastar un expediente real.

Lo que la puerta no ve: 31 casos no cubren 32.000 siniestros al año. Un desplazamiento del 3 % en la tasa de derivación es invisible en 31 casos. Por eso hay sombra, y por eso hay canary.

Dos reglas sobre esta puerta:

- **El caso de lesiones es bloqueante y no se negocia.** No hay «31 de 31 salvo ese, que es un caso raro». Si falla ese, el cambio no sale de la rama.
- **El conjunto crece con cada incidente.** Cada regresión detectada en sombra o en canary se convierte en un caso nuevo del conjunto. Así la puerta atrapa la próxima vez lo que esta vez se te escapó.

### 21. Métricas que se miran durante el canary y durante cuánto tiempo

Con el canary encendido hay dos familias de métricas y se comportan de forma distinta.

**Las técnicas**, que ya tienes del bloque de observabilidad y de costes: latencia p95 por caso, tasa de errores de esquema, número de vueltas del bucle, tokens y coste por caso. Estas se leen en horas y su valor está en detectar lo que se rompe rápido.

**Las de comportamiento**, que son las de este bloque y necesitan volumen:

- Tasa de derivación al tramitador, comparada con la de la versión anterior **en la misma ventana**, no contra el histórico.
- Distribución de vías de tramitación.
- Proporción de propuestas de importe que quedan por encima del umbral de 1.500 €, que es la métrica que traduce el cambio a carga de trabajo humana.
- Campos ausentes por expediente en la extracción.
- Y la mejor de todas: la **tasa de corrección del tramitador**, cuántas veces una persona modifica lo que el agente propuso antes de aprobarlo.

La última es la más honesta porque es una evaluación humana que ya estaba ocurriendo y no cuesta nada recoger. Es también la que más tarda en tener volumen.

Sobre la duración, la regla de la slide 8: **casos, no horas**. Y una ventana mínima de siete días naturales antes del 100 %, aunque los casos ya estén, porque el lunes y el viernes no se parecen y la composición del tráfico también es una variable.

### 22. Falsos positivos en la comparación: ruido frente a regresión

La primera semana de canary siempre trae diferencias, y la mayoría no significan nada. Con 4 o 22 casos al día, casi cualquier proporción se mueve varios puntos por azar. Reaccionar a todas quema al equipo y desactiva el mecanismo: a la tercera falsa alarma, nadie mira.

La forma práctica de separarlo es preguntarse **de qué tipo es la métrica**:

- **Eventos con consecuencia grave.** Un siniestro con indicios de lesiones que no se deriva. Aquí `n=1` es señal. No se espera volumen, no se calcula nada: se revierte y se investiga. Que sea raro es exactamente por lo que importa.
- **Proporciones agregadas.** Tasa de derivación, distribución de vías, cruces del umbral. Aquí `n=1` no es nada. Se necesita la ventana entera de casos acordada en la slide 8, y la comparación se hace contra la versión anterior corriendo **a la vez**, no contra el mes pasado.
- **Métricas técnicas continuas.** Latencia, coste. Se leen rápido, pero se leen en percentiles: un caso lento no es una regresión de latencia.

El error que más se ve es comparar contra el histórico. Si el canary corre una semana de mucho volumen y lo comparas con la media del trimestre, la diferencia que ves es la semana, no la versión. El grupo de control tiene que ser el tráfico que atiende la versión anterior en ese mismo momento. Por eso el canary reparte y no sustituye.

### 23. Convivencia de dos versiones de prompt en producción

Durante toda la escalera hay dos comportamientos vivos a la vez. Eso no es un estado excepcional: es el estado normal del sistema durante semanas, y hay cosas que se rompen si estaban escritas asumiendo una sola versión.

Lo que hay que revisar:

- **Las cachés.** Cualquier caché de respuestas o de contexto tiene que llevar la versión del prompt y la del modelo en la clave. Si no, un caso servido por la versión nueva puede recibir una respuesta cacheada de la anterior, y ahí ya no sabes qué estás midiendo.
- **Los ejemplos y el contexto compartido.** Si el prompt nuevo trae ejemplos distintos, no se mezclan.
- **Los cuadros de mando.** Un panel que agrega las dos versiones enseña una media de dos comportamientos, que es un número que no describe a ninguno. Todo panel del periodo se segmenta por variante.
- **La documentación de operaciones.** Si los tramitadores tienen una guía de qué esperar del agente, durante la convivencia hay dos respuestas posibles a la misma pregunta.

Y la regla que evita que esto se pudra: **la convivencia nace con fecha de fin**. Se escribe en el plan, junto a la bandera. Cuando el cambio lleva su semana al 100 %, se retira la variante antigua, se borra la bandera y se limpian los caminos muertos del código.

Dos versiones conviviendo es una fase. Cinco banderas de prompt abiertas desde hace meses es un sistema del que ya nadie sabe qué hace.

### 24. Registrar qué versión atendió cada caso

Es la slide más aburrida del bloque y la que sostiene la mitad de las anteriores. Sin ella no hay comparación, no hay reparación y no hay respuesta a «¿por qué se decidió esto así?».

Lo que se registra en cada expediente y en cada traza, no en los logs de la aplicación:

```sql
ALTER TABLE expediente_decision ADD COLUMN modelo_version   text NOT NULL;
ALTER TABLE expediente_decision ADD COLUMN prompt_version   text NOT NULL;
ALTER TABLE expediente_decision ADD COLUMN prompt_hash      text NOT NULL;
ALTER TABLE expediente_decision ADD COLUMN variante_bandera text NOT NULL;
```

Cuatro campos y un par de detalles que importan:

- **`NOT NULL`.** Un caso sin versión registrada es un caso que no se puede clasificar después, y en una lista de reparación de 150 expedientes esos son justo los que dan trabajo.
- **El hash además de la versión.** La etiqueta `@v7` la pone una persona y las personas se equivocan; el hash del texto del prompt no. Si el hash de un caso no coincide con el del registro para esa versión, alguien desplegó algo fuera del proceso.
- **En el expediente, no solo en la traza.** Las trazas tienen retención corta y los expedientes duran años. La pregunta «con qué versión se decidió esto» puede llegar mucho después.

Con estos cuatro campos, la lista de la slide 13 es una consulta. Sin ellos, es una semana de trabajo manual y una respuesta aproximada.

### 25. Auditoría de despliegues: qué se cambió, cuándo y quién lo aprobó

La slide anterior responde a «con qué versión se atendió este expediente». Esta responde a la otra mitad: «qué versiones han existido, cuándo estuvo activa cada una y quién decidió ponerla».

Es un registro de despliegues, y para artefactos de comportamiento tiene que ser explícito, porque el historial de Git no lo cuenta: en Git ves cuándo se fusionó el cambio, no cuándo la bandera llegó al 50 % ni quién firmó ese peldaño.

Una fila por cambio de estado, con estos campos:

- Artefacto y versión anterior → versión nueva.
- Motivo del cambio, en una frase escrita por una persona.
- Resultado del eval de preproducción y del informe de sombra.
- Fechas y porcentajes de cada peldaño del canary.
- Quién aprobó cada peldaño, con nombre.
- Si hubo reversión: cuándo, por qué criterio y qué reparación se hizo.

Esto no se guarda para nadie en concreto. Se guarda porque es la única forma de contestar preguntas que llegan meses después con un expediente delante, y porque un cambio de comportamiento sin firma es un cambio que nadie recuerda haber decidido.

El curso 4 retoma este registro cuando toque documentar el sistema. Aquí basta con que exista y se rellene sin heroísmos: si rellenarlo cuesta más de dos minutos, se dejará de rellenar.

### 26. Ensayo de reversión: hacerlo antes de necesitarlo

Un procedimiento de reversión que no se ha ejecutado nunca es una hipótesis. La forma de convertirlo en un hecho es barata: **revertir a propósito, en producción, un día tranquilo, con cronómetro**.

El ensayo, media hora:

1. Se avisa a operaciones de que va a haber un ensayo. Es un ensayo, no un simulacro sorpresa: aquí no se está probando a las personas.
2. Se pone un canary real al 5 % con un cambio inocuo —una variante que se comporta igual que la actual—.
3. Alguien que **no** escribió el procedimiento lo ejecuta, siguiéndolo al pie de la letra.
4. Se cronometra desde la decisión hasta el primer caso atendido con la versión anterior.
5. Se saca la lista de expedientes atendidos por la variante y se comprueba que está completa.

Lo que aparece la primera vez, casi siempre, y siempre es lo mismo:

- La persona de guardia no tiene permiso para cambiar la bandera.
- El procedimiento está escrito para alguien que ya sabe hacerlo.
- La bandera tarda más de lo esperado en propagarse porque estaba cacheada.
- La lista de expedientes no se puede sacar porque falta uno de los campos de la slide 24.

Ninguna de esas cuatro se descubre leyendo el documento. Las cuatro se arreglan en una tarde si las encuentras en un ensayo, y ninguna se arregla bien a las tres de la mañana.

### 27. Ejercicio práctico 1: Comparación en sombra sobre los 31 casos {ejercicio:B6-ej1}

Monta la comparación en sombra de `meridiana-agent` sobre el conjunto sintético y clasifica las diferencias por consecuencia, no por texto.

**Qué construyes.** Un modo `--shadow` que, para cada uno de los 31 siniestros, ejecuta el agente dos veces —con la configuración actual y con una configuración alternativa— y escribe un informe con las diferencias clasificadas según la escala de la slide 6: bloqueante, grave, relevante y cosmético.

**Cómo generas la diferencia sin gastar dinero.** No hace falta un modelo distinto. Usa dos variantes del proveedor determinista del repositorio: una es la actual y la otra introduce desviaciones controladas —por ejemplo, dejar ausente un campo que antes se extraía, y mover una propuesta de importe de 1.450 € a 1.550 €—. El objetivo del ejercicio es el comparador, no el modelo.

**Qué tiene que salir.** Un informe corto con el recuento por gravedad y la lista de los graves con el identificador del siniestro. Y una comprobación explícita: si la variante alternativa deja de derivar el caso con lesiones, el informe lo marca como **bloqueante** y el proceso termina con código de salida distinto de cero.

**Dónde se ve si está bien.** La ejecución en sombra no puede haber creado ninguna petición de documentación ni ningún adjunto. Compruébalo contando las llamadas a tools de escritura: tienen que ser cero.

### 28. Ejercicio práctico 2: Plan escrito y ensayo de reversión cronometrado {ejercicio:B6-ej2}

Escribe el plan de despliegue de un cambio de prompt en la extracción de FNOL y ensáyalo de principio a fin, incluida la vuelta atrás.

**Primera parte: el plan.** Una página, con la estructura de la slide 16. Fases con su puerta, criterios de promoción con las cuatro partes de la slide 10 —qué se mide, qué umbral, cuántos casos, quién firma— y criterios de reversión con nombres de quién puede ejecutarla. Se escribe **antes** de mirar ningún dato. Fecha el documento; esa fecha es media nota del ejercicio.

**Segunda parte: la bandera.** Implementa la selección de variante pegajosa por identificador de expediente, con la función de la slide 9. Comprueba dos cosas: que el mismo expediente cae siempre del mismo lado, y que un expediente marcado como excluido de experimentos nunca entra en el canary.

**Tercera parte: el ensayo.** Con el canary al 25 % sobre los 31 casos, ejecuta tu propio procedimiento de reversión con un cronómetro delante. Anota el tiempo desde la decisión hasta el primer caso atendido con la versión anterior, y saca la lista de expedientes que atendió la variante nueva.

**Lo que se entrega.** El plan fechado, el tiempo cronometrado y la lista de expedientes. Y una línea con lo que descubriste que no estaba, porque siempre hay algo.

### 29. Mini-quiz de comprensión — B6 {quiz:B6}

Tres preguntas sobre lo que decide si un cambio de comportamiento sale bien o sale caro: qué se compara antes de promocionar, por qué el reparto aleatorio no vale en este flujo, y qué parte de un despliegue no se deshace bajando una bandera.

Antes de responder, quédate con la idea que las une: en un sistema con LLM, el despliegue no termina cuando el artefacto está en producción. Termina cuando has comprobado, con casos suficientes y con gente mirando, que el comportamiento nuevo es al menos tan bueno como el anterior — o cuando has vuelto atrás y has reparado lo que se escribió por el camino.

Si al responder dudas entre dos opciones, aplica el mismo criterio que aplicarías en Meridiana un martes por la mañana: pregúntate qué consecuencia cambia para el asegurado, para el tramitador o para el expediente. Las opciones que solo hablan de texto, de tokens o de medias del trimestre pasado suelen ser las descartables.

## Qué te llevas

- Fija la versión del modelo o el proveedor desplegará por ti.
- El criterio de reversión se escribe antes del despliegue, no durante el incidente.
- Shadow cuesta el doble en tokens y sale barato la primera vez que evita un fallo.

## Mini-quiz (3 preguntas)

Una sola opción correcta por pregunta, con explicación. Se generan con el mismo formato que `assets/js/quizzes-data.js` para que el importador las recoja.

1. **Tema:** Qué comparar entre shadow y producción para decidir una promoción
   - **Enunciado:** Llevas cinco días de sombra sobre el tráfico de Meridiana. ¿Qué comparación te sirve para decidir si el cambio pasa al canary?
   - **Opciones:**
     - a) El porcentaje de salidas idénticas carácter a carácter entre las dos versiones.
     - b) **Las diferencias clasificadas por consecuencia: derivación por lesiones, cambio de vía y cruce del umbral de 1.500 €, con los casos graves revisados uno a uno.** ✅
     - c) La media de tokens por caso de cada versión.
     - d) El resultado de `--all --check` sobre los 31 casos, que ya se ejecutó antes de la sombra.
   - **Explicación:** Lo que importa es si cambia la consecuencia, no el texto. La (a) mide redacción: en un flujo con texto libre casi nada es idéntico y el número resultante mezcla erratas con derivaciones perdidas. La (c) es una métrica de coste, útil pero incapaz de detectar una regresión de comportamiento. La (d) es la puerta previa, no la comparación: 31 casos no ven un desplazamiento del 3 % sobre 88 siniestros diarios.

2. **Tema:** Por qué un canary aleatorio puede ser inaceptable en un flujo de siniestros
   - **Enunciado:** Propones un canary del 5 % sorteado al azar en cada petición. ¿Cuál es el problema más grave de ese diseño en Meridiana?
   - **Opciones:**
     - a) Que el 5 % de 88 siniestros diarios es demasiado tráfico para una primera fase.
     - b) **Que el sorteo por petición hace que un mismo expediente pueda ser atendido por las dos versiones en momentos distintos, y que no excluye los casos que no se experimentan.** ✅
     - c) Que un canary aleatorio siempre cuesta más tokens que uno segmentado.
     - d) Que impide comparar contra el histórico del trimestre anterior.
   - **Explicación:** La asignación tiene que ser pegajosa por identificador de expediente y respetar las exclusiones: un expediente partido entre dos comportamientos no se puede atribuir ni explicar. La (a) va justo al revés: cuatro casos al día son pocos, no muchos. La (c) es falsa, el coste depende del volumen expuesto, no de cómo se sortea. La (d) confunde el problema con la solución: el control correcto es la versión anterior corriendo a la vez, nunca el histórico.

3. **Tema:** Qué hace irreversible un despliegue y cómo se mitiga
   - **Enunciado:** Tras una semana de canary al 25 % detectas una regresión y bajas la bandera en menos de un minuto. ¿Qué queda por hacer y qué te permite hacerlo?
   - **Opciones:**
     - a) Nada más: bajar la bandera deja el sistema en el estado anterior.
     - b) Volver a ejecutar `--all --check` para confirmar que la versión anterior sigue pasando los 31 casos.
     - c) **Revisar los expedientes que ya atendió la versión nueva —con sus correos, adjuntos y propuestas ya emitidos— usando el registro de `modelo_version` y `prompt_version` de cada caso.** ✅
     - d) Restaurar la copia de seguridad de la base de datos al momento anterior al canary.
   - **Explicación:** La bandera detiene el daño futuro; no rebobina los efectos ya escritos ni los correos ya enviados, que en una semana al 25 % son unos 150 expedientes. Sacar esa lista solo es posible si cada caso registró su versión. La (a) es el error que este bloque intenta desmontar. La (b) es sano pero no repara nada. La (d) destruiría el trabajo legítimo de esa semana: los expedientes atendidos por la versión anterior también están en esa base.

## Lab

Desplegar un cambio de modelo en Meridiana en shadow, comparar y promocionar o revertir con criterios escritos.

**Enunciado.** Partes de `meridiana-agent` con los 31 siniestros sintéticos y su proveedor determinista. Vas a montar el mecanismo completo de despliegue de comportamiento —artefacto versionado, bandera pegajosa, sombra, canary y reversión— y a ejercitarlo dos veces: una con una variante buena, que promociona, y otra con una variante mala, que revierte.

**Pasos:**

1. **Saca el prompt y la versión del modelo del código.** Un fichero de configuración por variante, con `modelo`, `prompt` y el hash del texto del prompt. El código deja de contener el prompt; lo carga de un registro.
2. **Implementa la bandera.** Selección de variante pegajosa por identificador de expediente, con porcentaje configurable y una lista de exclusiones que nunca entran en el experimento.
3. **Añade el modo sombra.** Segunda ejecución sin efectos, con las tools de escritura sustituidas por dobles. El informe clasifica las diferencias en bloqueante, grave, relevante y cosmético.
4. **Escribe el plan, fechado, antes de mirar datos.** Criterios de promoción y de reversión con las cuatro partes de la slide 10 y nombres en la lista de quién revierte.
5. **Registra la versión en cada caso.** `modelo_version`, `prompt_version`, `prompt_hash` y `variante_bandera`, en la decisión del expediente, todos `NOT NULL`.
6. **Ejercita las dos rutas.** Variante A, equivalente en comportamiento: recorre la escalera hasta el 100 %. Variante B, que deja de derivar el caso con lesiones: tiene que ser detenida por el eval, y si la fuerzas hasta el canary, disparar la reversión.
7. **Cronometra la reversión** y saca la lista de expedientes atendidos por la variante retirada.

**Criterios de aceptación:**

- `meridiana-agent --all --check` sigue dando **31 de 31** con la variante actual. Cambiar el mecanismo de despliegue no cambia el comportamiento.
- La ejecución en sombra registra **cero llamadas a tools de escritura**. Se comprueba con un contador, no leyendo el código.
- Un expediente concreto cae **siempre del mismo lado** del canary en ejecuciones repetidas, y un expediente excluido nunca entra.
- La variante B **no pasa la puerta del eval**. El caso con lesiones es bloqueante y el proceso termina con código distinto de cero.
- Tras la reversión, una sola consulta devuelve la lista completa de expedientes atendidos por la variante retirada. Si algún caso tiene la versión sin registrar, el criterio no se cumple.
- El plan está fechado **antes** que el primer informe de comparación. Compruébalo con el historial del repositorio.
- Reproducible en menos de 60 minutos, sin servicios de pago: todo con el proveedor determinista.

**Solución de referencia:** en `content/caso/soluciones/B6/`, con las dos variantes, el informe de sombra de las dos rutas y el plan de despliegue de ejemplo.

## Cierre

- Recapitulación en los tres puntos de arriba.
- Mini-quiz.
- Enlace al siguiente bloque.
