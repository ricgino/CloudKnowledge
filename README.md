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
- Asynchronous document processing through Azure Service Bus semantics
- PDF, DOCX and TXT ingestion
- Blob-backed document storage
- Angular web UI
- Docker images for API, Worker and Web
- GitHub Actions for .NET, Angular, containers and Azure IaC validation

## Architecture

- ASP.NET Core API
- .NET background Worker
- Angular frontend
- PostgreSQL + pgvector
- Azure Blob Storage / Azurite for local development
- Azure Service Bus / Service Bus emulator for local development
- Ollama for local embeddings and answer generation
- Provider-neutral cloud AI boundary with direct OpenAI and AzureOpenAI support
- Microsoft Entra External ID for authentication

The application is a modular monolith with a separate background worker. Components are split only where asynchronous processing provides a clear architectural benefit.

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

Ollama intentionally remains host-side for local development. It is not the intended production AI deployment model; a cloud provider replaces it for the Azure-hosted demo.

### Stop the stack

```powershell
docker compose down
```

To also remove local PostgreSQL and Azurite data:

```powershell
docker compose down -v
```

## Azure deployment

Azure deployment work is isolated on `feat/azure-deployment` and draft PR #6 while the local Docker baseline remains on `feat/team-hierarchy-document-library`.

The Azure target keeps the same application architecture and replaces local emulators/providers with cloud services:

- Azure Container Apps for Web, API and Worker
- Azure Container Registry
- Azure Database for PostgreSQL Flexible Server + pgvector
- Azure Blob Storage
- Azure Service Bus
- Microsoft Entra External ID
- direct OpenAI API inference for the current demo, with AzureOpenAI retained as a supported provider
- Terraform remote state in Azure Storage
- GitHub Actions deployment through Microsoft Entra workload identity federation / OIDC

The current cloud demo configuration uses `text-embedding-3-small` at 768 dimensions and `gpt-4.1-nano`. Azure OpenAI discovery is retained so the deployment can move back to Azure-hosted inference when the subscription exposes a usable model catalog.

Bootstrap is intentionally explicit:

```powershell
az login
gh auth login

./scripts/azure/bootstrap.ps1
./scripts/azure/configure-github-oidc.ps1
```

After the two required GitHub Environment secrets, `POSTGRES_ADMIN_PASSWORD` and `OPENAI_API_KEY`, are configured, deployment runs manually through:

```text
Actions -> Azure Deployment -> Run workflow
```

The workflow builds immutable SHA-tagged images, pushes them to ACR, applies Terraform, runs EF Core migrations through a Container Apps Job and smoke-checks the public Web endpoint.

Full setup, cost guardrails, Entra redirect instructions, E2E checklist and teardown are documented in [`docs/azure-deployment.md`](docs/azure-deployment.md).

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

- configure the direct OpenAI API credential and PostgreSQL deployment secret
- execute the protected Azure Deployment workflow
- add the generated Azure Web redirect URI to Entra External ID
- run the complete Azure E2E smoke test with a second user
- harden networking/identity only where justified by a real production requirement

## Architecture decisions

Important architectural decisions are documented under `docs/adr` and the implementation design/plan documents under `docs/superpowers`.
