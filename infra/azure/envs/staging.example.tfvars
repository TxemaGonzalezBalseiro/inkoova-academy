# Staging: todo a mínimos. Copia a envs/staging.tfvars (NO se versiona) y rellena.
# Secretos PROPIOS de staging: no reutilices los de producción.

environment = "staging"

image_tag = "latest"

ghcr_username = "CHANGE_ME_github_user"
ghcr_token    = "CHANGE_ME_ghp_read_packages"

custom_domain = ""
developer_ip  = "CHANGE_ME_1.2.3.4"
owner_emails  = "CHANGE_ME@inkoova.com"

# Escala a cero: la primera petición tras un rato paga un arranque en frío (~segundos),
# que en staging es un precio estupendo por no pagar réplicas paradas.
api_min_replicas   = 0
caddy_min_replicas = 0

# Los jobs ni existen en staging (no pueden escalar a cero: sus schedulers nunca terminan).
enable_jobs = false

# Key Vault sin purge protection: staging se destruye y recrea sin esperar.
key_vault_purge_protection_enabled = false

# ── claves de firma de la aplicación ─────────────────────────────────────────
# Vacías = generadas por terraform y guardadas en el Key Vault (propias de staging).
content_token_key          = ""
jwt_signing_key            = ""
certificate_signing_secret = ""

# ── Stripe (modo test) ───────────────────────────────────────────────────────
stripe_secret_key     = "CHANGE_ME_sk_test_"
stripe_webhook_secret = "CHANGE_ME_whsec_"

# ── email ────────────────────────────────────────────────────────────────────
smtp_host     = "CHANGE_ME_smtp.resend.com"
smtp_port     = 587
smtp_username = "CHANGE_ME"
smtp_password = "CHANGE_ME"
smtp_from     = "hola@inkoova.com"

# ── facturación fiscal (datos de prueba) ─────────────────────────────────────
invoice_issuer_name    = "Pruebas"
invoice_issuer_tax_id  = "00000000T"
invoice_issuer_address = "Calle Falsa 123"
invoice_submit_to_aeat = false

# ── opcionales apagados ──────────────────────────────────────────────────────
discord_bot_token     = ""
discord_guild_id      = ""
discord_client_id     = ""
discord_client_secret = ""
otel_endpoint         = ""
otel_headers          = ""
analytics_origin      = ""
