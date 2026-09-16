# C-03 · B2 · Observabilidad de sistemas con LLM

> Curso: `agentes-en-produccion` · bloque `B2`

## Objetivo

Poder responder «por qué hizo eso» sobre una ejecución concreta de hace tres semanas, sin adivinar.

## Guion de slides

28 slides de contenido. Una idea por slide, con un ejemplo real o del caso Meridiana. Nada de relleno.

### 1. La pregunta que hay que poder responder: por qué hizo eso

Tres semanas después. Un tramitador abre el expediente SIN-2026-0142, ve que el agente lo sacó del flujo automático y lo puso en la cola humana, y pregunta por qué: en el relato del asegurado no hay una sola palabra sobre heridos. Otro día la pregunta llega desde fuera, y es peor: por qué el sistema propuso 1.480 € y no 1.520 €, justo por debajo del umbral de aprobación de un clic.

Ésas son las dos preguntas que examina este bloque. No «¿está el servicio levantado?», sino «¿qué hizo el sistema en **esta** ejecución, con qué información, y bajo qué versión de sí mismo?».

Para contestar sin adivinar hacen falta cinco cosas guardadas:

- Qué entró exactamente: relato, póliza, fecha de recepción, canal.
- Qué extrajo el modelo campo a campo, incluidos los que dejó vacíos.
- Qué tools se llamaron, con qué argumentos y qué devolvieron.
- Qué regla determinista disparó, y con qué valores de entrada.
- Qué versión de prompt y qué modelo estaban activos ese día.

Si falta cualquiera de las cinco, lo que puedes ofrecer es una reconstrucción a mano con el código delante. Eso no es una respuesta: es una hipótesis con buena presentación.

> Observabilidad no es enterarte de que algo falló. Es poder explicar una decisión concreta, meses después, a alguien que no tiene por qué creerte.

Con 32.000 siniestros al año, esta pregunta no es excepcional: llega varias veces por semana.

### 2. Por qué los logs de siempre no bastan aquí

Los logs de una aplicación CRUD funcionan porque cada línea es un hecho completo y cerrado: «expediente actualizado», «correo enviado». En un agente, cada línea es un fragmento de un razonamiento que duró once segundos, cuatro llamadas y dos tools, y a 88 siniestros al día esos fragmentos llegan entrelazados con los de otros veinte expedientes.

Esto es lo que produce hoy `meridiana-agent`:

```text
14:22:01 INFO llamando al modelo
14:22:03 INFO extraccion ok
14:22:03 INFO consultando poliza
14:22:04 INFO llamando al modelo
14:22:09 WARN derivado
```

Tres cosas que no puedes hacer con eso:

- **Atribuir.** Ninguna línea dice a qué siniestro pertenece. Con concurrencia, ni siquiera el orden es fiable.
- **Medir.** «Tardó» no está en ninguna parte: hay que restar marcas de tiempo a ojo, y sólo si las líneas son del mismo caso.
- **Explicar.** `WARN derivado` no dice por qué. Y ese porqué es justo lo que va a preguntar el tramitador.

La tentación es añadir campos hasta que la línea lo cuente todo. Entonces aparece el problema de verdad: el relato del asegurado acaba en el log, con su nombre y su matrícula, en un fichero que lee media empresa. Volveremos a eso en la slide 7.

Lo que falta no es más log. Es una estructura que sepa que estas cinco líneas son **una sola ejecución** con una forma de árbol.

### 3. Trazas: el vocabulario mínimo (span, atributo, contexto)

Cuatro palabras, y con ellas se entiende el resto del bloque.

- **Span.** Una operación con principio, fin y resultado. «Llamada al modelo», «consultar póliza», «iteración 2 del bucle».
- **Traza.** El árbol de spans de una ejecución completa. Un FNOL de Meridiana es una traza.
- **Atributo.** Un par clave-valor colgado de un span: `claim.id`, `llm.tokens.output`, `tool.result`. Es donde vive el porqué.
- **Contexto.** Lo que viaja de un span a su hijo para que el árbol se reconstruya, aunque el hijo se ejecute en otro proceso o dos horas más tarde desde una cola.

La traza de un FNOL tiene esta forma:

```text
fnol.procesar                          claim.id=SIN-2026-0142  1.180 ms
├── fnol.validar                                                  4 ms
├── agente.iteracion  n=1                                       620 ms
│   ├── llm.completar   prompt=fnol-extraccion@v7               610 ms
│   └── tool.consultar_poliza  poliza=PA-88231                   38 ms
├── agente.iteracion  n=2                                       410 ms
│   └── tool.consultar_coberturas                                31 ms
└── triaje.decidir     via=derivacion  motivo=posibles_lesiones    2 ms
```

Fíjate en el último span. Dura dos milisegundos, no llama a nadie y es el más importante de la traza: es la decisión. Un sistema instrumentado sólo en las llamadas de red mide bien lo que tarda y no registra lo que hace.

### 4. Instrumentar el loop: un span por iteración

La unidad natural de instrumentación del orquestador es la vuelta del bucle. Un span por iteración, anidado bajo el span del caso, con los atributos que explican por qué hubo otra vuelta.

```python
with tracer.start_as_current_span("agente.iteracion") as span:
    span.set_attribute("agente.iteracion.n", n)
    span.set_attribute("agente.presupuesto_ms_restante", presupuesto.restante_ms)
    decision = await self._paso(estado)
    span.set_attribute("agente.decision", decision.tipo)      # tool | responder | derivar
    span.set_attribute("agente.motivo_continuar", decision.motivo)
```

`agente.motivo_continuar` es el atributo que casi nadie pone y el que más sirve. Cuando un FNOL da ocho vueltas en vez de dos, la pregunta no es «cuántas»: es «qué le faltaba al agente en la vuelta tres que no le faltaba en la cuatro». Si cada iteración declara por qué no ha terminado, el patrón se lee de un vistazo en cincuenta trazas.

Dos errores frecuentes en esta instrumentación:

- **Un solo span para todo el bucle.** Mide la latencia total y pierde la forma. No sabrás si once segundos fueron dos vueltas lentas o siete rápidas.
- **Un span por línea de código.** Ruido. El coste de almacenamiento sube y la traza deja de leerse.

La regla práctica: un span por cosa que **pueda fallar o tardar por su cuenta**, y uno por cada decisión que quieras poder explicar después.

### 5. Instrumentar las tools: entrada, salida y error

La tool es la frontera de confianza del bloque anterior, y por eso es la frontera de observabilidad de éste. Cada llamada a `consultar_poliza`, `crear_peticion_documentacion` o `notificar` produce un span con la misma forma, y esa uniformidad es lo que permite consultarlas todas juntas.

Lo que lleva el span de una tool:

- `tool.name` y `tool.idempotente` — el segundo viene del decorador de B1, no se escribe a mano.
- `tool.args` **seleccionados**, no volcados: `poliza_id`, `expediente_id`, `documentos.count`. Nunca el diccionario entero.
- `tool.result` resumido: `ok`, filas devueltas, identificador creado.
- `tool.error.tipo` cuando falla, distinguiendo dos familias que no se tratan igual.

La distinción entre las dos familias es lo importante:

- **Error de negocio.** `poliza_no_en_vigor`, `documentos_no_reconocidos`. El sistema funciona; la respuesta es que no. No es un incidente, y no debe pintar el panel de rojo.
- **Error de infraestructura.** Timeout contra Postgres, 500 del servicio de correo. Sí es un incidente.

Mezclarlas es la causa número uno de que un panel se vuelva inútil: un pico de pólizas caducadas en enero se ve igual que una base de datos caída, y a la tercera vez nadie mira el panel.

Y una comprobación que sale gratis: el span de una tool nunca debe tener un hijo `llm.completar`. Si aparece, alguien ha metido el modelo dentro de una tool.

### 6. Qué se guarda del prompt y qué no

La tentación es guardar el prompt renderizado completo en cada span. Con 32.000 siniestros al año y prompts de miles de tokens, eso es mucho almacenamiento para guardar 32.000 veces el mismo texto con tres huecos distintos.

Lo que se guarda:

- **La identidad de la plantilla:** `prompt.id=fnol-extraccion`, `prompt.version=v7`, `prompt.hash=9f2c…`. El hash es la garantía: si alguien despliega un cambio sin subir la versión, los hashes divergen y se nota.
- **Las variables que se inyectaron**, por separado y con su tratamiento de datos personales propio (slide 8).
- **Los parámetros de la llamada:** modelo, temperatura, tope de salida, schema exigido.
- **La salida estructurada del modelo**, entera. Ocupa poco y es la mitad de la explicación.

Lo que no se guarda: el texto renderizado. Se **reconstruye** cuando hace falta, aplicando la plantilla `v7` a las variables registradas. Eso obliga a tener las plantillas versionadas en el repositorio y recuperables por versión, que es exactamente lo que ya montaste en el curso 1.

La excepción razonable: guardar el prompt renderizado íntegro en el muestreo dirigido de la slide 12, sólo para los casos que fallaron. Ahí el volumen es pequeño y el valor de depuración, alto.

### 7. Datos personales en las trazas: el problema y sus salidas

Un FNOL de Meridiana empieza así: «*me dio por detrás en la M-30 el martes, iba con mi hija y se quejaba del cuello*». Eso es, en una frase, un dato de localización, una relación familiar y una referencia a un posible daño físico.

Si guardas el relato en crudo en la traza, has creado una copia de datos personales en un sistema que:

- no estaba en el inventario cuando se diseñó el tratamiento,
- suele tener permisos mucho más laxos que la base de datos del expediente,
- muchas veces vive en un proveedor externo de observabilidad,
- y del que nadie sabe decir cuánto duran las cosas.

El régimen es el de categoría especial, y no cambia por estar dentro de una traza. El artículo 9.1 del RGPD prohíbe tratar datos relativos a la salud salvo que aplique una de las excepciones del 9.2, y el artículo 4.15 define «datos relativos a la salud» de forma deliberadamente amplia: cualquier dato sobre la salud física o mental —pasada, presente o futura— de una persona, venga de donde venga. Un parte de lesiones copiado en el atributo de un span entra ahí de lleno.

Para un expediente de siniestro la excepción que suele aplicar es la del artículo 9.2.f, «la formulación, el ejercicio o la defensa de reclamaciones». Y esa excepción ampara el **expediente**, no la traza: nadie defiende una reclamación con el volcado de observabilidad. Es exactamente por eso que la traza no puede llevar el relato.

Cuatro salidas, de peor a mejor:

- **No guardar nada del relato.** Barato y deja la traza sin poder explicar la extracción. Sirve de poco.
- **Guardar el relato entero y restringir el acceso.** Pasas el problema a la gestión de permisos de una herramienta que no controlas.
- **Guardar una referencia.** El span lleva `claim.id`; el relato vive sólo en Postgres, con sus permisos y su retención. Para investigar, se cruza. Es la opción por defecto de este curso.
- **Guardar una versión redactada.** Cuando de verdad necesitas el texto en la traza. Es la slide siguiente.

La decisión se toma una vez, se escribe, y se hace cumplir en el exportador. No en la disciplina de quien instrumenta.

### 8. Redacción y seudonimización antes de exportar

La redacción se aplica en un procesador de spans, entre el SDK y el exportador. Un solo sitio, que se prueba, y por el que pasa todo lo que sale del proceso. Ponerla en cada `set_attribute` es garantizar que alguien se olvide en tres meses.

```python
class RedactorSpanProcessor(SpanProcessor):
    def on_end(self, span):
        for clave, valor in list(span.attributes.items()):
            if clave not in ATRIBUTOS_PERMITIDOS:
                span.set_attribute(clave, "<omitido>")     # lista blanca, no negra
            elif clave in ATRIBUTOS_SEUDONIMIZABLES:
                span.set_attribute(clave, hmac_estable(valor))
```

Dos decisiones dentro de esas cuatro líneas:

- **Lista blanca, no lista negra.** Un atributo nuevo que nadie ha clasificado sale omitido, no sale entero. La lista negra falla justo el día que alguien añade `tool.args.nombre_asegurado`.
- **HMAC con clave, no hash a secas.** El espacio de matrículas españolas es pequeño: un SHA-256 sin clave se revierte con una tabla en una tarde. Con HMAC y clave guardada aparte, el valor sigue sirviendo para correlacionar —la misma matrícula da siempre el mismo seudónimo— y deja de ser reversible por quien tenga la traza.

Sobre el relato libre, sé honesto: detectar y tapar entidades en texto libre en español funciona a ratos. Nombres poco frecuentes, apodos y direcciones descritas a mano se escapan. Por eso la opción por defecto de la slide anterior es la referencia, y el texto redactado se reserva para el muestreo de fallos.

### 9. Métricas que importan: latencia, tokens, coste, tasa de derivación

Las trazas responden «qué pasó en este caso». Las métricas responden «cómo va el sistema». Son series temporales baratas, agregadas, sin identificadores dentro, y son lo que se mira todos los días.

Cuatro familias, y para cada una lo que hay que mirar de verdad:

- **Latencia de extremo a extremo**, en percentiles. La media miente: si p50 son 8 s y p99 son 47 s, el asegurado que abandona el formulario está en el p99, y la media de 9 s no lo ve. Segmentada por canal: portal y app no se parecen.
- **Tokens por caso**, entrada y salida por separado. La entrada crece sola cuando el contexto se arrastra; la salida crece cuando el modelo se pone a explicarse. Los dos síntomas son distintos y tienen arreglos distintos.
- **Coste por caso**, derivado de lo anterior. Cifra de ejemplo del curso, no de ningún proveedor: si un FNOL cuesta 0,02 €, 32.000 al año son 640 € y a nadie le importa; si un cambio lo lleva a 0,20 €, son 6.400 € y sí importa. Lo que se vigila es el **cambio**, no el valor.
- **Tasa de derivación**, el porcentaje de siniestros que salen del flujo automático. Es la métrica de negocio: cada punto son unas 320 derivaciones al año y horas de los 24 tramitadores.

Ninguna de las cuatro lleva `claim.id`. Eso es la slide 19.

### 10. La métrica que casi nadie pone: iteraciones por resolución

Cuántas vueltas del bucle necesita el agente para cerrar un FNOL. Es la métrica que antes avisa de que algo se ha torcido, y casi nunca está en el panel.

Sirve porque **se mueve antes que las demás**. Un cambio de prompt que empeora la extracción no sube la tasa de error el primer día: el agente lo compensa dando más vueltas. Sube el coste, sube la latencia y sólo después empiezan a fallar casos. Las iteraciones se mueven en el primer despliegue.

Y hay que mirarla como distribución, no como media. En Meridiana el histograma sano es bimodal:

- **2 iteraciones:** el caso limpio. Extrae, consulta la póliza, decide. La mayoría.
- **4 o 5:** falta un dato, pide documentación, reevalúa.
- **Por encima de 6:** casi siempre patológico. El agente está dando vueltas sobre un dato que no va a conseguir.

Una media de 2,8 puede ser «todo el mundo hace dos o tres» o «casi todos hacen dos y un 8 % hace nueve». Son dos sistemas distintos y sólo uno tiene un problema.

Y un límite duro además de la métrica: si el bucle llega a la vuelta ocho, se para y se deriva. Un agente que da vueltas indefinidamente sobre un siniestro no es un caso lento: es un caso perdido que además cuesta dinero.

### 11. Muestreo: por qué guardar el 100 % es caro y el 1 % es inútil

El muestreo se discute mal porque se discute en abstracto. Con los números de Meridiana se decide en dos minutos.

32.000 siniestros al año son 88 al día, unos 2.700 al mes. Eso no es volumen: un servicio web serio hace más peticiones en un minuto. **Guardar el 100 % de las trazas de Meridiana es perfectamente viable**, y decirlo importa porque medio sector muestrea por costumbre heredada de sistemas mil veces más grandes.

Lo que sí es voluminoso no es el número de trazas, sino lo que cuelga de cada una: prompts renderizados, respuestas completas del modelo, resultados de tools con la póliza entera dentro. Una traza de FNOL con todo puede pasar de 200 KB; sin los volcados grandes, baja a menos de 10 KB.

De ahí las dos palancas, en el orden correcto:

- **Primero adelgaza el span.** Resúmenes en vez de volcados, referencias en vez de copias. Suele bastar.
- **Después, si hace falta, muestrea.**

Y el otro extremo, el 1 % aleatorio: con 88 casos al día, guardas menos de uno. El día que un tramitador pregunte por SIN-2026-0142, la probabilidad de tenerlo es del 1 %. Has pagado almacenamiento para no poder responder ninguna pregunta concreta. Muestrear un 1 % de un sistema pequeño es lo peor de las dos opciones.

Si el pico de granizo mete 600 en 24 horas, sigue siendo un día flojo para cualquier sistema de trazas.

### 12. Muestreo dirigido: guardar siempre los casos que fallan

Cuando toque muestrear —porque el volumen crece o porque las trazas son gordas—, la decisión no se toma al empezar la traza, sino al terminarla. Se llama muestreo por cola, y cambia la pregunta de «¿guardo este caso?» a «¿ha resultado ser interesante este caso?».

La política de Meridiana, en orden de prioridad:

- **Se guarda siempre, entero y con prompt renderizado:** todo lo que terminó en error de infraestructura, toda derivación, todo caso por encima del p95 de latencia, todo caso con más de cinco iteraciones, y todo caso donde el detector de inyección saltó.
- **Se guarda siempre en versión ligera:** todo lo demás. Sin prompt renderizado, sin volcados de tools.
- **Nunca se muestrea fuera:** el `claim.id`, el resultado y la decisión. Eso va a un registro de decisiones aparte, con retención propia (slide 17).

La trampa del muestreo por cola: para decidir al final hay que haber ido acumulando los spans en algún sitio hasta que la traza cierra. Con FNOL de segundos, el búfer es minúsculo. Con la reevaluación nocturna por lotes, que dura horas, ya no lo es, y hay que decidir por adelantado para esa ruta.

Y una advertencia sobre el sesgo: si sólo miras lo que fallaste en guardar bien, tu idea del sistema es la de sus peores días. Por eso el porcentaje base de casos normales no puede ser cero.

### 13. Correlación: del ticket del cliente a la traza en dos saltos

El tramitador no tiene un `trace_id`. Tiene un número de expediente y una queja. El camino de uno a otro tiene que ser mecánico, o nadie lo hará y las trazas serán un adorno.

Dos saltos, y ninguno más:

1. **Expediente → ejecuciones.** Una tabla en Postgres, escrita por el orquestador al terminar cada ejecución.
2. **Ejecución → traza.** Un enlace directo al visor con el `trace_id`.

```sql
CREATE TABLE agent_run (
    run_id      uuid PRIMARY KEY,
    claim_id    text NOT NULL REFERENCES claim(claim_id),
    trace_id    text NOT NULL,
    started_at  timestamptz NOT NULL,
    outcome     text NOT NULL,     -- resuelto | derivado | error
    reason      text
);
CREATE INDEX ON agent_run (claim_id, started_at DESC);
```

Esa tabla vive en **tu** base de datos, no en el proveedor de observabilidad. Es deliberado: es la que sigue existiendo cuando la traza haya expirado, y la que responde «este expediente lo tocó el agente tres veces, la segunda derivó» aunque el detalle ya no esté.

El indicador de que lo has hecho bien: en la ficha del expediente, en la herramienta que usan los 24 tramitadores, hay un enlace que dice «ver qué hizo el agente». Si para llegar a la traza hay que escribir una consulta, el sistema es observable para ti y opaco para la empresa.

### 14. Alertas que se pueden atender frente a alertas que se silencian

Una alerta tiene una vida corta: se crea, salta varias veces, alguien la silencia «por ahora», y se queda silenciada dos años. Cuando salta la que importa, está enterrada entre las otras cuarenta.

Cuatro requisitos para que una alerta merezca despertar a alguien:

- **Un síntoma que note alguien de fuera.** «La tasa de error del FNOL supera el 5 % durante 10 minutos», no «la CPU está al 80 %».
- **Un umbral con historia.** Sacado de tus percentiles reales, no de un número redondo. Si el p99 de latencia son 40 s, alertar a 30 s es alertar todos los días.
- **Una acción escrita.** Qué mirar primero, qué se puede apagar, a quién se llama. Sin eso, la alerta traslada el problema, no lo resuelve.
- **Un dueño.** Una persona, no un canal de Slack donde todos suponen que ya lo mira otro.

Las tres de Meridiana que cumplen los cuatro requisitos:

- Tasa de error de infraestructura del FNOL por encima del 5 % en 10 minutos.
- Cola de derivaciones pendientes por encima de lo que 24 tramitadores absorben en un día.
- **Derivaciones por lesiones a cero durante 24 horas.** Ésta es la buena: no salta porque haya errores, salta porque ha dejado de pasar algo que siempre pasa. Es la única que detecta que la regla no negociable se ha roto en silencio.

Alertar sobre el coste diario también vale, pero eso es un correo por la mañana, no una llamada a las tres.

### 15. Dashboards: los cuatro paneles que se miran de verdad

Un panel con veinte gráficas es un panel que nadie lee. Cuatro, cada uno respondiendo una pregunta que alguien se hace de verdad.

- **Volumen y salida.** Siniestros entrados, resueltos y derivados por hora, y el pendiente acumulado. Responde «¿vamos al día?». Es el que se mira durante el episodio de granizo, cuando entran 600 en 24 horas y hay que decidir si se abre la cola de refuerzo.
- **Salud del camino.** Tasa de error de infraestructura, latencia p50/p95/p99 y estado del proveedor del modelo. Responde «¿está roto?». Es el primero que se abre cuando salta una alerta.
- **Decisiones.** Reparto por vía —daños propios, contrario conocido, amistoso, derivación— y la derivación desglosada por motivo. Responde «¿está decidiendo como ayer?». Es el que detecta que un despliegue cambió el comportamiento sin romper nada.
- **Coste.** Tokens y euros por caso, con la serie de los últimos 30 días. Responde «¿nos estamos pasando?». Se mira una vez por semana y cuando alguien toca el prompt.

Dos reglas sobre los cuatro: **cada gráfica lleva la línea de despliegues encima**, porque la primera pregunta ante cualquier escalón es «¿qué se desplegó?»; y ninguna gráfica que nadie haya mirado en un mes sobrevive. Un panel se poda como un repositorio.

### 16. Reconstruir un caso: el recorrido completo sobre un siniestro de Meridiana

El tramitador pregunta por SIN-2026-0142, derivado hace tres semanas sin motivo aparente. Así se contesta, con lo montado hasta aquí.

**Salto 1.** `SELECT * FROM agent_run WHERE claim_id = 'SIN-2026-0142'`. Una ejecución, `outcome=derivado`, `reason=posibles_lesiones`, con su `trace_id`.

**Salto 2.** El visor, con la traza entera. Dos iteraciones, 1.180 ms, sin errores.

**La lectura.** En `llm.completar` de la iteración 1, la salida estructurada del modelo:

```json
{
  "poliza": "PA-88231",
  "fecha_siniestro": "2026-02-17",
  "hay_lesiones": null,
  "hay_lesiones_incierto": true,
  "descripcion_danos": "golpe trasero, paragolpes y portón"
}
```

Ahí está. El modelo no dijo que hubiera heridos: dijo que **no podía descartarlo**. Y el span `triaje.decidir` confirma qué hizo el código con esa duda: `regla=lesiones_inciertas_derivan`, `ruleset.version=2026.02.03`.

**La respuesta al tramitador**, en una frase: el agente no detectó lesiones, detectó que no podía descartarlas, y la regla de la casa deriva ante la duda.

**Lo que hace falta después.** Recuperar el relato: no está en la traza, está en Postgres con el `claim.id`. Dice «*iba con mi hija y se quejaba del cuello*». La duda del modelo estaba bien fundada, y el sistema hizo exactamente lo que tenía que hacer.

Cuatro minutos, ninguna hipótesis. Eso es el bloque entero.

### 17. Retención: cuánto tiempo y por qué ese tiempo

«Todo un año» no es una política de retención: es no haber decidido. Tres tipos de dato, tres duraciones, y cada una con un motivo escrito.

- **Métricas agregadas.** 13 meses, para poder comparar el granizo de este año con el del anterior. Son series pequeñas, sin datos personales, y baratas de conservar.
- **Trazas completas.** 30 días para la versión ligera, 90 para las del muestreo dirigido. El motivo es operativo: son la herramienta de depuración, y una traza de hace seis meses casi nunca se usa porque el código ya no es el mismo.
- **Registro de decisiones.** La tabla `agent_run` con la decisión, el motivo, la versión de prompt y la de reglas. Vive con el expediente y dura lo que dure el expediente. No es observabilidad: es documentación de una decisión sobre una persona.

Esa separación es lo que hace que las trazas puedan durar 90 días sin perder capacidad de rendir cuentas: cuando llegue una reclamación al año y medio, lo que responde no es la traza, es el registro de decisiones.

Los plazos legales de conservación de un expediente de siniestro no son un dato técnico y no salen de un curso: dependen del ramo, del tipo de siniestro y de si hay reclamación abierta, y quien los fija es cumplimiento. Lo que sí es decisión tuya es **dónde vive ese número**: en la configuración del proyecto, versionada y revisable, no en la consola del proveedor de observabilidad, donde nadie lo ve en una revisión de código y donde cambiarlo no deja rastro.

Y el borrado se prueba. Una retención que nadie ha verificado que se aplica es una intención.

### 18. OpenTelemetry sin acoplarse al backend

OpenTelemetry es un estándar de instrumentación, no un producto. La razón de usarlo aquí es concreta: instrumentas una vez y decides después —y puedes cambiar de opinión— quién guarda y quién pinta.

La forma de conseguirlo tiene dos reglas.

**Primera: el código sólo conoce la API.** Nada de SDK de proveedor en el orquestador ni en las tools. Si mañana `import` de un proveedor concreto aparece en `orquestador/`, tienes una migración por delante en vez de un cambio de variable de entorno. Es exactamente el argumento de `ILlmClient` de B1 aplicado a otra dependencia, y merece su propio test de arquitectura.

**Segunda: el colector en medio.** El proceso exporta por OTLP a un colector; el colector decide adónde va y filtra. Eso te da tres cosas que no tienes exportando directo: la redacción de la slide 8 se puede aplicar también ahí, el muestreo se cambia sin desplegar la aplicación, y puedes mandar la misma traza a dos destinos mientras evalúas uno nuevo.

Sobre los nombres de atributos, un consejo práctico: usa las convenciones semánticas del estándar cuando existan para lo que estás midiendo, y prefija con `meridiana.` lo que sea tuyo.

Las de sistemas con LLM siguen **en desarrollo**: OpenTelemetry las marca como *Development*, que es su nivel más bajo de estabilidad, y en 2025 las sacó del repositorio principal de convenciones a uno propio, `open-telemetry/semantic-conventions-genai`. Los nombres vigentes son del estilo `gen_ai.operation.name`, `gen_ai.provider.name`, `gen_ai.request.model` y `gen_ai.usage.input_tokens`. Compruébalos ahí antes de fijarlos: *Development* significa literalmente que pueden cambiar sin aviso, y esa es la razón de prefijar lo tuyo en vez de inventarte un `llm.*` que dentro de un año chocará con el nombre que acabe ganando.

### 19. Coste de la observabilidad y cómo se controla

La observabilidad se factura por tres cosas, y conviene saber cuál te va a doler antes de que llegue la factura.

- **Volumen ingerido.** Bytes de spans y logs. En Meridiana lo domina el tamaño del span, no el número de trazas (slide 11).
- **Retención.** Multiplicador directo del anterior.
- **Cardinalidad de métricas.** El asesino silencioso.

La cardinalidad merece explicación porque es el error que todo el mundo comete una vez. Una métrica con etiquetas crea una serie temporal **por cada combinación de valores**. Esto:

```python
contador_siniestros.add(1, {"claim.id": claim_id})   # NO
```

crea 32.000 series al año. Añade `poliza_id` y son millones. La factura se multiplica, las consultas se arrastran y la métrica no sirve para nada, porque una serie con un punto no se agrega.

La regla: **en métricas, etiquetas de baja cardinalidad** —canal, vía, motivo de derivación, versión de prompt— y ninguna que identifique un caso. Los identificadores viven en las trazas, que están hechas para eso.

Tres controles más que se ponen el primer día y evitan sustos: un tope de tamaño por atributo en el colector, para que un volcado accidental de la póliza entera no se lleve el presupuesto; un alerta sobre el volumen ingerido diario; y muestreo de logs repetitivos, que suelen ser más caros que las trazas.

### 20. Logs estructurados: qué sigue teniendo sentido loguear

Con trazas y métricas montadas, los logs no desaparecen: se quedan con lo que las otras dos no cubren. Y lo que queda es poco, que es la señal de que está bien repartido.

Sigue teniendo sentido loguear:

- **Arranque y configuración efectiva.** Qué versión del servicio, qué modelo por defecto, qué versión del catálogo de reglas, qué política de muestreo. Es lo primero que se mira cuando algo va raro tras un despliegue.
- **Errores de infraestructura con su contexto completo.** Traza asociada, tipo de fallo, reintentos hechos.
- **Eventos de auditoría.** «El usuario `tramitador-07` cambió el umbral de aprobación». Esto no es un span: no forma parte de ninguna ejecución del agente y tiene que durar mucho más.
- **Decisiones de operación.** Circuito abierto, degradación activada, cola de reproceso arrancada.

Deja de tener sentido loguear el paso a paso del bucle: eso ya son spans, y duplicarlo es pagar dos veces por el mismo dato.

Dos requisitos innegociables sobre el formato:

- **JSON, un objeto por línea.** Un log que hay que parsear con expresiones regulares no es estructurado.
- **`trace_id` y `span_id` en cada línea.** Es lo que convierte los logs en la letra pequeña de una traza en vez de en un universo paralelo. Sin eso, tienes dos sistemas de observabilidad que no se hablan.

### 21. Identificadores que atraviesan el sistema entero

Media docena de identificadores, cada uno con su ciclo de vida. Confundirlos es la causa habitual de que una investigación se atasque.

- **`claim_id`** — el expediente. Nace en el FNOL y dura años. Es el único que conoce el tramitador.
- **`idempotency_key`** — la petición de entrada. Del cliente, evita el expediente duplicado. Es de B1 y sigue vivo aquí.
- **`run_id`** — una ejecución del agente sobre un expediente. Un expediente puede tener varias: la del FNOL, la de la reevaluación nocturna, la del reproceso tras la caída del proveedor.
- **`trace_id`** — la traza de esa ejecución. Uno a uno con `run_id`, pero vive en otro sistema y muere antes.
- **`request_id`** — la petición HTTP. Muere con la respuesta.
- **`actor`** — quién lo pidió: portal, app del tramitador, job por lotes.

La relación que hay que tener clara: un `claim_id` tiene N `run_id`; cada `run_id` tiene un `trace_id`. Cuando alguien dice «la traza del siniestro» está simplificando, y esa simplificación es la que hace que se investigue la ejecución equivocada de un expediente que el agente ha tocado tres veces.

Todos viajan en el contexto de la traza y todos aparecen en cada log. La propagación por la cola es la que se olvida: si el pico de granizo mete 600 siniestros en una cola y el mensaje no lleva el contexto, la traza se corta justo donde empieza lo interesante.

### 22. Versión de prompt y de modelo como atributos de la traza

Un sistema con LLM cambia de comportamiento sin que cambie una línea de código. Cambia el prompt, cambia la versión del modelo, cambia una regla de triaje. Si esas tres cosas no están en la traza, no hay forma de atribuir un cambio de comportamiento a su causa.

Cinco atributos en cada ejecución, siempre:

- `service.version` — la versión del servicio desplegado.
- `prompt.id` y `prompt.version` — la plantilla y su versión, con el hash de la slide 6.
- `model.id` — el modelo exacto solicitado, no la familia.
- `ruleset.version` — la versión del catálogo de reglas deterministas.
- `tools.catalog.version` — qué tools estaban disponibles y con qué schema.

Con eso puedes hacer la consulta que justifica el bloque entero: «tasa de derivación por `prompt.version`». Si `v7` deriva el 14 % y `v8` el 9 %, tienes un cambio de comportamiento medido, no una impresión, y puedes decidir si es una mejora o una regresión mirando el desglose por motivo.

El detalle que se escapa: **el proveedor puede cambiar el modelo bajo un mismo identificador**. Si el sistema empieza a comportarse distinto un martes sin que nadie haya desplegado, y el modelo era el sospechoso, sin un identificador de versión concreto no puedes ni demostrarlo ni descartarlo. Guarda todo lo que el proveedor devuelva sobre qué te ha atendido, aunque hoy te parezca ruido.

### 23. Reproducir una ejecución a partir de su traza

Reproducir no es una sola cosa. Son dos, y confundirlas lleva a discusiones estériles sobre si los modelos son deterministas.

**Reproducir las decisiones.** Coges la traza, tomas las salidas del modelo y los resultados de las tools tal y como quedaron registrados, y vuelves a pasar el código determinista sobre ellos. Esto es **exactamente reproducible**: mismas entradas, misma versión de reglas, misma decisión. Es lo que contesta «¿por qué derivó?» y lo que se ejecuta en una auditoría.

```bash
meridiana-agent replay --trace 4b9f0c... --stub-llm --stub-tools
# triaje.decidir  via=derivacion  motivo=posibles_lesiones  ruleset=2026.02.03
```

**Reproducir el modelo.** Coges el prompt reconstruido y lo vuelves a lanzar contra el proveedor. Esto **no** es reproducible, ni siquiera con temperatura cero, y menos semanas después. Sirve para explorar —«¿qué diría ahora?»— y no sirve como prueba de nada.

La consecuencia práctica es de diseño, no de herramientas: **si todo lo que tiene consecuencias está en el lado determinista, todo lo que tiene consecuencias es reproducible**. La arquitectura de B1 no era una preferencia estética; era la condición para que esta slide sea posible.

Lo que hace falta guardar para que el `replay` funcione: entradas, salidas del modelo, resultados de tools, versión de prompt y versión de reglas. Es la lista de la slide 1, otra vez.

### 24. Grabar y reproducir: trazas como fixtures de test

Una traza de producción es un caso de prueba que ya viene con la respuesta puesta. Convertirla en fixture es la forma más barata de que tu suite deje de parecerse a lo que imaginaste y empiece a parecerse a lo que pasa.

El flujo:

```bash
meridiana-agent trace export 4b9f0c... --out tests/fixtures/lesiones-inciertas.json
```

El fichero lleva las entradas, las salidas del modelo, los resultados de las tools y la decisión que salió. El test lo reproduce sin red y comprueba que la decisión sigue siendo la misma.

Tres reglas antes de meter la primera:

- **Redacción obligatoria en la exportación**, no después. Un fixture es un fichero que acaba en Git y en el portátil de todo el equipo. Un relato con nombre y matrícula dentro de un repositorio es una fuga de datos con `git log` incluido.
- **Se graban los casos raros, no los normales.** Los 31 sintéticos ya cubren el camino feliz. Lo que aportan las trazas son las cinco vueltas, la póliza caducada, el intento de inyección.
- **La decisión esperada la revisa un humano.** Grabar el comportamiento actual como esperado congela también los errores actuales. Si el caso derivó y no debía, el fixture documenta el fallo, no lo bendice.

Esta biblioteca de casos reales es la entrada del bloque siguiente: sin ella, las evals continuas de B3 miden lo que te inventaste un jueves.

### 25. Paneles para negocio frente a paneles para guardia

Son dos productos distintos y se estropean al mezclarlos. La diferencia no es de estética: es de horizonte temporal y de decisión asociada.

El de **guardia** responde «¿qué está roto ahora?». Ventana de minutos a horas, actualización continua, y todo lo que sale lleva una acción posible detrás: tasa de error, latencia p99, estado del proveedor, cola de reintentos. Lo abre una persona a la que acaban de llamar.

El de **negocio** responde «¿esto está funcionando?». Ventana de semanas a meses, actualización diaria, y en unidades de la compañía, no de sistemas: siniestros tramitados sin intervención humana, tiempo medio de tramitación —el número que la dirección quiere bajar de 11 días a 4—, derivaciones por motivo, coste por siniestro frente a los 1.850 € de coste medio, propuestas por debajo y por encima del umbral de 1.500 €.

Lo que ocurre al mezclarlos:

- El de negocio se llena de p99 y latencias que a nadie de negocio le dicen nada, y deja de abrirse.
- El de guardia se llena de agregados semanales que no se mueven durante un incidente, y estorban justo cuando hay prisa.

Comparten la fuente de datos, no la pantalla. Y el de negocio tiene un requisito extra: cada número necesita una definición escrita al lado. «Tramitado sin intervención» significa cosas distintas para ti y para la dirección, y esa diferencia se descubre siempre en la peor reunión posible.

### 26. Qué mide bien el porcentaje de derivaciones y qué esconde

La tasa de derivación es la métrica favorita de cualquiera que presente un agente, y sola no dice casi nada. Mide bien una cosa: cuánto trabajo llega a los 24 tramitadores. Eso es real y es planificación.

Lo que esconde:

- **La dirección del movimiento no tiene signo.** Si baja del 14 % al 9 %, puede ser que el agente extraiga mejor, o puede ser que haya dejado de detectar lesiones inciertas. La misma cifra, dos historias opuestas, y una acaba en un juzgado.
- **Mezcla motivos que no se parecen.** Derivar por posibles lesiones, por póliza fuera de vigencia, por importe sobre el umbral de 1.500 € y por «el agente no se aclara» son cuatro cosas con cuatro dueños distintos. Sumadas, ninguna se puede gestionar.
- **No dice nada de la calidad de lo no derivado.** El 91 % que el agente resolvió solo, ¿lo resolvió bien? Esa pregunta la contesta B3, no un contador.

Cómo se arregla, y es barato: **desglosar siempre por motivo**, y vigilar cada serie por separado con la expectativa escrita. Las derivaciones por lesiones deberían moverse con la siniestralidad, no con tus despliegues; si caen la semana que tocaste el prompt de extracción, tienes una regresión, aunque el agregado tenga mejor pinta que nunca.

Y la contramétrica de la slide 14: derivaciones por lesiones **a cero** durante 24 horas es una alerta, no una buena noticia.

### 27. Errores del proveedor: distinguir caído, limitado y lento

«El proveedor falla» son al menos tres situaciones con tres respuestas distintas. Un panel que las junta en una tasa de error hace que apliques la respuesta equivocada con confianza.

- **Limitado (429).** Estás pidiendo más de lo que te dejan. Es tu problema de ritmo, no su caída. La respuesta es esperar con retroceso exponencial y algo de aleatoriedad, y bajar la concurrencia. Reintentar más rápido lo empeora. Es lo que aparece a las dos horas del granizo, cuando 600 siniestros entran por la cola.
- **Caído (5xx sostenido).** No hay ritmo que valga. La respuesta es abrir el circuito y degradar: aceptar el FNOL, guardar el relato en crudo, marcarlo «pendiente de extracción» y procesarlo cuando vuelva. Es la slide 17 de B1, y aquí lo que se añade es **enterarse** de que ha pasado.
- **Lento.** Responde, pero tarde. Es el peor de los tres porque no aparece en ninguna tasa de error: consume tu presupuesto de tiempo, agota conexiones y hace que los timeouts salten en sitios que no tienen nada que ver. Se detecta mirando la latencia del span `llm.completar` por separado de la del caso.

Tres atributos en cada llamada al modelo y los tres se distinguen solos: `llm.http.status`, `llm.duracion_ms` y `llm.reintentos`. Con eso, un pico de 429 y una caída dejan de parecerse en el panel.

Y una regla que ahorra un incidente: el circuito abierto **es un estado del sistema**, y como tal se emite como métrica y se pinta. Un sistema degradado en silencio es peor que uno caído.

### 28. El primer día con observabilidad: qué sorprende siempre

Cuando el primer panel real se enciende sobre un agente que llevaba semanas «funcionando bien», aparecen cinco cosas. Aparecen casi siempre, y conviene saberlo para no interpretarlas como un desastre.

- **La cola larga de latencia es mucho peor de lo que creías.** El p50 confirma tus ocho segundos. El p99 son cuarenta y tantos, y nadie lo había visto porque nadie miraba percentiles.
- **Hay llamadas repetidas.** El agente consulta la misma póliza tres veces en la misma ejecución porque el resultado no se arrastró entre iteraciones. Sale gratis de arreglar y baja el coste de golpe.
- **Un puñado de casos se lleva un tercio del gasto.** Los de muchas vueltas. Con el límite duro de la slide 10 se corta el sangrado el mismo día.
- **Hay fallos silenciosos.** Casos que terminaron «bien» con un campo vacío que nadie comprobó. No estaban en ninguna tasa de error porque nadie los había definido como error.
- **La distribución de motivos de derivación no es la que contaba el equipo.** Se hablaba de las lesiones; la mayoría son pólizas con recibos impagados.

Ninguna de las cinco es nueva. Todas llevaban meses ahí. Lo único que ha cambiado es que ahora se ven, y ése es exactamente el argumento del bloque: **la observabilidad no empeora tu sistema, te informa de cómo era**.

La reacción sana es una lista priorizada. La reacción insana es apagar el panel.

### 29. Ejercicio práctico 1: instrumentar el bucle y la tools de Meridiana {ejercicio:B2-ej1}

Sobre `meridiana-agent` reestructurado en B1, añade la instrumentación mínima que permita responder a la pregunta de la slide 1.

**Qué hacer:**

1. Crea un span raíz `fnol.procesar` por caso, con `claim.id`, `run.id` y `actor`.
2. Un span `agente.iteracion` por vuelta, con `n`, `agente.decision` y `agente.motivo_continuar`.
3. Un span por llamada al modelo con `prompt.id`, `prompt.version`, `model.id`, tokens de entrada y salida, `llm.http.status` y `llm.duracion_ms`.
4. Un span por tool con `tool.name`, `tool.idempotente`, los argumentos de la lista blanca y `tool.error.tipo` distinguiendo negocio de infraestructura.
5. Un span `triaje.decidir` con `via`, `motivo` y `ruleset.version`.
6. Exporta a un colector local en consola. Nada de servicios de pago.

**Criterios de aceptación:**

- Ejecutar los 31 casos sintéticos produce 31 trazas, cada una con su span `triaje.decidir`.
- Ninguna traza contiene el relato del asegurado ni ningún nombre propio. Se comprueba con una búsqueda de texto sobre la salida del colector.
- Un test de arquitectura falla si un span de tool tiene un hijo `llm.completar`.
- La traza del caso con lesiones muestra `motivo=posibles_lesiones` sin abrir el código.

### 30. Ejercicio práctico 2: del expediente a la traza en dos saltos {ejercicio:B2-ej2}

El ejercicio anterior deja trazas que sólo tú sabes encontrar. Éste las conecta con el expediente.

**Qué hacer:**

1. Crea la tabla `agent_run` de la slide 13 con su migración `db/migrations/VNNN__agent_run.sql`. Sin tocar el esquema a mano.
2. Haz que el orquestador escriba una fila al terminar cada ejecución: `run_id`, `claim_id`, `trace_id`, `outcome` y `reason`.
3. Añade el comando `meridiana-agent explain --claim SIN-2026-0142`, que imprima las ejecuciones del expediente, la decisión de cada una y el `trace_id`.
4. Implementa `meridiana-agent replay --trace <id> --stub-llm --stub-tools`, que reejecute sólo el código determinista sobre las salidas registradas.
5. Añade un procesador de spans con lista blanca de atributos y HMAC para matrícula y póliza.

**Criterios de aceptación:**

- `explain` sobre un expediente tocado tres veces devuelve las tres ejecuciones ordenadas, no una.
- `replay` da **exactamente** la misma decisión que la ejecución original, para los 31 casos.
- Cambiar `ruleset.version` y volver a hacer `replay` produce una decisión distinta en al menos un caso, y queda registrado cuál.
- Un atributo nuevo sin clasificar sale como `<omitido>`, no íntegro. Compruébalo añadiendo uno a propósito.

### 31. Mini-quiz de comprensión — B2 {quiz:B2}

Tres preguntas sobre lo que decide de verdad si este bloque está aplicado: qué lleva el span de una tool, por qué el muestreo aleatorio puro traiciona a un sistema pequeño, y qué se hace con el relato del asegurado antes de que salga del proceso.

No hay preguntas de definición. Las tres describen una situación concreta de Meridiana y piden la decisión correcta.

## Qué te llevas

- Si no puedes reconstruir una decisión de hace un mes, no tienes observabilidad.
- Guardar el prompt entero con datos personales dentro es crear un problema nuevo.
- El muestreo dirigido conserva lo que importa: los fallos.

## Mini-quiz (3 preguntas)

Una sola opción correcta por pregunta, con explicación. Se generan con el mismo formato que `assets/js/quizzes-data.js` para que el importador las recoja.

1. **Tema:** Qué atributos debe llevar el span de una tool para ser útil en una investigación
   - **Enunciado:** `consultar_poliza` devuelve un fallo en producción. ¿Qué conjunto de atributos del span permite investigarlo sin abrir el código ni consultar la base?
   - **Opciones:**
     - a) La duración y un booleano `ok`, que es lo que hace falta para el panel de latencia.
     - b) El diccionario completo de argumentos y la respuesta íntegra de la tool, para no quedarse corto.
     - c) **`tool.name`, los argumentos de la lista blanca, `tool.idempotente`, un resumen del resultado y `tool.error.tipo` distinguiendo error de negocio de error de infraestructura.** ✅
     - d) El `claim.id` y el relato del asegurado, que es lo que explica el caso.
   - **Explicación:** La (c) es la única que permite responder qué se pidió, qué pasó y si el fallo es un incidente o una respuesta legítima del negocio. La (a) mide pero no explica: `ok=false` no distingue una póliza caducada de Postgres caído. La (b) mete la póliza entera en la traza, dispara el coste y arrastra datos personales. La (d) confunde el span de la tool con el contexto del caso y guarda justo lo que no debe salir del expediente.

2. **Tema:** Por qué el muestreo aleatorio puro es mala idea en un agente
   - **Enunciado:** Meridiana procesa unos 88 siniestros al día. Alguien propone muestrear el 1 % de las trazas de forma aleatoria para controlar el coste. ¿Cuál es la objeción correcta?
   - **Opciones:**
     - a) Que el muestreo aleatorio introduce sesgo estadístico y las métricas dejarían de ser fiables.
     - b) **Que con ese volumen guardarías menos de una traza al día, y la probabilidad de tener la del caso por el que preguntan sería del 1 %: pagas almacenamiento para no poder responder ninguna pregunta concreta.** ✅
     - c) Que el muestreo hay que decidirlo siempre al empezar la traza, y al 1 % no da tiempo.
     - d) Que las trazas de un agente no se pueden muestrear porque cada una es distinta.
   - **Explicación:** El valor de una traza es responder por **un** caso concreto, y eso no se promedia. Con 88 casos diarios el volumen no es el problema: lo es el tamaño de cada span, que se ataca adelgazando el contenido y, si hace falta, con muestreo por cola que conserva errores, derivaciones y colas de latencia. La (a) confunde trazas con métricas, que van agregadas y aparte. La (c) describe justo lo contrario del muestreo por cola. La (d) no es una objeción, es una excusa.

3. **Tema:** Qué hacer con los datos personales que aparecen en el relato antes de exportarlo
   - **Enunciado:** El relato «*iba con mi hija y se quejaba del cuello*» explica por qué el modelo marcó lesiones inciertas. ¿Qué se hace con él en la traza?
   - **Opciones:**
     - a) Guardarlo íntegro en el span y restringir el acceso a la herramienta de observabilidad.
     - b) No guardar nada del relato y añadir un comentario en el código explicando el motivo.
     - c) **Guardar sólo la referencia `claim.id` y dejar el relato en Postgres con sus permisos y su retención; si hace falta el texto en la traza, pasarlo antes por el procesador de redacción con lista blanca.** ✅
     - d) Guardar el relato con un SHA-256 de cada palabra sensible, que así queda anonimizado.
   - **Explicación:** La (c) mantiene el dato en el sistema donde ya está inventariado, con permisos y plazo conocidos, y no crea una segunda copia en una herramienta que suele ser más laxa y a veces externa. La (a) traslada el problema a la gestión de permisos de un sistema que no controlas. La (b) deja la traza sin capacidad de explicar la extracción, que es lo que se pedía. La (d) es falsa seguridad: un hash sin clave sobre un espacio pequeño —matrículas, nombres— se revierte con una tabla; para correlacionar sin exponer se usa HMAC con clave guardada aparte.

## Lab

Instrumentar Meridiana con OpenTelemetry y reconstruir, desde la traza, por qué se derivó un siniestro concreto.

**Enunciado.** Partes de `meridiana-agent` con la estructura que dejó el lab de B1. Al terminar, cualquiera del equipo podrá coger un número de expediente, llegar a la traza en dos saltos y explicar la decisión sin abrir el código. La prueba final es un caso que no has visto: el compañero de al lado elige uno de los 31 y tú explicas por qué salió así, mirando sólo la traza.

**Pasos:**

1. Añade el SDK de OpenTelemetry al orquestador y levanta un colector local en Docker que exporte por consola. Ni un `import` de proveedor fuera de `adaptadores/`.
2. Instrumenta el bucle, las cinco tools y el triaje según el ejercicio 1. El span `triaje.decidir` es obligatorio: sin él no hay explicación.
3. Escribe el procesador de redacción con lista blanca de atributos y HMAC para matrícula y póliza. Ponlo en el pipeline, no en las llamadas.
4. Crea la migración de `agent_run` y escribe una fila por ejecución con `claim_id`, `trace_id`, `outcome` y `reason`.
5. Implementa `explain --claim` y `replay --trace --stub-llm --stub-tools`.
6. Emite las cuatro métricas de la slide 9 más iteraciones por resolución, y comprueba que ninguna lleva `claim_id` como etiqueta.
7. Exporta la traza del caso con lesiones a `tests/fixtures/` y escribe un test de regresión que la reproduzca sin red.

**Criterios de aceptación:**

- Los 31 casos producen 31 trazas completas, cada una con `triaje.decidir` y su `ruleset.version`.
- **Prueba del compañero:** dado un `claim_id` cualquiera de los 31, explicas la decisión en menos de cinco minutos usando `explain`, la traza y —si hace falta el texto— el expediente en Postgres.
- Buscar en la salida del colector cualquier nombre propio, matrícula o fragmento del relato no devuelve **ningún** resultado.
- `replay` reproduce la decisión original en 31 de 31. Si alguna difiere, falta algo por registrar.
- Añadir un atributo nuevo sin clasificar lo exporta como `<omitido>`. Se comprueba añadiéndolo a propósito.
- Ninguna métrica supera 50 series temporales. Si alguna las supera, hay un identificador colado en una etiqueta.
- Reproducible en menos de 60 minutos, sin servicios de pago.

**Solución de referencia:** en `content/caso/soluciones/B2/`, con el colector, el procesador de redacción y la traza esperada del caso de lesiones inciertas.

## Cierre

- Recapitulación en los tres puntos de arriba.
- Mini-quiz.
- Enlace al siguiente bloque.
