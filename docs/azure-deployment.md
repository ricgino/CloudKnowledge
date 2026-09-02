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
   |            |              +--> OpenAI API inference
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
- Ollama remains the local AI provider.
- The deployed demo uses direct OpenAI API inference because the current Azure subscription exposes OpenAI account SKUs but returned an empty model catalog in all tested regions. AzureOpenAI remains implemented as a supported provider so the deployment can move back to Azure-hosted inference later without rewriting the application boundary.
- The deployed demo uses `text-embedding-3-small` with 768 dimensions for embeddings and `gpt-4.1-nano` for grounded RAG answers.
- The browser talks only to the public Web host. Nginx proxies `/api` to the internal API Container App, preserving same-origin browser traffic.
- API, Web and Worker use `min_replicas = 0` to reduce idle cost.
- EF Core migrations are executed through a dedicated manual Container Apps Job using `CloudKnowledge.Api.dll --migrate`.
- Images are tagged with the immutable Git commit SHA.
- GitHub Actions authenticates to Azure with OIDC. No Azure client secret is stored in GitHub.

## AI provider boundary

CloudKnowledge supports three AI providers:

```text
Ollama       local development
AzureOpenAI  Azure-hosted OpenAI-compatible inference when available
OpenAI       direct OpenAI API; current cloud-demo provider
```

The Azure deployment workflow currently version-controls these non-secret values:

```text
Provider:        OpenAI
Endpoint:        https://api.openai.com/
Embedding model: text-embedding-3-small
Answer model:    gpt-4.1-nano
Dimensions:      768
```

Only the API key is stored as a GitHub Environment secret. This keeps model selection reviewable in source control while credentials remain outside the repository.

## Current security posture

The first demo deployment uses a deliberately pragmatic database networking configuration:

- PostgreSQL requires TLS.
- PostgreSQL public network access is enabled.
- the Azure-services firewall rule is enabled so Container Apps can reach the database without a paid/complex private networking design.
- PostgreSQL credentials and the AI API key remain Container Apps secrets and Terraform sensitive variables.

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

## Step 3 - Configure deployment secrets

The current demo requires two GitHub Environment secrets:

```text
POSTGRES_ADMIN_PASSWORD
OPENAI_API_KEY
```

Create an OpenAI API key for the API project used by the demo and keep it outside the repository.

Set both secrets interactively so their values are not placed on the command line:

```powershell
gh secret set POSTGRES_ADMIN_PASSWORD `
  --repo ricgino/CloudKnowledge `
  --env azure-demo

gh secret set OPENAI_API_KEY `
  --repo ricgino/CloudKnowledge `
  --env azure-demo
```

Use a unique PostgreSQL password; do not reuse a personal password. Never paste either secret into issues, commits, logs or chat messages.

The AI endpoint and model names are not GitHub secrets or environment variables; they are versioned in `.github/workflows/azure-deploy.yml` and passed to Terraform as provider-neutral `ai_*` variables.

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

The Azure infrastructure is deliberately configured for a low-volume portfolio demo and uses scale-to-zero where supported. Azure free allowances and introductory grants depend on the subscription and can change; check Azure Cost Management before leaving the environment running long-term.

Direct OpenAI API inference is separately billed from ChatGPT subscriptions. The selected models are intended to keep low-volume demo inference inexpensive, but API usage is still metered and can generate charges.

Important consequences:

- `min_replicas = 0` is intentional for API, Web and Worker.
- ACR/PostgreSQL/Service Bus/Blob free grants may be time-limited or subscription-dependent; after benefits expire these services can generate normal charges.
- OpenAI API inference can generate charges from the first request once billing is enabled.
- Log ingestion can generate charges if free grants are exceeded.
- quotas and pricing can change; review Azure Cost Management and OpenAI API usage/billing after the first smoke test.

Recommended demo hygiene:

```text
keep test traffic low
disable OpenAI automatic credit recharge if you want strict prepaid control
avoid large document batches while evaluating AI cost
keep min replicas at zero
review Azure Cost Management after the first smoke test
review OpenAI API usage after the first RAG tests
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
