# CloudKnowledge Azure Deployment Design

## Status

Approved high-level direction: keep the existing local Docker implementation intact and develop Azure deployment work on a separate branch.

- Local baseline branch: `feat/team-hierarchy-document-library`
- Azure branch: `feat/azure-deployment`
- Azure branch base commit: `1cbc0db9ba2fb10f6e6f6bb9fe239f27b74bdb5f`
- Azure work will initially be reviewed against the local baseline branch. After PR #5 is merged, the Azure PR can be retargeted to `main`.

## Goals

1. Deploy CloudKnowledge to Azure so it is reachable from the public internet without the developer PC running.
2. Preserve the current local Docker Compose workflow unchanged on the local baseline branch.
3. Keep Azure usage within the Azure Free allowances wherever practical.
4. Preserve all current authorization semantics, team hierarchy rules, document processing, semantic search, RAG, and realtime notifications.
5. Make the cloud environment reproducible through Infrastructure as Code rather than manual portal-only configuration.
6. Add deployment automation suitable for a portfolio project: container registry, managed services, secret handling, and CI/CD.

## Non-goals

- Production HA/SLA tuning for a commercial workload.
- Multi-region deployment.
- Kubernetes/AKS.
- Running Ollama itself in Azure Container Apps.
- Replacing PostgreSQL/pgvector with Azure AI Search.
- Rewriting the application into microservices.

## Branch and PR strategy

The existing branch `feat/team-hierarchy-document-library` remains the local Docker baseline and is not modified for Azure-specific work.

`feat/azure-deployment` is created directly from the current local baseline HEAD. It will contain Azure infrastructure and environment-aware application changes.

The Azure implementation should remain capable of local development when practical, but no Azure-specific change is allowed to require changes to the preserved local baseline branch.

A draft Azure PR should target `feat/team-hierarchy-document-library` while PR #5 remains open. Once PR #5 is merged, the Azure PR should be retargeted to `main`.

## Recommended Azure architecture

```text
Internet
   |
   v
Azure Container Apps
   |
   +-- cloudknowledge-web      public ingress
   |       |
   |       +-- Angular static files through nginx
   |       +-- /api/* reverse proxy to API
   |
   +-- cloudknowledge-api      internal ingress
   |       |
   |       +-- PostgreSQL Flexible Server + pgvector
   |       +-- Azure Blob Storage
   |       +-- Azure Service Bus
   |       +-- Azure AI provider
   |
   +-- cloudknowledge-worker   no public ingress
           |
           +-- PostgreSQL Flexible Server + pgvector
           +-- Azure Blob Storage
           +-- Azure Service Bus
           +-- Azure AI provider

Microsoft Entra External ID
   +-- browser authentication
   +-- API bearer-token validation

Azure Container Registry
   +-- Web image
   +-- API image
   +-- Worker image
```

## Compute

Use Azure Container Apps Consumption for Web, API, and Worker.

### Web

- Public ingress enabled.
- Container port 8080.
- Minimum replicas: 0 for cost control.
- Maximum replicas: small demo-safe cap, initially 1 or 2.
- Nginx continues to serve Angular and reverse proxy `/api` so browser traffic remains same-origin.

### API

- Internal ingress only where Container Apps environment networking permits Web-to-API service discovery.
- Container port 8080.
- Minimum replicas: 0.
- Maximum replicas: initially 2.
- Health endpoint used by Container Apps probes.

### Worker

- No public ingress.
- Consumption plan.
- Prefer Service Bus queue-based scaling through the Container Apps/KEDA Service Bus scaler so the worker can scale to zero when the queue is empty.
- Maximum replicas kept deliberately small for demo cost containment.

## Container registry

Use one Azure Container Registry Standard instance while the account's 12-month Free allowance is active.

Images:

- `cloudknowledge-web`
- `cloudknowledge-api`
- `cloudknowledge-worker`

Images are tagged with immutable Git commit SHA. A friendly `azure-latest` tag may additionally be maintained for manual inspection, but deployment must be reproducible from the immutable SHA tag.

## PostgreSQL and pgvector

Use Azure Database for PostgreSQL Flexible Server with a Burstable B1ms SKU while the 12-month Azure Free allowance applies.

Requirements:

- PostgreSQL version selected from a version supported by the application's Npgsql stack and Azure pgvector.
- Storage kept within the free allowance.
- Public network access initially restricted to the minimum necessary deployment/application path; prefer Container Apps connectivity and Azure firewall rules over a database exposed broadly to the internet.
- TLS required.
- `vector` added to `azure.extensions` and enabled in the application database with `CREATE EXTENSION IF NOT EXISTS vector;`.
- Existing EF Core migrations remain authoritative for schema creation.

The cloud deployment must not depend on the local `pgvector/pgvector` Docker image; Azure PostgreSQL supplies the extension.

## Blob storage

Replace Azurite configuration with a real Azure Storage Account and private Blob container named `documents`.

The existing `Azure.Storage.Blobs` based implementation should continue to use the same application abstraction.

Secrets/credentials must not be committed. Prefer managed identity where the application structure supports it without unnecessary complexity; otherwise use a connection string stored as a Container Apps secret for the first deploy and migrate to managed identity as a hardening step.

## Service Bus

Replace the local Service Bus emulator connection string with Azure Service Bus Standard while the 12-month Free allowance is active.

Create queues needed by the application, including:

- `document-processing`
- `document-ready-events`

Queue names remain configuration-driven.

API publishes document processing messages and consumes notification semantics as currently implemented; Worker consumes processing messages and publishes completion events.

For the first deploy, a least-privilege connection string stored as a Container Apps secret is acceptable. Managed identity/RBAC is the preferred hardening endpoint.

## AI provider

Ollama remains the local-development provider only. It must not be hosted as a continuously running 4B model in Container Apps because that would be a poor fit for free-tier compute and would create slow/large cold starts.

The Azure branch introduces a provider boundary that supports:

- local: current Ollama provider
- Azure: managed OpenAI-compatible Azure inference

Recommended first Azure deployment:

- embeddings: Azure OpenAI / Foundry deployment compatible with configurable embedding dimensions; target 768 dimensions to avoid changing the existing vector schema
- answers: a low-cost small chat model suitable for RAG answers

Exact model deployment names/endpoints are configuration values, not source constants, because Azure model availability varies by region/subscription.

The application must fail clearly at startup or at AI invocation if required Azure AI configuration is missing; it must never silently fall back to a public or unrelated AI endpoint.

A small amount of AI inference cost is acceptable for the demo because managed generative AI is not covered by the same always-free Container Apps allowance. Infrastructure must include budget/usage safeguards to minimize accidental spend.

## Authentication

Keep the existing Microsoft Entra External ID tenant and API/client registrations.

The production Web URL must be added to the SPA application's allowed redirect URIs:

- `https://<web-fqdn>/redirect`

Post logout redirect uses the same production origin.

The Angular frontend already derives its redirect URI from `window.location.origin`; Azure deployment must preserve this behavior.

API audience/tenant validation remains the existing Entra configuration.

## Same-origin routing

Keep the existing browser contract where the Angular app calls `/api` on the same origin.

Azure Web container Nginx will reverse proxy `/api/*` to the API Container App internal endpoint. This avoids a new browser CORS dependency and preserves the current SSE notification route behavior.

The API's CORS configuration remains environment-configurable for diagnostics or direct API access, but normal Azure browser traffic should not depend on cross-origin requests.

## Realtime notifications

The current SSE route `/api/notifications/stream` must continue to work through Web Nginx to the API.

Container Apps ingress and Nginx timeouts must allow the existing long-lived SSE connection. Nginx proxy buffering remains disabled for the stream endpoint.

## Configuration and secrets

No Azure secret is committed to Git.

Terraform variables may define non-secret settings such as region, resource prefix, image names, and replica limits.

Sensitive values are delivered through one of:

1. Container Apps secrets populated during Terraform/deployment.
2. GitHub Actions environment secrets for deployment-only values.
3. Azure managed identity/RBAC where implemented.

Repository `.env` remains a local-only Compose mechanism.

## Infrastructure as Code

Add Terraform under:

`infra/azure/terraform/`

Expected modules/resources include:

- resource group
- Log Analytics workspace / Container Apps environment as required
- Azure Container Registry
- Azure Container Apps Web/API/Worker
- PostgreSQL Flexible Server + database + required server parameter for `vector`
- Storage Account + Blob container
- Service Bus namespace + queues
- role assignments / identities where practical
- budget/cost alert resources where supported without requiring personal notification data in source

Use conservative defaults designed for the Free allowances and low-volume demo traffic.

Terraform state must not be committed. The first iteration may use local state for bootstrap, but the target workflow is remote state in an Azure Storage backend created by a small bootstrap step.

## Deployment automation

Retain the existing CI workflow for build/test/container validation.

Add a separate Azure deployment workflow rather than mixing cloud writes into ordinary PR CI.

The deployment workflow should:

1. authenticate to Azure using GitHub OIDC, not a long-lived Azure password
2. build Web/API/Worker images
3. push SHA-tagged images to ACR
4. run Terraform plan/apply in a protected GitHub environment or on explicitly selected branch events
5. update Container Apps to the SHA-tagged images
6. output the public Web URL

Azure deploys must not run for arbitrary pull requests from untrusted branches.

## Cost controls

Design defaults target the free account:

- Container Apps Consumption with `minReplicas = 0`
- low maximum replica counts
- PostgreSQL B1ms and free-tier storage size
- one ACR Standard registry
- Blob usage below 5 GB
- Service Bus Standard usage within the free allowance
- no Azure GPU workload
- no always-on Ollama container
- AI model selected for low token cost

Add an Azure budget with a very small monthly threshold if the subscription/billing scope allows Terraform creation. Alert destinations are supplied as variables and are not hardcoded.

The README must document which allowances are 12-month benefits and which are always-free so the deployment is not accidentally assumed to remain zero-cost forever.

## Migrations and bootstrap

Production migration behavior must be explicit.

Do not blindly reuse Compose's `Database__ApplyMigrationsOnStartup=true` as a permanent production policy.

For the first portfolio deployment, either:

- run migrations as an explicit deployment step/job, or
- temporarily enable startup migration only while there is a single API replica and document the trade-off.

Preferred endpoint: explicit migration step/job before application rollout.

The PostgreSQL `vector` extension must be allowlisted/enabled before EF migrations that rely on vector types execute.

## Testing strategy

### Existing regression suite

All current backend, frontend, and container CI must remain green.

### New automated checks

Add tests/config validation for:

- AI provider selection and missing Azure AI configuration
- local Ollama configuration remains valid
- production URL/same-origin behavior where testable
- Terraform formatting and validation
- deployment workflow syntax/static validation where practical

### Azure smoke test

After first deployment:

1. public Web URL loads
2. Entra External ID login and redirect succeeds
3. user can upload supported documents
4. Worker processes the document
5. document becomes ready and notification reaches browser
6. semantic search returns authorized content only
7. Ask/RAG returns an answer with sources
8. team ownership/roles match the local authorization semantics
9. Team Owner delete behavior works
10. Admin/Member denial remains enforced
11. direct numeric pagination works

## Rollback

Container images use immutable SHA tags. Rollback is performed by redeploying the previous known-good Web/API/Worker SHA set.

Database schema changes must remain backward-compatible during a single rollout where practical; destructive schema work is outside this first Azure deployment scope.

## Success criteria

The Azure work is complete when:

- the local baseline branch remains unchanged and usable with Docker Compose + host Ollama
- `feat/azure-deployment` contains reproducible Azure infrastructure and deployment configuration
- CI is green
- Terraform validates
- Azure resources deploy successfully from the user's Azure subscription
- the application is reachable through a public HTTPS URL while the developer PC is off
- a second real user can authenticate and exercise document/team/RAG flows
- cost settings remain within intended Free allowances except for explicitly acknowledged managed AI inference
- Azure PR can be safely reviewed/merged after the local baseline PR lands
