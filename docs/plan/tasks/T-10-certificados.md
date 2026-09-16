# T-10 · Certificados y verificación pública

## Alcance
- Emisión automática al completar el 100 % de lecciones obligatorias: `Certificate` con código `INK-XXXX-XXXX`, hash SHA-256 de (userId, courseId, fecha, secreto).
- PDF generado en servidor (QuestPDF) con QR a `/check-certificate/{code}`; almacenado en Blob.
- Página `/check-certificate`: input de código → válido/inválido, nombre, curso, fecha.
- Compartir en LinkedIn (URL "Add to profile" prellenada).
- Revocación desde admin.

## Criterios de aceptación
- Certificado no emitible sin 100 % (test de dominio).
- Verificación pública sin login, rate-limited.
- PDF reproducible: mismo input → mismo hash.

## Dependencias
T-07, T-03.
