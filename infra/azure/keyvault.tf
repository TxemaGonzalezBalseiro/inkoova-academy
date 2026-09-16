# Key Vault: la única fuente de secretos del entorno.
#
# Flujo: terraform genera (o recibe por tfvars) cada secreto, lo escribe en el Key Vault, y
# las Container Apps lo leen de ahí con una identidad administrada (user-assigned). Las
# apps nunca reciben el valor por terraform: reciben la URL del secreto y lo resuelven
# ellas. Ventajas frente a pasar el valor directo:
#   - Rotación sin tocar las apps: al cambiar el secreto en el vault, ACA lo relee solo
#     (referencia sin versión, sondeo cada ~30 min) o al forzar una revisión nueva.
#   - Un sitio donde mirar qué secretos existen y quién los lee (Azure RBAC + auditoría).
#   - Los valores sensibles no pasan por el for_each de las apps (terraform lo prohíbe).
#
# Los valores siguen viviendo también en el state de terraform (random_password y
# azurerm_key_vault_secret los guardan): el state sigue siendo un secreto en sí mismo.

data "azurerm_client_config" "current" {}

# ── secretos generados ───────────────────────────────────────────────────────────────────
# Las tres claves de firma de la aplicación se generan aquí si el tfvars las deja vacías.
# Se usan como bytes UTF-8 (HMAC-SHA256 / SymmetricSecurityKey): 64 alfanuméricos dan
# 64 bytes de clave, por encima de los 32 que exige HS256 y equivalente a los
# `openssl rand -base64 48` de infra/.env.example.
#
# Rotar una clave generada: terraform apply -replace=random_password.jwt_signing_key ...
# (ver variables.tf para el impacto de cada una; la de certificados NO se rota).

resource "random_password" "content_token_key" {
  length  = 64
  special = false
}

resource "random_password" "jwt_signing_key" {
  length  = 64
  special = false
}

resource "random_password" "certificate_signing_secret" {
  length  = 64
  special = false
}

locals {
  content_token_key          = var.content_token_key != "" ? var.content_token_key : random_password.content_token_key.result
  jwt_signing_key            = var.jwt_signing_key != "" ? var.jwt_signing_key : random_password.jwt_signing_key.result
  certificate_signing_secret = var.certificate_signing_secret != "" ? var.certificate_signing_secret : random_password.certificate_signing_secret.result

  # Presencia de los secretos opcionales, desmarcada de sensible: solo revela si hay valor,
  # y hace falta como booleano en claro para decidir qué secretos y variables se crean
  # (terraform no admite valores sensibles en for_each ni en count).
  discord_bot_enabled   = nonsensitive(var.discord_bot_token != "")
  discord_oauth_enabled = nonsensitive(var.discord_client_secret != "")
  otel_headers_enabled  = nonsensitive(var.otel_headers != "")
}

# ── identidad de las apps ────────────────────────────────────────────────────────────────
# User-assigned y no system-assigned a propósito: con system-assigned la identidad nace
# con la app, pero la app necesita leer el vault ya al crearse (huevo y gallina). Con una
# identidad previa el rol se concede antes de que exista ninguna app.

resource "azurerm_user_assigned_identity" "apps" {
  name                = "id-${var.project}-${local.suffix}"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  tags                = local.tags
}

# ── vault ────────────────────────────────────────────────────────────────────────────────

resource "azurerm_key_vault" "main" {
  # Máx. 24 caracteres, único a nivel mundial, alfanuméricos y guiones.
  name                = "kv-academy-${local.suffix}-${random_string.suffix.result}"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  tenant_id           = data.azurerm_client_config.current.tenant_id
  sku_name            = "standard"

  # Autorización por Azure RBAC (roles), no por access policies: es el modelo que Azure
  # recomienda y el único que casa con las identidades administradas sin más bloques.
  rbac_authorization_enabled = true

  # 7 días es el mínimo de retención tras borrar. Con purge protection un vault borrado
  # no se puede purgar hasta que pase la retención (bloquea reusar el nombre); se activa
  # en prod (variables.tf) porque perder cert-signing-secret sería irrecuperable.
  soft_delete_retention_days = 7
  purge_protection_enabled   = var.key_vault_purge_protection_enabled

  # Mismo motivo que en Postgres: las apps de consumo de ACA no tienen IP fija y un
  # private endpoint costaría más que todo el vault. El acceso lo corta RBAC.
  public_network_access_enabled = true

  tags = local.tags
}

# Quien ejecuta terraform necesita escribir secretos. Ser Owner de la suscripción NO basta:
# con RBAC el plano de datos del vault tiene sus propios roles.
resource "azurerm_role_assignment" "terraform_secrets_officer" {
  scope                = azurerm_key_vault.main.id
  role_definition_name = "Key Vault Secrets Officer"
  principal_id         = data.azurerm_client_config.current.object_id
}

# Las apps solo leen.
resource "azurerm_role_assignment" "apps_secrets_user" {
  scope                = azurerm_key_vault.main.id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = azurerm_user_assigned_identity.apps.principal_id
}

# Las asignaciones de rol tardan hasta un par de minutos en propagarse; sin esta espera
# el primer apply falla con 403 al escribir el primer secreto (y ACA al leerlo).
resource "time_sleep" "rbac_propagation" {
  create_duration = "90s"

  depends_on = [
    azurerm_role_assignment.terraform_secrets_officer,
    azurerm_role_assignment.apps_secrets_user,
  ]
}

# ── secretos ─────────────────────────────────────────────────────────────────────────────
# Nombres = los de los `secret` de las apps del VPS/compose. Las connection strings van
# ya compuestas (ACA no puede componerlas): una por consumidor, con su pool acotado.

locals {
  secret_values = {
    postgres-admin-password = random_password.postgres.result
    content-token-key       = local.content_token_key
    jwt-signing-key         = local.jwt_signing_key
    cert-signing-secret     = local.certificate_signing_secret
    stripe-secret-key       = var.stripe_secret_key
    stripe-webhook-secret   = var.stripe_webhook_secret
    smtp-password           = var.smtp_password
    ghcr-token              = var.ghcr_token
    connstr-api             = local.api_connection_string
    connstr-jobs            = local.jobs_connection_string
    connstr-migrator        = local.migrator_connection_string
    discord-bot-token       = var.discord_bot_token
    discord-client-secret   = var.discord_client_secret
    otel-headers            = var.otel_headers
  }

  # Solo se crean los que traen valor: Key Vault y ACA rechazan secretos vacíos.
  secret_names = toset(concat(
    [
      "postgres-admin-password",
      "content-token-key",
      "jwt-signing-key",
      "cert-signing-secret",
      "stripe-secret-key",
      "stripe-webhook-secret",
      "smtp-password",
      "ghcr-token",
      "connstr-api",
      "connstr-jobs",
      "connstr-migrator",
    ],
    local.discord_bot_enabled ? ["discord-bot-token"] : [],
    local.discord_oauth_enabled ? ["discord-client-secret"] : [],
    local.otel_headers_enabled ? ["otel-headers"] : [],
  ))
}

resource "azurerm_key_vault_secret" "app" {
  for_each = local.secret_names

  name         = each.key
  value        = local.secret_values[each.key]
  key_vault_id = azurerm_key_vault.main.id
  content_type = "text/plain"
  tags         = local.tags

  depends_on = [time_sleep.rbac_propagation]
}

# name → URL sin versión del secreto. Es lo que reciben las apps: sin versión para que
# una rotación en el vault llegue sola, sin reaplicar terraform.
locals {
  kv_secret_ref = { for name, secret in azurerm_key_vault_secret.app : name => secret.versionless_id }
}
