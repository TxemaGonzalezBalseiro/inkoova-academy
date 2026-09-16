# Container Apps: la topología del VPS (caddy → api / jobs / migrator) trasladada a ACA.
#
# Diferencias deliberadas con docker-compose.prod.yml:
#   - El TLS lo termina el ingress de ACA: Caddy escucha :8080 en claro (Caddyfile.azure).
#   - La SPA no viaja en un volumen: va copiada dentro de la imagen caddy-azure
#     (Dockerfile.caddy), porque en ACA no hay volúmenes compartidos entre apps.
#   - No hay contenedor pgbouncer: en B1ms tampoco existe el integrado de Flexible Server,
#     así que la conexión va directa al 5432 con pools recortados (20+5+4 ≪ ~50 que admite
#     B1ms). Si enable_pgbouncer=true (tier General Purpose), se pasa al 6432 solo.
#   - El migrator es un Container Apps Job de arranque manual, no un servicio one-shot.
#   - Los secretos no van en el compose/env: cada app los referencia por URL en el Key
#     Vault (keyvault.tf) y los lee con su identidad administrada.

locals {
  image_base     = "ghcr.io/${var.github_repository}"
  api_app_name   = "ca-api-${local.suffix}"
  caddy_app_name = "ca-caddy-${local.suffix}"
  jobs_app_name  = "ca-jobs-${local.suffix}"
  migrator_name  = "job-migrator-${local.suffix}"

  # URL pública: dominio propio si lo hay; si no, el FQDN que ACA dará a la app de Caddy.
  # Se calcula a partir del nombre (determinista) y del default_domain del environment para
  # no crear un ciclo: la API necesita la URL pública y Caddy necesita el FQDN de la API.
  public_host     = var.custom_domain != "" ? var.custom_domain : "${local.caddy_app_name}.${azurerm_container_app_environment.main.default_domain}"
  public_base_url = "https://${local.public_host}"

  pg_host = azurerm_postgresql_flexible_server.main.fqdn
  pg_port = var.enable_pgbouncer ? 6432 : 5432

  # Pools acotados a mano, como en el VPS pero más cortos: sin pooler intermedio, la suma de
  # todos los pools tiene que caber en las ~50 conexiones de B1ms con margen para psql.
  api_connection_string  = "Host=${local.pg_host};Port=${local.pg_port};Database=${var.database_name};Username=${var.postgres_admin_login};Password=${random_password.postgres.result};Ssl Mode=Require;Maximum Pool Size=20;Minimum Pool Size=2;Connection Idle Lifetime=60;Timeout=10;Command Timeout=30"
  jobs_connection_string = "Host=${local.pg_host};Port=${local.pg_port};Database=${var.database_name};Username=${var.postgres_admin_login};Password=${random_password.postgres.result};Ssl Mode=Require;Maximum Pool Size=5;Minimum Pool Size=1;Connection Idle Lifetime=60;Timeout=10;Command Timeout=120"
  # DbUp siempre directo al 5432, nunca por un pooler de transacción (mismo motivo que en
  # docker-compose.prod.yml: DDL y transacciones por migración).
  migrator_connection_string = "Host=${local.pg_host};Port=5432;Database=${var.database_name};Username=${var.postgres_admin_login};Password=${random_password.postgres.result};Ssl Mode=Require;Maximum Pool Size=4"

  # Secretos de cada app: nombre del secreto en la app → URL del secreto en el Key Vault
  # (keyvault.tf). Las apps NO reciben valores: los resuelven ellas con la identidad
  # administrada. Los opcionales (Discord, OTEL) solo existen si traen valor.
  shared_secret_names = concat(
    [
      "content-token-key",
      "jwt-signing-key",
      "cert-signing-secret",
      "stripe-secret-key",
      "stripe-webhook-secret",
      "smtp-password",
      "ghcr-token",
    ],
    local.discord_bot_enabled ? ["discord-bot-token"] : [],
  )

  api_secrets = merge(
    { for name in local.shared_secret_names : name => local.kv_secret_ref[name] },
    { connstr-academy = local.kv_secret_ref["connstr-api"] },
    local.discord_oauth_enabled ? { discord-client-secret = local.kv_secret_ref["discord-client-secret"] } : {},
    local.otel_headers_enabled ? { otel-headers = local.kv_secret_ref["otel-headers"] } : {},
  )

  jobs_secrets = merge(
    { for name in local.shared_secret_names : name => local.kv_secret_ref[name] },
    { connstr-academy = local.kv_secret_ref["connstr-jobs"] },
  )

  # Variables de entorno respaldadas por secreto (nombre de env → nombre de secreto).
  api_env_secret = merge(
    {
      "ConnectionStrings__Academy"           = "connstr-academy"
      "Academy__ContentTokenKey"             = "content-token-key"
      "Academy__Jwt__SigningKey"             = "jwt-signing-key"
      "Academy__Certificates__SigningSecret" = "cert-signing-secret"
      "Academy__Stripe__SecretKey"           = "stripe-secret-key"
      "Academy__Stripe__WebhookSecret"       = "stripe-webhook-secret"
      "Academy__Email__Password"             = "smtp-password"
    },
    local.discord_bot_enabled ? { "Academy__Discord__BotToken" = "discord-bot-token" } : {},
    local.discord_oauth_enabled ? { "Academy__Discord__ClientSecret" = "discord-client-secret" } : {},
    local.otel_headers_enabled ? { "OTEL_EXPORTER_OTLP_HEADERS" = "otel-headers" } : {},
  )

  jobs_env_secret = merge(
    {
      "ConnectionStrings__Academy"           = "connstr-academy"
      "Academy__ContentTokenKey"             = "content-token-key"
      "Academy__Jwt__SigningKey"             = "jwt-signing-key"
      "Academy__Certificates__SigningSecret" = "cert-signing-secret"
      "Academy__Stripe__SecretKey"           = "stripe-secret-key"
      "Academy__Stripe__WebhookSecret"       = "stripe-webhook-secret"
      "Academy__Email__Password"             = "smtp-password"
    },
    local.discord_bot_enabled ? { "Academy__Discord__BotToken" = "discord-bot-token" } : {},
  )

  # Variables de entorno en claro. Mismo inventario que docker-compose.prod.yml, más
  # Academy__Auth__OwnerEmails (cadena separada por comas; la API la admite así), que en el
  # compose faltaba y sin ella nadie es administrador.
  api_env_plain = {
    ASPNETCORE_ENVIRONMENT            = var.aspnetcore_environment
    ASPNETCORE_HTTP_PORTS             = "8080"
    Academy__PublicBaseUrl            = local.public_base_url
    Academy__ContentRoot              = "/srv/content"
    Academy__Email__TemplatesPath     = "/app/emails"
    Academy__Cors__Origins__0         = local.public_base_url
    Academy__Auth__OwnerEmails        = var.owner_emails
    Academy__Email__Host              = var.smtp_host
    Academy__Email__Port              = tostring(var.smtp_port)
    Academy__Email__Username          = var.smtp_username
    Academy__Email__From              = var.smtp_from
    Academy__Invoicing__IssuerName    = var.invoice_issuer_name
    Academy__Invoicing__IssuerTaxId   = var.invoice_issuer_tax_id
    Academy__Invoicing__IssuerAddress = var.invoice_issuer_address
    Academy__Invoicing__SubmitToAeat  = var.invoice_submit_to_aeat ? "true" : "false"
    Academy__Discord__GuildId         = var.discord_guild_id
    Academy__Discord__ClientId        = var.discord_client_id
    Academy__Discord__RedirectUri     = "${local.public_base_url}/comunidad/discord/callback"
    OTEL_EXPORTER_OTLP_ENDPOINT       = var.otel_endpoint
  }

  jobs_env_plain = {
    DOTNET_ENVIRONMENT            = var.aspnetcore_environment
    Academy__PublicBaseUrl        = local.public_base_url
    Academy__ContentRoot          = "/srv/content"
    Academy__Email__TemplatesPath = "/app/emails"
    Academy__Email__Host          = var.smtp_host
    Academy__Email__Username      = var.smtp_username
    Academy__Email__From          = var.smtp_from
    Academy__Discord__GuildId     = var.discord_guild_id
  }
}

# ── environment ──────────────────────────────────────────────────────────────────────────

resource "azurerm_container_app_environment" "main" {
  name                       = "cae-${var.project}-${local.suffix}"
  location                   = azurerm_resource_group.main.location
  resource_group_name        = azurerm_resource_group.main.name
  log_analytics_workspace_id = azurerm_log_analytics_workspace.main.id
  tags                       = local.tags
}

# El mismo share, registrado dos veces con permisos distintos: rw para api/jobs, ro para
# caddy (ADR-012: caddy sirve bytes, no debe poder escribirlos).
resource "azurerm_container_app_environment_storage" "content_rw" {
  name                         = "content-rw"
  container_app_environment_id = azurerm_container_app_environment.main.id
  account_name                 = azurerm_storage_account.content.name
  share_name                   = azurerm_storage_share.content.name
  access_key                   = azurerm_storage_account.content.primary_access_key
  access_mode                  = "ReadWrite"
}

resource "azurerm_container_app_environment_storage" "content_ro" {
  name                         = "content-ro"
  container_app_environment_id = azurerm_container_app_environment.main.id
  account_name                 = azurerm_storage_account.content.name
  share_name                   = azurerm_storage_share.content.name
  access_key                   = azurerm_storage_account.content.primary_access_key
  access_mode                  = "ReadOnly"
}

# ── api ──────────────────────────────────────────────────────────────────────────────────

resource "azurerm_container_app" "api" {
  name                         = local.api_app_name
  container_app_environment_id = azurerm_container_app_environment.main.id
  resource_group_name          = azurerm_resource_group.main.name
  revision_mode                = "Single"
  tags                         = local.tags

  identity {
    type         = "UserAssigned"
    identity_ids = [azurerm_user_assigned_identity.apps.id]
  }

  dynamic "secret" {
    for_each = local.api_secrets
    content {
      name                = secret.key
      key_vault_secret_id = secret.value
      identity            = azurerm_user_assigned_identity.apps.id
    }
  }

  registry {
    server               = "ghcr.io"
    username             = var.ghcr_username
    password_secret_name = "ghcr-token"
  }

  # Ingress interno: solo Caddy habla con la API. allow_insecure_connections deja que el
  # tráfico interno vaya por HTTP plano (puerto 80 del ingress): evita que Caddy tenga que
  # fiarse del certificado privado del environment. No sale del plano interno de ACA.
  ingress {
    external_enabled           = false
    target_port                = 8080
    transport                  = "auto"
    allow_insecure_connections = true

    traffic_weight {
      latest_revision = true
      percentage      = 100
    }
  }

  template {
    min_replicas = var.api_min_replicas
    max_replicas = var.api_max_replicas

    http_scale_rule {
      name                = "http"
      concurrent_requests = "50"
    }

    container {
      name   = "api"
      image  = "${local.image_base}/api:${var.image_tag}"
      cpu    = 0.5
      memory = "1Gi"

      dynamic "env" {
        for_each = local.api_env_plain
        content {
          name  = env.key
          value = env.value
        }
      }

      dynamic "env" {
        for_each = local.api_env_secret
        content {
          name        = env.key
          secret_name = env.value
        }
      }

      volume_mounts {
        name = "content"
        path = "/srv/content"
      }

      liveness_probe {
        transport = "HTTP"
        port      = 8080
        path      = "/health/live"
      }

      readiness_probe {
        transport = "HTTP"
        port      = 8080
        path      = "/health/ready"
      }
    }

    volume {
      name         = "content"
      storage_type = "AzureFile"
      storage_name = azurerm_container_app_environment_storage.content_rw.name
    }
  }
}

# ── caddy ────────────────────────────────────────────────────────────────────────────────

resource "azurerm_container_app" "caddy" {
  name                         = local.caddy_app_name
  container_app_environment_id = azurerm_container_app_environment.main.id
  resource_group_name          = azurerm_resource_group.main.name
  revision_mode                = "Single"
  tags                         = local.tags

  identity {
    type         = "UserAssigned"
    identity_ids = [azurerm_user_assigned_identity.apps.id]
  }

  secret {
    name                = "ghcr-token"
    key_vault_secret_id = local.kv_secret_ref["ghcr-token"]
    identity            = azurerm_user_assigned_identity.apps.id
  }

  registry {
    server               = "ghcr.io"
    username             = var.ghcr_username
    password_secret_name = "ghcr-token"
  }

  ingress {
    external_enabled = true
    target_port      = 8080
    transport        = "auto"

    traffic_weight {
      latest_revision = true
      percentage      = 100
    }
  }

  template {
    min_replicas = var.caddy_min_replicas
    max_replicas = var.caddy_max_replicas

    http_scale_rule {
      name                = "http"
      concurrent_requests = "100"
    }

    container {
      name   = "caddy"
      image  = "${local.image_base}/${var.caddy_image_name}:${var.image_tag}"
      cpu    = 0.25
      memory = "0.5Gi"

      # El Caddyfile.azure resuelve sus upstreams con estos placeholders.
      env {
        name  = "API_UPSTREAM"
        value = azurerm_container_app.api.ingress[0].fqdn
      }

      env {
        name  = "ANALYTICS_ORIGIN"
        value = var.analytics_origin
      }

      volume_mounts {
        name = "content"
        path = "/srv/content"
      }
    }

    volume {
      name         = "content"
      storage_type = "AzureFile"
      storage_name = azurerm_container_app_environment_storage.content_ro.name
    }
  }
}

# ── jobs ─────────────────────────────────────────────────────────────────────────────────
# Los schedulers son bucles internos del proceso: no hay evento por el que escalar, así que
# min = max = 1. Con enable_jobs=false la app no existe (staging): un worker de min 0 sin
# ingress nunca se pararía solo, porque el proceso no termina.

resource "azurerm_container_app" "jobs" {
  count = var.enable_jobs ? 1 : 0

  name                         = local.jobs_app_name
  container_app_environment_id = azurerm_container_app_environment.main.id
  resource_group_name          = azurerm_resource_group.main.name
  revision_mode                = "Single"
  tags                         = local.tags

  identity {
    type         = "UserAssigned"
    identity_ids = [azurerm_user_assigned_identity.apps.id]
  }

  dynamic "secret" {
    for_each = local.jobs_secrets
    content {
      name                = secret.key
      key_vault_secret_id = secret.value
      identity            = azurerm_user_assigned_identity.apps.id
    }
  }

  registry {
    server               = "ghcr.io"
    username             = var.ghcr_username
    password_secret_name = "ghcr-token"
  }

  template {
    min_replicas = 1
    max_replicas = 1

    container {
      name   = "jobs"
      image  = "${local.image_base}/jobs:${var.image_tag}"
      cpu    = 0.25
      memory = "0.5Gi"

      dynamic "env" {
        for_each = local.jobs_env_plain
        content {
          name  = env.key
          value = env.value
        }
      }

      dynamic "env" {
        for_each = local.jobs_env_secret
        content {
          name        = env.key
          secret_name = env.value
        }
      }

      volume_mounts {
        name = "content"
        path = "/srv/content"
      }
    }

    volume {
      name         = "content"
      storage_type = "AzureFile"
      storage_name = azurerm_container_app_environment_storage.content_rw.name
    }
  }
}

# ── migrator ─────────────────────────────────────────────────────────────────────────────
# Job de arranque manual: se lanza en cada despliegue ANTES de mover las apps a la imagen
# nueva (en el VPS lo ordenaba depends_on; aquí lo ordena el runbook / workflow):
#   az containerapp job start -n job-migrator-<env> -g rg-inkoova-academy-<env>

resource "azurerm_container_app_job" "migrator" {
  name                         = local.migrator_name
  location                     = azurerm_resource_group.main.location
  resource_group_name          = azurerm_resource_group.main.name
  container_app_environment_id = azurerm_container_app_environment.main.id
  tags                         = local.tags

  replica_timeout_in_seconds = 900
  replica_retry_limit        = 0

  manual_trigger_config {
    parallelism              = 1
    replica_completion_count = 1
  }

  identity {
    type         = "UserAssigned"
    identity_ids = [azurerm_user_assigned_identity.apps.id]
  }

  secret {
    name                = "connstr-academy"
    key_vault_secret_id = local.kv_secret_ref["connstr-migrator"]
    identity            = azurerm_user_assigned_identity.apps.id
  }

  secret {
    name                = "ghcr-token"
    key_vault_secret_id = local.kv_secret_ref["ghcr-token"]
    identity            = azurerm_user_assigned_identity.apps.id
  }

  registry {
    server               = "ghcr.io"
    username             = var.ghcr_username
    password_secret_name = "ghcr-token"
  }

  template {
    container {
      name   = "migrator"
      image  = "${local.image_base}/migrator:${var.image_tag}"
      cpu    = 0.25
      memory = "0.5Gi"

      env {
        name        = "ConnectionStrings__Academy"
        secret_name = "connstr-academy"
      }
    }
  }
}
