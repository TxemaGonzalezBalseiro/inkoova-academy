# CLAUDE.md · Inkoova Academy

Plataforma de cursos (estilo hack4u.io) para el programa de Inkoova: Prompt Engineering,
Agent Engineering v3.0, Agentes en Producción y Gobernanza EU, más packs sectoriales.

El plan completo vive en `docs/plan/`. Las decisiones cerradas, en `docs/plan/DECISIONS.md`.
Léelas antes de proponer arquitectura: ya están decididas.

## Stack

| Capa | Tecnología |
|---|---|
| API | .NET 10, Clean/Hexagonal, Minimal APIs, `Result<T,E>` |
| Datos | PostgreSQL 17, **Dapper** (nunca EF Core), migraciones SQL con DbUp |
| Auth | ASP.NET Core Identity en la misma BD, JWT 15 min + refresh rotatorio |
| Pagos | Stripe Billing (Checkout, Customer Portal, webhooks) |
| Frontend | React 19 + Vite + TypeScript, React Router, CSS con tokens propios |
| Contenido | HTML plano generado por `content.py`, importado a manifest |
| Infra | 1 VPS, Docker Compose + Caddy (TLS automático) |
| Tests | xUnit + FluentAssertions + Testcontainers (backend), Playwright (frontend) |

## Mapa de carpetas

```
api/                    Solución .NET
  src/
    Inkoova.Academy.Domain/          Entidades, VOs, invariantes. Cero dependencias.
    Inkoova.Academy.Application/     Casos de uso, puertos (interfaces), DTOs.
    Inkoova.Academy.Infrastructure/  Dapper, Stripe, email, storage, Identity.
    Inkoova.Academy.Api/             Minimal APIs, auth, OpenAPI, OTel.
    Inkoova.Academy.Migrator/        Runner DbUp de db/migrations.
    Inkoova.Academy.Jobs/            Jobs programados (expiración, liquidaciones).
  tests/
    Inkoova.Academy.Domain.Tests/         Unitarios de dominio.
    Inkoova.Academy.Integration.Tests/    Testcontainers + WebApplicationFactory.
db/migrations/          VNNN__nombre.sql, solo avance, idempotentes.
web/                    SPA React (Vite).
content/
  tools/                Importador y generador de scaffolding de cursos.
  manifests/            course.manifest.json generados. Artefacto versionado.
  caso/                 Caso conductor Meridiana (C-00).
  cursos/               Fuentes de los cursos nuevos (C-02..C-14).
  packs/                Packs sectoriales (C-15..C-20).
infra/                  docker-compose, Caddyfile, bootstrap.sh, backups.
docs/plan/              Plan original + DECISIONS.md + LAUNCH.md.
```

## Comandos

El camino corto para todo es `dev.ps1`:

```powershell
.\dev.ps1 up          # contenido + Postgres + migraciones + API + web
.\dev.ps1 verify      # compila y ejecuta todas las comprobaciones
.\dev.ps1 down        # para lo levantado, conserva los datos
.\dev.ps1 help        # el resto de comandos
```

Por debajo hace esto:

```bash
# Arranque local completo (Postgres + API + web)
docker compose -f infra/docker-compose.yml up -d

# Backend
dotnet build api/Inkoova.Academy.slnx
dotnet test  api/Inkoova.Academy.slnx
dotnet run --project api/src/Inkoova.Academy.Migrator      # aplica migraciones
dotnet run --project api/src/Inkoova.Academy.Api           # API en :5080

# Frontend
npm --prefix web install
npm --prefix web run dev        # :5173
npm --prefix web run build
npm --prefix web run test:e2e   # Playwright

# Contenido
python content/tools/importer.py --source "<carpeta del curso>" --out content/manifests
python content/tools/scaffold.py                            # esqueleto de bloques C-xx
python content/caso/generar_datos.py                       # datos sintéticos del caso
```

## Convenciones no negociables

1. **Nunca EF Core.** Dapper con SQL explícito y visible en el diff.
2. **Nunca cambiar esquema a mano.** Migración `db/migrations/VNNN__*.sql`, solo avance.
3. **`Result<T, Error>`** en Domain y Application. Las excepciones son para fallos de
   infraestructura, no para reglas de negocio.
4. **Domain no referencia nada.** Ni Dapper, ni ASP.NET, ni Stripe. Comprobado por test de
   arquitectura.
5. **Sin lógica LLM en la plataforma.** Determinista por defecto. Solo donde una tarea lo pida.
6. **Dato que falta = `TODO` tipado + placeholder.** Nunca inventar cifras, plazos ni normativa.
7. **Autorización solo por `IAccessPolicy`.** Ningún endpoint decide acceso por su cuenta.
8. **Piso de calidad de UI:** navegable por teclado, responsive a 375 px, respeta
   `prefers-reduced-motion`, contraste AA.
9. Textos de cara al alumno en **español**. Identificadores, ramas y commits en inglés.

## Modelo de acceso (leer antes de tocar autorización)

Todo derecho de acceso es una fila en `entitlement`. Las fuentes son:

- `purchase` — compra única de curso, pack o programa vitalicio.
- `plan_included` — derivado de una suscripción activa; se retira al expirar (job diario).
- `manual` — beca o acceso de empresa, concedido desde admin.

`IAccessPolicy.CanAccessLessonAsync(user, lesson)` devuelve verdadero si la lección es
`is_free_preview` o si existe un entitlement vigente sobre el producto que la contiene.
Nada más consulta suscripciones directamente.

## Estado por tarea

Ver [docs/plan/STATUS.md](docs/plan/STATUS.md). El checklist de lanzamiento está en
[docs/plan/LAUNCH.md](docs/plan/LAUNCH.md).
