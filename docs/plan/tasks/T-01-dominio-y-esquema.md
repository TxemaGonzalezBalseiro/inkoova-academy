# T-01 · Modelo de dominio y esquema PostgreSQL

## Contexto
Modelo mínimo que sostiene catálogo, acceso por suscripción, progreso y certificados.

## Alcance
Entidades (Domain, sin dependencias externas):
- `Course` (slug, título, descripción corta/larga, portada, estado `draft|published|coming_soon`, nivel, `featured`, `isNew`, horas totales calculadas).
- `Section` (orden, título) → `Lesson` (orden, título, tipo `slides|video|lab|quiz`, duración min, `contentRef` al asset, `isFreePreview`).
- `User` (id Keycloak, email, displayName, createdAt).
- `Plan` (código `monthly|quarterly|yearly`, precio, moneda, beneficios JSON, `stripePriceId`).
- `Subscription` (userId, planId, estado, `currentPeriodEnd`, `stripeSubscriptionId`, `cancelAtPeriodEnd`).
- `LessonProgress` (userId, lessonId, completedAt, lastPositionRef).
- `QuizAttempt` (userId, quizId, puntuación, veredicto, respuestas JSON, fecha).
- `Certificate` (código público verificable, userId, courseId, emitidoEn, hash).
- Value objects: `Slug`, `Money`, `CertificateCode`.
- Invariantes: no publicar curso sin ≥1 lección; certificado solo con 100 % lecciones obligatorias; una suscripción activa por usuario.

Infraestructura:
- Migraciones SQL versionadas (`db/migrations/V001__init.sql`…). Índices en slugs, `(userId, lessonId)`, `certificate.code`.
- Repositorios Dapper con interfaces en Application.
- `Result<T,E>` para todas las operaciones de dominio.

## Fuera de alcance
Endpoints HTTP, Stripe, Keycloak.

## Criterios de aceptación
- Tests de dominio (xUnit + FluentAssertions) cubriendo invariantes.
- Tests de repositorio con Testcontainers Postgres: CRUD de Course/Section/Lesson y consulta de progreso.
- Migración aplicable desde cero y idempotente en CI.

## Dependencias
T-00.

## Añadido (programa + packs)
- `Product` (tipo `course|pack|program`, slug, precio único opcional, `stripePriceId`). `Course` pasa a ser un `Product` de tipo `course`.
- `Pack` (sector, versión semántica, lista de ficheros `PackFile` con hash y `contentRef`).
- `Entitlement` (userId, productId, origen `purchase|plan_included|manual`, validez). Regla única `IAccessPolicy`: suscripción activa **o** compra única **o** incluido en el plan (anual y vitalicio incluyen packs).
- `Program` agrupa productos con orden y prerequisitos; certificado de programa al completar todos.
- Compras únicas (`Purchase`: userId, productId, importe, `stripePaymentIntentId`).
