# C-11 · B3 · Proveedor y responsable del despliegue

> Curso: `gobernanza-eu` · bloque `B3`

## Objetivo

Saber qué rol se ocupa en cada sistema, qué obligaciones trae y cuándo se cambia de rol sin querer.

## ⚠ Bloque normativo: verificación obligatoria antes de publicar

Cada afirmación de este bloque debe citar artículo y fecha de verificación, y esa fecha debe salir de haber abierto la fuente primaria. No se redacta de memoria ni a partir de resúmenes de terceros.

**Fuentes primarias a consultar:**

- [x] AI Act, artículos sobre definiciones y obligaciones por rol — arts. 3, 16, 23, 24, 25 y 26
- [x] AI Act, artículo sobre modificaciones sustanciales — art. 3, punto 23, y art. 43, apdo. 4
- [ ] Cláusulas contractuales tipo publicadas por la Comisión, si disponibles — la base jurídica sí está verificada (art. 25, apdo. 4); el repositorio de cláusulas publicadas no se ha podido abrir

**Fecha de verificación:** 2026-08-31 · sobre el texto consolidado `02024R1689 — ES — 27.07.2026 — 001.001`, que incorpora el Reglamento (UE) 2026/1744

> Aviso al alumno que debe aparecer en el bloque: este material es formativo y no constituye asesoramiento jurídico.

## Guion de slides

26 slides de contenido. Una idea por slide, con un ejemplo real o del caso Meridiana. Nada de relleno.

### 1. Los roles del reglamento y por qué importan

El reglamento no regula «la IA». Regula a **sujetos** concretos: el que construye el sistema, el que lo usa bajo su autoridad, el que lo trae de fuera de la Unión, el que lo revende. Cada uno tiene su lista de obligaciones, y las listas no se parecen entre sí.

De ahí la consecuencia práctica que ordena todo este bloque: la pregunta «¿qué tenemos que cumplir?» no tiene respuesta hasta que se contesta antes «¿qué somos, y respecto a qué sistema?». No es una formalidad de abogados. Es la variable que decide si te toca montar un sistema de gestión de riesgos, un expediente técnico y una evaluación de conformidad, o si te toca usar un sistema conforme a unas instrucciones, poner personas competentes a supervisarlo y conservar registros.

Meridiana enseña la trampa en una frase: **usa un modelo de un tercero dentro de un sistema propio que ella misma pone en servicio**. Frente al modelo es cliente. Frente a su agente de siniestros es fabricante. Los dos roles conviven en la misma empresa, a veces en la misma persona, y llevan obligaciones distintas que nadie separa hasta que llega una reclamación.

El objetivo del bloque no es memorizar definiciones: es tener un procedimiento para asignar rol, saber qué te cambia cuando tocas un sistema ajeno, y dejarlo escrito antes de que alguien lo pregunte.

Las denominaciones oficiales en español están todas en el **artículo 3** (Definiciones): «proveedor» es el punto 3, «responsable del despliegue» el punto 4, «importador» el punto 6 y «distribuidor» el punto 7. El mismo artículo define en el punto 68 el «proveedor posterior», que es el nombre técnico de quien integra un modelo de IA —propio o ajeno— dentro de su sistema; conviene retenerlo, porque es la etiqueta que le corresponde a Meridiana en el capítulo de modelos de uso general. Verificado el 31/08/2026.

> Fuentes primarias a abrir: texto consolidado del AI Act en la versión española del DOUE, artículo de definiciones; considerandos que acompañan a esas definiciones.

### 2. Proveedor: definición y obligaciones

Proveedor es, en esencia, quien **desarrolla un sistema de IA y lo pone en el mercado o en servicio bajo su propio nombre o marca**, lo haya construido él o se lo haya encargado a otro. Los dos verbos importan: «poner en el mercado» es comercializarlo; «poner en servicio» es usarlo para su finalidad prevista. Una compañía que construye una herramienta solo para sí misma y nunca la vende puede ser proveedora igualmente, y ese es exactamente el caso de Meridiana.

Es el rol pesado. Cuando el sistema es de alto riesgo, el proveedor es quien tiene que sostener el sistema de gestión de riesgos, la gobernanza de datos, la documentación técnica, el registro de eventos, las instrucciones de uso, la evaluación de conformidad, la declaración correspondiente, la inscripción en el registro que proceda y la vigilancia posterior a la comercialización.

La enumeración vinculante es el **artículo 16**, letras a) a l), y conviene leerla en su orden porque cada letra remite a otro artículo: cumplir los requisitos de la sección 2 [a)]; hacer constar en el sistema, su embalaje o su documentación el nombre, nombre comercial o marca registrada y la dirección de contacto [b)]; contar con el sistema de gestión de la calidad del art. 17 [c)]; conservar la documentación del art. 18 [d)]; conservar, cuando estén bajo su control, los archivos de registro del art. 19 [e)]; someter el sistema a la evaluación de la conformidad del art. 43 **antes** de introducirlo en el mercado o ponerlo en servicio [f)]; elaborar la declaración UE de conformidad del art. 47 [g)]; colocar el marcado CE del art. 48 [h)]; cumplir el registro del art. 49, apdo. 1 [i)]; adoptar las medidas correctoras y facilitar la información del art. 20 [j)]; demostrar la conformidad previa solicitud motivada de la autoridad nacional competente [k)]; y velar por los requisitos de accesibilidad de las Directivas (UE) 2016/2102 y (UE) 2019/882 [l)]. Un detalle que se cuela en casi todos los resúmenes: la **vigilancia poscomercialización no es una letra del art. 16**; entra por el art. 17, apdo. 1, letra h), dentro del sistema de gestión de la calidad, y se regula en el art. 72. Verificado el 31/08/2026.

La idea que hay debajo, y que sí se puede afirmar sin fuente: el reglamento carga las obligaciones sobre **quien tiene el conocimiento y el control del diseño**. Solo el que decide la arquitectura puede documentar por qué el sistema hace lo que hace. Por eso el proveedor no puede delegar el fondo de estas obligaciones, aunque subcontrate el trabajo.

El error frecuente: creer que «no vendemos nada, es de uso interno» exime. No exime; a lo sumo cambia por qué vía entra la obligación.

> Fuentes primarias a abrir: AI Act, artículo de definiciones (entrada «proveedor», «introducción en el mercado», «puesta en servicio»); capítulo de obligaciones de los proveedores de sistemas de alto riesgo.

### 3. Responsable del despliegue: definición y obligaciones

Responsable del despliegue es quien **utiliza un sistema de IA bajo su propia autoridad** en el marco de una actividad profesional. No es «el usuario final» en el sentido coloquial: el asegurado que rellena el parte en el portal no es responsable del despliegue de nada. Lo es la organización que decide que ese sistema entre en su proceso y bajo qué condiciones.

Sus obligaciones son de otra naturaleza. No documenta el diseño —no lo conoce—, sino que responde de **cómo se usa**: emplear el sistema conforme a las instrucciones de uso del proveedor, encargar la supervisión humana a personas con competencia, formación y autoridad reales, vigilar el funcionamiento, reaccionar cuando algo se tuerce, conservar los registros que estén bajo su control e informar a quien corresponda cuando aparezca un riesgo o un incidente.

La lista completa es el **artículo 26**, apartados 1 a 12. Los que sostienen el bloque: medidas técnicas y organizativas para usar el sistema con arreglo a las instrucciones de uso [apdo. 1]; encomendar la supervisión humana a personas físicas «que tengan la competencia, la formación y la autoridad necesarias» [apdo. 2]; asegurar que los datos de entrada sean pertinentes y suficientemente representativos en vista de la finalidad prevista, **en la medida en que ejerza el control** sobre esos datos [apdo. 4]; vigilar el funcionamiento e informar al proveedor conforme al art. 72, y —si hay motivos para considerar que el uso conforme a las instrucciones genera un riesgo en el sentido del art. 79, apdo. 1— informar sin demora indebida al proveedor o distribuidor y a la autoridad de vigilancia del mercado **y suspender el uso** [apdo. 5]; conservar los archivos de registro que estén bajo su control [apdo. 6, tratado en B4]; e informar a los representantes de los trabajadores antes de poner en servicio el sistema en el lugar de trabajo [apdo. 7].

Sobre las dos preguntas que abría el marcador: cuando el responsable del despliegue es **autoridad pública** o institución, órgano u organismo de la Unión, se le añaden las obligaciones de registro del art. 49 y la prohibición de usar el sistema si constata que no está registrado en la base de datos de la UE [apdo. 8]; y además el art. 27 le impone una evaluación de impacto relativa a los derechos fundamentales, que alcanza también a las entidades privadas que prestan servicios públicos. Cuando el sistema **afecta a personas físicas**, el apdo. 11 obliga a informar a las personas físicas de que están expuestas a un sistema de alto riesgo del anexo III que toma decisiones o ayuda a tomarlas sobre ellas, y el apdo. 9 remite a la evaluación de impacto en protección de datos del art. 35 del RGPD. Verificado el 31/08/2026.

El matiz que se olvida siempre: **la supervisión humana no es una obligación del papel, es una obligación de plantilla**. Los 24 tramitadores de Meridiana son la supervisión humana del sistema. Si no tienen tiempo, ni criterio, ni potestad para desviarse de la propuesta del agente, la obligación está incumplida aunque el organigrama diga otra cosa.

> Fuentes primarias a abrir: AI Act, artículo de definiciones (entrada «responsable del despliegue»); artículo de obligaciones de los responsables del despliegue de sistemas de alto riesgo.

### 4. Importador y distribuidor, en una slide

Los otros dos roles de la cadena existen para que el reglamento tenga a quién agarrar cuando el proveedor está fuera de la Unión o cuando el producto pasa por varias manos antes de llegar al usuario.

- **Importador.** Quien está establecido en la Unión e introduce en el mercado un sistema de un proveedor establecido fuera. Es el punto de entrada geográfico de la responsabilidad.
- **Distribuidor.** Quien comercializa un sistema sin ser ni proveedor ni importador. Un integrador que revende una plataforma de terceros a aseguradoras españolas está aquí.

Lo que se les pide es **verificación documental, no técnica**: comprobar que el sistema lleva lo que tiene que llevar —marcado, documentación, instrucciones—, no rehacer la evaluación de conformidad. No tienen el diseño, así que no se les puede pedir que respondan de él. Sí se les pide que no pongan en circulación algo que evidentemente no cumple, y que actúen cuando lo detecten.

El alcance exacto está tasado y es corto. El **artículo 23**, apdo. 1, obliga al importador a verificar, antes de introducir el sistema en el mercado, cuatro cosas: que el proveedor haya llevado a cabo la evaluación de la conformidad del art. 43 [a)]; que haya elaborado la documentación técnica del art. 11 y el anexo IV [b)]; que el sistema lleve el marcado CE y vaya acompañado de la declaración UE de conformidad del art. 47 y de las instrucciones de uso [c)]; y que el proveedor haya designado representante autorizado conforme al art. 22, apdo. 1 [d)]. El **artículo 24**, apdo. 1, pide al distribuidor menos todavía: comprobar el marcado CE, la copia de la declaración UE de conformidad y las instrucciones de uso, y que proveedor e importador hayan cumplido, respectivamente, el art. 16, letras b) y c), y el art. 23, apdo. 3. Ninguno de los dos rehace la evaluación de conformidad: verifican que existe.

Lo que sí les alcanza es actuar cuando detectan el problema —no comercializar hasta que se consiga la conformidad, e informar al proveedor, al importador y a las autoridades de vigilancia del mercado (arts. 23, apdo. 2, y 24, apdos. 2 y 4)— y conservar durante **diez años** copia del certificado, las instrucciones de uso y la declaración UE de conformidad, en el caso del importador (art. 23, apdo. 5). Verificado el 31/08/2026.

Meridiana no es ninguno de los dos: no comercializa su agente ni lo importa. Se menciona porque **estos dos roles se convierten en proveedor con las mismas tres puertas de la slide 6**, y un distribuidor que le pone su marca al producto ajeno se lleva una sorpresa cara.

> Fuentes primarias a abrir: AI Act, artículo de definiciones (entradas «importador» y «distribuidor»); artículos de obligaciones de importadores y de distribuidores.

### 5. El caso habitual: usas un modelo de un tercero

La geometría real de casi todos los proyectos de este curso tiene dos capas y hay que verlas separadas:

```
Proveedor del modelo de propósito general
        │  (API, pesos, licencia, instrucciones de uso)
        ▼
Tu sistema de IA: prompts, reglas, tools, datos, interfaz
        │  (puesto en servicio bajo tu nombre)
        ▼
Uso profesional dentro de tu proceso
```

Tres consecuencias que la gente confunde a diario:

1. **El proveedor del modelo no es proveedor de tu sistema.** Él responde de su modelo. Tú respondes del sistema que has montado alrededor. Que tu producto contenga su modelo no le convierte en fabricante de tu producto, igual que el fabricante del motor no es el fabricante del coche.
2. **Su cumplimiento no es el tuyo.** Que él tenga su documentación en regla no te da a ti la tuya. Te da *insumos* para escribirla, que es otra cosa.
3. **Tú eres las dos cosas a la vez.** Responsable del despliegue hacia arriba —usas su modelo— y proveedor hacia abajo —pones en servicio tu sistema—. Este doble papel es el corazón del bloque y se desarrolla en las slides 10 y 11.

El error típico está en la capa intermedia: equipos que se declaran «solo usuarios de una API» porque no entrenan nada. No entrenar no es no construir. Prompts, reglas de triaje, tools con efectos sobre expedientes reales y una interfaz para 24 tramitadores son un sistema, y alguien lo ha puesto en servicio.

### 6. Cuándo el que despliega se convierte en proveedor

Aquí está la mecánica que da nombre al bloque. Un responsable del despliegue **pasa a ser tratado como proveedor** —con toda la carga que eso arrastra— cuando cruza alguna de estas puertas sobre un sistema de alto riesgo ya introducido en el mercado:

1. **Pone su nombre o su marca** sobre el sistema (slide 8).
2. **Lo modifica sustancialmente** (slide 7).
3. **Cambia su finalidad prevista**, incluida la de un sistema que no era de alto riesgo y pasa a serlo por el nuevo uso (slide 9).

El artículo es el **25**, «Responsabilidades a lo largo de la cadena de valor de la IA», y los supuestos son exactamente tres. Su apartado 1 dice que «cualquier distribuidor, importador, responsable del despliegue o tercero será considerado proveedor de un sistema de IA de alto riesgo» —y quedará sujeto a las obligaciones del art. 16— cuando: a) ponga su nombre o marca en un sistema de alto riesgo previamente introducido en el mercado o puesto en servicio; b) lo modifique sustancialmente de tal manera que siga siendo de alto riesgo con arreglo al art. 6; c) modifique la finalidad prevista de un sistema de IA, **incluido un sistema de IA de uso general**, que no haya sido considerado de alto riesgo, de tal manera que pase a serlo conforme al art. 6. Conviene fijar el matiz de la letra c): el supuesto está escrito sobre sistemas que **no** eran de alto riesgo y lo pasan a ser; cambiar la finalidad de uno que ya lo era se juzga por la vía de la modificación sustancial.

Y sí: el proveedor original queda liberado. El apdo. 2 dice que «el proveedor que inicialmente haya introducido en el mercado el sistema de IA o lo haya puesto en servicio dejará de ser considerado proveedor de ese sistema de IA específico». No se va de vacío, eso sí: debe cooperar estrechamente con el nuevo proveedor, poner a disposición documentación técnica suficiente [a)], informar de las limitaciones y los modos de fallo conocidos [b)] y proporcionar acceso técnico específico, incluido para prueba y validación [c)]. Ese deber decae si el proveedor inicial había indicado claramente que su sistema no debe transformarse en uno de alto riesgo. Verificado el 31/08/2026.

Lo importante no es la lista, es **cómo se cruza la puerta**: sin darse cuenta, en un sprint, con un cambio que a nadie le pareció grande. El patrón se repite con una regularidad deprimente:

- Se contrata una plataforma «de resúmenes documentales».
- Alguien la conecta al flujo de triaje porque ya está pagada.
- Marketing le pone el nombre del producto interno en la pantalla.
- Nadie vuelve a mirar las instrucciones de uso del fabricante.

Tres meses después, la compañía tiene las obligaciones de un proveedor y ni el expediente técnico ni la evaluación de conformidad de un proveedor. El control que evita esto no es jurídico: es una **puerta en el proceso de cambio**, un punto donde alguien se pregunta si este cambio toca alguna de las tres puertas. Eso se monta en B6.

> Fuentes primarias a abrir: AI Act, artículo sobre supuestos en que responsables del despliegue, distribuidores o importadores pasan a considerarse proveedores; considerandos asociados.

### 7. Modificación sustancial: qué la constituye

La intuición de todo el mundo es que «sustancial» significa «grande». No es eso. La idea del reglamento es otra y es más útil: una modificación es sustancial cuando **el sistema deja de ser aquel sobre el que se hizo la evaluación de conformidad**, porque el cambio no estaba previsto en ella y afecta al cumplimiento de los requisitos o al perfil de riesgo.

La definición está en el **artículo 3, punto 23**, y tiene tres elementos que conviene leer despacio: «un cambio en un sistema de IA tras su introducción en el mercado o puesta en servicio **que no haya sido previsto o proyectado en la evaluación de la conformidad inicial** realizada por el proveedor y a consecuencia del cual **se vea afectado el cumplimiento** por parte del sistema de los requisitos establecidos en el capítulo III, sección 2, **o que dé lugar a una modificación de la finalidad prevista** para la que se haya evaluado el sistema». No aparece por ningún lado la palabra «grande».

La consecuencia operativa está en el **artículo 43, apdo. 4**: un sistema ya evaluado se somete a una nueva evaluación de la conformidad en caso de modificación sustancial, con independencia de si va a distribuirse después o de si sigue usándolo el mismo responsable del despliegue. Y su párrafo segundo cierra el círculo con la lógica de ingeniería de más abajo: en sistemas que continúan aprendiendo, los cambios **predeterminados por el proveedor en el momento de la evaluación inicial y recogidos en la documentación técnica del anexo IV, punto 2, letra f), no constituyen modificaciones sustanciales**. Documentar el cambio de antemano es, literalmente, lo que evita tener que reevaluar. Verificado el 31/08/2026.

De ahí sale la mecánica que sí se puede enseñar sin fuente, porque es lógica de ingeniería:

- **Si el cambio estaba anticipado y documentado en el expediente**, no rompe nada. Un sistema diseñado para reentrenarse con datos nuevos dentro de límites descritos de antemano sigue siendo el mismo sistema.
- **Si el cambio no estaba anticipado y toca el perfil de riesgo**, sí. Y aquí entra el detalle que hace incómoda esta slide en un curso de agentes: en un sistema con LLM, buena parte de la conducta vive en sitios que no parecen cambios de producto.

Cuatro cambios de Meridiana, ordenados de menos a más sospechoso:

- Rediseñar la pantalla del tramitador. No toca la decisión.
- Reescribir el prompt de extracción de FNOL. Toca la entrada de todo lo demás.
- Cambiar el umbral de aprobación de 1.500 € a 6.000 €. Multiplica lo que se aprueba con un clic.
- Quitar la regla de derivación por lesiones personales. Cambia qué decide una máquina y qué decide una persona.

Los dos últimos huelen a modificación sustancial. Los dos son un diff de una línea, y ninguno de los dos parece «grande» en una revisión de código.

> Fuentes primarias a abrir: AI Act, definición de «modificación sustancial»; artículo sobre cambios en sistemas de alto riesgo tras la evaluación de conformidad; orientaciones de la Comisión sobre cambios, si están publicadas.

### 8. Poner tu marca sobre un sistema ajeno

La puerta más fácil de cruzar y la más barata de evitar. Coges un producto de un tercero, lo llamas «Asistente Meridiana», le pones el logo en la cabecera y lo despliegas para los tramitadores. Para quien lo usa, y para quien lo reclama, ese sistema **es de Meridiana**.

La lógica del reglamento es la de siempre en derecho de producto: quien se presenta ante el mercado como responsable de una cosa, responde de esa cosa. Si un tramitador tiene que reclamar, tiene que poder saber a quién. Un logo es una declaración de autoría, y las declaraciones de autoría se toman en serio.

La redacción exacta es la del **artículo 25, apdo. 1, letra a)**: se pasa a ser proveedor «cuando ponga su nombre o marca en un sistema de IA de alto riesgo previamente introducido en el mercado o puesto en servicio, **sin perjuicio de los acuerdos contractuales que estipulen que las obligaciones se asignan de otro modo**». Dos lecturas que importan para esta slide. La primera: el texto dice «su nombre o marca» y **no distingue** entre marca registrada, nombre comercial de producto o rotulación interna; no hay un umbral escrito por debajo del cual poner tu nombre no cuente. La segunda: la salvedad final sobre los acuerdos contractuales reparte obligaciones entre las partes, pero no evita que se te considere proveedor —es exactamente la distinción que desarrolla la slide 15. Verificado el 31/08/2026.

Lo que hace especialmente traicionero este punto es que **no es una decisión de ingeniería ni de cumplimiento**: la toma marca, o el equipo de producto interno, o alguien que quería que la herramienta «se sintiera nuestra». Nadie firma un documento titulado «asunción de obligaciones de proveedor»; se firma un ticket de diseño.

Prácticas concretas para no cruzar la puerta sin querer:

- Mantener visible la identidad del proveedor original en la interfaz, no esconderla detrás de una capa de marca.
- Que cualquier cambio de rotulación sobre software de terceros pase por la misma puerta de decisión que un cambio funcional.
- Si de verdad quieres tu marca encima —hay razones legítimas—, tomar la decisión **sabiendo el precio**: pasas a proveedor y necesitas el expediente completo.

> Fuentes primarias a abrir: AI Act, artículo sobre asunción del rol de proveedor por marcado o denominación; considerandos sobre identificación del responsable ante el mercado.

### 9. Cambiar la finalidad prevista

La **finalidad prevista** no es lo que tú piensas hacer con el sistema: es lo que el proveedor declaró que el sistema sirve para hacer, escrito en sus instrucciones de uso y en su documentación. Es el perímetro dentro del cual él respondió de que la cosa funciona.

Usarlo fuera de ese perímetro te deja solo. Y no en sentido moral: en sentido regulatorio, porque el cambio de finalidad es una de las puertas que te convierte en proveedor.

Las dos definiciones están en el **artículo 3**. «Finalidad prevista» (punto 12) es «el uso para el que un proveedor concibe un sistema de IA, incluidos el contexto y las condiciones de uso concretos, según la información facilitada por el proveedor **en las instrucciones de uso, los materiales y las declaraciones de promoción y venta, y la documentación técnica**». Fíjate en dónde vive: no en la cabeza de nadie, sino en documentos que se pueden leer y citar —y que incluyen el material comercial. «Uso indebido razonablemente previsible» (punto 13) es «la utilización de un sistema de IA de un modo que no corresponde a su finalidad prevista, pero que puede derivarse de un comportamiento humano o una interacción con otros sistemas, incluidos otros sistemas de IA, razonablemente previsible»: es decir, lo que el proveedor debía haber anticipado aunque no lo quisiera.

El artículo que ata el cambio de finalidad al cambio de rol es el **25, apdo. 1, letra c)**, con el matiz ya visto en la slide 6: el supuesto se activa cuando el sistema no era de alto riesgo y el nuevo uso lo convierte en tal. Verificado el 31/08/2026.

El ejemplo del caso, que es el que ocurre de verdad: Meridiana contrata una herramienta documental cuya finalidad declarada es **resumir documentos adjuntos para lectura humana**. Funciona bien. Alguien observa que el resumen ya identifica si hay parte amistoso, y lo enchufa a la decisión de vía del triaje. En ese momento la herramienta ha pasado de «ayudar a leer» a **participar en una decisión sobre el expediente de una persona**, que es un uso que su fabricante nunca evaluó ni documentó.

Cómo se detecta antes de que pase, sin necesidad de un jurista en cada reunión: pregunta si **la salida del sistema entra en algún camino que produce una consecuencia para un asegurado**. Si la respuesta cambia de «no» a «sí», la finalidad ha cambiado, se llame como se llame el ticket.

Y la contramedida barata: leer las instrucciones de uso del proveedor **antes** de integrarlo, no cuando ya está en producción. Suelen decir con bastante claridad para qué no sirve.

> Fuentes primarias a abrir: AI Act, definiciones de «finalidad prevista» y «uso indebido razonablemente previsible»; artículo sobre cambio de finalidad y asunción del rol de proveedor.

### 10. Meridiana: qué rol ocupa respecto al modelo que usa

Respecto al modelo de propósito general que hay detrás de la extracción de FNOL, Meridiana es **cliente y responsable del despliegue de ese modelo dentro de su sistema**. No lo entrenó, no lo diseñó, no puede documentar cómo funciona por dentro y no puede corregirlo. Lo consume por una API y lo trata —bien tratado— como una dependencia externa sustituible.

Lo que eso significa en la práctica, y es menos cómodo de lo que parece:

- **No hereda el cumplimiento del proveedor del modelo.** Que él haya hecho sus deberes no rellena ni una línea del expediente técnico de Meridiana.
- **No puede prometer lo que no controla.** Si el proveedor cambia la versión del modelo por debajo, la conducta del agente cambia sin que nadie de Meridiana haya tocado nada. Eso es un riesgo de proveedor, y se gestiona con contrato (slide 13), con evals en CI y con el runbook correspondiente del curso 3.
- **Sí hereda una dependencia informativa.** Lo único que tiene de ese modelo es lo que el proveedor le dé: ficha técnica, límites conocidos, política de versiones. Si no lo da, Meridiana tiene un agujero en su propio expediente que no puede tapar con buena voluntad (se trata en B4, slide 24).

El detalle que conviene fijar ahora porque vuelve en la slide 20: **usar el modelo por API, con prompts propios y recuperación de contexto propia, no convierte a Meridiana en proveedora del modelo**. La convierte en proveedora de otra cosa, que es su sistema. Son dos objetos regulados distintos y hay que nombrarlos por separado o la conversación se enreda enseguida.

### 11. Meridiana: qué rol ocupa respecto a su propio agente

Respecto al agente de siniestros, Meridiana es **proveedora**. Lo ha especificado, lo ha construido, decide sus reglas de triaje y lo ha puesto en servicio en su propia operación bajo su nombre. Que no lo venda a nadie no cambia nada: el rol se gana por poner en servicio, no por facturar.

Y a la vez es **responsable de su despliegue**, porque son sus 24 tramitadores los que lo usan bajo su autoridad. El rol doble no es una rareza: es lo normal en cualquier organización que construye herramientas para sí misma.

Lo que ese doble papel provoca de verdad, y por qué merece una slide:

- **No hay nadie a quien reclamarle la documentación.** Cuando eres solo responsable del despliegue, el expediente técnico y las instrucciones de uso llegan de fuera y tu trabajo es exigirlos y usarlos. Cuando eres las dos cosas, **tienes que escribirte las instrucciones de uso a ti misma**. Suena absurdo hasta que alguien pregunta en qué condiciones el sistema puede usarse y nadie sabe contestar.
- **No hay contraparte que discipline.** Un proveedor externo te obliga a formular requisitos, a leer un contrato, a comprobar entregables. En un desarrollo interno esa fricción desaparece y con ella la evidencia. La compensación es artificial: **puertas de decisión internas** y un revisor que no sea el mismo que escribió el código.
- **Se confunden los sombreros en la misma reunión.** El equipo que decide que la regla de lesiones se mantiene (proveedor) es el mismo que decide cuántos casos revisa un tramitador al día (responsable del despliegue). Conviene separarlo en el acta, porque las dos decisiones se justifican ante cosas distintas.

### 12. Obligaciones que se heredan y obligaciones que no

Regla corta y desagradable: **el cumplimiento no se hereda**. Lo que viaja por la cadena de suministro son *insumos* —información, documentación, garantías contractuales—, no obligaciones cumplidas.

Puesto en concreto sobre Meridiana:

- **No se hereda:** el expediente técnico de tu sistema, tu gestión de riesgos, tu evaluación de conformidad, tus registros de eventos, tu supervisión humana, tu vigilancia posterior. Todo eso es tuyo porque describe *tu* sistema, y nadie fuera de tu casa lo conoce.
- **Sí se recibe, si lo exiges:** la ficha técnica del modelo, sus límites conocidos, sus resultados de evaluación, su política de versiones, sus compromisos de retención de datos. Son ladrillos para construir lo tuyo.
- **Zona gris, ya no tanto:** las obligaciones que el proveedor del modelo tenga por su propio régimen. El **artículo 53, apdo. 1, letra b)**, le obliga a elaborar, mantener actualizada y poner a disposición de los proveedores de sistemas que vayan a integrar el modelo una información y documentación que debe permitirles «entender bien las capacidades y limitaciones del modelo» y cumplir sus propias obligaciones, y que contendrá **como mínimo los elementos del anexo XII**. Es decir: buena parte de esa lista se pide por reglamento, no por contrato. Con dos condiciones que también están en el texto: la obligación se entiende «sin perjuicio de la necesidad de observar y proteger los derechos de propiedad intelectual e industrial y la información empresarial confidencial o los secretos comerciales», y **no se aplica** —art. 53, apdo. 2— a los modelos divulgados con licencia libre y de código abierto cuyos parámetros, arquitectura e información de uso sean públicos, salvo que sean modelos con riesgo sistémico. Verificado el 31/08/2026.

El error simétrico también existe y se ve menos: organizaciones que asumen obligaciones que no les tocan, se ponen a documentar el modelo de un tercero como si lo hubieran hecho ellas, gastan seis meses y producen un documento que no describe nada verificable. Documentar lo que no controlas no es prudencia, es ruido en el expediente.

La prueba de si una obligación es tuya: **¿puedes cambiar algo para cumplirla?** Si la respuesta es no, o es del proveedor, o es una cláusula de contrato, pero no es una tarea tuya.

> Fuentes primarias a abrir: AI Act, artículos sobre obligaciones de los proveedores de modelos de propósito general hacia quienes los integran; anexos de documentación asociados a esas obligaciones.

### 13. Qué exigir por contrato al proveedor del modelo

Todo lo que la slide anterior deja fuera del reglamento se consigue por contrato o no se consigue. Y lo que no consigues, no lo tienes: aparece como un hueco en tu expediente el día de la inspección.

La lista que Meridiana debería llevar a la mesa de negociación, ordenada por lo que más duele cuando falta:

1. **Aviso previo de cambio de versión de modelo**, con ventana suficiente para pasar los evals antes de que el cambio llegue a producción. Es la cláusula que más te ahorra y la que menos se pide.
2. **Versiones fijadas y política de deprecación por escrito.** Poder anclar una versión concreta durante un periodo conocido convierte un riesgo incontrolable en uno planificable.
3. **Ficha técnica y limitaciones conocidas**, en un formato que puedas citar dentro de tu documentación.
4. **Resultados de evaluaciones de seguridad y de capacidad**, o al menos su resumen y su metodología.
5. **Tratamiento de los datos que envías:** retención, uso para entrenamiento, ubicación, subencargados. Se cruza con B5, pero también es materia de este bloque porque define qué puedes prometer tú aguas abajo.
6. **Cooperación ante requerimiento de una autoridad**, con plazo comprometido. Sin esto, la inspección te pilla dependiendo de la buena voluntad de un tercero.
7. **Exportación de tus propios registros** en formato utilizable, y qué pasa con ellos si el contrato termina.
8. **Notificación de incidentes** que afecten al servicio o a la seguridad del modelo.

Realismo, que también es parte del oficio: contra un proveedor grande y unos términos estándar, una aseguradora media no negocia gran cosa. Entonces la respuesta correcta no es fingir que se consiguió, es **anotar el hueco como riesgo aceptado, con firma de quien lo acepta**, y compensarlo con controles propios: evals en CI que detecten el cambio, capacidad de conmutar de proveedor, y el interruptor para volver al flujo manual.

### 14. Cláusulas modelo: qué cubren y qué se suele olvidar

Existen cláusulas contractuales tipo para la adquisición de sistemas de IA promovidas en el ámbito europeo, pensadas sobre todo para contratación pública y reutilizables como punto de partida en el sector privado.

Lo que sí está verificado es su base jurídica y quién las hace. El **artículo 25, apdo. 4**, párrafo segundo —redacción dada por el Reglamento (UE) 2026/1744— dice que «la Oficina de IA podrá elaborar y recomendar cláusulas contractuales tipo, **de carácter voluntario**, entre los proveedores de sistemas de IA de alto riesgo y terceros que suministren herramientas, servicios, componentes o procesos que se utilicen o integren en los sistemas de IA de alto riesgo», teniendo en cuenta los posibles requisitos contractuales de determinados sectores o modelos de negocio, y que «se publicarán y estarán disponibles gratuitamente en un formato electrónico fácilmente utilizable». Tres consecuencias: las publica la **Oficina de IA**, no obligan, y están escritas para la relación entre proveedor de sistema de alto riesgo y su cadena de suministro —no para toda compra de IA.

**Qué cláusulas concretas están publicadas hoy, con su título exacto y su versión, no se dice aquí**: el repositorio de la Comisión no se pudo abrir en la verificación del 31/08/2026 y no se escribe de memoria. Es lo primero que hay que mirar antes de apoyarse en ellas.

Lo que se puede decir sin abrir la fuente es **para qué sirven y para qué no**, que es lo que se enseña mal:

- **Sirven** como red de seguridad: recogen los compromisos que un comprador sin equipo jurídico especializado nunca se acordaría de pedir, y ahorran discutir de cero.
- **No sirven** como sustituto del análisis. Están escritas para un comprador genérico que adquiere un sistema cerrado, no para una aseguradora que integra un modelo por API dentro de un agente propio con tools que escriben en expedientes.

Los cuatro huecos que suele haber que añadir a mano, precisamente porque no encajan en el molde genérico:

- **Cambio de versión del modelo por debajo**, con aviso y ventana de prueba. El molde suele pensar en software que no cambia solo.
- **Acceso a evidencia utilizable en tu expediente**, no solo declaraciones comerciales.
- **Plazos de respuesta ante requerimiento de autoridad**, no solo obligación genérica de cooperar.
- **Continuidad y salida**: qué se te entrega y en qué formato cuando el contrato acaba.

Y un aviso de método: usar una cláusula tipo sin leerla produce contratos que prometen cosas que tu proveedor no puede cumplir. Una cláusula incumplible es peor que ninguna, porque da sensación de cobertura.

> Fuentes primarias a abrir: repositorio de cláusulas contractuales tipo para la contratación de IA publicado en el ámbito de la Comisión; guías de contratación pública de IA de la autoridad nacional competente.

### 15. Repartir responsabilidades por escrito

Un contrato **no cambia tu rol frente al regulador**. Si por los criterios del reglamento eres proveedor, lo eres aunque hayas firmado un anexo diciendo que las obligaciones son del integrador. El rol se determina por hechos: quién diseña, quién pone en servicio, bajo qué nombre.

Lo que sí hace un buen reparto por escrito, y no es poco:

- **Asigna el trabajo.** Quién redacta cada sección del expediente, quién ejecuta las pruebas, quién guarda qué registros y dónde.
- **Fija plazos.** Cuánto tarda el proveedor en entregarte información cuando se la pides con una autoridad esperando.
- **Reparte el coste.** Si un incumplimiento del proveedor te cuesta una sanción, quién la paga es materia civil y se pacta (slide 25).
- **Elimina el punto ciego.** La siguiente slide va de eso.

La forma práctica que funciona es aburrida: una tabla, con una fila por obligación y tres columnas —quién la ejecuta, quién la revisa, con qué evidencia se demuestra—, firmada por las dos partes y guardada con el contrato, no en el portátil de alguien.

El antipatrón, tan común que casi es un género literario: un anexo de dos páginas que dice que «cada parte cumplirá la normativa aplicable». Eso no reparte nada. El día del problema, las dos partes leen la misma frase y entienden cosas opuestas.

### 16. El punto ciego: nadie asume la evaluación de conformidad

El fallo más caro de este bloque no es hacer mal una obligación: es que **nadie la haga porque los dos creen que la hace el otro**.

El guion se repite:

> — Nosotros solo integramos. La evaluación de conformidad la habrá hecho el fabricante del sistema.

> — Nosotros vendemos un componente. El sistema final lo monta el cliente, y es él quien lo pone en servicio.

Los dos tienen media razón, y por eso el hueco sobrevive a varias reuniones. Nadie miente; simplemente cada uno describe su mitad.

Las vías están en el **artículo 43** y son dos, con reparto tasado. Para los sistemas de alto riesgo del **anexo III, punto 1** (biometría), el proveedor elige entre el procedimiento fundamentado en el **control interno** del anexo VI y el fundamentado en la evaluación del sistema de gestión de la calidad y de la documentación técnica **con participación de un organismo notificado**, del anexo VII (art. 43, apdo. 1); y queda obligado al anexo VII cuando no existan normas armonizadas o especificaciones comunes, no las haya aplicado, o solo haya aplicado parte de ellas. Para los sistemas del **anexo III, puntos 2 a 8** —donde cae el grueso de los casos de este curso— el apdo. 2 impone el control interno del **anexo VI**, «que no contempla la participación de un organismo notificado». Para los del **anexo I, sección A**, el apdo. 3 remite al procedimiento del acto sectorial correspondiente. Y el apdo. 4 añade la regla que más importa aquí: tras una modificación sustancial hay que volver a evaluar.

Traducido a lo que interesa en esta slide: en la vía más frecuente **no hay un tercero que firme por ti**. La evaluación de conformidad la asume el proveedor, y si nadie se ha declarado proveedor, no la ha hecho nadie. Verificado el 31/08/2026. En B2 se trata la clasificación; aquí solo interesa **quién la asume**.

La forma de detectar el hueco es una sola pregunta, y no admite respuesta vaga:

> «Enséñame el documento. Quién lo firmó, sobre qué versión del sistema y en qué fecha.»

Si nadie puede enseñarlo, el hueco existe. Si alguien enseña un documento del proveedor del modelo, no vale: describe el modelo, no tu sistema.

Tres señales tempranas de que estás en esta situación:

- El proyecto tiene un responsable técnico y un responsable de compras, y ninguno de los dos tiene un responsable de cumplimiento asignado por nombre.
- El contrato menciona el reglamento una vez, en una cláusula genérica.
- Cuando preguntas quién es el proveedor del sistema, la respuesta empieza por «depende de cómo lo mires».

> Fuentes primarias a abrir: AI Act, artículo sobre evaluación de la conformidad y sus procedimientos; anexos que describen cada vía; artículo sobre obligaciones del proveedor de sistemas de alto riesgo.

### 17. Ejercicio: mapa de roles de Meridiana

Antes de seguir, para que las slides anteriores dejen de ser abstractas: dibuja el mapa completo de roles del caso. No es una tabla de relleno; es el documento del que salen luego el expediente técnico (B4) y la gobernanza (B6).

Una fila por **cada sistema o componente de IA** que interviene en el flujo de siniestros, y estas columnas:

- **Componente.** El modelo de propósito general de extracción; el agente de FNOL y triaje; el agente de propuesta de resolución; cualquier herramienta de terceros conectada al flujo.
- **Quién lo desarrolló.**
- **Bajo qué nombre se pone en servicio.**
- **Rol de Meridiana respecto a él.** Proveedor, responsable del despliegue, o ambos.
- **Rol del tercero, si lo hay.**
- **Puertas cruzadas.** Marca, modificación sustancial, cambio de finalidad: sí o no, y por qué.
- **Quién responde de cada obligación**, con nombre de persona y no de departamento.

Dos reglas para que el ejercicio tenga valor:

1. **Una fila por componente, no por proyecto.** El error más común es escribir una sola fila que diga «Meridiana IA» y perder justo la distinción entre el modelo y el sistema, que es la que decide todo.
2. **Lo que no se sepa se escribe como hueco, no se rellena a ojo.** Un mapa con tres celdas marcadas como pendientes es un mapa útil. Un mapa completo a base de suposiciones es una trampa que alguien creerá dentro de un año.

La versión terminada de este mapa es el entregable del lab de este bloque.

### 18. Modelos de propósito general: obligaciones de su proveedor

Los modelos de propósito general tienen un régimen propio, distinto del de los sistemas de alto riesgo, porque el objeto regulado es distinto: un modelo no tiene finalidad prevista, tiene capacidades. Se puede usar para resumir contratos o para redactar peticiones de documentación, y su fabricante no sabe cuál de las dos harás tú.

A grandes rasgos, y sujeto a comprobación, sus proveedores deben mantener documentación del modelo, poner determinada información a disposición de quienes lo integran en sus sistemas, atender a la normativa de derechos de autor y publicar información sobre los contenidos usados en el entrenamiento; y existe un régimen reforzado para los modelos que se consideren de riesgo sistémico. Abierto el texto, esto es lo que dice.

La lista base es el **artículo 53, apdo. 1**, con cuatro letras: elaborar y mantener actualizada la documentación técnica del modelo —incluida la información sobre entrenamiento, pruebas y resultados de evaluación— con el contenido mínimo del **anexo XI**, para facilitarla a la Oficina de IA y a las autoridades nacionales previa solicitud [a)]; elaborar, mantener y poner a disposición de quienes integren el modelo la información del **anexo XII** [b)]; establecer directrices para cumplir el Derecho de la Unión sobre derechos de autor, incluida la detección de la reserva de derechos del art. 4, apdo. 3, de la Directiva (UE) 2019/790 [c)]; y publicar «un resumen suficientemente detallado del contenido utilizado para el entrenamiento», con el modelo facilitado por la Oficina de IA [d)].

El criterio de la categoría reforzada es el **artículo 51**: un modelo se clasifica con riesgo sistémico si tiene capacidades de gran impacto evaluadas con herramientas y metodologías adecuadas [apdo. 1 a)], o si la Comisión así lo decide atendiendo a los criterios del **anexo XIII** [apdo. 1 b)]. Y el apdo. 2 fija la presunción cuantitativa: se presume que hay capacidades de gran impacto «cuando la cantidad acumulada de cálculo utilizada para su entrenamiento, medida en operaciones de coma flotante, sea superior a 10²⁵». Las obligaciones adicionales son el **artículo 55**.

Quién supervisa: el **artículo 88, apdo. 1**, atribuye a la **Comisión** competencias exclusivas para supervisar y hacer cumplir el capítulo V, y le encarga confiar la ejecución de esas tareas a la **Oficina de IA**. No es la autoridad nacional. Verificado el 31/08/2026.

Por qué le importa esto a Meridiana, que no fabrica modelos: **porque esas obligaciones son la fuente de los insumos que necesita**. Todo lo que un proveedor de modelo esté obligado a poner a disposición de sus integradores es material que Meridiana puede reclamar sin negociar, y que va directo a su propio expediente. Saber qué te deben te ahorra pedir por favor lo que puedes pedir por derecho.

Y la cautela: este es el terreno del reglamento que más se ha movido y donde más resúmenes de terceros circulan desactualizados. Esta slide se reescribe con el texto delante, no con una entrada de blog.

> Fuentes primarias a abrir: AI Act, capítulo sobre modelos de propósito general y sus anexos; código de buenas prácticas para modelos de propósito general, si está publicado; comunicaciones del órgano europeo de supervisión de estos modelos.

### 19. Qué información debe darte el proveedor del modelo

Hay que separar dos cosas que se mezclan siempre: lo que un proveedor de modelo **debe** darte y lo que **puedes conseguir** si negocias bien.

Lo que **debe** darte está tasado en el **anexo XII**, al que remite el art. 53, apdo. 1, letra b). Su punto 1 pide una descripción general del modelo: las tareas que va a realizar y el tipo de sistemas en los que puede integrarse [a)], las políticas de usos aceptables [b)], la fecha de lanzamiento y los métodos de distribución [c)], cómo interactúa con hardware o software ajeno [d)], las versiones de software pertinentes [e)], **la arquitectura y el número de parámetros** [f)], la modalidad y el formato de entradas y salidas [g)] y la licencia del modelo [h)]. Su punto 2 baja al detalle de integración: los medios técnicos necesarios para integrarlo —instrucciones de uso, infraestructura, herramientas— [a)], la modalidad y el formato de entradas y salidas **y su tamaño máximo, por ejemplo la longitud de la ventana de contexto** [b)], e información sobre los datos usados para entrenamiento, pruebas y validación, «cuando proceda, incluidos el tipo y la procedencia de los datos y los métodos de gestión» [c)].

Sobre el nivel de detalle, el art. 53, apdo. 1, letra b), i), fija el listón por su función y no por su extensión: la información debe permitir «entender bien las capacidades y limitaciones del modelo» y cumplir las obligaciones propias. El formato no está tasado. Verificado el 31/08/2026.

Lo que sí se puede enseñar aquí es **qué hacer con lo que llegue**, que es donde falla casi todo el mundo. La documentación del proveedor tiene tres destinos y ninguno es «una carpeta compartida»:

- **Al expediente técnico**, como componente descrito con su versión, sus límites y su procedencia. Un expediente que dice «usamos un modelo de lenguaje» no describe nada.
- **A las decisiones de diseño.** Si la ficha declara que el modelo degrada con entradas muy largas o en idiomas poco representados, eso se convierte en una restricción de arquitectura y en un caso del conjunto de evaluación, no en una nota al pie.
- **Al registro de riesgos.** Cada limitación declarada por el proveedor es un riesgo identificado con fuente externa, que es la clase de riesgo más fácil de defender ante un auditor.

Dos criterios para juzgar la calidad de lo que te den:

1. **¿Es citable?** Tiene versión, fecha y un identificador estable. Una captura de una página web que cambia mañana no es evidencia.
2. **¿Es comprobable?** Dice algo que puedes verificar tú con pruebas sobre la API. Si solo contiene afirmaciones cualitativas de marketing, no ha entrado información en tu sistema de gestión: ha entrado publicidad.

> Fuentes primarias a abrir: AI Act, artículo sobre información que los proveedores de modelos de propósito general ponen a disposición de los proveedores de sistemas que los integran; anexo con el contenido mínimo de esa información.

### 20. Ajuste fino: cuándo te convierte en proveedor

La pregunta llega en todas las clases: si hago *fine-tuning* de un modelo, ¿me convierto en proveedor?

La respuesta honesta tiene dos partes. La primera es que hay que distinguir **de qué** te conviertes en proveedor: del *modelo* ajustado es una cosa, y del *sistema* que lo contiene es otra que ya tenías encima desde la slide 11. La segunda es que el umbral —a partir de qué intensidad de ajuste el modelo resultante se considera tuyo— es exactamente el tipo de dato que este curso no inventa.

De lo abierto el 31/08/2026 sí se puede afirmar una cosa: el reglamento **nombra** esta figura. El **artículo 3, punto 68**, define «proveedor posterior» como «un proveedor de un sistema de IA, también de un sistema de IA de uso general, que integra un modelo de IA, con independencia de que el modelo de IA lo proporcione él mismo y esté integrado verticalmente o lo proporcione otra entidad en virtud de relaciones contractuales». Y el **artículo 89, apdo. 2**, le reconoce un derecho concreto: presentar reclamaciones ante la Oficina de IA alegando infracciones del reglamento por parte del proveedor del modelo, debidamente motivadas y con el contenido mínimo de sus letras a) a c).

**No hay aquí ninguna cifra para el umbral** a partir del cual quien ajusta un modelo de uso general pasa a ser proveedor **del modelo**, y no la hay por un motivo: en el articulado y en los anexos del texto consolidado no se ha localizado ningún criterio cuantitativo, y los considerandos —donde suele situarse esta orientación— no forman parte del consolidado. Quien necesite el umbral tiene que leerlos en el Diario Oficial.

Lo que sí conviene ordenar es el gradiente técnico, porque la conversación se aclara sola cuando se ve escrito de menos a más:

- **Prompting.** Cambias las instrucciones. El modelo es el mismo bit a bit.
- **Recuperación de contexto.** Le das documentos propios. El modelo sigue siendo el mismo.
- **Ajuste ligero sobre el modelo base.** Ya existe un artefacto nuevo que solo tienes tú.
- **Entrenamiento continuado con volumen propio.** El resultado se parece más a un modelo tuyo que a uno ajeno.

Los dos primeros casi nadie discute que no te hacen proveedor del modelo. Los dos últimos generan un artefacto que **nadie más puede documentar, evaluar ni corregir**, y ese hecho material pesa más que cualquier etiqueta: si eres el único que puede describir cómo se comporta esa cosa, prepárate para tener que describirla.

Meridiana, hoy, está en los dos primeros escalones. Si algún día ajusta un modelo con sus 32.000 siniestros anuales, esta slide deja de ser teórica y arrastra además la trazabilidad de datos de entrenamiento de B4.

> Fuentes primarias a abrir: AI Act, artículos sobre modelos de propósito general modificados por terceros; considerandos sobre modificación de modelos; código de buenas prácticas, si trata el ajuste.

### 21. Sistemas de código abierto y sus matices

«Es de código abierto, así que no aplica» es una de las frases más caras que se pueden decir en una reunión de arquitectura.

Existen exenciones y matices para software y modelos publicados con licencias libres, pero vienen con condiciones y con excepciones a las excepciones. El texto es sorprendentemente claro y conviene citarlo entero.

Para **sistemas**, el **artículo 2, apdo. 12**: el reglamento «no se aplicará a los sistemas de IA divulgados con arreglo a licencias libres y de código abierto, **a menos que se introduzcan en el mercado o se pongan en servicio como sistemas de IA de alto riesgo o como sistemas de IA que entren en el ámbito de aplicación del artículo 5 o del artículo 50**». Es decir: la exención cae entera en cuanto hay alto riesgo, práctica prohibida u obligación de transparencia.

Para **modelos de uso general**, el **artículo 53, apdo. 2**, exime únicamente de las letras a) y b) del apdo. 1 —documentación técnica e información a los integradores— y solo para los modelos «que se divulguen con arreglo a una licencia libre y de código abierto que permita el acceso, la utilización, la modificación y la distribución del modelo y cuyos parámetros, **incluidos los pesos**, la información sobre la arquitectura del modelo y la información sobre el uso del modelo, se pongan a disposición del público». Esa es la definición operativa de «libre y de código abierto» a estos efectos, y es exigente: pesos y arquitectura públicos. La excepción «no se aplicará a los modelos de IA de uso general con riesgo sistémico». El art. 54, apdo. 6, exime en los mismos términos de nombrar representante autorizado, con la misma salvedad.

Y un tercer sitio donde asoma, menos conocido: el **artículo 25, apdo. 4** excluye de la obligación de acuerdo escrito a los terceros que pongan a disposición del público herramientas, servicios, procesos o componentes con licencia libre y de código abierto, **siempre que no sean modelos de IA de uso general**. Verificado el 31/08/2026.

Lo que se puede afirmar sin fuente, porque es estructura y no letra:

- **La exención, cuando existe, protege a quien publica, no a quien despliega.** Que un modelo se distribuya libremente no rebaja ni una obligación de la aseguradora que lo mete en su flujo de siniestros. Meridiana sería proveedora de su sistema exactamente igual.
- **«Gratis» y «sin obligaciones» son cosas distintas.** El coste que ahorras en licencia reaparece en documentación: con un modelo abierto tienes más control y más deber de describirlo, porque ya no hay un fabricante a quien pedirle la ficha.
- **La cadena se alarga.** Un modelo abierto suele venir de un repositorio, con pesos de una organización, ajustado por otra, empaquetado por una tercera. Reconstruir esa procedencia es trabajo tuyo, y aparece otra vez en B4 cuando toque documentar componentes.

El uso sensato de un modelo abierto en un contexto regulado no es «nos ahorramos el cumplimiento»: es «asumimos el cumplimiento a cambio de no depender de un tercero». Es un intercambio legítimo, siempre que se haga con los ojos abiertos.

> Fuentes primarias a abrir: AI Act, artículos y considerandos sobre exenciones para software libre y de código abierto; disposiciones sobre modelos de propósito general publicados con licencia libre.

### 22. Cadena de suministro: proveedores del proveedor

El proveedor de tu modelo tiene sus propios proveedores, y algunos de ellos acaban tocando tus datos o tu disponibilidad sin que su nombre aparezca en ningún contrato que tú hayas firmado.

La capa que normalmente hay debajo:

- **Infraestructura.** Dónde se ejecuta la inferencia, en qué región y de quién es el hierro.
- **Subencargados de tratamiento.** Quién más puede ver lo que envías. Materia de B5, pero se descubre aquí.
- **Evaluadores y equipos de pruebas externos**, cuyos informes son parte de la evidencia que te dan.
- **Proveedores de datos** usados en el entrenamiento, que tú no verás nunca pero que explican algunas limitaciones declaradas.

La pregunta útil no es «dame la lista completa de tu cadena» —no te la van a dar, y tampoco sabrías qué hacer con ella—. Son estas tres:

1. **¿Quién más puede ver lo que enviamos?** Con nombre y ubicación.
2. **¿Qué le pasa a nuestro servicio si uno de tus proveedores cae?** Tu disponibilidad depende de eslabones que no controlas.
3. **¿Cómo nos enteramos si cambias uno?** Un cambio de subencargado puede alterar la ubicación de un tratamiento sin que nadie te avise.

Meridiana, con 88 siniestros al día y picos de 600, no tiene margen para descubrir la cadena durante una caída. El resultado de este análisis no es un documento bonito: es una entrada en el registro de riesgos, un runbook de degradación y, si el análisis sale mal, la decisión de tener un segundo proveedor detrás del mismo puerto.

### 23. Auditar a un proveedor de modelo: qué se puede pedir de verdad

Conviene decirlo sin rodeos: **una aseguradora media no va a auditar los pesos de un modelo de un proveedor grande**. No le van a dar acceso, no tendría con qué mirarlos y no cambiaría nada de lo que haría después. Los cuestionarios de auditoría que piden «acceso a los datos de entrenamiento» se contestan con un no educado y queman el poco crédito que tenías.

Lo que sí se puede pedir y se consigue con frecuencia:

- **Informes de terceros y certificaciones** ya existentes, que no obligan al proveedor a abrir nada nuevo.
- **Ficha técnica del modelo y notas de versión**, con historial.
- **Cuestionario de seguridad y de tratamiento de datos**, respondido y firmado por alguien con nombre.
- **Resultados de sus evaluaciones**, o su metodología si los resultados son confidenciales.
- **Compromisos operativos verificables**: disponibilidad, avisos de cambio, plazos de respuesta.

Y lo que es tuyo y no depende de su permiso, que además es lo más valioso: **auditar el comportamiento del modelo desde fuera, con tu propio conjunto de evaluación**. Los 31 casos de Meridiana, ejecutados contra cada versión nueva, dicen más sobre el riesgo real para tu proceso que cualquier certificado genérico. Detectan lo que a ti te importa: que la extracción de lesiones ha empeorado, que el formato de salida cambió, que la latencia se ha doblado.

La regla que resume la slide: **audita el comportamiento, no las tripas**. Las tripas no te las van a enseñar; el comportamiento lo puedes medir tú todas las noches y guardarlo como evidencia fechada.

### 24. Salida del proveedor: portabilidad y plan de contingencia

Un plan de salida no se escribe cuando te quieres ir. Se escribe cuando firmas, porque los motivos para salir suelen ser urgentes: el proveedor sube el precio, deprecia la versión que usas, cambia condiciones de tratamiento de datos, sufre un incidente grave, o tú necesitas mover el tratamiento a otra región.

Lo que hay que tener resuelto de antemano:

- **Un puerto, no una integración.** El `ILlmClient` del curso 3 no era un capricho de arquitectura limpia: es la medida técnica que hace posible el cambio de proveedor. Aquí se convierte además en una medida de cumplimiento defendible ante un auditor.
- **Un conjunto de evaluación que sirva de contrato de calidad.** Los 31 casos de Meridiana permiten comparar candidato y titular con el mismo rasero y decidir con datos, no con impresiones.
- **Tus datos, recuperables.** Prompts, salidas, registros y, si aplica, cualquier artefacto ajustado. En formato utilizable y con plazo comprometido.
- **Un modo degradado probado.** Volver a la cola de los 24 tramitadores es lento, pero es correcto. Lo inaceptable es que el portal deje de aceptar siniestros.
- **Un segundo proveedor evaluado**, aunque no esté en uso. Evaluar bajo presión sale mal.

Lo que hace especial este caso frente a una migración de proveedor cualquiera: **los prompts no son portables del todo**. Un prompt ajustado a una familia de modelos rinde distinto en otra. El puerto abarata el cambio, no lo hace gratis, y el plan de contingencia debe presupuestar reajuste y revalidación, no solo un cambio de credenciales.

### 25. Responsabilidad civil frente a responsabilidad regulatoria

Dos planos distintos que la gente mezcla y que se defienden con documentos diferentes.

- **Responsabilidad regulatoria.** Frente a la autoridad. Nace de incumplir el reglamento. La determina tu rol, no tu contrato, y se materializa en requerimientos, medidas correctoras, retirada del sistema del mercado o sanciones. El **artículo 99, apdo. 1** —en la redacción del Reglamento (UE) 2026/1744— encarga a los Estados miembros establecer «el régimen de sanciones y otras medidas de garantía del cumplimiento, como multas administrativas, advertencias o **medidas no pecuniarias**», efectivas, proporcionadas y disuasorias. Los tramos de multa son tres y se calculan siempre sobre **el volumen de negocios mundial total del ejercicio financiero anterior**, aplicándose la cuantía superior de las dos: hasta 35 000 000 EUR o el **7 %** por incumplir la prohibición de prácticas del art. 5 [apdo. 3]; hasta 15 000 000 EUR o el **3 %** por incumplir, entre otras, las obligaciones de los proveedores del art. 16 [apdo. 4 a)], las de los importadores y distribuidores [c) y d)], las de los apdos. 2 y 4 del art. 25 [d bis)] y **las de los responsables del despliegue del art. 26** [e)]; y hasta 7 500 000 EUR o el **1 %** por facilitar información inexacta, incompleta o engañosa a los organismos notificados o a las autoridades [apdo. 5]. Para las pymes, incluidas las empresas emergentes, se aplica **el menor** de los dos importes, no el mayor [apdo. 6]. Verificado el 31/08/2026.
- **Responsabilidad civil.** Frente a quien sufre un daño: un asegurado al que se le denegó indebidamente una cobertura porque el agente extrajo mal una fecha. Se reclama por la vía de siempre, y aquí el contrato con tu proveedor sí reparte quién paga.

Tres consecuencias prácticas:

1. **Un contrato no te protege del regulador.** Puedes pactar que el integrador te indemnice si te sancionan; no puedes pactar que la sanción vaya dirigida a él.
2. **Sí te protege económicamente.** Y por eso el reparto de la slide 15 se negocia con cifras, límites y seguro, no con buenas intenciones.
3. **La evidencia que sirve es la misma.** Trazas, evals, expediente técnico y registro de decisiones sirven para las dos cosas: para demostrar diligencia ante la autoridad y para reconstruir qué pasó ante una reclamación. Ese es el argumento definitivo de por qué B4 no es burocracia.

La pregunta del caso —«¿cómo se demuestra ante una reclamación por qué se propuso ese importe?»— vive justo en el cruce de los dos planos. Y se contesta con la traza de una ejecución concreta, no con una política.

> Fuentes primarias a abrir: AI Act, capítulo de sanciones y de medidas de las autoridades de vigilancia del mercado; régimen de responsabilidad civil aplicable en España y normativa europea sobre responsabilidad por productos defectuosos vigente en la fecha de publicación.

### 26. Documentar el reparto: la matriz RACI del sistema

El entregable que cierra el bloque es corto y aburrido, y es exactamente lo que salva una inspección: **una tabla con una fila por obligación y un nombre en cada celda**.

| Obligación | Ejecuta | Revisa | Evidencia |
|---|---|---|---|
| Expediente técnico del agente | Ingeniería | Cumplimiento | Repositorio, versionado con el código |
| Conjunto de evaluación y umbrales | Ingeniería | Negocio de siniestros | Informes de CI fechados |
| Supervisión humana efectiva | Jefatura de tramitación | Cumplimiento | Registro de derivaciones revisadas |
| Contrato y vigilancia del proveedor | Compras | Cumplimiento | Contrato y actas de revisión |
| Clasificación y su revisión | Cumplimiento | Comité | Documento de clasificación firmado |

Cuatro reglas para que la tabla valga algo:

- **Nombres de persona, no de departamento.** «Cumplimiento» no revisa nada; lo revisa alguien.
- **Una columna de evidencia, siempre.** Una obligación sin evidencia asociada es una intención.
- **Ejecutor y revisor distintos.** Si coinciden, no hay revisión, hay una firma.
- **Una fecha de revisión.** Una matriz sin fecha de caducidad envejece en silencio.

Esta matriz es la costura entre este bloque y B6: aquí se decide quién responde de qué; allí se monta el proceso que hace que alguien lo compruebe con periodicidad. Y es también la primera cosa que pide un auditor cuando quiere saber si la gobernanza existe o solo está escrita.

### 27. Ejercicio práctico 1: separar los dos roles de Meridiana {ejercicio:B3-ej1}

Sobre el flujo de siniestros del caso, produce el mapa de roles de la slide 17 con **una fila por componente**: el modelo de propósito general, el agente de FNOL y triaje, el agente de propuesta de resolución, y cualquier herramienta de terceros conectada al flujo.

Criterio único de corrección: **el modelo y el sistema tienen que aparecer en filas distintas, con roles distintos**. Si tu mapa tiene una sola fila para «la IA de Meridiana», el ejercicio no está hecho, porque justo esa distinción es la que decide quién escribe el expediente técnico.

Marca como hueco lo que no sepas. Un mapa con celdas pendientes es un mapa honesto; uno completo a base de suposiciones es una trampa para el que lo lea dentro de un año.

### 28. Ejercicio práctico 2: cuatro cambios, ¿cambian el rol? {ejercicio:B3-ej2}

Para cada uno de estos cambios sobre el sistema de Meridiana, decide si cruza alguna de las tres puertas de la slide 6, cuál, y qué habría que hacer antes de desplegarlo:

1. Rediseñar la pantalla del tramitador sin tocar la lógica.
2. Reescribir el prompt de extracción de FNOL para mejorar la detección de matrículas.
3. Subir el umbral de aprobación con un clic de 1.500 € a 6.000 €.
4. Conectar la herramienta documental de un tercero —contratada para resumir adjuntos— a la decisión de vía del triaje.
5. Rotular la interfaz del proveedor documental como «Asistente Meridiana».

Escribe **una línea de justificación por caso** y, cuando la respuesta sea que sí cruza, qué documento habría que producir antes.

No busques la respuesta normativa exacta: lo que se evalúa es el razonamiento sobre finalidad, control y perfil de riesgo. Los dos últimos casos son los que separan a quien ha entendido el bloque de quien ha memorizado la lista.

### 29. Mini-quiz de comprensión — B3 {quiz:B3}

Tres preguntas sobre lo que decide este bloque: qué convierte a un responsable del despliegue en proveedor, qué no se puede trasladar por contrato y qué rol ocupa Meridiana respecto a cada cosa.

Ninguna pregunta depende de un número de artículo ni de una fecha. Aprobado con dos aciertos. Puedes repetirlo las veces que quieras.

## Qué te llevas

- Se puede pasar de deployer a proveedor sin proponérselo.
- Lo que no está en el contrato con el proveedor del modelo, no lo tienes.
- El reparto de responsabilidades se escribe antes, no durante la inspección.

## Mini-quiz (3 preguntas)

Una sola opción correcta por pregunta, con explicación. Se generan con el mismo formato que `assets/js/quizzes-data.js` para que el importador las recoja.

1. **Tema:** Qué convierte a un deployer en proveedor
   - **Enunciado:** Meridiana contrata una herramienta de un tercero cuya finalidad declarada es resumir documentos adjuntos para lectura humana. Seis meses después conecta su salida a la decisión de vía del triaje y rotula la pantalla como «Asistente Meridiana». ¿Qué ha ocurrido con su rol?
   - **Opciones:**
     - a) Nada: sigue siendo responsable del despliegue porque no ha tocado el código del tercero.
     - b) **Ha cruzado dos de las puertas que la hacen asumir el rol de proveedor: ha cambiado la finalidad prevista y ha puesto su marca sobre un sistema ajeno.** ✅
     - c) Nada, porque el contrato firmado con el tercero dice que él es el proveedor a todos los efectos.
     - d) Pasa a ser distribuidora, porque está poniendo el sistema a disposición de sus tramitadores.
   - **Explicación:** El rol se determina por hechos —qué se hace con el sistema y bajo qué nombre se presenta—, no por lo que diga un contrato ni por si se ha modificado el código. Usar el sistema fuera de la finalidad que su fabricante declaró y rotularlo con marca propia son dos supuestos independientes, y basta con uno. La (a) confunde modificar con reutilizar. La (c) confunde reparto civil de responsabilidades con determinación del rol frente al regulador. La (d) inventa un rol: distribuir es comercializar hacia fuera, no desplegar internamente.

2. **Tema:** Qué obligaciones no se pueden trasladar por contrato
   - **Enunciado:** El integrador propone una cláusula: «El proveedor del modelo asume todas las obligaciones regulatorias derivadas del sistema». ¿Qué efecto tiene esa cláusula?
   - **Opciones:**
     - a) Traslada las obligaciones, siempre que el proveedor del modelo la firme expresamente.
     - b) No tiene ningún efecto de ninguna clase, porque los contratos no afectan al cumplimiento.
     - c) **No cambia quién responde ante la autoridad, pero sí puede repartir el trabajo y el coste económico entre las partes.** ✅
     - d) Traslada las obligaciones de documentación pero no las de supervisión humana.
   - **Explicación:** El rol frente al regulador se determina por criterios objetivos —quién diseña, quién pone en servicio, bajo qué nombre— y no es disponible por pacto. Lo que un contrato sí hace, y es valioso, es asignar quién ejecuta cada tarea, con qué plazos y quién paga si un incumplimiento ajeno te cuesta dinero. La (a) y la (d) suponen que las obligaciones son negociables. La (b) se pasa al otro extremo: un buen contrato es exactamente lo que evita el punto ciego en el que nadie asume la evaluación de conformidad.

3. **Tema:** Qué rol ocupa Meridiana y respecto a qué
   - **Enunciado:** Meridiana consume por API un modelo de propósito general de un tercero y construye con él un agente de siniestros que despliega para sus 24 tramitadores. No lo vende a nadie. ¿Cómo se describe correctamente su situación?
   - **Opciones:**
     - a) Solo responsable del despliegue, porque no ha entrenado ningún modelo y no vende el agente.
     - b) Solo proveedora, porque cualquiera que use IA en un proceso regulado lo es.
     - c) **Responsable del despliegue respecto al modelo del tercero, y proveedora respecto al agente que ella misma pone en servicio bajo su nombre.** ✅
     - d) Proveedora del modelo, porque los prompts y la recuperación de contexto que ha escrito lo modifican.
   - **Explicación:** Son dos objetos regulados distintos y hay que nombrarlos por separado. Sobre el modelo, Meridiana es cliente: no lo diseñó ni puede documentarlo por dentro. Sobre su agente —prompts, reglas de triaje, tools con efectos sobre expedientes reales— es fabricante, y el rol se gana por poner en servicio, no por facturar. La (a) confunde no entrenar con no construir, y vender con poner en servicio. La (b) borra la distinción entre las dos capas. La (d) confunde usar un modelo con modificarlo: prompting y recuperación de contexto dejan el modelo intacto.

## Lab

Este curso no lleva labs de código. En su lugar, ejercicio de plantilla (DOCX/XLSX) sobre el caso Meridiana.

**Enunciado.** Produce el **mapa de roles y matriz de reparto** de Meridiana en la plantilla `content/caso/plantillas/B3-mapa-de-roles.xlsx`. El entregable son dos hojas: `Roles`, con una fila por componente de IA del flujo de siniestros, y `Reparto`, con una fila por obligación y su evidencia asociada.

**Pasos:**

1. **Inventaría los componentes.** Modelo de propósito general, agente de FNOL y triaje, agente de propuesta de resolución, herramientas de terceros conectadas al flujo. Una fila por componente; nunca una fila para «la IA».
2. **Asigna rol a cada fila** —proveedor, responsable del despliegue o ambos— y escribe en la columna contigua **el hecho** que lo justifica: quién lo diseñó, bajo qué nombre se pone en servicio.
3. **Marca las puertas cruzadas.** Para cada componente de terceros: marca, modificación sustancial, cambio de finalidad. Sí o no, con una línea de motivo.
4. **Rellena la hoja `Reparto`** con las obligaciones de la slide 26, un nombre de persona por celda y una evidencia concreta y localizable por fila.
5. **Marca los huecos.** Toda celda que no sepas rellenar se marca como pendiente con un responsable y una fecha. No se rellena a ojo.
6. **Revisión cruzada.** Que otra persona lea el mapa y señale las filas donde el rol y el hecho que lo justifica no encajan.

**Criterios de aceptación:**

- El modelo de terceros y el sistema propio están en **filas distintas y con roles distintos**. Es el criterio que más se falla.
- Ninguna celda de rol está justificada por una cláusula contractual: la justificación es siempre un hecho sobre diseño, puesta en servicio o marca.
- Toda obligación de la hoja `Reparto` tiene **nombre de persona y evidencia**, o está marcada como hueco con responsable y fecha.
- Ejecutor y revisor son personas distintas en todas las filas.
- Cada afirmación normativa del documento lleva su cita o queda marcada como pendiente. Un mapa con huecos reconocidos es válido; uno con afirmaciones sin fuente, no.

**Solución de referencia:** en `content/caso/soluciones/B3/`, con el mapa completo del caso y las tres puertas resueltas para cada componente.

## Cierre

- Recapitulación en los tres puntos de arriba.
- Mini-quiz.
- Enlace al siguiente bloque.
