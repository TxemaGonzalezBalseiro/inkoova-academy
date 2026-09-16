# C-10 · B2 · Clasificación de riesgo

> Curso: `gobernanza-eu` · bloque `B2`

## Objetivo

Clasificar un sistema concreto y poder defender la clasificación por escrito.

## ⚠ Bloque normativo: verificación obligatoria antes de publicar

Cada afirmación de este bloque debe citar artículo y fecha de verificación, y esa fecha debe salir de haber abierto la fuente primaria. No se redacta de memoria ni a partir de resúmenes de terceros.

**Fuentes primarias a consultar:**

- [x] AI Act, artículo 6 y Anexo III — texto consolidado (CELEX 02024R1689-20260727), leídos en EUR-Lex el 31/08/2026
- [x] Considerandos relativos a la excepción por tarea accesoria — considerando 53 del texto original (CELEX 32024R1689), leído el 31/08/2026
- [ ] Directrices de la Comisión sobre clasificación, si publicadas
- [ ] EIOPA: documentos sobre IA en seguros

**Fecha de verificación:** 2026-08-31. Lo cerrado está en
`content/cursos/gobernanza-eu/VERIFICADO.md`, con artículo, URL y cita literal por dato.
Lo que no se pudo comprobar se dice en su sitio, con la fuente que hay que abrir.

> Aviso al alumno que debe aparecer en el bloque: este material es formativo y no constituye asesoramiento jurídico.

## Guion de slides

26 slides de contenido. Una idea por slide, con un ejemplo real o del caso Meridiana. Nada de relleno.

### 1. Clasificar no es opinar: es un procedimiento

La conversación típica sobre clasificación empieza mal: alguien pregunta «¿esto es alto riesgo?» y tres personas dan tres impresiones. Al final decide la más segura de sí misma o la más veterana, y nadie escribe por qué.

Eso no es una clasificación. Es una opinión con testigos.

Clasificar es un **procedimiento con entradas, pasos y salida documentada**. Las entradas son hechos verificables sobre el sistema: qué hace, sobre quién produce efectos, quién decide qué y con qué margen real. Los pasos son un árbol de decisión que se recorre en un orden fijo. La salida es un documento que otra persona puede leer, seguir y **contradecir señalando un paso concreto**.

Esa última propiedad es la que distingue una clasificación buena de una mala. Si alguien discrepa de tu conclusión y solo puede decir «yo lo veo distinto», tu documento no sirve. Si puede decir «en el paso 4 asumiste que el tramitador puede rechazar la propuesta, y en la práctica tiene 40 segundos por expediente», entonces tienes un documento útil: es falsable.

En Meridiana esto se traduce en algo incómodo. La clasificación no la puede firmar solo el equipo de IA, porque el paso decisivo —¿la supervisión humana es efectiva?— depende de la carga de trabajo real de 24 tramitadores, no del diseño del sistema. Ese dato lo tiene operaciones. La clasificación es un trabajo conjunto o no es nada.

### 2. El árbol de decisión, paso a paso

El orden importa. Recorrerlo al revés produce clasificaciones que se caen a la primera pregunta.

1. **¿Es un sistema de IA según la definición del reglamento?** Si no lo es, se acabó. Y se documenta por qué no lo es.
2. **¿El uso está entre las prácticas prohibidas?** Se comprueba antes que nada, porque si la respuesta es sí, no hay nada más que clasificar. Se para el proyecto.
3. **¿Es un componente de seguridad de un producto ya regulado, o el producto mismo?** Es la primera vía a alto riesgo, y arrastra el régimen sectorial correspondiente.
4. **¿El uso encaja en alguno de los ámbitos y usos listados en el Anexo III?** Es la segunda vía.
5. **Si encaja, ¿aplica alguna excepción, y puedes justificarla con hechos?** Aquí vive la excepción por tarea accesoria.
6. **Si no es alto riesgo, ¿hay obligaciones de transparencia?** Interacción con personas, contenido generado.
7. **Documenta la conclusión y los pasos**, incluidos los que descartaste y por qué.

Dos advertencias sobre el recorrido. La primera: **se recorre por cada uso, no por cada sistema**. Meridiana tiene cuatro etapas y hay que recorrerlo cuatro veces; la conclusión puede ser distinta en cada una.

La segunda: el paso 7 no es burocracia añadida. Los pasos descartados son la mitad del valor del documento, porque son las preguntas que un auditor hará y que ya tienes contestadas.

Una precisión sobre el árbol, verificada en el texto consolidado el 31/08/2026: el **artículo 6** no enuncia un procedimiento numerado. Enuncia **reglas de clasificación**, y el orden de arriba es una lectura razonada de ellas, no una cita. Lo que sí está literalmente en el artículo es la estructura de los pasos 3 a 5:

- El **apartado 1** exige **dos condiciones acumulativas** para la vía de producto —«cuando reúna las dos condiciones que se indican a continuación»—: que el sistema sea componente de seguridad de un producto del Anexo I o sea él mismo ese producto [letra a)], **y** que ese producto deba someterse a una **evaluación de la conformidad de terceros** con arreglo a ese mismo anexo [letra b)]. Los apartados **1 bis**, **1 ter** y **1 quater**, añadidos por el Reglamento (UE) 2026/1744, acotan qué es y qué no es un componente de seguridad: no lo son los sistemas usados «únicamente para aspectos no relacionados con la seguridad» —asistencia al usuario, optimización del rendimiento, eficiencia del servicio, automatización, comodidad o control de calidad—, salvo que su fallo «pueda poner en peligro la salud y la seguridad».
- El **apartado 2** es la segunda vía, en una línea: «también se considerarán de alto riesgo los sistemas de IA contemplados en el anexo III».
- El **apartado 3** es la excepción, que se comprueba después y no antes.

El orden de las comprobaciones 1 y 2 del árbol no sale del artículo 6, sino de que sin sistema de IA no hay reglamento (artículo 3, punto 1) y de que el artículo 5 prohíbe con independencia de cualquier clasificación.

> Fuentes primarias a abrir: Reglamento (UE) 2024/1689, artículo 6 y considerandos asociados, en EUR-Lex; directrices de la Comisión sobre clasificación de sistemas de alto riesgo, si publicadas.

### 3. Anexo III: los ámbitos de alto riesgo y cómo se leen

El Anexo III es una lista de ámbitos y, dentro de cada uno, de **usos concretos**. Esa estructura de dos niveles es lo que más se lee mal.

El error es quedarse en el primer nivel. Alguien ve el ámbito, reconoce su sector y concluye que está dentro. O no lo ve y concluye que está fuera. Las dos conclusiones son prematuras: **la que decide es la descripción del uso**, que suele ser precisa sobre qué hace el sistema y sobre quién produce efectos.

La forma correcta de leerlo es punto por punto y con una pregunta doble para cada uno:

- ¿Describe este punto **lo que mi sistema hace**, con independencia de cómo llamemos internamente a la función?
- ¿Describe **las personas sobre las que produce efectos** y la naturaleza de esos efectos?

Y una tercera, que evita el autoengaño: si tuviera que argumentar ante alguien hostil que mi sistema **sí** encaja aquí, ¿qué diría? Si el argumento contrario es fácil de construir, tu conclusión de «no encaja» necesita más trabajo.

Un detalle de método que ahorra disgustos: **cita el punto exacto que descartaste y por qué**, no solo el que aplica. Un documento que dice «revisados todos los puntos del Anexo III, ninguno aplica» es una afirmación sin trabajo detrás. Uno que recorre los puntos plausibles y los descarta con un motivo es defendible.

La denominación oficial es **«Anexo III — Sistemas de IA de alto riesgo a que se refiere el artículo 6, apartado 2»**, y su frase de entrada confirma la estructura de dos niveles que describe esta slide: son los sistemas «que formen parte de cualquiera de los ámbitos siguientes». Los **ocho ámbitos**, con el número de usos que cuelga de cada uno:

1. **Biometría**, en la medida en que su uso esté permitido por el Derecho aplicable — tres usos: identificación biométrica remota (con exclusión expresa de la verificación cuya única finalidad sea confirmar que alguien es quien dice ser), categorización biométrica por atributos sensibles o protegidos, y reconocimiento de emociones.
2. **Infraestructuras críticas** — un uso: componentes de seguridad en la gestión y funcionamiento de infraestructuras digitales críticas, tráfico rodado o suministro de agua, gas, calefacción o electricidad.
3. **Educación y formación profesional** — cuatro usos: acceso o admisión y distribución entre centros; evaluación de resultados del aprendizaje; evaluación del nivel educativo adecuado; y seguimiento y detección de comportamientos prohibidos durante los exámenes.
4. **Empleo, gestión de los trabajadores y acceso al autoempleo** — dos usos: contratación o selección; y decisiones sobre condiciones laborales, promoción o rescisión, asignación de tareas a partir de comportamientos o rasgos personales, y supervisión y evaluación del rendimiento.
5. **Acceso a servicios privados esenciales y a servicios y prestaciones públicos esenciales y disfrute de estos servicios y prestaciones** — cuatro usos, y es el ámbito de la slide siguiente.
6. **Garantía del cumplimiento del Derecho** — cinco usos.
7. **Migración, asilo y gestión del control fronterizo** — cuatro usos.
8. **Administración de justicia y procesos democráticos** — dos usos.

Sobre si ha sido modificado: en la ficha del texto consolidado, el **único acto modificador** del Reglamento (UE) 2024/1689 es el Reglamento (UE) 2026/1744, y **el Anexo III no lleva ninguna marca de modificación** en la versión consolidada de 27/07/2026. No consta, por tanto, acto delegado del artículo 7 que lo haya tocado. Verificado en EUR-Lex el 31/08/2026.

> Fuentes primarias a abrir: Reglamento (UE) 2024/1689, Anexo III, texto consolidado en EUR-Lex; registro de actos delegados de la Comisión que modifiquen anexos.

### 4. Seguros y evaluación de riesgos: dónde aparece y dónde no

Esta es la slide donde el sector asegurador descubre que la respuesta no es uniforme. El Anexo III no dice «seguros». Describe **usos**, y algunos de esos usos ocurren en compañías de seguros mientras que otros, que también ocurren en compañías de seguros, no están descritos en ninguna parte.

Lo verificado, y es más estrecho de lo que casi todo el mundo supone: en todo el Anexo III **los seguros aparecen una sola vez**, en el **punto 5, letra c)**, con esta redacción literal:

> «Sistemas de IA destinados a ser utilizados para la evaluación de riesgos y la fijación de precios en relación con las personas físicas en el caso de los **seguros de vida y de salud**».

Tres acotaciones que están en esas dos líneas y que hay que citar juntas o no citarlas. **Por función**: evaluación de riesgos y fijación de precios; nada más. **Por ramo**: vida y salud; no auto, no hogar, no responsabilidad civil. **Por destinatario**: personas físicas.

Cerca viven otros dos usos del mismo punto 5 que conviene descartar por escrito en lugar de ignorarlos. La **letra b)** cubre los sistemas «destinados a ser utilizados para evaluar la solvencia de personas físicas o establecer su calificación crediticia, **salvo los sistemas de IA utilizados al objeto de detectar fraudes financieros**» —esa excepción importa a cualquier aseguradora con antifraude—. La **letra a)** cubre la admisibilidad para prestaciones esenciales de asistencia **pública**, y la **letra d)**, las llamadas de emergencia y el triaje de pacientes en asistencia sanitaria de urgencia; ninguna alcanza la tramitación de un siniestro privado.

**La gestión de siniestros no aparece como uso listado en ningún punto del Anexo III.** Verificado en el texto consolidado de EUR-Lex el 31/08/2026. Eso no cierra la clasificación de Meridiana —el árbol tiene más pasos y el ramo de auto no está en el punto 5, letra c)—, pero sí convierte el argumento de esta slide en algo comprobable en lugar de una intuición sectorial.

Lo que sí puede razonarse, y es lo que hay que entender:

La lógica del anexo es proteger a las personas frente a decisiones que **condicionan su acceso a algo importante**. Por eso los ámbitos listados giran alrededor del acceso: a un empleo, a una formación, a un servicio esencial, a una prestación. Cuando un sistema decide si alguien entra o no entra, el legislador presta atención.

Aplicado a una aseguradora, eso separa dos familias de usos que a menudo se tratan como una:

- **Antes del contrato:** admisión, tarificación, decisión sobre si se asegura a alguien y en qué condiciones. Aquí el sistema condiciona el acceso.
- **Después del contrato:** tramitación de un siniestro de alguien que **ya es cliente y ya tiene un derecho contractual**. Aquí no se decide el acceso a un producto; se ejecuta un contrato que ya existe.

Meridiana está en la segunda familia. Ese es el argumento central de su clasificación, y no es un tecnicismo: es una diferencia real en qué está en juego para la persona. Pero no cierra la discusión, porque una tramitación puede acabar en una denegación, y una denegación sí afecta a un derecho.

> Fuentes primarias a abrir: Reglamento (UE) 2024/1689, Anexo III y considerandos que explican cada ámbito, en EUR-Lex; documentos publicados por EIOPA sobre uso de IA en seguros.

### 5. La excepción por tarea accesoria y sus condiciones

El reglamento prevé que un sistema cuyo uso figura en el Anexo III **pueda no considerarse de alto riesgo** cuando no plantea un riesgo significativo para la salud, la seguridad o los derechos fundamentales, por no influir materialmente en el resultado de la decisión. La excepción está condicionada, y las condiciones son lo importante.

La excepción es el **artículo 6, apartado 3**, y su párrafo primero dice: un sistema del Anexo III «no se considerará de alto riesgo cuando no plantee un riesgo importante de causar un perjuicio a la salud, la seguridad o los derechos fundamentales de las personas físicas, también al no influir sustancialmente en el resultado de la toma de decisiones».

La lista de supuestos es **cerrada y de cuatro letras**, y basta con que se cumpla cualquiera de ellas:

- a) que el sistema «esté destinado a realizar una **tarea de procedimiento limitada**»;
- b) que esté destinado «a **mejorar el resultado de una actividad humana previamente realizada**»;
- c) que esté destinado «a detectar patrones de toma de decisiones o desviaciones con respecto a patrones de toma de decisiones anteriores y **no** esté destinado a sustituir la valoración humana previamente realizada sin una revisión humana adecuada, ni a influir en ella»;
- d) que esté destinado «a realizar una **tarea preparatoria** para una evaluación que sea pertinente a efectos de los casos de uso enumerados en el anexo III».

Y el corte tajante, en el párrafo tercero del mismo apartado: «No obstante lo dispuesto en el párrafo primero, los sistemas de IA a que se refiere el anexo III **siempre se considerarán de alto riesgo cuando el sistema de IA efectúe la elaboración de perfiles de personas físicas**». Sin excepción y sin matices.

La documentación y el registro son el **apartado 4**: el proveedor «documentará su evaluación **antes** de que dicho sistema sea introducido en el mercado o puesto en servicio», quedará sujeto a la obligación de registro del artículo 49, apartado 2, y facilitará esa documentación a las autoridades nacionales competentes cuando la pidan.

El **considerando 53** del texto original interpreta la condición de fondo con una definición que vale la pena tener a mano al redactar: «por sistema de IA que no influye sustancialmente en el resultado de la toma de decisiones debe entenderse un sistema de IA que **no afecta al fondo, ni por consiguiente al resultado**, de la toma de decisiones, ya sea humana o automatizada». Y da ejemplos de la letra a) que son casi el FNOL de Meridiana: «un sistema de IA que transforme datos no estructurados en datos estructurados», o «que clasifique en categorías los documentos recibidos». Verificado en EUR-Lex el 31/08/2026.

Lo que hay que entender es la **lógica** de la excepción, porque de ahí salen las condiciones sin necesidad de memorizarlas.

La excepción existe porque «uso listado» no siempre significa «riesgo real». Un sistema que ordena alfabéticamente los expedientes de un ámbito listado no amenaza los derechos de nadie. Extender el régimen completo de alto riesgo a esa tarea sería desproporcionado y desprestigiaría la norma.

De ahí, tres condiciones que son casi deducibles:

- **La tarea debe ser realmente accesoria**, no una parte con peso en el resultado. Una tarea preparatoria, un formato, una mejora de una actividad ya hecha por una persona.
- **La influencia sobre la decisión no puede ser material.** Si cambiar la salida del sistema cambia el resultado para la persona, hay influencia material aunque haya un humano firmando.
- **Hay que documentarlo y poder demostrarlo**, no solo declararlo.

Y una trampa: la excepción no aplica cuando el sistema **elabora perfiles de personas físicas**. Antes de invocar la excepción hay que descartar eso, y en un agente que lee relatos de asegurados no es una comprobación trivial.

> Fuentes primarias a abrir: Reglamento (UE) 2024/1689, artículo 6 (apartados sobre la excepción) y considerandos correspondientes, en EUR-Lex; directrices de la Comisión sobre clasificación, si publicadas.

### 6. El papel del humano en la clasificación

Aquí se concentra el malentendido más caro de todo el curso, así que conviene separar dos preguntas que suelen confundirse en una.

**Pregunta 1: ¿la presencia de un humano cambia la clasificación?**
A veces sí, pero no por estar presente. Cambia si su intervención hace que el sistema **no influya materialmente en el resultado**. Es decir, la relevancia del humano se mide por su efecto sobre la decisión, no por su existencia en el organigrama.

**Pregunta 2: si el sistema es de alto riesgo, ¿qué exige el reglamento respecto del humano?**
Supervisión humana como uno de los requisitos del régimen. Aquí el humano no evita la categoría: es una obligación **dentro** de ella.

Confundir las dos produce el razonamiento circular que se oye en todas las reuniones: «somos alto riesgo, así que ponemos supervisión humana; y como hay supervisión humana, ya no somos alto riesgo». No funciona en ninguna dirección.

En Meridiana la distinción es concreta. En la etapa de resolución, un tramitador aprueba siempre, y por encima de 1.500 € con revisión completa. Eso es un argumento para la pregunta 1 —si la revisión es real, el sistema propone pero no decide— y a la vez sería el cumplimiento del requisito de supervisión si la conclusión fuera alto riesgo.

El mismo hecho sirve para las dos cosas. Lo que no puede es servir dos veces en la misma dirección.

### 7. Qué cuenta como supervisión humana efectiva

Si la clasificación se apoya en el humano, el humano tiene que aguantar el peso. Y «aguantar el peso» tiene indicadores medibles, no declaraciones.

Cinco condiciones. Ninguna es opcional:

- **Competencia.** La persona entiende qué hace el sistema, cuáles son sus límites conocidos y en qué casos suele equivocarse. Un tramitador que no sabe que el modelo falla con matrículas ilegibles no puede supervisar esa parte.
- **Información suficiente.** Ve el resultado **y en qué se basó**. Si la pantalla muestra «importe propuesto: 1.240 €» sin los campos extraídos ni el fragmento del relato del que salen, no hay nada que supervisar.
- **Tiempo material.** Si el objetivo es bajar de 11 a 4 días y cada tramitador lleva un volumen que le deja segundos por expediente, la supervisión es imposible por aritmética, no por actitud.
- **Autoridad real para discrepar.** Puede rechazar la propuesta sin justificar más de lo que justificaría una decisión propia, y sin que rechazar penalice su productividad medida.
- **Registro de la discrepancia.** Se puede contar cuántas veces el humano cambió la propuesta. Sin ese dato no se puede demostrar nada.

El quinto punto es el que convierte todo lo anterior en verificable, y sale del sistema técnico que ya tienes: **la tasa de modificación humana es una métrica**, igual que la latencia o el coste. Se instrumenta en el mismo sitio que las trazas del curso 3.

Si esa tasa es del 0,3 %, tienes un problema. No prueba que el sistema sea perfecto: prueba que nadie está mirando.

### 8. El error de creer que «hay un humano» resuelve la clasificación

El patrón tiene nombre en la literatura de factores humanos: **sesgo de automatización**. Una persona que revisa propuestas correctas durante semanas deja de revisarlas. No por dejadez: porque su cerebro aprende, correctamente, que revisar no aporta.

El resultado es un humano que aprueba, no que decide. Y un humano que aprueba no sostiene ninguna clasificación.

Las tres formas en que aparece en un flujo como el de Meridiana:

- **La aprobación de un clic.** El caso por debajo del umbral se aprueba con un botón. Si el 96 % de los casos pasa por ahí, la revisión completa cubre el 4 % y la clasificación se apoya en ese 4 %.
- **La revisión que llega tarde.** El humano revisa después de que la petición de documentación ya haya salido al asegurado. Puede corregir, pero el efecto ya se produjo.
- **La revisión sin alternativa práctica.** El tramitador puede rechazar la propuesta, pero rechazar significa rehacer el expediente a mano en 25 minutos. Nadie lo hace dos veces.

La prueba honesta, y cabe en una tarde: **coge 50 expedientes aprobados y pregunta al tramitador por cinco de ellos**. Si no recuerda haberlos visto, no los vio.

Ese experimento produce el dato más incómodo y más valioso del expediente. Un documento de clasificación que lo incluye —con el resultado, sea cual sea— vale más que uno que afirma que la supervisión es efectiva sin haberlo comprobado nunca.

### 9. Clasificar Meridiana: los argumentos a favor de alto riesgo

Se escriben primero los argumentos contrarios a la conclusión que le conviene a la compañía. Es la única forma de saber si la conclusión aguanta.

Cinco argumentos serios a favor de considerar el sistema de alto riesgo:

- **Produce efectos económicos directos sobre personas físicas.** El resultado de un siniestro afecta al patrimonio del asegurado, y en el caso de un lesionado, a su asistencia. Un coste medio de 1.850 € no es trivial para la mayoría de los asegurados.
- **El triaje decide, no propone.** En la etapa 2, salvo la regla de lesiones, la vía de tramitación la elige el sistema. Una vía mal elegida cambia qué documentación se pide y cuánto tarda el expediente.
- **Hay elaboración de perfil, al menos en potencia.** El sistema lee un relato personal, extrae circunstancias y las cruza con historial. Si eso se usa para modular el trato, la excepción por tarea accesoria se complica.
- **El volumen convierte errores raros en errores frecuentes.** 32.000 siniestros al año significan que un fallo del 0,5 % son 160 personas afectadas.
- **La supervisión humana no cubre todo el flujo.** El humano aprueba la resolución, no el triaje ni la petición de documentación. Las etapas 2 y 3 corren sin revisión previa.

El último es el argumento más fuerte y el que menos se ve, porque la conversación siempre se va a la etapa 4, donde el humano sí está. La clasificación se hace por uso, y hay usos de Meridiana donde no hay humano ninguno.

Recorrido el Anexo III punto por punto en el texto consolidado el 31/08/2026, **ninguno de los ocho ámbitos describe la tramitación de siniestros**, y el único punto que menciona seguros —el **5, letra c)**— está acotado a «la evaluación de riesgos y la fijación de precios en relación con las personas físicas en el caso de los seguros de vida y de salud»: ni la función ni el ramo de Meridiana. Los puntos que un revisor hostil intentaría forzar, y que por eso hay que descartar por escrito citándolos, son el **5, letra b)** (solvencia y calificación crediticia, con su excepción para la detección de fraude financiero), el **5, letra a)** (prestaciones esenciales de asistencia pública) y el **5, letra d)** (llamadas de emergencia y triaje de pacientes).

Eso deja los cinco argumentos de arriba donde deben estar: son argumentos de **riesgo real y de elaboración de perfiles**, no de encaje literal en el anexo. El tercero es el que sigue vivo después de esta comprobación, porque el corte del artículo 6, apartado 3, párrafo tercero, sobre la elaboración de perfiles opera dentro del Anexo III, y el quinto —que hay usos sin humano ninguno— tampoco se resuelve leyendo el anexo.

> Fuentes primarias a abrir: Reglamento (UE) 2024/1689, Anexo III y artículo 6, en EUR-Lex; considerandos sobre elaboración de perfiles.

### 10. Clasificar Meridiana: los argumentos en contra

Ahora los del otro lado, con la misma exigencia. Un argumento en contra que no resistiría a un auditor no vale escribirlo.

- **No decide el acceso a un producto ni a un servicio esencial.** El asegurado ya tiene póliza y ya tiene derecho. El sistema ejecuta un contrato vigente, no decide si alguien entra.
- **Las decisiones con consecuencias están en código determinista, no en el modelo.** La tabla del curso 3 es explícita: el modelo extrae y redacta; el código decide vía, coberturas y documentos. Las coberturas se leen del contrato, no se infieren.
- **La regla de lesiones no la toca el modelo.** El caso más grave posible —hay un herido— se deriva por una regla determinista que además deriva ante la duda. Es el escenario de mayor riesgo y está fuera del alcance del sistema de IA.
- **El importe lo aprueba siempre una persona.** El agente nunca paga. Y el umbral de 1.500 € es un parámetro visible del sistema, no una constante escondida en un prompt.
- **Todo es reconstruible.** Existe traza por caso, evals en CI y versionado de prompts. Un error se detecta y se explica, y el asegurado tiene la vía de reclamación ordinaria intacta.

El conjunto apunta a que el sistema **asiste** una tramitación cuyas decisiones relevantes son deterministas o humanas. Es un argumento honesto y bastante sólido.

Ojo con la trampa: cuatro de los cinco puntos son afirmaciones **sobre el estado actual de la implementación**. No son propiedades del negocio. Cambian con un despliegue.

### 11. La decisión y su justificación escrita

Llega el momento de escribir la conclusión. Un documento de clasificación defendible tiene una forma reconocible, y la forma no es opcional.

**La conclusión va primero**, en una frase, con la categoría y el uso al que se refiere. Nada de suspense. Quien lee esto es un auditor con veinte documentos encima de la mesa.

**Después, los hechos en los que se apoya**, cada uno verificable de forma independiente por alguien que no estaba en la reunión. «El modelo no decide la vía en caso de lesiones» no es un hecho: es una afirmación. El hecho es «la regla vive en `triaje.py:47`, tiene test asociado, y el test de arquitectura impide moverla a un prompt». Eso se comprueba abriendo el repositorio.

**Después, los argumentos contrarios y por qué no prevalecen.** Un documento que solo argumenta en una dirección se lee como un alegato, y un auditor lo trata como tal.

**Después, las condiciones de validez.** Esta es la sección que casi nadie escribe y la que salva el documento un año después: bajo qué supuestos la conclusión es cierta, y qué cambio la invalidaría.

**Y al final, quién firma y cuándo.** Con nombres. Una clasificación anónima no compromete a nadie y por eso no se revisa nunca.

Una prueba de calidad antes de darlo por bueno: dáselo a alguien que no haya participado y pídele que ataque la conclusión. Si no encuentra por dónde, es que no has escrito los supuestos.

### 12. Qué cambiaría la clasificación: si el agente decidiera solo

El experimento mental que hace concreto todo lo anterior. Imagina que producto propone lo siguiente, y suena razonable en la reunión: *«los siniestros por debajo de 400 €, sin lesiones y con parte amistoso completo, se resuelven y se pagan sin intervención humana»*.

Parece un cambio menor. Afecta a una fracción de los casos, todos sencillos, y encaja perfectamente con el objetivo de bajar de 11 a 4 días.

Lo que cambia en la clasificación:

- **Desaparece el humano de la etapa 4** para ese subconjunto. Cuatro de los cinco argumentos en contra de la slide 10 dejan de aplicar a esos casos.
- **El sistema pasa a decidir sobre un derecho económico** del asegurado sin revisión previa. Es exactamente el supuesto que la excepción por tarea accesoria no cubre.
- **Aparece una decisión de denegación automática**, porque el mismo umbral que aprueba también rechaza. Y una denegación automática afecta al derecho contractual de una persona.
- **La reclamación cambia de naturaleza.** Antes, un asegurado descontento discutía con un tramitador que había decidido. Ahora discute con un sistema, y hay que poder explicarle la decisión.

Ninguna línea del modelo ha cambiado. El prompt es el mismo, las evals son las mismas, el modelo es el mismo. Ha cambiado **un parámetro de producto**, y con él la clasificación.

Por eso el umbral de aprobación automática debe estar en la lista de elementos cuya modificación dispara una revisión de la clasificación. Es el bloque de la slide 15.

### 13. Documentar la clasificación: qué debe contener

El contenido mínimo del documento, en el orden en que se lee bien. Diez apartados, ninguno largo:

1. **Identificación del sistema y de la versión.** Qué versión concreta se clasifica, con referencia al repositorio y al despliegue. Una clasificación sin versión no se puede vincular a nada.
2. **Descripción del uso previsto**, en lenguaje de negocio, y de los usos razonablemente previsibles que no se pretenden pero ocurrirán.
3. **Mapa de componentes**, separando inferencia de código determinista.
4. **Personas afectadas** y naturaleza de los efectos sobre ellas.
5. **Recorrido del árbol de decisión**, paso a paso, incluidos los descartados con su motivo.
6. **Conclusión** por uso, no una sola para todo el sistema.
7. **Argumentos contrarios** y por qué no prevalecen.
8. **Condiciones de validez** y lista de cambios que obligan a rehacerla.
9. **Evidencias**, con enlaces: tests, evals, trazas de ejemplo, registro de despliegues, métricas de supervisión humana.
10. **Firmas y fechas**: quién lo redactó, quién lo revisó, quién lo aprobó.

El apartado 9 es el que separa un documento vivo de uno muerto. Si las evidencias son enlaces a artefactos que se regeneran solos —el informe de evals de CI, el panel de tasa de modificación humana— el documento se mantiene actualizado sin esfuerzo. Si son capturas de pantalla pegadas, caduca el día que se firma.

Sobre la excepción por tarea accesoria, el reglamento **no fija un contenido mínimo formal para el documento de evaluación**: el **artículo 6, apartado 4**, solo exige documentarla antes de la introducción en el mercado o la puesta en servicio y facilitarla a las autoridades a petición. Lo que sí está tasado es **la obligación de registro**, y ahí sí hay una lista literal. El artículo 6, apartado 4, remite al **artículo 49, apartado 2**, que obliga al proveedor a registrar el sistema y a sí mismo en la base de datos de la UE del artículo 71 **antes** de introducirlo en el mercado. Y el contenido de esa inscripción es el **Anexo VIII, sección B**: nombre, dirección y datos de contacto del proveedor (1), de quien presente la información en su nombre (2) y del representante autorizado (3); nombre comercial y referencia inequívoca que permita identificar y trazar el sistema (4); descripción de la finalidad prevista (5); **«la condición o condiciones previstas en el artículo 6, apartado 3, con arreglo a las cuales se considera que el sistema de IA no es de alto riesgo»** (6); y la situación del sistema —comercializado, retirado, recuperado— (8). Los puntos 7 y 9 fueron suprimidos por el Reglamento (UE) 2026/1744.

El punto 6 es el que convierte esta slide en algo operativo: **hay que decir públicamente qué letra del artículo 6, apartado 3, se invoca**. Elegir la letra es, en la práctica, el resultado del documento de clasificación. Verificado en el texto consolidado de EUR-Lex el 31/08/2026.

> Fuentes primarias a abrir: Reglamento (UE) 2024/1689, artículo 6 y Anexo con el contenido de la documentación técnica, en EUR-Lex.

### 14. Revisar la clasificación: cuándo hay que rehacerla

Una clasificación es una foto de un sistema en una fecha. El sistema cambia; la foto no.

Hay dos disparadores, y el segundo es el que falla en todas las organizaciones.

**Disparador por calendario.** Una revisión periódica agendada, con dueño y con acta aunque la conclusión no cambie. La frecuencia razonable se decide según cuánto cambia el sistema; en un producto con despliegues semanales, anual es demasiado espaciado.

**Disparador por evento.** Aquí está el trabajo real. Hay una lista de cambios que obligan a rehacer la clasificación, y esa lista tiene que estar **enganchada al proceso de desarrollo**, no en un documento que nadie abre.

La forma que funciona, y es la única que he visto sostenerse: una comprobación en el flujo de cambios. Cuando un *pull request* toca uno de los ficheros o parámetros marcados, se exige una etiqueta que diga si afecta a la clasificación, y quién lo ha valorado.

- El umbral de aprobación automática y cualquier parámetro que module la intervención humana.
- El conjunto de tools disponibles para el agente.
- Las reglas de triaje deterministas.
- El alcance del sistema: nuevos ramos, nuevos canales, nuevos tipos de decisión.
- El proveedor o la familia del modelo.

Si esa comprobación vive en CI, la revisión de la clasificación ocurre. Si vive en una política, ocurre el primer trimestre y luego no.

**No existe una periodicidad numérica de revisión de la clasificación** en el reglamento: buscada en el texto consolidado el 31/08/2026, no hay ningún «cada X meses». Lo que hay es una obligación de continuidad, que es más exigente y peor de auditar. El **artículo 9, apartado 2**, define el sistema de gestión de riesgos como «un **proceso iterativo continuo** planificado y ejecutado durante todo el ciclo de vida de un sistema de IA de alto riesgo, que requerirá **revisiones y actualizaciones sistemáticas periódicas**». La periodicidad la pones tú y tienes que poder justificarla.

Lo que sí está definido con precisión es el disparador. La **«modificación sustancial»** es el **artículo 3, punto 23**: «un cambio en un sistema de IA tras su introducción en el mercado o puesta en servicio **que no haya sido previsto o proyectado en la evaluación de la conformidad inicial** realizada por el proveedor y a consecuencia del cual se vea afectado el cumplimiento de los requisitos del capítulo III, sección 2, **o que dé lugar a una modificación de la finalidad prevista**». Su consecuencia es el **artículo 43, apartado 4**: los sistemas ya evaluados «se someterán a un nuevo procedimiento de evaluación de la conformidad en caso de modificación sustancial».

Y hay una válvula de escape que encaja exactamente con la lista de disparadores de esta slide: el mismo apartado 4 aclara que, en sistemas que siguen aprendiendo, los cambios «**predeterminados por el proveedor en el momento de la evaluación inicial** de la conformidad y [que] figuren en la información recogida en la documentación técnica mencionada en el anexo IV, punto 2, letra f), **no constituirán modificaciones sustanciales**». Traducido: el umbral de aprobación automática que hayas declarado como parámetro variable, con su rango, no dispara reevaluación; el que muevas fuera de lo declarado, sí. Documentar el rango por adelantado es, literalmente, comprar margen. Verificado en el texto consolidado de EUR-Lex el 31/08/2026.

> Fuentes primarias a abrir: Reglamento (UE) 2024/1689, disposiciones sobre modificación sustancial y sobre el sistema de gestión de riesgos, en EUR-Lex.

### 15. Cambios que reclasifican sin que nadie se dé cuenta

Los cambios peligrosos no vienen etiquetados como peligrosos. Vienen como mejoras.

Seis que reclasifican y que ninguna organización detecta a la primera:

- **Subir el umbral de aprobación automática de 1.500 € a 3.000 €.** Lo pide operaciones para bajar el tiempo de tramitación. Duplica el volumen de decisiones sin revisión completa.
- **Añadir una tool que consulta el historial de siniestros del asegurado.** El equipo la añade para evitar pedir documentos ya aportados. De paso, el sistema empieza a cruzar comportamiento pasado con el caso actual: eso se parece mucho a elaborar un perfil.
- **Extender el agente a otro ramo.** De auto a hogar es «lo mismo con otra tabla». Salvo que el ramo nuevo tenga usos que sí figuran en el Anexo III.
- **Usar el sistema para priorizar la cola de tramitadores.** Suena a eficiencia interna. Si la prioridad determina quién cobra antes, tiene efectos sobre personas.
- **Reutilizar las extracciones para alimentar el modelo de tarificación.** El dato nació en tramitación y acaba decidiendo el precio de renovación: otro uso, otra clasificación, y probablemente otra familia del anexo.
- **Cambiar la interfaz para que el tramitador vea solo la propuesta y no los campos extraídos.** Nadie lo considera un cambio funcional. Destruye la supervisión efectiva.

El patrón: **ninguno de los seis es un cambio en el modelo**. Cinco son de producto y uno es de interfaz. Por eso una gobernanza que solo vigila el repositorio del agente no ve venir ninguno.

La contramedida es organizativa: la lista de disparadores de la slide 14 tiene que estar delante de quien escribe las historias de usuario, no solo de quien escribe el código.

### 16. Consecuencias prácticas de cada categoría

Clasificar no es un trámite: cada categoría abre una carga de trabajo distinta, y conviene tenerla presente antes de discutir, porque explica por qué la conversación se pone tensa.

- **Prohibido.** No hay proyecto. La única salida es cambiar el uso.
- **Alto riesgo.** Sistema de gestión de riesgos documentado, gobierno de los datos de entrenamiento y prueba, documentación técnica completa, registro automático de eventos, información al responsable del despliegue, supervisión humana diseñada, requisitos de precisión y robustez, evaluación de conformidad, declaración y marcado, registro en la base de datos correspondiente, y vigilancia poscomercialización. Es un programa de trabajo de meses, no un documento.
- **Riesgo limitado.** Obligaciones de transparencia. Semanas de trabajo, sobre todo de interfaz y de plantillas.
- **Riesgo mínimo.** Sin obligaciones específicas del reglamento. Siguen aplicando RGPD y normativa sectorial.

La asimetría entre el segundo y el tercero es enorme, y de ahí sale la presión que sentirás en la reunión de clasificación. Es la razón por la que la slide 25 existe.

Una recomendación de método que evita esa presión: **estima el coste de cada categoría antes de clasificar, no después**. Si la conversación empieza sabiendo lo que cuesta cada rama, la discusión es sobre hechos. Si empieza sin saberlo, alguien descubre el coste a mitad y a partir de ahí ya no está clasificando: está buscando la salida barata.

El listado exacto, para que la estimación de coste se haga sobre rúbricas reales y no sobre un resumen. Los **requisitos** son el capítulo III, **sección 2, artículos 9 a 15**: sistema de gestión de riesgos (9), datos y gobernanza de datos (10), documentación técnica (11), conservación de registros (12), transparencia y comunicación de información a los responsables del despliegue (13), supervisión humana (14), y precisión, solidez y ciberseguridad (15). Las **obligaciones por papel** son la **sección 3, artículos 16 a 27**: obligaciones de los proveedores (16), sistema de gestión de la calidad (17), conservación de la documentación (18), archivos de registro generados automáticamente (19), medidas correctoras y obligación de información (20), cooperación con las autoridades competentes (21), representantes autorizados (22), importadores (23), distribuidores (24), responsabilidades a lo largo de la cadena de valor (25), obligaciones de los responsables del despliegue (26) y evaluación de impacto relativa a los derechos fundamentales (27). A eso se suman la evaluación de la conformidad (43), la declaración UE de conformidad (47), el marcado CE (48), el registro (49) y la vigilancia poscomercialización (72).

Los **supuestos de transparencia** son los cuatro apartados del **artículo 50**: interacción directa con personas [1] y marcado legible por máquina del contenido sintético [2], sobre el proveedor; información a las personas expuestas a reconocimiento de emociones o categorización biométrica [3] y divulgación de ultrafalsificaciones y de texto publicado sobre asuntos de interés público [4], sobre el responsable del despliegue. Verificado en el texto consolidado de EUR-Lex el 31/08/2026.

> Fuentes primarias a abrir: Reglamento (UE) 2024/1689, capítulos sobre requisitos de los sistemas de alto riesgo, obligaciones de los operadores y obligaciones de transparencia, en EUR-Lex.

### 17. Plantilla de clasificación (ejercicio)

La plantilla del bloque tiene tres hojas y un documento. La estructura importa porque separa lo que se afirma de lo que se prueba.

**Hoja `usos`.** Una fila por uso, no por sistema. Para Meridiana: FNOL, triaje, petición de documentación, propuesta de resolución. Columnas: qué hace · sobre quién produce efectos · quién decide · si hay revisión humana previa al efecto · conclusión provisional.

**Hoja `arbol`.** Una columna por uso y una fila por paso del árbol de la slide 2. Cada celda: sí/no y una referencia a la evidencia. Las celdas vacías son el mapa de lo que falta.

**Hoja `evidencias`.** Identificador · qué demuestra · dónde está · quién la mantiene · fecha de la última comprobación. Aquí van los tests, las evals, las trazas de ejemplo y las métricas de supervisión.

**Documento de clasificación.** Los diez apartados de la slide 13, con la conclusión arriba.

La regla que hace que la plantilla funcione: **ninguna celda de la hoja `arbol` puede rellenarse sin apuntar a una fila de `evidencias`**, salvo que se marque explícitamente como supuesto pendiente de comprobar. Es tedioso los primeros veinte minutos y después es lo que impide que el documento se llene de afirmaciones cómodas.

Cuando termines, cuenta cuántas celdas son supuestos sin evidencia. Ese número es la medida honesta de cuánto sabes realmente de tu sistema.

### 18. Sistemas compuestos: clasificar el conjunto o las partes

Meridiana no es «un sistema de IA». Es un producto con varios componentes, algunos de los cuales infieren. La pregunta de si se clasifica el conjunto o las partes tiene una respuesta que decepciona: **las dos cosas, y en este orden**.

**Primero por uso.** Cada función que produce un efecto sobre una persona se clasifica por separado, porque el Anexo III describe usos. FNOL, triaje, petición y resolución se recorren por separado.

**Después el conjunto.** Porque el efecto agregado puede ser mayor que el de las partes. Cuatro componentes individualmente accesorios, encadenados, pueden producir una decisión que ninguno produce por sí solo. Si la salida de uno alimenta al siguiente sin intervención humana intermedia, la cadena es la unidad relevante, no el eslabón.

La prueba práctica es una pregunta: **¿hay algún punto entre la entrada del asegurado y el efecto sobre él donde una persona pueda parar el proceso con conocimiento de causa?** Si lo hay, tienes dos subsistemas y se clasifican por separado. Si no lo hay, tienes uno solo, por muchos servicios que tenga desplegados.

En Meridiana ese punto existe antes de la resolución. No existe entre el FNOL, el triaje y la petición de documentación: esas tres corren seguidas y el asegurado recibe un correo sin que nadie lo haya mirado. Esas tres son una unidad.

Documentar el sistema como cuatro piezas independientes cuando tres van encadenadas es la forma más común de clasificar a la baja sin darse cuenta.

### 19. Un mismo modelo en dos productos con clasificaciones distintas

El modelo no tiene categoría. La categoría es del **sistema en su uso**. Esto suena obvio y produce errores caros en cuanto una organización tiene dos productos.

El caso de Meridiana: el mismo modelo de extracción que lee relatos de FNOL sirve para dos cosas. En tramitación, extrae campos de un siniestro de un cliente existente. En el equipo de suscripción, alguien lo reutiliza para extraer datos de las solicitudes de alta y ayudar a decidir admisiones.

Es literalmente el mismo servicio, el mismo prompt base y el mismo despliegue. Y son dos sistemas distintos a efectos de clasificación, porque el uso previsto y las personas afectadas son distintos: uno ejecuta un contrato existente, el otro condiciona el acceso a un producto.

Tres consecuencias operativas:

- **El inventario se lleva por uso, no por despliegue.** Un inventario que lista «servicio de extracción» como una entrada no sirve para nada normativo.
- **Reutilizar un componente entre productos es un cambio que dispara clasificación**, en el producto que lo recibe. Añadirlo a la lista de la slide 14.
- **Quien reutiliza asume el papel que le corresponde.** El equipo de suscripción no puede apoyarse en la clasificación del equipo de tramitación: es otro uso.

La señal de alarma es una frase que se oye a menudo: «ese componente ya está aprobado». Los componentes no se aprueban. Se aprueban usos.

### 20. Obligaciones de transparencia cuando no hay alto riesgo

Concluir que un sistema no es de alto riesgo no cierra el expediente: abre la comprobación del régimen de transparencia, que es más barato y se olvida más.

La lógica es la asimetría de información: una persona que no sabe que interactúa con un sistema automatizado no puede ajustar su comportamiento ni pedir un humano. La obligación repara esa asimetría.

En Meridiana, los puntos concretos, ninguno en el modelo:

- **El chat del portal.** Que se sepa desde la primera pantalla que la respuesta es automática, y cómo llegar a una persona.
- **El correo de petición de documentación.** Redactado por el modelo. Si lleva la firma de un tramitador que no lo escribió, el problema excede lo normativo.
- **Los resúmenes del expediente** que se muestran al asegurado.
- **El canal telefónico transcrito**, si en algún momento hay respuesta automática.

Dos errores frecuentes al implementarlas. El primero: ponerlo en los términos y condiciones. Una obligación de transparencia se cumple **en el momento y el lugar de la interacción**, no en un documento que nadie lee. El segundo: redactarla en lenguaje jurídico. «Este servicio puede emplear sistemas de tratamiento automatizado» no informa a nadie; «te responde un asistente automático, pulsa aquí para hablar con una persona» sí.

El supuesto que activa la obligación en el chat del portal es el **artículo 50, apartado 1**, y recae sobre el **proveedor**: garantizar que los sistemas «destinados a interactuar directamente con personas físicas se diseñen y desarrollen de forma que las personas físicas de que se trate estén informadas de que están interactuando con un sistema de IA». Su excepción es la que hay que leer despacio antes de invocarla: la obligación no se aplica «cuando resulte evidente desde el punto de vista de una persona física razonablemente informada, atenta y perspicaz, teniendo en cuenta las circunstancias y el contexto de utilización». El estándar es esa persona, no el criterio del equipo de producto. La otra excepción, irrelevante aquí, es la de los sistemas autorizados por ley para detectar, prevenir, investigar o enjuiciar delitos.

El apartado 3 impone al **responsable del despliegue** informar del funcionamiento del sistema a las personas expuestas a reconocimiento de emociones o categorización biométrica —lo que convierte el punto sobre el canal telefónico en una comprobación obligatoria, no en una prudencia—. Y el **apartado 5** resuelve los dos errores de implementación que cierran esta slide: la información se facilitará «de manera clara y distinguible **a más tardar con ocasión de la primera interacción o exposición**», y «se ajustará a los requisitos de accesibilidad aplicables». Un aviso enterrado en los términos y condiciones incumple el apartado 5 por sí solo. Verificado en el texto consolidado de EUR-Lex el 31/08/2026.

> Fuentes primarias a abrir: Reglamento (UE) 2024/1689, capítulo sobre obligaciones de transparencia, en EUR-Lex.

### 21. Contenido generado: etiquetado y sus supuestos

El régimen de contenido generado o manipulado por IA persigue un problema distinto del anterior: no que la persona sepa con quién habla, sino que **el contenido pueda identificarse como generado** aunque circule fuera de su contexto original.

Eso se traduce en dos planos que conviene no mezclar:

- **Marcado técnico**, legible por máquina, que viaja con el archivo o el texto. Corresponde típicamente a quien genera el contenido.
- **Información visible** a la persona cuando el contenido se le presenta. Corresponde típicamente a quien lo despliega.

Los dos planos son literalmente los apartados 2 y 4 del **artículo 50**, y el reparto es el que anticipa esta slide.

**Marcado técnico, sobre el proveedor** [apartado 2]: los proveedores de sistemas que «generen contenido sintético de audio, imagen, vídeo o texto» velarán por que los resultados de salida «estén marcados en un formato legible por máquina y que sea posible detectar que han sido generados o manipulados de manera artificial», con soluciones «eficaces, interoperables, sólidas y fiables en la medida en que sea técnicamente viable». **Cubre el texto**, y por eso alcanza al correo de petición de documentación. Su excepción: no se aplica «en la medida en que los sistemas de IA desempeñen una función de apoyo a la edición estándar o no alteren sustancialmente los datos de entrada facilitados por el responsable del despliegue o su semántica».

**Información visible, sobre el responsable del despliegue** [apartado 4]: hacer público que el contenido es artificial en las ultrafalsificaciones de imagen, audio o vídeo, con un régimen atenuado cuando forme parte de «una obra o programa manifiestamente creativos, satíricos, artísticos, de ficción o análogos»; y divulgar que el texto se ha generado o manipulado artificialmente cuando se publique «con el fin de informar al público sobre asuntos de interés público». Aquí está la excepción por revisión editorial, con su condición: no se aplica «cuando el contenido generado por IA haya sido sometido a un proceso de revisión humana o de control editorial y cuando una persona física o jurídica tenga la **responsabilidad editorial** por la publicación del contenido». Nótese que es acumulativa: revisión humana **y** alguien que responda editorialmente.

El **apartado 7**, añadido por el Reglamento (UE) 2026/1744, encarga a la Comisión fomentar códigos de buenas prácticas sobre detección, marcado y etiquetado, y le permite adoptar un acto de ejecución con normas comunes si los considera inadecuados. Es decir: el *cómo* técnico de este artículo todavía se está escribiendo. Verificado en el texto consolidado de EUR-Lex el 31/08/2026.

Lo interesante para Meridiana es dónde aparece esto sin que nadie lo espere. El correo de petición de documentación lo redacta el modelo. La motivación de la propuesta de resolución también. Ese texto acaba en el expediente, y el expediente puede acabar en un procedimiento.

De ahí una práctica que conviene adoptar aunque la obligación concreta no te aplique: **marcar en la traza qué texto del expediente fue generado, con qué versión de prompt y de modelo, y si un humano lo modificó antes de enviarlo**. No es un requisito de etiquetado; es lo que permite responder dentro de dos años a «¿esta frase la escribió una persona?».

Ese metadato ya lo tienes si hiciste bien las trazas del curso 3. Solo hay que decidir que forma parte del expediente.

> Fuentes primarias a abrir: Reglamento (UE) 2024/1689, disposiciones sobre marcado de contenido generado por IA, en EUR-Lex.

### 22. Prácticas prohibidas que aparecen sin querer en seguros

Ninguna aseguradora se propone incurrir en una práctica prohibida. Llegan como propuestas razonables en una reunión de producto, y por eso hay que reconocerlas por su forma, no por su nombre.

Cuatro ideas que se plantean con naturalidad en el sector y que hay que parar y analizar antes de prototipar:

- **Puntuar la fiabilidad general del asegurado combinando señales ajenas al seguro.** Historial de siniestros más comportamiento en redes, más datos de consumo. Cuando la puntuación se usa para tratarlo peor en ámbitos no relacionados con el origen del dato, se acerca a la puntuación social.
- **Inferir características sensibles a partir de datos que no las contienen.** Deducir estado de salud, origen o creencias del texto libre de un relato de siniestro. Aunque el objetivo sea benigno, la inferencia en sí es el problema.
- **Explotar la vulnerabilidad de la persona en el momento de la interacción.** Un asegurado que acaba de tener un accidente está en una situación de estrés. Ajustar la comunicación para inducirle a aceptar una oferta rápida por debajo de lo que le corresponde no es persuasión: es explotación de vulnerabilidad.
- **Reconocimiento de emociones** aplicado a la voz de la llamada para detectar fraude o modular el trato.

Contrastadas las cuatro ideas con la redacción literal del **artículo 5, apartado 1**, leída en el texto consolidado el 31/08/2026, tres tienen una letra concreta enfrente y la cuarta no. Conviene saber cuál es cuál antes de la reunión.

- **La puntuación de fiabilidad** apunta a la **letra c)**, que prohíbe evaluar o clasificar personas «durante un período determinado de tiempo atendiendo a su comportamiento social o a características personales o de su personalidad conocidas, inferidas o predichas» cuando la puntuación resultante provoque «un trato perjudicial o desfavorable […] en contextos sociales que **no guarden relación con los contextos donde se generaron o recabaron los datos** originalmente» [inciso i)] o un trato «injustificado o desproporcionado» [inciso ii)]. La regla operativa del final de esta slide es, palabra por palabra, el inciso i).
- **La explotación de la vulnerabilidad** apunta a la **letra b)**, que exige que la vulnerabilidad derive «de su edad o discapacidad, o de una situación social o económica específica» y que la alteración del comportamiento provoque «perjuicios considerables». El estrés posterior a un accidente no está nombrado en esa lista cerrada; el argumento hay que construirlo sobre la situación económica o sobre la letra a), técnicas «deliberadamente manipuladoras o engañosas».
- **El reconocimiento de emociones** está en la **letra f)**, pero **solo «en los lugares de trabajo y en los centros educativos»**, con excepción por motivos médicos o de seguridad. Aplicado a la voz del asegurado en una llamada, **no encaja en la prohibición**: no es un lugar de trabajo respecto del asegurado. Es, en cambio, un uso del Anexo III, punto 1, letra c), y activa el artículo 50, apartado 3.
- **La inferencia de características sensibles** solo está prohibida en la **letra g)** cuando se hace «sobre la base de sus **datos biométricos**». Deducir estado de salud u origen del **texto libre** de un relato no cae en la letra g). Sigue siendo un problema serio —de RGPD, de categorías especiales del artículo 9, y de elaboración de perfiles a efectos del artículo 6, apartado 3—, pero no una práctica prohibida.

La conclusión práctica no es tranquilizadora, es la contraria: **dos de las cuatro no están prohibidas y siguen siendo malas ideas**. Si la única pregunta que hace el comité es «¿está prohibido?», dirá que sí a las dos.

La regla operativa: cualquier propuesta que **infiera algo sobre la persona que la persona no ha contado**, o que **use un dato fuera del contexto en que se dio**, va a revisión antes de escribir código. No después del prototipo.

> Fuentes primarias a abrir: Reglamento (UE) 2024/1689, capítulo sobre prácticas de IA prohibidas y considerandos asociados, en EUR-Lex; directrices de la Comisión sobre prácticas prohibidas, si publicadas; documentos de EIOPA sobre IA en seguros.

### 23. El registro de sistemas de alto riesgo: quién inscribe y qué

El reglamento prevé una base de datos a escala de la Unión donde se inscriben determinados sistemas de alto riesgo, con información que en parte es pública. La inscripción es una obligación, no un trámite opcional, y recae sobre operadores concretos según su papel.

La denominación oficial es la del **artículo 71**: **«Base de datos de la UE para los sistemas de IA de alto riesgo enumerados en el anexo III»**. La crea y mantiene la Comisión en colaboración con los Estados miembros.

**Qué se inscribe y quién**, según el **artículo 49**: el **proveedor** —o su representante autorizado— inscribe el sistema y se inscribe a sí mismo antes de introducirlo en el mercado o ponerlo en servicio, tanto si es de alto riesgo del Anexo III [apartado 1] como si ha concluido que **no** lo es por el artículo 6, apartado 3 [apartado 2]. Los **responsables del despliegue** solo se inscriben cuando son autoridades públicas, instituciones, órganos u organismos de la Unión o actúan en su nombre [apartado 3]. Queda fuera en todos los casos el Anexo III, punto 2, que se registra a nivel nacional [apartado 5]. Una aseguradora privada, por tanto, se inscribe si es proveedora, no por desplegar.

**Qué es público y qué no**, artículo 71, apartado 4: la información registrada conforme al artículo 49 «será accesible y estará a disposición del público de manera sencilla», «fácil de navegar» y «legible por máquina». Las excepciones son tasadas: los ámbitos de garantía del cumplimiento del Derecho, migración, asilo y control fronterizo —Anexo III, puntos 1, 6 y 7— van a «una sección segura no pública» con información recortada [artículo 49, apartado 4], y las pruebas en condiciones reales del artículo 60 solo son visibles para autoridades y Comisión salvo consentimiento del proveedor.

**Cuándo y qué obliga a actualizar**: la inscripción es **previa** a la introducción en el mercado o puesta en servicio, y el Anexo VIII exige que la información se «facilite y se actualice debidamente», incluyendo la situación del sistema —comercializado, retirado del mercado o del servicio, recuperado—. Verificado en el texto consolidado de EUR-Lex el 31/08/2026.

Lo que conviene entender es **por qué existe**, porque de ahí sale cómo prepararse. Un registro público convierte el cumplimiento en algo comprobable desde fuera: cualquiera puede ver qué sistemas de alto riesgo hay declarados y por quién. Eso cambia los incentivos, y también cambia lo que puedes decir.

Dos consecuencias prácticas:

- **Lo que inscribes tiene que ser coherente con lo que dices en otros sitios.** La descripción del sistema en el registro, en tu documentación técnica, en tu política de privacidad y en tu web comercial es la misma descripción. Cuatro versiones distintas del mismo sistema es lo primero que detecta cualquiera que las compare.
- **La inscripción obliga a tener el uso previsto escrito con precisión** mucho antes de la inspección. Es un ejercicio que sale barato hacer pronto y caro hacer tarde.

Para Meridiana esto solo aplica si la conclusión de B2 es alto riesgo. Pero conviene redactar la descripción del uso previsto en cualquier caso: es el mismo texto que necesitas para el apartado 2 del documento de clasificación.

> Fuentes primarias a abrir: Reglamento (UE) 2024/1689, capítulo sobre la base de datos de la UE y obligaciones de registro, en EUR-Lex.

### 24. Evaluación de conformidad: las vías posibles

Si un sistema es de alto riesgo, hay que demostrar que cumple los requisitos **antes** de ponerlo en el mercado o en servicio. Ese procedimiento es la evaluación de conformidad, y existen dos formas.

**Control interno.** El propio proveedor verifica el cumplimiento, redacta la declaración de conformidad y responde de ella. No hay tercero. Es el procedimiento habitual para buena parte de los sistemas listados en el Anexo III.

**Con intervención de un organismo notificado.** Un tercero acreditado examina el sistema y su documentación. Se reserva a determinados supuestos.

El reparto de procedimientos es el **artículo 43**, y es más nítido de lo que suele contarse.

**Anexo III, puntos 2 a 8 — control interno, siempre.** El apartado 2 lo dice sin condiciones: esos proveedores «se atendrán al procedimiento de evaluación de la conformidad fundamentado en un control interno a que se refiere el anexo VI, **que no contempla la participación de un organismo notificado**». Es decir: todo el alto riesgo del Anexo III **salvo biometría** va por control interno.

**Anexo III, punto 1 (biometría) — depende de las normas.** El apartado 1 da a elegir entre el control interno del Anexo VI y el procedimiento con organismo notificado del Anexo VII **si** el proveedor ha aplicado las normas armonizadas del artículo 40 o las especificaciones comunes del artículo 41. Si no las hay, si no las ha aplicado, si existen especificaciones comunes y no las ha aplicado, o si la norma se publicó con una limitación, el **Anexo VII es obligatorio**. Ahí está la respuesta a «cuándo interviene un tercero»: cuando falta la norma o no la sigues.

**Anexo I, sección A — el régimen sectorial manda.** Apartado 3: el proveedor «se atendrá al procedimiento de evaluación de la conformidad pertinente exigido» por el acto sectorial, y los requisitos de la sección 2 «formarán parte de dicha evaluación».

**Normas armonizadas y presunción**, artículo 40, apartado 1: los sistemas conformes con normas armonizadas «cuyas referencias estén publicadas en el Diario Oficial de la Unión Europea […] se presumirá que son conformes con los requisitos establecidos en la sección 2 […] en la medida en que dichas normas contemplen estos requisitos». La presunción es el mecanismo que abarata el control interno; sin ella, cada requisito hay que demostrarlo de cero.

**Marcado y declaración**: declaración UE de conformidad (artículo 47, con el contenido del Anexo V) y **marcado CE** (artículo 48), que en sistemas suministrados digitalmente puede ser un **marcado CE digital** «únicamente si es fácilmente accesible a través de la interfaz desde la que se accede a dicho sistema» [apartado 2], y que irá seguido del número de identificación del organismo notificado cuando lo haya [apartado 4]. Verificado en el texto consolidado de EUR-Lex el 31/08/2026.

Lo que hay que llevarse de aquí, más allá del procedimiento: **el control interno no es más fácil, es más solitario**. Nadie te va a decir que tu documentación es insuficiente hasta que sea tarde. La disciplina la pones tú.

De ahí una práctica que sale barata: someter el expediente a una **revisión interna hostil** antes de firmar la declaración. Alguien de la casa que no participó, con el encargo explícito de encontrar el punto débil. Es lo mismo que la revisión por pares de la slide 26, aplicada al expediente completo en vez de a la clasificación.

Y una nota de calendario: la evaluación de conformidad es lo último. Todo lo demás —gestión de riesgos, datos, documentación, registros, supervisión— es lo que se evalúa.

> Fuentes primarias a abrir: Reglamento (UE) 2024/1689, capítulo sobre evaluación de la conformidad, normas armonizadas y marcado, y sus anexos de procedimientos, en EUR-Lex.

### 25. Errores frecuentes al autoclasificarse a la baja

Nadie se autoclasifica a la baja de mala fe. Se hace con razonamientos que suenan bien y que comparten una estructura: **empiezan por la conclusión deseada**.

Seis, con lo que falla en cada uno:

- **«Hay un humano al final.»** Solo cuenta si su supervisión es efectiva, y eso se mide. Sin tasa de modificación humana, la afirmación no está probada.
- **«El modelo solo sugiere.»** Si la sugerencia se acepta el 99,7 % de las veces, decide. La palabra que uses en la documentación no cambia el efecto.
- **«Es una herramienta interna.»** El efecto llega al asegurado igualmente. Interna es la interfaz, no la consecuencia.
- **«Nuestro caso no aparece en el anexo.»** Comprobado leyendo los títulos de los ámbitos, no la descripción de los usos. Es el error de la slide 3.
- **«El proveedor dice que no es alto riesgo.»** El proveedor clasifica su producto, no tu uso. Y tiene un incentivo evidente.
- **«Si fuera alto riesgo no podríamos permitírnoslo.»** Es la única honesta de las seis, y es la que hay que sacar a la mesa. No es un argumento de clasificación; es una restricción de negocio, y se decide arriba, con el coste delante.

La contramedida es de proceso, no de conocimiento: **la persona que redacta la clasificación no debe ser la única que la aprueba**, y quien aprueba no debe tener el objetivo de que salga barata.

Y un indicador que no falla: si en el documento de clasificación no hay ni un solo argumento en contra bien escrito, es que no se buscó ninguno.

### 26. Revisión por pares de una clasificación

La última pieza del procedimiento, y la que convierte el documento en algo con valor probatorio: alguien que no lo escribió intenta tumbarlo.

El encargo del revisor es explícito y no es «dar el visto bueno». Es **encontrar el punto por donde se rompe**. Con eso en la cabeza, un protocolo de una hora:

1. **Lee solo la conclusión** y escribe qué esperarías encontrar para creértela. Después compara con lo que hay.
2. **Ataca los hechos, no los argumentos.** Coge tres afirmaciones y ve a comprobarlas: abre el repositorio, mira la traza, pide la métrica. El objetivo es encontrar una afirmación que nadie ha verificado.
3. **Busca el uso que falta.** ¿Hay alguna función del sistema que no aparece en la hoja `usos`? Los usos olvidados son el fallo más común.
4. **Prueba las condiciones de validez.** Propón tres cambios plausibles de producto y comprueba si el documento dice qué pasaría con cada uno. Si no lo dice, faltan condiciones.
5. **Cuenta los supuestos sin evidencia.** Si son más de un puñado, la clasificación no está terminada, está esbozada.

El resultado de la revisión se anexa al documento **con las objeciones, incluidas las que no prosperaron y por qué**. Eso es lo que demuestra que hubo escrutinio real.

Un consejo sobre quién revisa: la mejor combinación en Meridiana no es dos personas de IA. Es alguien de tramitación que conozca el trabajo real de los 24 tramitadores y alguien de cumplimiento. El equipo de IA sabe qué hace el sistema; ellos saben qué pasa con él.

### 27. Ejercicio práctico 1: clasificar los cuatro usos de Meridiana {ejercicio:B2-ej1}

Rellena la plantilla de la slide 17 para los cuatro usos del agente: FNOL, triaje, petición de documentación y propuesta de resolución. Un recorrido completo del árbol por cada uno.

Reglas del ejercicio:

- **Cada celda del árbol apunta a una evidencia** de la hoja correspondiente, o se marca como supuesto pendiente. No se admite una celda rellenada «por criterio».
- **Cada afirmación normativa** lleva el marcador de verificación pendiente con el precepto que habría que abrir, o una cita con fecha de comprobación.
- **La conclusión se escribe por uso**, no una sola para el sistema entero.
- **La slide 18 se aplica al final:** después de los cuatro recorridos, decide si alguno de ellos debe tratarse como una unidad encadenada y justifícalo.

**Criterio de aceptación:** al terminar, cuenta los supuestos sin evidencia y escríbelos como una lista al final del documento. Un ejercicio con cero supuestos pendientes está mal hecho: significa que se rellenó con impresiones. La lista de lo que no sabes es la salida más valiosa del ejercicio.

### 28. Ejercicio práctico 2: la revisión hostil y el cambio que reclasifica {ejercicio:B2-ej2}

Dos partes, y la segunda solo tiene sentido después de la primera.

**Parte A — revisión por pares.** Intercambia tu clasificación con la de otro alumno y aplica el protocolo de cinco pasos de la slide 26. Devuelve un informe con las objeciones encontradas, señalando en cada una el paso del árbol al que afecta. Una objeción que no señala un paso concreto no es una objeción: es una opinión.

**Parte B — el cambio que reclasifica.** Sobre tu propia clasificación ya revisada, aplica esta propuesta de producto: *«los siniestros por debajo de 400 €, sin lesiones y con parte amistoso completo, se resuelven y se pagan sin intervención humana»*.

Escribe una nota de dos páginas que responda a tres preguntas:

- Qué apartados del documento de clasificación dejan de ser ciertos, citándolos.
- Qué evidencias habría que producir de nuevo, y cuáles siguen sirviendo.
- Qué controles habría que añadir para que la propuesta fuera viable sin cambiar la categoría, si es que alguno lo consigue.

**Criterio de aceptación:** la nota identifica al menos un apartado que deja de ser cierto y al menos una evidencia que hay que rehacer. Y dice con claridad si, en tu opinión razonada, la propuesta se puede aceptar o no. Una nota que no se moja no sirve para llevar a un comité.

### 29. Mini-quiz de comprensión — B2 {quiz:B2}

Tres preguntas sobre el criterio del bloque: qué hace que una excepción sea invocable, qué convierte a un humano en supervisión efectiva y cuándo hay que rehacer una clasificación.

Ninguna depende de recordar un número de artículo, una fecha o un importe. Lo que se evalúa es el razonamiento, que es lo que tendrás que sostener delante de un comité sin poder consultar nada.

Aprobado con dos aciertos. Puedes repetirlo las veces que quieras.

## Qué te llevas

- La clasificación es un procedimiento documentado, no una impresión.
- Poner un humano en el flujo solo cuenta si su supervisión es efectiva.
- Un cambio de producto puede reclasificar el sistema sin tocar una línea de código.

## Mini-quiz (3 preguntas)

Una sola opción correcta por pregunta, con explicación. Se generan con el mismo formato que `assets/js/quizzes-data.js` para que el importador las recoja.

1. **Tema:** Qué condiciones debe cumplir la excepción por tarea accesoria
   - **Enunciado:** El equipo quiere invocar la excepción por tarea accesoria para la etapa de triaje de Meridiana. ¿Cuál de estas situaciones haría que la excepción **no** pudiera sostenerse?
   - **Opciones:**
     - a) Que el sistema realice una tarea preparatoria cuyo resultado revisa una persona antes de que produzca efecto.
     - b) **Que cambiar la salida del sistema cambie el resultado para el asegurado, es decir, que influya materialmente en la decisión.** ✅
     - c) Que el sistema use un modelo de lenguaje en lugar de reglas escritas a mano.
     - d) Que el equipo no haya redactado todavía la documentación que justifica la excepción.
   - **Explicación:** La excepción se apoya en que el sistema no influya materialmente en el resultado; si cambiar su salida cambia lo que le pasa a la persona, hay influencia material y la excepción decae. La (a) describe justamente el tipo de tarea preparatoria compatible con la excepción. La (c) confunde tecnología con riesgo: la categoría depende del uso y del efecto, no de si hay un modelo detrás. La (d) señala un incumplimiento documental real —la excepción hay que documentarla— pero no es lo que determina si la excepción es invocable: primero se cumple la condición de fondo, después se prueba.

2. **Tema:** Qué haría que Meridiana pasara a alto riesgo
   - **Enunciado:** ¿Cuál de estos cambios tiene más probabilidad de alterar la clasificación del agente de siniestros?
   - **Opciones:**
     - a) Cambiar el proveedor del modelo por otro de mayor tamaño y mejor rendimiento en las evals.
     - b) Migrar la infraestructura a otro proveedor de nube dentro de la Unión.
     - c) **Permitir que los siniestros por debajo de un umbral se resuelvan y se paguen sin intervención humana previa.** ✅
     - d) Añadir trazas más detalladas y ampliar el conjunto de evals que corre en CI.
   - **Explicación:** La (c) elimina la revisión humana previa al efecto sobre el asegurado y convierte una propuesta en una decisión sobre un derecho económico, que es exactamente lo que sostenía la clasificación anterior. Y no toca una línea del modelo: es un parámetro de producto. La (a) y la (b) cambian la implementación sin cambiar quién decide ni sobre quién se produce el efecto. La (d) mejora la evidencia disponible, que refuerza el expediente en vez de alterarlo.

3. **Tema:** Cuándo hay que rehacer una clasificación
   - **Enunciado:** ¿Cuál es la señal más fiable de que una clasificación necesita rehacerse antes de la revisión periódica prevista?
   - **Opciones:**
     - a) Que haya pasado tiempo desde la última firma, aunque nada del sistema haya cambiado.
     - b) Que el proveedor del modelo haya publicado una nueva versión de su documentación.
     - c) Que el equipo haya crecido y haya personas nuevas que no participaron en la clasificación original.
     - d) **Que se haya modificado un elemento de la lista de disparadores: el alcance del sistema, quién decide, el margen de intervención humana o el conjunto de acciones que el agente puede ejecutar.** ✅
   - **Explicación:** Lo que invalida una clasificación es un cambio en los hechos sobre los que se construyó: qué hace el sistema, sobre quién, quién decide y con qué margen. Por eso la lista de disparadores debe estar enganchada al proceso de desarrollo y no a un documento de política. La (a) describe la revisión por calendario, que también debe existir pero es la red de seguridad, no la señal. La (b) es un insumo del expediente que rara vez cambia el uso. La (c) es un problema de continuidad y de formación, no de clasificación.

## Lab

Este curso no lleva labs de código. En su lugar, ejercicio de plantilla (DOCX/XLSX) sobre el caso Meridiana.

**Enunciado.** Produce el **expediente de clasificación de Meridiana**: la hoja de cálculo de tres pestañas y el documento de diez apartados de la slide 13, revisado por un par y con la nota de impacto del cambio de producto. Es la pieza que B4 convierte en documentación técnica, así que se escribe para durar.

**Pasos:**

1. Rellena la hoja `usos` con los cuatro usos del agente. Una fila por uso, y para cada uno: quién decide, si hay revisión humana previa al efecto, y sobre quién recae ese efecto.
2. Recorre el árbol de la slide 2 en la hoja `arbol`, una columna por uso. Cada celda apunta a una fila de `evidencias` o queda marcada como supuesto pendiente.
3. Vuelca en `evidencias` lo que ya produce el sistema desde el curso 3: tests de las reglas de triaje, informe de evals de CI, tres trazas de ejemplo (una normal, una derivada por lesiones, una con intento de inyección), registro de despliegues y métrica de tasa de modificación humana. Si esta última no existe, esa es la primera conclusión del lab.
4. Redacta el documento de clasificación con la conclusión arriba, los argumentos contrarios de la slide 9 y las condiciones de validez.
5. Somételo a revisión por pares con el protocolo de la slide 26 y anexa el informe con todas las objeciones, incluidas las que no prosperaron.
6. Añade la nota de impacto del cambio de producto del ejercicio 2, como apéndice.

**Criterios de aceptación:**

- Hay **cuatro conclusiones**, una por uso, y una decisión razonada sobre si alguna de ellas debe tratarse como cadena según la slide 18.
- Ninguna celda del árbol está rellenada sin evidencia ni marca de supuesto pendiente. Se comprueba contando.
- El documento contiene al menos tres argumentos contrarios a su propia conclusión, escritos con la misma calidad que los favorables.
- La sección de condiciones de validez enumera los disparadores de reclasificación y dice dónde vive la comprobación en el proceso de desarrollo.
- El informe de revisión por pares señala, para cada objeción, el paso del árbol afectado.
- Toda afirmación normativa lleva cita con precepto y fecha de comprobación, o queda marcada como pendiente.
- Se completa en menos de 120 minutos con el expediente del curso 3 y la ficha de B1 delante.

**Solución de referencia:** en `content/caso/soluciones/C-10/B2/`, con las tres hojas rellenadas, el documento de clasificación por los cuatro usos, el informe de revisión por pares y la nota de impacto del umbral de resolución automática.

## Cierre

- Recapitulación en los tres puntos de arriba.
- Mini-quiz.
- Enlace al siguiente bloque.
