# Checklist de lanzamiento (T-15)

Nada de esto se marca por intuición. Cada casilla se marca **después de ejecutar la
comprobación** y anotando el resultado. Una casilla marcada sin evidencia es peor que una sin
marcar, porque nadie la volverá a mirar.

Fecha objetivo: `TODO`
Responsable: `TODO`

---

## 1. Legal y fiscal · bloqueante

- [ ] Datos identificativos de la empresa rellenados en aviso legal, privacidad y facturas
      (razón social, NIF, domicilio, datos registrales, email de contacto).
- [ ] Textos legales revisados por gestoría o asesoría jurídica. Anotar quién y cuándo.
- [ ] Banner de cookies verificado con Playwright: **sin analítica antes del consentimiento**.
- [ ] Adaptador Verifactu conectado y factura válida emitida en el entorno de pruebas de la AEAT.
- [ ] Serie de facturación decidida y su primer número reservado.
- [ ] Rectificativa probada sobre un reembolso real en modo test.
- [ ] Stripe Tax configurado para IVA de la UE y OSS.
- [ ] Registro de actividades de tratamiento redactado.
- [ ] Borrado de cuenta probado: quedan las facturas, no queda nada personal.
- [ ] Exportación de datos probada y revisada su completitud.

## 2. Pagos · bloqueante

- [ ] Cuenta de Stripe en modo real, con la información de negocio verificada.
- [ ] Precios definitivos decididos y sembrados. Confirmar que coinciden con la landing.
- [ ] Webhook apuntando al dominio de producción, con su secreto de firma.
- [ ] Evento duplicado reproducido con la CLI de Stripe: no altera el estado.
- [ ] Compra real de un plan: el acceso aparece en menos de 5 segundos tras el webhook.
- [ ] Reembolso real: el acceso se retira y se emite la rectificativa.
- [ ] Cancelación: el acceso se conserva hasta `currentPeriodEnd`.
- [ ] Impago simulado: se aplica el periodo de gracia y sale el email.

## 3. Contenido

- [ ] Curso Agent Engineering v3.0 importado y publicado.
- [ ] Fundamentos de IA Engineer importado y publicado, con P0 abierto sin plan y P1–P3 cerrados.
- [ ] Ningún fichero servido mezcla lecciones de muestra y de pago. Lo comprueba el importador
      (`ensure_access_is_per_file`), que aborta antes de escribir el manifest.
- [ ] Test de nivel accesible sin cuenta y con los tres veredictos correctos.
- [ ] Ningún `contentRef` de lección de pago llega al cliente sin acceso (test automatizado).
- [ ] El HTML original sigue abriéndose por doble clic fuera de la plataforma.
- [ ] Roadmap sembrado y coherente con los cursos publicados.

## 4. Seguridad

- [ ] Cabeceras verificadas en producción: HSTS, CSP, `X-Content-Type-Options`, `Referrer-Policy`.
- [ ] Rate limiting comprobado en registro, login, verificación de certificados y referidos.
- [ ] Todos los secretos fuera del repositorio y rotados respecto a los de desarrollo.
- [ ] Postgres sin puertos publicados al exterior.
- [ ] SSH solo por clave; `ufw` y `fail2ban` activos.
- [ ] Actualizaciones de seguridad automáticas habilitadas.
- [ ] Un token de contenido caducado devuelve 403 y el player lo renueva.
- [ ] Un afiliado no puede leer la liquidación de otro (test automatizado).
- [ ] Un usuario con rol `student` recibe 403 en `/api/admin/*`.

## 5. Recuperación ante desastres

- [ ] Backup nocturno ejecutándose y subiendo a B2.
- [ ] `restore-test.sh` ejecutado con éxito. **Anotar la fecha**: un backup sin restauración
      probada no es un backup.
- [ ] Snapshot semanal del VPS configurado.
- [ ] Tiempo de recuperación medido y anotado.
- [ ] Volumen de contenido incluido en la copia.

## 6. Rendimiento

- [ ] Lighthouse móvil ≥ 90 en las cuatro categorías sobre el dominio real.
- [ ] p95 de `GET /api/courses` por debajo de 50 ms con caché caliente. Anotar la medición.
- [ ] **Prueba de carga sobre el recorrido del player**, que es el que multiplica peticiones:
      abrir lección, pedir el documento, el chrome y los datos, y marcar progreso. Anotar
      alumnos concurrentes, p95 y dónde aparece el primer error. Sin esto, el techo de la
      máquina es una expectativa: ADR-012, PgBouncer y el ajuste de Postgres están medidos en
      funcionamiento, no en carga.
- [ ] `SHOW POOLS` de PgBouncer durante la prueba: `cl_waiting` sostenido por encima de cero
      significa que `DEFAULT_POOL_SIZE` se ha quedado corto.
- [ ] Responsive verificado a 375, 768 y 1440 px.
- [ ] Navegación completa por teclado en portada, ficha, player y checkout.
- [ ] `prefers-reduced-motion` respetado.

## 7. Observabilidad y operación

- [ ] Trazas llegando al colector y consultables.
- [ ] Alerta de caída del sitio (Uptime Kuma o equivalente).
- [ ] Alerta de fallo del webhook de Stripe.
- [ ] Alerta de error en el backup nocturno.
- [ ] Página de estado publicada.
- [ ] Canal de soporte operativo con su SLA escrito.
- [ ] Job diario verificado: revoca accesos expirados y aprueba comisiones maduras.

## 8. Emails

- [ ] SPF, DKIM y DMARC configurados en el dominio de envío.
- [ ] Las ocho plantillas enviadas y revisadas en cliente de escritorio y en móvil.
- [ ] Remitente y `reply-to` correctos.
- [ ] Enlaces de confirmación y de recuperación probados de punta a punta.

## 9. Analítica y SEO

- [ ] Instancia de analítica desplegada y funcionando solo tras el consentimiento.
- [ ] Eventos clave instrumentados: ver precios, iniciar checkout, completar test, completar lección.
- [ ] Embudo de conversión visible en un panel.
- [ ] Sitemap accesible y enviado a Search Console.
- [ ] `schema.org/Course` en la ficha de cada curso.
- [ ] Metatags OG verificados con el depurador de las redes.

## 10. Beta cerrada

- [ ] 10–20 alumnos invitados con acceso manual desde el panel.
- [ ] Formulario de feedback por lección disponible.
- [ ] Al menos 10 alumnos completan el bloque B0.
- [ ] Feedback recogido y clasificado.
- [ ] Correcciones críticas aplicadas antes de abrir al público.

---

## Firma

Al completar todo lo anterior:

| Campo | Valor |
|---|---|
| Fecha | `TODO` |
| Responsable | `TODO` |
| Versión desplegada (SHA) | `TODO` |
| Incidencias abiertas conocidas | `TODO` |

> Si alguna casilla bloqueante sigue sin marcar, **no se lanza**. Las de la sección 1 y 2 lo
> son porque vender sin ellas no es un riesgo de producto: es un incumplimiento.
