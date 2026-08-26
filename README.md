# CloudKnowledge

CloudKnowledge is a cloud-native private document knowledge platform built with .NET, Angular and Azure-oriented infrastructure.

It demonstrates production-oriented backend and cloud engineering practices: authentication, asynchronous processing, permission-aware retrieval, vector search, grounded RAG, containerization, CI/CD, infrastructure automation and observability.

## Current capabilities

- Microsoft Entra External ID authentication
- Per-user document ownership
- Teams, membership roles and document sharing
- Permission-aware document listing
- Permission-aware semantic search with PostgreSQL/pgvector
- Grounded RAG answers with source references
- Asynchronous PDF processing through Azure Service Bus semantics
- Blob-backed document storage
- Angular web UI
- Docker images for API, Worker and Web
- GitHub Actions for .NET, Angular and container builds

## Architecture

- ASP.NET Core API
- .NET background Worker
- Angular frontend
- PostgreSQL + pgvector
- Azure Blob Storage / Azurite for local development
- Azure Service Bus / Service Bus emulator for local development
- Ollama for local embeddings and answer generation
- Microsoft Entra External ID for authentication

The application is currently a modular monolith with a separate background worker. Components are split only where asynchronous processing provides a clear architectural benefit.

## Local development

### Prerequisites

- Docker Desktop
- Ollama running on the host machine
- The following local Ollama models:

```powershell
ollama pull nomic-embed-text-v2-moe
ollama pull qwen3:4b
```

- A `.env` file based on `.env.example`

```powershell
Copy-Item .env.example .env
```

Change the example passwords before using the stack.

### Run the complete local stack

From the repository root:

```powershell
docker compose up --build
```

This starts:

- Web UI: `http://localhost:4200`
- API: `http://localhost:8080`
- PostgreSQL/pgvector
- Azurite
- Azure Service Bus emulator + SQL dependency
- Document processing Worker

The API applies EF Core migrations automatically only in the Compose environment through `Database__ApplyMigrationsOnStartup=true`. This setting is intentionally configuration-driven and should not be enabled blindly in production.

The Web container proxies `/api` to the API container, so browser traffic remains same-origin. When running Angular through `ng serve`, the Angular development proxy forwards `/api` to the local HTTPS API instead.

Ollama intentionally remains host-side for local development. It is not the intended production AI deployment model; a managed cloud AI provider will replace it for Azure deployment.

### Stop the stack

```powershell
docker compose down
```

To also remove local PostgreSQL and Azurite data:

```powershell
docker compose down -v
```

## Engineering goals

- Clean and maintainable architecture
- Asynchronous document processing
- Reliable messaging and idempotency
- Permission enforcement before retrieval
- Containerized local development
- Automated CI/CD
- Infrastructure as Code with Terraform
- Production-grade observability
- Secure authentication and secret management
- Automated testing

## Next cloud milestones

- Azure Container Registry
- Azure Container Apps
- Managed PostgreSQL
- Azure Storage
- Azure Service Bus
- Managed AI provider
- Key Vault + Managed Identity
- OpenTelemetry / Application Insights
- Terraform deployment

## Architecture decisions

Important architectural decisions will be documented as ADRs under `docs/adr`.
