output "public_url" {
  description = "URL pública del entorno (FQDN de la app de Caddy, o el dominio propio si está configurado)."
  value       = local.public_base_url
}

output "caddy_fqdn" {
  description = "FQDN real que ACA asignó a Caddy. Es donde hay que apuntar el CNAME del dominio propio."
  value       = azurerm_container_app.caddy.ingress[0].fqdn
}

output "api_internal_fqdn" {
  description = "FQDN interno de la API (solo resoluble dentro del environment; es el API_UPSTREAM de Caddy)."
  value       = azurerm_container_app.api.ingress[0].fqdn
}

output "postgres_fqdn" {
  description = "Host del Flexible Server, para migrate-db.ps1 e import-catalog.ps1."
  value       = azurerm_postgresql_flexible_server.main.fqdn
}

output "postgres_admin_password" {
  description = "Contraseña del administrador de Postgres (generada; vive en el state y en el Key Vault). Recupérala con: terraform output -raw postgres_admin_password"
  value       = random_password.postgres.result
  sensitive   = true
}

output "key_vault_name" {
  description = "Key Vault del entorno con todos los secretos de la aplicación. Leer uno: az keyvault secret show --vault-name <nombre> --name jwt-signing-key --query value -o tsv"
  value       = azurerm_key_vault.main.name
}

output "key_vault_uri" {
  description = "URI del Key Vault."
  value       = azurerm_key_vault.main.vault_uri
}

output "key_vault_secret_names" {
  description = "Secretos creados en el Key Vault (los opcionales solo si traen valor)."
  value       = sort(tolist(local.secret_names))
}

output "apps_identity_client_id" {
  description = "Client id de la identidad administrada con la que las apps leen el Key Vault."
  value       = azurerm_user_assigned_identity.apps.client_id
}

output "storage_account_name" {
  description = "Cuenta de almacenamiento del contenido, para sync-content.ps1."
  value       = azurerm_storage_account.content.name
}

output "content_share_name" {
  description = "Nombre del share de Azure Files montado como /srv/content."
  value       = azurerm_storage_share.content.name
}

output "psql_command" {
  description = "Conexión psql lista (exporta antes PGPASSWORD con la salida de postgres_admin_password)."
  value       = "psql \"host=${azurerm_postgresql_flexible_server.main.fqdn} port=5432 dbname=${var.database_name} user=${var.postgres_admin_login} sslmode=require\""
}

output "migrator_start_command" {
  description = "Cómo lanzar las migraciones de base de datos."
  value       = "az containerapp job start --name ${local.migrator_name} --resource-group ${azurerm_resource_group.main.name}"
}
