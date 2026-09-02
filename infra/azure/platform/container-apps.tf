resource "azurerm_container_app" "api" {
  name                         = "ca-${var.resource_prefix}-${var.environment}-api"
  container_app_environment_id = azurerm_container_app_environment.cloudknowledge.id
  resource_group_name          = data.azurerm_resource_group.cloudknowledge.name
  revision_mode                = "Single"

  identity {
    type         = "UserAssigned"
    identity_ids = [azurerm_user_assigned_identity.container_apps.id]
  }

  registry {
    server   = data.azurerm_container_registry.cloudknowledge.login_server
    identity = azurerm_user_assigned_identity.container_apps.id
  }

  secret {
    name  = "postgres-connection"
    value = local.postgres_connection_string
  }

  secret {
    name  = "storage-connection"
    value = azurerm_storage_account.documents.primary_connection_string
  }

  secret {
    name  = "servicebus-connection"
    value = azurerm_servicebus_namespace.cloudknowledge.default_primary_connection_string
  }

  secret {
    name  = "ai-api-key"
    value = var.ai_api_key
  }

  template {
    min_replicas = 0
    max_replicas = var.api_max_replicas

    http_scale_rule {
      name                = "http"
      concurrent_requests = "20"
    }

    custom_scale_rule {
      name             = "document-ready-events"
      custom_rule_type = "azure-servicebus"
      metadata = {
        queueName    = azurerm_servicebus_queue.document_ready_events.name
        namespace    = azurerm_servicebus_namespace.cloudknowledge.name
        messageCount = "1"
      }

      authentication {
        secret_name       = "servicebus-connection"
        trigger_parameter = "connection"
      }
    }

    container {
      name   = "api"
      image  = "${data.azurerm_container_registry.cloudknowledge.login_server}/cloudknowledge-api:${var.image_tag}"
      cpu    = 0.5
      memory = "1Gi"

      env {
        name  = "ASPNETCORE_ENVIRONMENT"
        value = "Production"
      }

      env {
        name  = "ASPNETCORE_HTTP_PORTS"
        value = "8080"
      }

      env {
        name        = "ConnectionStrings__Postgres"
        secret_name = "postgres-connection"
      }

      env {
        name        = "Storage__ConnectionString"
        secret_name = "storage-connection"
      }

      env {
        name  = "Storage__ContainerName"
        value = azurerm_storage_container.documents.name
      }

      env {
        name        = "Messaging__ConnectionString"
        secret_name = "servicebus-connection"
      }

      env {
        name  = "Messaging__QueueName"
        value = azurerm_servicebus_queue.document_processing.name
      }

      env {
        name  = "Messaging__NotificationsQueueName"
        value = azurerm_servicebus_queue.document_ready_events.name
      }

      env {
        name  = "Messaging__NotificationsEnabled"
        value = "true"
      }

      env {
        name  = "Messaging__StartupRetrySeconds"
        value = "5"
      }

      env {
        name  = "Database__ApplyMigrationsOnStartup"
        value = "false"
      }

      env {
        name  = "AzureAd__Instance"
        value = var.azure_ad_instance
      }

      env {
        name  = "AzureAd__TenantId"
        value = var.azure_ad_tenant_id
      }

      env {
        name  = "AzureAd__ClientId"
        value = var.azure_ad_api_client_id
      }

      env {
        name  = "Ai__Provider"
        value = var.ai_provider
      }

      env {
        name  = "Ai__Endpoint"
        value = var.ai_endpoint
      }

      env {
        name        = "Ai__ApiKey"
        secret_name = "ai-api-key"
      }

      env {
        name  = "Ai__EmbeddingModel"
        value = var.ai_embedding_model
      }

      env {
        name  = "Ai__AnswerModel"
        value = var.ai_answer_model
      }

      env {
        name  = "Ai__EmbeddingDimensions"
        value = tostring(var.embedding_dimensions)
      }

      env {
        name  = "Ai__AnswerTemperature"
        value = tostring(var.answer_temperature)
      }

      env {
        name  = "Ai__AnswerMaxTokens"
        value = tostring(var.answer_max_tokens)
      }

      startup_probe {
        transport               = "HTTP"
        port                    = 8080
        path                    = "/health"
        interval_seconds        = 10
        timeout                 = 3
        failure_count_threshold = 12
      }

      liveness_probe {
        transport               = "HTTP"
        port                    = 8080
        path                    = "/health"
        interval_seconds        = 30
        timeout                 = 3
        failure_count_threshold = 3
      }
    }
  }

  ingress {
    external_enabled           = false
    allow_insecure_connections = false
    target_port                = 8080
    transport                  = "http"

    traffic_weight {
      latest_revision = true
      percentage      = 100
    }
  }

  tags = local.tags

  depends_on = [
    azurerm_role_assignment.acr_pull,
    azurerm_postgresql_flexible_server_firewall_rule.azure_services
  ]
}

resource "azurerm_container_app" "worker" {
  name                         = "ca-${var.resource_prefix}-${var.environment}-worker"
  container_app_environment_id = azurerm_container_app_environment.cloudknowledge.id
  resource_group_name          = data.azurerm_resource_group.cloudknowledge.name
  revision_mode                = "Single"

  identity {
    type         = "UserAssigned"
    identity_ids = [azurerm_user_assigned_identity.container_apps.id]
  }

  registry {
    server   = data.azurerm_container_registry.cloudknowledge.login_server
    identity = azurerm_user_assigned_identity.container_apps.id
  }

  secret {
    name  = "postgres-connection"
    value = local.postgres_connection_string
  }

  secret {
    name  = "storage-connection"
    value = azurerm_storage_account.documents.primary_connection_string
  }

  secret {
    name  = "servicebus-connection"
    value = azurerm_servicebus_namespace.cloudknowledge.default_primary_connection_string
  }

  secret {
    name  = "ai-api-key"
    value = var.ai_api_key
  }

  template {
    min_replicas = 0
    max_replicas = var.worker_max_replicas

    custom_scale_rule {
      name             = "document-processing"
      custom_rule_type = "azure-servicebus"
      metadata = {
        queueName    = azurerm_servicebus_queue.document_processing.name
        namespace    = azurerm_servicebus_namespace.cloudknowledge.name
        messageCount = "1"
      }

      authentication {
        secret_name       = "servicebus-connection"
        trigger_parameter = "connection"
      }
    }

    container {
      name   = "worker"
      image  = "${data.azurerm_container_registry.cloudknowledge.login_server}/cloudknowledge-worker:${var.image_tag}"
      cpu    = 0.5
      memory = "1Gi"

      env {
        name  = "DOTNET_ENVIRONMENT"
        value = "Production"
      }

      env {
        name        = "ConnectionStrings__Postgres"
        secret_name = "postgres-connection"
      }

      env {
        name        = "Storage__ConnectionString"
        secret_name = "storage-connection"
      }

      env {
        name  = "Storage__ContainerName"
        value = azurerm_storage_container.documents.name
      }

      env {
        name        = "Messaging__ConnectionString"
        secret_name = "servicebus-connection"
      }

      env {
        name  = "Messaging__QueueName"
        value = azurerm_servicebus_queue.document_processing.name
      }

      env {
        name  = "Messaging__NotificationsQueueName"
        value = azurerm_servicebus_queue.document_ready_events.name
      }

      env {
        name  = "Messaging__MaxDeliveryCount"
        value = "3"
      }

      env {
        name  = "Messaging__StartupRetrySeconds"
        value = "5"
      }

      env {
        name  = "Ai__Provider"
        value = var.ai_provider
      }

      env {
        name  = "Ai__Endpoint"
        value = var.ai_endpoint
      }

      env {
        name        = "Ai__ApiKey"
        secret_name = "ai-api-key"
      }

      env {
        name  = "Ai__EmbeddingModel"
        value = var.ai_embedding_model
      }

      env {
        name  = "Ai__EmbeddingDimensions"
        value = tostring(var.embedding_dimensions)
      }

      env {
        name  = "Ocr__Languages"
        value = "eng+ita"
      }

      env {
        name  = "Ocr__Dpi"
        value = "300"
      }
    }
  }

  tags = local.tags

  depends_on = [
    azurerm_role_assignment.acr_pull,
    azurerm_postgresql_flexible_server_firewall_rule.azure_services
  ]
}

resource "azurerm_container_app" "web" {
  name                         = "ca-${var.resource_prefix}-${var.environment}-web"
  container_app_environment_id = azurerm_container_app_environment.cloudknowledge.id
  resource_group_name          = data.azurerm_resource_group.cloudknowledge.name
  revision_mode                = "Single"

  identity {
    type         = "UserAssigned"
    identity_ids = [azurerm_user_assigned_identity.container_apps.id]
  }

  registry {
    server   = data.azurerm_container_registry.cloudknowledge.login_server
    identity = azurerm_user_assigned_identity.container_apps.id
  }

  template {
    min_replicas = 0
    max_replicas = var.web_max_replicas

    http_scale_rule {
      name                = "http"
      concurrent_requests = "50"
    }

    container {
      name   = "web"
      image  = "${data.azurerm_container_registry.cloudknowledge.login_server}/cloudknowledge-web:${var.image_tag}"
      cpu    = 0.25
      memory = "0.5Gi"

      env {
        name  = "API_UPSTREAM"
        value = "http://ca-${var.resource_prefix}-${var.environment}-api"
      }

      env {
        name  = "APP_VERSION"
        value = var.image_tag
      }
    }
  }

  ingress {
    external_enabled           = true
    allow_insecure_connections = false
    target_port                = 8080
    transport                  = "http"

    traffic_weight {
      latest_revision = true
      percentage      = 100
    }
  }

  tags = local.tags

  depends_on = [
    azurerm_role_assignment.acr_pull
  ]
}

resource "azurerm_container_app_job" "database_migration" {
  name                         = "caj-${var.resource_prefix}-${var.environment}-migrate"
  location                     = local.workload_location
  resource_group_name          = data.azurerm_resource_group.cloudknowledge.name
  container_app_environment_id = azurerm_container_app_environment.cloudknowledge.id
  replica_timeout_in_seconds   = 600
  replica_retry_limit          = 1

  manual_trigger_config {
    parallelism              = 1
    replica_completion_count = 1
  }

  identity {
    type         = "UserAssigned"
    identity_ids = [azurerm_user_assigned_identity.container_apps.id]
  }

  registry {
    server   = data.azurerm_container_registry.cloudknowledge.login_server
    identity = azurerm_user_assigned_identity.container_apps.id
  }

  secret {
    name  = "postgres-connection"
    value = local.postgres_connection_string
  }

  secret {
    name  = "storage-connection"
    value = azurerm_storage_account.documents.primary_connection_string
  }

  secret {
    name  = "servicebus-connection"
    value = azurerm_servicebus_namespace.cloudknowledge.default_primary_connection_string
  }

  secret {
    name  = "ai-api-key"
    value = var.ai_api_key
  }

  template {
    container {
      name    = "migrate"
      image   = "${data.azurerm_container_registry.cloudknowledge.login_server}/cloudknowledge-api:${var.image_tag}"
      cpu     = 0.25
      memory  = "0.5Gi"
      command = ["dotnet"]
      args    = ["CloudKnowledge.Api.dll", "--migrate"]

      env {
        name  = "ASPNETCORE_ENVIRONMENT"
        value = "Production"
      }

      env {
        name        = "ConnectionStrings__Postgres"
        secret_name = "postgres-connection"
      }

      env {
        name        = "Storage__ConnectionString"
        secret_name = "storage-connection"
      }

      env {
        name  = "Storage__ContainerName"
        value = azurerm_storage_container.documents.name
      }

      env {
        name        = "Messaging__ConnectionString"
        secret_name = "servicebus-connection"
      }

      env {
        name  = "Messaging__QueueName"
        value = azurerm_servicebus_queue.document_processing.name
      }

      env {
        name  = "Messaging__NotificationsQueueName"
        value = azurerm_servicebus_queue.document_ready_events.name
      }

      env {
        name  = "AzureAd__Instance"
        value = var.azure_ad_instance
      }

      env {
        name  = "AzureAd__TenantId"
        value = var.azure_ad_tenant_id
      }

      env {
        name  = "AzureAd__ClientId"
        value = var.azure_ad_api_client_id
      }

      env {
        name  = "Ai__Provider"
        value = var.ai_provider
      }

      env {
        name  = "Ai__Endpoint"
        value = var.ai_endpoint
      }

      env {
        name        = "Ai__ApiKey"
        secret_name = "ai-api-key"
      }

      env {
        name  = "Ai__EmbeddingModel"
        value = var.ai_embedding_model
      }

      env {
        name  = "Ai__AnswerModel"
        value = var.ai_answer_model
      }

      env {
        name  = "Ai__EmbeddingDimensions"
        value = tostring(var.embedding_dimensions)
      }

      env {
        name  = "Ai__AnswerTemperature"
        value = tostring(var.answer_temperature)
      }

      env {
        name  = "Ai__AnswerMaxTokens"
        value = tostring(var.answer_max_tokens)
      }
    }
  }

  tags = local.tags

  depends_on = [
    azurerm_role_assignment.acr_pull,
    azurerm_postgresql_flexible_server_configuration.extensions,
    azurerm_postgresql_flexible_server_firewall_rule.azure_services
  ]
}
