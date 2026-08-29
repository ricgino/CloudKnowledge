output "resource_group_name" {
  description = "Resource group containing the CloudKnowledge demo resources."
  value       = azurerm_resource_group.cloudknowledge.name
}

output "resource_group_location" {
  description = "Metadata region of the CloudKnowledge resource group."
  value       = azurerm_resource_group.cloudknowledge.location
}

output "workload_location" {
  description = "Azure region used by CloudKnowledge workload resources."
  value       = azurerm_container_registry.cloudknowledge.location
}

output "acr_name" {
  description = "Azure Container Registry name."
  value       = azurerm_container_registry.cloudknowledge.name
}

output "acr_login_server" {
  description = "Azure Container Registry login server."
  value       = azurerm_container_registry.cloudknowledge.login_server
}

output "terraform_state_storage_account_name" {
  description = "Storage account used by the platform Terraform backend."
  value       = azurerm_storage_account.terraform_state.name
}

output "terraform_state_container_name" {
  description = "Blob container used by the platform Terraform backend."
  value       = azurerm_storage_container.terraform_state.name
}

output "terraform_state_key" {
  description = "Recommended state key for the CloudKnowledge demo platform."
  value       = "cloudknowledge-demo.tfstate"
}
