data "azurerm_resource_group" "cloudknowledge" {
  name = var.resource_group_name
}

data "azurerm_container_registry" "cloudknowledge" {
  name                = var.acr_name
  resource_group_name = data.azurerm_resource_group.cloudknowledge.name
}
