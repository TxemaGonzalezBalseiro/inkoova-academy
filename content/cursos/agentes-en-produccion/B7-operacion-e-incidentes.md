# C-08 · B7 · Operación e incidentes

> Curso: `agentes-en-produccion` · bloque `B7`

## Objetivo

Estar de guardia de un sistema con LLM sin que cada aviso sea una investigación desde cero.

## Guion de slides

28 slides de contenido, dos ejercicios y el mini-quiz. Una idea por slide, con un ejemplo real
o del caso Meridiana. Nada de relleno.

### 1. Qué significa estar de guardia de un agente

Te toca la guardia. Meridiana recibe unos 88 siniestros al día por el portal, la app y el
teléfono transcrito, y el sistema que has construido en los seis bloques anteriores los
atiende. A las 03:10 suena el teléfono.

La buena noticia: el oficio es el de siempre. Contienes antes de entender, escribes lo que
haces mientras lo haces, y a las nueve alguien lee el registro y sabe qué pasó. Nada de eso
cambia porque haya un modelo dentro.

La mala: hay **tres diferencias**, y las tres rompen los hábitos que traes de sistemas
deterministas.

- **Los fallos suelen ser de calidad, no de disponibilidad.** No se cae nada. Todo responde
  con 200. Simplemente las decisiones empeoran, y a veces empeoran durante días.
- **La causa puede estar fuera de tu control.** Tu código no ha cambiado, tu infraestructura
  está sana, y aun así el comportamiento es otro porque el proveedor movió algo por debajo.
- **Reproducir exige haber guardado la ejecución entera.** «Vuelve a lanzarlo» no sirve:
  lanzarlo otra vez puede dar un resultado distinto, y el que te importa es el de hace tres
  semanas.

> Un sistema clásico te avisa cuando se rompe. Un sistema con LLM sigue contestando
> educadamente mientras se equivoca.

Este bloque va de eso: qué alarmas merecen despertar a alguien, qué se hace exactamente
cuando suenan, y cómo se cierra un incidente para que el siguiente sea más corto.

### 2. Los modos de fallo propios: silencioso, caro, plausible y equivocado

Los sistemas de siempre fallan de una manera que ya sabes reconocer: dejan de responder,
devuelven 500, se llenan de latencia. Eso también le pasa a Meridiana y no tiene nada de
especial. Los tres modos que sí son propios de un sistema con LLM:

- **Silencioso.** Todo funciona y las decisiones empeoran. La extracción empieza a devolver
  la matrícula como ausente en el 12 % de los casos en vez de en el 4 %, y como «ausente» es
  una respuesta legítima, ni la validación ni el schema protestan. Solo lo delata una serie
  temporal comparada con su línea base.
- **Caro.** El sistema hace lo correcto gastando el triple. Un bucle que da siete vueltas
  donde daba tres, un contexto que se arrastra, unos reintentos que se disparan. El síntoma
  no aparece en el panel de errores: aparece en la factura, y normalmente el día 3 del mes
  siguiente.
- **Plausible y equivocado.** La salida está bien formada, es coherente, pasa todas las
  validaciones y es falsa. Es el peor, y tiene la slide siguiente entera.

Los tres comparten una propiedad incómoda: **ninguno dispara una excepción**. Las
herramientas que traes de sistemas deterministas están afinadas para detectar cosas que
truenan, y aquí lo que hay que detectar es un cambio de distribución.

Si tu única alarma es «tasa de error 5xx», tienes cubierto el modo de fallo que menos daño
hace en Meridiana.

### 3. El peor: la salida plausible y equivocada

Un asegurado escribe «me dio por detrás en la M-30 el martes». El modelo devuelve
`fecha_siniestro: 2026-03-10`. Es un martes, es reciente, es coherente con el relato y cumple
el schema. Y es el martes anterior al que el asegurado tenía en la cabeza. La póliza entró en
vigor el miércoles 11.

A partir de ahí todo el sistema hace su trabajo bien sobre un dato malo: el código calcula
coberturas para una fecha fuera de vigencia, la vía es la que corresponde a esa situación, y
la petición de documentación que se redacta y se envía es impecable. Ningún componente ha
fallado. No hay excepción, no hay traza en rojo, no hay alerta.

Esto se descubre once días después, cuando el tramitador lo mira con calma, o —peor— cuando
llega la reclamación del asegurado.

Lo que hay que entender: **un valor no es sospechoso por sí mismo**. `2026-03-10` es una
fecha perfectamente válida. Solo es sospechosa comparada con algo: con la fecha de vigencia
de la póliza, con el día de la semana que menciona el texto, con lo que corrigió el
tramitador antes de aprobar.

De ahí la única defensa que funciona en operación: **para cada campo que decide algo, tener
una segunda fuente contra la que contrastar**. Si la fecha extraída cae fuera de la vigencia
de la póliza, no es un dato: es un aviso. Y ese contraste es código determinista, no una
alarma sobre una media.

### 4. Señales que los detectan antes que el cliente

Ninguna métrica dice «el modelo se está equivocando». Lo que hay son **proxies**, y todos
salen de la instrumentación de B2. Los cinco que más avisan en Meridiana:

- **Tasa de derivación.** Es la señal de cabecera. Su banda habitual es el 18 %; cualquier
  cosa sostenida fuera del 12–25 % significa algo, en un sentido o en otro.
- **Tasa de corrección del tramitador.** Qué porcentaje de extracciones se edita antes de
  aprobar, y qué campos. Es la señal más valiosa del sistema y la que casi nadie registra: el
  humano ya está revisando el caso, solo hay que guardar el *diff* entre lo que propuso el
  agente y lo que quedó.
- **Campos ausentes por extracción.** Sube cuando el modelo cambia por debajo o cuando entra
  un canal nuevo con texto distinto.
- **Iteraciones por resolución.** Si el agente necesita más vueltas para lo mismo, algo se ha
  puesto difícil antes de que se ponga imposible.
- **Distribución de importes propuestos.** No la media —que se queda pegada a los 1.850 €—,
  sino la forma: cuántos caen justo por debajo de los 1.500 € del umbral de aprobación
  rápida. Un desplazamiento ahí es una historia.

Las cinco necesitan lo mismo para servir: **una línea base medida en semanas buenas**. Una
métrica sin línea base no es una señal; es un número que alguien mira y no sabe si está bien.

### 5. Runbooks: uno por modo de fallo, no uno genérico

Un runbook genérico —«revisa los logs, mira el panel, avisa al equipo»— es un documento que
tranquiliza a quien lo escribe y no ayuda a quien lo lee a las tres de la mañana. Lo que
funciona es uno por síntoma, con la misma estructura fija de seis apartados:

1. **Síntoma.** Cómo se manifiesta, con el nombre exacto de la alerta que lo dispara.
2. **Confirmar en dos minutos.** La comprobación mínima que distingue esto de otra cosa.
3. **Mitigar.** La acción que para el daño, con el comando o la pantalla concretos.
4. **Verificar.** Qué número tiene que volver a qué banda, y en cuánto tiempo.
5. **Escalar.** A quién, con qué disparador, y qué se le dice.
6. **Registrar.** Qué queda escrito para el postmortem.

Meridiana tiene seis: derivación anómala, coste disparado, proveedor degradado, proveedor que
cambió el modelo, datos personales en trazas y cola atascada. Las cinco slides siguientes y
las dos de más adelante son exactamente esos.

El criterio de calidad no es que estén bien redactados: es que **alguien que no construyó el
sistema los ejecute sin llamarte**. Y eso no se comprueba leyéndolos en una revisión, se
comprueba haciéndole ejecutar uno en un simulacro. Un runbook nunca probado es una hipótesis.

### 6. El runbook de «el agente deriva todo»

**Síntoma.** La tasa de derivación lleva 30 minutos por encima del 45 %, sobre una base del
18 %. La cola de tramitación crece a un ritmo que 24 personas no van a absorber.

**Confirmar en dos minutos.** Dos preguntas, en este orden. ¿Sube el **porcentaje** o solo el
**número absoluto**? Si es el absoluto y el porcentaje aguanta, esto no es un incidente de
calidad: es granizo, y el runbook correcto es el de capacidad. Si es el porcentaje, segmentar
por **motivo de derivación**: no significa lo mismo que crezca «posibles lesiones» que
«extracción incompleta».

**Mitigar.** Depende del motivo. Si crece «extracción incompleta», el sospechoso es un cambio
de prompt o de modelo, y la acción es la reversión de B6. Si crece «posibles lesiones» sin
que haya cambiado nada, sospecha del canal: una plantilla nueva del portal que mete texto
clínico en todos los relatos derivaría todo legítimamente.

**Lo que no se toca, pase lo que pase: la regla de lesiones.** Nadie relaja ese umbral a las
tres de la mañana para descongestionar una cola.

**Verificar.** La tasa vuelve a la banda en 15 minutos.

**Escalar.** A tramitación en cuanto la cola supere lo que el turno de mañana puede absorber.
No es tu decisión cuánta gente entra mañana.

Y una nota que ahorra ansiedad: **derivar de más es el fallo seguro**. Cuesta dinero y
tiempo, y no daña a nadie. No hay prisa por des-derivar; hay prisa por entender.

### 7. El runbook de «el coste se ha triplicado esta noche»

**Confirmar en dos minutos: numerador o denominador.** El coste total dividido entre los casos
atendidos responde solo.

- **Coste por caso estable, más casos.** No es un incidente técnico, es capacidad. Es la
  noche del granizo: 600 siniestros en 24 horas contra los 88 de media. El sistema está
  haciendo su trabajo y la conversación es con negocio, no con el proveedor.
- **Coste por caso disparado.** Ahora sí. Tres sospechosos, en este orden porque es el orden
  en que se comprueban rápido:
  1. **Reintentos.** Un proveedor que devuelve errores intermitentes multiplica llamadas sin
     que nada parezca roto.
  2. **Iteraciones por resolución.** Si el bucle da siete vueltas donde daba tres, algo le
     impide converger: una tool que falla en silencio, un schema que no valida.
  3. **Tokens de entrada por llamada.** El contexto que se arrastra (B1, slide 23). En
     Meridiana, un FNOL por encima de 15.000 tokens ya es señal.

**Mitigar.** Bajar el tope de iteraciones y dejar que el presupuesto por caso de B4 corte. Si
el escalón en la gráfica coincide con un despliegue, revertir primero y diagnosticar después.

**Escalar.** Cuando el gasto proyectado de la noche se coma una parte del presupuesto del mes.
Ese umbral se escribe antes, en euros, no se estima con sueño.

### 8. El runbook de «el proveedor está degradado»

«Degradado» son tres estados distintos con tres respuestas distintas, y confundirlos hace
perder la primera media hora.

- **Caído** (5xx sostenido, 529). Se degrada: el portal acepta el FNOL, guarda el relato en
  crudo y lo marca pendiente de extracción. Lo que estaba en vuelo va a la cola humana. Es la
  slide 17 de B1, ya escrita y ya probada.
- **Limitado** (429). No está roto: te está diciendo que vas demasiado rápido. Bajar
  concurrencia, respetar el `Retry-After`, y priorizar el FNOL nuevo del portal por encima del
  reproceso nocturno. Si reintentas con la misma agresividad, alargas tu propia limitación.
- **Lento** (latencia p95 al doble, sin errores). El más peligroso, porque no dispara ninguna
  alarma de error y va agotando hilos, colas y paciencia en silencio. Los timeouts de B1 hacen
  el trabajo si existen; si el p95 dobla y se sostiene, se degrada igual que si estuviera
  caído.

**Lo que no se hace: subir los timeouts.** Es la reacción instintiva y convierte una
degradación acotada en un agotamiento de recursos.

**Registrar.** La duración exacta y los códigos observados. Eso alimenta el postmortem y, más
adelante, la conversación de contrato con el proveedor. Si su página de estado ya confirma el
problema, no hace falta abrir un incidente propio, pero sí medirlo tú: su versión de cuánto
duró y la tuya no siempre coinciden.

### 9. El interruptor: apagar el agente y volver al flujo manual

El interruptor es un valor de configuración, no un despliegue. Al activarlo, los FNOL nuevos
siguen entrando por el portal, se guardan completos y van directos a la cola de los
tramitadores. El sistema deja de tener modelo dentro y sigue siendo un sistema.

Tres propiedades sin las cuales no cuenta:

- **Efecto en menos de un minuto y sin desplegar.** Si apagar requiere pasar por CI, no tienes
  interruptor: tienes un procedimiento de despliegue con otro nombre. Y el día que lo
  necesites, el despliegue puede ser justo lo que está roto.
- **Lo acciona quien está de guardia, sin pedir permiso.** Si hay que despertar a un
  responsable para autorizarlo, el daño ocurre mientras suena el teléfono. La conversación
  sobre si estuvo bien apagarlo es al día siguiente, con datos, y nunca es una bronca.
- **Es granular.** Por etapa, no todo o nada. Eso es la slide 21.

Y una decisión que se toma **antes**, no durante: qué pasa con lo que está en vuelo. En
Meridiana, un expediente que ya tiene extracción termina su ciclo; uno que no la tiene se
abandona y va a la cola. Cualquiera de las dos es defendible. Lo que no lo es, es
improvisarla.

Cada accionamiento queda registrado con quién, cuándo y por qué. Esa línea es la primera del
postmortem.

### 10. Diseñar el sistema para que apagar el agente sea posible

El interruptor es la parte fácil: son cuatro líneas. La difícil es que **al otro lado haya un
flujo manual vivo**, y eso se atrofia solo.

Tres cosas que hay que mantener deliberadamente:

- **Capacidad.** 24 tramitadores y 88 siniestros al día. Antes del agente los sacaban en 11
  días de media. Pueden absorber un día entero sin agente; no pueden absorber una semana, y
  desde luego no una semana con granizo. Ese número —cuántas horas aguanta el modo manual— es
  un dato de operación que hay que conocer, no una intuición.
- **Pantallas.** Si el tramitador solo tiene una vista donde revisa y aprueba lo que propuso
  el agente, y no puede abrir un expediente y tramitarlo desde cero, has borrado el flujo
  manual sin que nadie lo decidiera. Se borró en una refactorización de UI, hace ocho meses.
- **Conocimiento.** La gente que entró después del agente no ha tramitado nunca sin él.

Por eso la práctica que lo mantiene todo vivo es tan simple como incómoda: **apagar el agente
dos horas al trimestre, en horario laboral, a propósito**. Si esa idea da miedo, ya sabes el
estado real de tu contingencia.

El criterio, entonces, no es que exista el interruptor. Es que **alguien lo haya pulsado en
producción y no haya pasado nada**.

### 11. Alertas: umbral, ventana y a quién despiertan

Una alerta tiene tres campos obligatorios y el tercero es el que decide si sirve. Sin «a quién
despierta» escrito, todas acaban despertando a todo el mundo o a nadie.

| Señal | Condición y ventana | Nivel |
|---|---|---|
| Entrada de FNOL a cero | 5 min | Despierta |
| Caso con lesiones no derivado | 1 caso | Despierta |
| Derivación > 45 % | sostenido 30 min | Despierta |
| Coste por caso × 3 | sostenido 1 h | Turno de mañana |
| p95 del FNOL > 25 s | sostenido 15 min | Turno de mañana |
| Campos ausentes +20 % sobre base | ventana de 6 h | Panel |

La **ventana** es lo que separa una alerta de un generador de ruido. La derivación se dispara
tres minutos cada vez que entra una tanda de siniestros parecidos; a los treinta minutos, ya
no es casualidad.

Y la regla que ordena la columna de la derecha: **solo despierta lo que tiene una acción esta
noche**. Un coste triplicado es grave y no mejora porque lo mires a las cuatro; el canal de
entrada caído deja a los asegurados sin poder declarar y sí. Todo lo demás son las 8:00.

### 12. Fatiga de alertas: la que se silencia deja de existir

Una alerta que alguien silencia dos veces está muerta. Sigue disparándose, sigue apareciendo
en el canal, y ya nadie la lee. Lo peligroso es que sigue *pareciendo* cobertura: en la
revisión de seguridad alguien dirá «eso está alertado», y técnicamente es cierto.

Tres prácticas que lo evitan:

- **Presupuesto de despertares.** Una guardia de una semana no debería sacar a nadie de la
  cama más de una vez. Si saca tres, el trabajo de la semana siguiente es arreglar las
  alertas, por delante de cualquier funcionalidad nueva.
- **Medir accionabilidad.** Cada aviso se cierra marcando «acción tomada» o «sin acción». Toda
  alerta con más de un 30 % de «sin acción» en un mes entra en revisión: se sube el umbral, se
  alarga la ventana o se baja de nivel. Se decide y se escribe; no se silencia en un cliente
  de móvil a las cuatro de la mañana.
- **Vigilar el exceso contrario.** Subir umbrales hasta que ya no salte nada es la otra forma
  de quedarse sin alertas, y esta viene con la sensación agradable de haber arreglado algo.

En Meridiana la alerta de latencia empezó en 12 segundos. Saltó nueve veces en dos semanas,
todas por picos de tarde, todas cerradas sin acción. Pasó a p95 por encima de 25 segundos
sostenido 15 minutos: dos avisos en tres meses, los dos con causa real.

### 13. Triaje de un incidente: las tres primeras preguntas

Antes de tocar nada, tres preguntas. Escritas en el canal del incidente, no pensadas.

1. **¿Desde cuándo?** La hora exacta, no «esta noche». Se saca del escalón en la gráfica, y ese
   escalón suele traer la causa pegada: si empezó a las 02:47 y a las 02:46 hubo un despliegue,
   ya casi has terminado.
2. **¿Qué cambió a esa hora?** Tu registro de despliegues, tus banderas, tu versión de prompt y
   de modelo, y los sistemas de al lado. Si en tu lado no cambió nada, el candidato principal
   pasa a ser el proveedor, y ese es un runbook distinto.
3. **¿Está contenido?** ¿Sigue creciendo el daño? Cuántos expedientes afectados, y —la que de
   verdad importa— **cuántos han llegado ya al asegurado**. Un expediente mal clasificado que
   sigue dentro del sistema se arregla; un correo enviado, no.

El orden entre contener y entender no se negocia: **primero contener**. Apagar el agente no
te quita información: las trazas de B2 siguen ahí y se pueden mirar con calma mañana. Aguantar
una hora sin apagar para «ver si vuelve a pasar» es pagar el diagnóstico con expedientes de
clientes.

Con esas tres respuestas escritas en los primeros diez minutos, cualquiera que se incorpore
al incidente se pone al día leyendo, en vez de preguntando.

### 14. Comunicación durante el incidente, dentro y fuera

Dos públicos, una sola verdad, dos lenguajes.

**Dentro.** Un canal por incidente. Una persona comunica y **no es la que está diagnosticando**:
quien tiene las manos en el sistema no puede además contestar preguntas. Actualización cada
30 minutos aunque el contenido sea «seguimos igual, siguiente actualización a las 04:15». El
silencio de cuarenta minutos produce cinco personas preguntando a la vez justo a quien no debe
interrumpirse.

**A tramitación.** Lo que necesitan es operativo: de qué expedientes no fiarse, qué hacer
mientras (tramitar a mano, y desde qué pantalla), y cuándo se les volverá a avisar. No les
interesa el proveedor ni el modelo.

**Al asegurado.** No se le habla de modelos, de proveedores ni de incidencias técnicas. Se le
habla de su expediente y de sus plazos. Si recibió una comunicación incorrecta, se corrige con
un mensaje específico sobre su caso, no con una nota genérica que multiplica las llamadas.

**Lo que no se improvisa a las tres de la mañana.** Si el incidente toca datos personales, o
afecta a plazos de tramitación, las obligaciones de notificación no se deducen durante la
guardia.

El plazo que más aprieta es conocido y va en el runbook: el artículo 33.1 del RGPD da **72
horas desde que se tuvo constancia** para notificar una violación de datos personales a la
autoridad de control, y el 34.1 obliga a avisar además a los afectados cuando el riesgo para
sus derechos es alto. Lo que importa para la guardia es la palabra *constancia*: el reloj
arranca cuando alguien se da cuenta, no cuando el comité lo confirma. Por eso la escalada a
protección de datos sale el mismo día, con el alcance todavía sin cerrar.

El resto —qué más hay que comunicar, ante qué supervisor sectorial y con qué contenido— lo
fijan la asesoría jurídica y la persona responsable de protección de datos. Esa ficha se
escribe antes y se guarda pegada al runbook.

### 15. Postmortem sin culpables, con acciones

«Sin culpables» no significa «sin responsables». Significa que la pregunta correcta es **por
qué el sistema permitió el fallo**, no quién lo cometió. Cualquier error que una persona
cometió a las tres de la mañana lo va a repetir otra persona a las tres de la mañana.

Contenido mínimo:

- **Línea temporal con horas y enlaces a trazas concretas.** No «sobre las tres empezó a
  fallar»: `02:47`, con el identificador del expediente donde se ve.
- **Impacto en números.** Expedientes afectados, cuántos llegaron al asegurado, euros
  implicados, tiempo de tramitación perdido.
- **Qué funcionó.** El interruptor respondió, la degradación aguantó, la alerta saltó a
  tiempo. Se aprende igual, y evita desmontar lo que sí sirvió.
- **Acciones con dueño y fecha.** Cinco como mucho. Diez acciones son cero acciones.

Acciones prohibidas: «tener más cuidado», «revisar mejor», «formar al equipo». No son
acciones, son deseos, y su única función es cerrar la reunión. La versión válida de un deseo
es un cambio en el sistema: *«las propuestas por encima de 1.500 € muestran los tres campos que
las justifican junto al botón de aprobar»*.

Y una prueba de que el postmortem existió: **cambió algo**. Código, alerta, runbook o eval. Si
no cambió ninguna de las cuatro, hubo una reunión.

### 16. Qué acciones del postmortem son evals nuevos

Regla sin excepciones: **todo incidente de calidad deja un caso nuevo en el conjunto de
evaluación de B3**.

No un caso «parecido»: la entrada exacta que falló, con su salida correcta etiquetada por
quien sabe cuál es. En Meridiana eso significa que la etiqueta la pone un tramitador, no el
ingeniero que investigó el incidente. El conjunto pasa de 31 casos a 32.

Lo que hace valiosos a estos casos es que **la imaginación no los produce**. Nadie sentado a
escribir tests inventa «un FNOL donde el asegurado dice el martes, el modelo elige el martes
anterior y la póliza entra en vigor el miércoles». La producción sí lo produce, una vez, y si
no lo capturas se pierde.

Dos errores frecuentes al añadirlos:

- **Etiquetar con lo que quería el ingeniero.** El caso entra con la respuesta que tramitación
  considera correcta, aunque sea incómoda de conseguir.
- **Añadirlo sin conectarlo al umbral de bloqueo.** Un caso que puede fallar sin impedir el
  despliegue es decoración. Va al conjunto que bloquea, o no va (B3, slide 14).

El efecto se compone: a los dos años, el conjunto de evaluación no protege de fallos
genéricos de LLM. Protege exactamente de lo que a esta compañía le ha pasado.

### 17. SLO realistas para un sistema no determinista

«99 % de acierto» no es un SLO: es un deseo sin instrumento. Para medirlo tendrías que revisar
a mano los 32.000 siniestros del año, y si pudieras hacerlo no necesitarías el agente.

Tres objetivos que sí se pueden sostener delante de un comité:

- **Disponibilidad del canal.** El portal acepta FNOL el 99,9 % del tiempo. Se mide sola, no
  depende del modelo, y es lo que de verdad le importa al asegurado. El **modo degradado cuenta
  como disponible**: el siniestro entró, aunque lo procese un humano.
- **Latencia.** p95 del FNOL síncrono por debajo de 15 segundos. Medible en continuo.
- **Calidad por muestreo.** 50 expedientes revisados por semana contra su etiqueta. Es una
  estimación con margen de error, y hay que presentarla así: «94 % ± 3». Un número de calidad
  sin intervalo de confianza invita a discutir sobre ruido.

Y algo que **no es un SLO**: la regla de lesiones. No admite un 99,9 %, porque no es una
métrica de un componente estadístico: es una regla determinista en código (B1). Su objetivo es
cero, y un caso de lesiones sin derivar no es una violación de objetivo, es un **bug**, con su
corrección y su test.

El presupuesto de error de los dos primeros sirve además para decidir cuándo se congelan
cambios: si te queda poco margen en el mes, no es el momento de promocionar un modelo nuevo.

### 18. El traspaso a operaciones: qué necesita el equipo que no lo construyó

Quien hace la guardia el mes que viene no eres tú. Lo que necesita, y casi nunca recibe:

- **Los seis runbooks, probados por ellos.** No revisados: ejecutados, en un simulacro, con
  las manos.
- **El interruptor, con el permiso ya concedido** y pulsado al menos una vez en un ensayo. Un
  permiso que hay que pedir la primera noche es un permiso que no existe.
- **Un panel de guardia**, distinto del panel de negocio (B2, slide 25). Cinco señales, no
  cuarenta: entrada de FNOL, tasa de derivación, coste por caso, p95 y errores del proveedor.
- **La lista de lo que no deben tocar.** La regla de lesiones, el umbral de 1.500 €, los
  prompts. Escrita, con el motivo al lado.
- **A quién llamar,** con nombre y teléfono: responsable de tramitación, responsable de
  protección de datos, y tú.
- **Una página explicando qué decide el modelo y qué decide el código.** La tabla de la slide
  8 de B1 vale tal cual. Sin ella diagnostican a ciegas y sospechan del modelo cuando la
  decisión la tomó un `if`.

El criterio de traspaso completado: **ejecutan un simulacro entero sin que tú digas nada**. Si
tienes que intervenir, el traspaso no ha ocurrido; se ha anunciado.

### 19. El runbook de «el proveedor cambió el modelo por debajo»

**Síntoma.** Las métricas se mueven sin que tú hayas desplegado nada. La derivación sube dos
puntos, los tokens de salida crecen un 15 %, el formato falla un poco más a menudo. Nada
truena. Es el modo silencioso de la slide 2 con la causa fuera de tu control, y es la razón de
que este bloque exista.

**Confirmar.** Pasar el conjunto de evaluación **congelado**, el mismo de siempre, con el mismo
código de siempre. Si ayer daba 31 de 31 y hoy da 29 de 31 sin que haya cambiado una línea, no
eres tú. Esa comprobación tarda minutos y es, sola, la mitad de la justificación de haber
construido B3.

**Mitigar.** Si tienes la versión de modelo fijada (B6, slide 3), volver a la anterior mientras
dure. Si no la tienes, esta es la noche en que se entiende por qué se fija, y la acción
inmediata pasa a ser bajar de nivel de servicio hasta que puedas evaluar el cambio con calma.

**Verificar.** Reejecutar el conjunto y comprobar que vuelve a su marca.

**Registrar.** Fecha, versiones implicadas y diferencias medidas, caso a caso. Eso es lo que se
le enseña al proveedor, y lo que sostiene el expediente técnico del curso 4 cuando alguien
pregunte por qué el sistema decidió distinto en marzo que en enero.

**Lo que no se hace esa noche: reescribir el prompt para acomodarse al modelo nuevo.** Es un
cambio sin probar, hecho con prisa, sobre el componente más sensible. Va por el camino de B6,
con su shadow y su canary, como cualquier otro.

### 20. El runbook de «hay datos personales en las trazas»

**Síntoma.** Alguien abre una traza para investigar otra cosa y ve el relato completo del
asegurado —nombre, matrícula, referencias médicas— en la herramienta de observabilidad, donde
tiene acceso medio equipo y, si el backend es de terceros, también sale de tu infraestructura.

Orden estricto, y el orden importa:

1. **Contener.** Cortar la exportación al destino externo. Pierdes visibilidad justo cuando la
   quieres; se acepta.
2. **Acotar.** Desde qué despliegue, qué campos, a qué destinos, y quién ha tenido acceso. Los
   atributos de versión de B2 dicen exactamente qué cambio lo introdujo.
3. **Arreglar en origen.** La redacción se hace antes de emitir el span (B2, slide 8), no con
   un filtro en el destino. Filtrar donde ya llegó el dato es limpiar después de la fuga.
4. **Purgar.** Lo ya exportado, en todos los destinos, incluidos índices de búsqueda y copias.
5. **Notificar.** Aquí la guardia no decide: ejecuta. Avisa a protección de datos el mismo día
   —las 72 horas del artículo 33.1 del RGPD cuentan desde que alguien tiene constancia, no
   desde que se confirma el alcance— y sigue la ficha del runbook para lo demás. Quién
   comunica, ante quién y con qué contenido se escribe antes del incidente, no durante.

Y la prevención que cuesta una tarde: **un test que falle si un atributo de traza contiene un
patrón de matrícula, de DNI o de número de póliza**. Los descuidos que fallan en CI dejan de
repetirse; los que dependen de acordarse, no.

### 21. Degradación por etapas: qué se apaga primero

Todo o nada es una mala interfaz para una noche mala. Meridiana tiene cuatro niveles y se bajan
en orden:

- **Nivel 0 · Normal.** Todo el sistema.
- **Nivel 1 · Sin propuesta de resolución.** Es la etapa más cara y la menos urgente: el
  importe puede esperar a mañana y un humano tenía que aprobarlo de todas formas. Se apaga
  primero porque es donde más se ahorra y menos se pierde.
- **Nivel 2 · Sin redacción de peticiones.** El cálculo de qué documentos faltan es
  determinista y sigue funcionando; lo único que se pierde es el texto redactado, que se
  sustituye por una plantilla fija. El asegurado recibe una petición correcta y más seca.
- **Nivel 3 · Sin extracción.** El FNOL entra en crudo y va entero a la cola humana.

Fíjate en el orden: **primero lo que menos duele perder, y la puerta de entrada no se cierra
nunca**. En el nivel 3 no hay triaje automático, así que todo pasa por un tramitador, que es
precisamente el comportamiento seguro: la regla de lesiones no se salta, se vuelve
innecesaria porque no hay decisión automática que tomar.

El nivel actual es visible y queda registrado. «Estamos en nivel 2 desde las 03:14» es una
frase que un responsable de tramitación entiende sin traducción.

### 22. Cola de expedientes atascados y su drenaje

Dos horas en nivel 3 dejan unos 200 expedientes marcados como pendientes de extracción. Volver
a nivel 0 y soltarlos todos de golpe es el segundo incidente de la noche: multiplicas por
varias veces la carga normal sobre el proveedor que acaba de recuperarse, y lo vuelves a tirar.

El drenaje necesita tres decisiones tomadas de antemano:

- **Ritmo.** Un caudal fijo y modesto —por ejemplo 20 expedientes por minuto— en lugar de
  «todos». Es contrapresión, y es exactamente lo mismo que se dimensionó en B4 para el pico de
  granizo.
- **Orden.** No FIFO ciego. Primero los que llevan más tiempo esperando; los duplicados
  detectados, al final o directamente fuera. Y un caso cuyo texto sugiera lesiones no espera
  su turno en el drenaje: va a la cola humana inmediatamente, porque es donde iba a terminar
  de todos modos.
- **Corte automático.** Si durante el drenaje la tasa de error o la latencia vuelven a
  subir, el drenaje se para solo. Un drenaje sin freno propio exige a alguien mirando una
  pantalla a las cuatro de la mañana, y esa persona se va a distraer.

Y una métrica visible mientras dura: cuántos quedan y a qué ritmo bajan. Sin ese número, «está
drenando» es una sensación, y nadie sabe si puede irse a dormir.

### 23. Reprocesar en lote sin duplicar avisos al cliente

Un expediente se puede reprocesar tantas veces como haga falta. Un asegurado no puede recibir
el mismo correo tres veces. La distancia entre las dos frases es un **modo de ejecución
explícito**.

- Las claves de idempotencia de B1 protegen de que la misma petición entre dos veces. **No
  protegen del reproceso deliberado**: eso es una ejecución nueva, con clave nueva, y hará
  todo lo que se le pida.
- Por eso el reproceso corre con las **tools de efecto desactivadas**.
  `crear_peticion_documentacion` y `notificar` no se ejecutan: se registra qué habrían hecho.
  Las lecturas —`consultar_poliza`, `consultar_coberturas`— sí corren normalmente.
- Al terminar, una persona compara lo que el reproceso habría enviado con lo que ya se envió, y
  decide qué se manda de verdad. Suele ser una fracción pequeña, y esa revisión cuesta minutos.

Los 200 expedientes de la slide anterior ya recibieron el acuse del portal. Si el reproceso
vuelve a notificar, son 200 asegurados con dos correos por el mismo siniestro y tramitación
recibiendo 200 llamadas por la mañana: un incidente de calidad convertido en un incidente de
atención al cliente.

**Criterio de aceptación de cualquier herramienta de reproceso:** ejecutarla dos veces seguidas
sobre el mismo expediente no cambia nada fuera del sistema. Si no puedes afirmarlo con un test,
no la lanzas en producción.

### 24. Guardias sostenibles: rotación y carga real

La guardia se mide igual que el sistema, y sus métricas se revisan igual de en serio.

- **Avisos por turno, y cuántos fuera de horario.** Si la media de una semana pasa de dos
  despertares, el trabajo prioritario de la semana siguiente es arreglar eso, por delante de
  cualquier funcionalidad. Una guardia ruidosa produce gente que no mira las alertas, y eso es
  peor que no tenerlas.
- **Cuánta gente hay en la rotación.** Con tres personas, a cada una le toca una semana de cada
  tres, y quema en seis meses. Con seis es sostenible. Si no hay seis, la respuesta no es
  apretar: es reducir la superficie que exige guardia, bajando el nivel de la mitad de las
  alertas al turno de mañana.
- **Qué horario tiene sentido de verdad.** Meridiana no es un sistema crítico 24/7. Los
  siniestros entran de noche, pero se tramitan de día: un expediente que espera hasta las 8:00
  no daña a nadie. Lo único que justifica despertar es que el canal de entrada esté caído o que
  un caso con lesiones no se haya derivado.

Dicho al revés: **si tu sistema con LLM necesita a alguien despierto todas las noches, el
problema no es la rotación. Es que el sistema no sabe degradar.**

### 25. Qué se escala a negocio y en qué momento

Escalar no es pedir ayuda técnica. Es que alguien con autoridad decida algo que tú no puedes
decidir.

Cuatro disparadores, y basta con uno:

- **Un asegurado ha recibido algo incorrecto.** En cuanto el error sale de tus sistemas, deja
  de ser un problema técnico.
- **La cola humana va a desbordar.** Cuántos tramitadores entran mañana y en qué orden se
  atienden 600 expedientes lo decide tramitación, no la guardia.
- **Hay dinero en juego más allá de la factura de tokens.** Propuestas de importe calculadas
  sobre datos malos, o aprobaciones ya firmadas sobre esas propuestas.
- **La regla de lesiones ha fallado.** Se escala siempre. Un solo caso, aunque ya esté
  corregido, aunque sean las cuatro de la mañana.

**El momento es en cuanto se cumple el disparador, no cuando tengas el diagnóstico.** Negocio no
necesita saber por qué: necesita saber que mañana hay que llamar a cuarenta asegurados y que
convendría tener a alguien preparado para hacerlo.

Y lo que se escala son cuatro líneas: qué pasa, a cuántos afecta, qué hemos hecho, qué decisión
necesitamos. Si tardas media hora en escribirlas, es que todavía no sabes lo primero, y eso
también es información que hay que transmitir.

### 26. Ejercicios de caos aplicados a un agente

Los ensayos clásicos —matar un proceso, cortar la base de datos— siguen siendo necesarios y no
cambian aquí. Los que son propios de un sistema con LLM son cuatro, y ninguno se prueba solo:

- **El proveedor devuelve 529 durante once minutos.** ¿El portal sigue aceptando FNOL? ¿Los
  expedientes en vuelo acaban en la cola humana o se quedan a medias?
- **El proveedor responde en 40 segundos en vez de en 8.** ¿Saltan los timeouts de B1 o se van
  agotando los hilos hasta que el gateway deja de contestar?
- **El modelo devuelve algo que cumple el schema y es absurdo:** una fecha de 2019, un importe
  de 400.000 €. ¿Lo para una validación de rango o llega al expediente?
- **Una tool falla siempre.** ¿El bucle reintenta indefinidamente quemando presupuesto, o corta
  y deriva?

Se hacen **en horario laboral, anunciados, con el interruptor a mano** y con una hipótesis
escrita antes: «creemos que pasará X». La mitad del valor está en descubrir que pasa Y, y esa
mitad se pierde si no escribiste la hipótesis.

Y el ensayo que siempre se pospone: **apagar el agente entero y dejar que tramitación trabaje
dos horas sin él**. Es el único que valida de verdad la slide 10, y el único cuyo resultado
interesa a alguien fuera del equipo técnico.

### 27. Los indicadores que anticipan un incidente por horas

Las alertas de la slide 11 avisan cuando el problema ya está pasando. Estos otros se mueven
antes, y por eso su sitio es un panel que se mira por la mañana con un café, no una alarma:

- **Iteraciones por resolución.** De 3,1 a 3,6 de media significa que el agente está teniendo
  más dificultad con el mismo trabajo. Es la métrica rara de B2 y la más predictiva de todas.
- **Tokens de entrada por caso.** Suben sin que nadie haya tocado el prompt: algo se está
  arrastrando entre iteraciones y todavía no duele.
- **Tasa de reintentos al proveedor.** Del 0,4 % al 3 % en una tarde es, muy a menudo, un
  proveedor que va a degradarse esta noche.
- **Campos ausentes por extracción.** Sube cuando el modelo cambia por debajo, y también
  cuando entra un canal nuevo cuyo texto no se parece al que había.
- **Tiempo del tramitador hasta aprobar.** Si crece, las propuestas son peores. El humano lo
  nota mucho antes que cualquier métrica automática, y ese conocimiento se pierde si no se
  mide.

Ninguno justifica despertar a nadie. Todos justifican mirar antes de que sea urgente, y varios
permiten llegar a la reunión con el proveedor con una serie temporal en la mano en vez de con
una impresión.

### 28. Cerrar el círculo: del incidente al eval y al runbook

Un incidente bien cerrado deja cuatro cosas, y las cuatro viven en algo que ya construiste:

1. **Un caso nuevo en el conjunto de evaluación**, etiquetado por tramitación.
2. **Una traza que se puede reproducir**, porque sin poder reconstruir la ejecución de hace tres
   semanas no hay causa: hay teorías.
3. **Un runbook nuevo o corregido**, probado por quien hará la próxima guardia.
4. A veces, **una alerta menos**: las que no sirvieron durante el incidente son ruido
   demostrado, y ahora tienes la prueba.

Y así se lee el curso entero hacia atrás. **B1** puso las capas que permiten apagar una etapa
sin apagar el sistema, y sacó las reglas del prompt para que no cambien solas. **B2** dio la
traza sin la cual no hay reproducción. **B3**, el conjunto de evaluación que distingue «cambió
el proveedor» de «la hemos liado nosotros». **B4**, el presupuesto que corta el coste antes de
que llegue la factura. **B5**, el diseño que hace que una instrucción inyectada no consiga
nada. **B6**, la reversión en un minuto que convierte media noche en cinco minutos.

Este bloque no ha añadido ninguna pieza nueva: ha usado las seis anteriores a las tres de la
mañana.

> Un sistema en producción no es el que funciona el día del despliegue. Es el que otra persona
> puede sostener el martes siguiente sin llamarte.

### 29. Ejercicio práctico 1: escribir un runbook y probarlo con otra persona {ejercicio:B7-ej1}

Escribe el runbook de **«el coste por caso se ha triplicado»** para Meridiana, con los seis
apartados de la slide 5, y después pásaselo a alguien que no lo haya escrito.

**Qué entregar.** Un documento de una página con: síntoma y nombre exacto de la alerta;
confirmación en dos minutos (los comandos o consultas literales que distinguen numerador de
denominador); mitigación con la acción concreta; verificación con el número y la banda a la que
tiene que volver; disparador de escalado en euros; y qué queda registrado.

**Cómo se prueba.** Otra persona lo ejecuta contra el escenario que tú prepares en
`meridiana-agent`: fuerza un bucle que dé siete iteraciones en vez de tres —por ejemplo, con una
tool que devuelva error las cuatro primeras veces— y déjale el panel y el runbook. No hables.

**Criterios de aceptación:**

- Llega a la mitigación correcta **sin preguntarte nada**. Cada pregunta que te haga es una
  línea que falta en el runbook; anótala y añádela.
- Tarda menos de **diez minutos** desde que lee el síntoma hasta que aplica la mitigación.
- El apartado de escalado contiene una cifra en euros, no «si es mucho».
- Ninguna instrucción dice «revisa los logs» sin decir cuáles y qué buscar en ellos.

### 30. Ejercicio práctico 2: decidir a quién despierta cada alerta {ejercicio:B7-ej2}

Clasifica estas ocho señales de Meridiana en tres niveles —**despierta**, **turno de mañana**,
**solo panel**— y asigna a cada una un umbral y una ventana concretos. Justifica cada decisión
con una sola frase que responda a «¿qué acción hay esta noche?».

1. El portal no ha registrado ningún FNOL en los últimos 8 minutos.
2. Un expediente con indicios de lesiones se ha resuelto sin derivar.
3. La tasa de derivación lleva 40 minutos en el 51 %.
4. El coste por caso está en 3,2 veces la media de los últimos 30 días.
5. El p95 del FNOL síncrono ha pasado de 9 a 22 segundos.
6. El proveedor devuelve 429 en el 8 % de las llamadas.
7. Las iteraciones medias por resolución han pasado de 3,1 a 3,7 en dos días.
8. El 40 % de las extracciones de hoy tienen la matrícula ausente, frente a un 12 % habitual.

**Criterios de aceptación:**

- Como mucho **tres** señales en «despierta». Si tienes cuatro o más, relee la slide 11.
- Cada una de las ocho tiene ventana, no solo umbral.
- La 2 está en «despierta» y su justificación menciona que es un fallo de una regla
  determinista, no una desviación estadística.
- Para cada señal en «solo panel», escribes qué otra señal saltaría si el problema empeora. Si
  no existe esa segunda señal, tienes un hueco de cobertura: anótalo.

### 31. Mini-quiz de comprensión — B7 {quiz:B7}

Tres preguntas sobre lo que decide una guardia: qué señal mirar primero cuando el agente
empieza a derivar de más, qué convierte el interruptor en un plan real y no en un botón, y qué
objetivo de servicio se puede defender delante de un comité sin prometer lo que no se puede
medir.

## Qué te llevas

- El fallo peligroso no es el que rompe: es el que responde bien y se equivoca.
- Si no puedes apagar el agente y seguir operando, no tienes plan de contingencia.
- Un postmortem sin un eval nuevo suele ser un postmortem sin aprendizaje.

## Mini-quiz (3 preguntas)

Una sola opción correcta por pregunta, con explicación. Se generan con el mismo formato que
`assets/js/quizzes-data.js` para que el importador las recoja.

1. **Tema:** Qué señal delata que el agente empezó a derivar de más
   - **Enunciado:** A las 03:10 la tasa de derivación de Meridiana lleva 40 minutos en el 52 %, cuando su banda habitual es el 18 %. ¿Qué comprobación distingue mejor un incidente de calidad de un aumento normal de volumen?
   - **Opciones:**
     - a) Comparar el número absoluto de derivaciones con el de ayer a la misma hora.
     - b) **Segmentar el porcentaje de derivación por motivo y comparar cada motivo con su línea base.** ✅
     - c) Revisar los logs de error del orquestador buscando excepciones nuevas.
     - d) Ejecutar el conjunto de evaluación completo antes de tocar nada.
   - **Explicación:** El porcentaje aísla el volumen, y el motivo dice si el problema es de extracción o de una regla que se está disparando legítimamente. La (a) engaña: en la noche del granizo el absoluto se multiplica sin que nada vaya mal. La (c) busca el modo de fallo equivocado: una derivación excesiva no genera excepciones, el sistema está funcionando. La (d) es útil y es el paso siguiente cuando se sospecha del proveedor, pero tarda y no contiene nada.

2. **Tema:** Qué hace falta para que apagar el agente sea una opción real
   - **Enunciado:** ¿Cuál de estas condiciones convierte el interruptor de apagado en un plan de contingencia y no en un botón decorativo?
   - **Opciones:**
     - a) Que accionarlo requiera la aprobación del responsable técnico, para evitar apagados por error.
     - b) Que el apagado se haga desplegando con la bandera cambiada, para que quede rastro en el repositorio.
     - c) **Que el flujo manual esté vivo —pantallas, capacidad y gente que sepa usarlo— y que se haya apagado a propósito en producción al menos una vez.** ✅
     - d) Que apague el sistema entero de golpe, para no dejar ninguna etapa en un estado ambiguo.
   - **Explicación:** El interruptor solo vale lo que valga el flujo al que devuelve el trabajo, y eso se atrofia salvo que se ensaye. La (a) mete una llamada de teléfono entre el daño y su contención. La (b) hace depender el apagado del despliegue, que puede ser justo lo que está roto y que además tarda. La (d) tira la degradación por etapas y cierra la puerta de entrada, que es lo único que nunca se apaga.

3. **Tema:** Cuál es un SLO honesto para un sistema con LLM
   - **Enunciado:** El comité pide un SLO para el agente de Meridiana. ¿Cuál es defendible?
   - **Opciones:**
     - a) 99 % de extracciones correctas, medido sobre el total de siniestros del mes.
     - b) **99,9 % de disponibilidad del canal de FNOL contando el modo degradado como disponible, p95 por debajo de 15 s, y calidad estimada por muestreo semanal presentada con su margen de error.** ✅
     - c) Cero errores del modelo, dado que hay lesiones de por medio.
     - d) 95 % de casos resueltos sin derivación, para demostrar el ahorro.
   - **Explicación:** La (b) mide en continuo lo que se puede medir en continuo y es explícita sobre lo que solo se puede estimar. La (a) exigiría revisar a mano 32.000 casos al año. La (c) confunde dos cosas: donde el fallo es inaceptable —las lesiones— la garantía la da una regla determinista en código, no un objetivo estadístico. La (d) convierte una métrica de negocio en objetivo del sistema e incentiva justo lo contrario de lo que protege al asegurado: derivar menos.

## Lab

Escribir los runbooks de Meridiana y ejecutar un simulacro de incidente con el interruptor de apagado.

**Enunciado.** Partes de `meridiana-agent` con la arquitectura de B1 y la instrumentación de B2.
Al terminar tendrás tres runbooks probados, un interruptor con cuatro niveles y el registro de
un simulacro completo hecho por otra persona.

**Pasos:**

1. **Implementa el interruptor por niveles** de la slide 21 (0 a 3) como configuración leída en
   caliente, no como constante. Cada cambio de nivel escribe una línea con quién, cuándo y por
   qué. En nivel 3 el FNOL entra, se guarda y se marca `pendiente_de_extraccion`.
2. **Escribe tres runbooks** con los seis apartados de la slide 5: derivación anómala, coste
   disparado y proveedor degradado. Una página cada uno, con comandos literales.
3. **Prepara tres escenarios reproducibles** en el doble de `ILlmClient`: uno que devuelva
   extracciones incompletas (dispara derivación), uno que falle las cuatro primeras llamadas
   (dispara reintentos y coste) y uno que responda 529 durante once minutos simulados.
4. **Ejecuta el simulacro con otra persona**, que solo tiene los runbooks y el panel. Tú lanzas
   los escenarios en orden aleatorio y no hablas. Cronometra desde el síntoma hasta la
   mitigación.
5. **Escribe el postmortem del simulacro** con el formato de la slide 15: línea temporal, qué
   funcionó, y como máximo cinco acciones con dueño y fecha.
6. **Convierte el fallo en eval.** Coge el caso que peor se comportó en el escenario 1, etiqueta
   la salida correcta y añádelo al conjunto de B3. Pasa de 31 casos a 32.

**Criterios de aceptación:**

- La otra persona **llega a la mitigación correcta en los tres escenarios sin preguntarte nada**.
  Cada pregunta que te haga se convierte en una línea nueva del runbook.
- El nivel 3 se activa y se desactiva **en menos de un minuto y sin desplegar**, y el sistema
  sigue aceptando FNOL durante todo el ensayo: cero rechazos en el canal de entrada.
- El drenaje de los expedientes acumulados en nivel 3 respeta un caudal máximo configurado y se
  puede parar a mitad. Al reprocesarlos, `crear_peticion_documentacion` y `notificar` **no se
  ejecutan**: se registra lo que habrían hecho.
- Ejecutar el reproceso **dos veces seguidas** sobre el mismo expediente no produce ningún efecto
  externo adicional. Hay un test que lo comprueba.
- `meridiana-agent --all --check` da **32 de 32** con el caso nuevo incorporado, y falla si se
  revierte el arreglo.
- Reproducible en menos de 60 minutos, sin servicios de pago.

**Solución de referencia:** en `content/caso/soluciones/B7/`, con los tres runbooks escritos, los
escenarios del doble de `ILlmClient` y el registro de un simulacro de ejemplo.

## Cierre

- Recapitulación en los tres puntos de arriba.
- Mini-quiz.
- Enlace al siguiente bloque.
