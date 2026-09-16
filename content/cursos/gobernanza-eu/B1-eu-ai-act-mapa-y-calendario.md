# C-09 · B1 · EU AI Act: mapa y calendario

> Curso: `gobernanza-eu` · bloque `B1`

## Objetivo

Situarse en el reglamento: qué regula, a quién, desde cuándo y con qué consecuencias.

## ⚠ Bloque normativo: verificación obligatoria antes de publicar

Cada afirmación de este bloque debe citar artículo y fecha de verificación, y esa fecha debe salir de haber abierto la fuente primaria. No se redacta de memoria ni a partir de resúmenes de terceros.

**Fuentes primarias a consultar:**

- [x] Reglamento (UE) 2024/1689 (AI Act) — identidad, fecha, entrada en vigor y numeración de los artículos 5, 6 y 113, comprobados en EUR-Lex el 31/08/2026
- [x] **Reglamento (UE) 2026/1744, de 8 de julio de 2026** — modifica el AI Act y **cambia el calendario**. Sin esto, todas las fechas del bloque estarían mal
- [x] Texto consolidado (CELEX 02024R1689-20260727): artículo 113 entero y Anexos I y III leídos el 31/08/2026. Del Anexo IV solo se ha verificado el título; su contenido se abre en B4
- [ ] DOUE: correcciones de errores posteriores
- [ ] Comisión Europea: directrices y calendario oficial de aplicación
- [ ] AEPD y autoridad nacional designada: criterios publicados

**Fecha de verificación parcial:** 2026-08-31. Lo cerrado está en
`content/cursos/gobernanza-eu/VERIFICADO.md`, con artículo, URL y cita literal por dato.
Lo que no se pudo comprobar se dice en su sitio, con la fuente que hay que abrir.

> Aviso al alumno que debe aparecer en el bloque: este material es formativo y no constituye asesoramiento jurídico.

## Guion de slides

27 slides de contenido. Una idea por slide, con un ejemplo real o del caso Meridiana. Nada de relleno.

### 1. Qué es el AI Act y qué no es: un reglamento sobre cómo se pone algo en el mercado

El Reglamento (UE) 2024/1689 no es una ley sobre inteligencia artificial en abstracto. Es una norma que dice **bajo qué condiciones un sistema de IA puede ponerse en el mercado europeo o usarse dentro de la Unión**, y qué tiene que poder demostrar quien lo pone ahí.

Eso descarta tres lecturas frecuentes y equivocadas:

- **No es un código ético.** No dice si está bien automatizar el triaje de siniestros. Dice qué controles necesitas si lo haces.
- **No prohíbe la IA en decisiones importantes.** La mayor parte del reglamento describe cómo hacerlo con obligaciones encima, no cómo evitarlo.
- **No sustituye a nada.** Se suma al RGPD, a la normativa sectorial de seguros y a la de defensa del consumidor. Cumplir uno no exime de los otros.

La consecuencia práctica para Meridiana es incómoda y conviene decirla ya: la pregunta «¿podemos usar el agente de siniestros?» casi nunca tiene por respuesta «no». Tiene por respuesta «sí, si eres capaz de enseñar esto, esto y esto». El trabajo de este curso es convertir ese «esto» en artefactos concretos que ya produce el sistema técnico que construiste en el curso 3: trazas, evals, registros de despliegue.

Un curso de gobernanza que solo enseñe a decir que no es un curso que nadie usará el segundo mes.

### 2. Por qué es un reglamento de producto y no de datos: la lógica del marcado CE

El AI Act está escrito con la gramática de la legislación europea de **productos**: la misma que regula un ascensor, un juguete o un producto sanitario. Esa elección explica casi todo lo demás.

En la legislación de producto, la lógica es siempre la misma:

1. Alguien **fabrica** algo y lo pone en el mercado.
2. Ese alguien declara que cumple unos requisitos esenciales.
3. Guarda un **expediente** que demuestre la declaración.
4. Una autoridad puede pedirle ese expediente y, si no aparece, sancionar.

Nada de eso mira los datos personales de nadie. Mira si el objeto es seguro y si su fabricante puede probarlo.

Trasladado a software esto choca con la intuición del equipo técnico. Un modelo no es un ascensor: se actualiza cada semana, se comporta distinto con la misma entrada y no tiene número de serie. El reglamento resuelve esa fricción exigiendo que **el sistema esté descrito, versionado y probado** con la misma seriedad que una pieza física.

Ahí es donde el curso 3 se paga solo. Si tu agente de Meridiana tiene versiones de prompt en un repositorio, un conjunto de evals que corre en CI y trazas que reconstruyen una decisión concreta, ya tienes la mitad del expediente. Si tu agente vive en un cuaderno, no tienes producto: tienes una demo que no se puede declarar conforme.

### 3. Relación con el RGPD: qué añade y qué no sustituye

RGPD y AI Act se solapan en el mismo sistema y responden a preguntas distintas. Confundirlos es el error más caro de este curso, porque lleva a equipos a creer que su evaluación de impacto de protección de datos ya cubre el AI Act. No lo hace.

La diferencia en una línea:

- **RGPD:** ¿puedes tratar estos datos personales, con qué base y con qué derechos para la persona?
- **AI Act:** ¿es este sistema apto para ponerse en el mercado y usarse, con qué controles y con qué documentación?

Un sistema puede cumplir uno y suspender el otro. Meridiana puede tener base jurídica impecable para tratar los datos del siniestro —ejecución del contrato de seguro— y aun así incumplir el AI Act por no tener documentación técnica ni supervisión humana descrita.

Y al revés: puedes tener un expediente técnico ejemplar y estar tratando datos de salud del lesionado sin haber pensado en la categoría especial a la que pertenecen.

Lo que sí conviene hacer es **reutilizar**, no duplicar. El inventario de tratamientos, el análisis de riesgos y el registro de proveedores del RGPD son insumos del expediente del AI Act. Dos carpetas separadas que dicen cosas distintas del mismo sistema es la señal más fiable de que ninguna de las dos se mantiene.

La evaluación de impacto relativa a los derechos fundamentales es el **artículo 27**, y no se exige a todo el mundo. El apartado 1 la impone, antes de desplegar un sistema de alto riesgo del artículo 6, apartado 2, a los responsables del despliegue «que sean organismos de Derecho público, o entidades privadas que prestan servicios públicos», y a los responsables del despliegue de los sistemas del **Anexo III, punto 5, letras b) y c)** —solvencia y calificación crediticia, y evaluación de riesgos y fijación de precios en seguros de vida y de salud—, con exclusión del Anexo III, punto 2. La articulación con el RGPD está en el **artículo 27, apartado 4**, en su redacción dada por el Reglamento (UE) 2026/1744: cuando la evaluación de impacto del artículo 35 del RGPD ya cumpla alguna de estas obligaciones, el responsable del despliegue podrá «incluir referencias cruzadas a las secciones pertinentes de dicha evaluación de impacto relativa a la protección de datos o incluir las partes pertinentes de esta». Se reutiliza; no se sustituye. Verificado en el texto consolidado de EUR-Lex el 31/08/2026.

> Fuentes primarias a abrir: Reglamento (UE) 2024/1689 en EUR-Lex (capítulo de obligaciones de los responsables del despliegue); Reglamento (UE) 2016/679; guías publicadas por la AEPD sobre IA y protección de datos.

### 4. Ámbito de aplicación: territorial y material, o por qué no basta con no estar en Europa

Dos preguntas ordenan todo el ámbito, y hay que responderlas por escrito antes de clasificar nada.

**¿Estoy dentro territorialmente?** El reglamento sigue la lógica del mercado interior: lo que importa no es dónde está tu empresa, sino **dónde se pone el sistema en el mercado y dónde se usa su resultado**. Un proveedor de fuera de la Unión que vende a Meridiana está dentro. Un modelo alojado en otro continente cuyo resultado se usa en Madrid está dentro. La sede social no es un escudo.

**¿Estoy dentro materialmente?** Es decir: ¿lo que tengo delante es un «sistema de IA» según la definición del reglamento y no cae en una exclusión? Esa es la slide siguiente.

Para Meridiana la respuesta territorial es trivial —aseguradora española, asegurados españoles— pero la cadena de suministro no lo es. El proveedor del modelo, el proveedor del almacén vectorial y el de la plataforma de observabilidad pueden estar en tres jurisdicciones. Que ellos estén dentro del ámbito no te libra a ti: **el reglamento reparte obligaciones entre varios papeles a la vez**, y ese reparto es el bloque B3.

El error clásico: dar por hecho que «el modelo es del proveedor, luego el problema es suyo». Meridiana integra, decide el uso y pone su marca delante del asegurado. Eso tiene consecuencias propias.

El ámbito es el **artículo 2**. Su apartado 1 alcanza a los proveedores que introduzcan en el mercado o pongan en servicio sistemas de IA en la Unión «con independencia de si dichos proveedores están establecidos o ubicados en la Unión o en un tercer país» [letra a)]; a los responsables del despliegue «que estén establecidos o ubicados en la Unión» [letra b)]; y a proveedores y responsables del despliegue de un tercer país «cuando los resultados de salida generados por el sistema de IA se utilicen en la Unión» [letra c)]. Las letras d) a g) añaden importadores y distribuidores, fabricantes de productos que incorporen un sistema de IA con su propio nombre o marca, representantes autorizados de proveedores no establecidos en la Unión y las personas afectadas ubicadas en la Unión. La sede social, en efecto, no aparece por ninguna parte. Verificado en el texto consolidado de EUR-Lex el 31/08/2026.

> Fuentes primarias a abrir: Reglamento (UE) 2024/1689, capítulo I (ámbito de aplicación y definiciones), en EUR-Lex; directrices de la Comisión Europea sobre ámbito, si publicadas.

### 5. Definición de sistema de IA y por qué la definición importa más de lo que parece

Toda la norma cuelga de una definición. Si tu software no encaja en ella, nada de lo demás te aplica; si encaja, empieza el árbol de decisión. Por eso la definición es lo primero que discuten los abogados y lo último que mira el equipo técnico.

La discusión real no es «¿esto es IA?» en el sentido de marketing. Es si el sistema **infiere** resultados a partir de entradas con cierto grado de autonomía, en lugar de limitarse a ejecutar reglas escritas por una persona.

Esa frontera es exactamente la que dibujaste en el curso 3 con la regla *deterministic-first*. Recuerda la tabla del bloque de arquitectura de Meridiana: el modelo **extrae** los campos del relato y el código **decide** la vía. Eso no te saca del ámbito —el componente de extracción sigue infiriendo— pero cambia la superficie regulada: cuanta más lógica esté en reglas auditables, menos depende tu expediente de justificar el comportamiento de un modelo.

Un aviso contra la tentación: **no diseñes para escaparte de la definición**. Convertir un modelo en una tabla de decisiones para poder decir «esto no es IA» funciona una vez ante un auditor ingenuo, y falla el día que alguien lee el código. La estrategia sostenible es la contraria: entrar en el ámbito y tener el expediente en orden.

La definición está en el **artículo 3, punto 1**, y su redacción vigente procede de una **corrección de errores** publicada en el DOUE, no del texto de julio de 2024 —motivo suficiente para no citarla de memoria—: «un sistema basado en una máquina que está diseñado para funcionar con distintos niveles de autonomía y que pueda mostrar capacidad de adaptación tras el despliegue, y que, para objetivos explícitos o implícitos, infiere de la información de entrada que recibe la manera de generar resultados de salida, como predicciones, contenidos, recomendaciones o decisiones, que pueden influir en entornos físicos o virtuales». Las tres palabras que hacen el trabajo son *autonomía*, *infiere* y *resultados de salida*. Verificado en el texto consolidado de EUR-Lex el 31/08/2026.

> Fuentes primarias a abrir: Reglamento (UE) 2024/1689, artículo de definiciones y considerandos asociados, en EUR-Lex; directrices de la Comisión sobre la definición de sistema de IA, si publicadas.

### 6. Los cuatro niveles de riesgo, en un vistazo: lo que cambia entre ellos

El reglamento organiza las obligaciones por niveles de riesgo. La forma útil de memorizarlos no es por su nombre, sino por **qué te obliga a hacer cada uno**:

- **Riesgo inaceptable.** No se puede. No hay expediente que lo arregle ni consentimiento que lo salve.
- **Alto riesgo.** Se puede, con un aparato completo encima: gestión de riesgos, gobierno de datos, documentación técnica, registros, supervisión humana, precisión y robustez, y la conformidad correspondiente.
- **Riesgo limitado.** Se puede, con obligaciones de **transparencia**: que la persona sepa que está ante una máquina o ante contenido generado.
- **Riesgo mínimo.** Se puede, sin obligaciones específicas del reglamento. Lo que no significa sin obligaciones: el RGPD y la normativa sectorial siguen ahí.

Dos ideas que hay que fijar ahora porque se malinterpretan durante todo el curso.

La primera: **la categoría no la elige quien construye**. La determina el uso previsto y el contexto. El mismo modelo de extracción de texto puede ser mínimo en un buscador interno y alto riesgo en un proceso que decide sobre el acceso de alguien a una prestación.

La segunda: los niveles **no son excluyentes en un sistema compuesto**. Meridiana puede tener un componente de transcripción de riesgo mínimo, un chat con el asegurado con obligaciones de transparencia y un componente de triaje cuya clasificación es precisamente lo que se discute en B2.

Una advertencia sobre los nombres, comprobada leyendo el índice del texto consolidado el 31/08/2026: **«riesgo limitado» y «riesgo mínimo» no son denominaciones del reglamento**. Son atajos didácticos, útiles para explicar y peligrosos para citar. Lo que el texto tiene son capítulos: el **capítulo II, «Prácticas de IA prohibidas»**; el **capítulo III, «Sistemas de IA de alto riesgo»**; y el **capítulo IV, «Obligaciones de transparencia de los proveedores y responsables del despliegue de determinados sistemas de IA»**. No hay un capítulo del riesgo mínimo porque no hay obligaciones que poner en él.

Los requisitos de los sistemas de alto riesgo son el **capítulo III, sección 2, artículos 9 a 15**, y sus rúbricas son el índice del expediente de B4: sistema de gestión de riesgos (9), datos y gobernanza de datos (10), documentación técnica (11), conservación de registros (12), transparencia y comunicación de información a los responsables del despliegue (13), supervisión humana (14) y precisión, solidez y ciberseguridad (15). Las obligaciones por papel están en la **sección 3, artículos 16 a 27**. Verificado en el texto consolidado de EUR-Lex el 31/08/2026.

> Fuentes primarias a abrir: Reglamento (UE) 2024/1689, capítulos sobre prácticas prohibidas, sistemas de alto riesgo y obligaciones de transparencia, en EUR-Lex.

### 7. Prácticas prohibidas: el catálogo y su lógica

Hay un conjunto cerrado de usos que el reglamento prohíbe directamente. No son una categoría de riesgo más alto con más papeles: son un **no**.

La lógica que las une es reconocible aunque no te sepas la lista: se prohíben las prácticas que **anulan la capacidad de decidir de la persona** o que **la clasifican socialmente** de formas que la Unión considera incompatibles con sus derechos fundamentales. Manipulación que explota vulnerabilidades, puntuación social generalizada, ciertas inferencias sobre características sensibles a partir de datos biométricos, y ciertos usos de identificación biométrica en espacios accesibles al público.

Para una aseguradora la reacción natural es «esto no va conmigo». Es la reacción peligrosa. Las prácticas prohibidas rara vez llegan por la puerta principal; llegan como una idea razonable en una reunión de producto. «Puntuar la fiabilidad del asegurado combinando su historial de siniestros con señales de comportamiento no relacionadas con el seguro» suena a innovación y se acerca peligrosamente a la puntuación social. En B2 vuelve esta idea con casos concretos del sector.

La consecuencia operativa es que **la revisión de prácticas prohibidas va antes que la clasificación**, no después. Si el uso está prohibido, clasificar el sistema es tiempo perdido.

Las prácticas prohibidas están en el **artículo 5**, comprobado en EUR-Lex el 31/08/2026.

El catálogo del **artículo 5, apartado 1**, leído en el texto consolidado el 31/08/2026, tiene **diez letras**, y dos de ellas no existían en la versión de 2024: técnicas subliminales o deliberadamente manipuladoras o engañosas [a)]; explotación de vulnerabilidades derivadas de la edad, la discapacidad o «una situación social o económica específica» [b)]; **b bis)** y **b ter)**, añadidas por el Reglamento (UE) 2026/1744, sobre generación o manipulación de material íntimo o sexualmente explícito de una persona identificable sin su consentimiento y sobre material del artículo 2, letras c) y e), de la Directiva 2011/93/UE; puntuación ciudadana por comportamiento social o características personales [c)]; predicción del riesgo de que una persona cometa un delito basada únicamente en perfiles o rasgos de personalidad [d)]; creación o ampliación de bases de datos de reconocimiento facial por extracción no selectiva de imágenes [e)]; inferencia de emociones en el trabajo y en centros educativos [f)]; categorización biométrica para deducir raza, opiniones políticas, afiliación sindical, convicciones religiosas o filosóficas, vida sexual u orientación sexual [g)]; e identificación biométrica remota «en tiempo real» en espacios de acceso público con fines de garantía del cumplimiento del Derecho [h)].

Casi todas llevan excepción incorporada, y ahí está el detalle que se pierde en los resúmenes: la letra d) no alcanza a los sistemas «utilizados para apoyar la valoración humana de la implicación de una persona en una actividad delictiva que ya se base en hechos objetivos y verificables»; la f) exceptúa los fines médicos o de seguridad; la g) no incluye el etiquetado o filtrado de conjuntos de datos biométricos adquiridos lícitamente; y la h) solo se levanta para los tres objetivos tasados de sus incisos i) a iii), con las condiciones de los apartados 2 y 3. Las nuevas letras b bis) y b ter) tienen además sus propias condiciones de aplicación en los apartados **1 bis** y **1 ter**, y —esto importa para el calendario— no son exigibles desde la misma fecha que el resto del artículo: ver la slide 11. Verificado en el texto consolidado de EUR-Lex el 31/08/2026.

> Fuentes primarias a abrir: texto consolidado del Reglamento (UE) 2024/1689 en EUR-Lex, artículo 5; directrices de la Comisión sobre prácticas prohibidas, si publicadas.

### 8. Alto riesgo: los dos caminos que llevan a esa categoría

Un sistema no llega a alto riesgo por ser complejo, ni por usar un modelo grande, ni por costar mucho. Llega por una de **dos vías**, y conviene tenerlas separadas mentalmente porque llevan a expedientes distintos.

**Vía 1 — componente de seguridad de un producto ya regulado.** Si el sistema de IA es un componente de seguridad de un producto que ya está sujeto a legislación de armonización de la Unión, o es él mismo ese producto, hereda la categoría. Aquí viven los productos sanitarios, la maquinaria, los vehículos. La conformidad se apoya en el régimen sectorial que ya existía.

**Vía 2 — uso listado en el anexo de ámbitos de alto riesgo.** El reglamento enumera ámbitos concretos —empleo, educación, servicios esenciales, aplicación de la ley, entre otros— y, dentro de cada uno, usos específicos. Si tu uso está en esa lista, eres alto riesgo salvo que apliques una excepción y puedas justificarla.

Meridiana entra, si entra, por la segunda vía. Ninguna parte del agente de siniestros es un componente de seguridad de un producto físico. Toda la discusión de B2 gira alrededor de si el triaje y la propuesta de resolución encajan en alguno de los usos listados, y de si la excepción por tarea accesoria aplica.

Una advertencia: las dos vías **no se excluyen**, y el orden de comprobación importa. Comprueba primero la vía de producto, porque si aplica arrastra un régimen sectorial que cambia quién certifica.

Las dos vías son el **artículo 6**, comprobado en EUR-Lex el 31/08/2026: el **apartado 1** remite al **Anexo I**, que es la vía de producto, y el **apartado 2** al **Anexo III**, que es la de usos listados. Esa numeración importa más allá de la cita, porque las dos vías tienen **fechas de aplicación distintas** y se leen en el artículo 113.

El **Anexo I** se titula «Lista de actos legislativos de armonización de la Unión» y va en dos secciones. La **sección A** recoge los actos basados en el nuevo marco legislativo —juguetes, embarcaciones de recreo, ascensores, atmósferas explosivas, equipos radioeléctricos, equipos a presión, transporte por cable, equipos de protección individual, aparatos de gas, productos sanitarios y productos sanitarios para diagnóstico in vitro— y su **punto 1 fue suprimido por el Reglamento (UE) 2026/1744**. La **sección B** enumera «otros actos legislativos de armonización de la Unión»: seguridad de la aviación civil, homologación de vehículos de dos o tres ruedas, agrícolas y forestales, y de motor, equipos marinos, interoperabilidad ferroviaria, seguridad de los vehículos y aviación civil, y cierra con un **punto 21, el Reglamento (UE) 2023/1230 sobre máquinas, añadido allí por el Reglamento (UE) 2026/1744**. Ese traslado no es cosmético: el **artículo 2, apartado 2**, en su nueva redacción, reduce lo que se aplica a los sistemas de alto riesgo del artículo 6, apartado 1, relativos a productos de la sección B.

El **Anexo III** se titula «Sistemas de IA de alto riesgo a que se refiere el artículo 6, apartado 2» y tiene **ocho ámbitos**: biometría (1), infraestructuras críticas (2), educación y formación profesional (3), empleo, gestión de los trabajadores y acceso al autoempleo (4), acceso a servicios privados esenciales y a servicios y prestaciones públicos esenciales y disfrute de estos (5), garantía del cumplimiento del Derecho (6), migración, asilo y gestión del control fronterizo (7), y administración de justicia y procesos democráticos (8). Dentro de cada uno hay usos concretos, y es la descripción del uso —no el título del ámbito— la que decide. El contenido íntegro de ambos anexos se recorre en B2. Verificado en el texto consolidado de EUR-Lex el 31/08/2026.

> Fuentes primarias a abrir: texto consolidado del Reglamento (UE) 2024/1689 en EUR-Lex, artículo 6 y Anexos I y III.

### 9. Riesgo limitado y obligaciones de transparencia: decir que hay una máquina

Por debajo del alto riesgo hay un régimen ligero pero real: cuando un sistema interactúa con personas o genera contenido, hay que **decirlo**.

La lógica es antigua y no tiene nada de tecnológica: una persona toma decisiones distintas según sepa o no con quién está hablando. Un asegurado que cree hablar con un tramitador redacta su relato de una forma; si sabe que le contesta un agente automático, pregunta de otra y sabe que puede pedir a un humano.

En Meridiana esto toca tres sitios concretos, y ninguno es el modelo:

- El **chat del portal**, donde el asegurado describe el siniestro. Tiene que quedar claro que la respuesta es automática y cómo se llega a una persona.
- El **correo de petición de documentación**, redactado por el modelo. Si va firmado con el nombre de un tramitador que no lo escribió, el problema no es solo normativo.
- Cualquier **resumen del expediente** generado que se muestre al asegurado como si fuera texto de la compañía.

Fíjate en que estas obligaciones son de producto, no de modelo: se cumplen en la interfaz, en la plantilla del correo y en el pie de página. Son las más baratas de cumplir y las que más a menudo se olvidan, porque no las implementa nadie del equipo de IA.

El régimen es el **artículo 50**, y reparte los supuestos entre los dos papeles. Sobre el **proveedor** pesan dos: diseñar los sistemas destinados a interactuar directamente con personas de forma que estas «estén informadas de que están interactuando con un sistema de IA», salvo cuando resulte evidente para una persona razonablemente informada y atenta [apartado 1]; y marcar en **formato legible por máquina** los resultados de salida de los sistemas que generen contenido sintético de audio, imagen, vídeo o texto [apartado 2]. Sobre el **responsable del despliegue** pesan los otros dos: informar del funcionamiento del sistema a las personas expuestas a reconocimiento de emociones o categorización biométrica [apartado 3]; y hacer público que el contenido es artificial en las ultrafalsificaciones y en el texto publicado para informar al público sobre asuntos de interés público [apartado 4]. El apartado 5 fija el *cuándo* y el *cómo*: «de manera clara y distinguible a más tardar con ocasión de la primera interacción o exposición», y con los requisitos de accesibilidad aplicables. El apartado 6 aclara que nada de esto desplaza al capítulo III. Verificado en el texto consolidado de EUR-Lex el 31/08/2026.

> Fuentes primarias a abrir: Reglamento (UE) 2024/1689, capítulo sobre obligaciones de transparencia para determinados sistemas de IA, en EUR-Lex.

### 10. Modelos de propósito general: el régimen aparte y por qué te afecta sin que lo parezca

Los modelos de propósito general tienen un régimen propio, distinto del de los sistemas. La razón es estructural: un modelo no tiene un uso previsto. Lo pone quien lo integra.

El reglamento resuelve eso poniendo obligaciones sobre **el proveedor del modelo** —documentación técnica del modelo, información para quienes lo integran, política de derechos de autor, y obligaciones reforzadas para los modelos con riesgo sistémico— y dejando las obligaciones de **uso** a quien construye el sistema encima.

Para Meridiana esto significa dos cosas muy prácticas.

Primera: parte de tu expediente **la escribe otro** y tú tienes que exigírsela. La documentación que el proveedor del modelo publica es un insumo de tu propia documentación técnica. Si tu proveedor no la publica, eso es un criterio de selección de proveedor, no una nota al pie.

Segunda: integrar un modelo de propósito general en un sistema con un uso concreto **no traslada las obligaciones hacia arriba**. Que el proveedor cumpla lo suyo no clasifica tu sistema ni redacta tu documentación. Es la conversación que hay que tener con el equipo de compras cuando alguien enseña el certificado del proveedor como si fuera el de Meridiana.

La denominación oficial es **«modelos de IA de uso general»**, y son el **capítulo V** entero. La subcategoría reforzada es la de los **«modelos de IA de uso general con riesgo sistémico»**, cuyas reglas de clasificación están en el **artículo 51**. El umbral cuantitativo es el del **artículo 51, apartado 2**, y conviene copiarlo bien: se presume que un modelo tiene capacidades de gran impacto «cuando la cantidad acumulada de cálculo utilizada para su entrenamiento, medida en operaciones de coma flotante, sea superior a 10^25». El apartado 3 faculta a la Comisión para moverlo por acto delegado, así que es una cifra con fecha de caducidad.

Las obligaciones de todo proveedor de un modelo de uso general son el **artículo 53, apartado 1**: documentación técnica del modelo con el contenido mínimo del Anexo XI [a)]; información y documentación para quienes lo integren, con el contenido mínimo del Anexo XII [b)]; directrices para cumplir el Derecho de autor, incluida la detección de la reserva de derechos del artículo 4, apartado 3, de la Directiva (UE) 2019/790 [c)]; y un «resumen suficientemente detallado del contenido utilizado para el entrenamiento», público y conforme al modelo de la Oficina de IA [d)]. Las letras a) y b) decaen para modelos con licencia libre y de código abierto en las condiciones del apartado 2, salvo que tengan riesgo sistémico. Si lo tienen, se suman las del **artículo 55, apartado 1**: evaluación con pruebas de simulación de adversarios, evaluación y mitigación de riesgos sistémicos, comunicación de incidentes graves a la Oficina de IA y protección de la ciberseguridad del modelo y de su infraestructura física. La letra b) del artículo 53 es la que te da derecho a exigir documentación a tu proveedor; la letra d) es la que te permite saber si hay un problema de origen de los datos. Verificado en el texto consolidado de EUR-Lex el 31/08/2026.

> Fuentes primarias a abrir: Reglamento (UE) 2024/1689, capítulo sobre modelos de IA de uso general, en EUR-Lex; código de buenas prácticas para modelos de uso general publicado por la Comisión, si disponible.

### 11. Calendario de aplicación por bloques de obligaciones: por qué no hay una sola fecha

La pregunta «¿desde cuándo me aplica?» no tiene una respuesta. Tiene varias, porque el reglamento **escalona la aplicación por bloques de obligaciones**: las prohibiciones y la alfabetización en IA por un lado, el régimen de modelos de propósito general por otro, el grueso del alto riesgo más tarde, y algunos supuestos con plazos más largos.

Ese diseño no es capricho. Prohibir algo no requiere que exista un ecosistema de normas técnicas ni organismos notificados; exigir una evaluación de conformidad sí. Las fechas siguen a la disponibilidad de la infraestructura de cumplimiento.

Lo que hay que llevarse, y es independiente de las fechas concretas: para saber qué te aplica hoy necesitas **tres datos**, no uno.

1. **Qué eres** en la cadena: proveedor, responsable del despliegue, importador, distribuidor (bloque B3).
2. **Qué categoría** tiene tu sistema (bloque B2).
3. **En qué punto del calendario** estamos para ese bloque de obligaciones.

Un plan de cumplimiento que no cruce las tres cosas produce el error más común: empezar por lo que vence más tarde porque es lo que más suena, y llegar tarde a lo que ya vencía.

Lo comprobado en EUR-Lex, con fecha de verificación **31/08/2026**:

- El reglamento es de **13 de junio de 2024**, se publicó en el DOUE el **12 de julio de 2024** y entró en vigor el **1 de agosto de 2024**, el vigésimo día tras la publicación.
- El calendario original **ha sido modificado** por el **Reglamento (UE) 2026/1744, de 8 de julio de 2026**. Cualquier material anterior a esa fecha lleva las fechas viejas, y ese es el motivo por el que esta slide existe.
- Con esa modificación, el capítulo III, secciones 1 a 3 —el grueso de las obligaciones de alto riesgo— se aplica a los sistemas del **artículo 6.2 y el Anexo III** desde el **2 de diciembre de 2027**.

El escalonado completo es el **artículo 113**, «Entrada en vigor y aplicación», leído en el texto consolidado el 31/08/2026. La regla general es que el reglamento «será aplicable a partir del **2 de agosto de 2026**», y sobre ella se recortan cuatro excepciones:

- **Letra a)** —modificada por el Reglamento (UE) 2026/1744—: los **capítulos I y II** (disposiciones generales y prácticas prohibidas) desde el **2 de febrero de 2025**, salvo las nuevas letras b bis) y b ter) del artículo 5, apartado 1, y los apartados 1 bis y 1 ter del artículo 5, que se aplican desde el **2 de diciembre de 2026**.
- **Letra b)**: el **capítulo III, sección 4**, el **capítulo V**, el **capítulo VII** y el **capítulo XII**, y el artículo 78, desde el **2 de agosto de 2025**, «a excepción del artículo 101», que queda por tanto en la regla general del 2 de agosto de 2026.
- **Letra c)** —modificada—: el **capítulo III, secciones 1, 2 y 3**, salvo el artículo 6, apartado 5, desde el **2 de diciembre de 2027** para los sistemas de alto riesgo del **artículo 6, apartado 2, y el Anexo III**, y desde el **2 de agosto de 2028** para los del **artículo 6, apartado 1, y el Anexo I**.
- **Letra d)** —añadida—: los **artículos 102 a 110** desde el **27 de julio de 2026**.

Aquí queda resuelta la discrepancia que arrastraban los resúmenes: para el Anexo I la fecha es el **2 de agosto de 2028**, no el 2 de agosto de 2027. Quien diera 2027 estaba leyendo el texto de 2024, donde el grueso del alto riesgo colgaba de una sola fecha. Es exactamente el tipo de error que este bloque existe para evitar.

> Fuentes primarias a abrir: texto consolidado del Reglamento (UE) 2024/1689 en EUR-Lex, artículo 113; Reglamento (UE) 2026/1744 y su tabla de correspondencias; DOUE, incluidas correcciones de errores.

### 12. Qué obligaciones ya están vigentes al escribir esto: la slide que caduca

Esta es la única slide del bloque cuyo contenido caduca por diseño, y por eso está aislada del resto: para poder actualizarla sin tocar nada más.

**A 31 de agosto de 2026**, leído el artículo 113 del texto consolidado ese mismo día, la situación es esta: el reglamento es aplicable **en su totalidad salvo dos cosas**. Ya son exigibles los capítulos I y II (con la salvedad de abajo), el capítulo III sección 4, los capítulos V, VII y XII, los artículos 78 y 101 y los artículos 102 a 110 —y, desde el 2 de agosto de 2026, todo lo demás no exceptuado, incluidos los capítulos IV, VI, VIII, IX y el artículo 6, apartado 5—. **No son exigibles todavía**: (1) las letras b bis) y b ter) del artículo 5, apartado 1, y los apartados 1 bis y 1 ter de ese mismo artículo, que esperan al 2 de diciembre de 2026; y (2) el capítulo III, secciones 1, 2 y 3 —el grueso del régimen de alto riesgo—, que espera al 2 de diciembre de 2027 para el Anexo III y al 2 de agosto de 2028 para el Anexo I.

Traducido a Meridiana: las prohibiciones y la transparencia del artículo 50 ya te obligan hoy; el expediente técnico de alto riesgo todavía no, y ese margen es precisamente el que hay que gastar en construirlo.

Lo que sí puede afirmarse con independencia de la fecha es **el método** para responder a la pregunta, y es lo que hay que aprender aquí:

1. Abre el texto consolidado en EUR-Lex y localiza las disposiciones sobre aplicación. Un texto consolidado incorpora correcciones que los resúmenes no recogen.
2. Comprueba si hay correcciones de errores publicadas en el DOUE posteriores a la versión que estás leyendo. Han existido y han cambiado detalles.
3. Contrasta con las comunicaciones de la Comisión sobre el calendario, que es donde aparecen matices y aplazamientos.
4. **Anota la fecha en que hiciste esto** junto a la conclusión. Una afirmación normativa sin fecha de comprobación es una afirmación sin valor.

Ese cuarto punto es la práctica que este curso quiere dejarte instalada. En el expediente de Meridiana, cada afirmación normativa lleva su fecha de verificación y el enlace a la fuente que se abrió. Cuando un auditor pregunte de dónde salió, la respuesta no puede ser «lo leímos en algún sitio».

> Fuentes primarias a abrir: EUR-Lex, texto consolidado del Reglamento (UE) 2024/1689; DOUE, correcciones de errores; página de la Comisión Europea sobre aplicación del AI Act.

### 13. Autoridades: europeas y nacionales, y quién hace qué

El reglamento no crea un único regulador. Crea un reparto en dos planos, y saber a quién llamar es parte del trabajo.

**Plano europeo.** Hay estructuras de la Comisión y órganos de coordinación entre Estados miembros que se ocupan de la interpretación común, de las directrices y, de forma señalada, de la supervisión de los modelos de propósito general. Ese último punto es relevante para ti: el proveedor de tu modelo probablemente no rinde cuentas ante tu autoridad nacional, sino ante el plano europeo.

**Plano nacional.** Cada Estado miembro designa sus autoridades: al menos una que **notifica** —es decir, que designa y vigila a los organismos que evalúan la conformidad— y al menos una de **vigilancia del mercado**, que es la que puede llamar a tu puerta.

La consecuencia práctica para Meridiana: en un mismo incidente puedes tener tres interlocutores. La autoridad de vigilancia de mercado por el sistema, la autoridad de protección de datos por los datos personales y el supervisor de seguros por la actividad aseguradora. No comparten expediente, y cada uno preguntará lo suyo. Tener un único cuerpo de documentación bien indexado es lo que evita que tres peticiones simultáneas se conviertan en tres versiones distintas de la verdad.

Los nombres son los del **capítulo VII**. En el plano europeo, la **Oficina de IA** (*artículo 64*), que es la estructura con la que la Comisión desarrolla los conocimientos y capacidades de la Unión, y el **Consejo Europeo de Inteligencia Artificial**, abreviado en el propio texto como **«Consejo de IA»** (*artículo 65*): un representante por Estado miembro, con el Supervisor Europeo de Protección de Datos como observador y la Oficina de IA asistiendo sin voto. Sus funciones están en el artículo 66. Los completan el **foro consultivo** (*artículo 67*) y el **grupo de expertos científicos independientes** (*artículo 68*).

El reparto nacional es el **artículo 70, apartado 1**, y es literalmente el que anticipaba esta slide: cada Estado miembro «establecerá o designará al menos una autoridad notificante y al menos una autoridad de vigilancia del mercado como autoridades nacionales competentes», que ejercerán sus poderes «de manera independiente, imparcial y sin sesgos». El mismo artículo permite que una sola autoridad asuma ambas funciones. El apartado 2 añade una figura práctica que conviene conocer antes de necesitarla: una de las autoridades de vigilancia del mercado actúa como **punto de contacto único**, y la Comisión publica la lista. Verificado en el texto consolidado de EUR-Lex el 31/08/2026.

> Fuentes primarias a abrir: Reglamento (UE) 2024/1689, capítulo sobre gobernanza y capítulo sobre autoridades nacionales competentes, en EUR-Lex.

### 14. La autoridad española y su papel: a quién le rindes cuentas aquí

España designa su propia autoridad para las funciones que el reglamento atribuye al plano nacional. Esa autoridad no sustituye a las que ya existían: convive con ellas.

Lo comprobado en el BOE el 31/08/2026, y conviene leerlo con cuidado porque es un dato negativo: existe la **Agencia Española de Supervisión de Inteligencia Artificial (AESIA)**, cuyo Estatuto aprueba el **Real Decreto 729/2023, de 22 de agosto** (BOE-A-2023-18911). Pero ese real decreto **no contiene la designación del artículo 70** del reglamento: abierto su texto consolidado, no menciona el Reglamento (UE) 2024/1689 ni designa a la AESIA como autoridad de vigilancia del mercado o autoridad notificante, ni regula su articulación con la AEPD o con el supervisor de seguros. La norma que haga esa designación es un anteproyecto de ley, y un anteproyecto no es Derecho.

**Todavía no existe la norma que haga esa designación**, así que aquí no se nombra ninguna autoridad del artículo 70 ni se describen sus competencias. Cuando se publique en el BOE habrá que leer allí la denominación oficial, el alcance y la articulación con la AEPD y con el supervisor sectorial de seguros.

Lo que sí puede afirmarse es **cómo prepararse** para esa interlocución, y es lo mismo con independencia de qué organismo acabe llamando:

- **Un punto de contacto nombrado.** Una persona responsable del expediente del sistema, con nombre y correo, no un buzón genérico. Los requerimientos tienen plazo y un buzón sin dueño se los come.
- **Un índice del expediente que quepa en una página.** Qué documentos existen, dónde están, quién los mantiene y cuándo se revisaron por última vez.
- **La capacidad de reconstruir un caso concreto.** «Enséñenos cómo decidió el sistema en el siniestro SIN-2026-0007» es la petición realista. Se responde con la traza que ya guardas desde el curso 3, no con un documento de política.
- **Un registro de decisiones de gobernanza.** Quién aprobó la clasificación, quién aprobó cada despliegue, quién revisó las evals.

Si esas cuatro cosas existen, la identidad concreta del organismo que pregunta cambia poco.

> Fuentes primarias a abrir: normativa española de designación de la autoridad nacional competente en materia de IA, en el BOE; AEPD, criterios publicados sobre IA; Reglamento (UE) 2024/1689, capítulo sobre autoridades nacionales.

### 15. Sanciones: los tramos y sobre qué se calculan

El régimen sancionador tiene una estructura reconocible en la normativa europea reciente: **varios tramos**, ordenados por gravedad, y cada tramo expresado como el **mayor** de dos magnitudes —un importe fijo o un porcentaje del volumen de negocios anual mundial del grupo.

Esa arquitectura de «lo que sea mayor» es deliberada. Un importe fijo es irrelevante para una multinacional y letal para una pyme; un porcentaje puro es lo contrario. Combinarlos hace que la sanción escale con el tamaño del infractor.

Los tramos del **artículo 99** son **tres**, y todos con la fórmula «o … si esta cuantía fuese superior»:

- **35 000 000 EUR o el 7 %** del volumen de negocios mundial total del ejercicio anterior, para el incumplimiento de las prácticas prohibidas del artículo 5 [apartado 3].
- **15 000 000 EUR o el 3 %**, para el incumplimiento de las obligaciones de los proveedores (artículo 16), representantes autorizados (22), importadores (23), distribuidores (24), responsables del despliegue (26), organismos notificados y las **obligaciones de transparencia del artículo 50** [apartado 4]. El Reglamento (UE) 2026/1744 añadió aquí una letra d bis) para el artículo 25, apartados 2 y 4.
- **7 500 000 EUR o el 1 %**, por presentar información inexacta, incompleta o engañosa a un organismo notificado o a una autoridad nacional competente [apartado 5].

Para las **pymes**, incluidas las empresas emergentes, se aplica **el menor** de los dos, no el mayor [apartado 6]; el Reglamento (UE) 2026/1744 extendió esa regla a las pequeñas empresas de mediana capitalización [apartado 6 bis]. Los proveedores de modelos de uso general tienen régimen aparte en el **artículo 101**: multas impuestas por la propia Comisión, de hasta el 3 % o 15 000 000 EUR, si esta cifra es superior. Verificado en el texto consolidado de EUR-Lex el 31/08/2026.

Y los criterios de modulación de la slide están en el **artículo 99, apartado 7**, letras a) a h): entre ellos, «el grado de cooperación con las autoridades nacionales competentes con el fin de subsanar la infracción y mitigar sus posibles efectos adversos» [f)] y «el grado de responsabilidad del operador, teniendo en cuenta las medidas técnicas y organizativas aplicadas por este» [g)].

Lo relevante para el trabajo diario no es memorizar cifras, sino entender **qué eleva o rebaja la sanción**, porque eso sí depende de ti:

- La gravedad y duración de la infracción, y si fue intencionada o negligente.
- Si el operador **cooperó** con la autoridad y si comunicó la infracción por iniciativa propia.
- Las medidas adoptadas para mitigar el daño.
- Si hubo infracciones anteriores.

Todo eso se prueba con documentación fechada. Un equipo que detectó una desviación en sus evals, la registró, la escaló y la corrigió está en una posición radicalmente distinta ante una inspección que uno que se enteró por la reclamación de un cliente. El expediente no solo demuestra cumplimiento: **modula la consecuencia cuando el cumplimiento falla**.

> Fuentes primarias a abrir: Reglamento (UE) 2024/1689, capítulo sobre sanciones, en EUR-Lex; régimen sancionador nacional de desarrollo, en el BOE.

### 16. Cómo leer el reglamento sin perderse: estructura y anexos

El texto es largo y se lee mal de principio a fin. Tiene una estructura que, una vez conocida, permite ir directo.

Tres piezas y su función:

- **Considerandos.** Van antes del articulado y no son vinculantes por sí mismos, pero son la mejor herramienta de interpretación que tienes. Cuando el artículo dice algo ambiguo, el considerando explica qué problema quería resolver. En la discusión de B2 sobre la excepción por tarea accesoria, los considerandos son más útiles que el artículo.
- **Articulado.** Agrupado en capítulos por materia: ámbito y definiciones, prácticas prohibidas, alto riesgo, transparencia, modelos de uso general, gobernanza, vigilancia, sanciones, disposiciones finales.
- **Anexos.** Aquí vive el detalle operativo: los ámbitos y usos de alto riesgo, el contenido mínimo de la documentación técnica, la información de las declaraciones. **Los anexos son donde trabajas de verdad**, y son también la parte que puede modificarse por acto delegado sin reabrir el reglamento entero.

Ese último detalle tiene consecuencias: **un anexo puede cambiar sin que cambie el artículo que lo invoca**. Si tu documentación cita «el anexo» sin decir qué versión consultaste y cuándo, no sabrás si sigue siendo cierta.

Consejo de método: trabaja siempre sobre el **texto consolidado** de EUR-Lex, no sobre el PDF que alguien descargó hace un año.

Los anexos vigentes a 31/08/2026 son **catorce**, con estos títulos oficiales:

| Anexo | Título |
|---|---|
| I | Lista de actos legislativos de armonización de la Unión |
| II | Lista de los delitos a que se refiere el artículo 5, apartado 1, párrafo primero, letra h), inciso iii) |
| III | Sistemas de IA de alto riesgo a que se refiere el artículo 6, apartado 2 |
| IV | Documentación técnica a que se refiere el artículo 11, apartado 1 |
| V | Declaración UE de conformidad |
| VI | Procedimiento de evaluación de la conformidad fundamentado en un control interno |
| VII | Conformidad fundamentada en la evaluación del sistema de gestión de la calidad y la evaluación de la documentación técnica |
| VIII | Información que debe presentarse para la inscripción en el registro de sistemas de IA de alto riesgo de conformidad con el artículo 49 |
| IX | Información que debe presentarse para la inscripción en el registro de los sistemas de IA de alto riesgo enumerados en el anexo III en relación con las pruebas en condiciones reales |
| X | Actos legislativos de la Unión relativos a sistemas informáticos de gran magnitud en el espacio de libertad, seguridad y justicia |
| XI | Documentación técnica a que se refiere el artículo 53, apartado 1, letra a) |
| XII | Información sobre transparencia a que se refiere el artículo 53, apartado 1, letra b) |
| XIII | Criterios para la clasificación de los modelos de IA de uso general con riesgo sistémico |
| XIV | Lista de códigos, categorías y tipos correspondientes de sistemas de IA a efectos del procedimiento de notificación previsto en el artículo 30 |

El **Anexo XIV es nuevo**: lo añadió el Reglamento (UE) 2026/1744. Es la mejor ilustración posible de lo que dice esta slide: quien citara «los trece anexos del AI Act» estaría citando una lista que ya no existe. Verificado en el texto consolidado de EUR-Lex el 31/08/2026.

> Fuentes primarias a abrir: EUR-Lex, texto consolidado del Reglamento (UE) 2024/1689, incluidos considerandos y anexos; registro de actos delegados de la Comisión.

### 17. Errores frecuentes al interpretar el ámbito

Cinco errores que aparecen una y otra vez en las primeras reuniones de cumplimiento. Los cinco se resuelven con la misma disciplina: leer el ámbito por escrito antes de opinar.

- **«Usamos un modelo de terceros, así que la responsabilidad es del proveedor.»** El reglamento reparte obligaciones entre varios papeles simultáneos. Integrar y desplegar genera obligaciones propias, y en algunos supuestos puedes convertirte en proveedor de un sistema nuevo sin pretenderlo. Es el bloque B3 entero.
- **«Es interno, no lo vendemos.»** El uso propio dentro de la Unión está en el ámbito. Un sistema que solo usan los 24 tramitadores de Meridiana no queda fuera por no venderse.
- **«Es un piloto.»** Un piloto con datos reales y efectos sobre asegurados reales es un sistema en uso. La etiqueta la pone el efecto, no el nombre del proyecto.
- **«El humano decide, luego no aplica.»** Una revisión humana que aprueba lo que propone el sistema sin capacidad ni tiempo real de discrepar no cambia la clasificación. Esta idea vuelve, con detalle, en B2.
- **«El proveedor está fuera de la Unión.»** No es un escudo. Lo determinante es dónde se pone el sistema en el mercado y dónde se usa su resultado.

El patrón común de los cinco: buscar la puerta de salida antes de haber entendido la habitación. Es más rápido —y más barato— clasificar bien y documentar, que construir una interpretación creativa que hay que defender ante alguien que ha leído el texto.

### 18. Dónde encaja Meridiana en este mapa: el planteamiento, no la respuesta

Con el mapa delante, ya se puede formular la pregunta de Meridiana con precisión. La respuesta es el bloque B2 completo, así que aquí solo se deja bien planteada.

El agente hace cuatro cosas y no todas se clasifican igual:

1. **FNOL.** Extrae campos de un texto libre. Ni decide ni propone. Es una tarea de comprensión de lenguaje sobre un relato del propio interesado.
2. **Triaje.** Decide la vía de tramitación, salvo cuando hay indicio de lesiones personales, en cuyo caso la regla determinista deriva a un humano sin consultar al modelo.
3. **Petición de documentación.** Calcula lo que falta con una tabla y el modelo redacta el correo.
4. **Propuesta de resolución.** Propone importe y motivación. **Un humano aprueba siempre**, con revisión completa por encima del umbral de 1.500 € del caso.

Las preguntas que hay que responder por escrito en B2 son tres, y ninguna se contesta con una impresión:

- ¿Alguno de estos usos encaja en un ámbito listado como de alto riesgo?
- Si encaja, ¿se cumple alguna excepción, y puede justificarse con hechos verificables del sistema?
- ¿La supervisión humana que existe es **efectiva**, o es un botón de aprobar?

Anticipo una idea que va a doler: la respuesta puede cambiar sin tocar una línea de código. Basta con que producto suba el umbral de aprobación automática.

### 19. Aviso: esto no es asesoramiento jurídico, y por qué eso no te exime

Este material es formativo. No es asesoramiento jurídico y no sustituye la opinión de un profesional sobre tu caso concreto. Ese aviso no es una fórmula defensiva: describe una diferencia real de función.

Lo que este curso puede darte:

- El **método**: cómo se clasifica, qué se documenta, cómo se defiende por escrito una decisión.
- El **vocabulario** para hablar con quien sí asesora sin perder tres reuniones en traducciones.
- Los **artefactos técnicos** que el expediente necesita y que solo puede producir el equipo que construyó el sistema.

Lo que no puede darte: la conclusión firmada sobre tu sistema, tu contrato con el proveedor y tu jurisdicción.

Y aquí está el punto que interesa. **Que necesites un jurista no traslada el trabajo a su mesa.** Ningún abogado externo puede decirte si vuestras evals cubren el escenario de las lesiones personales, si la traza permite reconstruir una decisión de hace ocho meses o si el humano que aprueba tiene tiempo material de revisar. Eso solo lo sabe el equipo.

El reparto sano es: el equipo técnico aporta hechos verificables sobre el sistema; el jurista aporta la calificación y el riesgo legal. Un expediente escrito solo por juristas describe un sistema que no existe. Uno escrito solo por ingenieros no resiste una pregunta normativa.

### 20. Excepciones del ámbito: militar, investigación y uso personal

El reglamento excluye de su ámbito determinados supuestos. Los tres que más se citan:

- **Fines militares, de defensa y de seguridad nacional.** Es una exclusión de ámbito, no una rebaja de obligaciones.
- **Investigación y desarrollo científico.** Con matices importantes: la actividad de investigación previa a la puesta en el mercado no es lo mismo que probar con usuarios reales.
- **Uso personal no profesional.** Una persona usando un asistente en su casa no está en el ámbito.

La lección para Meridiana está en la segunda. Es la excepción que más se invoca mal, porque «estamos en fase de investigación» es una frase que se puede decir durante dos años. La frontera real no es cómo llame el equipo a la fase: es si el sistema **produce efectos sobre personas reales**.

El día que el agente procesa el siniestro de un asegurado de verdad y de ahí sale una petición de documentación que llega a su correo, se acabó la investigación. Que el proyecto siga en una rama llamada `experimental` y que nadie haya firmado un despliegue formal no cambia nada.

El indicador práctico, y es el que debe estar en el registro de decisiones del proyecto: **la fecha del primer caso real**. Antes de esa fecha, una conversación. Después, otra.

Las tres están en el **artículo 2**, y las condiciones importan más que los titulares.

**Militar, defensa y seguridad nacional**, apartado 3: el reglamento no se aplica a los ámbitos fuera del Derecho de la Unión y «no afectará a las competencias de los Estados miembros en materia de seguridad nacional», ni a los sistemas usados «exclusivamente con fines militares, de defensa o de seguridad nacional». La palabra que hace el trabajo es *exclusivamente*: un sistema de doble uso no queda fuera.

**Investigación**, y aquí hay dos exclusiones distintas que conviene no mezclar. El apartado 6 excluye los sistemas y modelos «desarrollados y puestos en servicio específicamente con la investigación y el desarrollo científicos como única finalidad». El apartado 8 excluye «cualquier actividad de investigación, prueba o desarrollo relativa a sistemas de IA o modelos de IA **antes de su introducción en el mercado o puesta en servicio**», y cierra con la frase que zanja la discusión de esta slide: «**Las pruebas en condiciones reales no estarán cubiertas por esa exclusión.**»

**Uso personal**, apartado 10: quedan fuera las obligaciones de los responsables del despliegue «que sean personas físicas que utilicen sistemas de IA en el ejercicio de una actividad puramente personal de carácter no profesional». Verificado en el texto consolidado de EUR-Lex el 31/08/2026.

> Fuentes primarias a abrir: Reglamento (UE) 2024/1689, artículo de ámbito de aplicación y considerandos sobre exclusiones, en EUR-Lex.

### 21. Sistemas ya en el mercado antes de la entrada en vigor: el régimen transitorio

Los sistemas que ya estaban en el mercado o en uso antes de las fechas de aplicación tienen un tratamiento transitorio propio, con plazos y condiciones específicos según el tipo de sistema.

El régimen transitorio es el **artículo 111**, y su apartado 2 —en la redacción dada por el Reglamento (UE) 2026/1744— dice exactamente lo que esta slide teme. A los sistemas de alto riesgo introducidos en el mercado o puestos en servicio antes de la fecha de aplicación del capítulo III, el reglamento se aplicará «**únicamente si, a partir de esa fecha, dichos sistemas se ven sometidos a cambios significativos en sus diseños**». Ahí está la condición que hace decaer la transitoriedad, y es una condición sobre el diseño, no sobre el calendario. Para los sistemas destinados a autoridades públicas hay además un tope duro: cumplir «a más tardar el **2 de agosto de 2030**».

Los otros tres supuestos, para tenerlos completos: los sistemas de IA que sean componentes de los sistemas informáticos de gran magnitud del Anexo X introducidos antes del 2 de agosto de 2027 deberán ser conformes «a más tardar el 31 de diciembre de 2030» [apartado 1]; los proveedores de modelos de uso general introducidos antes del 2 de agosto de 2025 debían cumplir «a más tardar el 2 de agosto de 2027» [apartado 3]; y los proveedores de sistemas que generen contenido sintético introducidos antes del 2 de agosto de 2026 deben cumplir el artículo 50, apartado 2, «a más tardar el 2 de diciembre de 2026» [apartado 4, añadido en 2026]. En los cuatro casos, el artículo 5 se aplica al margen de la transitoriedad. Verificado en el texto consolidado de EUR-Lex el 31/08/2026.

Lo que sí puede afirmarse, y es lo que hay que llevarse, es la trampa: **el régimen transitorio protege al sistema que no cambia**. Y en un sistema con LLM, «no cambiar» es una ficción.

Enumera lo que cambia en Meridiana en un trimestre normal:

- La versión del modelo del proveedor, a veces sin aviso y sin que tú lo elijas.
- Los prompts de extracción, que se ajustan cuando las evals detectan una regresión.
- El umbral de aprobación automática, si producto lo mueve.
- El catálogo de documentos que se pueden pedir.
- La lista de tools disponibles para el agente.

Cualquiera de esos puede constituir una modificación sustancial y hacer que el sistema deje de estar amparado. La consecuencia operativa es concreta y encaja con lo que ya haces: **el registro de versiones del sistema es también un registro normativo**. Si tienes versionado el prompt, el conjunto de evals y la configuración, puedes decir con precisión qué había desplegado en cada fecha. Si no, la discusión sobre transitoriedad no la puedes ni empezar.

> Fuentes primarias a abrir: Reglamento (UE) 2024/1689, disposiciones transitorias y finales, en EUR-Lex; concepto de modificación sustancial en el articulado y en los considerandos.

### 22. Bancos de pruebas regulatorios: qué son y a quién sirven

El reglamento prevé espacios controlados de pruebas —*sandboxes*— establecidos por las autoridades nacionales, donde un sistema puede desarrollarse y probarse bajo supervisión del regulador antes de su puesta en el mercado.

La idea que hay detrás merece entenderse aunque nunca entres en uno: el regulador reconoce que **hay incertidumbre interpretativa** y prefiere resolverla acompañando al operador que descubriéndola tres años después en una inspección.

Qué se obtiene realmente:

- **Criterio anticipado.** Sales sabiendo cómo ve la autoridad tu clasificación y tus controles. Ese es el valor principal, muy por encima de cualquier otro.
- **Documentación de la interlocución.** La prueba de que actuaste de buena fe y consultaste, que pesa si algo va mal.

Qué **no** se obtiene, y conviene decirlo porque se vende mal: no es una exención, no es un certificado y no te libra de las obligaciones. Tampoco es gratis en esfuerzo; requiere dedicar personas durante meses.

Para una aseguradora de tamaño medio como Meridiana, la decisión honesta suele ser: si tu clasificación es clara, no lo necesitas. Si estás en una frontera discutible —y el triaje de siniestros con humano en el circuito lo es— tiene sentido evaluarlo, porque el coste de equivocarse en la clasificación es rehacer el expediente entero.

Lo verificado el 31/08/2026, artículo por artículo.

**Existencia y plazo.** El **artículo 57, apartado 1**, en su redacción dada por el Reglamento (UE) 2026/1744, obliga a cada Estado miembro a que sus autoridades competentes establezcan al menos un espacio controlado de pruebas a escala nacional, «que estará operativo a más tardar el **2 de agosto de 2027**».

**Condiciones de acceso y duración.** No están en el reglamento. El **artículo 58, apartado 1**, remite «los criterios de admisibilidad y selección para participar» y las demás disposiciones detalladas a actos de ejecución de la Comisión. Quien prometa hoy una duración concreta de la participación se la está inventando.

**Efectos**, y es donde la venta suele exagerar. El **artículo 57, apartado 12**, dice las dos cosas a la vez: los participantes «responderán, con arreglo al Derecho de la Unión y nacional en materia de responsabilidad, de cualquier daño infligido a terceros», y, si respetan el plan y las condiciones y siguen de buena fe las orientaciones de la autoridad, «las autoridades no impondrán multas administrativas por infracciones del presente Reglamento». No es una exención de obligaciones: es un escudo frente a la multa, no frente al daño. Lo que sí se obtiene por escrito es el **informe de salida** del apartado 7, que las autoridades de vigilancia y los organismos notificados «tendrán en cuenta positivamente» para acelerar la evaluación de la conformidad.

**España.** El **Real Decreto 817/2023, de 8 de noviembre** (BOE-A-2023-22767) estableció un entorno controlado de pruebas anterior al reglamento, con la Secretaría de Estado de Digitalización e Inteligencia Artificial como órgano competente y una vigencia máxima de treinta y seis meses; su artículo 4.2 deja claro que participar no exime de cumplir. No es todavía el espacio del artículo 57.

**Ni la norma española del espacio del artículo 57 ni los actos de ejecución del artículo 58 están publicados**, de modo que hoy no hay autoridad designada, ni criterios de admisión, ni duración que puedan darse por buenos. Es en esas dos fuentes donde habrá que leerlos.

> Fuentes primarias a abrir: Reglamento (UE) 2024/1689, capítulo sobre medidas de apoyo a la innovación, en EUR-Lex; convocatorias y bases del espacio controlado de pruebas español, en el BOE.

### 23. Códigos de conducta voluntarios: para qué sirven de verdad

El reglamento fomenta códigos de conducta voluntarios para sistemas que **no** son de alto riesgo: compromisos de aplicar por decisión propia parte de los requisitos que la norma solo exige a la categoría superior.

La lectura cínica es que son marketing. La lectura útil es otra: un código de conducta es la forma de **ensayar el régimen de alto riesgo sin estar obligado**. Y eso resuelve un problema real de calendario.

Piénsalo desde Meridiana. Supón que en B2 concluís, con argumentos sólidos, que el sistema no es de alto riesgo. Buena noticia. Ahora supón que dentro de un año producto sube el umbral de aprobación automática y la conclusión se invierte. Si habéis mantenido voluntariamente la gestión de riesgos, la documentación técnica y los registros, la reclasificación es un trámite de semanas. Si no, es un proyecto de meses con el sistema ya en producción y el reloj corriendo.

Además tiene dos efectos colaterales que valen su coste:

- **Ante el asegurado y ante el mediador**, es un argumento comercial verificable, no un eslogan.
- **Ante una autoridad**, demuestra diligencia con hechos fechados.

El riesgo: adherirse a un código y no cumplirlo es peor que no adherirse. Un compromiso público incumplido es una prueba en tu contra, no una atenuante.

El régimen es el **artículo 95**, y confirma la lectura de esta slide. Su apartado 1 dice para qué sirven: fomentar «la aplicación **voluntaria** de alguno o de todos los requisitos establecidos en el capítulo III, sección 2, a los sistemas de IA que **no sean de alto riesgo**». Es decir, ensayar el régimen de alto riesgo sin estar obligado, literalmente. El apartado 2 añade una segunda familia de códigos, sobre requisitos específicos para todos los sistemas —directrices éticas, sostenibilidad medioambiental, alfabetización en IA, diseño inclusivo y prevención de perjuicios a personas vulnerables—, y exige que se construyan «sobre la base de objetivos claros e indicadores clave de resultados para medir la consecución de dichos objetivos». Quien redacte un código sin indicadores no ha leído el artículo.

Quién los elabora, apartado 3: «proveedores o responsables del despliegue de sistemas de IA particulares, por las organizaciones que los representen o por ambos», con participación de las partes interesadas. No hay una aprobación administrativa constitutiva: la Oficina de IA y los Estados miembros «fomentarán y facilitarán» su elaboración. Verificado en el texto consolidado de EUR-Lex el 31/08/2026.

Si a día de hoy hay códigos de conducta publicados que apliquen al sector asegurador es algo que no se ha comprobado al escribir esto, y por tanto no se afirma ni que los haya ni que no. Se mira en las publicaciones de la Comisión Europea y de EIOPA.

> Fuentes primarias a abrir: Reglamento (UE) 2024/1689, capítulo sobre códigos de conducta, en EUR-Lex; publicaciones de la Comisión Europea sobre códigos disponibles.

### 24. Interacción con la legislación sectorial de producto: no eres el único que regula tu sistema

El AI Act se superpone a marcos que ya existían y no los desplaza. Para una aseguradora española, el agente de siniestros vive simultáneamente bajo varios regímenes:

- **Distribución de seguros y protección del cliente**, con sus obligaciones de información y de tratamiento de reclamaciones.
- **Supervisión de la actividad aseguradora**, con exigencias de gobernanza interna, externalización y control de funciones críticas.
- **Protección de datos**, con su propio análisis y sus derechos ejercitables.
- **Defensa del consumidor**, cuando el asegurado actúa como tal.

La coordinación funciona en dos direcciones y conviene tenerlo claro:

**Hacia arriba:** cuando el sistema es un componente de seguridad de un producto ya regulado, el AI Act se apoya en el régimen sectorial en vez de duplicar la evaluación de conformidad.

**Hacia los lados:** cuando no hay esa relación, los regímenes son acumulativos. Nada de lo que hagas para el AI Act te exime de tus obligaciones como entidad aseguradora.

El error operativo que produce esto: **tres equipos escribiendo tres documentos sobre el mismo sistema sin hablarse**. El de riesgos, el de cumplimiento normativo y el de IA. La señal de alarma es fácil de detectar: pregunta a los tres cuántos sistemas de IA hay en producción. Si dan tres números distintos, no tienes un problema de AI Act, tienes un problema de inventario.

La articulación con la legislación de armonización de la Unión está en el **artículo 2** y en el **artículo 43**, y funciona en las dos direcciones que describe esta slide. **Hacia arriba**: cuando el sistema es de alto riesgo del artículo 6, apartado 1, y el producto está regulado por un acto del **Anexo I, sección A**, el **artículo 43, apartado 3** ordena que el proveedor «se atendrá al procedimiento de evaluación de la conformidad pertinente exigido» por ese acto sectorial, dentro del cual se evalúan los requisitos de la sección 2 del capítulo III; y el **artículo 2, apartado 13** —añadido por el Reglamento (UE) 2026/1744— permite incluso limitar la aplicación de los artículos 9 a 15 y 17 a 25 cuando el acto sectorial dé un nivel de protección equivalente o superior. Para los productos del **Anexo I, sección B**, el **artículo 2, apartado 2** va más lejos y deja aplicables solo el artículo 6, apartado 1, el artículo 60 bis y los artículos 102 a 112.

**Hacia los lados**, es decir, cuando no hay esa relación, los regímenes son acumulativos y el texto lo dice sin rodeos: el **artículo 2, apartado 7** deja intactos los Reglamentos (UE) 2016/679 y (UE) 2018/1725 y las Directivas 2002/58/CE y (UE) 2016/680, y el **apartado 9** añade que el reglamento «se entenderá sin perjuicio de las normas establecidas por otros actos jurídicos de la Unión relativos a la protección de los consumidores y a la seguridad de los productos». Verificado en el texto consolidado de EUR-Lex el 31/08/2026.

La lista concreta de normativa sectorial española aplicable a una aseguradora —ordenación, supervisión y solvencia, distribución de seguros y protección del cliente— **no se enumera aquí**: se consulta en el BOE. Una lista de normas citada de memoria en un curso de cumplimiento es justo el error que este bloque enseña a no cometer.

> Fuentes primarias a abrir: Reglamento (UE) 2024/1689, artículo de relación con otra normativa de la Unión, en EUR-Lex; normativa española de ordenación y supervisión de seguros, en el BOE; documentos publicados por EIOPA sobre IA en seguros.

### 25. Quién vigila el mercado y qué puede pedir: la inspección realista

La vigilancia del mercado es el mecanismo por el que la norma deja de ser papel. Una autoridad puede requerir información, acceder a documentación y, en determinados supuestos, exigir medidas correctoras o la retirada del sistema.

El marco es el **artículo 74**, y empieza por una remisión que conviene conocer: su apartado 1 declara aplicable a los sistemas de IA el **Reglamento (UE) 2019/1020**, de vigilancia del mercado, del que salen las facultades generales y los plazos procedimentales. Sobre esa base, el AI Act añade dos accesos propios y muy concretos.

El **apartado 12**: los proveedores concederán a las autoridades «pleno acceso a la documentación, así como a los conjuntos de datos de entrenamiento, validación y prueba utilizados para el desarrollo de los sistemas de IA de alto riesgo», incluso «a través de interfaces de programación de aplicaciones (API) o de otras herramientas y medios técnicos pertinentes que permitan el acceso a distancia».

El **apartado 13**, el que suele citarse mal: el acceso al **código fuente** no es libre. Se concede «previa solicitud motivada y solo si se cumplen las dos siguientes condiciones»: que sea necesario para evaluar la conformidad con los requisitos del capítulo III, sección 2, y que «se han agotado todos los procedimientos de prueba o auditoría» y las comprobaciones basadas en la información facilitada. Es un último recurso, no una puerta abierta. La confidencialidad de lo que se entregue está protegida por el **artículo 78**. Verificado en el texto consolidado de EUR-Lex el 31/08/2026.

Los plazos concretos de respuesta a un requerimiento **no están en el AI Act** y este material no los reproduce: los fija el Reglamento (UE) 2019/1020, que es donde hay que leerlos.

Con independencia del detalle, la forma de una inspección es previsible, y prepararse es barato si el sistema técnico ya está bien construido. Lo que se pide, en la práctica:

- **Qué sistemas de IA tienes.** Un inventario. Si tardas dos semanas en producirlo, la inspección ya ha empezado mal.
- **Cómo los clasificaste y con qué argumentos.** El documento de clasificación de B2.
- **Qué documentación técnica existe.** El expediente de B4.
- **Enséñame un caso.** La reconstrucción de una decisión concreta a partir de las trazas.
- **Qué haces cuando falla.** El registro de incidentes y las medidas adoptadas.

Fíjate en cuántas de esas cinco las responde el sistema y no un documento. El inventario sale de tu registro de despliegues; el caso concreto sale de las trazas; el fallo sale de tu registro de incidentes. **La inspección se prepara construyendo bien, no escribiendo bien.**

La única de las cinco que no puede improvisarse a posteriori es la traza: si no la guardaste en su momento, no existe.

> Fuentes primarias a abrir: Reglamento (UE) 2024/1689, capítulo sobre vigilancia del mercado y ejecución, en EUR-Lex; Reglamento (UE) 2019/1020 sobre vigilancia del mercado, en lo que resulte aplicable.

### 26. Bloque de cambios recientes, actualizable sin tocar el resto

Esta slide existe por una razón de mantenimiento, no de contenido: **aísla lo que caduca**. Un material normativo que mezcla lo estable con lo cambiante envejece entero a la vez y acaba retirándose.

Lo estable de este bloque —la lógica de producto, el reparto de papeles, el método de verificación, los errores frecuentes— aguanta años. Lo cambiante son fechas, cifras, directrices publicadas y anexos modificados por acto delegado. Todo lo cambiante debe poder actualizarse aquí y en las slides marcadas, sin reescribir el resto.

**Qué revisar en cada repaso del material:** correcciones de errores publicadas en el DOUE; directrices de la Comisión aparecidas desde la última revisión; actos delegados que modifiquen anexos; normativa española de designación y desarrollo; criterios publicados por la autoridad nacional y por la AEPD; documentos de EIOPA sobre IA en seguros.

La disciplina que hace que esto funcione es una convención de escritura, y aplica también a tu expediente interno:

- Cada afirmación normativa lleva **fuente, artículo y fecha de comprobación**.
- Las afirmaciones sin fecha se tratan como no verificadas, no como ciertas.
- Cuando una fuente cambia, se actualiza **la afirmación y su fecha**, no solo el enlace.

En el expediente de Meridiana esto se traduce en una tabla de trazabilidad: afirmación, fuente, fecha, quién la comprobó. Es aburrida y es lo primero que salva una discusión.

> Fuentes primarias a abrir: EUR-Lex (texto consolidado y actos delegados); DOUE (correcciones de errores); página de aplicación del AI Act de la Comisión Europea; BOE; AEPD; EIOPA.

### 27. Cómo mantenerse al día sin depender de resúmenes de terceros

Terminamos con el hábito que sostiene todo lo anterior, porque es lo único de este bloque que se practica cada semana.

El problema de los resúmenes no es que estén mal escritos. Es que **pierden la información que necesitas**: qué artículo exactamente, en qué versión del texto, con qué excepciones y desde cuándo. Un resumen te da una conclusión; una inspección te pide la cadena que lleva a ella.

Una rutina que funciona y cabe en una hora al mes:

1. **Una lista corta de fuentes primarias.** EUR-Lex para el texto consolidado, el DOUE para correcciones, la página de aplicación de la Comisión, el BOE para lo nacional y el supervisor sectorial. Cinco marcadores, no cincuenta.
2. **Una revisión mensual agendada**, con dueño. Media hora en el calendario de alguien con nombre.
3. **Un registro de cambios del expediente**, donde se anota qué se comprobó, qué cambió y qué documentos hubo que tocar.
4. **Los resúmenes de terceros, solo como alerta.** Sirven para enterarte de que algo ha pasado. Nunca como fuente citable. Si un boletín dice que ha cambiado algo, la acción es abrir la fuente primaria, no copiar la frase.

El indicador de que la rutina funciona es concreto: cuando alguien de dirección pregunte «¿nos afecta esto que ha salido hoy?», la respuesta llega en un día y con enlace, no en dos semanas y con un «parece que sí».

### 28. Ejercicio práctico 1: la ficha de ámbito de Meridiana {ejercicio:B1-ej1}

Rellena una ficha de una página que responda, para el agente de siniestros de Meridiana, a las preguntas de ámbito de este bloque. Sin clasificar todavía: eso es B2.

La ficha tiene cinco campos y ninguno admite una respuesta de una palabra:

1. **Componentes del sistema.** Enumera las partes que hacen inferencia y las que son código determinista, usando la separación del curso 3. Para cada una, una frase de qué hace.
2. **Ámbito territorial.** Dónde se pone en el mercado, dónde se usa el resultado y dónde está establecido cada proveedor de la cadena.
3. **Papeles.** Qué papel crees que juega Meridiana respecto de cada componente y por qué. Marca las dudas: se resuelven en B3.
4. **Exclusiones.** ¿Aplica alguna exclusión de ámbito? Justifica el «no» con la misma seriedad que justificarías un «sí».
5. **Fecha del primer caso real.** Si ya ha ocurrido, la fecha. Si no, qué evento la marcará.

**Criterio de aceptación:** cada afirmación normativa de la ficha lleva el marcador de verificación pendiente que usa este curso, o bien una cita con artículo y fecha de comprobación. Una ficha sin ningún marcador es una ficha en la que alguien escribió de memoria.

### 29. Ejercicio práctico 2: verificar una afirmación en la fuente primaria {ejercicio:B1-ej2}

Coge tres afirmaciones sobre el AI Act de un resumen público cualquiera —un boletín de un despacho, un artículo de prensa técnica, una entrada de blog— y verifícalas contra el texto en EUR-Lex.

Para cada una, produce cuatro líneas:

- La afirmación tal cual la leíste.
- El artículo o considerando concreto que la sostiene, o la constatación de que no lo encontraste.
- Si es exacta, imprecisa o falsa, y en qué.
- La fecha en que hiciste la comprobación y sobre qué versión del texto.

El objetivo del ejercicio no es cazar al que escribió el resumen. Es que compruebes cuánto se pierde en el camino: normalmente la excepción, el matiz sobre a quién se aplica, o la diferencia entre entrada en vigor y aplicación.

**Criterio de aceptación:** al menos una de las tres resulta ser imprecisa. Si las tres son exactas, has elegido afirmaciones demasiado genéricas; coge otras con número, fecha o plazo dentro.

### 30. Mini-quiz de comprensión — B1 {quiz:B1}

Tres preguntas sobre el razonamiento del bloque: qué tipo de norma es el AI Act, cómo convive con el RGPD y sobre qué se construye una afirmación normativa defendible.

Ninguna pregunta depende de recordar una fecha, un artículo o un importe. Si necesitas ese dato para trabajar, se abre la fuente; lo que no se puede consultar en el momento es el criterio.

Aprobado con dos aciertos. Puedes repetirlo las veces que quieras.

## Qué te llevas

- El AI Act regula productos puestos en el mercado, no datos personales.
- La categoría de riesgo no la elige quien construye: la determina el uso.
- El calendario es escalonado: qué te aplica depende de la fecha y del rol.

## Mini-quiz (3 preguntas)

Una sola opción correcta por pregunta, con explicación. Se generan con el mismo formato que `assets/js/quizzes-data.js` para que el importador las recoja.

1. **Tema:** Qué distingue un sistema de alto riesgo de uno de riesgo limitado
   - **Enunciado:** Dos sistemas de Meridiana: un chat que responde dudas generales sobre la póliza y un componente que decide la vía de tramitación de un siniestro. ¿Qué es lo que determina que puedan caer en categorías distintas?
   - **Opciones:**
     - a) El tamaño del modelo y la cantidad de datos con los que se entrenó.
     - b) **El uso previsto y el contexto en el que se emplea, no la tecnología ni su complejidad.** ✅
     - c) Que uno hable con el cliente y el otro no: interactuar con personas es siempre lo que eleva la categoría.
     - d) Que el equipo que lo construyó lo declare como tal en su documentación interna.
   - **Explicación:** La categoría depende del uso y del contexto, no de la tecnología. La (a) confunde potencia con riesgo: un modelo pequeño en un uso sensible puede ser alto riesgo y uno enorme en un buscador interno no. La (c) describe las obligaciones de transparencia, que son otra cosa: interactuar con personas obliga a decirlo, no convierte el sistema en alto riesgo. La (d) invierte la lógica: la clasificación se justifica, no se elige.

2. **Tema:** Qué relación tiene el AI Act con el RGPD en un mismo sistema
   - **Enunciado:** El delegado de protección de datos de Meridiana dice que la evaluación de impacto ya hecha para el agente de siniestros cubre el AI Act. ¿Cómo se responde?
   - **Opciones:**
     - a) Es correcto: si el tratamiento de datos es lícito, el sistema es conforme.
     - b) Es incorrecto, porque el AI Act sustituye al RGPD para los sistemas que caen en su ámbito.
     - c) **Es incorrecto: son marcos acumulativos que responden a preguntas distintas, aunque el análisis del RGPD sea un insumo reutilizable del expediente.** ✅
     - d) Es correcto siempre que no se traten categorías especiales de datos.
   - **Explicación:** El RGPD pregunta si puedes tratar esos datos personales y con qué garantías; el AI Act pregunta si el sistema es apto para ponerse en el mercado y usarse, y qué puedes demostrar. Se cumplen los dos o se incumple alguno. La (b) es falsa: ninguno desplaza al otro. La (a) y la (d) reducen el AI Act a una cuestión de datos, que es justamente el error que este bloque combate. Lo que sí es cierto, y está en la (c), es que el inventario, el análisis de riesgos y el registro de proveedores se reutilizan en vez de duplicarse.

3. **Tema:** Sobre qué base se sostiene una afirmación normativa defendible
   - **Enunciado:** En el expediente de Meridiana aparece la frase «los registros de funcionamiento deben conservarse durante el plazo legalmente previsto». ¿Qué le falta para ser utilizable ante una autoridad?
   - **Opciones:**
     - a) Nada: es prudente precisamente por no comprometerse con un plazo concreto.
     - b) **La fuente concreta, el precepto que la sostiene y la fecha en que se comprobó en la fuente primaria.** ✅
     - c) La firma del director de tecnología, que es quien responde del sistema.
     - d) Una referencia al boletín jurídico donde el equipo leyó la obligación.
   - **Explicación:** Una afirmación normativa sin fuente, precepto y fecha de comprobación no se puede defender ni mantener: nadie sabe si sigue siendo cierta. La (a) confunde vaguedad con prudencia; lo prudente es marcar el dato como pendiente de verificar, no redactarlo de forma que no se pueda comprobar. La (c) añade responsabilidad sin añadir evidencia. La (d) es el hábito que este bloque desmonta: un resumen de terceros sirve como alerta, nunca como fuente citable.

## Lab

Este curso no lleva labs de código. En su lugar, ejercicio de plantilla (DOCX/XLSX) sobre el caso Meridiana.

**Enunciado.** Construye la **ficha de ámbito y trazabilidad normativa** de Meridiana: un documento de dos páginas más una hoja de cálculo, que será la primera pieza del expediente que se completa en B2 y B4. No se clasifica todavía; se delimita qué es el sistema, dónde está y qué habrá que verificar.

**Pasos:**

1. En la hoja `sistema`, enumera los componentes del agente separando los que hacen inferencia de los que son código determinista. Reutiliza la tabla de decisiones del curso 3: qué decide el modelo y qué decide el código.
2. En la hoja `cadena`, lista los proveedores implicados —modelo, alojamiento, observabilidad, almacenamiento— con su papel y dónde están establecidos. Marca en rojo aquel del que no tengas documentación técnica publicada.
3. En el documento, redacta el apartado de ámbito territorial y material: dos párrafos, marcando como **pendiente de comprobar** cada afirmación normativa que no hayas abierto en EUR-Lex.
4. En la hoja `trazabilidad`, crea las columnas: afirmación · fuente · artículo o considerando · fecha de comprobación · quién lo comprobó · estado. Vuelca ahí todas las afirmaciones que dejaste pendientes en el paso anterior, con estado «pendiente».
5. Añade el apartado de exclusiones: para cada una de las tres del bloque, una frase justificando por qué aplica o por qué no. El «no» se justifica igual que el «sí».
6. Cierra con la fecha del primer caso real de Meridiana, o con el evento que la marcará.

**Criterios de aceptación:**

- Ninguna afirmación normativa del documento carece de cita con fecha o de la marca de pendiente. Cero excepciones.
- La hoja `trazabilidad` tiene tantas filas como marcadores hay en el documento. Se comprueba contándolos.
- Cada componente de la hoja `sistema` está clasificado como inferencia o determinista, y ninguno queda sin clasificar.
- Una persona que no haya trabajado en el proyecto puede leer la ficha y decir qué hace el sistema y quién lo opera.
- Se completa en menos de 90 minutos con la documentación del curso 3 delante.

**Solución de referencia:** en `content/caso/soluciones/C-09/B1/`, con la ficha rellenada sobre el agente tal y como queda al final del curso 3 y la hoja de trazabilidad con los marcadores ya volcados.

## Cierre

- Recapitulación en los tres puntos de arriba.
- Mini-quiz.
- Enlace al siguiente bloque.
