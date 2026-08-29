# CloudKnowledge Azure Deployment

This document describes the first Azure deployment path for CloudKnowledge.

The cloud deployment is intentionally an extension of the local architecture, not a rewrite. Local development keeps Docker Compose, PostgreSQL/pgvector, Azurite, the Service Bus emulator and host-side Ollama. Azure replaces only the infrastructure/provider boundaries that are environment-specific.

## Target architecture

```text
Internet
   |
   v
Azure Container Apps - Web (public HTTPS, scale to zero)
   |
   | /api reverse proxy
   v
Azure Container Apps - API (internal ingress, scale to zero)
   |            |              |
   |            |              +--> managed Azure OpenAI-compatible inference
   |            |
   |            +--> Azure Blob Storage
   |
   +--> Azure Database for PostgreSQL Flexible Server + pgvector
   |
   +--> Azure Service Bus
             |
             +--> document-processing
             |        |
             |        v
             |   Container Apps Worker (scale to zero)
             |
             +--> document-ready-events
                      |
                      v
                  API notification worker
```

Deployment artifacts are stored in Azure Container Registry. Terraform state is stored in a separate private blob container created by the bootstrap stack.

## Deliberate architecture choices

- CloudKnowledge remains a modular monolith with one separate Worker for asynchronous processing.
- PostgreSQL/pgvector remains the vector store. Azure AI Search is not introduced.
- Embedding dimensions remain exactly 768, matching the current database schema.
- Ollama remains the local AI provider. Azure uses the managed provider only when `Ai:Provider=AzureOpenAI`.
- The Azure provider uses the current `/openai/v1` REST surface. Deployment names are sent in the `model` field, so no dated Azure OpenAI `api-version` setting is required.
- The browser talks only to the public Web host. Nginx proxies `/api` to the internal API Container App, preserving same-origin browser traffic.
- API, Web and Worker use `min_replicas = 0` to reduce idle cost.
- EF Core migrations are executed through a dedicated manual Container Apps Job using `CloudKnowledge.Api.dll --migrate`.
- Images are tagged with the immutable Git commit SHA.
- GitHub Actions authenticates to Azure with OIDC. No Azure client secret is stored in GitHub.

## Current security posture

The first demo deployment uses a deliberately pragmatic database networking configuration:

- PostgreSQL requires TLS.
- PostgreSQL public network access is enabled.
- the Azure-services firewall rule is enabled so Container Apps can reach the database without a paid/complex private networking design.
- PostgreSQL credentials remain Container Apps secrets and Terraform sensitive variables.

For a production hardening pass, move PostgreSQL, Storage and Service Bus to private networking/private endpoints and replace connection-string authentication with managed identity where the application/provider boundary supports it cleanly.

The API itself is already internal-only inside the Container Apps environment.

## Prerequisites

On the developer machine:

```text
Azure CLI
Terraform >= 1.15
GitHub CLI
PowerShell 7
Docker (only needed for local validation; GitHub Actions builds deployment images)
```

Authenticate first:

```powershell
az login
gh auth login
```

Verify the intended Azure subscription:

```powershell
az account show --output table
```

If necessary:

```powershell
az account set --subscription "<subscription-id-or-name>"
```

## Step 1 - Bootstrap shared Azure resources

From the repository root on `feat/azure-deployment`:

```powershell
./scripts/azure/bootstrap.ps1
```

The script runs a Terraform plan and requires typing `DEPLOY` before apply.

It creates:

- the CloudKnowledge resource group;
- one Standard Azure Container Registry;
- one LRS StorageV2 account for Terraform state;
- one private `tfstate` blob container.

For non-interactive use only when the plan has already been reviewed:

```powershell
./scripts/azure/bootstrap.ps1 -AutoApprove
```

Bootstrap output includes the resource group, ACR and remote-state names required by later steps.

## Step 2 - Configure GitHub OIDC

Run:

```powershell
./scripts/azure/configure-github-oidc.ps1
```

The script:

1. creates or reuses the `azure-demo` GitHub Environment;
2. creates or reuses the `CloudKnowledge GitHub Deploy` Microsoft Entra application and service principal;
3. creates a federated credential with subject:

```text
repo:ricgino/CloudKnowledge:environment:azure-demo
```

4. grants the deployment principal only the roles needed within CloudKnowledge resources:
   - `Contributor` on the CloudKnowledge resource group;
   - `Role Based Access Control Administrator` on the CloudKnowledge ACR only, so Terraform can assign `AcrPull` to the Container Apps identity;
   - `AcrPush` on the CloudKnowledge ACR;
   - `Storage Blob Data Contributor` on the Terraform-state storage account;
5. writes the non-secret Azure/GitHub environment variables with `gh variable set`.

The OIDC credential uses issuer:

```text
https://token.actions.githubusercontent.com/
```

and audience:

```text
api://AzureADTokenExchange
```

No client secret is created.

## Step 3 - Configure managed AI

The managed AI resource is intentionally not hard-coded in Terraform yet. Model availability, region availability and price can change independently of the application architecture.

CloudKnowledge uses the Azure OpenAI v1 REST endpoints:

```text
/openai/v1/embeddings
/openai/v1/chat/completions
```

The configured deployment name is sent as `model`; no dated `api-version` variable is required.

Create/select an Azure OpenAI / Microsoft Foundry deployment that provides:

- one embedding deployment capable of returning exactly 768 dimensions;
- one chat-completions deployment for grounded RAG answers.

Then configure these GitHub Environment variables on `azure-demo`:

```text
AZURE_OPENAI_ENDPOINT
AZURE_OPENAI_EMBEDDING_DEPLOYMENT
AZURE_OPENAI_ANSWER_DEPLOYMENT
```

The OIDC helper can set them when passed explicitly:

```powershell
./scripts/azure/configure-github-oidc.ps1 `
  -AzureOpenAiEndpoint "https://<resource>.openai.azure.com/" `
  -AzureOpenAiEmbeddingDeployment "<embedding-deployment>" `
  -AzureOpenAiAnswerDeployment "<answer-deployment>"
```

Set the two required GitHub Environment secrets interactively:

```powershell
gh secret set POSTGRES_ADMIN_PASSWORD `
  --repo ricgino/CloudKnowledge `
  --env azure-demo

gh secret set AZURE_OPENAI_API_KEY `
  --repo ricgino/CloudKnowledge `
  --env azure-demo
```

Use a unique PostgreSQL password; do not reuse a personal password.

## Step 4 - Run the deployment workflow

The workflow is intentionally manual while PR #6 is a draft:

```text
.github/workflows/azure-deploy.yml
```

In GitHub:

```text
Actions
  -> Azure Deployment
  -> Run workflow
  -> feat/azure-deployment
```

The workflow performs these operations in order:

1. verifies all required GitHub Environment values;
2. authenticates with Azure through OIDC;
3. logs in to ACR;
4. builds API, Worker and Web images;
5. pushes each image with `${github.sha}` as its tag;
6. initializes the Azure Storage Terraform backend using Azure AD authentication;
7. validates and plans the platform stack;
8. applies the platform stack;
9. starts the explicit database migration Container Apps Job;
10. waits for the migration execution to report `Succeeded`;
11. outputs the public Web URL;
12. verifies that the public Web endpoint responds successfully.

A deployment failure does not mark PR #6 ready. Fix the failure and rerun the workflow.

## Step 5 - Register the Azure Web redirect URI in Entra External ID

The Angular app already derives its redirect URI from `window.location.origin`, so the same image works locally and in Azure.

After the first deployment gives you the Web URL, add:

```text
https://<cloudknowledge-web-fqdn>/redirect
```

to the existing CloudKnowledge SPA application registration.

Keep the local redirect URI as well:

```text
http://localhost:4200/redirect
```

Do not replace the localhost URI; add the Azure URI alongside it.

## Step 6 - Azure smoke test

PR #6 must remain draft until all of these checks pass against the real Azure deployment:

- public Web page loads over HTTPS;
- Microsoft Entra External ID login completes;
- API calls authenticate successfully;
- PDF upload completes;
- DOCX upload completes;
- TXT upload completes;
- Worker starts from the processing queue and completes document processing;
- document-ready notification reaches the user;
- SSE notification stream reconnects correctly after scale-to-zero/cold start;
- document library numeric pagination works;
- filename search works;
- semantic search respects explicit team membership authorization;
- Ask/RAG returns grounded answers with valid `[S1]`, `[S2]`, etc. sources;
- parent/child team structure still grants no inherited authorization;
- Team Owner can delete a team-owned document;
- Team Admin and Member cannot delete that team-owned document;
- owning a team does not grant deletion of another user's document merely shared with that team;
- administration controls remain contained on desktop and narrow layouts;
- the application remains usable while the developer PC and local Ollama are off.

## Free-tier and cost posture

Verified against Microsoft Azure pricing/free-services pages on 2026-08-29.

| Service | Current free allowance relevant to CloudKnowledge | Period |
| --- | --- | --- |
| Azure Container Apps Consumption | 180,000 vCPU-seconds, 360,000 GiB-seconds and 2 million requests per subscription/month | Always-free monthly allowance |
| Azure Container Registry | 1 Standard registry, 100 GB storage, 10 webhooks | First 12 months for eligible new Azure accounts |
| Azure Database for PostgreSQL Flexible Server | 750 hours B1ms, 32 GB data storage, 32 GB backup storage | First 12 months for eligible new Azure accounts |
| Azure Service Bus Standard | 750 hours and 13 million Standard-tier base-unit operations | First 12 months for eligible new Azure accounts |
| Azure Blob Storage | 5 GB hot LRS block storage, 20,000 reads, 10,000 writes | First 12 months for eligible new Azure accounts |
| Azure Monitor / Log Analytics | first 5 GB/month of qualifying ingestion per billing account | Current free grant; monitor current pricing |
| Azure OpenAI / Foundry model inference | usage-dependent; Standard is pay-as-you-go by input/output tokens | Not treated as free by this project |

Important consequences:

- `min_replicas = 0` is intentional for API, Web and Worker.
- ACR/PostgreSQL/Service Bus/Blob free grants are time-limited for eligible new accounts; after the 12-month benefit expires these services can generate normal charges.
- Azure AI inference can generate charges from the first request depending on the selected model/deployment.
- Log ingestion can generate charges if the free ingestion grant is exceeded.
- quotas and pricing can change; check Azure Cost Management and the current Azure pricing pages before leaving the demo running long-term.

Recommended demo hygiene:

```text
keep test traffic low
avoid large document batches while evaluating AI cost
use small managed models where they satisfy quality requirements
keep min replicas at zero
review Azure Cost Management after the first smoke test
remove the environment when it is no longer needed
```

## Teardown

Destroy the platform first so applications stop consuming managed resources:

```powershell
terraform -chdir=infra/azure/platform init `
  -reconfigure `
  -backend-config="use_cli=true" `
  -backend-config="use_azuread_auth=true" `
  -backend-config="tenant_id=<tenant-id>" `
  -backend-config="storage_account_name=<state-account>" `
  -backend-config="container_name=tfstate" `
  -backend-config="key=cloudknowledge-demo.tfstate"

terraform -chdir=infra/azure/platform destroy
```

Review the destroy plan before approving it.

Only after the platform is gone, destroy the bootstrap stack if the complete CloudKnowledge Azure demo is no longer needed:

```powershell
terraform -chdir=infra/azure/bootstrap destroy
```

Destroying bootstrap removes the ACR and Terraform-state storage, so it must be the last step.

## Production hardening backlog

The demo is intentionally strong enough to demonstrate cloud architecture without adding infrastructure solely for complexity. If CloudKnowledge becomes a real production service, the next hardening items are:

1. private networking/private endpoints for PostgreSQL, Storage and Service Bus;
2. managed-identity authentication for Blob Storage and Service Bus instead of connection strings where practical;
3. Azure Key Vault for application secrets if operational requirements justify it;
4. tighter RBAC split between infrastructure provisioning and routine application deployments;
5. deployment slots/revision traffic strategy and rollback automation;
6. explicit backup/restore drills;
7. budget alerts and operational dashboards;
8. application-level telemetry, SLOs and alerting based on measured usage rather than portfolio-demo assumptions.
