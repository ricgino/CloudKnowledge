# Hierarchical Teams and Document Library Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add arbitrary-depth organizational team hierarchy and a scalable permission-aware Documents library with ownership, team-branch, and filename filtering.

**Architecture:** Keep the existing modular-monolith boundaries. Model hierarchy with a nullable `Team.ParentTeamId` adjacency-list self-reference, keep membership explicit, expose direct memberships plus structural ancestors through the team API, and extend the existing permission-aware document repository so all filtering happens in PostgreSQL before protected data leaves persistence. Angular consumes flat team nodes, builds a presentation tree, and sends server-side document filters.

**Tech Stack:** .NET 10, ASP.NET Core, EF Core 10, PostgreSQL/pgvector, xUnit, Angular, TypeScript, SCSS, GitHub Actions.

**Spec:** `docs/superpowers/specs/2026-08-27-team-hierarchy-document-library-design.md`

## Global Constraints

- Team hierarchy is organizational only; it never grants membership or document access.
- Parent/child memberships and roles never inherit.
- Authorization must be enforced during database retrieval, not after loading documents.
- Existing `GET /api/documents?page=1&pageSize=20` behavior remains equivalent to `scope=all`.
- Existing teams migrate as root teams with `parent_team_id = NULL`.
- No drag/drop, team moving, recursive deletion, inherited sharing, global admin, closure table, or new frontend package.
- Pagination remains server-side with page size 1..100.
- All backend, frontend, integration, and container CI jobs must be green before merge.

---

### Task 1: Persist arbitrary-depth team hierarchy

**Files:**
- Modify: `src/CloudKnowledge.Domain/Teams/Team.cs`
- Modify: `src/CloudKnowledge.Infrastructure/Persistence/Configurations/TeamConfiguration.cs`
- Create: `src/CloudKnowledge.Infrastructure/Persistence/Migrations/<timestamp>_AddTeamHierarchy.cs` via `dotnet ef migrations add AddTeamHierarchy`
- Modify: `src/CloudKnowledge.Infrastructure/Persistence/Migrations/CloudKnowledgeDbContextModelSnapshot.cs` via EF tooling
- Create/modify tests under `tests/CloudKnowledge.Domain.Tests/Teams/` and `tests/CloudKnowledge.Infrastructure.Tests/Teams/`

**Interfaces:**
- `Team.Create(string name, Guid? parentTeamId = null) -> Team`
- `Team.ParentTeamId : Guid?`
- Existing call sites using `Team.Create(name)` remain source-compatible.

- [ ] **Step 1: Add failing domain tests**

Add tests that assert root creation leaves `ParentTeamId` null and child creation stores the supplied parent id:

```csharp
[Fact]
public void Create_WithoutParent_CreatesRootTeam()
{
    var team = Team.Create("Rai");
    Assert.Null(team.ParentTeamId);
}

[Fact]
public void Create_WithParent_StoresParentTeamId()
{
    var parentId = Guid.NewGuid();
    var team = Team.Create("DeskSharing", parentId);
    Assert.Equal(parentId, team.ParentTeamId);
}
```

- [ ] **Step 2: Run the focused domain tests and verify RED**

Run:

```powershell
dotnet test tests/CloudKnowledge.Domain.Tests/CloudKnowledge.Domain.Tests.csproj --configuration Release
```

Expected: compilation/test failure because `ParentTeamId` and the new `Create` argument do not exist yet.

- [ ] **Step 3: Implement the minimal domain change**

`Team` must expose:

```csharp
public Guid? ParentTeamId { get; private set; }
```

The private constructor becomes conceptually:

```csharp
private Team(Guid id, string name, Guid? parentTeamId, DateTime createdAtUtc)
```

and creation remains backward-compatible:

```csharp
public static Team Create(string name, Guid? parentTeamId = null)
```

Keep the existing name validation and trim behavior.

- [ ] **Step 4: Configure the self-reference and index**

In `TeamConfiguration` map:

```csharp
builder.Property(team => team.ParentTeamId)
    .HasColumnName("parent_team_id");

builder.HasOne<Team>()
    .WithMany()
    .HasForeignKey(team => team.ParentTeamId)
    .OnDelete(DeleteBehavior.Restrict);

builder.HasIndex(team => team.ParentTeamId)
    .HasDatabaseName("ix_teams_parent_team_id");
```

- [ ] **Step 5: Generate the EF migration**

Run from repository root:

```powershell
dotnet tool restore
dotnet ef migrations add AddTeamHierarchy `
  --project src\CloudKnowledge.Infrastructure `
  --startup-project src\CloudKnowledge.Api
```

Inspect the generated migration: it must add nullable `parent_team_id`, FK to `teams(id)` with restrictive delete behavior, and the parent index. It must not rewrite existing team rows.

- [ ] **Step 6: Add an infrastructure persistence test**

Persist a root and child team in the PostgreSQL test container, reload with `AsNoTracking`, and assert the child retains the parent id while the root remains null.

- [ ] **Step 7: Run focused tests then commit**

```powershell
dotnet test tests/CloudKnowledge.Domain.Tests/CloudKnowledge.Domain.Tests.csproj --configuration Release
dotnet test tests/CloudKnowledge.Infrastructure.Tests/CloudKnowledge.Infrastructure.Tests.csproj --configuration Release
git add src tests
git commit -m "feat: persist hierarchical teams"
```

---

### Task 2: Authorize root and child-team creation

**Files:**
- Modify: `src/CloudKnowledge.Application/Teams/CreateTeam/CreateTeamUseCase.cs`
- Modify: `src/CloudKnowledge.Application/Teams/CreateTeam/CreateTeamResult.cs`
- Modify: `src/CloudKnowledge.Application/Teams/ITeamRepository.cs`
- Modify: `src/CloudKnowledge.Infrastructure/Teams/EfTeamRepository.cs`
- Modify: `src/CloudKnowledge.Api/Contracts/Teams/CreateTeamRequest.cs`
- Modify: `src/CloudKnowledge.Api/Contracts/Teams/TeamResponse.cs`
- Modify: `src/CloudKnowledge.Api/Controllers/TeamsController.cs`
- Modify: `tests/CloudKnowledge.Application.Tests/Teams/CreateTeam/CreateTeamUseCaseTests.cs`
- Add API integration coverage under `tests/CloudKnowledge.Api.IntegrationTests/Teams/`

**Interfaces:**
- `CreateTeamUseCase.ExecuteAsync(string name, Guid? parentTeamId, CancellationToken) -> CreateTeamResult`
- `ITeamRepository.GetByIdAsync(Guid teamId, CancellationToken) -> Team?`
- Child creation requires a direct parent membership with role `Admin` or `Owner`.

- [ ] **Step 1: Extend application tests first**

Add cases for:

```text
root -> succeeds, creator becomes Owner
parent Owner -> child succeeds
parent Admin -> child succeeds
parent Member -> Forbidden
not a parent member -> ParentNotFoundOrNotMember
missing parent -> ParentNotFoundOrNotMember
child creation does not copy any parent members
```

The test doubles must expose parent lookup and membership role; assertions must verify `ParentTeamId` on the stored child and exactly one owner membership for the creator.

- [ ] **Step 2: Run application tests and verify RED**

```powershell
dotnet test tests/CloudKnowledge.Application.Tests/CloudKnowledge.Application.Tests.csproj --configuration Release
```

- [ ] **Step 3: Add explicit creation status**

Use a result/status model that can represent:

```csharp
public enum CreateTeamStatus
{
    Created,
    ParentNotFoundOrNotMember,
    Forbidden
}
```

`CreateTeamResult` carries `Status`, optional created team fields, `ParentTeamId`, and creator role when created. Do not use exceptions for expected authorization outcomes.

- [ ] **Step 4: Implement parent authorization**

Algorithm in `CreateTeamUseCase`:

```text
currentUserId = current user
if parentTeamId is null:
    create root + Owner membership
else:
    parent = repository.GetByIdAsync(parentTeamId)
    if missing -> ParentNotFoundOrNotMember
    membership = membershipRepository.GetMembershipAsync(parentTeamId, currentUserId)
    if missing -> ParentNotFoundOrNotMember
    if role not Admin/Owner -> Forbidden
    create child(parentTeamId) + Owner membership for current user
```

Do not add inherited memberships.

- [ ] **Step 5: Update API contract and controller mapping**

Request:

```csharp
public sealed record CreateTeamRequest(string Name, Guid? ParentTeamId);
```

Created response must include the hierarchy/access fields that Task 3 standardizes. Controller behavior:

```text
Created -> 201
ParentNotFoundOrNotMember -> 404
Forbidden -> 403
```

- [ ] **Step 6: Add integration tests for API behavior**

Use real PostgreSQL infrastructure and verify root creation, authorized child creation, and forbidden Member creation.

- [ ] **Step 7: Run tests and commit**

```powershell
dotnet test CloudKnowledge.slnx --configuration Release
git add src tests
git commit -m "feat: authorize child team creation"
```

---

### Task 3: Return a navigable team hierarchy without widening access

**Files:**
- Modify: `src/CloudKnowledge.Application/Teams/GetTeams/GetTeamsResult.cs`
- Modify: `src/CloudKnowledge.Application/Teams/GetTeams/GetTeamsUseCase.cs` only if orchestration is needed
- Modify: `src/CloudKnowledge.Application/Teams/ITeamRepository.cs`
- Modify: `src/CloudKnowledge.Infrastructure/Teams/EfTeamRepository.cs`
- Modify: `src/CloudKnowledge.Api/Contracts/Teams/TeamResponse.cs`
- Modify: `src/CloudKnowledge.Api/Controllers/TeamsController.cs`
- Add tests under `tests/CloudKnowledge.Infrastructure.Tests/Teams/`
- Add API integration tests under `tests/CloudKnowledge.Api.IntegrationTests/Teams/`

**Interfaces:**

```csharp
public sealed record GetTeamsResult(
    Guid Id,
    string Name,
    Guid? ParentTeamId,
    bool IsMember,
    TeamRole? Role,
    bool CanManage);
```

- [ ] **Step 1: Add failing repository tests**

Create hierarchy:

```text
Stellantis (not direct member)
└── Finance (not direct member)
    ├── Reporting (direct Member)
    └── Budgeting (not member)
```

Expected result contains `Stellantis`, `Finance`, `Reporting`; excludes `Budgeting`. Structural ancestors have `IsMember=false`, `Role=null`, `CanManage=false`; Reporting has direct membership metadata.

Add another case where a direct `Admin`/`Owner` returns `CanManage=true`.

- [ ] **Step 2: Verify RED**

```powershell
dotnet test tests/CloudKnowledge.Infrastructure.Tests/CloudKnowledge.Infrastructure.Tests.csproj --configuration Release
```

- [ ] **Step 3: Implement hierarchy projection**

`EfTeamRepository.GetForUserAsync` must return direct memberships plus all required ancestors. Prefer a single recursive PostgreSQL query/CTE or one bounded data retrieval followed by in-memory ancestor closure; do not perform one query per ancestor. The resulting set must be distinct by team id and ordered deterministically by name/id.

Direct membership is the only source of role/can-manage:

```csharp
canManage = role is TeamRole.Admin or TeamRole.Owner;
```

Structural ancestors never receive an inferred role.

- [ ] **Step 4: Update API response**

Standardize `TeamResponse` as:

```csharp
public sealed record TeamResponse(
    Guid Id,
    string Name,
    Guid? ParentTeamId,
    bool IsMember,
    string? Role,
    bool CanManage);
```

Both GET and POST team responses use this contract.

- [ ] **Step 5: Run backend tests and commit**

```powershell
dotnet test CloudKnowledge.slnx --configuration Release
git add src tests
git commit -m "feat: expose team hierarchy navigation"
```

---

### Task 4: Add permission-aware document scopes, search, and access provenance

**Files:**
- Create: `src/CloudKnowledge.Application/Document/GetDocuments/DocumentListScope.cs`
- Create: `src/CloudKnowledge.Application/Document/GetDocuments/GetDocumentsQuery.cs`
- Modify: `src/CloudKnowledge.Application/Document/Access/IDocumentAccessRepository.cs`
- Modify: `src/CloudKnowledge.Application/Document/GetDocuments/GetDocumentsResult.cs`
- Modify: `src/CloudKnowledge.Application/Document/GetDocuments/GetDocumentsUseCase.cs`
- Modify: `src/CloudKnowledge.Infrastructure/Documents/EfDocumentAccessRepository.cs`
- Add infrastructure tests under `tests/CloudKnowledge.Infrastructure.Tests/Documents/`
- Add application tests under `tests/CloudKnowledge.Application.Tests/Documents/GetDocuments/`

**Interfaces:**

```csharp
public enum DocumentListScope
{
    All,
    Owned,
    Team
}

public sealed record GetDocumentsQuery(
    int Page,
    int PageSize,
    DocumentListScope Scope,
    Guid? TeamId,
    bool IncludeDescendants,
    string? SearchQuery);

public sealed record DocumentAccessTeamResult(Guid Id, string Name, string Path);
```

Repository list/count methods consume the same normalized filter so count and page cannot drift.

- [ ] **Step 1: Add failing repository tests**

Cover these database-backed cases:

```text
All: owned + explicitly shared documents only
Owned: owner documents only
Team/direct: selected direct team only
Team/descendants: descendants INTERSECT direct memberships
Structural parent with no membership: may aggregate accessible descendant teams
Unauthorized descendant: never returned
Filename query: case-insensitive and combined with authorization
One doc shared through multiple allowed teams: returned once
Pagination/count: calculated after filters
Access provenance: all visible team paths, no unauthorized paths
```

- [ ] **Step 2: Verify RED**

```powershell
dotnet test tests/CloudKnowledge.Infrastructure.Tests/CloudKnowledge.Infrastructure.Tests.csproj --configuration Release
```

- [ ] **Step 3: Normalize query in the use case**

`GetDocumentsUseCase.ExecuteAsync` changes from `(page, pageSize, cancellationToken)` to `(GetDocumentsQuery query, cancellationToken)`. Preserve the existing page validation and maximum page size. Trim `SearchQuery`; normalize blank text to null.

- [ ] **Step 4: Implement a single permission-aware base query**

In `EfDocumentAccessRepository`, build an `IQueryable<Document>` that applies authorization and scope before `CountAsync`, `Skip`, or `Take`.

Required semantics:

```text
All -> existing WhereAccessibleTo(userId)
Owned -> document.OwnerUserId == userId
Team/direct -> requested team must be a direct membership; docs shared with that team
Team/descendants -> descendant ids of requested node INTERSECT user's direct memberships; docs shared with resulting ids
```

A structural parent is selectable only when it belongs to the user's navigable hierarchy; arbitrary team ids must not be usable as an oracle or access expansion.

Use `Distinct()`/equivalent SQL semantics before paging so multi-team sharing does not duplicate document rows.

Filename search must use a translated case-insensitive PostgreSQL predicate such as `EF.Functions.ILike(document.FileName, pattern)` with safe parameterization.

- [ ] **Step 5: Return provenance without N+1**

For the page of document ids, fetch all visible direct-membership team shares plus team names/parents in bounded set queries, construct paths in memory from the fetched hierarchy map, then group by document id. Do not issue one query per document or per team.

`GetDocumentsItem` becomes:

```csharp
public sealed record GetDocumentsItem(
    Guid Id,
    string FileName,
    string ContentType,
    DocumentStatus Status,
    bool IsOwner,
    IReadOnlyList<DocumentAccessTeamResult> SharedTeams);
```

- [ ] **Step 6: Run focused and full backend tests, then commit**

```powershell
dotnet test tests/CloudKnowledge.Infrastructure.Tests/CloudKnowledge.Infrastructure.Tests.csproj --configuration Release
dotnet test tests/CloudKnowledge.Application.Tests/CloudKnowledge.Application.Tests.csproj --configuration Release
dotnet test CloudKnowledge.slnx --configuration Release
git add src tests
git commit -m "feat: filter permission-aware document library"
```

---

### Task 5: Extend Documents API contract with deterministic validation

**Files:**
- Modify: `src/CloudKnowledge.Api/Contracts/Documents/GetDocumentsRequest.cs`
- Create: `src/CloudKnowledge.Api/Contracts/Documents/DocumentAccessTeamResponse.cs`
- Modify: `src/CloudKnowledge.Api/Contracts/Documents/DocumentResponse.cs`
- Modify: `src/CloudKnowledge.Api/Controllers/DocumentController.cs`
- Add/modify integration tests under `tests/CloudKnowledge.Api.IntegrationTests/Documents/`

**Interfaces:**

Request query parameters:

```text
page=1
pageSize=20
scope=all|owned|team
teamId=<guid?>
includeDescendants=<bool>
query=<string?>
```

- [ ] **Step 1: Add failing API validation tests**

Assert:

```text
no new params -> 200 and current All behavior
scope=team without teamId -> 400
scope=all with teamId -> 400
scope=owned with teamId -> 400
scope!=team with includeDescendants=true -> 400
unknown scope -> 400
team scope for arbitrary unauthorized id -> no data disclosure (404/400 according to final controller mapping)
```

- [ ] **Step 2: Verify RED**

```powershell
dotnet test tests/CloudKnowledge.Api.IntegrationTests/CloudKnowledge.Api.IntegrationTests.csproj --configuration Release
```

- [ ] **Step 3: Implement request parsing/validation**

Keep the HTTP contract strings lowercase. Map `all`, `owned`, `team` explicitly to `DocumentListScope`; do not rely on permissive enum binding.

Construct one `GetDocumentsQuery` and call the application use case. Invalid combinations return `BadRequest` with a concise `message`.

- [ ] **Step 4: Extend response metadata**

```csharp
public sealed record DocumentAccessTeamResponse(
    Guid Id,
    string Name,
    string Path);
```

`DocumentResponse` includes `IReadOnlyList<DocumentAccessTeamResponse> SharedTeams`. Creation/get-by-id paths may return an empty list until their provenance is explicitly requested; list responses must populate it.

- [ ] **Step 5: Run backend suite and commit**

```powershell
dotnet test CloudKnowledge.slnx --configuration Release
git add src tests
git commit -m "feat: expose document library filters"
```

---

### Task 6: Add reusable Angular team-tree and filtered document clients

**Files:**
- Modify: `src/CloudKnowledge.Web/src/app/features/teams/teams.ts`
- Create: `src/CloudKnowledge.Web/src/app/features/teams/team-tree.ts`
- Create: `src/CloudKnowledge.Web/src/app/features/teams/team-tree.spec.ts`
- Modify: `src/CloudKnowledge.Web/src/app/features/documents/documents.ts`
- Modify/add tests under `src/CloudKnowledge.Web/src/app/features/documents/`

**Interfaces:**

```ts
export interface TeamItem {
  id: string;
  name: string;
  parentTeamId: string | null;
  isMember: boolean;
  role: string | null;
  canManage: boolean;
}

export interface TeamTreeNode extends TeamItem {
  children: TeamTreeNode[];
}

export type DocumentScope =
  | { kind: 'all' }
  | { kind: 'owned' }
  | { kind: 'team'; teamId: string; includeDescendants: boolean };
```

- [ ] **Step 1: Write failing tree-helper tests**

Test flat nodes -> roots/children/grandchildren, deterministic alphabetical sorting, and structural ancestors with `isMember=false` preserved rather than discarded.

- [ ] **Step 2: Verify frontend RED**

```powershell
cd src\CloudKnowledge.Web
npm test -- --watch=false
```

- [ ] **Step 3: Implement `buildTeamTree`**

Build a map by id, attach nodes to known parents, return roots, and sort each level by `name.localeCompare`. Treat a missing parent defensively as a root so malformed API data does not crash the page.

- [ ] **Step 4: Update Teams client**

`createTeam` becomes:

```ts
createTeam(name: string, parentTeamId: string | null = null)
```

and POSTs `{ name, parentTeamId }`.

- [ ] **Step 5: Update Documents client**

Use `HttpParams` instead of string concatenation. New method concept:

```ts
getDocuments(
  page: number,
  pageSize: number,
  scope: DocumentScope,
  query?: string
): Observable<DocumentsPageResponse>
```

Map scope to `scope`, `teamId`, and `includeDescendants`. Extend `DocumentItem` with:

```ts
sharedTeams: { id: string; name: string; path: string }[];
```

- [ ] **Step 6: Run frontend tests/build and commit**

```powershell
npm test -- --watch=false
npm run build
git add src/CloudKnowledge.Web
git commit -m "feat: add team tree and document filter clients"
```

---

### Task 7: Turn Documents into a scalable hierarchical library UI

**Files:**
- Modify: `src/CloudKnowledge.Web/src/app/features/documents/documents-page/documents-page.ts`
- Modify: `src/CloudKnowledge.Web/src/app/features/documents/documents-page/documents-page.html`
- Modify: `src/CloudKnowledge.Web/src/app/features/documents/documents-page/documents-page.scss`
- Add/update frontend tests for the page

**Interfaces:**
- Page owns `selectedScope`, `searchQuery`, `teamTree`, expanded-node state, and current page.
- Scope/search changes always reset current page to 1 and reload from server.

- [ ] **Step 1: Add failing component tests**

Assert user actions produce these requests:

```text
All documents -> scope=all
My documents -> scope=owned
member team -> scope=team + selected id + includeDescendants=false
structural ancestor -> scope=team + selected id + includeDescendants=true
search change -> page reset to 1
team change -> page reset to 1
```

Also assert `Mine` and one/multiple hierarchy path chips render from document metadata.

- [ ] **Step 2: Verify RED**

```powershell
cd src\CloudKnowledge.Web
npm test -- --watch=false
```

- [ ] **Step 3: Implement library navigation**

Desktop structure:

```text
sidebar: All documents / My documents / hierarchical Teams
content: filename search / current scope title / document list / pagination
```

Structural-only nodes remain selectable as branch aggregators but receive a visual treatment that does not imply membership. Expand/collapse controls must not accidentally select the node.

- [ ] **Step 4: Implement search behavior**

Use a small RxJS debounce for text input (for example 250-350ms using existing RxJS only). Trim text before requests. Do not preload all documents for client-side search.

- [ ] **Step 5: Render provenance compactly**

Show status plus `Mine` when owned; show hierarchy-path chips from `sharedTeams`. Keep existing document actions and disable owner-only actions for shared documents as before.

- [ ] **Step 6: Responsive styling**

At narrow widths collapse the sidebar into a compact scope/team selector or stacked panel without losing hierarchy or selected state. Reuse existing visual tokens; do not add a UI package.

- [ ] **Step 7: Run frontend tests/build and commit**

```powershell
npm test -- --watch=false
npm run build
git add src/CloudKnowledge.Web
git commit -m "feat: add hierarchical document library UI"
```

---

### Task 8: Add hierarchical team administration UI and complete verification

**Files:**
- Modify: `src/CloudKnowledge.Web/src/app/features/teams/teams-page/teams-page.ts`
- Modify: `src/CloudKnowledge.Web/src/app/features/teams/teams-page/teams-page.html`
- Modify: `src/CloudKnowledge.Web/src/app/features/teams/teams-page/teams-page.scss`
- Add frontend page tests
- Update `README.md` only if current feature documentation lists flat teams/document listing behavior

**Interfaces:**
- Root creation passes `parentTeamId=null`.
- Child creation is shown only for selected nodes with `canManage=true`.
- Existing add-member behavior remains limited to manageable/member teams according to current API permissions.

- [ ] **Step 1: Add failing UI tests**

Cover:

```text
flat nodes render as hierarchy
Create root team always available to authenticated page user
Create sub-team visible for canManage=true
Create sub-team hidden/disabled for canManage=false
child creation sends selected team id as parentTeamId
structural-only ancestors do not expose member-management actions
```

- [ ] **Step 2: Verify RED**

```powershell
cd src\CloudKnowledge.Web
npm test -- --watch=false
```

- [ ] **Step 3: Implement tree-oriented team management**

Use the shared `buildTeamTree` helper. Allow selecting nodes, displaying role/member/structural status, creating root teams, and creating a child under a manageable parent. Refresh the tree after successful creation.

- [ ] **Step 4: Run complete local verification**

From repository root:

```powershell
dotnet build CloudKnowledge.slnx --configuration Release
dotnet test CloudKnowledge.slnx --configuration Release --no-build
cd src\CloudKnowledge.Web
npm ci
npm test -- --watch=false
npm run build
cd ..\..\..
docker build -f src/CloudKnowledge.Api/Dockerfile -t cloudknowledge-api:verify .
docker build -f src/CloudKnowledge.Worker/Dockerfile -t cloudknowledge-worker:verify .
docker build -f src/CloudKnowledge.Web/Dockerfile -t cloudknowledge-web:verify .
```

Expected: all commands exit 0.

- [ ] **Step 5: Apply migration to the local development database and smoke-test**

```powershell
dotnet ef database update `
  --project src\CloudKnowledge.Infrastructure `
  --startup-project src\CloudKnowledge.Api
```

Manual smoke scenario:

```text
create Rai root
create Rai / DeskSharing child
create Stellantis root
create Stellantis / Finance / Reporting hierarchy
verify no membership inheritance
share different documents to leaf teams
verify All, My, direct-team, and parent aggregation
verify filename search
verify an unauthorized sibling is never returned
```

- [ ] **Step 6: Push and verify GitHub Actions**

Push the feature branch and confirm the PR has:

```text
backend    success
frontend   success
containers success
```

Do not merge while any check is pending or failed.

- [ ] **Step 7: Final commit if documentation changed**

```powershell
git add README.md src tests
git commit -m "docs: describe hierarchical document library"
```

Skip this commit if README required no change.

## Final Review Checklist

Before declaring the feature complete, verify directly against the spec:

- [ ] Parent/child hierarchy supports arbitrary depth.
- [ ] Membership/roles do not inherit.
- [ ] Direct memberships plus structural ancestors are returned for navigation.
- [ ] Structural ancestors never become an authorization predicate.
- [ ] All/My/Team scopes and filename search are server-side.
- [ ] Parent aggregation intersects descendants with explicit direct memberships.
- [ ] Pagination/count happen after authorization/filtering.
- [ ] Duplicate shares do not duplicate documents.
- [ ] Visible document access paths are returned without leaking unauthorized team paths.
- [ ] Documents and Teams UIs represent hierarchy clearly.
- [ ] Existing document actions and default list behavior regressions are covered.
- [ ] Migration preserves existing teams/shares.
- [ ] Full .NET tests, Angular tests/build, container builds, and GitHub Actions are green.
