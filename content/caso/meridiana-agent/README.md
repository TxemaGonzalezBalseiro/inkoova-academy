# meridiana-agent

Agente de gestión de siniestros de la aseguradora ficticia Meridiana. Es el punto de llegada
del **curso 2 (Agent Engineering v3.0)** y el punto de partida del **curso 3 (Agentes en
Producción)**.

```bash
dotnet run -- --claim SIN-2026-0007
dotnet run -- --all          # los 31 siniestros sintéticos
```

## Qué hace

El loop clásico: percibir → razonar → actuar → observar, sobre las cuatro etapas del caso
(FNOL, triaje, documentación, propuesta). El humano aprueba siempre; el agente nunca paga.

## Por qué arranca sin API key

El proveedor por defecto es `StubLlmClient`: reglas deterministas que imitan lo que
devolvería un modelo. Existe por tres razones:

1. El repositorio arranca en cualquier máquina, sin cuenta ni gasto.
2. Los evals del curso 3 necesitan un baseline reproducible con el que comparar.
3. Obliga a que el resto del sistema —tools, validación, política de derivación— esté bien
   construido, porque el "modelo" no puede tapar sus fallos.

Para usar un modelo real:

```bash
export MERIDIANA_PROVIDER=anthropic
export ANTHROPIC_API_KEY=sk-ant-...
dotnet run -- --claim SIN-2026-0007
```

> TODO(C-00): `AnthropicLlmClient` se implementa en el bloque B1 del curso 2. En este repo de
> partida solo está declarado el puerto: pedirle al alumno que lo escriba es el ejercicio.

## Reglas que no se saltan

Están en `Policy/ClaimPolicy.cs`, en código, **fuera del prompt**:

- Si hay lesiones personales, el agente deriva. Siempre.
- Si la fecha del siniestro cae fuera de la vigencia de la póliza, deriva.
- Si falta un dato esencial (matrícula o fecha), deriva.
- Por encima de 1.500 € la propuesta exige revisión completa.
- El agente nunca marca un siniestro como pagado.

Que estén en código y no en el prompt es el punto pedagógico entero: un modelo puede ser
persuadido; un `if` no.

## Estructura

```
Program.cs                 Entrada por consola
Agent/ClaimAgent.cs        El loop
Agent/Tools.cs             Las tools que el agente puede invocar
Agent/ILlmClient.cs        Puerto del modelo + StubLlmClient
Policy/ClaimPolicy.cs      Reglas deterministas
Data/CaseRepository.cs     Lee los datos sintéticos de ../datos
```

## Datos

Los lee de `../datos/`, generados con `python ../generar_datos.py`. Son siempre los mismos:
misma semilla, mismos 31 siniestros.
