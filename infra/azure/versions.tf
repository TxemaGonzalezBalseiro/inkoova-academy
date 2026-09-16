# Terraform para Azure Container Apps (alternativa gestionada al VPS de
# docker-compose.prod.yml). Un único root module parametrizado por entorno: el aislamiento
# entre prod y staging lo dan los workspaces de terraform más un tfvars por entorno.

terraform {
  required_version = ">= 1.9.0"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 4.0"
    }
    random = {
      source  = "hashicorp/random"
      version = "~> 3.6"
    }
    time = {
      source  = "hashicorp/time"
      version = "~> 0.12"
    }
  }
}

provider "azurerm" {
  features {
    key_vault {
      # Al destruir (staging, sobre todo) se purga el vault soft-deleted para poder
      # recrearlo con el mismo nombre; con purge protection activado Azure lo impide y
      # el vault queda en soft-delete hasta que venza la retención.
      purge_soft_delete_on_destroy    = true
      recover_soft_deleted_key_vaults = true
    }
  }
}

# Aviso (no bloquea) si el workspace no coincide con el entorno del tfvars: aplicar
# envs/prod.tfvars sobre el workspace de staging machacaría el entorno equivocado.
check "workspace_matches_environment" {
  assert {
    condition     = terraform.workspace == var.environment
    error_message = "El workspace de terraform ('${terraform.workspace}') no coincide con var.environment ('${var.environment}'). Ejecuta 'terraform workspace select ${var.environment}' antes de aplicar."
  }
}
