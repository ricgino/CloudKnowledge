# Scoped RAG and Answer Performance Design

Date: 2026-08-27

## Context

CloudKnowledge already provides permission-aware semantic retrieval and grounded answers over documents the current user may access. The current `Search` and `Ask` endpoints are global across all accessible knowledge and accept only the query/question plus `take`.

Manual local measurements on the current Docker stack showed:
- semantic retrieval: ~3.0 s
- full grounded answer: ~52.2 s
- minimal `qwen3:4b` prompt: ~3.9 s
- Ollama embedding model running on GPU

The main latency is therefore answer generation with the real RAG context, not PostgreSQL authorization filtering or semantic search.

The user also wants to narrow Search/Ask to a known team or team subtree when they know the organizational area but not the exact document.

## Goals

1. Allow Semantic Search and Ask to operate over either:
   - all knowledge currently accessible to the user, or
   - one selected team, optionally including accessible descendant teams.
2. Apply the scope server-side before vector similarity retrieval and before any chunk is sent to the LLM.
3. Preserve the existing authorization invariant: hierarchy never grants access; direct membership and explicit document access remain authoritative.
4. Improve answer latency without materially reducing answer correctness.
5. Add enough Ollama timing telemetry to identify future bottlenecks.
6. Prevent the reverse proxy from terminating a legitimate slow answer at the current default timeout.

## Non-goals

- No separate vector store or embedding index per team.
- No inherited team permissions.
- No change to document chunk size in this iteration.
- No HNSW/IVFFlat index work in this iteration; current semantic retrieval is already ~3 s with the local dataset.
- No response streaming in this iteration. Streaming is the next optimization if generation remains slow after the bounded improvements below.
- No reduction of default RAG source count from 5 to 3 in this iteration; correctness is prioritized over latency.

## User Experience

The Knowledge page gets one shared retrieval context selector above the Ask and Semantic Search panels.

Default state:

```text
Search in: [ All accessible knowledge v ]
```

Team state:

```text
Search in: [ Rai v ]    [ ] Include subteams
```

Rules:
- `All accessible knowledge` is the default.
- Selecting a team defaults to exact-team scope; `Include subteams` is off by default.
- The same scope applies to both Semantic Search and Ask so the two panels cannot silently search different corpora.
- Changing scope clears stale search results, answers, sources, and errors from the previous scope.
- Structural ancestors returned by the Teams API remain selectable. If the user is not a direct member of that ancestor, exact-team scope may produce no results; enabling `Include subteams` searches only descendant teams where the user is a direct member.
- The UI should show enough hierarchy/path information to disambiguate similarly named teams.

## API Contracts

### Search

`POST /api/search`

```json
{
  "query": "passaggio di stato",
  "take": 5,
  "scope": "all",
  "teamId": null,
  "includeDescendants": false
}
```

or

```json
{
  "query": "passaggio di stato",
  "take": 5,
  "scope": "team",
  "teamId": "<guid>",
  "includeDescendants": true
}
```

### Ask

`POST /api/ask`

```json
{
  "question": "Come si effettua un passaggio di stato?",
  "take": 5,
  "scope": "team",
  "teamId": "<guid>",
  "includeDescendants": false
}
```

### Validation

Accepted scopes are `all` and `team`.

Rules:
- `scope=team` requires `teamId`.
- `scope=all` must not include `teamId`.
- `includeDescendants=true` is valid only with `scope=team`.
- Unknown scope values return `400`.
- Existing `take` bounds remain unchanged.

For a valid team scope that resolves to no authorized team IDs, Search returns an empty result set and Ask returns the existing "no pertinent information" result. This follows the existing Library behavior and avoids creating a second authorization semantics for RAG.

## Authorization and Scope Resolution

The existing Library already implements the desired semantics:

- exact team: selected team only, and only if the current user is a direct member;
- team + descendants: selected branch intersected with the teams where the current user has direct membership;
- hierarchy is structural only and never grants access.

That logic must not be copied independently into the semantic repository. Extract it behind one reusable scope-resolution boundary used by both document listing and semantic retrieval.

Conceptually:

```text
selected organizational branch
        INTERSECT
current user's direct memberships
        =
authorized team IDs for this request
```

The semantic query then applies two filters before vector ordering:

1. normal `WhereAccessibleTo(currentUser)` authorization;
2. optional team-scope restriction:
   - `OwnerTeamId` in authorized team IDs, or
   - explicit `DocumentTeamAccess.TeamId` in authorized team IDs.

Personal documents that are not explicitly shared with the selected team are excluded from team-scoped Search/Ask, even if their owner is a member of that team.

This preserves the key invariant:

> Authorization and user-selected scope are enforced during retrieval, before the LLM receives any document content.

## Application Design

Introduce a small retrieval-scope value/contract shared by Search and Ask, representing:
- `All`
- `Team(teamId, includeDescendants)`

`SearchDocumentsUseCase` receives that scope and passes it to the semantic repository. `AskDocumentsUseCase` passes the same scope into Search, so Ask cannot bypass the filter.

The default remains `All` for backward-compatible behavior when callers omit the new fields.

## Infrastructure Design

Extract the current `ResolveAllowedTeamIdsAsync` behavior from `EfDocumentAccessRepository` into a reusable team-scope resolver. `EfDocumentAccessRepository` and `EfDocumentSemanticSearchRepository` both depend on this resolver.

For semantic search:

```text
Documents visible to user
  -> optional selected-team restriction
  -> join chunks/embeddings
  -> cosine distance ordering
  -> take N
```

The team filter must occur in SQL before `Take` so irrelevant chunks from other authorized teams cannot occupy the top-N slots and cannot be passed to the LLM.

## Answer Performance Changes

Correctness remains the first priority, so the number of retrieved sources stays at the current default of 5.

### Ollama generation options

Add explicit answer-generation options:
- `temperature`: low/deterministic, default `0.1`;
- `num_predict`: configurable, initial default `256` tokens;
- keep `Think=false` and the existing `/no_think` instruction.

The values should be configuration-driven rather than magic constants so the local benchmark can be tuned without changing contracts.

If manual validation shows that 256 tokens truncates otherwise correct answers, increase the configured cap rather than reducing the source count.

### Ollama telemetry

Extend the response DTO mapping to capture and log, when provided by Ollama:
- total duration;
- load duration;
- prompt evaluation token count;
- prompt evaluation duration;
- generated token count;
- generation duration;
- done reason if available.

Logging stays inside Infrastructure; API response contracts do not expose implementation timings.

This lets a future slow request answer: "Was time spent loading the model, evaluating the RAG context, or generating tokens?"

### Nginx safety timeout

Add a dedicated exact `/api/ask` proxy location with `proxy_read_timeout 120s` and the same proxy headers as the normal API route.

This is a safety net, not the primary optimization. Other API routes keep their existing timeout behavior.

## Frontend Design

Extend the Documents/Knowledge client calls to accept a shared knowledge-scope object.

The Knowledge page:
- loads the visible team hierarchy from the existing Teams service;
- renders one scope selector shared by Ask and Search;
- defaults to `all`;
- when a team is selected, sends `scope=team`, `teamId`, and `includeDescendants`;
- defaults `includeDescendants=false` after every team selection;
- clears stale answer/search state when the scope changes.

No client-side filtering is considered an authorization control. The UI only expresses the requested scope; the backend enforces it.

## Error Handling

- Invalid scope combinations return `400` from the API.
- Empty authorized scope returns normal empty Search / no-information Ask behavior, not a server error.
- Ollama/network failures continue to surface as server errors, but the new telemetry should make diagnosis faster.
- The 120 s proxy timeout prevents the current ~60 s Nginx cutoff while still bounding stalled requests.

## Testing Strategy

### Application tests
- Search forwards `All` scope correctly.
- Search forwards exact-team and descendant scope correctly.
- Ask forwards the exact same scope to Search.
- Existing validation for empty query/question and take remains green.

### Infrastructure/PostgreSQL tests
Create a hierarchy such as:

```text
Rai
├── DeskSharing   (user is member)
├── Booking       (user is not member)
└── HR Portal     (user is member)
```

Verify:
- global retrieval can return all normally accessible documents;
- exact `DeskSharing` scope returns only documents owned/shared to DeskSharing;
- `Rai + descendants` can return DeskSharing + HR Portal documents but never Booking;
- a personal document owned by the user but not shared to DeskSharing is excluded from DeskSharing scope;
- team filtering happens before top-N semantic selection.

### API integration tests
Verify `200/400` behavior for valid and invalid Search/Ask scope combinations and end-to-end authorization semantics.

### Ollama generator tests
Verify request JSON contains configured low temperature and `num_predict`, and timing fields can be deserialized/logged without changing the returned answer.

### Frontend tests
Verify:
- default scope is all;
- team selection sends exact-team scope;
- `Include subteams` sends `includeDescendants=true`;
- changing team resets descendants to false;
- changing scope clears stale results/answer;
- Search and Ask use the same selected scope.

### CI
The final head must pass:
- backend restore/build/tests;
- frontend `npm ci`, tests, build;
- API, Worker, and Web container builds.

## Manual Acceptance Test

Using the same question that previously measured ~52.2 s:

1. Ask globally and record total request time plus Ollama timing logs.
2. Ask scoped to the known team and verify the answer remains grounded and the returned sources all belong to the selected authorized scope.
3. Ask on a parent with `Include subteams` and verify inaccessible descendants never appear.
4. Compare response correctness before/after the generation-option change.
5. Confirm Nginx no longer returns a 504 around the previous timeout boundary.

Performance target is directional rather than a hard correctness gate: aim for a meaningful reduction from ~52 s, ideally toward 10-20 s on the current GTX 1080, while preserving answer quality. If latency remains high, the next design iteration is streamed answer delivery rather than further shrinking retrieval context blindly.
