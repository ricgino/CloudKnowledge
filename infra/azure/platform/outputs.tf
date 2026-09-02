output "web_url" {
  description = "Public CloudKnowledge Web URL."
  value       = "https://${azurerm_container_app.web.ingress[0].fqdn}"
}

output "api_fqdn" {
  description = "Internal API FQDN used by the Web reverse proxy."
  value       = azurerm_container_app.api.latest_revision_fqdn
}

output "postgres_server_fqdn" {
  description = "PostgreSQL Flexible Server FQDN."
  value       = azurerm_postgresql_flexible_server.cloudknowledge.fqdn
}

output "storage_account_name" {
  description = "Storage account holding uploaded documents."
  value       = azurerm_storage_account.documents.name
}

output "servicebus_namespace_name" {
  description = "Service Bus namespace used by document processing and notification queues."
  value       = azurerm_servicebus_namespace.cloudknowledge.name
}

output "migration_job_name" {
  description = "Container Apps Job used for EF Core migrations."
  value       = azurerm_container_app_job.database_migration.name
}
