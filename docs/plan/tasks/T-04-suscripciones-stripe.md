# T-04 · Suscripciones y pagos (Stripe)

## Contexto
Replica el pricing de 3 planes (mensual / trimestral / anual) con beneficios crecientes. Planes propuestos (ajustar precios): `Starter` 1 mes · `Pro` 3 meses · `Elite` 12 meses.

## Alcance
- Seed de `Plan` con `stripePriceId` (productos creados por script idempotente vía Stripe API, no a mano).
- `POST /api/checkout/{planCode}` → Stripe Checkout Session; `GET /api/billing/portal` → Customer Portal.
- Webhook `POST /api/webhooks/stripe` con verificación de firma e idempotencia (tabla `stripe_events`): `checkout.session.completed`, `invoice.paid`, `invoice.payment_failed`, `customer.subscription.updated|deleted`.
- `Subscription` como única fuente de verdad de acceso; `IAccessPolicy` la consulta.
- Periodo de gracia configurable en impago (default 3 días).
- Emails transaccionales (bienvenida, pago fallido, cancelación) vía proveedor SMTP/Resend, plantillas en `/emails`.
- Frontend: sección `#pricing` en landing, página `/cuenta/suscripcion` con estado, renovación y botón al portal.

## Fuera de alcance
Facturación fiscal Verifactu (T-14). Cupones (backlog).

## Criterios de aceptación
- Test de webhook con payloads reales de Stripe CLI, evento duplicado no altera estado.
- Compra en modo test desbloquea el curso en < 5 s tras el webhook.
- Cancelación mantiene acceso hasta `currentPeriodEnd`.

## Dependencias
T-01, T-03.

## Añadido (pago único + packs)
- Stripe Checkout en modo `payment` para cursos sueltos, packs, bundle de packs y acceso vitalicio al programa; modo `subscription` para los planes.
- Precios propuestos (ajustables en admin): mensual 19 · trimestral 49 · semestral 79 · anual 129 · programa vitalicio 249 · pack 9,90 · bundle 6 packs 29.
- Al activar plan anual/vitalicio se crean `Entitlement` de origen `plan_included` para todos los packs; al expirar el anual se retiran (los comprados sueltos se conservan).
- Webhook `payment_intent.succeeded` / `checkout.session.completed` → `Purchase` + `Entitlement`.
- Página de pack: "incluido en tu plan" vs "comprar 9,90 €" según entitlement.
