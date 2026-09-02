# Hybrid Document Retrieval Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add permission-aware PostgreSQL lexical search and fuse it with the existing pgvector semantic retrieval so Ask/RAG can recover exact technical evidence without ABB-specific heuristics.

**Architecture:** Keep the existing semantic search path intact, add a parallel full-text-search repository over a generated `tsvector` column, and combine semantic/lexical ranks with reciprocal-rank fusion (`k = 60`). `AskDocumentsUseCase` consumes the hybrid per-query results, preserves focused-query evidence coverage, applies a soft generic navigation-chunk penalty, uses document diversity only for near ties, and exposes channel-level retrieval diagnostics through the API and Angular UI.

**Tech Stack:** .NET 10, EF Core 10, Npgsql/PostgreSQL full-text search, pgvector, xUnit, Testcontainers PostgreSQL, ASP.NET Core, Angular 19, Vitest, Azure Container Apps.

**Spec:** `docs/superpowers/specs/2026-08-31-hybrid-retrieval-design.md`

## Global Constraints

- No ABB-specific production rules, aliases, values, or product-specific heuristics.
- Keep current pgvector semantic retrieval and query rewriter.
- Keep exactly the same user/document/team permission boundary for lexical and semantic retrieval.
- Use PostgreSQL `simple` text-search configuration.
- Existing PDFs and embeddings must remain valid; no document reprocessing or re-upload is required.
- Do not add an LLM reranker in this iteration.
- Lexical query syntax failure may fall back to semantic for that query; permission/scope/database failures must not broaden scope or be swallowed.
- Keep the visible Git-SHA build/version badge unchanged.
- Final real-corpus success is determined only after the exact ABB question is deployed and retested; automated tests alone do not close the bug.

---

### Task 1: PostgreSQL lexical index and permission-aware lexical retrieval

**Files:**
- Create: `src/CloudKnowledge.Application/Document/SearchDocuments/LexicalSearchResult.cs`
- Create: `src/CloudKnowledge.Application/Document/SearchDocuments/IDocumentLexicalSearchRepository.cs`
- Create: `src/CloudKnowledge.Application/Document/SearchDocuments/LexicalSearchDocumentsUseCase.cs`
- Create: `src/CloudKnowledge.Infrastructure/Documents/DocumentRetrievalScopeQuery.cs`
- Create: `src/CloudKnowledge.Infrastructure/Documents/EfDocumentLexicalSearchRepository.cs`
- Modify: `src/CloudKnowledge.Infrastructure/Documents/EfDocumentSemanticSearchRepository.cs`
- Modify: `src/CloudKnowledge.Infrastructure/Persistence/Configurations/DocumentChunkConfiguration.cs`
- Create: `src/CloudKnowledge.Infrastructure/Persistence/Migrations/20260831XXXXXX_AddDocumentChunkSearchVector.cs`
- Modify: `src/CloudKnowledge.Infrastructure/Persistence/Migrations/CloudKnowledgeDbContextModelSnapshot.cs`
- Modify: `src/CloudKnowledge.Api/Program.cs`
- Test: `tests/CloudKnowledge.Api.IntegrationTests/Documents/LexicalSearchAccessTests.cs`

**Interfaces:**
- Produces: `IDocumentLexicalSearchRepository.SearchAccessibleAsync(Guid userId, string query, int take, DocumentRetrievalScope scope, CancellationToken cancellationToken)`.
- Produces: `LexicalSearchDocumentsUseCase.ExecuteAsync(string query, int take, DocumentRetrievalScope scope, CancellationToken cancellationToken)`.
- Produces: `LexicalSearchResult(Guid DocumentId, Guid ChunkId, int Position, string Content, double Rank)` where larger `Rank` is better.
- Produces: internal shared scope builder used by both semantic and lexical repositories.

- [ ] **Step 1: Write PostgreSQL-backed failing tests**

Add `LexicalSearchAccessTests` using the same `pgvector/pgvector:0.8.6-pg18` Testcontainers pattern as `ScopedSemanticSearchTests`. The test fixture must insert:

```csharp
var exactChunk = DocumentChunk.Create(
    accessibleDocument.Id,
    0,
    "At high installation altitude the rated output current requires derating.");

var distractorChunk = DocumentChunk.Create(
    inaccessibleDocument.Id,
    0,
    "High altitude rated output current derating confidential rule.");
```

Assert that a lexical query `"rated output current altitude derating"` returns `exactChunk`, never returns the inaccessible document, and respects a `DocumentRetrievalScope.Team(...)` restriction exactly as semantic search does.

In the same integration test, query PostgreSQL metadata after migrations:

```sql
SELECT data_type
FROM information_schema.columns
WHERE table_name = 'document_chunks'
  AND column_name = 'search_vector';
```

and:

```sql
SELECT indexdef
FROM pg_indexes
WHERE tablename = 'document_chunks'
  AND indexname = 'IX_document_chunks_search_vector';
```

Assert `search_vector` exists and the index definition contains `USING gin`. Insert the chunk after migrations and assert it is immediately searchable without calling document processing or embedding generation.

- [ ] **Step 2: Run the integration test and verify RED**

Run through CI or locally:

```bash
dotnet test tests/CloudKnowledge.Api.IntegrationTests/CloudKnowledge.Api.IntegrationTests.csproj --configuration Release --filter LexicalSearchAccessTests
```

Expected: compile/test failure because the lexical repository/use case/schema do not exist yet.

- [ ] **Step 3: Add lexical contracts and use case**

`LexicalSearchResult.cs`:

```csharp
namespace CloudKnowledge.Application.Documents.SearchDocuments;

public sealed record LexicalSearchResult(
    Guid DocumentId,
    Guid ChunkId,
    int Position,
    string Content,
    double Rank);
```

`IDocumentLexicalSearchRepository.cs`:

```csharp
namespace CloudKnowledge.Application.Documents.SearchDocuments;

public interface IDocumentLexicalSearchRepository
{
    Task<IReadOnlyList<LexicalSearchResult>> SearchAccessibleAsync(
        Guid userId,
        string query,
        int take,
        DocumentRetrievalScope scope,
        CancellationToken cancellationToken);
}
```

`LexicalSearchDocumentsUseCase` validates nonblank query and `take` in `1..20`, gets the current user through `ICurrentUser`, and delegates to the lexical repository.

- [ ] **Step 4: Extract one shared accessible-document scope helper**

Create an internal infrastructure helper that starts with:

```csharp
IQueryable<Document> accessibleDocuments =
    dbContext.Documents
        .AsNoTracking()
        .WhereAccessibleTo(dbContext, userId);
```

and applies `DocumentRetrievalScope.All` / `DocumentRetrievalScope.Team` with the existing `ITeamScopeResolver` logic. Refactor `EfDocumentSemanticSearchRepository` to call this helper without changing its returned order or cosine-distance behavior. The lexical repository must call the same helper.

- [ ] **Step 5: Add generated tsvector column and GIN index**

Configure a shadow property named `SearchVector` on `DocumentChunk` using `NpgsqlTypes.NpgsqlTsVector`:

```csharp
builder.Property<NpgsqlTsVector>("SearchVector")
    .HasColumnName("search_vector")
    .HasColumnType("tsvector")
    .HasComputedColumnSql(
        "to_tsvector('simple'::regconfig, coalesce(content, ''::text))",
        stored: true);

builder.HasIndex("SearchVector")
    .HasDatabaseName("IX_document_chunks_search_vector")
    .HasMethod("GIN");
```

Add a migration whose `Up` adds that stored generated column and GIN index and whose `Down` drops the index and column. Update the EF model snapshot so future migrations preserve the generated property.

- [ ] **Step 6: Implement parameterized lexical PostgreSQL search**

Use EF/Npgsql full-text APIs or a parameterized SQL projection. The resulting SQL semantics must be equivalent to:

```sql
websearch_to_tsquery('simple', @query)
```

against `search_vector`, filtered through the shared accessible-document query, ordered by `ts_rank_cd(search_vector, query)` descending, and capped before materialization. Never concatenate user text into SQL.

- [ ] **Step 7: Register lexical services**

In `Program.cs` register `IDocumentLexicalSearchRepository -> EfDocumentLexicalSearchRepository` and `LexicalSearchDocumentsUseCase` as scoped services.

- [ ] **Step 8: Run lexical integration tests and semantic scope regressions**

```bash
dotnet test tests/CloudKnowledge.Api.IntegrationTests/CloudKnowledge.Api.IntegrationTests.csproj --configuration Release --filter "LexicalSearchAccessTests|ScopedSemanticSearchTests|SemanticSearchAccessTests"
```

Expected: PASS; semantic access behavior remains unchanged.

- [ ] **Step 9: Commit**

```bash
git add src/CloudKnowledge.Application src/CloudKnowledge.Infrastructure src/CloudKnowledge.Api/Program.cs tests/CloudKnowledge.Api.IntegrationTests/Documents/LexicalSearchAccessTests.cs
git commit -m "feat: add permission-aware lexical document search"
```

---

### Task 2: Hybrid per-query fusion and navigation-chunk quality penalty

**Files:**
- Create: `src/CloudKnowledge.Application/Document/HybridSearchDocuments/HybridRetrievalChannel.cs`
- Create: `src/CloudKnowledge.Application/Document/HybridSearchDocuments/HybridSearchResult.cs`
- Create: `src/CloudKnowledge.Application/Document/HybridSearchDocuments/HybridSearchDiagnostics.cs`
- Create: `src/CloudKnowledge.Application/Document/HybridSearchDocuments/ChunkNavigationQualityClassifier.cs`
- Create: `src/CloudKnowledge.Application/Document/HybridSearchDocuments/HybridSearchDocumentsUseCase.cs`
- Test: `tests/CloudKnowledge.Application.Tests/Documents/HybridSearchDocuments/HybridSearchDocumentsUseCaseTests.cs`
- Test: `tests/CloudKnowledge.Application.Tests/Documents/HybridSearchDocuments/ChunkNavigationQualityClassifierTests.cs`

**Interfaces:**
- Consumes: `SearchDocumentsUseCase` and `LexicalSearchDocumentsUseCase`.
- Produces: per-query hybrid results containing the original `SemanticSearchResult`-compatible chunk fields, nullable cosine distance, RRF score, semantic rank, lexical rank, channel membership, and navigation-penalty flag.
- Produces: diagnostics for semantic, lexical, and fused candidates for one retrieval query.

- [ ] **Step 1: Write failing hybrid fusion tests**

Use fakes to create three chunks: semantic-only, lexical-only, and present in both. Assert:

```csharp
Assert.Contains(results, x => x.ChunkId == lexicalOnlyId);
Assert.Equal(HybridRetrievalChannel.Both, both.Channel);
Assert.True(both.FusedScore > semanticOnly.FusedScore);
```

The combined RRF score is:

```text
sum(1 / (60 + rank))
```

with 1-based ranks in each channel.

- [ ] **Step 2: Write failing navigation-quality tests**

A realistic TOC-like fixture must trigger the penalty only when at least two independent signals are present, for example:

```text
Table of contents
3 Mechanical installation............................13
4 Electrical installation............................15
5 Technical data......................................17
Ratings...............................................17
Definitions...........................................32
```

A normal technical passage containing one heading must not be penalized. Use a fixed score multiplier of `0.80` when penalized; this is a soft reduction, never exclusion.

- [ ] **Step 3: Run the new application tests and verify RED**

```bash
dotnet test tests/CloudKnowledge.Application.Tests/CloudKnowledge.Application.Tests.csproj --configuration Release --filter "HybridSearchDocumentsUseCaseTests|ChunkNavigationQualityClassifierTests"
```

Expected: compile/test failure because hybrid types do not exist.

- [ ] **Step 4: Implement channel model and RRF fusion**

`HybridRetrievalChannel` values: `Semantic`, `Lexical`, `Both`.

For a result that appears in both channels, deduplicate by `ChunkId`, keep the semantic cosine distance from the semantic candidate, sum both RRF contributions, and record both ranks. For lexical-only candidates `CosineDistance` is null.

Sort per-query hybrid candidates by adjusted fused score descending, then semantic cosine distance ascending when available, then `ChunkId` for deterministic output.

- [ ] **Step 5: Implement generic navigation classifier**

Count independent signals such as explicit `Table of contents`, three or more dotted-leader/page-number lines, and dense short heading/page-number lines with low sentence punctuation. `IsNavigationLike` returns true only when at least two signals are present.

- [ ] **Step 6: Handle lexical syntax failure narrowly**

Introduce a dedicated application/infrastructure exception for lexical query parsing failures. `HybridSearchDocumentsUseCase` catches only that exception and continues with semantic candidates. It must not catch authorization/team resolution or general PostgreSQL failures.

- [ ] **Step 7: Run tests GREEN and commit**

```bash
dotnet test tests/CloudKnowledge.Application.Tests/CloudKnowledge.Application.Tests.csproj --configuration Release --filter "HybridSearchDocumentsUseCaseTests|ChunkNavigationQualityClassifierTests"
```

Expected: PASS.

```bash
git add src/CloudKnowledge.Application/Document/HybridSearchDocuments tests/CloudKnowledge.Application.Tests/Documents/HybridSearchDocuments
git commit -m "feat: fuse semantic and lexical retrieval"
```

---

### Task 3: Use hybrid retrieval in Ask with evidence coverage and near-tie diversity

**Files:**
- Modify: `src/CloudKnowledge.Application/Document/AskDocuments/AskDocumentsUseCase.cs`
- Modify: `src/CloudKnowledge.Application/Document/AskDocuments/AskDocumentsResult.cs`
- Modify: `src/CloudKnowledge.Application/Document/AskDocuments/AskDocumentsSource.cs`
- Create: `src/CloudKnowledge.Application/Document/AskDocuments/AskRetrievalDiagnostics.cs`
- Modify: `src/CloudKnowledge.Api/Program.cs`
- Test: `tests/CloudKnowledge.Application.Tests/Documents/AskDocuments/EvidenceCoverageRetrievalTests.cs`
- Create: `tests/CloudKnowledge.Application.Tests/Documents/AskDocuments/HybridAskRetrievalTests.cs`

**Interfaces:**
- Consumes: `HybridSearchDocumentsUseCase` for every original/focused query.
- Produces: `AskDocumentsResult` with answer, sources, retrieval queries, and structured retrieval diagnostics.
- `AskDocumentsSource.Similarity` becomes `double?`; lexical-only source has `null` instead of fabricated semantic similarity.

- [ ] **Step 1: Write RED regression for the technical-evidence failure class**

Create a generic fixture where semantic search ranks broad rating/temperature/TOC chunks and lexical search uniquely returns a passage like:

```text
Above the reference installation altitude, rated output current is derated by one percent for each additional one hundred metres.
```

The focused query generator returns a generic `rated output current altitude derating` query. Assert the final answer generator context contains that lexical evidence and that it survives into final sources.

Production code and generic tests must not depend on `ACS880`, `ABB`, `3500`, or the known expected numeric answer.

- [ ] **Step 2: Add RED tests for evidence coverage and diversity**

Preserve at least the top useful result from each focused query before global fill. For near-tie diversity, define near tie as adjusted fused score within `2%` of the best candidate currently considered; within that band, prefer the candidate whose `DocumentId` has fewer already-selected chunks. Do not cap per document.

- [ ] **Step 3: Run Ask tests and verify RED**

```bash
dotnet test tests/CloudKnowledge.Application.Tests/CloudKnowledge.Application.Tests.csproj --configuration Release --filter "EvidenceCoverageRetrievalTests|HybridAskRetrievalTests"
```

- [ ] **Step 4: Replace per-query semantic calls with hybrid calls**

Keep original + up to 3 focused queries and current candidate count bounds. For each query store its full hybrid diagnostics and candidate list. Cross-query fusion may continue to use RRF, but the representative result for each chunk must preserve its hybrid channel/rank metadata and nullable cosine distance.

- [ ] **Step 5: Preserve evidence then fill with ranked/diverse candidates**

Selection sequence:

1. add the best non-duplicate result from each focused query, in query order, until `take` is reached;
2. rank remaining candidates by cross-query fused adjusted score;
3. only within the `2%` near-tie band, prefer documents with fewer selected chunks;
4. fill until `take`.

Do not reserve the original query separately because the original is the broad baseline and focused queries are the evidence-coverage mechanism.

- [ ] **Step 6: Make source similarity nullable**

If a selected source has a semantic cosine distance, expose `1.0 - distance`; otherwise expose null. Do not call lexical rank or RRF score a similarity/confidence value.

- [ ] **Step 7: Add structured diagnostics to Ask result**

For each retrieval query record:

```text
kind: original|focused
query text
semantic candidates: documentId, chunkId, rank
lexical candidates: documentId, chunkId, rank
hybrid candidates: documentId, chunkId, fused score, channels, navigationPenalty, selected
```

Cap each diagnostic candidate list at 8 items per channel/query to keep the API response bounded.

- [ ] **Step 8: Register `HybridSearchDocumentsUseCase`, run Ask tests GREEN, commit**

```bash
dotnet test tests/CloudKnowledge.Application.Tests/CloudKnowledge.Application.Tests.csproj --configuration Release --filter "EvidenceCoverageRetrievalTests|HybridAskRetrievalTests"
```

```bash
git add src/CloudKnowledge.Application/Document/AskDocuments src/CloudKnowledge.Api/Program.cs tests/CloudKnowledge.Application.Tests/Documents/AskDocuments
git commit -m "feat: use hybrid evidence retrieval for ask"
```

---

### Task 4: Expose channel diagnostics through API and Angular

**Files:**
- Modify: `src/CloudKnowledge.Api/Contracts/Ask/AskDocumentsResponse.cs`
- Modify: `src/CloudKnowledge.Api/Contracts/Ask/AskDocumentSourceResponse.cs`
- Create: `src/CloudKnowledge.Api/Contracts/Ask/AskRetrievalQueryDiagnosticsResponse.cs`
- Create: `src/CloudKnowledge.Api/Contracts/Ask/AskRetrievalCandidateResponse.cs`
- Modify: `src/CloudKnowledge.Api/Controllers/AskController.cs`
- Modify: `src/CloudKnowledge.Web/src/app/features/documents/documents.ts`
- Modify: `src/CloudKnowledge.Web/src/app/features/knowledge/knowledge-page/knowledge-page.ts`
- Modify: `src/CloudKnowledge.Web/src/app/features/knowledge/knowledge-page/knowledge-page.html`
- Modify: `src/CloudKnowledge.Web/src/app/features/knowledge/knowledge-page/knowledge-page.scss`
- Test: `tests/CloudKnowledge.Api.IntegrationTests/Documents/ScopedRagApiTests.cs`
- Create: `src/CloudKnowledge.Web/src/app/features/knowledge/knowledge-page/knowledge-page.spec.ts`

**Interfaces:**
- API `AskDocumentsResponse` keeps `answer`, `sources`, and `retrievalQueries` for compatibility and adds `retrievalDiagnostics`.
- Angular accepts empty/missing diagnostics safely and renders detailed channel information only when present.

- [ ] **Step 1: Write failing API integration assertions**

Extend `ScopedRagApiTests` to assert `retrievalDiagnostics` exists, the first entry is `original`, and candidate arrays contain channel/rank metadata. Keep the existing `retrievalQueries` assertion.

- [ ] **Step 2: Write failing Angular component test**

Mock an Ask response with one semantic-only, one lexical-only, and one both-channel candidate. Assert rendered text contains `Semantic`, `Lexical`, `Both`, and `Navigation penalty` only for the penalized candidate. Assert a lexical-only source does not render a fake percentage.

- [ ] **Step 3: Run API/frontend tests and verify RED**

```bash
dotnet test tests/CloudKnowledge.Api.IntegrationTests/CloudKnowledge.Api.IntegrationTests.csproj --configuration Release --filter ScopedRagApiTests
```

```bash
cd src/CloudKnowledge.Web && npm test -- --watch=false
```

- [ ] **Step 4: Map diagnostics in API contracts/controller**

Keep JSON field names stable through normal ASP.NET camel casing. Map nullable source similarity directly. Diagnostics must be descriptive debug metadata, never labelled confidence.

- [ ] **Step 5: Render compact nested diagnostics in Angular**

Keep the existing outer `<details>` collapsed by default. For each query render small groups for Semantic, Lexical, and Hybrid candidates, including shortened document/chunk IDs, rank/fused score, channels, selected flag, and navigation-penalty flag. Do not show these details outside the diagnostics expander.

- [ ] **Step 6: Make Ask source similarity optional in UI**

Display the existing percentage badge only when `source.similarity !== null && source.similarity !== undefined`.

- [ ] **Step 7: Run API/frontend tests GREEN and commit**

```bash
dotnet test tests/CloudKnowledge.Api.IntegrationTests/CloudKnowledge.Api.IntegrationTests.csproj --configuration Release --filter ScopedRagApiTests
cd src/CloudKnowledge.Web && npm test -- --watch=false && npm run build
```

```bash
git add src/CloudKnowledge.Api src/CloudKnowledge.Web tests/CloudKnowledge.Api.IntegrationTests/Documents/ScopedRagApiTests.cs
git commit -m "feat: expose hybrid retrieval diagnostics"
```

---

### Task 5: Full verification, immutable Azure deployment, and real ABB retest

**Files:**
- Verify only unless a failing test reveals a defect.
- If migration deployment coverage needs an explicit CI trigger, modify `.github/workflows/azure-validate.yml` only to include the persistence migration/configuration paths; do not alter runtime infrastructure semantics.

**Interfaces:**
- Final immutable build SHA is surfaced by existing `/version` and `Build <sha8>` badge.

- [ ] **Step 1: Run complete backend test suite**

```bash
dotnet restore CloudKnowledge.slnx
dotnet build CloudKnowledge.slnx --configuration Release --no-restore
dotnet test CloudKnowledge.slnx --configuration Release --no-build
```

Expected: zero failures.

- [ ] **Step 2: Run complete frontend verification**

```bash
cd src/CloudKnowledge.Web
npm ci
npm test -- --watch=false
npm run build
```

Expected: zero test failures and successful production build.

- [ ] **Step 3: Verify container builds**

```bash
docker build --file src/CloudKnowledge.Api/Dockerfile --tag cloudknowledge-api:hybrid-ci .
docker build --file src/CloudKnowledge.Worker/Dockerfile --tag cloudknowledge-worker:hybrid-ci .
docker build --file src/CloudKnowledge.Web/Dockerfile --tag cloudknowledge-web:hybrid-ci .
```

- [ ] **Step 4: Confirm GitHub CI and Azure infrastructure validation on the exact final SHA**

CI must show backend/frontend/container jobs green. Azure validation must show all validation steps green. Do not treat an older successful run as evidence for the final SHA.

- [ ] **Step 5: Deploy the exact final SHA with one `azure-apply-*` tag**

Use the existing immutable-image Azure workflow. After completion, call `/version` and assert it equals the local final SHA before testing RAG.

- [ ] **Step 6: Re-run the real ABB question unchanged**

```text
Posso installare un ACS880-01 a 3500 metri di altitudine mantenendo la corrente nominale completa? Spiega eventuali limitazioni usando esclusivamente la documentazione disponibile.
```

Inspect both the answer and expanded hybrid diagnostics. Success requires that the actual altitude/current derating evidence is among the selected sources; an answer-text change without supporting evidence is not sufficient.

- [ ] **Step 7: Close or continue based on real evidence**

If the correct evidence is selected and the grounded answer follows it, mark the ABB retrieval regression fixed. If the lexical channel still does not surface the passage, investigate the lexical tokenization/index/query using diagnostics. If the passage is surfaced but rejected by final selection, investigate fusion/diversity. If it is selected but the answer is wrong, investigate generation. Do not add product-specific heuristics; if hybrid retrieval is still insufficient, design an LLM reranker as a separate change.
