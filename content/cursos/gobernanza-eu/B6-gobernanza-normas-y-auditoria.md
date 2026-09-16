# C-14 · B6 · Gobernanza, normas y auditoría

> Curso: `gobernanza-eu` · bloque `B6`

## Objetivo

Montar la gobernanza que sostiene todo lo anterior cuando quien lo construyó ya no está.

## ⚠ Bloque normativo: verificación obligatoria antes de publicar

Cada afirmación de este bloque debe citar artículo y fecha de verificación, y esa fecha debe salir de haber abierto la fuente primaria. No se redacta de memoria ni a partir de resúmenes de terceros.

**Fuentes primarias a consultar:**

- [x] Reglamento (UE) 2024/1689 (AI Act), texto consolidado tras el Reglamento (UE) 2026/1744, artículo 4 (alfabetización en materia de IA) — abierto y leído el 31/08/2026
- [ ] AI Act, artículos 40 y 41 (normas armonizadas y presunción de conformidad), 72 (vigilancia poscomercialización), 73 (incidentes graves) y 99 (sanciones) — **no abiertos**: el texto consolidado de EUR-Lex se trunca hacia el artículo 22
- [ ] **Parcial.** ISO/IEC 42001:2023 — confirmadas su referencia, su título y su alcance declarado en la ficha oficial de IEC/ISO; el texto de la norma es de pago y **no** se ha leído
- [ ] Listado de normas armonizadas publicadas en el DOUE — **no consultado**

**Fecha de verificación:** 31/08/2026 (verificación **parcial**: la mayoría de las obligaciones del AI Act
que cita este bloque viven en artículos que EUR-Lex no llegó a servir. Cada marcador que sigue abierto
lleva escrito su motivo. Registro completo en `VERIFICADO.md`.)

> Aviso al alumno que debe aparecer en el bloque: este material es formativo y no constituye asesoramiento jurídico.

## Guion de slides

27 slides de contenido. Una idea por slide, con un ejemplo real o del caso Meridiana. Nada de relleno.

### 1. Gobernanza: qué es, más allá de un comité

Gobernanza no es un comité, ni una política en un PDF, ni un curso obligatorio de veinte minutos con test final. Es la respuesta a una pregunta muy concreta y muy poco elegante:

> Cuando la persona que construyó el sistema se marche de la empresa, ¿quién sabe por qué el agente decide lo que decide, quién puede cambiarlo y quién se entera si cambia?

En Meridiana esa persona existe y tiene nombre. Construyó el agente, escribió las reglas de triaje, ajustó los prompts y sabe de memoria por qué el umbral está en 1.500 € y no en 2.000. Todo el conocimiento que hace que el sistema sea defendible ante una inspección está en su cabeza y en su historial de commits.

Gobernanza es el conjunto de mecanismos que hacen que esa marcha sea un contratiempo de recursos humanos y no un incidente regulatorio. Son cuatro cosas, y ninguna es un comité:

1. **Alguien nombrado** para cada decisión que el sistema puede necesitar. No un departamento: una persona, con nombre, en un documento.
2. **Un sitio donde se escriben las decisiones** y por qué se tomaron, que sobrevive a quien las tomó.
3. **Una puerta** por la que tienen que pasar los cambios relevantes antes de llegar a producción.
4. **Una revisión periódica** que compruebe que lo escrito sigue siendo verdad.

Todo lo demás de este bloque —ISO, auditorías, indicadores, incidentes— es maquinaria para sostener esas cuatro. Si las tienes con una hoja de cálculo y tres reuniones al año, tienes gobernanza. Si tienes un comité de nueve personas y ninguna de las cuatro, no.

### 2. Roles y responsabilidades escritas

«El equipo de datos se encarga» no es una responsabilidad: es una forma educada de decir que no hay nadie. Una responsabilidad escrita tiene tres partes —**qué decisión, qué persona, qué ocurre si no está**— y las tres son necesarias.

En Meridiana, la lista mínima de decisiones que necesitan dueño es más corta de lo que parece:

| Decisión | Quién decide | Suplente |
|---|---|---|
| Aprobar un caso de uso nuevo | Dirección de operaciones | Dirección técnica |
| Aprobar un cambio de modelo o de proveedor | Dirección técnica, informado el comité | Arquitecto de la plataforma |
| Cambiar el umbral de aprobación automática | Dirección de siniestros | Nadie: no se delega |
| Cambiar una regla de triaje | Dirección técnica, con visto bueno de siniestros | — |
| Declarar un incidente grave | Responsable de cumplimiento | Dirección técnica |
| Retirar el sistema de producción | Dirección técnica, sin necesidad de comité | Guardia |

Dos detalles de esa tabla valen más que el resto del bloque.

El primero: **el umbral no se delega**. Es el parámetro que decide si un expediente lo mira una persona o no, y en el B5 vimos que es justo el que determina si estamos ante una decisión automatizada con efecto sobre el asegurado. Cambiarlo de 1.500 a 3.000 € es una decisión de riesgo disfrazada de configuración.

El segundo: **retirar el sistema no necesita comité**. Si apagar requiere convocar a seis personas, no se apaga a tiempo. La autoridad para parar tiene que estar más abajo que la autoridad para arrancar; en cualquier otro reparto, la gobernanza empuja hacia dejar en marcha lo que debería estar parado.

### 3. Política de IA de la organización: qué debe decir

Una política de IA útil ocupa entre dos y cuatro páginas, la puede leer entera alguien de siniestros y responde a preguntas que la gente se hace de verdad. Casi todas las que circulan son de veinte páginas, empiezan hablando de valores y no responden a ninguna.

Lo que debe decir, en este orden:

1. **A qué se aplica.** Definir qué cuenta como «sistema de IA» a efectos internos. Sin esto, el inventario de la slide 4 es indefendible: nadie sabe qué hay que declarar.
2. **Qué está prohibido, con ejemplos.** «No se usarán datos de asegurados en herramientas de IA no aprobadas» es una frase que todo el mundo entiende. Y un ejemplo: pegar el relato de un siniestro en un asistente personal para resumirlo.
3. **Qué requiere aprobación y de quién.** Con el nombre del rol y cómo se pide.
4. **Qué se puede hacer sin pedir permiso.** Este apartado es el que hace que la política se cumpla. Si todo requiere aprobación, la gente deja de preguntar.
5. **Quién responde de qué.** La tabla de la slide 2, resumida.
6. **Cómo se revisa la política y cada cuánto.**

Y una decisión que hay que tomar explícitamente: **qué pasa con las herramientas de IA que la gente ya usa por su cuenta**. En Meridiana hay tramitadores que pegan relatos en asistentes públicos para resumirlos. Eso ya está ocurriendo. Una política que lo ignora no lo elimina: solo garantiza que nadie lo cuente.

La prueba de que la política sirve: dásela a una persona de siniestros y pregúntale si puede usar una herramienta concreta para una tarea concreta. Si tiene que llamar a alguien para saberlo, la política no está escrita para su lector.

### 4. Inventario de sistemas de IA y por qué se descuadra siempre

Sin inventario no hay gobernanza posible: no puedes clasificar, documentar ni vigilar lo que no sabes que existe. Y sin embargo el inventario está mal en todas las organizaciones, siempre, por una razón estructural que conviene entender antes de intentar arreglarlo.

**El inventario se llena con un proceso anual y el sistema cambia con un despliegue.** Un ciclo dura doce meses y el otro dos días. La divergencia no es un fallo de disciplina: es aritmética.

Los tres huecos típicos, todos presentes en Meridiana:

- **Lo que empezó como prueba.** Un cuaderno de análisis que clasifica reclamaciones lleva ocho meses corriendo cada lunes porque a alguien le resultó útil. Nadie lo declaró: era una prueba.
- **Lo que está dentro de otra cosa.** El proveedor del CRM añadió sugerencias automáticas de respuesta en la última versión. Meridiana no ha desplegado ningún sistema de IA: le ha llegado uno en una actualización.
- **Lo que cambió de categoría sin cambiar de nombre.** El agente de FNOL sigue llamándose igual, pero desde marzo también propone importes. La ficha del inventario describe el sistema del año pasado.

Qué funciona de verdad, por orden de eficacia:

- **Atar el inventario a algo que la organización ya controla.** El gasto. Cada clave de API de un proveedor de modelos aparece en una factura, y esa factura tiene un centro de coste con un responsable. Reconciliar facturas contra inventario cada trimestre encuentra más sistemas que cualquier campaña de sensibilización.
- **Hacer que declarar sea más barato que no declarar.** Un formulario de cinco campos y una respuesta en 48 horas.
- **Puerta en el despliegue.** Un sistema nuevo que llama a un proveedor de modelo no pasa a producción sin ficha. Eso es un control técnico, no una política, y por eso funciona.

### 5. Ciclo de vida: puertas de decisión y quién las abre

Un sistema con LLM tiene una vida larga y va cambiando por debajo. La gobernanza consiste en poner un número pequeño de puertas —tres o cuatro— en los puntos donde el riesgo cambia de verdad, y dejar libre todo lo demás. Poner puertas en cada paso produce el efecto contrario al deseado: la gente aprende a rodearlas.

Las puertas del caso:

| Puerta | Cuándo | Quién la abre | Qué se comprueba |
|---|---|---|---|
| Concepción | Antes de escribir código | Operaciones + cumplimiento | Finalidad, clasificación preliminar (B2), si hay datos personales (B5) |
| Puesta en producción | Antes del primer usuario real | Dirección técnica + cumplimiento | Expediente técnico (B4), EIPD, evals, plan de retirada |
| Cambio sustancial | Cuando cambia lo que el sistema decide | Comité de riesgos | Reclasificación (B2), actualización del expediente |
| Retirada | Al apagar | Dirección técnica | Registros conservados, interesados informados si procede |

La puerta difícil es la tercera, porque exige definir qué es un cambio sustancial, y esa definición no puede depender de la opinión de quien hace el cambio. En Meridiana está escrita como una lista de disparadores concretos:

- Cambia el modelo o su versión mayor.
- Cambia una regla de triaje o el umbral de aprobación.
- El sistema empieza a decidir algo que antes solo proponía.
- Se amplían los datos que entran en el contexto.
- Se añade un canal de entrada nuevo.

Cualquiera de los cinco dispara revisión. Un ajuste de redacción de un prompt, no. La virtud de una lista así no es que sea perfecta: es que **quien hace el cambio no tiene que interpretar nada**, y por tanto no puede equivocarse de buena fe.

### 6. Comité de riesgos: composición y cadencia realistas

Un comité que se reúne una vez al año no gobierna nada, y uno que se reúne todas las semanas se vacía de asistentes en dos meses. Lo que funciona en una organización del tamaño de Meridiana:

- **Cuatro personas.** Operaciones de siniestros, tecnología, cumplimiento y una persona de negocio con autoridad para decir que no. Cuatro caben en una sala y deciden; nueve delegan.
- **Trimestral, una hora, con orden del día fijo.** Si hay que decidir algo urgente, no se convoca al comité: decide el rol que tiene esa decisión asignada en la slide 2 y lo lleva al comité siguiente para constancia. Un comité que es cuello de botella operativo se salta.
- **Orden del día siempre igual:** indicadores del trimestre, incidentes y casi-incidentes, cambios sustanciales aprobados o pendientes, sistemas nuevos en el inventario, revisión de un punto abierto del trimestre anterior.

Lo que produce el comité no es una reunión: es **un acta corta con decisiones fechadas y con dueño**. Ese documento es la evidencia principal que va a pedir cualquier auditor, y es también lo que permite reconstruir por qué se hizo algo tres años después. Un acta que dice «se debatió sobre el uso de IA» no vale nada; una que dice «se aprueba subir el umbral a 2.500 € a partir del 1 de julio, condicionado a que la tasa de modificación humana en el tramo se mida mensualmente; responsable: dirección de siniestros» vale por sí sola.

Y una regla que evita el teatro: **si en tres comités seguidos no se ha rechazado ni condicionado nada, el comité no está funcionando como control**. Está sellando.

### 7. ISO/IEC 42001 como marco de gestión

Lo que se puede afirmar de esta norma abriendo una fuente oficial abierta —la ficha de catálogo de IEC/ISO, consultada el 31/08/2026— es su identidad y su alcance declarado, y nada más:

- Su referencia y título oficiales son **ISO/IEC 42001:2023, *Information technology — Artificial intelligence — Management system***, publicada en diciembre de 2023, primera edición, elaborada por el comité técnico ISO/IEC JTC 1/SC 42 «Artificial Intelligence».
- Su alcance, según el resumen de esa ficha, es que «especifica los requisitos y proporciona orientación para establecer, implementar, mantener y mejorar de forma continua un sistema de gestión de la IA en el contexto de una organización», y se dirige a **cualquier organización, con independencia de su tamaño, tipo y naturaleza**, que proporcione o utilice productos o servicios que empleen sistemas de IA.
- El documento **se vende** (225 francos suizos en el catálogo oficial). Su texto no es público.

Eso último no es un detalle administrativo: es la razón por la que la slide se detiene aquí. **Su estructura de cláusulas, sus requisitos concretos y los controles de su anexo no se describen en este material**, y el motivo importa: el texto de la norma es de pago y no se ha adquirido. Describir sus cláusulas de memoria o a partir de resúmenes comerciales es exactamente lo que este curso enseña a no hacer. Si alguien te enseña una lista de los requisitos de la 42001 y no puede decirte de qué página salió, esa lista no vale nada.

Lo que sí se puede explicar sin abrir la norma es **qué clase de cosa es y qué clase de cosa no**:

- Es un marco de **gestión**, no de producto. Te dice que tengas un proceso para identificar riesgos, no cuál debe ser tu umbral de aprobación automática.
- Su valor real, en una organización pequeña, no es el certificado: es que **obliga a escribir el ciclo de mejora**. Objetivos, medición, revisión por la dirección, acciones correctivas. Es exactamente lo que se descuida cuando todo lo lleva una persona competente.
- No sustituye a nada de lo que este curso ha explicado. La clasificación de riesgo (B2), el expediente técnico (B4) y la EIPD (B5) siguen siendo obligaciones propias.

Cuándo tiene sentido certificarse: cuando un cliente o un contrato lo pide, o cuando la organización necesita una disciplina externa para sostener el proceso. Cuándo no: cuando se busca un sello para enseñar. Certificar un sistema de gestión que no se usa cuesta dinero y produce una carpeta.

> Fuentes primarias a abrir: texto oficial de ISO/IEC 42001 (adquirido a través de ISO o de UNE); la propia norma para su estructura de cláusulas y su anexo de controles.

### 8. Relación entre ISO 42001 y las obligaciones del AI Act

Aquí hay una confusión cara y conviene desmontarla con claridad: **certificarse en ISO/IEC 42001 no equivale a cumplir el AI Act**. Son cosas de distinta naturaleza y una certificación voluntaria no otorga por sí sola presunción de conformidad con un reglamento; esa presunción, cuando existe, viene de otro mecanismo, que es el de la slide siguiente. Qué relación reconoce exactamente el reglamento entre normas internacionales, normas armonizadas y presunción de conformidad **no se detalla aquí**: los artículos que la regulan no se pudieron abrir en la verificación, y es en ellos donde hay que leerla.

> **Marcador abierto a 31/08/2026 — motivo:** los artículos 40 y 41 del AI Act, que son los que regulan las normas armonizadas y las especificaciones comunes, no se han podido leer. El texto consolidado de EUR-Lex se trunca hacia el artículo 22. Sí se ha confirmado, abriendo el DOUE, que el reglamento modificador se titula «Reglamento (UE) 2026/1744 del Parlamento Europeo y del Consejo de 8 de julio de 2026 por el que se modifican los Reglamentos (UE) 2024/1689, (UE) 2018/1139 y (UE) 2023/1230 en lo que respecta a la simplificación de la aplicación de normas armonizadas en materia de inteligencia artificial (Ómnibus digital sobre IA)» —es decir, que toca precisamente esta materia—, pero su articulado también se trunca antes de poder transcribir la modificación. Hasta leer ambos, no se escribe aquí ninguna afirmación sobre el mecanismo.

La forma útil de verlo:

- El **AI Act** dice *qué* obligaciones tienes según la clasificación de tu sistema: gestión de riesgos, documentación, registro de eventos, supervisión humana, exactitud, robustez.
- **ISO 42001** te da un *cómo* organizarlo: un sistema de gestión con sus procesos, responsabilidades, mediciones y revisiones.

Se solapan mucho, y ese solapamiento es aprovechable. Si ya tienes un sistema de gestión montado, gran parte del sistema de gestión de la calidad que exige el reglamento para determinados supuestos está cubierto en su forma. Lo que no está cubierto es el contenido específico: la clasificación de tu sistema, tu expediente técnico, tu evaluación de conformidad.

La regla práctica para no duplicar, que es la misma que cerraba el B5: **un hecho, una fuente, dos documentos que la referencian**. Tu procedimiento de gestión de riesgos se escribe una vez y sirve para la cláusula de la norma y para la obligación del reglamento. Lo que no puede pasar es tener dos procedimientos de riesgos distintos porque los pidieron dos auditores diferentes.

> Fuentes primarias a abrir: AI Act, artículos sobre presunción de conformidad, normas armonizadas y sistema de gestión de la calidad; ISO/IEC 42001, cláusulas correspondientes; comunicaciones de la Comisión sobre normalización en IA.

### 9. Normas armonizadas: qué presunción dan y cuál no

Las normas armonizadas son el mecanismo por el que la legislación europea de producto conecta un requisito legal, que es abstracto, con una especificación técnica concreta. La Comisión encarga a los organismos europeos de normalización que desarrollen normas; cuando una se publica en el Diario Oficial como armonizada bajo un reglamento, aplicarla otorga presunción de conformidad respecto de los requisitos que cubre. **El mecanismo exacto, los artículos aplicables del AI Act y qué normas armonizadas hay publicadas en el DOUE no se concretan aquí**: es el apartado que antes queda obsoleto de todo el curso, y el catálogo hay que mirarlo en el Diario Oficial el día que se necesite.

> **Marcador abierto a 31/08/2026 — motivo:** el mismo de la slide 8 para el mecanismo (artículos 40 y 41 inaccesibles por truncamiento de EUR-Lex), y además no se ha consultado el listado de normas armonizadas publicado en el DOUE bajo este reglamento. Aviso al redactor: el catálogo cambia, y una lista copiada aquí caducaría antes que el resto del bloque. Compruébala el día de publicar, no antes.

Los tres malentendidos que hay que evitar, y que no dependen de qué esté publicado hoy:

- **La presunción es parcial.** Cubre los requisitos que la norma aborda, no el reglamento entero. Aplicar una norma sobre gestión de riesgos no te exime de la documentación técnica.
- **La presunción es rebatible.** Presunción significa que se presume, no que se demuestra. Una autoridad puede constatar que tu sistema no cumple pese a haber seguido la norma.
- **Aplicar la norma no es comprarla.** La presunción viene de aplicarla y poder demostrarlo con evidencias, no de tener el PDF.

Y el punto que más afecta a un equipo pequeño: **mientras el catálogo de normas armonizadas no esté completo, la ausencia de norma no suspende la obligación**. Si no hay norma armonizada para un requisito que te aplica, tienes que demostrar el cumplimiento por otros medios: tu propia documentación, tus evals, tu expediente técnico. Esperar a que salga la norma es una estrategia con fecha de caducidad.

> Fuentes primarias a abrir: AI Act, artículos sobre normas armonizadas y presunción de conformidad; Diario Oficial de la Unión Europea, listado de normas armonizadas bajo el reglamento; reglamento europeo de normalización.

### 10. Auditoría interna: alcance y evidencias

Una auditoría interna no es una revisión técnica del sistema: es una comprobación de que **lo que dices que haces es lo que haces**. El auditor no juzga si tu arquitectura es buena. Comprueba si existe el procedimiento, si se ha seguido y si hay rastro de ello.

De ahí que la unidad de trabajo sea la evidencia, y que una evidencia tenga que ser **datable, atribuible y recuperable por alguien que no la produjo**. Lo que vale y lo que no, con ejemplos de Meridiana:

| Afirmación | Evidencia que vale | Evidencia que no |
|---|---|---|
| «Revisamos los cambios de modelo» | Acta del comité con la decisión y la fecha | «Siempre lo hablamos» |
| «Hay supervisión humana» | Métrica mensual de tasa de modificación por tramo | Captura de la pantalla con el botón |
| «Los evals corren antes de desplegar» | Ejecución de CI enlazada al despliegue | El fichero de configuración de CI |
| «El inventario está al día» | Reconciliación trimestral firmada contra facturas | El propio inventario |
| «Formamos al personal» | Lista de asistentes con fecha y contenido | La existencia del curso |

Fíjate en el patrón: **la evidencia buena es un registro de que algo ocurrió; la mala es la prueba de que algo existe**. Que exista un fichero de CI no demuestra que se ejecutara.

El alcance razonable de una auditoría interna anual en Meridiana son tres preguntas, no treinta: ¿el inventario refleja lo que hay?, ¿los cambios sustanciales del año pasaron por su puerta?, ¿los incidentes se trataron según el procedimiento? Con eso ya se descubre casi todo lo que se puede descubrir.

Y el hallazgo más valioso de una auditoría interna nunca es un incumplimiento: es **un procedimiento que nadie sigue porque no se puede seguir**. Eso se arregla cambiando el procedimiento, no regañando a nadie.

### 11. Preparar una auditoría externa

Preparar una auditoría externa no consiste en producir documentos: consiste en poder **encontrarlos**. La diferencia entre una auditoría cómoda y una desastrosa casi nunca está en si la organización cumple; está en si tarda dos minutos o dos días en enseñar cada cosa.

El trabajo previo, en orden:

1. **Un índice de evidencias.** Una tabla con la obligación, dónde está la evidencia y quién la custodia. Una página. Es el documento que más rentabiliza el tiempo invertido en todo este bloque.
2. **Congelar versiones.** El auditor mira una fotografía del sistema en una fecha. Ten claro qué versión de expediente, de política y de inventario corresponde a esa fecha, y no la toques durante la auditoría.
3. **Una persona de contacto y una sola.** Que canaliza preguntas y respuestas. Sin esto, tres personas dan tres versiones y las tres son ciertas.
4. **Ensayar tres preguntas.** Las de la slide 12. En voz alta, con el documento delante.
5. **Preparar la lista de lo que no tienes.** Y esta es la que nadie hace.

El punto 5 merece explicación, porque es contraintuitivo. Un auditor va a encontrar los huecos: es su trabajo y se le da bien. La diferencia entre un hallazgo menor y uno grave suele ser si **ya lo tenías identificado y con un plan**. «No tenemos aún medida la tasa de modificación humana en el tramo bajo; está planificado para el segundo trimestre, con este responsable» es una organización que se conoce. «Eso lo tenemos cubierto» seguido de un silencio incómodo es otra cosa.

Los marcadores de verificación pendiente que este curso te ha hecho dejar por escrito son exactamente esa lista. No son una deuda: son el inventario honesto de lo que falta.

### 12. Las preguntas que hace un auditor y las respuestas que no valen

Un auditor con experiencia hace pocas preguntas y todas son de la misma familia: pide que le enseñes algo. Estas son las que caen y lo que no sirve como respuesta.

**«Enséñame el inventario y elige tú un sistema: quiero su expediente.»** No sirve enseñar el expediente del sistema que tienes preparado. La prueba es que puedas hacerlo con cualquiera de la lista.

**«¿Quién aprobó el último cambio de modelo y cuándo?»** No sirve «lo decidió el equipo». Un nombre y una fecha, o no hubo aprobación.

**«¿Cómo sabes que la supervisión humana funciona?»** No sirve describir la pantalla. La respuesta es un número medido y su serie temporal.

**«¿Qué pasó con este incidente de marzo?»** No sirve «se resolvió». Registro de incidente, análisis, acción correctiva, comprobación de que la acción se implantó.

**«¿Qué harías si el proveedor del modelo cambia sus condiciones mañana?»** No sirve «lo evaluaríamos». Un procedimiento y un responsable, o al menos la constancia de que os habéis hecho la pregunta.

**«¿Quién más sabe hacer esto además de ti?»** La pregunta que hunde a las organizaciones dependientes de una persona. No sirve nombrar a alguien que en realidad no lo ha hecho nunca.

El patrón: **el auditor no pregunta si cumples, pregunta si puedes demostrarlo**. Y hay una respuesta que sí vale y mucha gente teme dar: «no lo tenemos, lo hemos identificado, este es el plan y este el responsable». Es una respuesta profesional. Improvisar una que suene bien es lo que convierte un hallazgo menor en un problema de credibilidad sobre todo lo demás que has dicho.

### 13. Formación del personal: obligación y contenido

El artículo aplicable es el **artículo 4 del AI Act, «Alfabetización en materia de IA»**, y sigue vigente sin modificación en el texto consolidado tras el Reglamento (UE) 2026/1744 (verificado 31/08/2026). Su apartado 1 dice literalmente:

> «Los proveedores y responsables del despliegue de sistemas de IA adoptarán medidas para apoyar la promoción de la alfabetización en materia de IA de su personal y demás personas que se encarguen en su nombre del funcionamiento y la utilización de sistemas de IA, teniendo en cuenta sus conocimientos técnicos, su experiencia, su educación y su formación, así como el contexto previsto de uso de los sistemas de IA y las personas o los colectivos de personas en que se van a utilizar dichos sistemas. Esta obligación no exige que los proveedores o los responsables del despliegue garanticen un nivel específico de alfabetización en materia de IA de ninguna persona en particular.»

Tres cosas de ese texto que conviene no perder:

- **A quién obliga:** a proveedores **y** a responsables del despliegue. Meridiana es responsable del despliegue: la obligación es suya, no solo de quien le vende el modelo.
- **Sobre quién:** su personal y «demás personas que se encarguen en su nombre» del funcionamiento y la utilización. Los 24 tramitadores entran de lleno; también quien opere el sistema desde fuera de la plantilla.
- **Hasta dónde llega:** la última frase es una limitación explícita y muy poco citada. El artículo **no** exige garantizar un nivel concreto en ninguna persona. Exige adoptar medidas, y adoptarlas calibradas al contexto de uso. Quien te venda una certificación individual obligatoria para cada tramitador está vendiendo algo que este artículo no pide.

El apartado 2 encarga a la Comisión y a los Estados miembros apoyar ese esfuerzo, en particular en las pymes, publicando ejemplos prácticos de cumplimiento; el apartado 3 encarga al Consejo de IA adoptar recomendaciones al respecto, teniendo en cuenta los marcos europeos de competencias.

Lo que aporta esta slide es **qué formación sirve**, que es donde casi todo el mundo falla aunque cumpla la obligación formal. Un curso genérico de dos horas sobre qué es la inteligencia artificial no cambia ninguna conducta. Lo que cambia conductas es formación específica por rol:

- **Tramitadores.** Qué hace exactamente el agente con su expediente, qué no decide, cómo se ve que una extracción puede estar mal, y —lo más importante— **cómo discrepar y qué pasa cuando discrepan**. Si la formación no incluye que discrepar es lo esperado y no penaliza, la supervisión humana del B5 seguirá siendo aparente por mucho que se forme a nadie.
- **Ingeniería.** Qué cambios disparan revisión (slide 5), qué datos no pueden entrar en un prompt (B5), cómo se declara un sistema nuevo.
- **Dirección.** Qué decisiones les corresponden y qué firman cuando firman.
- **Atención al cliente.** Qué contestar cuando un asegurado pregunta si le ha atendido una máquina. Esta es la que nunca se prepara y la que más se usa.

El contenido más valioso no es normativo: son **los tres casos en que el sistema falló** y qué se hizo. Un caso real enseña más que un módulo entero, y además demuestra que la organización mira sus propios fallos.

Registro: lista de asistentes, fecha, contenido y versión del material. Es la evidencia de la slide 10, y sin ella la formación, a efectos de auditoría, no ocurrió.

> Fuentes primarias a abrir: AI Act, artículo sobre alfabetización en materia de IA y su ámbito de aplicación; orientaciones publicadas por la Comisión o por la autoridad nacional sobre su alcance.

### 14. Gestión de proveedores de IA

En el B3 vimos el reparto de roles y en el B5 el contrato de encargo. Aquí toca la parte aburrida y continua: **qué hace la organización con sus proveedores de IA una vez firmado**, mes a mes.

Un proveedor de modelo no se parece a un proveedor de software clásico en una cosa decisiva: **cambia el producto por debajo sin que tú despliegues nada**. Una versión nueva, un ajuste del modelo, un cambio en las condiciones o en la lista de subencargados. Ninguna de esas cosas aparece en tu control de cambios, y todas modifican tu sistema.

Lo que compone una gestión de proveedores que funciona:

- **Una ficha por proveedor.** Qué servicio, qué datos recibe, con qué contrato, quién es el responsable interno, cuándo se revisó por última vez.
- **Vigilancia automática de lo que cambia sin avisarte.** Un trabajo programado que compare semanalmente la versión de los términos, la lista de subencargados y las versiones de modelo anunciadas como obsoletas, y avise. Es poco trabajo y sustituye a una vigilancia manual que nunca se hace.
- **Fijar versiones de modelo.** Si tu sistema apunta a un alias que se actualiza solo, tu comportamiento cambia sin control de cambios. Fijar versión convierte una sorpresa en una decisión programada.
- **Revisión anual con el cuestionario del B5.**
- **Plan de salida escrito antes de necesitarlo.** Qué modelo alternativo, cuánto costaría migrar los prompts, qué evals hay que volver a pasar. Un plan de salida sin una prueba real es una intención: al menos una vez al año, ejecutar los evals contra el proveedor alternativo.

La señal de alarma que hay que saber leer: si nadie sabe decir qué versión exacta de modelo está en producción hoy, no hay gestión de proveedor. Hay una suscripción.

### 15. Incidentes graves: qué se notifica, a quién y en qué plazo

El AI Act establece obligaciones de notificación de incidentes graves para determinados sistemas, con una definición de qué constituye incidente grave, un destinatario y unos plazos. **La definición exacta, el destinatario, los plazos, el contenido de la notificación y el alcance de la obligación no se reproducen aquí**: el artículo que los fija no se pudo abrir en la verificación, y ninguno de esos datos se escribe de memoria. Y ojo con la coincidencia: una misma situación puede activar a la vez la notificación del reglamento de IA y la de brecha de datos personales del B5, con destinatarios y plazos distintos.

> **Marcador abierto a 31/08/2026 — motivo:** el artículo 73 del AI Act no se ha podido leer. El texto consolidado de EUR-Lex se trunca hacia el artículo 22 y no se ha encontrado otra vía para servirlo completo. No se escribe aquí ningún plazo. Del lado del RGPD sí está cerrado el plazo paralelo, y sirve para dimensionar el problema del solapamiento: 72 horas desde que se tuvo constancia, artículo 33.1 (ver B5, slide 18).

Lo que este bloque sí puede resolver es el problema que hace fracasar cualquier plazo, sea el que sea: **el reloj empieza a contar cuando la organización tiene conocimiento, y casi nadie ha decidido quién tiene autoridad para reconocer que hay un incidente.**

El caso de Meridiana: se descubre que durante seis semanas la extracción marcó «sin lesiones» en relatos donde el asegurado mencionaba dolor cervical sin usar esa palabra. Cuarenta expedientes siguieron la vía automática y no se derivaron.

Las preguntas que hay que tener respondidas **antes**, porque el día que pasa no hay tiempo:

1. ¿Quién puede declarar que esto es un incidente grave? Un nombre.
2. ¿Cuándo se considera que la organización «tuvo conocimiento»? ¿Cuando lo vio el ingeniero de guardia o cuando llegó a dirección? Escríbelo.
3. ¿Quién reúne la información técnica? Aquí las trazas (B2) y los evals (B3) del curso 3 son la diferencia entre acotar el alcance en dos horas o en dos semanas.
4. ¿Quién redacta y quién firma la notificación?
5. ¿Qué se les cuenta a los cuarenta afectados y quién decide eso?

Un procedimiento de incidentes que no se ha ensayado nunca no es un procedimiento. Es un documento.

> Fuentes primarias a abrir: AI Act, artículos sobre notificación de incidentes graves y su definición; orientaciones y formularios de la autoridad nacional competente; RGPD, artículos sobre notificación de violaciones de seguridad, para el solapamiento.

### 16. Vigilancia poscomercialización

El AI Act exige, para determinados sistemas, vigilancia poscomercialización: recoger y analizar de forma activa y sistemática información sobre cómo funciona el sistema una vez en uso. El artículo aplicable, el alcance y el contenido exigido al plan **no se concretan aquí**: son datos que hay que leer en el articulado, que no se pudo abrir en la verificación.

> **Marcador abierto a 31/08/2026 — motivo:** el artículo 72 del AI Act no se ha podido leer, por el mismo truncamiento de EUR-Lex. Lo único verificado del reglamento que toca esta slide es el artículo 12, cuyo apartado 2 sitúa la vigilancia poscomercialización entre las finalidades a las que deben servir las capacidades de registro automático de acontecimientos (ver B5, slide 23). Eso confirma que las dos cosas están conectadas en el texto; no dice qué debe contener el plan.

La buena noticia para quien viene del curso 3: **la mayor parte de la vigilancia poscomercialización ya está construida y se llama observabilidad**. Lo que falta no es instrumentación, son tres cosas que la convierten en proceso de gobernanza:

- **Que alguien mire.** Un panel que nadie abre no es vigilancia. Una revisión mensual de media hora con guion fijo sí lo es.
- **Que haya umbrales decididos de antemano.** «La derivación por lesiones baja del 2,1 % al 1,3 %» solo es alarma si alguien escribió antes cuánta variación es tolerable. Decidir el umbral después de ver el número es decidir que no pasa nada.
- **Que quede constancia, incluso cuando no hay nada.** «Revisado, sin desviaciones», con fecha y firma. La ausencia de registro es indistinguible de la ausencia de revisión.

Lo que Meridiana vigila sale entero de lo ya construido:

| Señal | De dónde sale | Qué indicaría |
|---|---|---|
| Derivación por lesiones | Trazas (B2) | Deriva del extractor |
| Modificación humana por tramo | Trazas | Supervisión volviéndose aparente |
| Evals en cada despliegue | Evals (B3) | Regresión de calidad |
| Reclamaciones de asegurados | Negocio | Efecto real sobre personas |
| Derivaciones que el humano revierte | Trazas | Derivación innecesaria |

La quinta es la que casi nadie mide y la que más dice: un sistema que deriva de más también falla, solo que su fallo lo pagan los tramitadores y no llega a ninguna queja.

> Fuentes primarias a abrir: AI Act, artículos sobre vigilancia poscomercialización y plan de vigilancia; actos de ejecución o plantillas publicados sobre el plan.

### 17. La gobernanza de Meridiana en una página

Todo lo anterior, para una compañía de 24 tramitadores y 32.000 siniestros al año, cabe en una página. Si no cabe, se está copiando el modelo de una organización que no es la tuya.

**Quién.** Cuatro roles nombrados: dirección de siniestros, dirección técnica, cumplimiento y una persona de negocio. Ninguno dedicado a tiempo completo a esto.

**Qué se decide y quién.** La tabla de la slide 2, con el umbral de aprobación no delegable y la retirada del sistema en manos de quien está de guardia.

**Cuándo se reúnen.** Comité trimestral, una hora, orden del día fijo. Acta con decisiones fechadas.

**Qué documentos vivos hay.** Cinco, y ninguno más:

| Documento | Dueño | Se revisa |
|---|---|---|
| Inventario de sistemas | Cumplimiento | Trimestral, contra facturas |
| Expediente técnico (B4) | Dirección técnica | Con cada cambio sustancial y una vez al año |
| EIPD y registro de tratamientos (B5) | Cumplimiento | Con cada cambio de datos |
| Política de IA | Cumplimiento | Anual |
| Registro de decisiones e incidentes | Cumplimiento | Continuo |

**Qué se mide.** Cinco indicadores de la slide 16, revisados mensualmente en media hora.

**Qué pasa cuando algo va mal.** Procedimiento de incidentes con una persona autorizada a declararlo y un ensayo anual.

Eso es todo. Cabe en una página, se puede explicar a un auditor en veinte minutos y sobrevive a que se marche cualquiera de los cuatro, porque cada casilla tiene un nombre y cada documento tiene un dueño distinto de quien lo escribió.

Lo que **no** hay: ningún comité de nueve personas, ninguna herramienta de gobernanza comprada, ningún consultor permanente.

### 18. Ejercicio: política de IA mínima viable

Escribe la política de IA de Meridiana en **dos páginas**, con los seis apartados de la slide 3. El límite de dos páginas no es un capricho: es la restricción que fuerza a decidir qué importa.

**Cómo trabajarlo:**

1. Empieza por el apartado 4 —qué se puede hacer sin pedir permiso—, no por el 1. Escribir primero lo permitido obliga a que la política sea utilizable, y evita el documento que prohíbe todo y no lo cumple nadie.
2. Escribe el apartado 2 con **ejemplos reales de Meridiana**, no con categorías. «Pegar el texto de un FNOL en un asistente público para resumirlo» en vez de «uso de herramientas no autorizadas».
3. Nombra roles, no departamentos, en el apartado 5.
4. En el apartado 6, fija fecha de revisión y responsable. Una política sin fecha de caducidad es una política que ya caducó.

**Prueba de validación, que es la parte que de verdad enseña:** dásela a leer a alguien que no la haya escrito y hazle tres preguntas concretas.

- «Quiero usar un asistente para redactar un correo a un asegurado con su nombre dentro. ¿Puedo?»
- «He montado un cuaderno que clasifica reclamaciones y lo uso los lunes. ¿Tengo que declararlo?»
- «El proveedor ha sacado una versión nueva del modelo. ¿Puedo actualizar?»

Si tiene que preguntarte a ti alguna de las tres, ese apartado está mal escrito. Reescríbelo y vuelve a probar. Una política se valida como se valida una interfaz: con un usuario delante, no con una revisión.

### 19. Gobernanza proporcionada: una persona no monta un comité de nueve

La mayor parte del material de gobernanza que circula está escrito para organizaciones de miles de personas con departamento de riesgos. Aplicarlo tal cual en una compañía de tamaño medio produce un fracaso predecible: se monta la estructura, se hacen dos reuniones, se abandona, y queda un rastro documental que demuestra que existía un proceso que no se siguió. **Eso es peor que no haberlo montado**: documenta el incumplimiento.

Proporcionar bien significa reducir la estructura sin reducir las cuatro funciones de la slide 1. Cómo se ve eso a distintas escalas:

- **Una persona.** No hay comité: hay una decisión escrita antes de cada cambio relevante y un registro en un fichero versionado. La función de «segunda opinión» se cubre con una revisión externa anual, no con una reunión imposible.
- **Un equipo pequeño (Meridiana).** Cuatro personas, trimestral. Cumplimiento no es un departamento: es una parte del trabajo de alguien.
- **Una organización grande.** Ahí sí caben comités, funciones separadas y auditoría interna independiente.

Lo que **nunca** se recorta, sea cual sea el tamaño:

- El inventario. Sin él no hay nada.
- El registro de decisiones. Es lo único que sobrevive a las personas.
- El procedimiento de incidentes. Es lo único que tiene que funcionar el peor día.

Y lo que sí se recorta sin remordimiento: la cadencia de las reuniones, el número de participantes, el formato de los documentos, la separación de funciones. Una gobernanza de tres folios que se usa vale más que una de cuarenta que se cita.

### 20. Indicadores de gobernanza que se pueden medir de verdad

Casi todos los cuadros de mando de gobernanza miden actividad: cuántas reuniones, cuántas personas formadas, cuántas políticas publicadas. Son fáciles de subir y no dicen nada sobre si la gobernanza funciona.

Un indicador sirve si **puede dar un resultado malo**. Si solo puede salir bien, es decoración.

Los que sí miden algo, con lo que revelan:

| Indicador | Qué revela | Señal mala |
|---|---|---|
| Sistemas encontrados fuera del inventario por trimestre | Si el inventario está vivo | Cero durante un año: no se está buscando |
| Cambios sustanciales que pasaron por su puerta / total | Si el control se aplica o se rodea | Por debajo del 100 % |
| Días desde la última revisión del expediente técnico | Si el documento está vivo | Creciendo |
| Tasa de modificación humana por tramo de importe | Si la supervisión es real (B5) | Tendiendo a cero |
| Tiempo desde la detección de un fallo hasta su registro formal | Si el procedimiento de incidentes se usa | Semanas |
| Decisiones del comité rechazadas o condicionadas | Si el comité controla o sella | Cero sostenido |
| Personas capaces de ejecutar un despliegue de emergencia | Dependencia de una sola persona | Uno |

Los dos últimos son los que ninguna plantilla incluye y los que mejor predicen problemas.

Un aviso sobre el primero: **encontrar sistemas fuera del inventario es un buen resultado, no uno malo**, y hay que decirlo en voz alta en el comité. Si descubrir un sistema no declarado penaliza a alguien, el trimestre siguiente el indicador saldrá en cero y no será porque no los haya. Los indicadores de gobernanza cambian la conducta que miden, y por eso hay que elegir con cuidado cuáles se premian.

### 21. Aprobar un caso de uso nuevo: el guion de la decisión

Llega una propuesta: usar el mismo modelo para redactar las cartas de rechazo de siniestros. Alguien tiene que decidir, y la calidad de la decisión depende de tener un guion, no un debate abierto.

El guion, en seis preguntas y en este orden:

1. **¿Qué decide el sistema y qué decide una persona?** Si la respuesta no cabe en una tabla como la del curso 3, la propuesta no está madura.
2. **¿Qué efecto tiene sobre la persona afectada?** Una carta de rechazo no es una redacción: es la comunicación de una denegación. El efecto es alto aunque el trabajo técnico sea trivial.
3. **¿Qué datos personales entran y cuáles salen del contexto?** (B5.)
4. **¿Qué clasificación de riesgo le corresponde y cambia la del sistema existente?** (B2.) Esta es la que se olvida: un caso de uso nuevo sobre un sistema ya clasificado puede reclasificarlo.
5. **¿Cómo sabríamos que va mal?** Qué se mide, con qué umbral, quién lo mira. Si no hay respuesta, no hay puesta en producción.
6. **¿Cómo lo apagamos?** Qué pasa con los expedientes en curso si se retira mañana.

La decisión se escribe con **cuatro elementos y no más**: qué se aprueba, con qué condiciones, quién es el responsable y cuándo se revisa. Media página.

Y una salida que casi nunca se usa y debería: **aprobar con alcance limitado**. Un trimestre, un tramo de importes, revisión obligatoria al final. No es un no y no es un sí en blanco. Es la forma de que las propuestas razonables avancen sin que la organización se comprometa con algo que todavía no puede evaluar. Un comité que solo sabe aprobar o rechazar empuja a la gente a no preguntar.

### 22. Retirar un sistema: el proceso que nadie escribe

Todo el mundo escribe cómo poner un sistema en producción. Casi nadie escribe cómo sacarlo, y luego se retira mal: se apaga un servicio un viernes y quedan cabos sueltos durante meses.

Retirar el agente de Meridiana implica, como mínimo:

1. **Los expedientes en curso.** Hay 88 al día en el flujo. ¿Los termina el sistema antes de apagarse o pasan todos a la cola humana? Decidido antes, no durante.
2. **La capacidad humana.** El equipo se dimensionó contando con el agente. Apagarlo devuelve una carga de trabajo que quizá ya no cabe.
3. **Los registros y trazas.** No se borran con el sistema: tienen sus propios plazos (B5) y siguen haciendo falta para responder reclamaciones de expedientes que el sistema tramitó. Hay que decidir dónde viven cuando la aplicación ya no exista.
4. **El expediente técnico y el inventario.** El sistema pasa a estado «retirado», con fecha. No se borra la ficha: se cierra. Un inventario sin historia no sirve para responder qué había en marzo del año pasado.
5. **Los interesados y terceros.** Si se informó de que un sistema automatizado intervenía, hay que revisar esa información. Y avisar al proveedor para terminar el contrato y solicitar el borrado (B5).
6. **El conocimiento.** Qué se aprendió y por qué se retira. Un documento de una página que evita que dentro de dos años alguien vuelva a proponer exactamente lo mismo sin saber cómo acabó.

Los dos motivos habituales de retirada son opuestos y conviene distinguirlos en el documento: **se retira porque falló** o **se retira porque ya no hace falta**. El primero exige análisis de causa; el segundo, solo orden. Confundirlos hace que una retirada tranquila se viva como un fracaso, o que un fracaso se archive como una limpieza.

### 23. Relación con auditoría interna y con cumplimiento

En una organización con funciones separadas hay tres actores y se pisan constantemente si nadie escribe quién hace qué. La forma clásica de ordenarlo distingue quién ejecuta, quién controla y quién verifica de forma independiente:

- **Quien construye y opera** el sistema: ingeniería y operaciones. Son los dueños del riesgo. No es «el equipo que se equivoca»: es el equipo que decide y responde.
- **Quien define el marco y vigila**: cumplimiento. Escribe la política, mantiene el inventario, lleva el comité. Vigila, no construye.
- **Quien verifica de forma independiente**: auditoría interna. No participa en el diseño, para poder auditar sin auditarse.

En Meridiana, esa separación existe sobre el papel y se rompe en un punto concreto y muy común: **cumplimiento no entiende el sistema lo bastante para vigilarlo, y quien lo entiende es quien lo construyó**. El resultado práctico es que el ingeniero acaba redactando la documentación de cumplimiento que después cumplimiento aprueba sin poder evaluarla.

Eso no se arregla contratando a nadie. Se arregla con dos cosas baratas:

- **Que la evidencia sea automática y no interpretada.** Si la tasa de modificación humana la genera un informe del sistema y no un cálculo del ingeniero, cumplimiento puede vigilarla sin entender el código.
- **Que cumplimiento pueda hacer preguntas tontas sin coste social.** «¿Por qué el umbral está en 1.500?» es una gran pregunta, y en muchas organizaciones no se hace porque quien la hace queda mal.

Auditoría interna, cuando existe, aporta lo que ninguna de las dos puede: **preguntar a los tramitadores**. La verdad sobre si la supervisión humana es real no está en el código ni en la política. Está en lo que contesta quien pulsa el botón cuando le preguntan si alguna vez ha discrepado y qué pasó.

### 24. Presupuesto de cumplimiento: qué cuesta esto al año

La pregunta llega siempre y merece una respuesta seria. No se responde con un importe —las tarifas dependen del mercado y de cada organización— sino con **una descomposición en jornadas** que cualquiera puede valorar con sus propios costes.

Para un sistema como el de Meridiana, ya en producción, el esfuerzo anual recurrente se reparte así:

| Partida | Esfuerzo anual estimado | Quién |
|---|---|---|
| Comité trimestral (preparación incluida) | 4 × 4 jornadas-persona | Cuatro roles |
| Mantenimiento del inventario y reconciliación | 4 × 0,5 jornadas | Cumplimiento |
| Revisión del expediente técnico (B4) | 3–5 jornadas | Técnica |
| Revisión mensual de indicadores | 12 × 0,5 jornadas | Técnica y operaciones |
| Formación por roles | 2–3 jornadas de preparación + asistencia | Cumplimiento |
| Auditoría interna anual | 5–8 jornadas | Auditoría o externo |
| Ensayo del procedimiento de incidentes | 1 jornada | Todos |

Falta sumar el coste puntual de la puesta en marcha —clasificación, expediente, EIPD—, mucho mayor pero que se paga una vez, y los costes externos de certificación o auditoría de tercera parte, que hay que presupuestar con proveedores reales en vez de estimar.

Dos avisos para presentar esto a dirección:

- **La mayor parte no es dinero nuevo: es tiempo de gente que ya está.** Y ese tiempo compite con entregar funcionalidad. Si nadie lo protege en la planificación, no ocurre.
- **La comparación honesta no es contra cero**, sino contra el coste de un incumplimiento: sanciones cuyos tramos fija el reglamento (los importes y su base de cálculo no se reproducen aquí: hay que leerlos en el articulado), interrupción del servicio y coste reputacional. Basta con hacerla explícita y dejar que la dirección decida con los números delante.

> **Marcador abierto a 31/08/2026 — motivo:** el artículo 99 del AI Act, que es el de las sanciones, no se ha podido leer: el texto consolidado de EUR-Lex se trunca hacia el artículo 22. No se escribe ninguna cifra ni ningún porcentaje. Y este es justo el dato que más circula de memoria y peor envejece: si lo necesitas para una diapositiva de dirección, ábrelo tú y anota la fecha.

> Fuentes primarias a abrir: AI Act, artículos sobre sanciones y su cuantía; criterios de graduación de la autoridad nacional; presupuestos reales de auditoría y certificación solicitados a proveedores.

### 25. El error de externalizar la gobernanza entera

Es tentador y hay mercado para ello: contratar una consultora que entregue la política, el inventario, el expediente técnico y el procedimiento de incidentes. Sale un paquete impecable, con buena redacción y todas las referencias correctas. Y falla, de una forma muy concreta.

Falla porque **la gobernanza no es un conjunto de documentos: es un conjunto de conductas**, y las conductas no se subcontratan. Los síntomas aparecen en el mismo orden siempre:

- Los documentos describen una organización que no es la tuya. El procedimiento tiene siete roles y en Meridiana hay cuatro personas.
- Nadie sabe por qué las cosas están como están. Cuando el auditor pregunta por qué el umbral de revisión es ese, la respuesta es «lo puso la consultora».
- La primera decisión difícil real no encaja en ningún procedimiento, porque los procedimientos se escribieron sin conocer el sistema.
- A los dieciocho meses todo está desactualizado, porque mantenerlo exige entender por qué se escribió.

Qué sí tiene sentido externalizar, y es mucho:

- **Verificación normativa.** Comprobar artículos, plazos y vigencias: exactamente lo que este curso te ha hecho marcar como pendiente de verificar. Es trabajo especializado y no tiene sentido hacerlo a medias.
- **Auditoría independiente.** Por definición, es mejor de fuera.
- **Formación específica.**
- **Revisión crítica de lo que tú has escrito.** Que es lo contrario de que te lo escriban.

La línea es nítida: **externaliza el conocimiento normativo, nunca el conocimiento del sistema**. Un consultor puede decirte qué exige el reglamento. No puede decirte qué hace tu agente cuando el proveedor devuelve un error, ni por qué el umbral está donde está, ni quién debería poder cambiarlo.

### 26. Madurez: de la hoja de cálculo al sistema de gestión

No hace falta llegar al final. Hace falta saber en qué escalón estás y cuál es el siguiente, porque el error caro es saltarse escalones comprando herramientas para un nivel de madurez que la organización no tiene.

- **Nivel 0 — nada.** Hay sistemas en producción y nadie sabe cuántos. Es el punto de partida honesto de mucha gente.
- **Nivel 1 — la hoja de cálculo.** Existe un inventario, aunque esté incompleto, y alguien lo mantiene. Este salto es el que más valor aporta de todos y cuesta una semana.
- **Nivel 2 — decisiones escritas.** Hay registro de decisiones con fecha y dueño, y una puerta antes de producción. Ya se puede responder a un auditor.
- **Nivel 3 — medición.** Hay indicadores que pueden salir mal, y una revisión periódica que deja constancia. La gobernanza deja de depender de la memoria.
- **Nivel 4 — sistema de gestión.** Ciclo completo de mejora, auditoría interna, revisión por la dirección. Aquí es donde ISO 42001 (slide 7) empieza a tener sentido, y no antes.

Meridiana está entre el 1 y el 2, que es donde está casi todo el mundo que ha hecho algo. Lo importante:

- **Los saltos son baratos hacia abajo y caros hacia arriba.** Del 0 al 1 es una semana. Del 3 al 4, meses.
- **Cada nivel exige que el anterior funcione.** Medir indicadores sobre un inventario incompleto produce números falsos, y un número falso es peor que ninguno porque genera confianza.
- **Comprar herramienta no sube de nivel.** Una plataforma de gobernanza sobre un nivel 1 produce un nivel 1 con licencias.

Elige el escalón siguiente, no el destino.

### 27. Qué hacer el primer trimestre si no hay nada montado

Cierre del curso, y la única slide que hay que poder ejecutar el lunes. Trece semanas, sin presupuesto nuevo, sin contratar a nadie.

**Semanas 1–2. Inventario.** Reúne las facturas de proveedores de IA de los últimos doce meses y pregunta a cada equipo qué tiene en marcha, sin consecuencias por declarar. Una hoja con: sistema, qué hace, quién lo mantiene, qué datos toca, en producción sí o no. Incompleta, pero real.

**Semanas 3–4. Nombres.** La tabla de la slide 2 para el sistema más importante. Enviada por correo y aceptada por escrito, aunque no haya política todavía.

**Semanas 5–7. El sistema que más importa.** Elige uno —el de más efecto sobre personas— y hazle lo del curso: clasificación (B2), roles frente al proveedor (B3), índice de expediente técnico con sus huecos marcados (B4), inventario de datos (B5). No lo termines: identifica los huecos y ponles dueño y fecha.

**Semanas 8–9. Procedimiento de incidentes.** Una página. Quién declara, a quién se avisa, quién reúne la información, quién notifica. Y **ensáyalo** con un caso inventado, en una hora, con la gente real en la sala. El ensayo encontrará más fallos que el documento.

**Semanas 10–11. Política de IA.** Dos páginas, el ejercicio de la slide 18. Validada con tres preguntas reales.

**Semanas 12–13. Primer comité y plan del año.** Con acta. Se aprueba el plan de los tres trimestres siguientes y se fijan los indicadores que se empezarán a medir.

Lo que **no** hay que hacer el primer trimestre: comprar una herramienta, contratar una consultora para que escriba los documentos, o intentar cubrir todos los sistemas a la vez.

Y lo que hay que aceptar: al final del trimestre habrá muchos puntos abiertos pendientes de verificar en fuente primaria. Eso no es fracaso. Es la lista, escrita y con dueño, de lo que falta, y es la diferencia entre una organización que se conoce y una que improvisa.

### 28. Ejercicio práctico 1: la prueba del autobús {ejercicio:B6-ej1}

Coge a la persona que más sabe del sistema —en Meridiana, quien construyó el agente— y **simula que hoy es su último día**. No un simulacro largo: dos horas.

Con esa persona fuera de la sala, el resto del equipo tiene que responder por escrito:

1. ¿Qué versión de modelo hay en producción hoy y quién decidió ponerla?
2. ¿Por qué el umbral de aprobación automática es 1.500 €?
3. ¿Dónde está la regla que deriva por lesiones y cómo se prueba que sigue funcionando?
4. ¿Qué datos personales salen hacia el proveedor en cada llamada?
5. ¿Cómo se apaga el sistema y qué pasa con los expedientes en curso?
6. ¿Quién más ha desplegado alguna vez?

**Entregable:** las seis respuestas y, al lado, dónde estaba escrita cada una. La columna «dónde estaba escrita» es el ejercicio; las respuestas son solo el pretexto.

**Criterio de aceptación:** cada pregunta que solo se pudo responder preguntando a la persona ausente, o mirando el historial de commits, es un hallazgo de gobernanza con dueño y fecha. Si las seis se responden desde documentos, tienes gobernanza. Si ninguna, tienes un experto.

### 29. Ejercicio práctico 2: el índice de evidencias de una hora {ejercicio:B6-ej2}

Monta el índice de evidencias de la slide 11 para el sistema de Meridiana, y valídalo con un cronómetro.

**Paso 1.** Una tabla con cuatro columnas: obligación o afirmación, dónde está la evidencia, quién la custodia, fecha de la última actualización. Diez filas como máximo, escogiendo las diez cosas que un auditor pediría primero.

**Paso 2.** Dale la tabla a alguien del equipo que no la haya hecho, y pídele que localice cinco evidencias al azar. Cronometra cada una.

**Criterio de aceptación:** las cinco se encuentran en menos de cinco minutos cada una, sin preguntar a nadie. Cualquiera que tarde más, o que acabe en «se lo pido a fulano», es una fila que no está bien resuelta: la evidencia existe pero no es recuperable, que a efectos de auditoría es casi lo mismo que no tenerla.

**Variante que merece la pena:** repite el ejercicio tres meses después, sin tocar la tabla. Lo que ha dejado de encontrarse te dice qué documentos están muertos, y esa es la información que ninguna revisión programada te da.

### 30. Mini-quiz de comprensión — B6 {quiz:B6}

Tres preguntas sobre el razonamiento de este bloque: qué aporta y qué no una norma armonizada, qué convierte un inventario en algo útil y qué hace que una organización pueda responder a tiempo ante un incidente grave.

**Ninguna depende de recordar un plazo, un importe ni un número de artículo**, y es deliberado. Esos datos se verifican en fuente primaria cada vez que se usan; memorizarlos de un curso es exactamente el hábito que este bloque intenta desmontar. Lo evaluable es otra cosa: distinguir una presunción parcial de un cumplimiento acreditado, saber qué campos hacen que un inventario sirva para decidir, y reconocer que un procedimiento de incidentes sin persona autorizada, sin criterio de conocimiento y sin trazas es un documento y no un procedimiento.

Con esto se cierra el curso. Aprobado con dos aciertos, y puedes repetirlo las veces que quieras.

## Qué te llevas

- Sin inventario de sistemas no hay gobernanza posible.
- ISO 42001 organiza la gestión; no sustituye a las obligaciones del reglamento.
- La vigilancia poscomercialización es una obligación continua, no un trámite.

## Mini-quiz (3 preguntas)

Una sola opción correcta por pregunta, con explicación. Se generan con el mismo formato que `assets/js/quizzes-data.js` para que el importador las recoja.

1. **Tema:** Qué presunción aporta una norma armonizada
   - **Enunciado:** El equipo propone aplicar una norma armonizada publicada bajo el reglamento y dar por cerrado el cumplimiento. ¿Cuál es la lectura correcta?
   - **Opciones:**
     - a) Aplicarla acredita el cumplimiento del reglamento completo y cierra el asunto.
     - b) **Aporta presunción de conformidad solo respecto de los requisitos que la norma cubre, y es rebatible: hay que poder demostrar que se aplica.** ✅
     - c) No sirve de nada mientras no exista una certificación de tercera parte que lo acredite.
     - d) Basta con haber adquirido la norma y tenerla archivada.
   - **Explicación:** La presunción es parcial —alcanza a los requisitos abordados por esa norma— y rebatible: una autoridad puede constatar el incumplimiento pese a su aplicación. La (a) confunde una parte con el todo. La (c) niega el mecanismo, que opera por aplicación de la norma y no por certificación obligatoria. La (d) confunde comprar el documento con aplicarlo y evidenciarlo.

2. **Tema:** Qué debe contener el inventario de sistemas de IA
   - **Enunciado:** Meridiana tiene un inventario con el nombre de cada sistema y el equipo que lo mantiene, revisado una vez al año. ¿Cuál es la mejora que más aumenta su utilidad?
   - **Opciones:**
     - a) Añadir el logotipo del proveedor y el nombre comercial del modelo a cada ficha.
     - b) Pasarlo de una hoja de cálculo a una herramienta de gobernanza con flujos de aprobación.
     - c) **Añadir a cada ficha la finalidad, los datos que trata, su clasificación de riesgo y quién decide sobre él, y reconciliarlo trimestralmente contra el gasto en proveedores.** ✅
     - d) Ampliar la revisión anual a dos revisiones anuales.
   - **Explicación:** Un inventario sirve para decidir, y para eso necesita finalidad, datos, clasificación y dueño; la reconciliación contra el gasto es lo que encuentra los sistemas que nadie declaró, que es el fallo estructural de todo inventario. La (a) añade datos que no cambian ninguna decisión. La (b) compra herramienta sobre un proceso que aún no funciona. La (d) mantiene un ciclo anual frente a un sistema que cambia con cada despliegue: duplicar la frecuencia no cierra esa brecha.

3. **Tema:** Qué permite responder a tiempo ante un incidente grave
   - **Enunciado:** Se descubre que durante seis semanas la extracción no marcó lesiones en cuarenta expedientes que debían haberse derivado. ¿Qué es lo que determina que la organización pueda cumplir sus obligaciones de notificación?
   - **Opciones:**
     - a) Que exista un documento de procedimiento de incidentes aprobado por dirección.
     - b) **Que haya una persona con autoridad para declarar el incidente, un criterio escrito de cuándo se considera que hubo conocimiento, y trazas que permitan acotar el alcance.** ✅
     - c) Que el proveedor del modelo asuma contractualmente la responsabilidad del fallo.
     - d) Que el fallo se corrija en producción antes de notificar nada.
   - **Explicación:** El reloj de cualquier notificación corre desde el conocimiento, así que lo decisivo es quién puede reconocer el incidente, cuándo se entiende que la organización lo supo y con qué evidencias se acota a quién afectó. La (a) es necesaria pero insuficiente: un procedimiento no ensayado no produce ninguna de las tres cosas. La (c) reparte responsabilidad económica, no obligaciones de notificación. La (d) invierte el orden y consume el tiempo disponible.

## Lab

Montar la gobernanza mínima de Meridiana en una página y someterla a la prueba del autobús.

**Enunciado.** Tienes que producir el documento de la slide 17 —la gobernanza de Meridiana en una página— y demostrar que funciona sin la persona que construyó el sistema. La plantilla está en `content/caso/plantillas/B6-gobernanza-una-pagina.docx`, con el índice de evidencias en `content/caso/plantillas/B6-indice-evidencias.xlsx`.

**Pasos:**

1. Rellena la página de gobernanza: quién, qué se decide y quién decide, cadencia, documentos vivos con dueño, indicadores y procedimiento de incidentes. Cabe en una página; si no cabe, sobra estructura.
2. Marca explícitamente las dos decisiones que **no** se delegan y la que se toma **sin** comité. Justifica las tres en una línea cada una.
3. Completa el índice de evidencias con diez filas y valídalo cronometrando cinco búsquedas hechas por otra persona (ejercicio 2).
4. Ejecuta la prueba del autobús con las seis preguntas del ejercicio 1 y anota los hallazgos.
5. Convierte cada hallazgo en una fila con dueño y fecha. Donde el hallazgo dependa de un dato normativo sin verificar, márcalo como pendiente y anota la fuente primaria que abrirías.

**Criterios de aceptación:**

- La página de gobernanza asigna cada decisión a una **persona o rol nombrado**, nunca a un departamento, y cada documento vivo tiene un dueño distinto de quien lo redactó.
- La autoridad para retirar el sistema está por debajo, en la jerarquía de aprobación, de la autoridad para ponerlo en producción.
- Al menos cinco de las seis preguntas de la prueba del autobús se responden desde un documento, sin recurrir al historial de commits.
- Al menos cuatro de las cinco evidencias cronometradas se localizan en menos de cinco minutos por alguien que no montó la tabla.
- La lista de hallazgos incluye al menos un pendiente con su fuente primaria: una gobernanza que no reconoce ningún hueco no se ha mirado de verdad.
- Reproducible en menos de 120 minutos, con tres personas y sin presupuesto.

**Solución de referencia:** en `content/caso/soluciones/B6/`, con la página de gobernanza de Meridiana, el índice de evidencias completo, el resultado de la prueba del autobús y la lista de hallazgos con sus pendientes abiertos.

## Cierre

- Recapitulación en los tres puntos de arriba.
- Mini-quiz.
- Enlace al siguiente bloque.
