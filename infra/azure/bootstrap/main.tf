resource "azurerm_resource_group" "cloudknowledge" {
  name     = local.resource_group_name
  location = var.location

  tags = {
    application = "CloudKnowledge"
    environment = var.environment
    managed_by  = "Terraform"
  }
}

resource "azurerm_container_registry" "cloudknowledge" {
  name                = local.acr_name
  resource_group_name = azurerm_resource_group.cloudknowledge.name
  location            = azurerm_resource_group.cloudknowledge.location
  sku                 = "Standard"
  admin_enabled       = false

  tags = azurerm_resource_group.cloudknowledge.tags
}

resource "azurerm_storage_account" "terraform_state" {
  name                            = local.state_account_name
  resource_group_name             = azurerm_resource_group.cloudknowledge.name
  location                        = azurerm_resource_group.cloudknowledge.location
  account_tier                    = "Standard"
  account_replication_type        = "LRS"
  account_kind                    = "StorageV2"
  min_tls_version                 = "TLS1_2"
  https_traffic_only_enabled      = true
  allow_nested_items_to_be_public = false

  tags = azurerm_resource_group.cloudknowledge.tags
}

resource "azurerm_storage_container" "terraform_state" {
  name                  = "tfstate"
  storage_account_id    = azurerm_storage_account.terraform_state.id
  container_access_type = "private"
}
