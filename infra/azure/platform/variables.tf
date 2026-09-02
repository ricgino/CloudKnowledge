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

variable "ai_provider" {
  description = "AI provider used by the deployed API and Worker."
  type        = string
  default     = "OpenAI"

  validation {
    condition     = contains(["OpenAI", "AzureOpenAI"], var.ai_provider)
    error_message = "ai_provider must be OpenAI or AzureOpenAI for the Azure deployment."
  }
}

variable "ai_endpoint" {
  description = "AI provider HTTPS endpoint used by the API and Worker."
  type        = string
  default     = "https://api.openai.com/"

  validation {
    condition     = can(regex("^https://", var.ai_endpoint))
    error_message = "ai_endpoint must be an HTTPS URL."
  }
}

variable "ai_api_key" {
  description = "AI provider API key."
  type        = string
  sensitive   = true
}

variable "ai_embedding_model" {
  description = "Embedding model or Azure OpenAI embedding deployment name."
  type        = string
  default     = "text-embedding-3-small"
}

variable "ai_answer_model" {
  description = "Answer model or Azure OpenAI answer deployment name."
  type        = string
  default     = "gpt-4.1-nano"
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
