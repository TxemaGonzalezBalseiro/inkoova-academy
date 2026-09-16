# Inkoova Academy — Plan de desarrollo (tareas para Claude Code)

Plataforma de cursos estilo hack4u.io para los dos cursos de Inkoova:
**Prompt Engineering** y **Agent Engineering v3.0**.

## Referencia: qué tiene hack4u.io que replicamos
- Landing: hero + prueba social, "nuevo curso" destacado, grid de cursos (horas / clases / secciones / rating / "Sólo para miembros"), "Próximos cursos", bloque de ayuda (Roadmap, Verifica certificado, Instructor, Soporte), pricing 3 planes.
- Ficha de curso pública con temario; contenido solo para suscriptores.
- Suscripción (mensual / trimestral / anual) con beneficios crecientes (comunidad Discord, sesiones grupales, 1:1).
- Roadmap de aprendizaje, verificación pública de certificados, FAQ, soporte, páginas legales.

## Decisiones de partida (ajustables en T-00)
| Área | Decisión |
|---|---|
| Backend | .NET 10 LTS, Clean/Hexagonal, Dapper (nunca EF Core), `Result<T,E>`, PostgreSQL 17 |
| Auth | ASP.NET Core Identity (misma BD). Keycloak → backlog B2B |
| Pagos | Stripe Billing (Checkout + Customer Portal + webhooks). Facturas sujetas a **Verifactu** (RD 1007/2023) |
| Frontend | React (app Inkoova existente) + Vite, tokens del design system ya definido (Manrope, azul `#1E3A8A`, magenta `#DB2777`) |
| Contenido | El curso ya existe como HTML plano (slides con IntersectionObserver, `nivel.html`, `pre-curso/`). Se **importa y sirve**, no se reescribe |
| Infra | 1 VPS (~4 €/mes) con Docker Compose + Caddy. Ver T-13 |
| Tests | xUnit + FluentAssertions + Testcontainers (Postgres). Playwright en frontend |

## Fases y orden
| Fase | Tareas | Resultado |
|---|---|---|
| 0 Cimientos | T-00 → T-03 | Repo, dominio, API catálogo, auth |
| 1 Producto mínimo vendible | T-04 → T-08 | Landing, catálogo, player con el curso real, pagos |
| 2 Completar la academia | T-09 → T-12 | Roadmap, certificados, admin, Discord |
| 3 Producción | T-13 → T-16 | Infra, legal/Verifactu, lanzamiento |

Hito de valor: al cerrar la **Fase 1** ya se puede vender y consumir el curso de Agent Engineering.

## Cómo usarlo con Claude Code
1. Copia esta carpeta a `docs/plan/` en el repo.
2. Una tarea por sesión, en orden. Prompt tipo:
   > Implementa `docs/plan/tasks/T-02-api-catalogo.md`. Respeta las convenciones del README y del CLAUDE.md. No toques nada fuera del alcance.
3. Revisa el diff, ejecuta tests y arranca la app antes de pasar a la siguiente.

## Convenciones (aplican a todas las tareas)
- Cada tarea: **Contexto / Alcance / Fuera de alcance / Criterios de aceptación / Dependencias**.
- Sin lógica LLM en la plataforma salvo donde una tarea lo pida explícitamente. Determinista por defecto.
- Cambios de esquema siempre por migración versionada (DbUp o Flyway), nunca a mano.
- Todo dato que falte: `TODO` tipado + placeholder, nunca inventar.
- Piso de calidad: a11y por teclado, responsive hasta móvil, `prefers-reduced-motion`.

## Presupuesto objetivo
VPS ~4–5 €/mes + dominio ~10 €/año + Stripe (solo % por venta). Todo lo demás en free tier. **< 10 €/mes** hasta cientos de alumnos.

## Programa (contenido) — ver `content-tasks/`
| # | Curso | Estado | Tareas |
|---|---|---|---|
| 1 | Prompt Engineering para builders | existe, se extiende | C-01 |
| 2 | Agent Engineering v3.0 | existe | — |
| 3 | Agentes en Producción | nuevo | C-02 → C-08 |
| 4 | Gobernanza y normativa europea | nuevo | C-09 → C-14 |
| P | Packs sectoriales (descarga) | nuevo | C-15 → C-20 |
| — | Caso conductor + proyecto final + certificado de programa | nuevo | C-00, C-21 |

Caso conductor: **"Aseguradora Meridiana"**, agente de gestión de siniestros que atraviesa los 4 cursos.
