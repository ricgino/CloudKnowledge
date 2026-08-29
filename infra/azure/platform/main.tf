locals {
  deterministic_suffix = substr(md5("${var.subscription_id}-${var.resource_group_name}"), 0, 6)
  compact_prefix       = replace("${var.resource_prefix}${var.environment}", "-", "")
  workload_location    = data.azurerm_container_registry.cloudknowledge.location

  postgres_server_name = substr("psql-${var.resource_prefix}-${var.environment}-${local.deterministic_suffix}", 0, 63)
  storage_account_name = substr("st${local.compact_prefix}${local.deterministic_suffix}", 0, 24)
  servicebus_name      = substr("sb-${var.resource_prefix}-${var.environment}-${local.deterministic_suffix}", 0, 50)

  tags = {
    application = "CloudKnowledge"
    environment = var.environment
    managed_by  = "Terraform"
  }

  postgres_connection_string = "Host=${azurerm_postgresql_flexible_server.cloudknowledge.fqdn};Port=5432;Database=${azurerm_postgresql_flexible_server_database.cloudknowledge.name};Username=${var.postgres_admin_login};Password=${var.postgres_admin_password};SSL Mode=Require;Trust Server Certificate=false"
}

resource "azurerm_postgresql_flexible_server" "cloudknowledge" {
  name                          = local.postgres_server_name
  resource_group_name           = data.azurerm_resource_group.cloudknowledge.name
  location                      = local.workload_location
  version                       = "18"
  administrator_login           = var.postgres_admin_login
  administrator_password        = var.postgres_admin_password
  sku_name                      = "B_Standard_B1ms"
  storage_mb                    = 32768
  backup_retention_days         = 7
  geo_redundant_backup_enabled  = false
  public_network_access_enabled = true

  tags = local.tags
}

resource "azurerm_postgresql_flexible_server_configuration" "extensions" {
  name      = "azure.extensions"
  server_id = azurerm_postgresql_flexible_server.cloudknowledge.id
  value     = "VECTOR"
}

resource "azurerm_postgresql_flexible_server_database" "cloudknowledge" {
  name      = "cloudknowledge"
  server_id = azurerm_postgresql_flexible_server.cloudknowledge.id
  collation = "en_US.utf8"
  charset   = "UTF8"

  depends_on = [
    azurerm_postgresql_flexible_server_configuration.extensions
  ]
}

resource "azurerm_postgresql_flexible_server_firewall_rule" "azure_services" {
  name             = "AllowAzureServices"
  server_id        = azurerm_postgresql_flexible_server.cloudknowledge.id
  start_ip_address = "0.0.0.0"
  end_ip_address   = "0.0.0.0"
}

resource "azurerm_storage_account" "documents" {
  name                            = local.storage_account_name
  resource_group_name             = data.azurerm_resource_group.cloudknowledge.name
  location                        = local.workload_location
  account_tier                    = "Standard"
  account_replication_type        = "LRS"
  account_kind                    = "StorageV2"
  min_tls_version                 = "TLS1_2"
  https_traffic_only_enabled      = true
  allow_nested_items_to_be_public = false

  tags = local.tags
}

resource "azurerm_storage_container" "documents" {
  name                  = "documents"
  storage_account_id    = azurerm_storage_account.documents.id
  container_access_type = "private"
}

resource "azurerm_servicebus_namespace" "cloudknowledge" {
  name                = local.servicebus_name
  location            = local.workload_location
  resource_group_name = data.azurerm_resource_group.cloudknowledge.name
  sku                 = "Standard"
  minimum_tls_version = "1.2"
  local_auth_enabled  = true

  tags = local.tags
}

resource "azurerm_servicebus_queue" "document_processing" {
  name               = "document-processing"
  namespace_id       = azurerm_servicebus_namespace.cloudknowledge.id
  lock_duration      = "PT5M"
  max_delivery_count = 3
}

resource "azurerm_servicebus_queue" "document_ready_events" {
  name               = "document-ready-events"
  namespace_id       = azurerm_servicebus_namespace.cloudknowledge.id
  lock_duration      = "PT1M"
  max_delivery_count = 5
}

resource "azurerm_log_analytics_workspace" "cloudknowledge" {
  name                = "log-${var.resource_prefix}-${var.environment}-${local.deterministic_suffix}"
  location            = local.workload_location
  resource_group_name = data.azurerm_resource_group.cloudknowledge.name
  sku                 = "PerGB2018"
  retention_in_days   = 30

  tags = local.tags
}

resource "azurerm_container_app_environment" "cloudknowledge" {
  name                       = "cae-${var.resource_prefix}-${var.environment}"
  location                   = local.workload_location
  resource_group_name        = data.azurerm_resource_group.cloudknowledge.name
  log_analytics_workspace_id = azurerm_log_analytics_workspace.cloudknowledge.id

  tags = local.tags
}

resource "azurerm_user_assigned_identity" "container_apps" {
  name                = "id-${var.resource_prefix}-${var.environment}-containers"
  location            = local.workload_location
  resource_group_name = data.azurerm_resource_group.cloudknowledge.name

  tags = local.tags
}

resource "azurerm_role_assignment" "acr_pull" {
  scope                = data.azurerm_container_registry.cloudknowledge.id
  role_definition_name = "AcrPull"
  principal_id         = azurerm_user_assigned_identity.container_apps.principal_id
}
