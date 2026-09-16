# C-21 · Proyecto final y certificado de programa

> Cierra los cuatro cursos. Se entrega al terminar Gobernanza EU.

## Enunciado

Llevar el agente Meridiana —o uno propio con un caso equivalente— a **listo para
producción**. No se pide que esté desplegado: se pide que esté en condiciones de estarlo y
que puedas demostrarlo.

El entregable es un repositorio y un expediente. Nada más, y nada menos.

## Qué se entrega

1. **Repositorio** con:
   - Arquitectura por capas y las reglas críticas en código, fuera del prompt (curso 3, B1).
   - Observabilidad con trazas que permitan reconstruir una decisión concreta (B2).
   - Evals en CI con umbrales que bloquean el merge (B3).
   - Coste por caso instrumentado y presupuesto por ejecución (B4).
   - Pruebas adversariales en el conjunto de evaluación (B5).
   - Plan de despliegue progresivo escrito, con criterios de promoción y reversión (B6).
   - Runbooks e interruptor de apagado (B7).

2. **Expediente técnico** con:
   - Clasificación de riesgo bajo el AI Act, **justificada por escrito** (curso 4, B2).
   - Mapa de roles proveedor/deployer y qué se exige por contrato (B3).
   - Documentación técnica según el índice del Anexo IV (B4).
   - Tratamiento de datos personales y, si procede, evaluación de impacto (B5).
   - Gobernanza mínima: inventario, roles y vigilancia poscomercialización (B6).

## Rúbrica de autoevaluación

Marca solo lo que puedas demostrar señalando un fichero o una traza. «Lo tengo pensado» no
cuenta.

### Arquitectura y código (25 %)

- [ ] Las reglas con consecuencias reguladas están en código, no en el prompt.
- [ ] El sistema funciona (degradado) con el proveedor del modelo caído.
- [ ] Reprocesar el mismo caso dos veces no duplica efectos.
- [ ] El modelo es sustituible sin tocar el loop.

### Observabilidad (15 %)

- [ ] Puedo reconstruir por qué el sistema decidió X en un caso concreto de hace un mes.
- [ ] Las trazas no contienen datos personales sin tratar.
- [ ] Los casos que fallan se guardan siempre, aunque haya muestreo.

### Evals (20 %)

- [ ] Hay un conjunto de evaluación con casos difíciles a propósito.
- [ ] Los evals corren en CI y bloquean el merge por debajo de un umbral.
- [ ] Las métricas están segmentadas, no solo agregadas.
- [ ] Al menos un eval cubre una regla de seguridad concreta.

### Coste y operación (15 %)

- [ ] Sé el coste por caso y el coste mensual estimado.
- [ ] Hay presupuesto por ejecución que corta un bucle caro.
- [ ] Existen runbooks por modo de fallo, no uno genérico.
- [ ] Se puede apagar el agente y seguir operando.

### Gobernanza (25 %)

- [ ] La clasificación de riesgo está escrita y argumentada, con sus fuentes.
- [ ] El mapa de roles está claro y lo que exijo al proveedor está por contrato.
- [ ] El expediente técnico sigue el índice del Anexo IV y está completo.
- [ ] Cada afirmación normativa lleva artículo y fecha de verificación.
- [ ] La gobernanza dice quién mantiene esto cuando yo no esté.

## Tiempo estimado

Entre 8 y 12 horas si has hecho los labs de los cuatro cursos. Si partes de cero con un caso
propio, cuenta el doble.

## Entrega y revisión

- **Autoevaluación:** cualquier alumno, con la rúbrica de arriba.
- **Revisión con devolución escrita:** incluida en el plan Elite.

## Certificado de programa

Se emite al completar los cuatro cursos y marcar el proyecto como entregado. Lleva código
público verificable, igual que los de curso, y el ámbito «programa» en lugar de un curso
concreto.

`TODO(C-21)`: definir el criterio exacto de «proyecto entregado» — autodeclaración frente a
revisión — antes del lanzamiento. La emisión técnica ya está implementada en
`IssueProgramCertificateHandler`.
