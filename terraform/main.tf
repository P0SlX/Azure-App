# Ce fichier content la définition de nos ressources Azure
terraform {
  required_version = "1.3.8"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "3.42.0"
    }
  }
}

provider "azurerm" {
  features {}
  subscription_id = ""
  tenant_id       = ""
}

# Création d'un groupe de ressources
resource "azurerm_resource_group" "gaming_1" {
  name     = var.rg_name
  location = var.rg_location
}

# Création d'un app service plan
resource "azurerm_service_plan" "gaming_1" {
  name                = var.app_service_plan_name
  location            = azurerm_resource_group.gaming_1.location
  resource_group_name = azurerm_resource_group.gaming_1.name
  os_type             = var.app_service_os_type
  sku_name            = var.app_service_sku_name
}

# Création d'une application web
resource "azurerm_linux_web_app" "gaming_1" {
  name                = var.app_service_name
  location            = azurerm_resource_group.gaming_1.location
  resource_group_name = azurerm_resource_group.gaming_1.name
  service_plan_id     = azurerm_service_plan.gaming_1.id

  site_config {
    application_stack {
      dotnet_version = "7.0"
    }
  }
}

# Création d'un serveur SQL
resource "azurerm_mssql_server" "db_server_1" {
  name                         = var.db_server_name
  resource_group_name          = azurerm_resource_group.gaming_1.name
  location                     = azurerm_service_plan.gaming_1.location
  version                      = "12.0"
  administrator_login          = var.db_login
  administrator_login_password = var.db_password

  tags = {
    environment = "production"
  }
}

# Création d'un compte de stockage
resource "azurerm_storage_account" "db_account_1" {
  name                     = var.db_account_name
  resource_group_name      = azurerm_resource_group.gaming_1.name
  location                 = azurerm_service_plan.gaming_1.location
  account_tier             = "Standard"
  account_replication_type = "LRS"
}

# Création d'une base de données SQL
resource "azurerm_mssql_database" "db_vm" {
  name           = var.db_name
  server_id      = azurerm_mssql_server.db_server_1.id
  collation      = "SQL_Latin1_General_CP1_CI_AS"
  license_type   = "LicenseIncluded"
  max_size_gb    = 1
  read_scale     = false
  sku_name       = "S0"
  zone_redundant = false
}
