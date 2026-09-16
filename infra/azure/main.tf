# Base del entorno: resource group, logs, ficheros de contenido y PostgreSQL.
# Las Container Apps viven en containerapps.tf.

locals {
  suffix = var.environment

  tags = {
    project     = var.project
    environment = var.environment
    managed_by  = "terraform"
  }
}

resource "azurerm_resource_group" "main" {
  name     = "rg-${var.project}-${local.suffix}"
  location = var.location
  tags     = local.tags
}

# ── Log Analytics ────────────────────────────────────────────────────────────────────────
# Destino de los logs de consola de todas las apps del entorno. Se paga por GB ingerido:
# con el tráfico de la academia entra de sobra en el tramo bajo.

resource "azurerm_log_analytics_workspace" "main" {
  name                = "log-${var.project}-${local.suffix}"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  sku                 = "PerGB2018"
  retention_in_days   = var.log_retention_days
  tags                = local.tags
}

# ── Azure Files: /srv/content ────────────────────────────────────────────────────────────
# En el VPS el contenido de los cursos es un volumen de docker compartido entre api, jobs y
# caddy. Aquí es un share SMB montado en las tres apps: rw para api/jobs (importaciones),
# ro para caddy (solo sirve bytes tras el forward_auth, ADR-012).

# Sufijo aleatorio para los recursos con nombre único a nivel mundial (storage account,
# Key Vault).
resource "random_string" "suffix" {
  length  = 4
  lower   = true
  upper   = false
  special = false
}

moved {
  from = random_string.storage_suffix
  to   = random_string.suffix
}

resource "azurerm_storage_account" "content" {
  # Máx. 24 caracteres, solo minúsculas y dígitos, y único a nivel mundial: de ahí el
  # nombre corto y el sufijo aleatorio.
  name                     = "stacademy${local.suffix}${random_string.suffix.result}"
  resource_group_name      = azurerm_resource_group.main.name
  location                 = azurerm_resource_group.main.location
  account_tier             = "Standard"
  account_replication_type = "LRS"
  min_tls_version          = "TLS1_2"
  tags                     = local.tags
}

resource "azurerm_storage_share" "content" {
  name               = "content"
  storage_account_id = azurerm_storage_account.content.id
  quota              = var.content_share_quota_gb
}

# ── PostgreSQL Flexible Server ───────────────────────────────────────────────────────────
# B_Standard_B1ms (1 vCore burstable, 2 GiB, 32 GB de disco), sin HA y sin redundancia
# geográfica: el mínimo que ofrece el servicio gestionado, alineado con el presupuesto.
#
# B1ms admite ~50 conexiones: los pools de las apps van acotados en consecuencia (ver
# locals de connection strings en containerapps.tf). El PgBouncer integrado NO existe en
# el tier Burstable; var.enable_pgbouncer queda preparado para un futuro salto de tier.

# Generada; queda en el Key Vault como `postgres-admin-password` (keyvault.tf) y en el
# output `postgres_admin_password`.
resource "random_password" "postgres" {
  length = 32
  # Sin especiales: la contraseña viaja dentro de connection strings y de PGPASSWORD y no
  # merece la pena pelearse con el escapado. 32 alfanuméricos sobran como entropía.
  special = false
}

resource "azurerm_postgresql_flexible_server" "main" {
  name                = "psql-${var.project}-${local.suffix}"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location

  version                = var.postgres_version
  sku_name               = var.postgres_sku
  storage_mb             = var.postgres_storage_mb
  zone                   = var.postgres_zone
  administrator_login    = var.postgres_admin_login
  administrator_password = random_password.postgres.result

  backup_retention_days        = 7
  geo_redundant_backup_enabled = false

  # Acceso público con firewall: las apps de consumo de ACA no tienen IP fija, así que la
  # alternativa (VNet + private endpoint) costaría más que la propia base. El tráfico va
  # cifrado (Ssl Mode=Require) y el firewall corta todo lo que no sea Azure o la IP del
  # desarrollador.
  public_network_access_enabled = true

  tags = local.tags
}

resource "azurerm_postgresql_flexible_server_database" "academy" {
  name      = var.database_name
  server_id = azurerm_postgresql_flexible_server.main.id
  charset   = "UTF8"
  collation = "en_US.utf8"
}

# La regla 0.0.0.0-0.0.0.0 es el interruptor «permitir servicios de Azure» del portal: deja
# pasar a las Container Apps (IPs dinámicas) sin abrir la base a internet en general.
resource "azurerm_postgresql_flexible_server_firewall_rule" "azure_services" {
  name             = "allow-azure-services"
  server_id        = azurerm_postgresql_flexible_server.main.id
  start_ip_address = "0.0.0.0"
  end_ip_address   = "0.0.0.0"
}

resource "azurerm_postgresql_flexible_server_firewall_rule" "developer" {
  count            = var.developer_ip != "" ? 1 : 0
  name             = "developer"
  server_id        = azurerm_postgresql_flexible_server.main.id
  start_ip_address = var.developer_ip
  end_ip_address   = var.developer_ip
}

# Solo aplicable fuera del tier Burstable (ver variables.tf).
resource "azurerm_postgresql_flexible_server_configuration" "pgbouncer" {
  count     = var.enable_pgbouncer ? 1 : 0
  name      = "pgbouncer.enabled"
  server_id = azurerm_postgresql_flexible_server.main.id
  value     = "true"
}
