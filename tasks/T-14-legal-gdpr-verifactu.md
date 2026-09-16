# T-14 · Legal, GDPR y facturación Verifactu

## Contexto
Venta de suscripciones desde España a consumidores: RGPD, LSSI, derecho de desistimiento y **Verifactu (RD 1007/2023)** para las facturas. Ya existen implementaciones Verifactu previas reutilizables.

## Alcance
- Páginas: aviso legal, política de cookies (banner con consentimiento real, sin cookies no esenciales antes de aceptar), privacidad, términos y condiciones (incluye desistimiento 14 días y su excepción por contenido digital iniciado).
- Registro de actividades de tratamiento; exportación y borrado de cuenta (`/cuenta/datos`).
- Facturación: por cada `invoice.paid` de Stripe generar factura fiscal con la librería Verifactu existente (encadenamiento hash, QR, envío a AEAT), PDF descargable en `/cuenta/facturas`. Stripe Tax para IVA UE/OSS.
- Rectificativas en reembolsos.

## Fuera de alcance
Asesoría fiscal (revisar textos con gestor).

## Criterios de aceptación
- Factura Verifactu válida generada en sandbox AEAT por cada pago de test.
- Borrado de cuenta elimina datos personales y conserva solo lo fiscalmente obligatorio.
- Banner de cookies bloquea analítica hasta consentimiento (verificado con Playwright).

## Dependencias
T-04.
