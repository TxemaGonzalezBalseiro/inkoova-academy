# T-05 · Landing pública

## Contexto
Estructura equivalente a hack4u.io adaptada a Inkoova. Design tokens ya definidos (Manrope, azul `#1E3A8A`, magenta `#DB2777`, teal `#0891B2`).

## Alcance
Secciones, en este orden:
1. Header fijo: Cursos · Precios · Empresas · Roadmap · Sobre mí · Comunidad · Login/Registro.
2. Hero: "Ingeniería de agentes y prompts para builders senior en entornos regulados" + CTA a planes + prueba social (nº alumnos, valoraciones; `TODO` hasta tener datos reales).
3. Banner "Nuevo curso" (configurable desde admin).
4. Grid "Nuestros cursos" con la tarjeta: portada, badge `Nuevo|Destacado`, horas · clases · secciones, rating, "Sólo para miembros", CTA.
5. "Próximos cursos" (estado `coming_soon`, sin CTA).
6. Bloque de ayuda: Roadmap · Verifica tu certificado · Conoce al instructor · Soporte.
7. Contadores (alumnos, cursos, labs, 24/7).
8. Pricing (consume planes de la API).
9. Footer legal + redes.
- Página `/sobre-mi` reutilizando la biografía real de la slide 7 de B0.
- Página `/faq` y `/soporte` (formulario → email).
- SEO: metatags OG/Twitter, sitemap, `robots.txt`, Lighthouse ≥ 90 en Performance/A11y/SEO.

## Fuera de alcance
Ficha de curso (T-06), player (T-07).

## Criterios de aceptación
- Todos los datos vienen de la API (cursos, planes); cero contenido hardcodeado salvo textos de marca.
- Responsive verificado en 375 px, 768 px, 1440 px.
- Lighthouse móvil ≥ 90 en las 4 categorías.

## Dependencias
T-02, T-04 (pricing).
