# T-06 · Catálogo y ficha de curso

## Alcance
- `/cursos`: grid completo con filtros por nivel y estado.
- `/curso/{slug}`: portada, resumen, "qué aprenderás", requisitos (enlaza al quiz de admisión si el curso lo tiene), instructor, temario desplegable por sección con duración por lección, lecciones `isFreePreview` abribles sin login, CTA contextual (suscribirse / continuar donde lo dejaste).
- Estado del alumno en la ficha: % completado, siguiente lección.
- Página `coming_soon`: temario tentativo + "avísame" (captura email en tabla `waitlist`).

## Fuera de alcance
Reproducción de contenido (T-07).

## Criterios de aceptación
- Ficha renderiza los dos cursos reales con su temario importado.
- Usuario sin suscripción ve el temario pero los `contentRef` no llegan al cliente.
- Playwright: preview accesible sin login; lección no preview redirige a pricing.

## Dependencias
T-02, T-05.
