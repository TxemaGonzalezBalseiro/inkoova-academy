# Producción. Copia a envs/prod.tfvars (NO se versiona: ver .gitignore) y rellena.
# Las claves de firma de la aplicación las genera terraform y las guarda en el Key Vault:
# solo los secretos externos (GHCR, Stripe, SMTP, Discord, OTEL) hay que traerlos.

environment = "prod"

# Key Vault: purge protection en prod (irreversible; protege cert-signing-secret).
key_vault_purge_protection_enabled = true

# Imágenes: fija el SHA del commit desplegado; 'latest' solo para el arranque inicial.
image_tag = "latest"

# GHCR: PAT clásico con SOLO read:packages.
ghcr_username = "CHANGE_ME_github_user"
ghcr_token    = "CHANGE_ME_ghp_read_packages"

# Dominio propio cuando esté apuntado; vacío mientras se use el FQDN de ACA.
custom_domain = ""

# IP pública del desarrollador para migrate-db.ps1 / import-catalog.ps1 (curl ifconfig.me).
developer_ip = "CHANGE_ME_1.2.3.4"

# Administradores globales (separados por comas). Sin esto nadie es admin.
owner_emails = "CHANGE_ME@inkoova.com"

# ── claves de firma de la aplicación ─────────────────────────────────────────
# Vacías = terraform las genera (64 alfanuméricos) y las guarda en el Key Vault.
# Si migras desde el VPS con certificados YA emitidos, copia aquí el
# Academy__Certificates__SigningSecret de infra/.env: con otra clave ningún
# certificado antiguo verificará. Las otras dos pueden regenerarse sin más.
content_token_key          = ""
jwt_signing_key            = ""
certificate_signing_secret = ""

# ── Stripe (cuenta live) ─────────────────────────────────────────────────────
stripe_secret_key     = "CHANGE_ME_sk_live_"
stripe_webhook_secret = "CHANGE_ME_whsec_"

# ── email ────────────────────────────────────────────────────────────────────
smtp_host     = "CHANGE_ME_smtp.resend.com"
smtp_port     = 587
smtp_username = "CHANGE_ME"
smtp_password = "CHANGE_ME"
smtp_from     = "hola@inkoova.com"

# ── facturación fiscal ───────────────────────────────────────────────────────
invoice_issuer_name    = "CHANGE_ME"
invoice_issuer_tax_id  = "CHANGE_ME"
invoice_issuer_address = "CHANGE_ME"
invoice_submit_to_aeat = false

# ── Discord (opcional: deja vacío para desactivar) ───────────────────────────
discord_bot_token     = ""
discord_guild_id      = ""
discord_client_id     = ""
discord_client_secret = ""

# ── observabilidad (opcional) ────────────────────────────────────────────────
otel_endpoint    = ""
otel_headers     = ""
analytics_origin = ""
