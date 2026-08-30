data "azurerm_resource_group" "cloudknowledge" {
  name = var.resource_group_name
}

data "azurerm_container_registry" "cloudknowledge" {
  name                = var.acr_name
  resource_group_name = var.resource_group_name
}

resource "azurerm_user_assigned_identity" "container_apps" {
  name                = "id-${var.resource_prefix}-${var.environment}-apps"
  location            = local.workload_location
  resource_group_name = data.azurerm_resource_group.cloudknowledge.name

  tags = local.tags
}

resource "azurerm_role_assignment" "acr_pull" {
  scope                = data.azurerm_container_registry.cloudknowledge.id
  role_definition_name = "AcrPull"
  principal_id         = azurerm_user_assigned_identity.container_apps.principal_id
}

resource "azurerm_container_app_environment" "cloudknowledge" {
  name                       = "cae-${var.resource_prefix}-${var.environment}"
  location                   = local.workload_location
  resource_group_name        = data.azurerm_resource_group.cloudknowledge.name
  log_analytics_workspace_id = azurerm_log_analytics_workspace.cloudknowledge.id
  logs_destination           = "log-analytics"

  tags = local.tags
}

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
      concurrent_requests = "50"
    }

    container {
      name   = "api"
      image  = "${data.azurerm_container_registry.cloudknowledge.login_server}/cloudknowledge-api:${var.image_tag}"
      cpu    = 0.5
      memory = "1Gi"

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
        name  = "AzureAd__TenantId"
        value = var.azure_ad_tenant_id
      }

      env {
        name  = "AzureAd__ClientId"
        value = var.azure_ad_api_client_id
      }
    }
  }

  ingress {
    external_enabled           = false
    allow_insecure_connections = true
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
    min_replicas = var.worker_min_replicas
    max_replicas = var.worker_max_replicas

    container {
      name   = "worker"
      image  = "${data.azurerm_container_registry.cloudknowledge.login_server}/cloudknowledge-worker:${var.image_tag}"
      cpu    = 1.0
      memory = "2Gi"

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

  template {
    container {
      name   = "migrate"
      image  = "${data.azurerm_container_registry.cloudknowledge.login_server}/cloudknowledge-api:${var.image_tag}"
      cpu    = 0.5
      memory = "1Gi"

      args = ["--migrate-only"]

      env {
        name        = "ConnectionStrings__Postgres"
        secret_name = "postgres-connection"
      }
    }
  }

  tags = local.tags

  depends_on = [
    azurerm_role_assignment.acr_pull,
    azurerm_postgresql_flexible_server_firewall_rule.azure_services
  ]
}
