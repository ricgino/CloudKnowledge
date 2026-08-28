# Scoped RAG and Answer Performance Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add permission-safe team/subteam scoping to Semantic Search and Ask, while reducing grounded-answer latency without sacrificing retrieval correctness.

**Architecture:** Introduce an application-level retrieval scope (`All` or `Team`) shared by Search and Ask. Extract the existing Library branch/membership resolution into one infrastructure resolver and reuse it in both document listing and semantic retrieval. Apply the selected scope in SQL before vector ordering and `Take`, so out-of-scope chunks never reach the LLM. Keep the current five-source default, make Ollama generation bounded/configurable, add timing telemetry, and add a dedicated Nginx safety timeout for `/api/ask`. Angular uses one shared scope state for Search and Ask.

**Tech Stack:** .NET 10, ASP.NET Core, EF Core 10, PostgreSQL/pgvector, Ollama (`nomic-embed-text-v2-moe`, `qwen3:4b`), xUnit/Testcontainers, Angular 22/TypeScript/Vitest, Nginx, Docker Compose, GitHub Actions.

**Spec:** `docs/superpowers/specs/2026-08-27-scoped-rag-performance-design.md`

## Global Constraints

- Correctness and authorization take priority over latency.
- Keep the default RAG source count at 5 in this iteration.
- Hierarchy is structural only; membership/authorization never inherits.
- Exact-team scope requires direct membership in the selected team.
- Descendant scope is `selected branch ∩ current user's direct memberships`.
- Personal documents are excluded from team-scoped retrieval unless explicitly shared to one of the resolved team IDs.
- Apply normal `WhereAccessibleTo(currentUser)` authorization AND the selected scope before vector ordering/`Take`.
- Search and Ask must use exactly the same scope object.
- Existing callers that omit the new scope fields remain global (`all`).
- No separate vector stores, HNSW/IVFFlat changes, chunk-size changes, streaming, or source-count reduction in this iteration.
- `temperature` and `num_predict` are configuration-driven; initial values are 0.1 and 256.
- Nginx 120s timeout is a safety net, not the primary performance fix.
- Do not merge PR #5 until fresh backend/frontend/container CI is green and manual E2E/benchmark passes.

---

### Task 1: Add the shared application retrieval-scope contract

**Files:**
- Create: `src/CloudKnowledge.Application/Document/SearchDocuments/DocumentRetrievalScope.cs`
- Modify: `src/CloudKnowledge.Application/Document/SearchDocuments/IDocumentSemanticSearchRepository.cs`
- Modify: `src/CloudKnowledge.Application/Document/SearchDocuments/SearchDocumentsUseCase.cs`
- Modify: `src/CloudKnowledge.Application/Document/AskDocuments/AskDocumentsUseCase.cs`
- Modify: `tests/CloudKnowledge.Application.Tests/Documents/SearchDocuments/SearchDocumentsUseCaseTests.cs`
- Modify: `tests/CloudKnowledge.Application.Tests/Documents/AskDocuments/AskDocumentsUseCaseTests.cs`

**Contract:**

```csharp
public enum DocumentRetrievalScopeKind
{
    All,
    Team
}

public sealed record DocumentRetrievalScope(
    DocumentRetrievalScopeKind Kind,
    Guid? TeamId,
    bool IncludeDescendants)
{
    public static DocumentRetrievalScope All { get; } =
        new(DocumentRetrievalScopeKind.All, null, false);

    public static DocumentRetrievalScope ForTeam(
        Guid teamId,
        bool includeDescendants) =>
        new(DocumentRetrievalScopeKind.Team, teamId, includeDescendants);
}
```

Repository signature becomes:

```csharp
Task<IReadOnlyList<SemanticSearchResult>> SearchAccessibleAsync(
    Guid userId,
    float[] queryEmbedding,
    int take,
    DocumentRetrievalScope scope,
    CancellationToken cancellationToken);
```

Keep backward-compatible Search/Ask overloads that delegate to `DocumentRetrievalScope.All` so existing internal tests/callers do not need accidental behavioral changes.

- [ ] **Step 1: Write RED application tests**

Search tests prove `All`, exact-team, and descendant scope reach the fake repository unchanged. Ask tests prove the exact team scope passed to Ask reaches Search/repository unchanged and that no-result fallback still avoids the answer generator.

- [ ] **Step 2: Verify RED**

```powershell
dotnet test tests\CloudKnowledge.Application.Tests\CloudKnowledge.Application.Tests.csproj --configuration Release
```

Expected: compile failures for the missing retrieval scope/signatures.

- [ ] **Step 3: Implement minimum application contract/overloads**

Validate `DocumentRetrievalScope` invariants at construction/factory boundary: team ID non-empty for team scope; all scope contains no team ID and no descendants. Do not introduce membership logic in Application.

- [ ] **Step 4: Verify GREEN and commit**

```powershell
dotnet test tests\CloudKnowledge.Application.Tests\CloudKnowledge.Application.Tests.csproj --configuration Release
```

Expected: all Application tests pass.

Commit: `feat: add shared RAG retrieval scope`

---

### Task 2: Extract one team-scope resolver and enforce scope before semantic top-N

**Files:**
- Create: `src/CloudKnowledge.Application/Teams/ITeamScopeResolver.cs`
- Create: `src/CloudKnowledge.Infrastructure/Teams/EfTeamScopeResolver.cs`
- Modify: `src/CloudKnowledge.Infrastructure/Documents/EfDocumentAccessRepository.cs`
- Modify: `src/CloudKnowledge.Infrastructure/Documents/EfDocumentSemanticSearchRepository.cs`
- Modify: `tests/CloudKnowledge.Infrastructure.Tests/Documents/DocumentLibraryFiltersTests.cs`
- Modify: `tests/CloudKnowledge.Api.IntegrationTests/Documents/SemanticSearchAccessTests.cs`
- Modify: `src/CloudKnowledge.Api/Program.cs` (DI registration only for resolver)

**Resolver:**

```csharp
public interface ITeamScopeResolver
{
    Task<Guid[]> ResolveAllowedTeamIdsAsync(
        Guid userId,
        Guid selectedTeamId,
        bool includeDescendants,
        CancellationToken cancellationToken);
}
```

`EfTeamScopeResolver` contains the existing Library semantics currently private inside `EfDocumentAccessRepository`:
- exact: selected team iff direct member;
- descendants: BFS selected branch then intersect with direct memberships;
- unknown/unauthorized selection -> empty IDs.

`EfDocumentAccessRepository` receives `ITeamScopeResolver` and deletes its private duplicate resolver.

`EfDocumentSemanticSearchRepository` receives the same resolver. For team scope:

```csharp
var allowedTeamIds = await _teamScopeResolver.ResolveAllowedTeamIdsAsync(...);

accessibleDocuments = allowedTeamIds.Length == 0
    ? accessibleDocuments.Where(_ => false)
    : accessibleDocuments.Where(document =>
        (document.OwnerTeamId.HasValue &&
         allowedTeamIds.Contains(document.OwnerTeamId.Value)) ||
        _dbContext.DocumentTeamAccess.Any(access =>
            access.DocumentId == document.Id &&
            allowedTeamIds.Contains(access.TeamId)));
```

This filtered document query must be joined to chunks/embeddings **before** cosine ordering and `.Take(take)`.

- [ ] **Step 1: Write RED PostgreSQL tests**

Build hierarchy:

```text
Rai
├── DeskSharing   member
├── Booking       not member
└── HR Portal     member
```

Seed:
- user-personal private document with strongest vector match;
- DeskSharing shared document;
- DeskSharing team-owned document;
- HR Portal document;
- Booking inaccessible document with very strong vector match.

Assert:
- global returns all normally accessible docs (including personal);
- exact DeskSharing excludes personal and HR/Booking;
- `Rai + descendants` returns DeskSharing + HR only;
- Booking never appears;
- with `take=1`, an out-of-scope strongest vector cannot occupy the slot (proves filtering occurs before `Take`).

- [ ] **Step 2: Verify RED**

```powershell
dotnet test tests\CloudKnowledge.Api.IntegrationTests\CloudKnowledge.Api.IntegrationTests.csproj --configuration Release --filter SemanticSearchAccessTests
dotnet test tests\CloudKnowledge.Infrastructure.Tests\CloudKnowledge.Infrastructure.Tests.csproj --configuration Release --filter DocumentLibraryFiltersTests
```

Expected: missing scope/resolver support or wrong scoped result set.

- [ ] **Step 3: Implement resolver + repository wiring**

Do not duplicate hierarchy traversal. Update existing direct constructor usages in tests to pass `new EfTeamScopeResolver(dbContext)`.

- [ ] **Step 4: Verify GREEN + regression Library test**

Run both commands above. Existing Library Rai/DeskSharing/HR behavior must remain unchanged.

Commit: `feat: enforce team scope during semantic retrieval`

---

### Task 3: Extend Search/Ask API contracts with strict shared validation

**Files:**
- Modify: `src/CloudKnowledge.Api/Contracts/Documents/Search/SearchDocumentsRequest.cs`
- Modify: `src/CloudKnowledge.Api/Contracts/Ask/AskDocumentsRequest.cs`
- Create: `src/CloudKnowledge.Api/Contracts/Documents/RetrievalScopeRequestParser.cs`
- Modify: `src/CloudKnowledge.Api/Controllers/SearchController.cs`
- Modify: `src/CloudKnowledge.Api/Controllers/AskController.cs`
- Create: `tests/CloudKnowledge.Api.IntegrationTests/Documents/ScopedRagApiTests.cs`
- Modify if needed: `tests/CloudKnowledge.Api.IntegrationTests/CloudKnowledgeApiFactory.cs`

**Request additions (both Search and Ask):**

```csharp
public string Scope { get; init; } = "all";
public Guid? TeamId { get; init; }
public bool IncludeDescendants { get; init; }
```

Shared parser returns either a valid `DocumentRetrievalScope` or a deterministic validation error. Required behavior:
- omitted/default -> all;
- `all` + no team/no descendants -> valid;
- `team` + valid team ID -> valid;
- `team` without team ID -> 400;
- `all` with team ID -> 400;
- `all` with descendants -> 400;
- unknown scope -> 400;
- empty GUID for team -> 400.

Controllers pass the parsed scope to use cases. A valid but unauthorized team scope returns `200` with empty Search / no-information Ask, not 403/404.

For API tests that call Ask, replace `IAnswerGenerator` with a deterministic fake in the test factory so CI never needs Ollama.

- [ ] **Step 1: Add RED API tests**

Test both `/api/search` and `/api/ask` contract validation and at least one end-to-end exact-team/descendant authorization scenario.

- [ ] **Step 2: Verify RED**

```powershell
dotnet test tests\CloudKnowledge.Api.IntegrationTests\CloudKnowledge.Api.IntegrationTests.csproj --configuration Release --filter ScopedRagApiTests
```

- [ ] **Step 3: Implement parser/contracts/controllers**

Keep response contracts unchanged.

- [ ] **Step 4: Verify GREEN**

Run the filtered tests, then all API integration tests.

Commit: `feat: expose scoped search and ask APIs`

---

### Task 4: Bound Qwen generation and add Ollama timing telemetry

**Files:**
- Modify: `src/CloudKnowledge.Infrastructure/Documents/OllamaAnswerGenerator.cs`
- Modify: `tests/CloudKnowledge.Infrastructure.Tests/Documents/OllamaAnswerGeneratorTests.cs`
- Modify: `src/CloudKnowledge.Api/Program.cs`
- Modify: `src/CloudKnowledge.Api/appsettings.json`
- Modify: `compose.yaml`

**Constructor target:**

```csharp
public OllamaAnswerGenerator(
    HttpClient httpClient,
    string model,
    double temperature,
    int maxTokens,
    ILogger<OllamaAnswerGenerator> logger)
```

Validate `temperature` is sensible/non-negative and `maxTokens > 0`.

**Ollama request:** keep `stream=false`, `think=false`, plus:

```json
"options": {
  "temperature": 0.1,
  "num_predict": 256
}
```

Use `[JsonPropertyName("num_predict")]` so Ollama receives snake_case correctly.

**Ollama response telemetry fields:** nullable support for `total_duration`, `load_duration`, `prompt_eval_count`, `prompt_eval_duration`, `eval_count`, `eval_duration`, `done_reason`. Ollama duration values are nanoseconds; log human-readable milliseconds. Do not expose timings in API response.

Configuration:

```json
"AnswerTemperature": 0.1,
"AnswerMaxTokens": 256
```

Compose API environment:

```yaml
Ai__AnswerTemperature: 0.1
Ai__AnswerMaxTokens: 256
```

- [ ] **Step 1: Write RED generator test**

Assert captured request JSON has `think:false`, `/no_think`, `temperature:0.1`, `num_predict:256`. Fake response includes timing fields and final answer remains unchanged. Use a recording logger or otherwise assert the telemetry code path is exercised.

- [ ] **Step 2: Verify RED**

```powershell
dotnet test tests\CloudKnowledge.Infrastructure.Tests\CloudKnowledge.Infrastructure.Tests.csproj --configuration Release --filter OllamaAnswerGeneratorTests
```

- [ ] **Step 3: Implement options/telemetry/configuration**

Use structured logs, e.g. model, total/load/prompt/eval durations, prompt/output token counts, done reason.

- [ ] **Step 4: Verify GREEN**

Run filtered Infrastructure tests and `dotnet build CloudKnowledge.slnx --configuration Release`.

Commit: `perf: bound and measure Ollama answer generation`

---

### Task 5: Add a dedicated `/api/ask` Nginx safety timeout

**Files:**
- Modify: `src/CloudKnowledge.Web/nginx.conf`

Add this exact location **before** the generic `/api/` location:

```nginx
location = /api/ask {
    proxy_pass http://api:8080;
    proxy_http_version 1.1;
    proxy_set_header Host $host;
    proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    proxy_set_header X-Forwarded-Proto $scheme;
    proxy_read_timeout 120s;
}
```

Do not increase timeouts for unrelated endpoints.

- [ ] **Step 1: Modify config**
- [ ] **Step 2: Validate Web image/Nginx syntax**

Local optional command:

```powershell
docker build -f src\CloudKnowledge.Web\Dockerfile -t cloudknowledge-web:scoped-rag .
docker run --rm cloudknowledge-web:scoped-rag nginx -t
```

CI container build is the canonical remote gate.

Commit: `fix: allow bounded slow RAG answers through nginx`

---

### Task 6: Add one shared Knowledge scope selector in Angular

**Files:**
- Create: `src/CloudKnowledge.Web/src/app/features/knowledge/knowledge-scope.ts`
- Create: `src/CloudKnowledge.Web/src/app/features/knowledge/knowledge-scope.spec.ts`
- Modify: `src/CloudKnowledge.Web/src/app/features/documents/documents.ts`
- Modify: `src/CloudKnowledge.Web/src/app/features/documents/documents.spec.ts`
- Modify: `src/CloudKnowledge.Web/src/app/features/teams/teams.ts`
- Modify: `src/CloudKnowledge.Web/src/app/features/teams/teams.spec.ts`
- Modify: `src/CloudKnowledge.Web/src/app/features/knowledge/knowledge-page/knowledge-page.ts`
- Modify: `src/CloudKnowledge.Web/src/app/features/knowledge/knowledge-page/knowledge-page.html`
- Modify: `src/CloudKnowledge.Web/src/app/features/knowledge/knowledge-page/knowledge-page.scss`

**Pure scope state:**

```ts
export interface KnowledgeScopeState {
  teamId: string | null;
  includeDescendants: boolean;
}

export type KnowledgeRetrievalScope =
  | { scope: 'all'; teamId: null; includeDescendants: false }
  | { scope: 'team'; teamId: string; includeDescendants: boolean };
```

Helpers:
- initial state = all;
- selecting any team always resets descendants to false;
- selecting all resets descendants false;
- toggling descendants applies only with team selected;
- `toKnowledgeRetrievalScope` produces the API payload.

Add a team-option helper that exposes full hierarchy paths (e.g. `Rai / DeskSharing`) using defensive cycle/orphan handling so duplicate names are unambiguous.

`Documents.search` and `.ask` accept an optional `KnowledgeRetrievalScope` and send `scope`, `teamId`, `includeDescendants`; defaults remain global.

`KnowledgePage`:
- inject `Teams`;
- load team hierarchy on init;
- render one selector above both panels;
- selector default `All accessible knowledge`;
- team selection exact by default;
- checkbox `Include subteams` only meaningful for selected team;
- same `currentScope` passed to Search and Ask;
- scope changes clear `searchResults`, `searchSubmitted`, `answer`, `answerSources`, `errorMessage`;
- disable scope controls while Search/Ask is active to avoid stale response races;
- change `Download PDF` labels to `Download document` because DOCX/TXT are now supported.

- [ ] **Step 1: Write RED pure frontend tests**

Verify default all; team selection exact; descendant toggle; switching teams resets descendants; full path labels; request payload serialization.

- [ ] **Step 2: Verify RED**

```powershell
cd src\CloudKnowledge.Web
npm test -- --watch=false
```

- [ ] **Step 3: Implement scope helpers/client/UI**

No client-side filtering is an authorization mechanism; the client only requests a scope.

- [ ] **Step 4: Verify GREEN + build**

```powershell
npm test -- --watch=false
npm run build
```

Commit: `feat: add shared team scope to knowledge search`

---

### Task 7: Fresh full verification and manual benchmark gate

**Files:**
- No production changes unless verification finds a real root-cause defect.
- Update plan checkboxes only if desired; do not weaken tests to make CI pass.

- [ ] **Step 1: Run full backend locally/CI-equivalent**

```powershell
cd E:\Dev\CloudKnowledge
dotnet restore CloudKnowledge.slnx
dotnet build CloudKnowledge.slnx --configuration Release --no-restore
dotnet test CloudKnowledge.slnx --configuration Release --no-build
```

- [ ] **Step 2: Run full frontend**

```powershell
cd E:\Dev\CloudKnowledge\src\CloudKnowledge.Web
npm ci
npm test -- --watch=false
npm run build
```

- [ ] **Step 3: Require fresh GitHub CI on final head**

Required jobs:
- backend ✅
- frontend ✅
- containers API ✅ / Worker ✅ / Web ✅

Use `verification-before-completion`; do not claim completion from an older green commit.

- [ ] **Step 4: Manual Docker acceptance (only after CI green)**

```powershell
cd E:\Dev\CloudKnowledge
git switch feat/team-hierarchy-document-library
git pull --ff-only
docker compose up -d --build
docker compose ps
```

Open `http://localhost:4200` (do not run `npm start`).

Using the same question previously measured at ~52.20 s:
1. Ask globally and record browser Timing.
2. Check API telemetry:
   ```powershell
   docker compose logs --since 5m api
   ```
3. Verify the answer is not truncated and remains correct/grounded with expected sources.
4. Select exact known team and repeat; every returned source must belong to that selected authorized team.
5. Select parent + `Include subteams`; only direct-member descendants may contribute sources.
6. Verify inaccessible child (Booking-style case) never appears.
7. Confirm no Nginx 504 around the previous cutoff.

Performance target is directional: materially better than ~52 s, ideally ~10–20 s on the current GTX 1080, **without lowering correctness**. If generation is still too slow while correct, next iteration is streaming; do not blindly shrink context/source count.

- [ ] **Step 5: Only after manual E2E passes**

Mark PR #5 ready for review, then merge according to the existing project workflow.