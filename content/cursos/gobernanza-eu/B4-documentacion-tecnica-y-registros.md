# C-12 · B4 · Documentación técnica y registros

> Curso: `gobernanza-eu` · bloque `B4`

## Objetivo

Construir el expediente técnico y el registro de eventos que una inspección pediría, con lo que el sistema ya produce.

## ⚠ Bloque normativo: verificación obligatoria antes de publicar

Cada afirmación de este bloque debe citar artículo y fecha de verificación, y esa fecha debe salir de haber abierto la fuente primaria. No se redacta de memoria ni a partir de resúmenes de terceros.

**Fuentes primarias a consultar:**

- [x] AI Act, Anexo IV (documentación técnica) — puntos 1 a 9, y art. 11 que remite a él
- [x] AI Act, artículos sobre registro de eventos y conservación — arts. 12, 18, 19, 21 y 26, apdo. 6
- [ ] Normas armonizadas publicadas, si las hay — no verificadas; el art. 40 prevé su publicación en el DOUE, pero no se ha comprobado cuáles están publicadas

**Fecha de verificación:** 2026-08-31 · sobre el texto consolidado `02024R1689 — ES — 27.07.2026 — 001.001`, que incorpora el Reglamento (UE) 2026/1744

> Aviso al alumno que debe aparecer en el bloque: este material es formativo y no constituye asesoramiento jurídico.

## Guion de slides

27 slides de contenido. Una idea por slide, con un ejemplo real o del caso Meridiana. Nada de relleno.

### 1. Qué es el expediente técnico y para quién se escribe

El expediente técnico es el documento con el que un proveedor demuestra que su sistema cumple los requisitos que se le exigen. No es un manual, no es una memoria de proyecto y no es material de marketing: es **la prueba de que alguien pensó, midió y decidió**, con fechas y nombres.

La pregunta que ordena todo el bloque es *para quién se escribe*, porque de ahí sale el tono. Sus lectores son tres, y ninguno es tu equipo:

- **Una autoridad de vigilancia del mercado**, que llega sin conocer tu negocio y quiere comprobar afirmaciones concretas.
- **Un evaluador externo**, si la vía de evaluación de conformidad lo requiere.
- **Tú mismo dentro de dos años**, cuando el que escribió el sistema ya no trabaje ahí y haya que explicar por qué el agente derivó un siniestro de 2026.

De ese tercer lector sale el argumento que hace que este bloque no sea burocracia. La pregunta incómoda del caso —«¿cómo se demuestra ante una reclamación por qué se propuso ese importe?»— no se contesta con una política de empresa. Se contesta con documentación de diseño más la traza de una ejecución concreta.

Los tres lectores no son una metáfora: dos de ellos están en el texto. El **artículo 11, apdo. 1**, exige que la documentación técnica se redacte «de modo que demuestre que el sistema de IA de alto riesgo cumple los requisitos establecidos en la presente sección y que proporcione de manera clara y completa **a las autoridades nacionales competentes y a los organismos notificados** la información necesaria para evaluar la conformidad». El **artículo 18, apdo. 1**, obliga al proveedor a mantenerla a disposición de las autoridades nacionales competentes durante **diez años** desde la introducción en el mercado o la puesta en servicio. Y el **artículo 21, apdo. 1**, es el que fija la lengua: previa solicitud motivada, el proveedor entrega la información y la documentación «en una lengua que la autoridad pueda entender fácilmente y que sea una de las lenguas oficiales de las instituciones de la Unión, indicada por el Estado miembro de que se trate»; su apdo. 2 añade el acceso a los archivos de registro cuando estén bajo su control.

Dos cosas que el texto **no** dice, y que por eso no se escriben aquí: el art. 21 no fija un plazo numérico de respuesta al requerimiento, y el art. 11 no impone un formato concreto para el expediente —lo único que menciona es un formulario simplificado que la Comisión establecerá para pymes, empresas emergentes y pequeñas empresas de mediana capitalización, que los organismos notificados deberán aceptar. Verificado el 31/08/2026.

Y la tesis del bloque, que se repetirá hasta el final: **un expediente que no se alimenta de lo que el sistema ya produce está desactualizado el mes siguiente**. Si mantenerlo requiere que alguien copie números a mano cada trimestre, no se mantendrá.

> Fuentes primarias a abrir: AI Act, artículo sobre documentación técnica y Anexo IV; artículos sobre obligaciones de puesta a disposición ante las autoridades competentes.

### 2. El índice del expediente, sección por sección

El contenido mínimo del expediente técnico está tasado en el Anexo IV. No se inventa un índice propio: se sigue el del anexo y se rellena.

El **anexo IV** tiene nueve puntos, en este orden: **1.** una descripción general del sistema, con ocho letras que van de la finalidad prevista, el nombre del proveedor y la versión [a)] a las instrucciones de uso y la descripción básica de la interfaz [h)], pasando por cómo interactúa con otro hardware o software [b)], las versiones de software o firmware [c)], **todas las formas en que se introduce en el mercado o se pone en servicio, «como paquetes de software integrados en el hardware, descargas o API»** [d)], el hardware previsto [e)] y, si es componente de un producto, fotografías o ilustraciones [f)]. **2.** una descripción detallada de los elementos del sistema y de su proceso de desarrollo, con ocho letras: métodos y medidas de desarrollo, **incluido el recurso a sistemas o herramientas previamente entrenados facilitados por terceros y cómo se han utilizado, integrado o modificado** [a)]; especificaciones de diseño y decisiones clave [b)]; arquitectura y recursos informáticos [c)]; requisitos en materia de datos [d)]; evaluación de las medidas de supervisión humana del art. 14 [e)]; **cambios predeterminados** [f)]; procedimientos de validación y prueba, con «los archivos de registro de las pruebas y todos los informes de las pruebas **fechados y firmados** por las personas responsables» [g)]; y medidas de ciberseguridad [h)]. **3.** información sobre supervisión, funcionamiento y control, incluidos los niveles de precisión. **4.** una descripción de la idoneidad de los parámetros de rendimiento. **5.** una descripción detallada del sistema de gestión de riesgos del art. 9. **6.** una descripción de los cambios pertinentes realizados a lo largo del ciclo de vida. **7.** la lista de normas armonizadas aplicadas, o la descripción detallada de las soluciones adoptadas si no se aplicaron. **8.** una copia de la declaración UE de conformidad del art. 47. **9.** una descripción detallada del sistema de evaluación poscomercialización del art. 72, incluido su plan.

El artículo que remite al anexo es el **11**, y sí contiene la precisión para pymes que anticipaba la línea de fuentes: su apdo. 1 permite a las pymes —incluidas las empresas emergentes— y a las pequeñas empresas de mediana capitalización presentar los elementos del anexo IV «de manera simplificada», mediante un formulario que establecerá la Comisión y que los organismos notificados aceptarán. Verificado el 31/08/2026.

Lo que sí se puede enseñar es **cómo agrupar el trabajo** para que sea abordable, porque el anexo se lee como una lista larga y desanima. Las secciones caen en cuatro familias, y cada una tiene un dueño natural distinto:

| Familia | Qué contesta | Quién la escribe |
|---|---|---|
| Descripción | Qué es el sistema y para qué sirve | Producto y negocio |
| Diseño | Cómo está construido y por qué así | Ingeniería |
| Evidencia | Qué se midió y con qué resultado | Ingeniería y calidad |
| Operación | Cómo se usa, se vigila y se mantiene | Operaciones y cumplimiento |

Dos consejos de método que valen más que la lista:

- **Empieza por la familia de evidencia.** Es la que más tarda en producirse y la única que no se puede escribir la noche antes. Descripción y diseño se redactan en una semana; los resultados de evaluación necesitan que existan evaluaciones.
- **Un fichero por sección, no un documento único de ochenta páginas.** Un documento monolítico lo mantiene una persona, en su portátil, hasta que se va. Ficheros separados en el repositorio los mantiene el equipo que toca cada parte.

> Fuentes primarias a abrir: AI Act, Anexo IV completo en la versión española del DOUE; artículo que remite al anexo y precisa su alcance para PYMES, si existe tal precisión.

### 3. Descripción del sistema y finalidad prevista

La primera sección es la más fácil de escribir mal, porque parece trivial. Describir el sistema no es contar qué tecnología usa: es **fijar el perímetro dentro del cual afirmas que funciona**. Todo lo demás del expediente se juzga contra esa frase.

Una descripción útil contesta cinco cosas y no divaga:

1. **Qué hace.** «Extrae datos estructurados de avisos de siniestro de auto en texto libre, propone una vía de tramitación y redacta la petición de documentación al asegurado.»
2. **Para quién.** Tramitadores de siniestros de una aseguradora, con formación en el proceso.
3. **Sobre quién produce efectos.** Los asegurados. Este punto se olvida en nueve de cada diez descripciones y es el que más importa.
4. **Dónde termina su intervención.** La propuesta de resolución la aprueba siempre una persona. El agente nunca paga.
5. **En qué condiciones.** Volumen esperado, canales de entrada, idioma, tipo de póliza.

La finalidad prevista es una declaración con consecuencias, no una descripción amable: es el perímetro que, si alguien lo cruza, cambia roles y obligaciones (B3, slide 9). Por eso conviene escribirla **estrecha**. Una finalidad amplia —«asistente de siniestros»— parece prudente y es lo contrario: te obliga a responder de usos que nunca evaluaste.

El **anexo IV, punto 1**, es el que manda en esta sección, y pide menos de lo que la gente teme y más de lo que suele escribir: la **finalidad prevista**, el nombre del proveedor y la versión del sistema «de tal manera que se refleje su relación con versiones anteriores» [a)]; cómo interactúa o puede utilizarse para interactuar con hardware o software que no forme parte del sistema, incluidos otros sistemas de IA [b)]; las versiones de software o firmware y los requisitos de actualización [c)]; **todas** las formas en que se introduce en el mercado o se pone en servicio [d)]; el hardware previsto [e)]; y la descripción básica de la interfaz de usuario facilitada al responsable del despliegue [g) y h)].

Sobre la distinción que preguntaba el marcador: el anexo IV, punto 1, **no** separa finalidad prevista, condiciones de uso y usos indebidos como tres epígrafes. La razón es que las «condiciones de uso concretas» ya forman parte de la definición de finalidad prevista (art. 3, punto 12), y el **uso indebido razonablemente previsible** aparece en otros sitios del expediente: en el sistema de gestión de riesgos (art. 9, apdo. 2, letra b), que llega al anexo IV por su punto 5) y en las instrucciones de uso (art. 13, apdo. 3, letra b), inciso iii). El punto 3 del anexo IV recoge, además, «los resultados no deseados previsibles». Verificado el 31/08/2026.

> Fuentes primarias a abrir: AI Act, Anexo IV, punto de descripción general del sistema; definiciones de «finalidad prevista» y «uso indebido razonablemente previsible».

### 4. Datos: procedencia, tratamiento y limitaciones conocidas

La sección de datos es donde más gente se bloquea con un argumento aparentemente sólido: «nosotros no entrenamos nada, usamos un modelo por API, aquí no hay nada que contar».

Es falso, y por dos motivos. El primero: los datos que sí controlas son **los de entrada y los de evaluación**, y sobre esos tienes que responder. El segundo: lo que no controlas —los datos de entrenamiento del modelo de terceros— se documenta como **procedencia externa con lo que el proveedor haya declarado y con lo que no**, que es información en sí misma.

Lo que Meridiana tiene que poder describir:

- **Datos de entrada.** Relatos de FNOL en texto libre por web, app y transcripción telefónica; datos de póliza y coberturas del sistema de gestión; documentos adjuntos por el asegurado.
- **Tratamiento previo.** Qué se normaliza, qué se recorta, qué se seudonimiza antes de salir hacia el modelo. Se cruza con B5 y no se duplica: se enlaza.
- **Conjunto de evaluación.** Los 31 casos sintéticos: cómo se construyeron, con qué semilla, quién decidió las etiquetas correctas y con qué criterio. Esto es del bloque B3 del curso 3 y aquí se referencia tal cual.
- **Limitaciones conocidas de los datos.** El conjunto es sintético; cubre auto y no otros ramos; el sesgo de canal existe porque la transcripción telefónica tiene una calidad distinta a la del formulario web.

El texto respalda el argumento de esta slide con más precisión de la esperada. El **anexo IV, punto 2, letra d)**, pide los requisitos en materia de datos «cuando proceda, en forma de **fichas técnicas** que describan las metodologías y técnicas de entrenamiento, así como los conjuntos de datos de entrenamiento utilizados, e incluyan una descripción general de dichos conjuntos e información acerca de **su procedencia, su alcance y sus características principales**; la manera en que se obtuvieron y seleccionaron los datos; los procedimientos de etiquetado y las metodologías de depuración». Y su letra g) pide los procedimientos de validación y prueba, con la información sobre los **datos de validación y prueba** y sus características principales. Ese «cuando proceda» es lo que modula la sección para quien no entrena.

Para el caso de Meridiana hay dos anclajes mejores todavía. El **anexo IV, punto 2, letra a)**, obliga a describir los métodos de desarrollo «incluido, en su caso, **el recurso a sistemas o herramientas previamente entrenados facilitados por terceros y la manera en que han sido utilizados, integrados o modificados por el proveedor**»: ahí es exactamente donde se documenta el modelo de propósito general con su versión y su procedencia. Y el **artículo 10, apdo. 6** —redacción del Reglamento (UE) 2026/1744— cierra la duda de fondo: para el desarrollo de sistemas de alto riesgo «que no emplean técnicas que implican el entrenamiento de modelos de IA», las prácticas de gobernanza de datos de los apdos. 2, 3 y 4 se aplican **únicamente a los conjuntos de datos de prueba**. Traducido: quien no entrena no queda exento, queda enfocado en su conjunto de evaluación. Verificado el 31/08/2026.

> Fuentes primarias a abrir: AI Act, Anexo IV, puntos sobre datos y conjuntos de datos; artículo sobre gobernanza y gestión de datos para sistemas de alto riesgo.

### 5. Arquitectura y decisiones de diseño

Aquí es donde el curso 3 empieza a pagar. La sección de arquitectura no pide un diagrama bonito: pide **explicar por qué el sistema está construido así y qué garantiza esa construcción**.

Y da la casualidad de que la arquitectura de referencia del curso 3 fue diseñada, por otros motivos, para poder contestar exactamente eso. Las cuatro capas —gateway, orquestador, tools y memoria— no son una preferencia estética: son la explicación de dónde vive cada decisión.

Lo que se documenta, con nombre y ubicación en el código:

- **Qué decide el modelo y qué decide el código.** La tabla de la slide 8 del bloque B1 del curso 3 es, literalmente, una sección del expediente. Dice que el modelo extrae y el código decide, y que la derivación por lesiones personales es una regla determinista.
- **Dónde está esa regla.** Módulo, función, test que la cubre, test de arquitectura que impide moverla a un prompt.
- **Las fronteras de confianza.** Que el relato del asegurado es dato y nunca instrucción, y qué medida concreta lo garantiza.
- **El puerto del modelo.** `ILlmClient`, con qué implementación y qué versión de modelo se usa hoy.

El argumento que hay que saber defender ante un auditor: **el sistema no delega en un modelo probabilístico ninguna decisión con consecuencia regulada**, y eso no es una promesa, es una propiedad comprobable en el código y en los tests de arquitectura. Esa frase, respaldada por tres ficheros, vale más que veinte páginas de descripción de red neuronal.

Si tu sistema no puede escribir esta sección, el problema no es documental: es que nadie sabe dónde vive cada decisión.

### 6. Métricas de rendimiento y cómo se obtuvieron

Una métrica sin metodología no es evidencia; es una cifra. La sección de rendimiento se juzga por lo segundo, no por lo primero: un 94 % sin decir sobre qué conjunto, con qué versión y quién puso las etiquetas no demuestra nada.

Lo que va en esta sección, para cada métrica:

- **Qué mide**, en una frase que un no técnico entienda.
- **Sobre qué conjunto**, con su versión y su tamaño.
- **Con qué versión del sistema**: modelo, versión de prompt, revisión de código.
- **Cuándo se midió**, con fecha.
- **Quién decidió la respuesta correcta** y con qué criterio de desempate.

Y una advertencia que en un contexto regulado importa más que en uno técnico: **el agregado oculta lo que más duele**. Un 95 % de acierto global con el 100 % de los casos de lesiones mal clasificados es un sistema que no debe estar en producción, y el número grande no lo dice. En el expediente hay que publicar la **segmentación** —por vía, por canal, por dificultad— y en particular el rendimiento sobre los casos que activan las reglas críticas.

Meridiana tiene ahí una ventaja que no todo el mundo aprecia: sus 31 casos incluyen a propósito uno con lesiones, uno con fecha fuera de vigencia, uno con matrícula ilegible, uno con intento de inyección y dos duplicados. Ese diseño deliberado convierte el conjunto en algo que se puede citar por casos concretos, no solo por porcentajes.

### 7. Los evals del curso 3 como evidencia

Los evals continuos del bloque B3 del curso 3 se construyeron para no desplegar regresiones. Resulta que son, además, **la mejor evidencia disponible** para el expediente técnico, y con dos ajustes pequeños pasan de una cosa a la otra.

Qué aportan tal cual:

- **Resultados fechados y reproducibles** sobre un conjunto versionado con semilla fija.
- **Ejecución automática en cada cambio**, que demuestra vigilancia continua y no una foto puntual.
- **Umbrales de bloqueo**, que demuestran que existe un criterio previo y no una interpretación posterior de los números.
- **Segmentación por caso difícil**, que es justo lo que un auditor va a querer mirar.

Qué hay que añadir para que sirvan como evidencia:

1. **Conservar los informes, no solo el semáforo.** CI suele guardar «pasa/no pasa» y tirar el detalle. Para el expediente hace falta el resultado por caso, archivado.
2. **Que cada informe identifique la versión completa del sistema**: revisión de código, versión de prompt y versión de modelo. Sin eso, el informe no se puede atribuir a nada.
3. **Que el archivo sea inmutable y fechado.** Un informe que se puede reescribir no prueba nada (slide 25).

El coste de estos tres ajustes es de horas. El valor es que la sección más difícil del expediente —la de evidencia— se rellena sola cada noche, que es exactamente la tesis del bloque.

### 8. Gestión de riesgos: identificación, evaluación y mitigación

La gestión de riesgos que exige el reglamento no es un documento: es **un proceso continuo a lo largo de todo el ciclo de vida**, con identificación, estimación, mitigación y comprobación de que la mitigación funciona. El **artículo 9** lo dice casi con esas palabras: el sistema de gestión de riesgos «se entenderá como un **proceso iterativo continuo planificado y ejecutado durante todo el ciclo de vida** de un sistema de IA de alto riesgo, que requerirá revisiones y actualizaciones sistemáticas periódicas» [apdo. 2], y consta de cuatro etapas: determinación y análisis de los riesgos conocidos y previsibles para la salud, la seguridad o los derechos fundamentales [a)]; estimación y evaluación de los riesgos [b)]; evaluación de otros riesgos a partir de los datos de la vigilancia poscomercialización del art. 72 [c)]; y adopción de medidas adecuadas y específicas de gestión de riesgos [d)].

Las dos preguntas del marcador tienen respuesta expresa. Sobre el **uso indebido razonablemente previsible**: sí, obliga. La letra b) del apdo. 2 manda evaluar los riesgos que podrían surgir tanto cuando el sistema se utiliza conforme a su finalidad prevista «**como cuando se le dé un uso indebido razonablemente previsible**». Sobre los **grupos vulnerables**: el apdo. 9 exige que, al implantar el sistema, los proveedores presten atención a si «es probable que el sistema de IA de alto riesgo afecte negativamente a las personas **menores de dieciocho años** y, en su caso, a otros colectivos vulnerables». Y dos límites útiles para no inflar el registro: el apdo. 3 acota los riesgos a aquellos «que pueden mitigarse o eliminarse razonablemente mediante el desarrollo o el diseño del sistema o el suministro de información técnica adecuada», y el apdo. 8 exige que las pruebas se hagan «utilizando parámetros y umbrales de probabilidades **previamente definidos**» —que es, palabra por palabra, el argumento de los umbrales de bloqueo en CI. Verificado el 31/08/2026.

Lo enseñable sin fuente es la **mecánica**, y es más simple de lo que parece. Cada riesgo se escribe en una fila con cinco columnas: qué puede salir mal, a quién le pasa, qué probabilidad y qué impacto le atribuyes, qué has hecho para reducirlo, y **cómo compruebas que la mitigación sigue funcionando**. La quinta columna es la que separa un registro de riesgos vivo de uno decorativo.

Cuatro riesgos reales de Meridiana, con su mitigación y su comprobación:

- **Extraer mal la fecha del siniestro y cambiar la cobertura.** Mitigación: validación contra la vigencia de la póliza y derivación si no cuadra. Comprobación: caso específico en el conjunto de evaluación.
- **No detectar lesiones personales.** Mitigación: la duda deriva igual que el sí. Comprobación: eval segmentado, umbral de bloqueo en CI.
- **Inyección de instrucciones en el relato del asegurado.** Mitigación: el modelo no decide vía ni importe, así que no hay nada que ganar. Comprobación: conjunto de evals de seguridad aparte.
- **Cambio de versión del modelo por el proveedor.** Mitigación: versión anclada y aviso contractual. Comprobación: evals nocturnos que detectan la deriva.

Fíjate en que las cuatro comprobaciones ya existían por motivos de ingeniería. El registro de riesgos no las inventa: las nombra y las conecta con el riesgo que justifican.

> Fuentes primarias a abrir: AI Act, artículo sobre el sistema de gestión de riesgos para sistemas de alto riesgo; Anexo IV, punto sobre gestión de riesgos.

### 9. Supervisión humana: cómo se implementa y cómo se demuestra

Escribir «hay un humano que revisa» es la afirmación menos verificable de todo el expediente, y por eso es la que más se comprueba. La sección tiene que contestar dos preguntas distintas: **cómo está diseñada** la supervisión y **cómo se demuestra** que ocurre.

El diseño, en Meridiana, es concreto y se puede citar: el agente propone y una persona aprueba siempre; por debajo de 1.500 € la aprobación es un clic y por encima hay revisión completa; cualquier indicio de lesiones personales sale del flujo automático y entra en la cola humana.

Lo que hay que documentar del diseño:

- **Qué puede hacer la persona**: aceptar, modificar, rechazar, devolver al flujo. Si solo puede aceptar, no hay supervisión.
- **Qué información ve** en el momento de decidir: la propuesta, el relato original, los datos extraídos y **la marca de qué campos son inciertos**.
- **Qué formación tiene** y quién se la dio.
- **Cuánto tiempo dispone**, que es la variable que convierte la supervisión en un trámite. Con 88 siniestros al día y 24 tramitadores el número sale; con una plantilla a la mitad, no.

La demostración es lo que casi nadie prepara: **la tasa de desviación**. Qué porcentaje de propuestas se modifican o se rechazan. Si es cero durante meses, o el sistema es perfecto o nadie está revisando de verdad, y la segunda hipótesis es la que va a suponer quien lea el expediente.

El **artículo 14** reparte las medidas en dos tipos, y las dos son responsabilidad del proveedor. Su apdo. 3 dice que la supervisión se garantizará «bien mediante uno de los siguientes tipos de medidas, bien mediante ambos»: **a)** las que el proveedor defina e **integre en el sistema** antes de introducirlo en el mercado, cuando sea técnicamente viable; y **b)** las que el proveedor defina antes de esa fecha y «que sean adecuadas para que las ponga en práctica el responsable del despliegue». Es decir, lo organizativo no es un cajón donde tirar lo que no se quiso implementar: es una medida que el proveedor **también** tiene que haber definido y documentado.

El apdo. 4 concreta qué debe poder hacer la persona que supervisa, y su lista es la que hay que citar en el expediente: entender las capacidades y limitaciones y vigilar el funcionamiento [a)]; ser consciente de la posible tendencia a confiar en exceso en los resultados, «**sesgo de automatización**», en particular en sistemas que aportan información o recomendaciones para que una persona decida [b)]; interpretar correctamente los resultados [c)]; **decidir no utilizar el sistema o descartar, invalidar o revertir su salida** [d)]; e intervenir o interrumpir el sistema mediante un botón de parada o un procedimiento similar [e)]. La letra d) es la que convierte en incumplimiento la interfaz donde el tramitador solo puede aceptar.

Sobre la interfaz: el art. 14, apdo. 1, exige dotar al sistema de «herramientas de interfaz humano-máquina adecuadas», y el anexo IV la pide por escrito en tres sitios —punto 1, letras g) y h), «una descripción básica de la interfaz de usuario facilitada al responsable del despliegue»; punto 2, letra e), «una evaluación de las medidas de supervisión humana necesarias de conformidad con el artículo 14»; y punto 3, las medidas técnicas para facilitar la interpretación de los resultados. Verificado el 31/08/2026.

> Fuentes primarias a abrir: AI Act, artículo sobre supervisión humana en sistemas de alto riesgo; Anexo IV, punto sobre medidas de supervisión humana.

### 10. Robustez, exactitud y ciberseguridad

Tres propiedades que el reglamento trata juntas y que en un sistema con LLM significan cosas parcialmente nuevas. El artículo que las agrupa es el **15**, «Precisión, solidez y ciberseguridad», y exige diseñar y desarrollar los sistemas «de modo que alcancen un nivel adecuado de precisión, solidez y ciberseguridad y funcionen de manera uniforme en esos sentidos **durante todo su ciclo de vida**» [apdo. 1].

Qué niveles hay que declarar, y dónde: el apdo. 3 dice que «en las instrucciones de uso que acompañen a los sistemas de IA de alto riesgo **se indicarán los niveles de precisión de dichos sistemas, así como los parámetros pertinentes para medirla**». No basta la cifra: el propio texto pide el parámetro con el que se ha medido, que es justo la tesis de la slide 6.

Sobre las normas armonizadas, cuidado con el atajo: el art. 15 **no** remite a ellas para medir. Su apdo. 2 encarga a la Comisión, en cooperación con las partes interesadas y con las autoridades de metrología y de evaluación comparativa, «fomentar el desarrollo de **parámetros de referencia y metodologías de medición**» —es decir, todavía no existen por esta vía. La presunción de conformidad por normas armonizadas es un mecanismo general del art. 40, y el art. 42, apdo. 2, añade una presunción específica en ciberseguridad para los sistemas certificados conforme al Reglamento (UE) 2019/881. Los vectores que el propio art. 15, apdo. 5, nombra son los que se documentan aquí: envenenamiento de datos y de modelos, «ejemplos adversarios» o evasión de modelos, y ataques a la confidencialidad. Verificado el 31/08/2026.

Lo que sí se puede desarrollar es qué se documenta bajo cada una en un sistema como el de Meridiana:

- **Exactitud.** Los niveles declarados y su metodología (slide 6). El matiz específico: en un sistema no determinista, la exactitud se declara **por segmento y con su conjunto de referencia**, no como un número único con dos decimales.
- **Robustez.** Qué pasa con entradas raras, largas, en otro idioma, con ruido de transcripción o directamente vacías. Y qué pasa con los fallos de infraestructura: el comportamiento cuando el proveedor devuelve error durante once minutos, que en el curso 3 se llamó degradación y aquí es una propiedad documentada. Que el portal siga aceptando siniestros con el modelo caído es una afirmación comprobable, y se comprueba apagando el proveedor a propósito.
- **Ciberseguridad.** Además de lo habitual, los vectores propios: inyección de instrucciones a través del relato del asegurado, extracción de datos de otros expedientes vía contexto, y abuso de las tools como camino hacia efectos reales. La respuesta arquitectónica —que el modelo no decide nada con consecuencias— se documenta aquí como control de seguridad, porque lo es.

El material de esta sección sale entero de los bloques B5 y B7 del curso 3. Lo que hay que hacer es **reescribirlo para un lector que no conoce tu sistema**, no copiarlo.

> Fuentes primarias a abrir: AI Act, artículo sobre exactitud, solidez y ciberseguridad; Anexo IV, puntos correspondientes; normas armonizadas publicadas sobre estos requisitos, si las hay.

### 11. Registro automático de eventos: qué exige y qué no

El sistema de alto riesgo debe registrar automáticamente eventos a lo largo de su ciclo de vida, con el fin de permitir la trazabilidad de su funcionamiento y facilitar la vigilancia posterior.

El artículo es el **12**, «Conservación de registros», y su redacción respalda punto por punto lo que viene después. El apdo. 1 obliga a que los sistemas «permitirán técnicamente el registro automático de acontecimientos (en lo sucesivo, "archivos de registro") **a lo largo de todo el ciclo de vida** del sistema». El apdo. 2 no enumera campos: fija **para qué** deben servir las capacidades de registro, y son tres finalidades —«un nivel de trazabilidad del funcionamiento que resulte adecuado para la finalidad prevista»— a saber, detectar situaciones que puedan hacer que el sistema presente un riesgo en el sentido del art. 79, apdo. 1, **o dar lugar a una modificación sustancial** [a)]; facilitar la vigilancia poscomercialización del art. 72 [b)]; y permitir la vigilancia del funcionamiento por el responsable del despliegue del art. 26, apdo. 5 [c)]. Que la obligación esté escrita por finalidad y no por lista de campos es exactamente lo que permite —y obliga a— diseñar el registro en vez de volcarlo todo.

¿Requisitos específicos por tipo de sistema? Solo uno: el apdo. 3 fija un contenido mínimo para los sistemas del **anexo III, punto 1, letra a)** (identificación biométrica remota): periodo de cada uso con fecha y hora de inicio y fin, base de datos de referencia, datos de entrada con los que se obtuvo correspondencia e identificación de las personas físicas que verificaron los resultados. Para el resto, no hay lista tasada.

La conservación se reparte y los dos plazos coinciden en el suelo. El **artículo 19, apdo. 1**, obliga al **proveedor** a conservar los archivos «en la medida en que dichos archivos estén bajo su control», durante «un período de tiempo adecuado para la finalidad prevista del sistema, de **al menos seis meses**», salvo que el Derecho de la Unión o nacional —«en particular el Derecho de la Unión en materia de protección de datos personales»— disponga otra cosa. El **artículo 26, apdo. 6**, impone al **responsable del despliegue** la misma regla y el mismo mínimo de seis meses, también limitada a los archivos bajo su control. Y el art. 21, apdo. 2, permite a la autoridad competente pedir acceso a esos archivos previa solicitud motivada. Verificado el 31/08/2026.

Lo que conviene fijar antes de abrir el texto, porque es donde la gente se equivoca de dirección:

- **No es una obligación de guardarlo todo.** Un registro que lo guarda todo es caro, es un problema de protección de datos y es inútil para buscar. La obligación es de **trazabilidad**: que se pueda reconstruir qué pasó en un caso concreto.
- **No es lo mismo que los logs de aplicación.** Los logs sirven para depurar y se rotan en días. El registro de eventos sirve para responder de una decisión y vive en otro plano, con otra retención y otro control de acceso.
- **No es lo mismo que las trazas de observabilidad**, aunque se construya sobre ellas. La diferencia se desarrolla en la slide siguiente.
- **No lo sustituye la base de datos de negocio.** El expediente del siniestro dice qué se decidió; el registro dice **con qué entradas, qué versión y qué camino** se llegó a esa decisión.

La prueba práctica de si tu registro cumple su función: coge un siniestro cerrado hace tres meses y reconstruye, sin preguntar a nadie, qué datos se extrajeron, qué versión de modelo los extrajo, qué regla decidió la vía y quién aprobó. Si necesitas a la persona que escribió el código, no tienes registro: tienes memoria oral.

> Fuentes primarias a abrir: AI Act, artículo sobre registro automático de eventos; artículo sobre conservación de los registros por parte del proveedor y del responsable del despliegue; Anexo IV, punto sobre capacidades de registro.

### 12. Las trazas del curso 3 como registro: qué falta

Las trazas del bloque B2 del curso 3 ya contienen casi todo lo que hace falta. Un span por iteración del bucle, un span por tool con entrada, salida y error, la versión de prompt y la de modelo como atributos, identificadores que atraviesan el sistema entero y la posibilidad de reconstruir una ejecución completa desde su traza.

Lo que **falta** para que sirvan como registro son cuatro cosas, y ninguna es un rediseño:

1. **Muestreo.** Las trazas se muestrean por coste; el registro no puede. Hay que separar los dos planos: muestreo agresivo para observabilidad, cobertura completa para los eventos que la trazabilidad exige. El muestreo dirigido del curso 3 —guardar siempre lo que falla— es el punto de partida, pero «lo que falla» no es lo mismo que «lo que hay que poder reconstruir».
2. **Retención.** Las trazas viven semanas porque el almacenamiento cuesta. El registro vive lo que exija su plazo, que es otro orden de magnitud (slide 13).
3. **Integridad.** Un backend de observabilidad admite borrado y reescritura, y está bien que lo haga. Un registro que se puede modificar no prueba nada (slide 25).
4. **Datos personales.** Las trazas se redactan y seudonimizan antes de exportar, y con razón. Pero un registro seudonimizado hasta el punto de no poder vincularse a un expediente concreto ha perdido su función. Hay que decidir **qué se conserva sin redactar, dónde, y quién puede leerlo**, y justificarlo. Es el conflicto que B5 trata de frente.

La forma barata de resolverlo, y la que se recomienda: **una exportación derivada**. La instrumentación es la misma; un canal alimenta el backend de observabilidad con muestreo y redacción, y otro escribe el registro completo, íntegro y con acceso restringido. Un origen, dos destinos, dos políticas.

### 13. Retención de registros y su justificación

Cuánto tiempo se guardan los registros es una de esas preguntas cuya respuesta hay que buscar, no deducir. Buscada, el reglamento tiene **dos relojes distintos** y conviene no confundirlos.

Para los **archivos de registro**, el plazo no es una cifra fija sino un suelo: «un período de tiempo adecuado para la finalidad prevista del sistema, **de al menos seis meses**», salvo que el Derecho de la Unión o nacional disponga otra cosa —art. 19, apdo. 1, para el proveedor, y art. 26, apdo. 6, para el responsable del despliegue—. Sí distingue entre los dos roles, pero les impone la misma regla, y a ambos los limita a los archivos «bajo su control». El punto de partida del cómputo no se fija en el texto: se ata a la finalidad prevista del sistema, no a un hito.

Para la **documentación**, en cambio, el plazo sí es cerrado y sí tiene hito: el art. 18, apdo. 1, obliga al proveedor a mantener a disposición de las autoridades nacionales competentes, «durante un período de **diez años a contar desde la introducción en el mercado o la puesta en servicio**», la documentación técnica del art. 11, la del sistema de gestión de la calidad del art. 17, la relativa a cambios aprobados por organismos notificados, las decisiones de estos y la declaración UE de conformidad. El art. 47, apdo. 1, repite los diez años y el mismo hito para la declaración. Verificado el 31/08/2026.

Lo que sí se puede enseñar es que **el plazo del AI Act no es el único que aplica**, y que ese es el error de método más caro de esta slide. Sobre los mismos datos de Meridiana concurren al menos cuatro relojes distintos:

- El del reglamento de IA, para los registros del sistema.
- El de la normativa de seguros y de contrato de seguro, para el expediente del siniestro.
- El mercantil, para la documentación contable asociada.
- El de protección de datos, que empuja en dirección contraria: **minimizar y suprimir**.

De esos cuatro relojes, tres se han abierto y uno no. Van por separado, con su norma, y **sin decir cuál manda**, porque eso no lo resuelve ninguna de las fuentes leídas:

- **Reglamento de IA.** Archivos de registro: al menos seis meses, adecuados a la finalidad prevista (arts. 19, apdo. 1, y 26, apdo. 6). Documentación técnica y declaración de conformidad: diez años desde la introducción en el mercado o la puesta en servicio (arts. 18, apdo. 1, y 47, apdo. 1).
- **Contrato de seguro.** La Ley 50/1980, de 8 de octubre, de Contrato de Seguro, no fija un plazo de conservación: fija uno de **prescripción**. Su artículo 23 dice que «las acciones que se deriven del contrato de seguro prescribirán en el término de **dos años** si se trata de seguro de daños y de **cinco** si el seguro es de personas». Es un dato de exposición al riesgo de reclamación, no una obligación de guardar; usarlo como plazo de retención es una decisión propia que hay que justificar como tal.
- **Mercantil.** El artículo 30, apdo. 1, del Código de Comercio obliga a los empresarios a conservar «los libros, correspondencia, documentación y justificantes concernientes a su negocio, debidamente ordenados, durante **seis años, a partir del último asiento realizado en los libros**, salvo lo que se establezca por disposiciones generales o especiales».
- **Protección de datos.** El RGPD **no fija ningún plazo**. Su artículo 5, apdo. 1, letra e), impone el principio contrario: los datos personales serán «mantenidos de forma que se permita la identificación de los interesados **durante no más tiempo del necesario** para los fines del tratamiento» («limitación del plazo de conservación»). Es el reloj que empuja hacia abajo, y por eso los arts. 19, apdo. 1, y 26, apdo. 6, del reglamento de IA reservan expresamente lo que disponga el Derecho de protección de datos.

Falta el cuarto reloj: el plazo que imponga la normativa española de **ordenación, supervisión y solvencia** de las entidades aseguradoras. No se ha abierto en esta verificación y es el único de los cuatro que podría fijar una conservación específica del expediente de siniestro, así que **aquí no se da ningún plazo por ese concepto**. Hay que leerlo antes de fijar el del expediente.

La consecuencia práctica es que la política de retención se escribe **por finalidad, no por comodidad**, y se documenta con la norma que la justifica al lado de cada plazo. Una tabla de retención sin columna de fundamento es una tabla que nadie podrá defender ni actualizar.

Y el conflicto que hay que nombrar aunque se resuelva en B5: cuando un asegurado ejerce su derecho de supresión sobre datos que están en un registro de conservación obligatoria, las dos obligaciones chocan de verdad. La respuesta no se improvisa el día de la solicitud: se decide antes, se escribe y se sostiene.

> Fuentes primarias a abrir: AI Act, artículo sobre conservación de registros y documentación; Ley de contrato de seguro y normativa española de ordenación y supervisión de seguros; Código de Comercio; RGPD y LOPDGDD para los plazos en conflicto.

### 14. Instrucciones de uso para el responsable del despliegue

Las instrucciones de uso son el documento con el que el proveedor le dice al responsable del despliegue **en qué condiciones el sistema funciona y en cuáles no**. Son el contrato técnico entre los dos roles de B3, y su contenido está tasado en el **artículo 13**.

El apdo. 2 fija la forma: el sistema irá acompañado de las instrucciones «en un **formato digital o de otro tipo adecuado**», con información «concisa, completa, correcta y clara que sea pertinente, accesible y comprensible para los responsables del despliegue». No impone un formato concreto —y sí impone, en cambio, un criterio de comprensibilidad que es el que justifica el indicador del final de esta slide.

El apdo. 3 enumera el contenido mínimo, en seis letras: identidad y datos de contacto del proveedor y, en su caso, de su representante autorizado [a)]; las características, capacidades y **limitaciones** del funcionamiento, con siete incisos que incluyen la finalidad prevista, el nivel de precisión, solidez y ciberseguridad «con respecto al cual se haya probado y validado el sistema» **y los parámetros para medirlo**, las circunstancias conocidas o previsibles —de uso conforme o de uso indebido razonablemente previsible— que puedan generar riesgos, las capacidades para explicar los resultados de salida, el funcionamiento respecto de determinados colectivos, y las especificaciones de los datos de entrada [b)]; los **cambios predeterminados** por el proveedor en el momento de la evaluación de la conformidad inicial [c)]; las medidas de supervisión humana del art. 14 [d)]; los recursos informáticos y de hardware necesarios, **la vida útil prevista** y las medidas de mantenimiento, incluida su frecuencia y las actualizaciones de software [e)]; y la descripción de los mecanismos que permiten al responsable del despliegue «recabar, almacenar e interpretar correctamente los archivos de registro de conformidad con el artículo 12» [f)].

Sobre el idioma: el art. 13 **no** fija lengua para las instrucciones de uso. La exigencia lingüística que sí está en el texto es otra —el art. 21, apdo. 1, para la documentación que se entrega a la autoridad, y el art. 47, apdo. 2, para la declaración de conformidad—. Verificado el 31/08/2026.

Lo interesante en Meridiana es el efecto de su rol doble: **tiene que escribirse las instrucciones a sí misma**. Suena absurdo hasta que alguien pregunta cuántos siniestros al día puede revisar un tramitador sin que la supervisión deje de ser efectiva, y la respuesta correcta no está en ningún sitio.

Lo que unas instrucciones útiles dicen, más allá de lo tasado:

- **Para qué sirve y para qué no.** Auto, sí. Otros ramos, no evaluado.
- **Qué tiene que hacer la organización que lo usa.** Qué perfil supervisa, con qué formación, con qué carga máxima.
- **Qué mirar en la interfaz.** Qué significa un campo marcado como incierto y qué se espera que haga el tramitador con él.
- **Qué señales indican que algo va mal.** Subida súbita de derivaciones, propuestas fuera del rango habitual, tiempos de respuesta anómalos.
- **Qué hacer entonces**, incluido cómo se apaga el agente y se vuelve al flujo manual.

El indicador de que están bien escritas: **un tramitador nuevo puede leerlas y trabajar**. Si solo las entiende quien construyó el sistema, son documentación de ingeniería con otro nombre.

> Fuentes primarias a abrir: AI Act, artículo sobre instrucciones de uso y transparencia hacia los responsables del despliegue; Anexo IV, punto correspondiente.

### 15. Declaración de conformidad y marcado, si aplica

Cuando corresponde, el proveedor emite una declaración de conformidad y coloca el marcado que proceda. Es el acto por el que alguien **firma con su nombre** que el sistema cumple. Y el texto es aquí más literal que en ningún otro sitio del bloque.

El **artículo 47, apdo. 1**: el proveedor redacta la declaración «por escrito en un **formato legible por máquina**, con firma electrónica o manuscrita, para cada sistema de IA de alto riesgo», y la mantiene a disposición de las autoridades nacionales competentes durante **diez años** desde la introducción en el mercado o la puesta en servicio. El apdo. 4 añade lo que convierte el documento en lo que esta slide dice que es: «al elaborar la declaración UE de conformidad, el proveedor **asumirá la responsabilidad** del cumplimiento de los requisitos establecidos en la sección 2», y la mantendrá actualizada.

El contenido está en el **anexo V**, ocho puntos: nombre y tipo del sistema con referencia inequívoca que permita su identificación y trazabilidad [1]; nombre y dirección del proveedor o su representante [2]; la afirmación de que se expide «bajo la exclusiva responsabilidad del proveedor» [3]; la declaración de conformidad con el reglamento [4]; cuando haya tratamiento de datos personales, la declaración de que el sistema se ajusta al RGPD, al Reglamento (UE) 2018/1725 y a la Directiva (UE) 2016/680 [5]; las normas armonizadas o especificaciones comunes aplicadas [6]; en su caso, el organismo notificado y el certificado [7]; y el lugar y fecha de expedición, **el nombre y el cargo de la persona que la firme**, la indicación de en nombre de quién firma, y la firma [8]. El punto 8 es la respuesta a «quién firma»: una persona con nombre y cargo, no una entidad.

Sobre el marcado y los sistemas que no son productos físicos, el **artículo 48, apdo. 2**, resuelve el caso de Meridiana: «en el caso de los sistemas de IA de alto riesgo que se proporcionan digitalmente, se utilizará un **marcado CE digital**, únicamente si es fácilmente accesible a través de la interfaz desde la que se accede a dicho sistema o mediante un código fácilmente accesible legible por máquina u otros medios electrónicos». Y el apdo. 4 añade que, cuando ha intervenido un organismo notificado, el marcado va seguido de su número de identificación. Verificado el 31/08/2026.

Lo que importa entender aquí, y no requiere fuente:

- **Es una firma, no un sello.** Hay una persona identificada asumiendo una afirmación sobre un sistema concreto y una versión concreta. Ese es todo el mecanismo: convertir el cumplimiento en algo atribuible.
- **Va sobre una versión.** Si el sistema cambia sustancialmente, la declaración anterior deja de describir lo que hay en producción. Es la conexión directa con la slide 7 de B3, y explica por qué el control de cambios no es opcional.
- **Depende de la clasificación.** Si el sistema no es de alto riesgo, este paso puede no aplicar, y la respuesta está en B2, no aquí.

El antipatrón que hay que saber reconocer: **la declaración retroactiva**. Se firma un documento con fecha de hoy sobre un sistema que lleva ocho meses en producción, con la evidencia que se ha podido reunir a posteriori. Es la señal más clara de que el expediente se montó para pasar un trámite, y un auditor la detecta comparando la fecha de la firma con la del primer despliegue.

> Fuentes primarias a abrir: AI Act, artículos sobre declaración UE de conformidad y marcado CE, y sus anexos de contenido; artículo sobre evaluación de conformidad y sus vías.

### 16. Mantener el expediente vivo: quién y con qué periodicidad

El expediente que se escribe una vez está desactualizado el mes siguiente. No por dejadez: porque el sistema sigue cambiando y el documento no.

La solución no es más disciplina, es **cambiar de dónde salen los datos**. Cada sección del expediente se clasifica en una de tres categorías, y la proporción entre ellas determina si el expediente sobrevivirá:

- **Generada.** Sale sola de lo que el sistema produce: resultados de evaluación, versiones desplegadas, métricas de supervisión, inventario de reglas. Se regenera en cada despliegue (slide 23).
- **Enlazada.** No se copia, se referencia con una versión: el conjunto de evaluación, la arquitectura, el registro de riesgos.
- **Redactada a mano.** Descripción, finalidad prevista, justificación de decisiones. Es la que envejece, y por eso hay que tenerla acotada.

Con esa clasificación hecha, el mantenimiento se convierte en algo asignable:

- **En cada cambio relevante**, quien lo hace actualiza la parte redactada que toca. Es una casilla en la plantilla de pull request, no una reunión.
- **En cada despliegue**, se regenera lo generado. Automático.
- **Trimestralmente**, alguien revisa que las referencias siguen apuntando a algo que existe.
- **Anualmente**, revisión completa con guion (slide 27).

Y un nombre por cada una de esas cuatro frecuencias. Un expediente sin dueño no es de nadie, y se nota a los dos meses.

### 17. El expediente de Meridiana: estructura y huecos

Puesto todo junto, así queda el expediente del agente de siniestros, con lo que ya existe y lo que falta. Esta es la foto realista, no la ideal:

- **Descripción y finalidad prevista.** No existe. Hay que escribirla. Media jornada.
- **Datos.** Parcial. El conjunto de evaluación está documentado desde el curso 3; la procedencia de los datos de entrada, no.
- **Arquitectura y decisiones.** Casi completa. Sale de la arquitectura de referencia y de la tabla de qué decide el modelo y qué el código. Necesita reescritura para lector externo.
- **Rendimiento.** Existe la medición; falta el archivo fechado y la segmentación publicada.
- **Gestión de riesgos.** Los riesgos están identificados y mitigados en el código, pero **no escritos como registro de riesgos**. Es el hueco más grande y el que más rápido se cierra.
- **Supervisión humana.** Diseñada e implementada; falta la evidencia de que ocurre, es decir, la tasa de desviación.
- **Registro de eventos.** Las trazas existen; faltan las cuatro cosas de la slide 12.
- **Instrucciones de uso.** No existen. Nadie se las escribe a sí mismo hasta que se lo piden.
- **Declaración de conformidad.** Depende de la clasificación de B2.

El patrón que conviene ver: **casi nada falta por completo**. Lo que falta es la última milla —archivar, fechar, escribir para alguien de fuera— sobre un trabajo técnico que ya estaba hecho. Ese es el mensaje del bloque entero, y también la razón de que un equipo que hizo bien el curso 3 tenga aquí semanas de trabajo y no meses.

### 18. Ejercicio: rellenar la plantilla de expediente

Antes de las slides que quedan, un ejercicio corto que ordena todo lo anterior y produce el punto de partida real del lab.

Coge la plantilla del expediente y, **sin escribir contenido todavía**, rellena solo tres columnas por sección:

1. **Estado.** Existe, parcial o no existe.
2. **Origen.** De dónde saldrá el contenido: generado por CI, enlazado a un artefacto versionado, o redactado a mano.
3. **Dueño.** Nombre de persona.

Es un ejercicio de inventario, y se hace en una hora. Su valor está en lo que revela, que casi siempre es lo mismo en cualquier equipo:

- **Más secciones «parciales» de las esperadas.** La información existe, dispersa, sin fechar y sin formato citable.
- **Demasiadas secciones marcadas como «redactadas a mano».** Cada una es una promesa de mantenimiento que nadie va a cumplir. Si más de la mitad del expediente es manual, el diseño del expediente está mal y hay que rehacerlo antes de escribir una línea.
- **Secciones sin dueño.** Normalmente las de la frontera: gestión de riesgos, que no es de ingeniería ni de cumplimiento, y supervisión humana, que no es de nadie porque la ejecuta operaciones y la documenta otro.

La regla de salida del ejercicio: **ninguna sección sin dueño**, y ninguna marcada como manual si hay forma de generarla o enlazarla.

### 19. Trazabilidad de datos de entrenamiento y de ajuste, si aplica

Esta sección solo aplica si entrenas o ajustas, y a Meridiana hoy no le aplica: usa un modelo de propósito general por API con prompts propios y recuperación de contexto. Conviene decirlo así de claro en el expediente, porque **una sección declarada como no aplicable con su motivo es información**; una sección en blanco es un hueco.

Si algún día sí aplica —y con 32.000 siniestros anuales la tentación llegará—, lo que hay que poder reconstruir es la procedencia completa de cada dato usado: de dónde salió, con qué base jurídica, qué transformaciones sufrió, quién decidió incluirlo y qué versión del artefacto resultante lo contiene.

Lo que el anexo exige sobre datos está en el **anexo IV, punto 2**, y es más concreto de lo que se suele suponer. Su letra d) pide, cuando proceda, los requisitos en materia de datos «en forma de fichas técnicas que describan las metodologías y técnicas de entrenamiento, así como los conjuntos de datos de entrenamiento utilizados», con una descripción general de esos conjuntos e información «acerca de **su procedencia, su alcance y sus características principales**; la manera en que se obtuvieron y seleccionaron los datos; los procedimientos de etiquetado y las metodologías de depuración de datos». Su letra g) extiende la exigencia a los **datos de validación y prueba** y a sus características principales, junto con los parámetros usados para medir precisión y solidez «así como los efectos potencialmente discriminatorios», y exige archivar «los archivos de registro de las pruebas y todos los informes de las pruebas **fechados y firmados** por las personas responsables». Esas dos palabras —fechados y firmados— son las que convierten un informe de CI en evidencia. Verificado el 31/08/2026.

Sobre quien **ajusta** un modelo de propósito general, lo verificado es esto y solo esto: el texto consolidado define al «proveedor posterior» (art. 3, punto 68) y le reconoce el derecho de reclamación del art. 89, apdo. 2. **Cuándo el ajuste convierte el modelo resultante en propio no consta** ni en el articulado ni en los anexos, así que no se afirma aquí. Es el mismo hueco que queda abierto en B3, slide 20, y se cierra con la misma fuente.

Dos advertencias de las que muerden después:

- **La trazabilidad se construye antes, no se reconstruye.** Un conjunto de entrenamiento ensamblado a mano durante seis meses por tres personas distintas no se puede documentar a posteriori con precisión. O hay un proceso que registra la procedencia mientras se construye, o hay una declaración vaga.
- **El derecho de supresión llega hasta aquí.** Un dato personal que entró en un ajuste no se borra del artefacto resultante como se borra una fila. Esa conversación se tiene **antes** de decidir ajustar, y se tiene en B5.

> Fuentes primarias a abrir: AI Act, Anexo IV, punto sobre datos de entrenamiento y metodología; artículos sobre modelos de propósito general modificados por terceros y su documentación.

### 20. Limitaciones conocidas: escribirlas sin maquillarlas

La sección de limitaciones es la que mejor mide la honestidad de un expediente, y es contraintuitiva: **cuanto más específica, más creíble es el resto del documento**.

La razón es sencilla. Quien lee el expediente sabe que ningún sistema funciona igual de bien en todos los casos. Una sección de limitaciones que dice «el sistema puede cometer errores ocasionales» no informa de nada y le indica al lector que o no habéis medido, o no queréis contarlo. Ambas conclusiones le hacen desconfiar de las secciones anteriores.

Cómo se escribe una limitación útil, con ejemplos del caso:

- **Mal:** «La extracción puede fallar con textos poco claros.»
- **Bien:** «La extracción de matrícula degrada de forma medible cuando el relato llega por transcripción telefónica frente al formulario web. Se mitiga marcando el campo como incierto y solicitándolo explícitamente al asegurado. Medido sobre el conjunto de evaluación, segmento *canal telefónico*.»

Tres reglas:

1. **Cada limitación con su mitigación y su evidencia.** Una limitación sin mitigación es un riesgo aceptado, y entonces se escribe como tal, con quien lo acepta.
2. **Las limitaciones heredadas del proveedor del modelo también van aquí**, citadas como externas. Es información que él declaró y tú incorporaste, y demuestra que la leíste.
3. **Se escriben en el idioma del lector, no en el tuyo.** «Degrada con entradas largas» le dice poco a un tramitador; «con relatos de más de dos páginas conviene revisar la extracción campo a campo» le dice qué hacer.

### 21. Casos en los que el sistema no debe usarse

La cara operativa de la slide anterior, y la que más protege al proveedor. Las limitaciones dicen dónde el sistema rinde peor; esta sección dice **dónde directamente no se usa**, y es una frontera, no una advertencia.

Para Meridiana, la lista sale casi sola de lo que se ha ido decidiendo en el programa:

- **Siniestros con lesiones personales.** No es que rinda peor: es que la decisión no le corresponde. Derivación obligatoria.
- **Ramos distintos de auto.** No evaluado. Que la extracción parezca funcionar en un siniestro de hogar no significa nada sin conjunto de evaluación propio.
- **Casos con indicio de fraude.** Otra clase de problema, con otros requisitos y otro perfil de riesgo.
- **Decisión final sobre importes.** El agente propone; nunca aprueba.
- **Uso sin supervisión disponible.** Si no hay tramitadores para revisar —una noche, un pico de granizo, una plantilla reducida—, el sistema no debe seguir proponiendo como si nada. La cola crece y espera.

El último punto es el que la mayoría de los equipos no escribe, y es el que un auditor detecta antes: **la supervisión humana es una condición de uso, no una característica del sistema**. Si desaparece, el sistema está fuera de sus condiciones documentadas.

Y una consecuencia de diseño, no documental: cada uno de estos casos debería tener un control técnico que lo haga cumplir. Una lista de usos prohibidos que solo vive en un PDF se incumple en el primer trimestre.

### 22. Versionar el expediente junto al código

El expediente técnico describe una versión del sistema. Si el expediente vive en una unidad compartida y el sistema en Git, las dos cosas divergen desde el primer despliegue, y no hay proceso que lo arregle.

La decisión es simple y hay que tomarla al principio: **el expediente vive en el repositorio**, en Markdown, junto al código que describe. Lo que se compra con eso:

- **Cambian a la vez.** Un pull request que toca la regla de triaje toca también la sección que la describe, y las dos cosas se revisan en el mismo diff.
- **Hay historial.** «¿Qué decía el expediente cuando se desplegó la versión de marzo?» se contesta con un comando, no con arqueología de correos.
- **Hay revisión.** El expediente pasa por la misma revisión por pares que el código, con las mismas personas mirando.
- **Se puede etiquetar.** Cada versión desplegada lleva su expediente correspondiente en el mismo tag. Eso es exactamente lo que hace verificable una declaración de conformidad sobre una versión concreta.

Dos objeciones previsibles y sus respuestas: *«cumplimiento no sabe usar Git»* —cumplimiento revisa un PDF generado en cada versión, y comenta en la interfaz web como en cualquier revisión—; *«hay que entregarlo en Word»* —se genera Word desde el Markdown, igual que se genera el HTML de este curso desde el suyo—.

El formato de entrega es un detalle de exportación. La fuente de verdad es una sola, y está donde está el código.

### 23. Generar partes del expediente automáticamente desde CI

Aquí se cierra la tesis del bloque. Las secciones que se pueden generar se generan, y dejan de ser trabajo humano recurrente.

Qué se puede generar de verdad, con lo que el curso 3 ya montó:

- **Informe de rendimiento fechado**, con resultados por caso y por segmento, en cada ejecución del conjunto de evaluación.
- **Inventario de reglas deterministas** y sus tests, extraído del código.
- **Versiones desplegadas**: revisión, versión de prompt, versión de modelo, fecha de despliegue.
- **Métricas de supervisión**: volumen procesado, tasa de derivación, tasa de desviación del tramitador.
- **Estado del registro de eventos**: cobertura, volumen, integridad.

```yaml
# Cada tag de release deja el expediente de esa versión archivado.
- name: expediente
  run: |
    python tools/expediente.py \
      --evals informes/evals-${GITHUB_SHA}.json \
      --reglas src/triaje/ \
      --version ${GITHUB_REF_NAME} \
      --out expediente/generado/
```

Dos límites que hay que decir para no vender humo:

- **Lo generado no sustituye al criterio.** Un informe dice qué pasó; alguien tiene que escribir por qué es aceptable. La justificación es humana y no se automatiza.
- **Un generador roto produce evidencia falsa**, que es peor que no tener evidencia. El generador se prueba como código de producción, porque lo es.

Lo que se gana: la parte del expediente que caduca cada semana deja de depender de que alguien se acuerde.

### 24. Qué pasa cuando el proveedor no da la información que necesitas

Situación real y frecuente: necesitas describir un componente de tu sistema y el proveedor del modelo no publica lo que necesitas, o publica algo que no es citable.

Lo primero que hay que tener claro: **el hueco no te exime**. Tu obligación de documentar tu sistema no desaparece porque un tercero no colabore. Pero antes de asumir el hueco conviene comprobar si lo que pides te lo deben.

Lo que te debe está en el **artículo 53, apdo. 1, letra b)**: el proveedor del modelo de uso general debe elaborar, mantener actualizada y poner a disposición de quienes vayan a integrarlo una información y documentación que les permita «entender bien las capacidades y limitaciones del modelo» y cumplir sus propias obligaciones, y que contendrá **como mínimo los elementos del anexo XII** —descripción general del modelo, políticas de usos aceptables, fecha de lanzamiento y métodos de distribución, versiones, arquitectura y número de parámetros, modalidad y formato de entradas y salidas con su tamaño máximo, licencia, medios técnicos de integración e información sobre los datos de entrenamiento, prueba y validación cuando proceda—. Todo eso se pide por reglamento, no por favor. Con dos salvedades leídas en el mismo artículo: la protección de secretos comerciales e información empresarial confidencial, y la exención del apdo. 2 para modelos de licencia libre y código abierto que no presenten riesgo sistémico.

Y sí hay un recurso cuando no lo da. El **artículo 89, apdo. 2**, reconoce a los «proveedores posteriores» —art. 3, punto 68: quien integra un modelo de IA en su sistema— el derecho a **presentar reclamaciones ante la Oficina de IA** alegando infracciones del reglamento, debidamente motivadas y con el contenido mínimo de sus letras a) a c): punto de contacto del proveedor del modelo, descripción de los hechos con las disposiciones afectadas y los motivos, y cualquier otra información pertinente. La supervisión del capítulo V corresponde en exclusiva a la Comisión, que la ejecuta a través de la Oficina de IA (art. 88, apdo. 1). Es decir: el paso 1 de la secuencia siguiente no es solo una buena práctica de diligencia, es la prueba que sostendría una reclamación. Verificado el 31/08/2026.

La secuencia sensata, en orden:

1. **Pídelo por escrito y guarda la respuesta.** Aunque sea un no. Un correo con fecha demuestra diligencia; una conversación en una llamada no demuestra nada.
2. **Documenta lo que sí puedes verificar tú.** El comportamiento observable del modelo sobre tu conjunto de evaluación es información de primera mano, la genera tu equipo y es la más pertinente para tu sistema.
3. **Escribe el hueco como hueco.** «El proveedor no facilita X; solicitado en tal fecha; sin respuesta.» Un expediente que declara sus límites es defendible. Uno que los rellena con suposiciones plausibles, no.
4. **Convierte el hueco en riesgo.** Entra en el registro de la slide 8, con su mitigación —evals propios, versión anclada, plan de salida— y su aceptación firmada.
5. **Si el hueco impide cumplir, es una decisión de negocio.** Puede que ese proveedor no sea utilizable para este caso de uso. Es una conclusión legítima y hay que poder decirla.

Lo que no vale: rellenar la sección con lo que dice la página comercial del proveedor. Un texto de marketing citado como evidencia técnica es peor que una sección vacía, porque además demuestra que no sabéis distinguir una cosa de la otra.

> Fuentes primarias a abrir: AI Act, artículo sobre información que los proveedores de modelos de propósito general facilitan a los integradores; anexo con el contenido de esa información; código de buenas prácticas, si está publicado.

### 25. Conservar evidencias: formato, integridad y plazo

Una evidencia sirve si se cumplen tres condiciones a la vez, y en la práctica falla siempre la segunda.

- **Formato.** Legible sin tu infraestructura. Un informe en JSON o CSV con su esquema documentado se lee dentro de cinco años; una consulta a un panel de un proveedor de observabilidad que ya no usas, no. La regla: **exporta lo que quieras conservar**, no confíes en poder consultarlo.
- **Integridad.** Que se pueda demostrar que no se ha modificado desde que se creó. No hace falta nada exótico: almacenamiento de solo lectura, huella criptográfica de cada informe registrada en un índice, y un índice que también sea inmutable. Sin esto, un informe favorable es una afirmación tuya sobre ti mismo.
- **Plazo.** Son dos plazos distintos y con hitos distintos, y mezclarlos es el error de esta sección. La **documentación técnica** se conserva **diez años** a contar desde la introducción en el mercado o la puesta en servicio del sistema (art. 18, apdo. 1), y el mismo plazo y el mismo hito valen para la declaración UE de conformidad (art. 47, apdo. 1). Los **archivos de registro** no tienen plazo cerrado: «un período de tiempo adecuado para la finalidad prevista del sistema, de **al menos seis meses**», salvo que el Derecho de la Unión o nacional disponga otra cosa (art. 19, apdo. 1, para el proveedor; art. 26, apdo. 6, para el responsable del despliegue). Verificado el 31/08/2026. Los otros relojes que concurren sobre los mismos datos están en la slide 13.

El error más común no es no guardar: es **guardar sin poder demostrar cuándo**. Un fichero con fecha de modificación de la semana pasada, sobre una ejecución que dice ser de hace dos años, no prueba nada. La fecha tiene que ser parte de la evidencia, no un atributo del sistema de ficheros.

Y una recomendación práctica que ahorra discusiones: **archiva el informe completo, no el resumen**. El resumen contesta la pregunta que te hiciste tú; la pregunta del auditor será otra, y solo el detalle por caso permite contestarla.

> Fuentes primarias a abrir: AI Act, artículos sobre conservación de la documentación técnica y de los registros generados automáticamente; disposiciones sobre puesta a disposición de las autoridades.

### 26. Preparar el expediente para que lo lea alguien de fuera

Todo lo anterior puede estar hecho y el expediente seguir fallando en la prueba que importa: **que alguien que no conoce tu sistema encuentre lo que busca en pocos minutos**.

Lo que hace que un expediente sea legible desde fuera:

- **Un índice que mapea contra el anexo**, con una columna que diga dónde está cada punto. El lector no viene a explorar: viene a comprobar una lista.
- **Un resumen de dos páginas al principio.** Qué es el sistema, qué decide, qué decide una persona, en qué estado está la conformidad. Si el lector solo lee eso, tiene que salir con la idea correcta.
- **Glosario.** «Vía», «FNOL», «derivación» y «tramitador» son jerga del caso. Cuestan tres líneas y evitan diez malentendidos.
- **Nombres de personas y fechas**, no «el equipo» y «recientemente».
- **Enlaces que funcionan sin tus credenciales.** Una referencia a un panel interno que el lector no puede abrir es una referencia que no existe.
- **Los huecos, marcados como huecos**, con responsable y fecha. Declarados, no escondidos.

La prueba antes de darlo por terminado, y se hace en una tarde: **dáselo a alguien de otro equipo con una pregunta concreta**. «¿Qué pasa si el agente no detecta lesiones?» o «¿quién aprueba un importe de 3.000 €?». Si tarda más de cinco minutos en encontrar la respuesta, el problema no es del lector.

Un expediente correcto pero ilegible se comporta, en una inspección, exactamente igual que un expediente incompleto.

### 27. Revisión anual del expediente: quién y con qué guion

La revisión periódica completa es lo que impide que el mantenimiento incremental de la slide 16 acumule deriva sin que nadie lo note. Hay que fijar su periodicidad y su alcance, y comprobar qué exige la norma al respecto. Comprobado: **el reglamento no impone ninguna frecuencia numérica**, ni para la documentación técnica ni para la gestión de riesgos. Lo que impone es continuidad. El art. 11, apdo. 1, se limita a exigir que la documentación técnica «se mantendrá actualizada». El art. 9, apdo. 2, describe el sistema de gestión de riesgos como «un proceso iterativo continuo planificado y ejecutado durante todo el ciclo de vida», que «requerirá **revisiones y actualizaciones sistemáticas periódicas**» —periódicas, sin decir cada cuánto—. Esto es una buena noticia y una trampa: nadie te va a multar por no revisar en marzo, y nadie te va a salvar si no revisaste nunca. La periodicidad la fijas tú, y por eso conviene escribirla.

La relación con la vigilancia poscomercialización sí es explícita y de doble sentido. El art. 9, apdo. 2, letra c), obliga a evaluar los riesgos que surjan «a partir del análisis de los datos recogidos con el sistema de vigilancia poscomercialización a que se refiere el artículo 72»: la vigilancia alimenta la gestión de riesgos. Y el art. 72, apdo. 3, cierra el círculo documental: «el plan de vigilancia poscomercialización **formará parte de la documentación técnica** a que se refiere el anexo IV» —que lo recoge en su punto 9—. Es decir, el expediente contiene el plan que lo mantiene vivo. El art. 72, apdo. 2, describe además qué debe hacer ese sistema: recopilar, documentar y analizar «de manera activa y sistemática» los datos sobre el funcionamiento durante toda la vida útil, para evaluar «el cumplimiento permanente» de los requisitos. Verificado el 31/08/2026.

El guion de la revisión, que sí es cosa nuestra, cabe en siete preguntas:

1. **¿La descripción sigue siendo cierta?** Lo que el sistema hace hoy, ¿es lo que dice la sección 1?
2. **¿Ha cambiado la finalidad prevista de hecho?** Nuevos canales, nuevos ramos, nuevos consumidores de la salida.
3. **¿Ha habido cambios que debieron evaluarse como sustanciales y no se evaluaron?** Se contesta leyendo el historial de despliegues, no de memoria.
4. **¿Los números publicados son los últimos?** Y si no, ¿por qué el generador dejó de correr?
5. **¿Los riesgos identificados siguen siendo los relevantes?** Y los incidentes del año, ¿aparecen como riesgos nuevos?
6. **¿La supervisión humana sigue siendo efectiva?** Carga por tramitador y tasa de desviación, comparadas con las del año pasado.
7. **¿Qué huecos declarados siguen abiertos?** Y quién los tiene asignados desde hace cuánto.

Dos reglas de ejecución: **la revisa alguien que no la escribió**, y **produce acciones con dueño y fecha**, no un acta. Una revisión anual cuyo resultado es «todo correcto» sin una sola acción es una revisión que no se hizo.

> Fuentes primarias a abrir: AI Act, artículos sobre actualización de la documentación técnica, sistema de gestión de la calidad y vigilancia poscomercialización; normas armonizadas sobre revisión periódica, si están publicadas.

### 28. Ejercicio práctico 1: de la traza al registro {ejercicio:B4-ej1}

Coge una traza real de una ejecución del agente de Meridiana —de las que produce la instrumentación del bloque B2 del curso 3— y decide, campo a campo, qué va al registro de eventos y qué se queda solo en observabilidad.

Para cada campo escribe tres cosas: **si se conserva**, **cuánto tiempo** y **por qué**. El «por qué» es lo que se corrige: si la respuesta es «por si acaso», el campo no debería conservarse.

Al terminar, contesta la pregunta de la slide 11 sobre tu propuesta: con lo que has decidido conservar, ¿se puede reconstruir dentro de tres meses qué datos se extrajeron, con qué versión, qué regla decidió la vía y quién aprobó? Si falta una sola de las cuatro, vuelve a la lista.

### 29. Ejercicio práctico 2: la sección de limitaciones que nadie quiere escribir {ejercicio:B4-ej2}

Escribe la sección de limitaciones conocidas del agente de Meridiana. Mínimo cinco limitaciones, cada una con **su mitigación y su evidencia**, en el formato de la slide 20.

Al menos una tiene que venir del proveedor del modelo y estar citada como externa. Al menos una tiene que ser un riesgo aceptado sin mitigación, con quién lo acepta.

Después haz la prueba que da valor al ejercicio: **enséñasela a alguien que no ha trabajado en el sistema y pídele que te diga qué haría distinto si tuviera que usarlo mañana**. Si no cambia nada de su forma de trabajar, la sección está escrita para el archivo y no para el lector.

### 30. Mini-quiz de comprensión — B4 {quiz:B4}

Tres preguntas sobre lo que decide este bloque: qué partes del expediente puede alimentar la observabilidad, qué función cumple el registro de eventos y quién mantiene el documento vivo.

Ninguna pregunta depende de un número de artículo, de un anexo concreto ni de un plazo. Aprobado con dos aciertos. Puedes repetirlo las veces que quieras.

## Qué te llevas

- El expediente técnico se alimenta de lo que el sistema ya produce, o no se mantiene.
- Las trazas y los evals son evidencia; hay que diseñarlos sabiéndolo.
- Un expediente que solo se escribe una vez está desactualizado el mes siguiente.

## Mini-quiz (3 preguntas)

Una sola opción correcta por pregunta, con explicación. Se generan con el mismo formato que `assets/js/quizzes-data.js` para que el importador las recoja.

1. **Tema:** Qué secciones del expediente puede alimentar la observabilidad
   - **Enunciado:** El equipo quiere reducir el trabajo manual de mantener el expediente técnico. ¿Qué parte es la mejor candidata a generarse automáticamente desde lo que el sistema ya produce?
   - **Opciones:**
     - a) La descripción del sistema y su finalidad prevista, porque son las secciones más largas.
     - b) **Los informes de rendimiento por caso y por segmento, con la versión de código, prompt y modelo con la que se obtuvieron.** ✅
     - c) La justificación de por qué el nivel de exactitud alcanzado es aceptable para el caso de uso.
     - d) Ninguna: la documentación técnica exige redacción humana en todas sus secciones.
   - **Explicación:** El rendimiento medido es un dato que el sistema produce en cada ejecución del conjunto de evaluación, y es además la sección que caduca antes; generarla y archivarla fechada resuelve la parte del expediente que más envejece. La (a) y la (c) son criterio humano: describir el perímetro y justificar que un resultado es aceptable no se automatiza. La (d) confunde «requiere criterio» con «requiere teclear»: si más de la mitad del expediente es manual, no se mantendrá.

2. **Tema:** Qué exige el registro automático de eventos
   - **Enunciado:** ¿Cuál de estas afirmaciones describe correctamente la función del registro de eventos frente a otras cosas que el sistema ya guarda?
   - **Opciones:**
     - a) Es equivalente a los logs de aplicación, así que basta con subir el nivel de detalle y alargar la rotación.
     - b) Obliga a conservar absolutamente toda la información que atraviesa el sistema, sin excepción.
     - c) **Sirve para poder reconstruir qué ocurrió en un caso concreto: con qué entradas, qué versión y qué camino se llegó a la decisión, cosa que ni los logs ni la base de datos de negocio contestan por separado.** ✅
     - d) Lo cubre la base de datos del expediente del siniestro, porque ahí queda registrado lo que se decidió.
   - **Explicación:** La finalidad es la trazabilidad de una ejecución concreta, y eso exige vincular entrada, versión del sistema, camino de decisión y resultado. Los logs sirven para depurar y se rotan; la base de datos guarda el resultado pero no cómo se llegó a él ni con qué versión. La (b) confunde trazabilidad con acumulación: guardarlo todo es caro, es un problema de protección de datos y hace el registro inútil para buscar.

3. **Tema:** Quién mantiene el expediente y con qué frecuencia
   - **Enunciado:** El expediente se terminó en marzo. ¿Cuál es el modelo de mantenimiento que tiene alguna probabilidad de funcionar?
   - **Opciones:**
     - a) Una revisión completa anual a cargo de cumplimiento, que es quien responde ante la autoridad.
     - b) **Actualización en cada cambio relevante por quien lo hace, regeneración automática de lo generable en cada despliegue, y una revisión completa periódica hecha por alguien que no lo escribió.** ✅
     - c) Un encargo externo anual a una consultora, que aporta independencia y descarga al equipo.
     - d) Ninguno hace falta mientras no cambie la clasificación de riesgo del sistema.
   - **Explicación:** El expediente envejece al ritmo del sistema, no al del calendario, así que el mantenimiento tiene que engancharse al flujo de cambios; y la parte que caduca cada semana solo sobrevive si se genera sola. La revisión periódica sigue haciendo falta para detectar la deriva acumulada, y la hace otra persona porque el autor no ve sus propios huecos. La (a) llega tarde y con el conocimiento equivocado. La (c) compra un documento, no un proceso. La (d) confunde la clasificación con el contenido: el sistema cambia aunque su categoría no.

## Lab

Este curso no lleva labs de código. En su lugar, ejercicio de plantilla (DOCX/XLSX) sobre el caso Meridiana.

**Enunciado.** Monta el **esqueleto del expediente técnico** del agente de Meridiana en la plantilla `content/caso/plantillas/B4-expediente.docx`, acompañada de la hoja de inventario `B4-inventario-secciones.xlsx`. No se pide el expediente terminado: se pide un esqueleto completo, con dueños, orígenes y huecos declarados, y **tres secciones redactadas de verdad**.

**Pasos:**

1. **Inventario.** Una fila por sección del índice, con estado (existe / parcial / no existe), origen (generado / enlazado / manual) y dueño con nombre y apellido.
2. **Redacta tres secciones completas:** descripción y finalidad prevista, arquitectura y decisiones de diseño, y limitaciones conocidas. Las tres se apoyan en material del curso 3; hay que reescribirlas para un lector externo, no copiarlas.
3. **Registro de riesgos.** Al menos seis riesgos con las cinco columnas de la slide 8, incluida la de cómo se comprueba que la mitigación sigue funcionando.
4. **Diseño del registro de eventos.** Qué campos se conservan, cuánto tiempo, con qué integridad y quién puede leerlos. Los plazos se dejan marcados como pendientes, con la norma que habrá que abrir.
5. **Plan de generación.** Qué secciones se generarán desde CI y con qué artefacto de entrada de los que ya produce el curso 3.
6. **Prueba de lectura externa.** Que alguien de otro equipo conteste dos preguntas concretas usando solo el documento, cronometrando cuánto tarda.

**Criterios de aceptación:**

- El índice del documento **mapea contra el índice del anexo de documentación técnica**, con una columna de correspondencia. Los puntos del anexo se transcriben tras abrir la fuente; hasta entonces se dejan marcados como pendientes.
- **Ninguna sección sin dueño**, y ningún dueño que sea un departamento.
- Todo hueco aparece **declarado como hueco**, con responsable y fecha, nunca relleno con suposiciones.
- Ninguna afirmación normativa sin cita o sin marca de pendiente; ningún plazo, artículo ni anexo escrito de memoria.
- Las secciones marcadas como manuales son **menos de la mitad** del total. Si no, hay que rediseñar el expediente antes de seguir escribiendo.
- La sección de limitaciones tiene al menos cinco entradas, cada una con mitigación y evidencia, y al menos una heredada del proveedor del modelo.
- La prueba de lectura externa se supera en menos de cinco minutos por pregunta.

**Solución de referencia:** en `content/caso/soluciones/B4/`, con el esqueleto completo, las tres secciones redactadas y el registro de riesgos del caso.

## Cierre

- Recapitulación en los tres puntos de arriba.
- Mini-quiz.
- Enlace al siguiente bloque.
