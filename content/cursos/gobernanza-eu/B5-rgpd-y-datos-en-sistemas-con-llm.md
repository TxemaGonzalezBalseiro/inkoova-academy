# C-13 · B5 · RGPD y datos en sistemas con LLM

> Curso: `gobernanza-eu` · bloque `B5`

## Objetivo

Resolver las preguntas de protección de datos que aparecen cuando el tratamiento pasa por un modelo.

## ⚠ Bloque normativo: verificación obligatoria antes de publicar

Cada afirmación de este bloque debe citar artículo y fecha de verificación, y esa fecha debe salir de haber abierto la fuente primaria. No se redacta de memoria ni a partir de resúmenes de terceros.

**Fuentes primarias a consultar:**

- [x] Reglamento (UE) 2016/679 (RGPD), artículos 4.5, 5, 6, 9, 12, 13, 14, 15, 17, 18, 22, 28, 30, 33, 34 y 35, y considerando 26 — abiertos y leídos el 31/08/2026
- [ ] RGPD, artículos 44 y siguientes (transferencias internacionales) — **no abiertos**: EUR-Lex trunca el documento hacia el artículo 40
- [x] Reglamento (UE) 2024/1689 (AI Act), texto consolidado tras el Reglamento (UE) 2026/1744, artículos 12 y 19
- [x] Ley 50/1980, de Contrato de Seguro, artículo 23 (BOE)
- [ ] **Parcial.** AEPD: confirmada la existencia de la lista de tratamientos sujetos a EIPD (art. 35.4); su contenido **no** se ha podido leer
- [ ] **Parcial.** CEPD: identificada la directriz aplicable (WP251rev.01, refrendada por el CEPD); su texto **no** se ha leído

**Fecha de verificación:** 31/08/2026 (verificación **parcial**: el capítulo V del RGPD —transferencias
internacionales— no se pudo abrir; el texto de EUR-Lex se trunca hacia el artículo 40. Los marcadores
que siguen abiertos en este bloque llevan escrito su motivo. Registro completo en `VERIFICADO.md`.)

> Aviso al alumno que debe aparecer en el bloque: este material es formativo y no constituye asesoramiento jurídico.

## Guion de slides

28 slides de contenido. Una idea por slide, con un ejemplo real o del caso Meridiana. Nada de relleno.

### 1. Qué cambia y qué no cuando hay un LLM de por medio

Casi nada de lo que ya sabes de protección de datos deja de valer porque haya un modelo en medio. Meridiana sigue siendo la responsable del tratamiento, los principios siguen siendo los mismos y el asegurado conserva exactamente los mismos derechos que tenía antes. Quien te diga que «la IA lo cambia todo» te está vendiendo un proyecto.

Lo que sí cambia es **dónde acaban los datos y en qué forma**.

- Antes, el relato del siniestro vivía en un campo de texto de una base de datos y lo leían veinticuatro tramitadores. Ahora ese mismo relato se copia a un prompt, sale hacia un tercero, vuelve como extracción estructurada, se guarda en una traza y puede acabar dentro de un conjunto de evaluación.
- Antes, «¿dónde están los datos del señor Ortiz?» tenía una respuesta: su expediente. Ahora tiene cinco, y tres de ellas no las ha inventariado nadie.
- Antes, el tratamiento era estructurado: campos con nombre y tipo. Ahora hay un tramo de texto libre que puede contener cualquier cosa, incluidas categorías de datos que nadie pidió.

Ninguna de esas tres es una pregunta nueva del derecho. Son las preguntas de siempre —qué datos, para qué, dónde, cuánto tiempo, quién los ve— aplicadas a copias que antes no existían. La dificultad de este bloque no es jurídica: es que el sistema técnico creó ubicaciones de datos sin que nadie las apuntara en ningún registro.

Ese desajuste entre el mapa y el territorio es lo que vamos a cerrar.

### 2. Base jurídica del tratamiento en Meridiana

Un error frecuente: pensar que meter un LLM en el flujo obliga a buscar una base jurídica nueva. No es así. Meridiana ya trataba el relato del siniestro para tramitarlo, y esa base sigue siendo la misma cuando parte del trabajo lo hace un modelo. El **artículo 6, apartado 1** del RGPD tasa las bases jurídicas en seis, y solo seis (verificado 31/08/2026): a) consentimiento del interesado para uno o varios fines específicos; b) tratamiento necesario para la ejecución de un contrato en el que el interesado es parte o para medidas precontractuales a petición de este; c) necesario para el cumplimiento de una obligación legal aplicable al responsable; d) necesario para proteger intereses vitales; e) necesario para una misión realizada en interés público o en el ejercicio de poderes públicos; f) necesario para la satisfacción de intereses legítimos del responsable o de un tercero, «siempre que sobre dichos intereses no prevalezcan los intereses o los derechos y libertades fundamentales del interesado». Cuál encaja aquí depende de la finalidad concreta, no de la tecnología.

Lo que sí conviene entender es que **una base cubre una finalidad, no un sistema**. En Meridiana conviven al menos cuatro finalidades distintas sobre los mismos datos, y no todas se apoyan en lo mismo:

1. Tramitar el siniestro y pagar lo que corresponda.
2. Detectar posibles fraudes.
3. Guardar trazas de lo que hizo el agente, para poder responder a una reclamación.
4. Reutilizar casos reales como conjunto de evaluación del sistema.

Las dos primeras son negocio de toda la vida. La tercera es defensiva y probablemente se sostenga bien. **La cuarta es la que casi nunca se piensa**: coger cien FNOL reales, guardarlos aparte y usarlos para medir si un cambio de modelo empeora la extracción es un tratamiento con finalidad propia, distinta de tramitar el siniestro de esas cien personas.

Si esa cuarta finalidad no aparece en ningún sitio, tienes un tratamiento sin base declarada funcionando en tu CI.

> Fuentes primarias a abrir: RGPD, capítulo de principios y de licitud del tratamiento (texto consolidado en EUR-Lex); guía de la AEPD sobre bases jurídicas; directrices del CEPD sobre tratamiento ulterior compatible.

### 3. Minimización: lo que no se envía al modelo

Minimizar es fácil de decir y raro de ver, porque el camino cómodo es meter en el contexto todo lo que se tenga a mano «por si acaso ayuda». En Meridiana ese «por si acaso» es concreto: el prompt de extracción arrastra el histórico de siniestros del asegurado, sus datos de contacto completos, el NIF, el IBAN de domiciliación y la póliza entera en 4 KB.

Pregunta útil, tarea por tarea: **¿qué campo, si lo quito, empeora la salida de forma medible?** Ese es el criterio, y es comprobable con los evals del curso 3. No es una opinión jurídica: es un experimento.

Aplicado al FNOL:

| Dato | ¿Va al modelo? | Por qué |
|---|---|---|
| Relato en texto libre | Sí | Es la tarea |
| Fecha y lugar declarados | Sí | Se cruzan con el relato |
| Coberturas de la póliza | Resumidas | Solo tres campos deciden la vía |
| Nombre, NIF, IBAN | No | El modelo no extrae nada de ellos |
| Histórico de siniestros | No | Es entrada del triaje, no del extractor |

El IBAN es el ejemplo canónico: llega al prompt porque venía en el mismo objeto de póliza que se serializó entero, no porque nadie decidiera enviarlo. Nadie lo decidió; simplemente estaba en la estructura.

La minimización en un sistema con LLM se implementa en un sitio muy preciso: **la función que construye el contexto**. Si esa función recibe entidades de dominio completas y las vuelca, no hay política que lo arregle. Si recibe un DTO estrecho con los campos que la tarea necesita, la minimización está garantizada por el tipo y se revisa en un pull request.

### 4. Seudonimización antes de la llamada

Seudonimizar en un sistema con LLM significa sustituir los identificadores directos por referencias antes de construir el prompt, y volver a colocarlos al recibir la respuesta. Suena a truco y es una medida técnica seria, pero solo funciona si entiendes qué compra y qué no.

Un flujo realista en Meridiana:

```python
# El relato entra en crudo; sale con referencias.
relato, mapa = seudonimizar(relato_original)
# mapa: {"[PERSONA_1]": "Marta Ortiz", "[MATRICULA_1]": "1234 ABC"}

extraccion = await llm.extraer_fnol(relato)          # el proveedor ve [PERSONA_1]
extraccion = rehidratar(extraccion, mapa)            # el expediente ve Marta Ortiz
```

Lo que compras: el proveedor deja de recibir nombres, matrículas y números de póliza. La traza que guardas puede quedarse en la versión seudonimizada y el mapa vivir cifrado y aparte, con su propio control de acceso.

Lo que **no** compras, y hay que decirlo sin adornos:

- **Los datos seudonimizados siguen siendo datos personales.** No sales del RGPD. El **artículo 4, punto 5** define seudonimización como «el tratamiento de datos personales de manera tal que ya no puedan atribuirse a un interesado sin utilizar información adicional, siempre que dicha información adicional figure por separado y esté sujeta a medidas técnicas y organizativas» destinadas a impedir esa atribución. Y el **considerando 26** lo cierra sin ambigüedad: «Los datos personales seudonimizados, que cabría atribuir a una persona física mediante la utilización de información adicional, deben considerarse información sobre una persona física identificable» (verificado 31/08/2026).
- **El texto libre se reidentifica solo.** «Choqué contra la furgoneta de mi cuñado delante de su taller de Alcorcón el día de mi cumpleaños» no tiene un solo identificador directo y señala a una persona concreta. Ningún detector de entidades lo tapa.
- **Las lesiones se quedan.** Lo que estás tratando con más cuidado —el dato de salud— es justo lo que la tarea necesita y por tanto lo que no se puede quitar.

Seudonimizar reduce el daño de una fuga. No convierte el tratamiento en otra cosa.

> Fuentes primarias a abrir: RGPD, definiciones y artículos sobre medidas técnicas y organizativas; considerandos sobre reidentificación; guía de la AEPD sobre seudonimización y anonimización.

### 5. El prompt como tratamiento: implicaciones

Aquí está el cambio de mentalidad del bloque. **Un prompt no es una llamada a una API: es una operación de tratamiento de datos personales**, con su finalidad, su base, su destinatario y su plazo. Y como la escribe un ingeniero en un fichero de plantillas, casi nunca llega al inventario de nadie.

Las consecuencias son muy concretas:

- **Cada plantilla de prompt define qué categorías de datos salen del sistema.** Cambiar `{{poliza.resumen}}` por `{{poliza}}` en una plantilla es, jurídicamente, ampliar el tratamiento. Técnicamente son doce caracteres en un diff que nadie revisa con esa lente.
- **La plantilla es la documentación.** Si quieres saber qué datos personales van al proveedor, el sitio donde está escrito con precisión no es el registro de actividades: es el repositorio.
- **El prompt tiene versiones, y el tratamiento también.** La plantilla v4 mandaba el histórico; la v5 dejó de mandarlo. Las trazas de enero contienen datos que las de marzo no. Cualquier respuesta a un interesado que ignore esto será incorrecta para una parte del periodo.

De ahí una práctica que vale más que tres políticas: **las plantillas de prompt viven versionadas en el repositorio, con un test que comprueba qué campos pueden aparecer en el contexto renderizado**. En Meridiana ese test es literal: se renderiza la plantilla con un expediente de prueba y se comprueba que la cadena resultante no contiene el IBAN ni el NIF sintéticos. Falla en CI, no en una inspección.

El prompt es el punto exacto donde una decisión de arquitectura se convierte en una decisión de protección de datos, y por eso se revisa en el pull request.

### 6. El proveedor del modelo como encargado

Cuando Meridiana manda el relato de un siniestro a un proveedor de modelo, ese proveedor trata datos personales por cuenta de Meridiana. La figura que le corresponde en el RGPD es la de encargado del tratamiento, y la regula el **artículo 28** (verificado 31/08/2026). Su apartado 1 obliga al responsable a elegir «únicamente un encargado que ofrezca garantías suficientes para aplicar medidas técnicas y organizativas apropiadas». Su apartado 3 exige un contrato u otro acto jurídico que fije «el objeto, la duración, la naturaleza y la finalidad del tratamiento, el tipo de datos personales y categorías de interesados, y las obligaciones y derechos del responsable», y que estipule ocho compromisos del encargado: a) tratar solo siguiendo instrucciones documentadas del responsable; b) confidencialidad de las personas autorizadas; c) medidas de seguridad del artículo 32; d) condiciones para subcontratar (apartados 2 y 4); e) asistencia al responsable ante los derechos de los interesados; f) ayuda en el cumplimiento de los artículos 32 a 36; g) supresión o devolución de los datos al final, a elección del responsable; h) puesta a disposición de la información necesaria para demostrar el cumplimiento y permitir auditorías.

Lo que importa entender aquí es **por qué esto se escapa tan a menudo**. Un encargado clásico —la empresa de hosting, la gestoría de nóminas— se contrata con un procedimiento en el que participa alguien de cumplimiento. Un proveedor de modelo se contrata con una tarjeta de crédito, en cinco minutos, desde un portátil, porque el ingeniero necesitaba una clave de API para probar una idea. Ese día quedó formalizada una transferencia de datos que nadie evaluó.

Tres consecuencias prácticas:

- **El «modo de prueba» no existe jurídicamente.** Si el relato que mandaste para probar era real, fue un tratamiento real. La frase «solo estaba probando» describe tu intención, no la operación.
- **Cada cambio de proveedor es un cambio de encargado.** Migrar de un modelo a otro por precio o por calidad no es una decisión solo técnica: cambia el destinatario de los datos.
- **La responsabilidad no se transfiere.** El proveedor responde de lo suyo; frente al asegurado responde Meridiana. Que el proveedor tenga certificaciones impresionantes no traslada la responsabilidad, la hace más defendible.

> Fuentes primarias a abrir: RGPD, artículos sobre encargado del tratamiento y sobre responsabilidad; cláusulas contractuales tipo aprobadas por la Comisión; guía de la AEPD sobre contratos de encargo.

### 7. Transferencias internacionales y sus garantías

La pregunta operativa es simple de formular y sorprendentemente difícil de responder con precisión: **¿en qué territorio se procesa cada llamada al modelo?** Y la respuesta no puede ser «el proveedor es europeo» ni «la web dice que hay región europea». Tiene que salir de la documentación contractual del proveedor y de la configuración concreta de tu cuenta.

Si hay salida de datos fuera del Espacio Económico Europeo, el RGPD exige una garantía adecuada, y las vías posibles están tasadas. **Cuáles son esas vías, en qué artículos están y qué decisiones de adecuación siguen vigentes no se enumeran aquí**: es el apartado del reglamento que más ha cambiado en los últimos años y el que peor envejece en cualquier curso. Se leen en el capítulo V del RGPD y en la lista de decisiones de adecuación de la Comisión, en la fecha en que se necesiten.

> **Marcador abierto a 31/08/2026 — motivo:** el capítulo V del RGPD (artículos 44 y siguientes) no se ha podido leer. El texto consolidado de EUR-Lex se trunca hacia el artículo 40 y la versión original se trunca en los considerandos, así que ni el artículo 44 ni el 45, 46 o 49 llegaron a abrirse. No se escribe ninguna vía de transferencia hasta poder leerlas.

Lo específico de un sistema con LLM, que es donde este bloque aporta algo:

- **La inferencia y el almacenamiento pueden estar en sitios distintos.** Que el modelo se ejecute en una región europea no dice nada sobre dónde se guardan los registros del proveedor, ni sobre desde dónde accede su equipo de soporte.
- **El «fallback» silencioso.** Muchos proveedores desvían tráfico a otra región cuando la tuya está saturada. Si eso no está desactivado explícitamente por contrato o configuración, tu transferencia depende de la carga de un centro de datos.
- **El acceso remoto también es transferencia.** Un ingeniero de soporte fuera del EEE mirando una traza que contiene el relato de un accidente es un flujo de datos, aunque el disco esté en Fráncfort.

Esta slide se resuelve pidiendo tres documentos, no leyendo una página de marketing.

> Fuentes primarias a abrir: RGPD, capítulo de transferencias internacionales; decisiones de adecuación publicadas por la Comisión Europea; documentación contractual y de subencargados del proveedor de modelo.

### 8. Retención de prompts y salidas por parte del proveedor

Casi todos los proveedores de modelos guardan durante algún tiempo lo que les envías y lo que te devuelven, normalmente por motivos de abuso y seguridad. Los plazos, las excepciones y las opciones de desactivación varían por proveedor, por plan contratado y por región, y cambian con frecuencia. Los plazos y condiciones concretos **dependen del proveedor, del plan y de la región**, así que este material no da ninguno: se leen en la documentación contractual del proveedor que use Meridiana, en la fecha en que se firme, y no en un blog.

> **Marcador abierto a 31/08/2026 — motivo:** no es un dato normativo y no tiene fuente primaria pública. Depende del contrato, del plan y de la región de cada proveedor, y solo se cierra abriendo la adenda firmada. Este marcador debe seguir abierto en el material: es la instrucción, no una omisión.

Lo que sí puedes fijar sin consultar a nadie es **cómo se comporta tu sistema respecto a esa retención**, y eso es una decisión tuya:

- **Es una decisión, no un hecho consumado.** Si el plan que has contratado retiene y existe uno que no lo hace, elegiste retener. Esa elección debe estar escrita y firmada por alguien, no heredada del valor por defecto de una consola web.
- **Multiplica el radio de una brecha.** Si el proveedor sufre un incidente, lo que se expone no es la llamada de hoy: son las de la ventana de retención. Con 32.000 siniestros al año, la diferencia entre unos días y unas semanas de ventana es un orden de magnitud en el número de personas afectadas.
- **La retención del proveedor es opaca para tus derechos.** Cuando un asegurado pida supresión, tú puedes borrar tus tablas. Lo que el proveedor conserva depende del contrato y de sus mecanismos, y tienes que poder describirlo con exactitud en tu respuesta.
- **Cambia sola.** Un cambio de plan o de condiciones del proveedor modifica tu tratamiento sin que se despliegue una sola línea de código tuyo.

> Fuentes primarias a abrir: condiciones de servicio y adenda de tratamiento de datos del proveedor contratado; su documentación de retención y de *zero data retention*, con fecha de consulta; el contrato firmado y sus anexos.

### 9. Qué preguntar a un proveedor antes de firmar

Esta slide es una lista y se usa como lista. Ninguna pregunta admite «sí» como respuesta: todas piden un documento o una configuración concreta.

1. **¿Qué se retiene, dónde y durante cuánto?** De entrada y de salida, por separado. Con la referencia documental exacta.
2. **¿Se puede desactivar la retención, en qué plan y con qué efecto sobre las funciones?** A veces se paga con perder el historial de la consola.
3. **¿Se usan mis datos para entrenar o mejorar modelos?** Y si la respuesta es «no por defecto», dónde está escrito y qué lo activa.
4. **¿Quién más los ve?** Lista de subencargados, con territorio y función. Y cómo te avisan cuando esa lista cambia.
5. **¿En qué región se ejecuta la inferencia y qué pasa en caso de sobrecarga?** El desvío automático a otra región es la respuesta que buscas.
6. **¿Qué compromisos hay ante una brecha?** No el plazo genérico: quién te avisa, por qué canal, con qué información mínima y a qué contacto tuyo.
7. **¿Qué puedo auditar de verdad?** Informes de auditoría de terceros, certificaciones vigentes, derecho a inspección y sus límites reales.
8. **¿Qué pasa cuando me voy?** Borrado a la terminación, plazo y evidencia de que ocurrió.

Guarda las respuestas con **fecha y captura del documento**. Dentro de dieciocho meses la página web dirá otra cosa, y lo que tendrás que demostrar es qué decía el día que decidiste enviar el primer relato de un accidente.

Si el proveedor no responde por escrito a la 1, la 3 y la 4, no tienes un encargado: tienes una dependencia.

### 10. Decisiones automatizadas: el artículo 22 y su alcance

El **artículo 22 del RGPD** dice, literalmente en su apartado 1 (verificado 31/08/2026): «Todo interesado tendrá derecho a no ser objeto de una decisión basada únicamente en el tratamiento automatizado, incluida la elaboración de perfiles, que produzca efectos jurídicos en él o le afecte significativamente de modo similar.»

Sus otros tres apartados completan la regla:

- **22.2 — las tres excepciones**, y son solo tres: que la decisión a) sea necesaria para la celebración o ejecución de un contrato entre el interesado y el responsable; b) esté autorizada por el Derecho de la Unión o de los Estados miembros, que debe establecer además medidas adecuadas de salvaguardia; o c) se base en el consentimiento explícito del interesado.
- **22.3 — las garantías**, exigibles en los supuestos a) y c): «como mínimo el derecho a obtener intervención humana por parte del responsable, a expresar su punto de vista y a impugnar la decisión».
- **22.4 — el cerrojo de las categorías especiales**: esas decisiones no se basarán en datos del artículo 9.1 salvo que se aplique el artículo 9.2, letras a) o g), y con medidas adecuadas de salvaguardia.

Ojo con el rango de lo que viene después. La interpretación de «únicamente» y de «efecto significativo» no está en el reglamento: está en una **directriz**, las *Guidelines on Automated individual decision-making and Profiling for the purposes of Regulation 2016/679*, **WP251rev.01**, del Grupo de Trabajo del Artículo 29, refrendadas por el CEPD y listadas como tales en su web (verificado 31/08/2026). Una directriz orienta e interpreta; no es el reglamento y no se cita como si lo fuera. De la WP251rev.01 se ha confirmado **cuál es y que sigue refrendada**, pero no se ha leído su texto: por eso aquí no se reproduce ni se resume su contenido.

Lo que aporta esta slide es el **razonamiento que se usa para aplicarlo**, y que no depende del texto exacto:

- La pregunta no es «¿hay IA?». Es **si una persona interviene de verdad en la decisión concreta**, no en el diseño del sistema.
- «Intervenir de verdad» significa que esa persona tiene información suficiente, tiempo real para valorarla, autoridad para decidir lo contrario y consecuencias asumibles si lo hace. Un tramitador con 88 expedientes al día y un botón de «aprobar» preseleccionado cumple la forma y no la sustancia.
- El efecto se mide **sobre el interesado**, no sobre tu proceso. Que para ti sea «una propuesta interna» no cambia nada si el resultado que recibe el asegurado es que le pagan menos, más tarde o nada.

Este es el punto donde el AI Act (B2) y el RGPD hacen la misma pregunta desde ángulos distintos: allí decidía la clasificación de riesgo, aquí decide si hace falta una garantía adicional. La respuesta debería ser la misma en los dos documentos, y sorprendentemente a menudo no lo es.

> Fuentes primarias a abrir: RGPD, artículo 22 y considerandos relacionados; directrices del CEPD sobre decisiones automatizadas y elaboración de perfiles; guía de la AEPD sobre decisiones automatizadas.

### 11. Cuándo la propuesta de Meridiana entra en el artículo 22

Apliquemos el razonamiento anterior al caso, que es donde deja de ser cómodo. El agente de Meridiana propone importe y motivación; un humano aprueba siempre. Sobre el papel, no hay decisión únicamente automatizada. En la práctica hay tres configuraciones y solo una aguanta el análisis.

- **Por debajo del umbral de 1.500 €, la aprobación es un clic.** El tramitador ve el importe propuesto, el botón está preseleccionado y su indicador de productividad premia el volumen. Materialmente, quien decide es el sistema. Aquí es donde hay que mirar con lupa.
- **Por encima del umbral, hay revisión completa.** El tramitador abre el expediente, contrasta el parte y puede cambiar el importe. Esa intervención sí es sustantiva.
- **La derivación por lesiones.** El sistema no decide nada: marca y aparta. No hay efecto sobre el interesado más allá de que su expediente lo vea una persona antes.

La conclusión honesta del caso: **el tramo de bajo importe es el que puede caer dentro del ámbito del artículo 22**, precisamente el que se diseñó para no molestar a nadie. Y hay una métrica que lo delata sin necesidad de abogados: **el porcentaje de propuestas que el humano modifica**. Si en el tramo alto es del 18 % y en el bajo del 0,4 %, la supervisión del tramo bajo es un trámite y tu documentación dice otra cosa.

Las **garantías** que exigiría el reglamento en ese supuesto sí están cerradas: el artículo 22.3 fija «como mínimo el derecho a obtener intervención humana por parte del responsable, a expresar su punto de vista y a impugnar la decisión» (verificado 31/08/2026). Y como el relato lleva datos de salud, entra además el artículo 22.4, que prohíbe basar esas decisiones en categorías especiales salvo por las vías del artículo 9.2, letras a) o g).

Leer el tramo bajo como decisión «basada únicamente» en tratamiento automatizado es **una calificación interpretativa del caso, no la lectura de un artículo**, y depende del contenido de la WP251rev.01, que no se ha abierto. Se propone como hipótesis de trabajo, no como conclusión jurídica.

> Fuentes primarias a abrir: RGPD, artículo 22 y sus excepciones; directrices del CEPD sobre intervención humana significativa; criterios de la AEPD sobre supervisión efectiva.

### 12. Derecho a explicación: qué se puede dar realmente

Cuando el asegurado pregunta «¿por qué me proponen 900 € y no 1.400 €?», hay dos respuestas posibles y solo una es honesta.

La respuesta que no sirve: «lo calculó un modelo de lenguaje entrenado con miles de millones de parámetros». Es cierta, es inútil y suena a excusa.

La respuesta que sirve se construye **antes**, y consiste en que la parte explicable de la decisión no la tomó el modelo. En Meridiana, la cadena real es:

1. Del relato se extrajeron estos campos: fecha, vía de tramitación, daños declarados. *El modelo hizo esto.*
2. La póliza tiene estas coberturas y esta franquicia. *Lectura determinista del contrato.*
3. Se aplicó la vía de daños propios, porque el contrario no está identificado. *Regla en código, con su test.*
4. El importe sale del baremo con estos parámetros. *Cálculo.*
5. La tramitadora Elena Ruiz lo revisó y aprobó el 14 de marzo. *Acto humano registrado.*

De los cinco pasos, el modelo interviene en uno, y en ese uno lo que hizo es **transcribir información que el propio asegurado escribió**. Eso sí se puede explicar: «de tu relato entendimos que el golpe fue por detrás y que no había heridos; si eso no es correcto, dínoslo y se revisa».

Ahí está el argumento completo del programa: la explicabilidad no se obtiene interpretando el modelo, **se obtiene reduciendo su cuota de decisión**. Un sistema donde el modelo solo extrae y redacta es explicable por construcción. Uno donde el modelo decide el importe no lo es, y ninguna herramienta de interpretabilidad te va a salvar en una reclamación.

Qué información exige el reglamento en estos casos, y bajo qué artículos (verificado 31/08/2026): el **artículo 13.2, letra f)** obliga a informar, al recoger los datos del propio interesado, de «la existencia de decisiones automatizadas, incluida la elaboración de perfiles, a que se refiere el artículo 22». Y el **artículo 15.1, letra h)** da al interesado, cuando ejerce su derecho de acceso, «la existencia de decisiones automatizadas, incluida la elaboración de perfiles, a que se refiere el artículo 22, apartados 1 y 4, y, al menos en tales casos, información significativa sobre la lógica aplicada, así como la importancia y las consecuencias previstas de dicho tratamiento».

Fíjate en la formulación exacta: el reglamento pide **información significativa sobre la lógica aplicada**, no el modelo ni sus pesos. Los cinco pasos de arriba son precisamente eso.

> Fuentes primarias a abrir: RGPD, artículos sobre información al interesado y sobre decisiones automatizadas; directrices del CEPD sobre transparencia; guía de la AEPD sobre información y explicabilidad.

### 13. Derechos del interesado cuando los datos pasaron por un modelo

Los derechos son los de siempre, y estos son sus artículos y sus títulos oficiales (verificado 31/08/2026): **artículo 15**, «Derecho de acceso del interesado»; **artículo 16**, «Derecho de rectificación»; **artículo 17**, «Derecho de supresión ("el derecho al olvido")»; **artículo 18**, «Derecho a la limitación del tratamiento»; **artículo 20**, «Derecho a la portabilidad de los datos»; **artículo 21**, «Derecho de oposición».

El plazo lo fija el **artículo 12, apartado 3**: la información se facilita «sin dilación indebida y, en cualquier caso, en el plazo de un mes a partir de la recepción de la solicitud», prorrogable «otros dos meses en caso necesario, teniendo en cuenta la complejidad y el número de solicitudes», informando al interesado de la prórroga y de sus motivos dentro del primer mes. Un mes de partida, tres como máximo, y la prórroga hay que avisarla: no es un silencio que se estira solo.

Lo que este bloque aporta es que **cada derecho se rompe en un sitio distinto cuando hay un LLM en medio**:

- **Acceso.** El asegurado pide «todos mis datos». ¿Incluye eso los prompts que se construyeron con su relato? ¿Las trazas de las cinco ejecuciones? ¿La extracción que el modelo devolvió y que resultó estar mal? Si tu respuesta solo cubre las tablas del expediente, es incompleta, y lo será de forma demostrable el día que alguien reclame.
- **Rectificación.** El modelo extrajo «lesiones: no» de un relato donde el asegurado mencionaba dolor cervical. Rectificar la fila del expediente es un `UPDATE`. Pero esa extracción errónea también vive en la traza de aquella ejecución y quizá en el conjunto de evaluación. La traza **no se rectifica**: es un registro de lo que pasó, y falsearla destruye su valor probatorio. Lo correcto es anotar la corrección, no reescribir la historia.
- **Oposición.** Oponerse a un tratamiento concreto —por ejemplo, a que su caso se use como material de evaluación— exige que ese tratamiento sea separable. Si tus evals se construyen copiando expedientes sin marca de origen, no puedes atender la oposición aunque quieras.

La regla operativa: **un derecho que no puedes ejecutar sobre una copia es una copia que no deberías tener**. Cada almacén nuevo que crea el sistema —prompts registrados, trazas, evals, cachés de contexto— tiene que entrar en el procedimiento de derechos el día que se crea, no el día que llega la primera solicitud.

> Fuentes primarias a abrir: RGPD, capítulo de derechos del interesado; directrices del CEPD sobre derecho de acceso; formularios y criterios publicados por la AEPD.

### 14. Supresión: qué se puede borrar y qué no

La slide más incómoda del bloque. Conviene mirarla de frente.

Marta Ortiz pide que se supriman sus datos. Su relato —donde cuenta que iba con su hija, que se golpeó la cabeza y que la llevaron a urgencias— no está en un sitio. Está, como mínimo, en:

- la fila del expediente;
- el prompt registrado de cada llamada al modelo, si guardas prompts;
- la traza de las ejecuciones del agente, con el relato dentro del contexto;
- los mensajes intermedios del bucle, si los persistes;
- el conjunto de evaluación, si su caso se seleccionó por ser difícil;
- las copias de seguridad de todo lo anterior;
- y lo que el proveedor retenga en su ventana.

Borrar la primera es trivial. Las demás son el problema real, y ninguna aparece en el procedimiento de supresión que Meridiana escribió antes de que existiera el agente.

Tres criterios para ordenar el desastre:

- **La supresión se ejecuta por identificador, no por búsqueda de texto.** Si tus trazas no llevan el identificador del interesado como campo indexado, no puedes suprimir: puedes hacer `grep`, que no es lo mismo y falla en cuanto el nombre está mal escrito.
- **El derecho no es absoluto.** El **artículo 17, apartado 3** enumera cinco supuestos en los que la supresión no se aplica (verificado 31/08/2026): a) el ejercicio de la libertad de expresión e información; b) el cumplimiento de una obligación legal impuesta por el Derecho de la Unión o de los Estados miembros, o una misión de interés público; c) razones de interés público en salud pública; d) fines de archivo en interés público, investigación científica o histórica o fines estadísticos; y e) «la formulación, el ejercicio o la defensa de reclamaciones». En Meridiana los que juegan son la b) y la e), y son el conflicto de la slide 24.
- **Las copias de seguridad se tratan aparte.** Lo habitual no es borrar dentro del *backup*, sino garantizar que el dato no se reintroduce al restaurar. Escrito antes de que lo pregunten.

Y si el caso está en el conjunto de evaluación, borrarlo cambia tus métricas. Ese es el precio correcto.

> Fuentes primarias a abrir: RGPD, artículo sobre supresión y sus excepciones; directrices del CEPD sobre el derecho al olvido; criterios de la AEPD sobre copias de seguridad y supresión.

### 15. Evaluación de impacto: cuándo es obligatoria aquí

La evaluación de impacto relativa a la protección de datos (EIPD) la regula el **artículo 35 del RGPD** (verificado 31/08/2026). Su apartado 1 la exige «cuando sea probable que un tipo de tratamiento, en particular si utiliza nuevas tecnologías, por su naturaleza, alcance, contexto o fines, entrañe un alto riesgo para los derechos y libertades de las personas físicas», y con una precisión que importa mucho aquí: **antes del tratamiento**.

El apartado 3 enumera tres supuestos en los que se requiere en particular, y los tres primeros describen Meridiana casi literalmente:

- a) «evaluación sistemática y exhaustiva de aspectos personales de personas físicas que se base en un tratamiento automatizado, como la elaboración de perfiles, y sobre cuya base se tomen decisiones que produzcan efectos jurídicos para las personas físicas o que les afecten significativamente de modo similar»;
- b) «tratamiento a gran escala de las categorías especiales de datos a que se refiere el artículo 9, apartado 1»;
- c) observación sistemática a gran escala de una zona de acceso público.

El apartado 4 encarga a la autoridad de control «establecer y publicar una lista de los tipos de operaciones de tratamiento que requieran una evaluación de impacto». En España esa lista existe y se titula «Listas de tipos de tratamientos de datos que requieren Evaluación de impacto relativa a protección de datos (art 35.4)» (verificado 31/08/2026 en la web de la AEPD). **Su contenido no se transcribe**: el PDF de la AEPD no se pudo leer en esta verificación, así que ninguno de los tipos que enumera aparece en este material. Hay que abrir la lista antes de decidir si un tratamiento concreto exige evaluación.

Y el apartado 7 fija el contenido mínimo, que es el esqueleto de la slide siguiente: descripción sistemática de las operaciones y sus fines, evaluación de la necesidad y la proporcionalidad, evaluación de los riesgos, y medidas previstas para afrontarlos.

En Meridiana, tres factores apuntan en la misma dirección y conviene evaluarlos juntos, no por separado:

- **Categorías especiales de datos.** Los relatos contienen información de salud: lesiones, urgencias, bajas médicas. No de forma ocasional, sino estructural.
- **Escala.** 32.000 siniestros al año, 180.000 pólizas en cartera. No es un piloto con veinte casos.
- **Automatización con efecto.** Un sistema que interviene en cuánto y cuándo cobra una persona tras un accidente.

Con esos tres a la vez, la conversación no es «¿hace falta EIPD?» sino «quién la firma y para cuándo».

Dos avisos prácticos:

- **La EIPD se hace antes del tratamiento, no después del incidente.** Una EIPD fechada tres semanas después de la puesta en producción documenta que no la hiciste.
- **Se rehace cuando el riesgo cambia.** Cambiar de proveedor de modelo, ampliar el prompt con el histórico del asegurado o empezar a guardar trazas completas son cambios de riesgo, aunque nadie los llame así. En la práctica, la EIPD es el documento que conecta este bloque con la gobernanza del B6: alguien tiene que decidir cuándo se revisa.

> Fuentes primarias a abrir: RGPD, artículo sobre evaluación de impacto y consulta previa; lista de tratamientos sujetos a EIPD publicada por la AEPD; directrices del CEPD sobre EIPD y criterios de alto riesgo.

### 16. La EIPD de Meridiana: estructura y conclusiones

Una EIPD útil cabe en quince páginas y se lee. Una de ochenta es un pasivo. El esqueleto del caso, con lo que de verdad se escribe en cada apartado:

1. **Descripción sistemática del tratamiento.** El flujo de punta a punta: por dónde entra el relato, a qué sistemas va, qué sale hacia el proveedor, qué se guarda y dónde. Aquí el diagrama de arquitectura del curso 3 se reutiliza tal cual, y por eso este apartado se escribe en una tarde y no en un mes.
2. **Necesidad y proporcionalidad.** Por qué hace falta un modelo y qué se evaluó como alternativa. La respuesta honesta —«once días de tramitación media y un equipo que se dedica a leer y clasificar»— es más sólida que cualquier párrafo sobre innovación.
3. **Riesgos identificados.** No genéricos: los del sistema real.
4. **Medidas y riesgo residual.** Cada riesgo con su medida y lo que queda después.
5. **Conclusión y firma.** Nombre, fecha y, si procede, consulta a la autoridad.

Los riesgos del caso, que no salen de ninguna plantilla comprada:

| Riesgo | Medida | Residual |
|---|---|---|
| Datos de salud en el prompt | Seudonimización, contexto mínimo | Medio: la lesión es la tarea |
| Extracción errónea que cambia la cobertura | Evals en CI | Bajo |
| Supervisión aparente en el tramo bajo | Métrica de tasa de modificación | Medio, en revisión |
| Trazas con relatos completos | Retención acotada, acceso restringido | Medio |
| Retención en el proveedor | Plan sin retención, contrato | Según contrato: hay que leerlo |

La última fila es honesta a propósito: hasta que no se verifique con el contrato, el residual no se puede escribir. Sigue abierta a 31/08/2026 por el mismo motivo que la slide 8: el dato no está en ninguna norma, está en la adenda firmada con el proveedor.

> Fuentes primarias a abrir: RGPD, artículo sobre evaluación de impacto y su contenido mínimo; directrices del CEPD sobre EIPD; plantilla de EIPD de la AEPD; adenda de tratamiento de datos del proveedor.

### 17. Registro de actividades de tratamiento

El registro de actividades es el inventario de qué trata la organización, para qué, con qué base, con quién lo comparte y cuánto lo conserva. Lo regula el **artículo 30 del RGPD** (verificado 31/08/2026). Su apartado 1 exige que el registro del responsable contenga: a) nombre y datos de contacto del responsable, corresponsable, representante y delegado de protección de datos; b) los fines del tratamiento; c) una descripción de las categorías de interesados y de datos personales; d) las categorías de destinatarios, «incluidos los destinatarios en terceros países u organizaciones internacionales»; e) en su caso, las transferencias a terceros países, identificándolos; f) «cuando sea posible, los plazos previstos para la supresión de las diferentes categorías de datos»; g) cuando sea posible, una descripción general de las medidas técnicas y organizativas de seguridad.

Y el apartado 5 es el que hay que leer despacio antes de creerse exento: la obligación «no se aplicará a ninguna empresa ni organización que emplee a menos de 250 personas, **a menos que** el tratamiento que realice pueda entrañar un riesgo para los derechos y libertades de los interesados, no sea ocasional, o incluya categorías especiales de datos personales indicadas en el artículo 9, apartado 1». Meridiana trata datos de salud, de forma estructural y no ocasional: las tres puertas de la excepción están abiertas a la vez. El umbral de 250 no la salva.

Lo interesante para nosotros es que **un sistema con LLM crea actividades de tratamiento que el registro de la organización no contempla**, y lo hace en un ritmo que ninguna revisión anual alcanza. El registro de Meridiana tiene una entrada llamada «Gestión de siniestros de automóvil» que se escribió cuando el flujo era humano. Sigue siendo verdad y ya no es suficiente.

Lo que falta por añadir, y que sale directamente de las slides anteriores:

- La **comunicación al proveedor del modelo** como destinatario, con su territorio y su base para la transferencia.
- El **registro de trazas** como tratamiento con finalidad propia —responder a reclamaciones y auditorías— y con su plazo propio.
- El **conjunto de evaluación**, si contiene casos reales, con su finalidad de mejora y control de calidad.
- Los **prompts almacenados**, si se almacenan.

Regla que evita que esto se vuelva a descuadrar: **la entrada del registro se actualiza en el mismo pull request que introduce el nuevo almacén de datos**. Si crear una tabla de trazas y actualizar el registro son dos tareas de dos equipos con dos calendarios, el registro va a estar mal siempre. Y un registro desactualizado es peor que uno inexistente: documenta por escrito que creías estar tratando otra cosa.

> Fuentes primarias a abrir: RGPD, artículo sobre registro de actividades de tratamiento; modelos y plantillas de registro publicados por la AEPD.

### 18. Brechas de seguridad con datos en prompts

Una brecha en un sistema con LLM tiene formas que el plan de respuesta de la organización no anticipó, porque los datos están en sitios que el plan no conoce. Las obligaciones de notificación están en los **artículos 33 y 34 del RGPD** (verificado 31/08/2026).

El artículo 33.1 fija el plazo y su excepción: el responsable notificará la violación a la autoridad de control competente «sin dilación indebida y, de ser posible, a más tardar 72 horas después de que haya tenido constancia de ella, a menos que sea improbable que dicha violación de la seguridad constituya un riesgo para los derechos y las libertades de las personas físicas». Y añade algo que casi nadie recuerda: si se pasa de 72 horas, la notificación «deberá ir acompañada de indicación de los motivos de la dilación». Llegar tarde no cierra la puerta; llegar tarde y callar, sí.

El artículo 33.3 fija el contenido mínimo: a) naturaleza de la violación, con las categorías y el número aproximado de interesados y de registros afectados; b) nombre y datos de contacto del delegado de protección de datos o de otro punto de contacto; c) posibles consecuencias; d) medidas adoptadas o propuestas, incluidas las de mitigación.

Y el artículo 34.1 marca el segundo umbral: «cuando sea probable que la violación de la seguridad de los datos personales entrañe un **alto** riesgo para los derechos y libertades de las personas físicas», hay que comunicarla además al interesado, sin dilación indebida. Dos umbrales distintos y dos destinatarios distintos: riesgo → autoridad; alto riesgo → también las personas.

El detalle que decide todo en un sistema con LLM: el reloj de las 72 horas arranca en «haya tenido constancia», no en «haya terminado el análisis». Por eso la letra a) del 33.3 —cuántas personas— es la que hay que poder responder rápido, y por eso importa que las trazas correlacionen cada llamada con un expediente.

Los cuatro escenarios concretos de Meridiana, que conviene tener escritos antes de que ocurran:

- **Fuga en el proveedor.** Se expone lo que hubiera en su ventana de retención. Tu primera pregunta operativa es *qué relatos salieron y de quién*, y solo puedes responderla si tus trazas correlacionan cada llamada con un expediente.
- **Traza con permisos demasiado abiertos.** El caso más frecuente y el menos vistoso: el panel de observabilidad del curso 3 muestra prompts completos, y tiene acceso todo el equipo de ingeniería. Ahí hay relatos con datos de salud de miles de personas leídos por gente que no tramita siniestros.
- **Contexto cruzado.** Un fallo de aislamiento hace que el contexto de un expediente incluya fragmentos de otro. La salida sale bien formada y contiene datos de un tercero. Es una brecha silenciosa: no hay error en ningún log.
- **Exfiltración por inyección.** El relato del asegurado incluye instrucciones para que el sistema devuelva información de otros expedientes que estén en el contexto. Si el agente tiene acceso a más de lo que la tarea necesita, funciona.

Los dos últimos son específicos de esta arquitectura y **la detección no puede ser un log de errores**: hay que buscarlos en las salidas, con muestreo y con evals dedicados.

> Fuentes primarias a abrir: RGPD, artículos sobre notificación de violaciones de seguridad; directrices del CEPD sobre notificación de brechas y ejemplos prácticos; canal y formulario de notificación de la AEPD.

### 19. Ejercicio: EIPD sobre el flujo de FNOL

Este es el ejercicio central del bloque y conviene hacerlo con el flujo delante, no de memoria.

**Qué se entrega:** los cinco apartados de la slide 16 aplicados únicamente al FNOL —desde que entra el relato hasta que se guarda la extracción—, en un máximo de ocho páginas. El triaje y la propuesta de resolución quedan fuera a propósito: acotar el alcance es parte del ejercicio.

**Cómo empezar, en este orden:**

1. **Dibuja el flujo de datos, no la arquitectura.** Cada flecha es una copia de datos personales. Cuenta las flechas: si te salen menos de seis, te falta alguna.
2. **Marca en cada almacén qué categorías de datos hay.** Y sé específico: «texto libre» no es una categoría, «relato que puede contener información de salud de la persona y de terceros» sí.
3. **Para cada almacén, tres columnas:** cuánto dura, quién puede leerlo, con qué finalidad. Son las mismas tres preguntas de la memoria del agente en el curso 3, formuladas desde otro sitio.
4. **Ahora sí, los riesgos.** Uno por cada fila que no supiste rellenar.

**Criterio de calidad:** el documento sirve si alguien que no construyó el sistema puede leerlo y decir dónde está el relato de un asegurado concreto. Si no puede, has escrito una descripción del proyecto, no una EIPD.

**Trampa habitual:** listar como riesgo «uso indebido de la IA». No es un riesgo, es una categoría. Un riesgo se escribe con sujeto, acción y consecuencia: «un ingeniero con acceso al panel de trazas lee el historial médico declarado de un asegurado sin relación con su trabajo».

### 20. Categorías especiales de datos en un relato de siniestro

Un siniestro de automóvil parece un tratamiento de datos anodino —matrícula, fecha, daños— hasta que lees los relatos. En cuanto hay lesiones, hay datos de salud, y el **artículo 9 del RGPD** somete esas categorías a un régimen reforzado (verificado 31/08/2026). Su apartado 1 **prohíbe** —esa es la regla, y las excepciones vienen después— el tratamiento de datos que revelen origen étnico o racial, opiniones políticas, convicciones religiosas o filosóficas o afiliación sindical, y el tratamiento de datos genéticos, datos biométricos dirigidos a identificar de manera unívoca a una persona, **datos relativos a la salud** y datos relativos a la vida sexual o la orientación sexual.

El apartado 2 levanta la prohibición en diez supuestos tasados, de la letra a) a la j): consentimiento explícito, Derecho laboral y de seguridad social, intereses vitales, organismos sin ánimo de lucro, datos hechos manifiestamente públicos por el interesado, «formulación, ejercicio o defensa de reclamaciones», interés público esencial, medicina preventiva o laboral y asistencia sanitaria o social, interés público en salud pública, y fines de archivo, investigación o estadística.

Cuál de esas letras sostiene el tratamiento de Meridiana no es una pregunta retórica: es la que hay que responder por escrito en la EIPD, y la respuesta cambia según se hable de tramitar el siniestro o de reutilizar el caso en un conjunto de evaluación.

Lo que importa aquí es que en Meridiana **esto no es un caso raro: es una parte estructural del volumen**. Y llega en la peor forma posible, en texto libre y sin avisar:

> «Fui al hospital porque llevo una prótesis de cadera desde hace dos años y el golpe me dejó sin poder apoyar. Estoy de baja y en tratamiento con antidepresivos desde antes del accidente.»

De esas tres líneas, la tarea del sistema necesita exactamente un bit: **hay lesiones, sí o no**. Todo lo demás —la prótesis, la baja, la medicación— es información de salud que entra en el prompt, viaja al proveedor, vuelve en la respuesta y se queda en la traza sin que nadie la haya pedido.

Tres consecuencias de diseño:

- **La derivación por lesiones tiene ahora un segundo motivo.** No solo protege al herido: saca del flujo automático los expedientes con más carga de datos sensibles.
- **La traza de esos casos merece un tratamiento distinto** del resto: retención más corta, acceso más restringido, o guardar la extracción y no el relato.
- **La minimización tiene un techo.** No puedes quitar del prompt lo que necesitas para detectar lesiones. Puedes, en cambio, no arrastrar el relato completo en las vueltas siguientes del bucle una vez extraído el campo.

> Fuentes primarias a abrir: RGPD, artículo sobre categorías especiales de datos y sus excepciones; considerandos sobre datos de salud; guía de la AEPD sobre tratamiento de datos de salud.

### 21. Datos de terceros que aparecen sin que nadie los pidiera

El asegurado contrató la póliza y recibió la información sobre el tratamiento. Su relato, en cambio, no habla solo de él:

> «El del Kia se bajó insultando, dijo que iba tarde a recoger a su hijo del colegio Santa Ana. Mi mujer, que iba de copiloto, se dio un latigazo cervical. Un señor de la terraza lo vio todo y me dio su móvil: 6XX XXX XXX.»

En cuatro líneas han entrado el conductor contrario, un menor no implicado, la esposa con un dato de salud y un testigo con su teléfono. Ninguno ha sido informado, y tres de los cuatro no tienen relación contractual con Meridiana.

Esto no lo inventó el LLM: pasaba igual cuando el relato lo leía un tramitador. Lo que cambia es la **persistencia**: ese texto se copia a un prompt, sale hacia un tercero, se guarda en varias trazas y quizá acaba en un eval. El dato del testigo tiene ahora cinco vidas.

Qué se puede hacer de verdad, en orden de eficacia:

- **Pedir menos.** Si el formulario separa «qué pasó» de «datos de contacto de testigos», el relato libre se llena menos de terceros. Es diseño de producto, y es lo que más reduce el problema.
- **No propagar.** Extraídos los campos, las vueltas siguientes del bucle no necesitan el relato en crudo. Cortar ahí evita que el dato del testigo se replique diez veces.
- **Detectar y marcar**, no borrar en silencio. Un detector que señale menores o datos de salud de no asegurados permite tratar esos expedientes con otra política.
- **Informar cuando proceda.** El artículo aplicable es el **14 del RGPD**, «Información que deberá facilitarse cuando los datos personales no se hayan obtenido del interesado» (verificado 31/08/2026). Su apartado 3 fija el momento: dentro de un plazo razonable y **a más tardar en un mes** desde que se obtuvieron; o, si van a usarse para comunicarse con esa persona, a más tardar en la primera comunicación; o, si van a comunicarse a otro destinatario, a más tardar cuando se comuniquen por primera vez. Su apartado 5 recoge las excepciones: a) que el interesado ya disponga de la información; b) que informar «resulte imposible o suponga un esfuerzo desproporcionado»; c) que la obtención o comunicación esté expresamente establecida por el Derecho de la Unión o de los Estados miembros; d) que los datos deban seguir siendo confidenciales por una obligación de secreto profesional. La letra b) es la que se invoca siempre y la que hay que poder justificar caso a caso, no por costumbre.

> Fuentes primarias a abrir: RGPD, artículo sobre información cuando los datos no se obtienen del interesado y sus excepciones; criterios de la AEPD sobre datos de terceros en expedientes.

### 22. Plazos de conservación por finalidad, no por comodidad

«Lo guardamos todo, por si acaso» no es una política de conservación: es la ausencia de una. Y en un sistema con LLM se nota antes que en cualquier otro, porque los almacenes se multiplican.

El principio operativo es que **el plazo se ata a la finalidad, y finalidades distintas dan plazos distintos sobre el mismo dato**. El relato de Marta Ortiz existe para tramitar su siniestro; también existe, en otra copia, para poder demostrar dentro de tres años por qué se le propuso ese importe. Son dos relojes, y confundirlos lleva a los dos errores clásicos: borrar lo que había que conservar y conservar lo que había que borrar.

En Meridiana, los relojes que hay que separar son al menos cinco:

| Almacén | Finalidad | Plazo |
|---|---|---|
| Expediente | Tramitar y responder de la póliza | Sin plazo verificado |
| Traza del agente | Defensa ante reclamación y auditoría | Sin plazo verificado |
| Prompt registrado | Depuración técnica | Decisión propia: corto |
| Conjunto de evaluación | Control de calidad del sistema | Decisión propia |
| Copias de seguridad | Continuidad | Ciclo de restauración |

Los dos primeros plazos **no los decides tú**: dependen de la normativa de seguros, de los plazos de prescripción de acciones y de las obligaciones contables y regulatorias del sector.

De ahí hay una pieza cerrada y conviene tenerla delante. La **Ley 50/1980, de 8 de octubre, de Contrato de Seguro, artículo 23** dice literalmente (verificado 31/08/2026 en el BOE): «Las acciones que se deriven del contrato de seguro prescribirán en el término de dos años si se trata de seguro de daños y de cinco si el seguro es de personas.» Es el reloj que marca durante cuánto tiempo Meridiana puede verse reclamada por un siniestro, y por tanto el suelo del que parte cualquier conversación sobre conservar el expediente. Dos años y cinco años: dos relojes distintos sobre el mismo expediente en cuanto hay lesionados.

Lo que **no** se ha verificado, y por eso las dos primeras filas de la tabla no llevan un plazo: las obligaciones de conservación que imponen la normativa de ordenación y supervisión de los seguros y la normativa contable y mercantil. La prescripción de la acción y el plazo de conservación documental no son lo mismo, y traducir el uno en el otro sin abrir la norma es exactamente el error que este bloque intenta evitar.

Los tres siguientes **sí los decides tú**, y por eso son los que más se descuidan. Un prompt guardado para depurar un fallo de hace ocho meses no tiene ninguna finalidad viva. Lo correcto es un plazo corto y automático: un trabajo programado que borre, no una nota en una wiki.

> Fuentes primarias a abrir: normativa española de contrato de seguro y de ordenación y supervisión de seguros; plazos de prescripción aplicables; RGPD, principio de limitación del plazo de conservación.

### 23. Trazas y registros: cuánto tiempo y con qué justificación

En el curso 3 (B2) construimos trazas para poder responder a las tres de la mañana qué hizo el agente. Ahora hay que decir la otra mitad: **una traza guardada es un tratamiento de datos personales**, con todo lo que eso arrastra.

Y no es un tratamiento cualquiera. Una traza completa contiene el prompt renderizado —es decir, el relato íntegro del accidente—, la salida del modelo, los argumentos de cada tool y a menudo la póliza. Concentra, en un almacén pensado para ingenieros, más datos personales por registro que la propia base de negocio.

Además, el AI Act pide registro automático de eventos para determinados sistemas (B4). Aquí aparece una tentación peligrosa: **usar «me obliga el AI Act» como justificación para guardar trazas completas para siempre**. No sirve: la obligación de registrar no es una autorización para registrar cualquier cosa durante cualquier plazo.

La forma de resolverlo es separar dos cosas que se suelen guardar juntas:

- **Lo que hace falta para reconstruir la decisión:** qué versión de prompt y de modelo se usó, qué campos se extrajeron, qué reglas dispararon, qué tools se llamaron con qué argumentos, quién aprobó. Esto es casi todo metadato y justifica plazos largos.
- **El contenido en crudo:** el relato del asegurado dentro del prompt. Esto es lo que carga la traza de datos sensibles y lo que debe tener el plazo más corto, o no estar: en muchos casos basta con guardar una referencia al expediente, que ya contiene el relato con su propia política.

Con esa separación, la traza a un año pesa poco y sigue sirviendo para lo que se construyó.

Y aquí el AI Act sí se ha podido leer, en su texto consolidado tras la modificación de 2026 (verificado 31/08/2026). El **artículo 12** obliga a que los sistemas de IA de alto riesgo «permitan técnicamente el registro automático de acontecimientos (en lo sucesivo, "archivos de registro") a lo largo de todo el ciclo de vida del sistema», con capacidades orientadas a detectar situaciones de riesgo o modificaciones sustanciales, a facilitar la vigilancia poscomercialización y a supervisar el funcionamiento del sistema.

El plazo lo fija el **artículo 19, apartado 1**, y es más corto de lo que la gente supone: los proveedores conservarán esos archivos «durante un período de tiempo adecuado para la finalidad prevista del sistema de IA de alto riesgo, **de al menos seis meses**, salvo que el Derecho de la Unión o nacional aplicable, en particular el Derecho de la Unión en materia de protección de datos personales, disponga otra cosa».

Lee esa última cláusula dos veces, porque desarma la tentación de la que hablábamos: el propio reglamento remite al Derecho de protección de datos. Seis meses es un **mínimo**, no una licencia para guardar el relato íntegro indefinidamente, y el RGPD sigue mandando sobre qué hay dentro de esos archivos.

> Fuentes primarias a abrir: AI Act, artículos sobre registro automático de eventos y conservación de registros; RGPD, principios de minimización y limitación del plazo; guía de la AEPD sobre registros de actividad y logs.

### 24. El conflicto entre supresión y registro obligatorio

Marta Ortiz ejerce su derecho de supresión. La normativa de seguros y las obligaciones de conservación del expediente dicen que Meridiana debe conservar cierta documentación durante un plazo. El AI Act pide conservar registros del sistema. Los tres mandatos son reales y apuntan en direcciones distintas.

Este conflicto **no se resuelve eligiendo un bando**, ni borrando y esperando que nadie pregunte. Se resuelve descomponiendo la solicitud, que es lo que casi nadie hace:

1. **Qué se conserva por obligación legal.** Se conserva, y se le dice: qué se conserva, por qué norma y hasta cuándo. Una respuesta que explica la base de la negativa parcial es una respuesta atendida, no una denegación.
2. **Qué se conserva por interés propio sin obligación.** El conjunto de evaluación, los prompts de depuración, la copia que alguien hizo en un cuaderno de análisis. Aquí no hay nada que oponer: se borra.
3. **Qué se puede bloquear en vez de borrar.** Limitar el tratamiento —conservar sin usar, con acceso restringido— es a menudo la salida técnica correcta para lo que no se puede eliminar.
4. **Qué queda fuera de tu alcance.** Lo que retenga el proveedor del modelo. Tienes que poder describirlo con precisión, no con una frase evasiva.

Dos de las tres piezas están cerradas (verificado 31/08/2026). Las **excepciones a la supresión** son las cinco del artículo 17.3 que ya vimos en la slide 14; aquí operan la letra b) —obligación legal de conservar— y la letra e), «para la formulación, el ejercicio o la defensa de reclamaciones». Y la **limitación del tratamiento** sí es una figura del reglamento con nombre propio: el artículo 18, «Derecho a la limitación del tratamiento», cuyo apartado 1, letra c), contempla exactamente el punto 3 de arriba —que el responsable ya no necesite los datos para sus fines «pero el interesado los necesite para la formulación, el ejercicio o la defensa de reclamaciones»— y cuya letra b) permite limitar en vez de suprimir cuando el interesado se opone a la supresión.

Falta la tercera pieza: **qué plazos de conservación impone la normativa de seguros no se afirma aquí**. Lo único abierto es el artículo 23 de la Ley 50/1980 (prescripción de las acciones: dos años en seguro de daños, cinco en seguro de personas), que no es lo mismo que un plazo de conservación documental. Ver la slide 22.

Lo que se lleva un ingeniero de esta slide: **la capacidad de responder bien depende de una decisión que se toma en el diseño**, no en el momento de la solicitud. Si los datos que hay que conservar y los que hay que borrar están en el mismo blob, no hay procedimiento posible.

> Fuentes primarias a abrir: RGPD, artículos sobre supresión, limitación del tratamiento y sus excepciones; normativa española de seguros sobre conservación de expedientes; AI Act, obligaciones de conservación de registros.

### 25. Información al interesado: qué se le cuenta y cuándo

La información que hay que dar al interesado tiene un contenido tasado y un momento, y depende de una sola pregunta: de quién se obtuvieron los datos (verificado 31/08/2026).

- Si se obtienen **del propio interesado**, manda el **artículo 13**, «Información que deberá facilitarse cuando los datos personales se obtengan del interesado». Su apartado 1 exige: a) identidad y datos de contacto del responsable y, en su caso, de su representante; b) datos de contacto del delegado de protección de datos; c) los fines y **la base jurídica** del tratamiento; d) si la base es el interés legítimo, cuál es ese interés; e) los destinatarios o categorías de destinatarios; f) la intención de transferir a un tercer país y la existencia o ausencia de decisión de adecuación. Y su apartado 2 añade: a) el plazo de conservación o los criterios para determinarlo; b) los derechos de acceso, rectificación y supresión; c) el derecho a retirar el consentimiento, si esa era la base; d) el derecho a reclamar ante una autoridad de control; e) si facilitar los datos es un requisito legal o contractual; f) la existencia de decisiones automatizadas del artículo 22.
- Si **no** se obtienen del interesado —el caso de la slide 21—, manda el **artículo 14**, con los plazos y las excepciones que allí se detallan.

Fíjate en dónde caen las cuatro frases que vienen a continuación: casi todas están en el artículo 13, apartado 1, letras e) y f), y apartado 2, letras a) y f). No es casualidad. Son justo los cuatro elementos que un LLM en el flujo modifica.

Lo que aporta esta slide es **qué frases de una política de privacidad dejan de ser ciertas** el día que un LLM entra en el flujo, porque suele ser el mismo puñado en todas partes:

- «Sus datos son tratados por personal de Meridiana». Ya no solo: hay un encargado que recibe el relato completo.
- «No se realizan transferencias internacionales». A verificar contra la región del proveedor y sus subencargados.
- «No se toman decisiones automatizadas». A contrastar con la slide 11 y con la tasa real de modificación humana.
- «Los datos se conservan durante la vigencia de la relación». Ya no: hay trazas y evals con relojes propios.

Cambiar esas cuatro frases es media hora de trabajo y es lo que más a menudo se queda sin hacer, porque la política de privacidad la custodia un departamento y el sistema lo construye otro.

Y una decisión de producto que no es jurídica pero se decide aquí: **si se le dice al asegurado que hay un sistema automatizado leyendo su relato, y cómo se le dice**. Enterrarlo en la página 4 de un documento cumple la forma. Una frase clara en el propio formulario del portal —qué hace el sistema, qué decide una persona y cómo pedir revisión humana— es lo que evita la sensación de engaño el día que alguien reclama. El AI Act empuja además en esa dirección con sus obligaciones de transparencia (B1), así que se resuelve una vez y sirve para las dos normas.

> Fuentes primarias a abrir: RGPD, artículos sobre información al interesado; directrices del CEPD sobre transparencia; AI Act, obligaciones de transparencia frente a personas expuestas al sistema.

### 26. Contrato de encargo con el proveedor: cláusulas que importan

El contrato con el proveedor del modelo suele venir dado: es su adenda de tratamiento de datos, se acepta con un clic y no se negocia. Eso no es excusa para no leerlo. Aunque no puedas cambiar una coma, **leerlo es lo que te dice qué riesgo estás asumiendo**, y ese riesgo hay que documentarlo en la EIPD.

El contenido mínimo del contrato de encargo lo fija el **artículo 28, apartado 3, del RGPD**, y está detallado en la slide 6: objeto, duración, naturaleza y finalidad del tratamiento, tipo de datos y categorías de interesados, obligaciones y derechos del responsable, más las ocho estipulaciones de las letras a) a h) —instrucciones documentadas, confidencialidad, seguridad del artículo 32, condiciones para subcontratar, asistencia en los derechos de los interesados, ayuda en los artículos 32 a 36, supresión o devolución al terminar, y puesta a disposición de la información necesaria para demostrar el cumplimiento (verificado 31/08/2026).

Eso es el mínimo legal. Y conviene decirlo claro: **el mínimo lo cumple casi cualquier adenda estándar**. Por eso la lista que viene ahora no repite el artículo 28, sino que mira lo que ese mínimo no resuelve en un proveedor de modelos.

Las cláusulas donde se juega el partido en un proveedor de modelos, más allá de ese mínimo:

- **Finalidad limitada y prohibición de uso para entrenamiento.** Que esté escrito, no en una FAQ.
- **Subencargados.** Lista, territorios y —lo importante— **cómo te notifican un cambio y qué puedes hacer si no te gusta**. Un derecho de oposición cuyo único ejercicio posible es rescindir el contrato es un derecho de adorno, pero al menos sabes lo que tienes.
- **Retención y borrado.** Plazos, cómo se solicita el borrado anticipado y qué evidencia obtienes de que ocurrió.
- **Asistencia en derechos y en brechas.** El proveedor debe ayudarte a atender solicitudes y avisarte de incidentes. Concreta: canal, contacto, contenido mínimo del aviso.
- **Auditoría.** Qué puedes pedir realmente. Casi siempre un informe de auditoría de terceros, no una inspección.
- **Terminación.** Borrado o devolución, plazo, y qué pasa con lo que quede en registros internos.

Lo que casi siempre falta y hay que buscar expresamente: **qué ocurre con las trazas y registros internos del proveedor cuando pides el borrado**. Suele estar en otro documento, o no estar.

> Fuentes primarias a abrir: RGPD, artículo sobre contrato de encargo; cláusulas contractuales tipo de la Comisión; adenda de tratamiento de datos y política de subencargados del proveedor contratado.

### 27. Auditar el cumplimiento del encargado

«Auditar al proveedor» suena a enviar dos personas a un centro de datos. Con un proveedor de modelos, eso no va a pasar. Lo que sí puedes hacer es un control real y proporcionado, y conviene tenerlo escrito porque un auditor lo va a preguntar (B6).

Lo que se puede obtener de verdad, de menos a más esfuerzo:

- **Informes de auditoría de terceros y certificaciones vigentes.** Se piden, se leen —incluidas las excepciones y el alcance, que es donde está la información— y se archivan con fecha.
- **Un cuestionario anual respondido por escrito.** Las ocho preguntas de la slide 9, con la respuesta firmada. Su valor no está en la respuesta: está en tener constancia de qué te dijeron y cuándo.
- **Verificación técnica de lo verificable.** Aquí es donde un equipo de ingeniería aporta algo que ningún cuestionario da:
  - Comprobar en qué región se resuelven realmente tus llamadas, mirando cabeceras y latencias, no la consola.
  - Confirmar que la configuración de retención de tu cuenta es la que crees, y volver a comprobarlo tras cada cambio de plan.
  - Alertar cuando cambia la lista de subencargados o la versión de los términos, con un trabajo programado que compare y avise. Es media jornada de trabajo y sustituye a una vigilancia manual que nadie hace.
- **Registro de incidentes del proveedor.** Qué te comunicaron, cuándo y qué hiciste.

Y el control que más se olvida: **revisar tu propio uso**. La mayoría de las desviaciones no son del proveedor. Son un equipo que empezó a mandar el histórico completo del asegurado en el prompt porque mejoraba una métrica, sin que nadie revisara qué datos añadía eso al envío.

### 28. Coordinar RGPD y AI Act sin duplicar documentación

Al llegar aquí tienes dos cuerpos normativos pidiendo documentos que se parecen mucho, y la tentación es montar dos carpetas paralelas. Es un error caro: se duplica el trabajo, y en cuanto los dos documentos divergen —y divergen en el primer cambio— tienes una contradicción escrita entre tus propios papeles. Un auditor que encuentra dos versiones distintas del mismo flujo de datos ya no cree ninguna de las dos.

La forma correcta es **una fuente por hecho, y dos documentos que la referencian**:

| Hecho | Se escribe una vez en | Lo reutiliza |
|---|---|---|
| Flujo de datos y arquitectura | Descripción del sistema (B4) | EIPD, registro de actividades |
| Riesgos y mitigaciones | Gestión de riesgos (B4) | EIPD, sección de riesgos |
| Supervisión humana | Expediente técnico (B4) | Análisis del artículo 22 (slide 11) |
| Registros y su retención | Política de retención | Registro de eventos AI Act, RGPD |
| Proveedores | Matriz de roles (B3) | Contratos de encargo, subencargados |

Lo que **no** se puede fusionar, porque las preguntas son genuinamente distintas:

- El AI Act pregunta por el **riesgo del sistema**: si funciona bien, si es robusto, si alguien lo supervisa. Es un reglamento de producto (B1).
- El RGPD pregunta por el **riesgo para la persona** cuyos datos se tratan: si sabe lo que pasa, si puede oponerse, si sus datos están donde deben.

Un sistema puede ser técnicamente impecable y tratar datos sin base jurídica. Y puede tener un tratamiento impecable y ser un sistema poco fiable.

La conclusión operativa, que enlaza con el bloque siguiente: esto solo se sostiene si hay **una persona responsable de que las dos vistas cuadren**, con una revisión periódica. Sin eso, se separan en tres meses.

### 29. Ejercicio práctico 1: seguir el relato de Marta Ortiz por todo el sistema {ejercicio:B5-ej1}

Coge un expediente sintético con lesiones de `content/caso/datos/siniestros.json` y **encuentra físicamente todas las copias de su relato**. No las que deberían existir: las que existen.

Recorre el sistema del curso 3 y anota, para cada copia: en qué almacén está, con qué identificador se puede localizar, quién tiene acceso hoy y qué plazo de borrado tiene configurado. Los sitios donde mirar son al menos: la tabla del expediente, las trazas, los registros de la plataforma de observabilidad, los mensajes intermedios del bucle, el conjunto de evaluación y las copias de seguridad.

**Entregable:** una tabla de una página, y una segunda columna que casi todo el mundo tiene que dejar en blanco: *cómo ejecutarías una supresión sobre esa copia*.

**Criterio de aceptación:** si alguna fila no se puede localizar por identificador de interesado —solo buscando texto—, esa fila es un hallazgo, y es el hallazgo más valioso del ejercicio. Anótalo como tal en vez de arreglarlo por el camino.

### 30. Ejercicio práctico 2: minimizar el contexto sin perder calidad {ejercicio:B5-ej2}

Coge la plantilla de prompt de extracción de FNOL y quítale campos, uno a uno, ejecutando los evals del curso 3 después de cada retirada.

Orden sugerido: primero lo que evidentemente sobra (NIF, IBAN, teléfono), después lo dudoso (póliza completa frente a tres campos, histórico de siniestros), y al final lo que crees imprescindible, para comprobar si lo es.

**Entregable:** una tabla con tres columnas —campo retirado, métrica de extracción antes y después, decisión— y una frase de justificación por fila.

**Criterio de aceptación:** al menos un campo que el equipo daba por necesario sale del prompt sin que las métricas se muevan. Si no encuentras ninguno, revisa si tus evals son lo bastante sensibles para detectar el cambio: un eval que no distingue entre dos contextos muy distintos tampoco va a detectar una regresión.

Escribe también el resultado inverso cuando ocurra: un campo que quitaste y empeoró las métricas es exactamente la justificación documentada de por qué ese dato sí se envía. Eso es proporcionalidad demostrada, y vale más en una EIPD que tres párrafos de argumentación.

### 31. Mini-quiz de comprensión — B5 {quiz:B5}

Tres preguntas sobre el razonamiento del bloque: cuándo una supervisión humana es real o solo aparente, qué hay que exigir por escrito a un proveedor de modelo y qué se puede sacar del contexto sin dañar la tarea.

**Ninguna depende de recordar un plazo, un artículo ni un importe**, y eso es deliberado. Los números de este bloque son precisamente los que hay que verificar en fuente primaria antes de usarlos, y memorizarlos de un curso es la peor forma posible de aprenderlos. Lo que sí tienes que llevarte es el razonamiento: cómo se distingue una intervención humana sustantiva de un trámite, qué convierte una respuesta de un proveedor en evidencia utilizable y cómo se demuestra la minimización en vez de opinarla.

Si alguna pregunta te hace dudar, la slide correspondiente está enlazada en la explicación de la respuesta. Aprobado con dos aciertos, y puedes repetirlo las veces que quieras.

## Qué te llevas

- El prompt es un tratamiento de datos y se documenta como tal.
- Lo que el proveedor del modelo retiene es una decisión tuya, no suya.
- El derecho a explicación exige diseñar la trazabilidad antes, no después.

## Mini-quiz (3 preguntas)

Una sola opción correcta por pregunta, con explicación. Se generan con el mismo formato que `assets/js/quizzes-data.js` para que el importador las recoja.

1. **Tema:** Cuándo la propuesta de resolución entra en el ámbito del artículo 22
   - **Enunciado:** En Meridiana, los expedientes por debajo del umbral se aprueban con un clic sobre un botón preseleccionado, y la tasa de propuestas modificadas por el tramitador en ese tramo es del 0,4 %. ¿Qué conclusión es la correcta?
   - **Opciones:**
     - a) No hay problema: existe un humano en el flujo y eso descarta la decisión automatizada.
     - b) **Hay indicios de que la supervisión es aparente en ese tramo, y la tasa de modificación es la evidencia que hay que analizar.** ✅
     - c) El problema desaparece añadiendo una advertencia en la política de privacidad.
     - d) Basta con quitar el LLM del cálculo del importe y dejarlo solo en la redacción del texto de la propuesta.
   - **Explicación:** Lo que importa no es que exista un humano, sino que su intervención sea sustantiva: información suficiente, tiempo, autoridad y consecuencias asumibles al discrepar. La tasa de modificación es la métrica que distingue una revisión real de un trámite. La (a) confunde forma con sustancia. La (c) informa de un tratamiento sin cambiarlo. La (d) describe un cambio de arquitectura que puede ser buena idea, pero no responde a la pregunta de si la supervisión actual es efectiva.

2. **Tema:** Qué hay que preguntar a un proveedor de modelo sobre retención
   - **Enunciado:** Vas a contratar un proveedor de modelo para el FNOL. ¿Cuál de estas respuestas del proveedor te sirve como evidencia utilizable?
   - **Opciones:**
     - a) Un correo del comercial diciendo que «no guardan nada de los clientes empresariales».
     - b) Una entrada de su blog de ingeniería sobre privacidad, muy detallada.
     - c) **La cláusula de la adenda de tratamiento de datos que fija qué se retiene, dónde, cuánto y cómo se desactiva, archivada con fecha de consulta.** ✅
     - d) Que el proveedor sea una empresa grande con certificaciones conocidas.
   - **Explicación:** Solo la (c) es un compromiso contractual verificable y datable, que es lo que tendrás que enseñar si alguien pregunta qué sabías el día que enviaste el primer relato. La (a) no obliga a nadie. La (b) describe intenciones y cambia sin aviso. La (d) es reputación, no una obligación sobre tus datos.

3. **Tema:** Qué se puede minimizar en el FNOL antes de llamar al modelo
   - **Enunciado:** El prompt de extracción de FNOL incluye el relato, la fecha declarada, la póliza serializada entera (con NIF e IBAN) y el histórico de siniestros del asegurado. ¿Cuál es el criterio correcto para decidir qué se queda?
   - **Opciones:**
     - a) Enviarlo todo: cuanto más contexto tenga el modelo, mejor extraerá.
     - b) **Retirar cada campo y medir con los evals si la extracción empeora; lo que no mueve la métrica no vuelve al prompt.** ✅
     - c) Dejar solo el relato, porque cualquier otro dato es siempre innecesario.
     - d) Mantener todos los campos pero seudonimizarlos, con lo que la minimización deja de aplicar.
   - **Explicación:** La minimización se demuestra, no se opina, y los evals convierten la discusión en un experimento reproducible que además sirve como justificación documentada de proporcionalidad. La (a) es el hábito que llevó el IBAN al prompt. La (c) es una regla ciega: algunos campos de la póliza sí mejoran la extracción y su envío está justificado. La (d) es falsa: los datos seudonimizados siguen siendo datos personales y la minimización se sigue aplicando.

## Lab

Inventario de datos y respuesta a una solicitud de supresión sobre el flujo de FNOL de Meridiana.

**Enunciado.** Meridiana recibe una solicitud de supresión de una asegurada cuyo siniestro incluye lesiones. Tienes que producir dos documentos: el inventario de dónde vive su relato y la respuesta razonada a la solicitud, distinguiendo lo que se borra, lo que se conserva y por qué. La plantilla está en `content/caso/plantillas/B5-inventario-y-supresion.xlsx` (una hoja por documento).

**Pasos:**

1. Rellena la hoja *Inventario* con una fila por almacén: sistema, contenido, categorías de datos, identificador de búsqueda, quién accede, plazo actual y plazo justificado. Mínimo seis filas: si te salen menos, faltan almacenes.
2. Marca en rojo cada fila cuyo plazo actual sea «indefinido» o «no configurado». Esas son las decisiones que nadie ha tomado.
3. En la hoja *Supresión*, clasifica cada fila del inventario en una de las cuatro categorías de la slide 24: se borra, se conserva por obligación, se limita, o está fuera de tu alcance.
4. Para cada fila de «se conserva por obligación», escribe la norma en la que te apoyas. Donde no la sepas con certeza, márcala como pendiente y anota la fuente que abrirías. Está permitido y es lo correcto.
5. Redacta la respuesta a la interesada en un máximo de una página, en castellano llano, sin citar artículos que no hayas verificado.

**Criterios de aceptación:**

- El inventario incluye al menos un almacén que no figura en el registro de actividades de tratamiento de Meridiana. Localizarlo es el objetivo real del lab.
- Cada fila tiene un identificador de búsqueda que **no** es «buscar por nombre en texto libre».
- La respuesta a la interesada explica qué se conserva y por qué, sin usar la palabra «algoritmo» ni «inteligencia artificial» para justificar una negativa.
- Todo dato normativo que no hayas verificado en fuente primaria aparece marcado como pendiente, no como una afirmación.
- Reproducible en menos de 90 minutos, sin acceso a asesoría jurídica.

**Solución de referencia:** en `content/caso/soluciones/B5/`, con el inventario completo, la clasificación de las cuatro categorías y la carta de respuesta, incluidos los huecos que la solución deja abiertos a propósito.

## Cierre

- Recapitulación en los tres puntos de arriba.
- Mini-quiz.
- Enlace al siguiente bloque.
