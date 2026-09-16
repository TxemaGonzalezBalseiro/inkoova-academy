# T-13 · Infraestructura low-cost (sustituye a la versión AKS)

## Decisión
Todo en **una máquina** con Docker Compose. Cero Kubernetes, cero Terraform, cero servicios gestionados de pago.

## Opciones (elegir en T-00)
| Opción | Coste | Notas |
|---|---|---|
| **A. VPS Hetzner CX22 / CPX11** (2 vCPU, 4 GB) | ~4–5 €/mes | Recomendada. Sin cold starts, Postgres local, backups por snapshot |
| B. Azure VM B1s (12 meses gratis, cuenta nueva) | 0 € el 1er año, luego ~8 €/mes | Mismo compose; migrar a A al cumplir el año |
| C. 100 % free tier Azure | 0 € | Static Web Apps Free (web) + Container Apps (grant gratuito mensual, API) + Neon/Supabase Postgres free + Cloudflare R2 (contenido, 10 GB gratis). Más piezas, cold starts, límites |

## Alcance
- `docker-compose.prod.yml`: `caddy` (TLS automático), `api` (.NET), `web` (estáticos servidos por Caddy), `postgres:17`, `content` (volumen con el HTML del curso, protegido por la API).
- Cache: `IMemoryCache` en la API. Sin Valkey.
- Auth: ASP.NET Core Identity + cookies/JWT en la misma BD. Sin Keycloak (queda en backlog si hay B2B).
- Contenido del curso en el propio disco (volumen); si crece, Cloudflare R2 (gratis hasta 10 GB, sin egress).
- Email: Resend/Brevo free tier.
- Backups: `pg_dump` nocturno + rclone a Backblaze B2 (gratis 10 GB) + snapshot semanal del VPS.
- Deploy: GitHub Actions → `ssh` + `docker compose pull && up -d`. Imágenes en GHCR.
- Observabilidad: OpenTelemetry → Grafana Cloud free tier (o solo logs con `docker logs` + Uptime Kuma).
- Hardening: ufw, fail2ban, SSH por clave, actualizaciones automáticas.

## Criterios de aceptación
- Servidor desde cero a producción con un script (`bootstrap.sh`) en < 30 min.
- Restore de `pg_dump` probado.
- Coste mensual total documentado ≤ 10 € (VPS + dominio).

## Dependencias
T-00.
