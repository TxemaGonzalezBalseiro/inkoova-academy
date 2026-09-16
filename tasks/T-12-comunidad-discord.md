# T-12 · Comunidad y beneficios por plan

## Alcance
- Vinculación cuenta ↔ Discord (OAuth2) y bot que asigna roles según plan (`starter|pro|elite`) y los retira al expirar (job diario).
- Canales exclusivos por plan; canal por curso.
- Beneficio Pro: calendario de sesiones grupales (página `/comunidad/sesiones`, eventos en BD, enlace Meet/Zoom).
- Beneficio Elite: reserva de sesión 1:1 (integración Cal.com o enlace).
- Página `/empresas`: formulario de contacto para formación in-company (Inkoova B2B).

## Criterios de aceptación
- Expiración de suscripción retira rol Discord en ≤ 24 h.
- Usuario sin plan Pro no ve el calendario de sesiones.

## Dependencias
T-04.
