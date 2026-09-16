# T-16 · Códigos de descuento y programa de afiliados (influencers)

## Contexto
Un influencer recibe un % de las ventas netas atribuidas a él. Debe ser auditable (él ve sus ventas), fiscalmente correcto (él factura, o autofactura pactada) y resistente a fraude.

## Modelo
- `Affiliate` (userId, código público p.ej. `TXEMA20`, % comisión, estado, datos fiscales: NIF/VAT, país, IBAN, método de pago).
- `DiscountCode` (código, % o importe, productos aplicables, validez, límite de usos, `affiliateId` opcional, `stripePromotionCodeId`).
- `Referral` (visitorId, affiliateId, primer clic, expira a 30 días, last-click gana).
- `Commission` (purchaseId, affiliateId, base neta, %, importe, estado `pending|approved|paid|reversed`).
- `Payout` (affiliateId, periodo, total, factura recibida/autofactura, fecha de pago).

**Base neta** = importe cobrado − IVA − comisión Stripe − reembolsos. La comisión pasa a `approved` a los 14 días (ventana de desistimiento); un reembolso la revierte.
Suscripciones: comisión sobre cada renovación durante 12 meses (configurable), no solo la primera.

## Alcance
- Atribución: `?ref=CODIGO` guarda cookie/`Referral`; el código de descuento en Checkout también atribuye (Stripe Promotion Codes con metadata `affiliateId`). Prioridad: código > cookie.
- Webhooks (T-04) crean `Commission` al confirmarse el pago y la revierten en `charge.refunded`.
- Panel afiliado `/afiliado`: clics, conversiones, comisiones por estado, histórico de liquidaciones, materiales (enlaces, banners), descarga de su liquidación mensual en PDF.
- Liquidación mensual (job día 1): genera resumen por afiliado, marca `approved` → `payout pendiente`, envía email con el importe y las instrucciones de factura. Mínimo de pago 50 €; el resto arrastra.
- Facturación: dos modos por afiliado (a) el afiliado emite factura a Inkoova por el importe (con IRPF 15 % si es profesional en España; inversión del sujeto pasivo si es UE); (b) **autofactura** acordada por escrito (art. 5 RD 1619/2012), generada por la plataforma con la librería Verifactu existente. Registro del acuerdo firmado.
- Admin (T-11): alta de afiliados, aprobar/rechazar comisiones, marcar pagadas, exportar CSV para la gestoría.
- Antifraude: bloquear autorreferidos (mismo email/IP/tarjeta), límite de usos por código, comisiones de ventas reembolsadas o con chargeback nunca se pagan.

## Fuera de alcance
Pago automático por Stripe Connect (backlog si hay >20 afiliados). Códigos por niveles/gamificación.

## Criterios de aceptación
- Compra con código atribuye, reembolso a los 5 días revierte la comisión, compra sin reembolso pasa a `approved` el día 15 (tests con reloj simulado).
- Liquidación mensual reproducible: mismo periodo → mismo PDF y mismo total.
- Un afiliado no puede ver datos de otro (test de autorización).

## Dependencias
T-04, T-11, T-14.
