# T-00 · Discovery y fijación del stack

## Contexto
Existe una app React de Inkoova desplegada en Azure (rediseño "control room") y el contenido del curso Agent Engineering v3.0 en `E:/Txema/Inkoova/Formación/autodidacta/` (HTML plano, `nivel.html`, `pre-curso/`). Hay que decidir si la academia vive dentro de esa app o en un repo nuevo.

## Alcance
- Inventariar la app React actual: router, gestión de estilos, estructura de carpetas, auth actual.
- Inventariar el contenido del curso: estructura de carpetas, assets, cómo se generan los slides (`content.py`, `generate_pre.py`), array `BLOQUES` hardcodeado en `index.html`.
- Decidir y documentar en `docs/plan/DECISIONS.md` (ADR corto por decisión):
  - Monorepo (`/api`, `/web`, `/content`, `/infra`) vs repos separados.
  - Academia como nueva sección de la app existente vs app independiente en subdominio (`academy.inkoova.com`).
  - Cómo se sirve el contenido: assets estáticos versionados en blob/CDN protegidos por URL firmada vs servidos por la API.
- Crear `CLAUDE.md` raíz con stack, comandos, convenciones y mapa de carpetas.
- Esqueleto de solución: `Inkoova.Academy.Domain / Application / Infrastructure / Api`, proyecto de tests, `docker-compose.yml` con Postgres 17, Valkey y Keycloak.

## Fuera de alcance
Cualquier funcionalidad de negocio. Solo estructura y decisiones.

## Criterios de aceptación
- `docker compose up` levanta Postgres, Valkey y Keycloak sanos.
- `dotnet build` y `npm run build` pasan en el esqueleto.
- `DECISIONS.md` con al menos las 3 decisiones anteriores y su justificación.
- `CLAUDE.md` permite a una sesión nueva orientarse sin preguntar.

## Dependencias
Ninguna.
