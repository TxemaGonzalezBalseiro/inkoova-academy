# ── entorno y nombres ────────────────────────────────────────────────────────────────────

variable "environment" {
  description = "Entorno a desplegar. Debe coincidir con el workspace de terraform activo."
  type        = string

  validation {
    condition     = contains(["prod", "staging"], var.environment)
    error_message = "environment debe ser 'prod' o 'staging'."
  }
}

variable "location" {
  description = "Región de Azure."
  type        = string
  default     = "spaincentral"
}

variable "project" {
  description = "Nombre base del proyecto; entra en el nombre de casi todos los recursos."
  type        = string
  default     = "inkoova-academy"
}

# ── imágenes y registro (GHCR) ───────────────────────────────────────────────────────────

variable "github_repository" {
  description = "Repositorio de GitHub del que salen las imágenes (owner/repo, en minúsculas)."
  type        = string
  default     = "inkoova/inkoova-academy"
}

variable "image_tag" {
  description = "Etiqueta de las imágenes a desplegar. En producción conviene fijar el SHA del commit, no 'latest': con el SHA un rollback es volver a aplicar con la etiqueta anterior."
  type        = string
  default     = "latest"
}

variable "caddy_image_name" {
  description = "Nombre de la imagen del Caddy adaptado a ACA (se construye con infra/azure/Dockerfile.caddy y se publica en GHCR junto a las demás)."
  type        = string
  default     = "caddy-azure"
}

variable "ghcr_username" {
  description = "Usuario de GitHub con el que ACA se autentica contra ghcr.io."
  type        = string
}

variable "ghcr_token" {
  description = "PAT de GitHub con permiso read:packages. Solo lectura: este token vive en el state y en los secretos de las apps, no le des más alcance del que necesita."
  type        = string
  sensitive   = true
}

# ── dominio y web ────────────────────────────────────────────────────────────────────────

variable "custom_domain" {
  description = "Dominio propio (p.ej. academy.inkoova.com). Vacío = se usa el FQDN que genera ACA para la app de Caddy. OJO: la URL pública se compila dentro del bundle de la SPA (VITE_API_BASE_URL es relativa, así que la SPA funciona igual), pero PublicBaseUrl, CORS y los enlaces de los emails salen de aquí."
  type        = string
  default     = ""
}

variable "analytics_origin" {
  description = "Origen de la instancia de Plausible para la CSP de Caddy. Vacío si no hay analítica (igual que en el VPS)."
  type        = string
  default     = ""
}

# ── PostgreSQL Flexible Server ───────────────────────────────────────────────────────────

variable "postgres_sku" {
  description = "SKU del Flexible Server. B_Standard_B1ms es el mínimo razonable (1 vCore burstable, 2 GiB)."
  type        = string
  default     = "B_Standard_B1ms"
}

variable "postgres_storage_mb" {
  description = "Disco del servidor en MB. 32768 (32 GB) es el mínimo que ofrece Azure."
  type        = number
  default     = 32768
}

variable "postgres_version" {
  description = "Versión mayor de PostgreSQL. 17 para ir a la par con el postgres:17-alpine del VPS."
  type        = string
  default     = "17"
}

variable "postgres_admin_login" {
  description = "Usuario administrador del servidor. 'academy' para que las cadenas de conexión calquen las del VPS."
  type        = string
  default     = "academy"
}

variable "postgres_zone" {
  description = "Zona de disponibilidad del servidor. Fijarla evita que terraform vea drift cuando Azure elige una."
  type        = string
  default     = "1"
}

variable "enable_pgbouncer" {
  description = "Activa el PgBouncer integrado del Flexible Server (puerto 6432) y apunta la API y los jobs contra él. ATENCIÓN: Azure NO lo soporta en el tier Burstable, así que con B_Standard_B1ms debe quedarse en false (el apply fallaría). Si algún día se sube a General Purpose, ponlo a true y recuperas el papel que hacía el contenedor pgbouncer del VPS. Mientras tanto la conexión va directa al 5432 con pools acotados (ver locals de connection strings)."
  type        = bool
  default     = false
}

variable "database_name" {
  description = "Nombre de la base de datos de la aplicación."
  type        = string
  default     = "academy"
}

variable "developer_ip" {
  description = "IP pública del desarrollador, para abrir el firewall de Postgres durante migraciones (migrate-db.ps1) e importaciones (import-catalog.ps1). Vacío = no se crea la regla."
  type        = string
  default     = ""
}

# ── contenido y observabilidad ───────────────────────────────────────────────────────────

variable "content_share_quota_gb" {
  description = "Cuota del share de Azure Files que hace de /srv/content."
  type        = number
  default     = 5
}

variable "log_retention_days" {
  description = "Retención del Log Analytics workspace. 30 es el mínimo del SKU PerGB2018."
  type        = number
  default     = 30
}

# ── escala de las apps ───────────────────────────────────────────────────────────────────

variable "api_min_replicas" {
  description = "Réplicas mínimas de la API. 1 en prod (sin arranques en frío); 0 en staging (escala a cero y no cuesta nada parada)."
  type        = number
  default     = 1
}

variable "api_max_replicas" {
  description = "Réplicas máximas de la API."
  type        = number
  default     = 2
}

variable "caddy_min_replicas" {
  description = "Réplicas mínimas de Caddy. Mismo criterio que la API."
  type        = number
  default     = 1
}

variable "caddy_max_replicas" {
  description = "Réplicas máximas de Caddy."
  type        = number
  default     = 2
}

variable "enable_jobs" {
  description = "Crea (o no) la app de jobs. Sus schedulers son bucles internos que nunca terminan, así que la app no puede escalar a cero: o corre siempre (min=max=1) o no existe. En staging se ahorra poniéndolo a false."
  type        = bool
  default     = true
}

variable "aspnetcore_environment" {
  description = "ASPNETCORE_ENVIRONMENT / DOTNET_ENVIRONMENT de las apps. Staging también corre como 'Production': es una réplica del entorno real, no un entorno de desarrollo."
  type        = string
  default     = "Production"
}

# ── Key Vault ────────────────────────────────────────────────────────────────────────────

variable "key_vault_purge_protection_enabled" {
  description = "Purge protection del Key Vault. Una vez activado NO se puede desactivar, y un vault borrado no se puede purgar hasta pasar la retención (7 días). true en prod (cert-signing-secret es irrecuperable si se pierde); false en staging para poder destruir y recrear sin esperar."
  type        = bool
  default     = false
}

# ── secretos de la aplicación (mismos nombres que infra/.env.example) ────────────────────
# Las tres claves de firma se GENERAN si se dejan vacías (random_password, 64 alfanuméricos)
# y acaban en el Key Vault. Solo hay que rellenarlas para migrar desde el VPS conservando
# las claves actuales (ver README, «Migrar desde el VPS»).

variable "content_token_key" {
  description = "Academy__ContentTokenKey. Vacío = generada. Rotarla invalida los tokens de contenido vivos (duran 60 s: impacto nulo)."
  type        = string
  sensitive   = true
  default     = ""
}

variable "jwt_signing_key" {
  description = "Academy__Jwt__SigningKey. Vacío = generada. Rotarla cierra todas las sesiones."
  type        = string
  sensitive   = true
  default     = ""
}

variable "certificate_signing_secret" {
  description = "Academy__Certificates__SigningSecret. Vacío = generada. Rotarla invalida la verificación de TODOS los certificados emitidos: si el VPS ya emitió certificados, hay que traer aquí SU valor. No se rota."
  type        = string
  sensitive   = true
  default     = ""
}

variable "stripe_secret_key" {
  description = "Clave secreta de Stripe (sk_live_… en prod, sk_test_… en staging)."
  type        = string
  sensitive   = true
}

variable "stripe_webhook_secret" {
  description = "Secreto del endpoint de webhooks de Stripe (whsec_…). Cada entorno tiene el suyo: el endpoint apunta a la URL pública de ese entorno."
  type        = string
  sensitive   = true
}

variable "owner_emails" {
  description = "Academy__Auth__OwnerEmails: administradores globales, separados por comas. La API acepta este formato de cadena (ver ReadOwnerEmails en Infrastructure). Sin esto NADIE es administrador en el entorno."
  type        = string
}

# ── email ────────────────────────────────────────────────────────────────────────────────

variable "smtp_host" {
  description = "Servidor SMTP (Resend o Brevo, free tier)."
  type        = string
}

variable "smtp_port" {
  description = "Puerto SMTP."
  type        = number
  default     = 587
}

variable "smtp_username" {
  description = "Usuario SMTP."
  type        = string
}

variable "smtp_password" {
  description = "Contraseña SMTP."
  type        = string
  sensitive   = true
}

variable "smtp_from" {
  description = "Remitente de los correos."
  type        = string
  default     = "hola@inkoova.com"
}

# ── facturación fiscal ───────────────────────────────────────────────────────────────────

variable "invoice_issuer_name" {
  description = "Nombre fiscal del emisor de facturas."
  type        = string
  default     = ""
}

variable "invoice_issuer_tax_id" {
  description = "NIF del emisor."
  type        = string
  default     = ""
}

variable "invoice_issuer_address" {
  description = "Dirección fiscal del emisor."
  type        = string
  default     = ""
}

variable "invoice_submit_to_aeat" {
  description = "Solo a true cuando el adaptador Verifactu esté conectado; si no, la API rechaza el pago a propósito antes que emitir una factura que nunca se declaró."
  type        = bool
  default     = false
}

# ── Discord (opcional) ───────────────────────────────────────────────────────────────────

variable "discord_bot_token" {
  description = "Token del bot de Discord. Vacío si la integración no está activa."
  type        = string
  sensitive   = true
  default     = ""
}

variable "discord_guild_id" {
  description = "Id del servidor de Discord."
  type        = string
  default     = ""
}

variable "discord_client_id" {
  description = "Client id de la app OAuth de Discord."
  type        = string
  default     = ""
}

variable "discord_client_secret" {
  description = "Client secret de la app OAuth de Discord."
  type        = string
  sensitive   = true
  default     = ""
}

# ── observabilidad (opcional) ────────────────────────────────────────────────────────────

variable "otel_endpoint" {
  description = "OTEL_EXPORTER_OTLP_ENDPOINT (Grafana Cloud free tier). Vacío si no hay."
  type        = string
  default     = ""
}

variable "otel_headers" {
  description = "OTEL_EXPORTER_OTLP_HEADERS. Suele llevar el token de autenticación, así que se trata como secreto."
  type        = string
  sensitive   = true
  default     = ""
}
