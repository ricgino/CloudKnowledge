# Hybrid Document Retrieval Design

## Context

CloudKnowledge currently answers document questions by generating one original query plus up to three focused AI retrieval queries, embedding each query, searching accessible chunks by pgvector cosine distance, and fusing the per-query rankings. The flow is permission-aware and returns grounded sources.

The ABB ACS880-01 altitude test exposed a repeatable failure mode. The query planner now generates useful focused queries such as `ACS880-01 current rating at high altitude` and `ACS880-01 altitude limitations and derating`, but vector retrieval still promotes semantically adjacent material such as marine rating tables, temperature derating sections, and table-of-contents chunks instead of the specific altitude/output-current rule needed to answer the question. The corpus also contains supplements that explicitly refer the reader to the main hardware manual for altitude derating, which semantic similarity alone does not reliably follow.

## Goal

Improve technical-document retrieval by combining semantic similarity with PostgreSQL lexical/full-text search while preserving existing document permissions, retrieval scopes, grounded-answer behavior, and focused-query planning.

The implementation must make the ABB-style failure class better in a general way: exact technical terms, parameter names, fault codes, standards, model identifiers, units, and wording such as `altitude derating` or `output current` must be able to rescue a relevant chunk that vector similarity ranks poorly.

## Non-goals

- Do not add ABB-specific rules, product-specific synonyms, or hard-coded altitude/current logic.
- Do not add an LLM reranker in this iteration.
- Do not replace pgvector semantic retrieval.
- Do not weaken permission checks or team/document retrieval scopes.
- Do not require users to re-upload PDFs or regenerate embeddings.
- Do not redesign document ingestion or PDF text extraction in this iteration.
- Do not make the regular document search UI depend on the new hybrid path unless explicitly wired later; the first consumer is Ask/RAG.

## Architecture

### 1. Lexical index in PostgreSQL

Add a stored generated `tsvector` search column for document chunks, derived from `DocumentChunks.Content`, and a GIN index on that column.

Use PostgreSQL's `simple` text-search configuration rather than an English-only dictionary. CloudKnowledge documents can be multilingual and technical identifiers must remain searchable without language-specific stemming assumptions.

The migration populates the generated value automatically for existing chunks. Existing PDFs, chunk rows, and vector embeddings remain valid; no document reprocessing is required.

### 2. Permission-aware lexical repository

Introduce an application interface dedicated to lexical chunk search. Its infrastructure implementation must reuse the same accessible-document and team-scope rules as semantic retrieval.

For every lexical search:

1. Resolve the same `DocumentRetrievalScope` used by semantic search.
2. Restrict the candidate document set with the existing `WhereAccessibleTo` logic and team access rules.
3. Build a PostgreSQL full-text query using the `simple` configuration.
4. Rank matching chunks with `ts_rank_cd`.
5. Return at most the requested candidate count.

No lexical result may bypass or broaden the semantic search permission boundary.

### 3. Hybrid per-query retrieval

Add a `HybridSearchDocumentsUseCase` used by `AskDocumentsUseCase` for each original/focused retrieval query.

For one retrieval query it performs:

- semantic search: current embedding + pgvector path;
- lexical search: new PostgreSQL full-text path;
- merge by `ChunkId`;
- combine rankings with Reciprocal Rank Fusion (RRF), using the same fixed rank constant already used elsewhere (`k = 60`);
- retain channel metadata so diagnostics can show whether a selected chunk came from semantic, lexical, or both.

Equal channel weight is the initial policy. This avoids tuning arbitrary similarity-vs-lexical score scales because RRF only depends on rank positions.

If lexical search cannot parse or execute a query, semantic retrieval remains available and Ask continues rather than failing the whole request. Infrastructure/database errors unrelated to query syntax are not silently swallowed.

### 4. General chunk-quality penalty

Apply a small ranking penalty to obvious low-information navigation chunks after hybrid fusion, before final selection.

A chunk is considered table-of-contents/index-like when multiple generic signals occur together, for example:

- contains `Table of contents` or equivalent navigation heading;
- has many dotted leaders / repeated page-number patterns;
- has many short heading-like lines and very little sentence punctuation;
- contains a dense sequence of section titles followed by page numbers.

The classifier must be content-generic and deterministic. A single heading or a table must not automatically be penalized.

This is a soft penalty, not an exclusion: a TOC chunk can still survive when no stronger evidence exists.

### 5. Final evidence selection and diversity

Keep the current focused-query evidence-coverage behavior: the best useful result from a focused query should not disappear merely because another generic chunk accumulates more RRF score across queries.

After cross-query fusion, prefer source diversity only as a tie-breaker/near-tie rule. Do not impose a hard per-document cap that could discard multiple necessary passages from one authoritative manual.

Selection order is therefore:

1. preserve evidence coverage across focused queries;
2. rank by fused score adjusted by chunk-quality penalty;
3. when candidates are effectively tied, prefer a chunk from a document not already over-represented;
4. return only the requested final source count.

### 6. Retrieval diagnostics

Extend the existing `Retrieval diagnostics` payload/UI so each retrieval query can show:

- query text and whether it is Original or Focused;
- top semantic candidates with document/chunk identifiers and semantic rank;
- top lexical candidates with document/chunk identifiers and lexical rank;
- final hybrid candidates with fused rank and channel membership (`semantic`, `lexical`, or `both`);
- whether a TOC/index quality penalty was applied;
- which chunks were ultimately selected as answer sources.

Diagnostics remain a debugging/admin aid and must not be presented as model confidence.

The current visible build SHA/version badge remains unchanged and continues to identify the exact deployed commit.

## Data model and migration

Add a generated PostgreSQL `tsvector` column to the persisted document-chunk table and a GIN index.

Conceptually:

```sql
ALTER TABLE "DocumentChunks"
ADD COLUMN "SearchVector" tsvector
GENERATED ALWAYS AS (to_tsvector('simple', coalesce("Content", ''))) STORED;

CREATE INDEX "IX_DocumentChunks_SearchVector"
ON "DocumentChunks"
USING GIN ("SearchVector");
```

The EF Core model/migration must represent this database-generated column without application writes to it.

Existing rows are indexed by PostgreSQL as part of the schema change. No application-level backfill job is required.

## Query construction

Lexical search must favor robust technical matching over natural-language cleverness.

Initial implementation:

- normalize whitespace;
- use PostgreSQL `websearch_to_tsquery('simple', query)` when possible so quoted/excluded syntax remains safe and parameters are bound;
- if the generated focused query contains punctuation-heavy identifiers, preserve them in the raw query string and let PostgreSQL tokenization handle them;
- do not concatenate user input into SQL.

The lexical path must be parameterized through EF Core/Npgsql.

## Failure handling

- Empty query: same validation behavior as current search.
- No lexical matches: semantic candidates still proceed.
- No semantic matches: lexical candidates can still proceed.
- Both channels empty: existing no-relevant-sources behavior remains.
- Lexical query syntax issue: log/debug-diagnose and fall back to semantic for that retrieval query.
- Permission/team-scope resolution failure: fail normally; never broaden scope as fallback.

## Testing strategy

### Application tests

Add regression tests proving that:

1. a chunk that is only strong in lexical search can survive hybrid fusion;
2. a chunk returned by both channels is deduplicated and receives combined RRF credit;
3. focused-query evidence coverage is preserved after hybrid fusion;
4. TOC-like chunks receive a soft penalty but are not universally removed;
5. near-tie diversity prefers a second useful document without imposing a hard document cap;
6. diagnostics accurately report semantic, lexical, fused, penalized, and selected candidates.

Use the ACS880-style scenario only as a generic regression fixture: no production code may contain ABB-specific terms or values.

### Infrastructure tests

Add PostgreSQL-backed tests proving that:

1. lexical search finds exact technical wording that semantic fixtures intentionally do not rank first;
2. inaccessible documents never appear in lexical results;
3. team scope and descendant-team scope match semantic-search behavior;
4. the generated search vector and GIN index exist after migrations;
5. existing chunk rows become searchable without reprocessing.

### API/UI tests

Extend Ask integration/UI tests to verify the richer diagnostics contract and rendering while retaining backward-safe handling for missing/empty diagnostic arrays where appropriate.

### End-to-end validation

After CI and Azure infrastructure validation pass, deploy the exact build SHA and repeat the real ABB question:

`Posso installare un ACS880-01 a 3500 metri di altitudine mantenendo la corrente nominale completa? Spiega eventuali limitazioni usando esclusivamente la documentazione disponibile.`

Success criteria are not merely that the answer text changes. Diagnostics must show whether the relevant altitude/current evidence entered through semantic, lexical, or both, and the final sources must contain the actual rule needed for the conclusion.

## Performance and cost

Hybrid retrieval adds PostgreSQL full-text queries but does not add another LLM call. The existing focused-query generator remains the only query-planning model call.

The GIN index is intended to keep lexical candidate retrieval bounded as the document corpus grows. Candidate counts remain capped before RRF fusion.

Semantic embedding generation remains unchanged in this iteration, including the existing per-query embedding calls.

## Rollout

1. Ship schema migration and lexical repository.
2. Wire hybrid retrieval only into Ask/RAG.
3. Expose richer diagnostics.
4. Validate automated tests and Azure Terraform/deployment checks.
5. Deploy immutable SHA.
6. Re-run the real ABB corpus test.
7. Only if hybrid retrieval still fails to surface the correct evidence, evaluate a separate LLM reranker design rather than adding product-specific heuristics.
