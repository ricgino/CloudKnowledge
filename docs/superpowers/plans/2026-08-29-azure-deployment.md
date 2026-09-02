# CloudKnowledge Azure Deployment Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deploy CloudKnowledge to Azure from a dedicated branch while preserving the existing Docker Compose/Ollama local baseline unchanged.

**Architecture:** Keep the current modular monolith plus background Worker. Run Web, API, and Worker on Azure Container Apps; keep PostgreSQL/pgvector as the retrieval store; replace local Azurite and Service Bus emulator with Azure Storage and Azure Service Bus; use a configurable Azure OpenAI provider in cloud while retaining Ollama locally. Provision Azure resources with Terraform and deploy immutable SHA-tagged images through GitHub Actions using Azure OIDC.

**Tech Stack:** .NET 10, Angular 22, Docker, Azure Container Apps, Azure Container Registry, Azure Database for PostgreSQL Flexible Server, pgvector, Azure Blob Storage, Azure Service Bus, Microsoft Entra External ID, Azure OpenAI REST, Terraform azurerm 5.x, GitHub Actions/OIDC.

**Spec:** `docs/superpowers/specs/2026-08-29-azure-deployment-design.md`

## Global Constraints

- Local baseline branch remains `feat/team-hierarchy-document-library` at base commit `1cbc0db9ba2fb10f6e6f6bb9fe239f27b74bdb5f`.
- Azure work stays on `feat/azure-deployment` and PR #6 remains draft until Azure smoke tests pass.
- Preserve modular-monolith architecture; only the existing Worker remains independently deployed for asynchronous processing.
- Preserve all authorization semantics and permission-aware retrieval rules.
- PostgreSQL/pgvector remains the vector store; do not introduce Azure AI Search.
- Ollama remains the local provider and Azure uses managed inference.
- Embedding dimensions remain 768 so existing vector schema/migrations remain valid.
- Web browser traffic remains same-origin through Nginx `/api` reverse proxy.
- Azure Container Apps use Consumption with `min_replicas = 0` and conservative maximum replicas.
- No Azure secret is committed to Git.
- Azure deploy workflow authenticates with GitHub OIDC, not a stored client secret.
- Terraform state files and `.terraform/` directories are never committed.

---

### Task 1: Azure-aware AI provider boundary

**Files:**
- Create: `src/CloudKnowledge.Infrastructure/Documents/AzureOpenAiEmbeddingGenerator.cs`
- Create: `src/CloudKnowledge.Infrastructure/Documents/AzureOpenAiAnswerGenerator.cs`
- Create: `src/CloudKnowledge.Infrastructure/Documents/AiProviderConfiguration.cs`
- Modify: `src/CloudKnowledge.Api/Program.cs`
- Modify: `src/CloudKnowledge.Worker/Program.cs`
- Modify: `src/CloudKnowledge.Api/appsettings.json`
- Modify: `src/CloudKnowledge.Worker/appsettings.json`
- Test: `tests/CloudKnowledge.Infrastructure.Tests/Documents/AzureOpenAiEmbeddingGeneratorTests.cs`
- Test: `tests/CloudKnowledge.Infrastructure.Tests/Documents/AzureOpenAiAnswerGeneratorTests.cs`
- Test: `tests/CloudKnowledge.Infrastructure.Tests/Documents/AiProviderConfigurationTests.cs`

**Interfaces:**
- Consumes: existing `IEmbeddingGenerator` and `IAnswerGenerator`.
- Produces: `AiProviderConfiguration.From(IConfiguration)` and Azure implementations of the same application interfaces.

- [ ] **Step 1: Write failing provider-selection and Azure REST contract tests**

Tests must prove:

```csharp
Assert.Equal("AzureOpenAI", options.Provider);
Assert.Equal(768, options.EmbeddingDimensions);
```

and verify embedding requests target:

```text
/openai/deployments/{embeddingDeployment}/embeddings?api-version={apiVersion}
```

with an `api-key` header, `dimensions: 768`, and a response converted to `IReadOnlyList<float[]>`.

Answer tests must verify chat requests target:

```text
/openai/deployments/{answerDeployment}/chat/completions?api-version={apiVersion}
```

and return grounded `message.content` without changing the existing `[S1]` source contract.

- [ ] **Step 2: Run the Infrastructure tests and confirm RED**

Run:

```powershell
dotnet test tests/CloudKnowledge.Infrastructure.Tests/CloudKnowledge.Infrastructure.Tests.csproj --configuration Release
```

Expected: FAIL because the Azure provider types/configuration do not exist yet.

- [ ] **Step 3: Implement minimal Azure provider classes**

Use `HttpClient` and the Azure OpenAI REST API rather than introducing an SDK dependency. The configuration parser must require these values only when `Ai:Provider=AzureOpenAI`:

```text
Ai:Endpoint
Ai:ApiKey
Ai:ApiVersion
Ai:EmbeddingDeployment
Ai:AnswerDeployment
Ai:EmbeddingDimensions
Ai:AnswerTemperature
Ai:AnswerMaxTokens
```

`Ai:Provider` defaults to `Ollama` so local settings keep working.

- [ ] **Step 4: Wire provider selection in API and Worker**

API selects both `IEmbeddingGenerator` and `IAnswerGenerator`; Worker selects only `IEmbeddingGenerator`. Local `Ollama` construction remains behaviorally identical, including `search_query: ` and `search_document: ` prefixes.

- [ ] **Step 5: Run focused and full .NET tests**

```powershell
dotnet test tests/CloudKnowledge.Infrastructure.Tests/CloudKnowledge.Infrastructure.Tests.csproj --configuration Release
dotnet test CloudKnowledge.slnx --configuration Release
```

Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/CloudKnowledge.Infrastructure src/CloudKnowledge.Api src/CloudKnowledge.Worker tests/CloudKnowledge.Infrastructure.Tests
git commit -m "feat: add Azure OpenAI provider"
```

---

### Task 2: Explicit migration execution mode

**Files:**
- Create: `src/CloudKnowledge.Api/Database/DatabaseStartupMode.cs`
- Modify: `src/CloudKnowledge.Api/Program.cs`
- Test: `tests/CloudKnowledge.Api.IntegrationTests/Database/DatabaseStartupModeTests.cs`

**Interfaces:**
- Produces: `DatabaseStartupMode.IsMigrationOnly(IReadOnlyList<string> args)`.

- [ ] **Step 1: Write a failing test**

```csharp
Assert.True(DatabaseStartupMode.IsMigrationOnly(["--migrate"]));
Assert.False(DatabaseStartupMode.IsMigrationOnly([]));
```

- [ ] **Step 2: Run test and confirm RED**

```powershell
dotnet test tests/CloudKnowledge.Api.IntegrationTests/CloudKnowledge.Api.IntegrationTests.csproj --configuration Release --filter DatabaseStartupModeTests
```

- [ ] **Step 3: Implement migration-only mode**

When `--migrate` is supplied, build services, run `CloudKnowledgeDbContext.Database.MigrateAsync()`, log success, then exit without starting HTTP listeners. Normal local Compose startup migration remains governed by `Database:ApplyMigrationsOnStartup`.

- [ ] **Step 4: Run API integration tests**

```powershell
dotnet test tests/CloudKnowledge.Api.IntegrationTests/CloudKnowledge.Api.IntegrationTests.csproj --configuration Release
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/CloudKnowledge.Api tests/CloudKnowledge.Api.IntegrationTests
git commit -m "feat: add explicit database migration mode"
```

---

### Task 3: Runtime-configurable Web API upstream

**Files:**
- Create: `src/CloudKnowledge.Web/nginx.conf.template`
- Modify: `src/CloudKnowledge.Web/Dockerfile`
- Delete: `src/CloudKnowledge.Web/nginx.conf`
- Modify: `compose.yaml` only if required to keep the current local default explicit.

**Interfaces:**
- Consumes environment variable `API_UPSTREAM`.
- Local default: `http://api:8080`.
- Azure value: `https://<internal-api-fqdn>`.

- [ ] **Step 1: Convert Nginx config to startup template**

Every existing proxy route keeps its current behavior, replacing only the fixed upstream:

```nginx
proxy_pass ${API_UPSTREAM};
```

SSE keeps:

```nginx
proxy_buffering off;
proxy_cache off;
proxy_read_timeout 3600s;
```

and `/api/ask` keeps the 120-second read timeout.

- [ ] **Step 2: Give the image a local-safe default**

Dockerfile contains:

```dockerfile
ENV API_UPSTREAM=http://api:8080
COPY src/CloudKnowledge.Web/nginx.conf.template /etc/nginx/templates/default.conf.template
```

- [ ] **Step 3: Build all containers**

```powershell
docker build -f src/CloudKnowledge.Api/Dockerfile -t cloudknowledge-api:azure-check .
docker build -f src/CloudKnowledge.Worker/Dockerfile -t cloudknowledge-worker:azure-check .
docker build -f src/CloudKnowledge.Web/Dockerfile -t cloudknowledge-web:azure-check .
```

Expected: all three builds succeed.

- [ ] **Step 4: Commit**

```bash
git add src/CloudKnowledge.Web compose.yaml
git commit -m "feat: make web api upstream environment aware"
```

---

### Task 4: Terraform bootstrap for Resource Group, ACR, and remote state

**Files:**
- Create: `infra/azure/bootstrap/versions.tf`
- Create: `infra/azure/bootstrap/variables.tf`
- Create: `infra/azure/bootstrap/main.tf`
- Create: `infra/azure/bootstrap/outputs.tf`
- Create: `infra/azure/bootstrap/terraform.tfvars.example`
- Create: `infra/azure/bootstrap/.gitignore`

**Interfaces:**
- Produces Resource Group, Standard ACR, Terraform-state Storage Account/container, and outputs consumed by platform deployment.

- [ ] **Step 1: Add Terraform provider/version constraints**

Use:

```hcl
terraform {
  required_version = ">= 1.15.0"
  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 5.0"
    }
  }
}

provider "azurerm" {
  features {}
  subscription_id = var.subscription_id
}
```

- [ ] **Step 2: Provision bootstrap resources**

Create:

```text
rg-cloudknowledge-demo
ACR Standard
StorageV2 LRS state account
private blob container tfstate
```

Names are prefix/suffix driven so globally unique resources can be created without source edits.

- [ ] **Step 3: Validate formatting/configuration**

```powershell
terraform -chdir=infra/azure/bootstrap fmt -check
terraform -chdir=infra/azure/bootstrap init -backend=false
terraform -chdir=infra/azure/bootstrap validate
```

- [ ] **Step 4: Commit**

```bash
git add infra/azure/bootstrap
git commit -m "infra: add Azure bootstrap Terraform"
```

---

### Task 5: Terraform platform resources

**Files:**
- Create: `infra/azure/platform/versions.tf`
- Create: `infra/azure/platform/backend.tf`
- Create: `infra/azure/platform/variables.tf`
- Create: `infra/azure/platform/data.tf`
- Create: `infra/azure/platform/main.tf`
- Create: `infra/azure/platform/container-apps.tf`
- Create: `infra/azure/platform/outputs.tf`
- Create: `infra/azure/platform/terraform.tfvars.example`
- Create: `infra/azure/platform/.gitignore`

**Interfaces:**
- Consumes existing Resource Group/ACR names, image tag, PostgreSQL admin password, Azure AI settings, and existing Entra tenant/client IDs.
- Produces public Web URL and migration job name.

- [ ] **Step 1: Provision managed data/messaging resources**

Use free-tier-oriented settings:

```hcl
resource "azurerm_postgresql_flexible_server" "postgres" {
  version    = "18"
  sku_name   = "B_Standard_B1ms"
  storage_mb = 32768
}

resource "azurerm_postgresql_flexible_server_configuration" "extensions" {
  name      = "azure.extensions"
  server_id = azurerm_postgresql_flexible_server.postgres.id
  value     = "VECTOR"
}
```

Also create the application database, Blob Storage LRS account/container `documents`, Service Bus Standard namespace, and queues `document-processing` and `document-ready-events`.

- [ ] **Step 2: Provision Container Apps environment and registry pull identity**

Create Log Analytics workspace, Consumption Container Apps environment, user-assigned identity, and `AcrPull` role assignment on ACR.

- [ ] **Step 3: Provision API/Worker/Web apps**

Use immutable images:

```text
<acr>/cloudknowledge-api:<image_tag>
<acr>/cloudknowledge-worker:<image_tag>
<acr>/cloudknowledge-web:<image_tag>
```

API is internal ingress on port 8080; Web is external HTTPS ingress on port 8080; Worker has no ingress. All use `min_replicas = 0`; API/Web max 2, Worker max 1.

Set Web:

```text
API_UPSTREAM=https://<api-internal-fqdn>
```

Set API/Worker secrets for PostgreSQL, Blob Storage, Service Bus, and Azure OpenAI. Do not commit values.

- [ ] **Step 4: Add migration Container Apps Job using the API image**

The job runs:

```text
dotnet CloudKnowledge.Api.dll --migrate
```

with the same PostgreSQL secret/configuration as API.

- [ ] **Step 5: Validate Terraform**

```powershell
terraform -chdir=infra/azure/platform fmt -check
terraform -chdir=infra/azure/platform init -backend=false
terraform -chdir=infra/azure/platform validate
```

Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add infra/azure/platform
git commit -m "infra: define CloudKnowledge Azure platform"
```

---

### Task 6: Azure bootstrap and deployment automation

**Files:**
- Create: `scripts/azure/bootstrap.ps1`
- Create: `scripts/azure/configure-github-oidc.ps1`
- Create: `.github/workflows/azure-validate.yml`
- Create: `.github/workflows/azure-deploy.yml`

**Interfaces:**
- Bootstrap script uses the user's active `az` subscription.
- Deploy workflow expects GitHub repository/environment variables `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`, `AZURE_RESOURCE_GROUP`, `AZURE_ACR_NAME`, and protected secrets for PostgreSQL/Azure AI.

- [ ] **Step 1: Add Azure validation workflow**

On pushes to `feat/azure-deployment` and PRs, run existing .NET/Angular/container checks plus:

```bash
terraform fmt -check -recursive infra/azure
terraform -chdir=infra/azure/bootstrap init -backend=false
terraform -chdir=infra/azure/bootstrap validate
terraform -chdir=infra/azure/platform init -backend=false
terraform -chdir=infra/azure/platform validate
```

- [ ] **Step 2: Add one-time bootstrap script**

`bootstrap.ps1` validates `az account show`, applies bootstrap Terraform, then prints the backend values and ACR login server needed by the next step.

- [ ] **Step 3: Add OIDC setup script**

The script creates or reuses an Entra application/service principal, adds a GitHub federated credential scoped to `ricgino/CloudKnowledge`, grants only the deployment roles needed at the CloudKnowledge resource-group/ACR scope, and prints the three non-secret GitHub values.

- [ ] **Step 4: Add protected deployment workflow**

Use:

```yaml
permissions:
  contents: read
  id-token: write
```

Authenticate with `azure/login`, login to ACR, build/push all three images tagged `${{ github.sha }}`, initialize the remote Terraform backend, apply platform Terraform with that SHA, start the migration job, then output the Web FQDN.

Do not deploy automatically from arbitrary pull requests. First implementation uses `workflow_dispatch` and may later add deployment on `main` after PR #6 is merged.

- [ ] **Step 5: Commit**

```bash
git add scripts/azure .github/workflows
git commit -m "ci: add Azure deployment automation"
```

---

### Task 7: Cost guardrails and deployment documentation

**Files:**
- Modify: `README.md`
- Create: `docs/azure-deployment.md`

**Interfaces:**
- Documents exactly which Azure allowances are 12-month benefits versus always-free and how to destroy the demo resources.

- [ ] **Step 1: Document architecture and free-tier posture**

Document current limits as verified on 2026-08-29:

```text
Container Apps: 180,000 vCPU-s + 360,000 GiB-s + 2M requests/month, always-free allowance
ACR Standard: 1 registry / 100 GB / 10 webhooks, 12 months
PostgreSQL Flexible Server B1ms: 750 h + 32 GB data + 32 GB backup, 12 months
Service Bus Standard: 750 h + 13M operations, 12 months
Blob Storage: 5 GB hot LRS + 20k reads + 10k writes, 12 months
```

State explicitly that managed AI inference may incur usage charges.

- [ ] **Step 2: Document first deployment commands**

Include:

```powershell
./scripts/azure/bootstrap.ps1
./scripts/azure/configure-github-oidc.ps1
```

followed by the GitHub Actions workflow dispatch and Entra External ID redirect URI update.

- [ ] **Step 3: Document teardown**

Provide Terraform destroy order: platform first, then bootstrap only when the whole demo should be removed.

- [ ] **Step 4: Commit**

```bash
git add README.md docs/azure-deployment.md
git commit -m "docs: document Azure deployment"
```

---

### Task 8: Full verification and Azure smoke test

**Files:**
- No production files unless verification exposes a defect.

- [ ] **Step 1: Run full local automated verification**

```powershell
dotnet restore CloudKnowledge.slnx
dotnet build CloudKnowledge.slnx --configuration Release --no-restore
dotnet test CloudKnowledge.slnx --configuration Release --no-build
npm --prefix src/CloudKnowledge.Web ci
npm --prefix src/CloudKnowledge.Web test -- --watch=false
npm --prefix src/CloudKnowledge.Web run build
terraform fmt -check -recursive infra/azure
terraform -chdir=infra/azure/bootstrap init -backend=false
terraform -chdir=infra/azure/bootstrap validate
terraform -chdir=infra/azure/platform init -backend=false
terraform -chdir=infra/azure/platform validate
docker build -f src/CloudKnowledge.Api/Dockerfile -t cloudknowledge-api:verify .
docker build -f src/CloudKnowledge.Worker/Dockerfile -t cloudknowledge-worker:verify .
docker build -f src/CloudKnowledge.Web/Dockerfile -t cloudknowledge-web:verify .
```

Expected: all PASS.

- [ ] **Step 2: Deploy to Azure using the protected workflow**

Confirm Terraform apply, image pulls, migration job, API health, and public Web URL.

- [ ] **Step 3: Update Entra External ID redirect URI**

Add:

```text
https://<web-fqdn>/redirect
```

without removing the localhost redirect.

- [ ] **Step 4: Run Azure E2E smoke test**

Verify: login; upload PDF/DOCX/TXT; Worker completion; SSE notification; library pagination; semantic search authorization; Ask/RAG sources; team Owner/Admin/Member authorization; team-owned delete behavior; application still works with developer PC off.

- [ ] **Step 5: Keep PR #6 draft until all smoke checks pass**

After PR #5 merges, retarget PR #6 to `main`. Do not mark ready or merge before the Azure smoke test is complete.
