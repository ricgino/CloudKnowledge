variable "subscription_id" {
  description = "Azure subscription ID used for the CloudKnowledge demo environment."
  type        = string
}

variable "location" {
  description = "Azure region for shared CloudKnowledge resources."
  type        = string
  default     = "westeurope"
}

variable "resource_prefix" {
  description = "Short resource prefix. Keep it lowercase and alphanumeric-friendly because it contributes to globally unique names."
  type        = string
  default     = "cloudknowledge"

  validation {
    condition     = can(regex("^[a-z0-9-]+$", var.resource_prefix))
    error_message = "resource_prefix must contain only lowercase letters, digits, and hyphens."
  }
}

variable "environment" {
  description = "Environment label."
  type        = string
  default     = "demo"

  validation {
    condition     = can(regex("^[a-z0-9-]+$", var.environment))
    error_message = "environment must contain only lowercase letters, digits, and hyphens."
  }
}

data "azurerm_client_config" "current" {}

locals {
  deterministic_suffix = substr(md5(var.subscription_id), 0, 6)
  resource_group_name   = "rg-${var.resource_prefix}-${var.environment}"
  compact_prefix        = replace("${var.resource_prefix}${var.environment}", "-", "")
  acr_name              = substr("${local.compact_prefix}${local.deterministic_suffix}", 0, 50)
  state_account_name    = substr("st${local.compact_prefix}${local.deterministic_suffix}", 0, 24)
}
