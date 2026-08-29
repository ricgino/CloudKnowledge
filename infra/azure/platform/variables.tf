variable "subscription_id" {
  description = "Azure subscription ID."
  type        = string
}

variable "resource_group_name" {
  description = "Resource group created by the bootstrap stack."
  type        = string
}

variable "acr_name" {
  description = "Azure Container Registry created by the bootstrap stack."
  type        = string
}

variable "image_tag" {
  description = "Immutable container image tag, normally the Git commit SHA."
  type        = string
}

variable "resource_prefix" {
  description = "Prefix used for CloudKnowledge Azure resources."
  type        = string
  default     = "cloudknowledge"
}

variable "environment" {
  description = "Environment label."
  type        = string
  default     = "demo"
}

variable "postgres_admin_login" {
  description = "PostgreSQL administrator login."
  type        = string
  default     = "cloudknowledgeadmin"
}

variable "postgres_admin_password" {
  description = "PostgreSQL administrator password."
  type        = string
  sensitive   = true
}

variable "azure_ad_instance" {
  description = "Microsoft Entra External ID instance URL."
  type        = string
  default     = "https://cloudknowledgecustomers.ciamlogin.com/"
}

variable "azure_ad_tenant_id" {
  description = "Microsoft Entra External ID tenant ID used by the API."
  type        = string
}

variable "azure_ad_api_client_id" {
  description = "Microsoft Entra application/client ID used by the API."
  type        = string
}

variable "azure_openai_endpoint" {
  description = "Azure OpenAI endpoint used by the API and Worker."
  type        = string

  validation {
    condition     = can(regex("^https://", var.azure_openai_endpoint))
    error_message = "azure_openai_endpoint must be an HTTPS URL."
  }
}

variable "azure_openai_api_key" {
  description = "Azure OpenAI API key."
  type        = string
  sensitive   = true
}

variable "azure_openai_embedding_deployment" {
  description = "Azure OpenAI embedding deployment name."
  type        = string
}

variable "azure_openai_answer_deployment" {
  description = "Azure OpenAI chat/answer deployment name."
  type        = string
}

variable "embedding_dimensions" {
  description = "Embedding dimensions aligned with the existing pgvector schema."
  type        = number
  default     = 768

  validation {
    condition     = var.embedding_dimensions == 768
    error_message = "CloudKnowledge currently requires exactly 768 embedding dimensions."
  }
}

variable "answer_temperature" {
  description = "Temperature used for RAG answers."
  type        = number
  default     = 0.1
}

variable "answer_max_tokens" {
  description = "Maximum response tokens for RAG answers."
  type        = number
  default     = 256
}

variable "api_max_replicas" {
  description = "Maximum API replicas for the demo environment."
  type        = number
  default     = 2
}

variable "web_max_replicas" {
  description = "Maximum Web replicas for the demo environment."
  type        = number
  default     = 2
}

variable "worker_max_replicas" {
  description = "Maximum Worker replicas for the demo environment."
  type        = number
  default     = 1
}
