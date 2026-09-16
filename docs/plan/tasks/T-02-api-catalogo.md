# T-02 · API de catálogo y progreso

## Contexto
API pública de solo lectura para el catálogo y API autenticada para progreso. Sin auth real todavía (stub de usuario en dev).

## Alcance
- `GET /api/courses` (publicados y `coming_soon`, con métricas: horas, nº clases, nº secciones).
- `GET /api/courses/{slug}` (temario completo; `contentRef` solo si el usuario tiene acceso o la lección es preview).
- `GET /api/me/progress/{courseSlug}`, `POST /api/me/progress/lessons/{lessonId}/complete`.
- `GET /api/certificates/{code}` público (verificación).
- Política de acceso centralizada: `IAccessPolicy.CanAccess(user, lesson)` → suscripción activa o preview.
- Cache de catálogo en Valkey (TTL 5 min, invalidación al publicar).
- OpenAPI, ProblemDetails, OpenTelemetry (traces + métricas), health checks.

## Fuera de alcance
Escritura de cursos (T-11), pagos (T-04), login real (T-03).

## Criterios de aceptación
- Tests de integración (WebApplicationFactory + Testcontainers) por endpoint, incluyendo el caso "sin acceso → sin contentRef".
- p95 de `GET /api/courses` < 50 ms con cache caliente.
- Trazas visibles en el collector OTel local.

## Dependencias
T-01.
