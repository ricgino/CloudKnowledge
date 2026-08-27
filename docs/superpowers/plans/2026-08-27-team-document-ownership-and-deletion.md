# Team Document Ownership and Deletion Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add real team-owned documents and safe leaf-team deletion while preserving user-owned shared documents.

**Architecture:** Add nullable team ownership to `Document`, keep ownership separate from `DocumentTeamAccess`, extend all permission-aware queries to direct team ownership, implement owner-authorized leaf-team deletion with database cascades plus idempotent blob cleanup, preserve realtime notification semantics for team-owned documents, then expose the new ownership/delete behavior in Angular.

**Tech Stack:** .NET 10, ASP.NET Core, EF Core 10, PostgreSQL/pgvector, Azure Blob/Azurite, Azure Service Bus emulator, xUnit/Testcontainers, Angular/TypeScript, GitHub Actions.

**Spec:** `docs/superpowers/specs/2026-08-27-team-document-ownership-and-deletion-design.md`

## Global Constraints

- Every newly-created document has exactly one logical owner: user OR team.
- Existing legacy ownerless/user-owned documents are not rewritten.
- The owner team is not duplicated in `DocumentTeamAccess`.
- Only direct team membership grants team-owned document access; hierarchy never grants authorization.
- `scope=owned` remains personal user ownership only.
- Only a direct Team `Owner` may delete a team.
- Team deletion is blocked while any child team exists.
- Deleting a team deletes its team-owned documents and blobs but preserves user-owned documents merely shared to it.
- No recursive team deletion, reparenting, ownership transfer, soft-delete, or inherited permissions.
- Existing PDF/DOCX/TXT processing and permission-aware RAG must keep working.
- Realtime document-ready notifications must support team-owned documents.
- Do not merge until backend tests/build, frontend tests/build, and all container builds are green.

---

### Task 1: Model and persist team document ownership

**Files:**
- Modify: `src/CloudKnowledge.Domain/Documents/Document.cs`
- Modify: `src/CloudKnowledge.Infrastructure/Persistence/Configurations/DocumentConfiguration.cs`
- Generate: `src/CloudKnowledge.Infrastructure/Persistence/Migrations/*_AddTeamDocumentOwnership.cs`
- Generate: matching migration designer and model snapshot
- Modify: `tests/CloudKnowledge.Domain.Tests/Documents/DocumentOwnershipTests.cs`
- Add/modify persistence coverage under `tests/CloudKnowledge.Infrastructure.Tests/Documents/`

**Interfaces:**

```csharp
public Guid? OwnerTeamId { get; private set; }
public void AssignUserOwner(Guid ownerUserId);
public void AssignTeamOwner(Guid ownerTeamId);
public void AssignOwner(Guid ownerUserId); // compatibility wrapper
```

- [ ] **Step 1: Write failing domain tests**

Add cases proving team assignment, same-team idempotence, empty-team rejection, user-after-team rejection, and team-after-user rejection.

- [ ] **Step 2: Verify RED**

```powershell
dotnet test tests/CloudKnowledge.Domain.Tests/CloudKnowledge.Domain.Tests.csproj --configuration Release
```

Expected: compile failures for `OwnerTeamId` / `AssignTeamOwner`.

- [ ] **Step 3: Implement the ownership invariant**

`AssignUserOwner` and `AssignTeamOwner` reject empty ids, are idempotent only for the same existing owner, and reject switching/adding a second owner. Keep `AssignOwner` delegating to `AssignUserOwner` for existing callers.

- [ ] **Step 4: Map the new FK**

Add nullable `owner_team_id`, an index, and FK `documents.owner_team_id -> teams.id` with `DeleteBehavior.Cascade`. Keep `owner_user_id` restrictive.

- [ ] **Step 5: Generate the migration with EF tooling**

```powershell
dotnet tool restore
dotnet ef migrations add AddTeamDocumentOwnership `
  --project src\CloudKnowledge.Infrastructure `
  --startup-project src\CloudKnowledge.Api
```

Inspect that existing rows are untouched and the migration only adds the nullable column/index/FK.

- [ ] **Step 6: Run domain/infrastructure tests and commit**

```powershell
dotnet test tests/CloudKnowledge.Domain.Tests/CloudKnowledge.Domain.Tests.csproj --configuration Release
dotnet test tests/CloudKnowledge.Infrastructure.Tests/CloudKnowledge.Infrastructure.Tests.csproj --configuration Release
```

---

### Task 2: Make team-selected uploads team-owned

**Files:**
- Modify: `src/CloudKnowledge.Application/Document/CreateDocument/CreateDocumentUseCase.cs`
- Modify: `src/CloudKnowledge.Api/Controllers/DocumentController.cs`
- Modify: `tests/CloudKnowledge.Application.Tests/Documents/CreateDocument/CreateDocumentUseCaseTests.cs`
- Add/modify API integration tests under `tests/CloudKnowledge.Api.IntegrationTests/Documents/`

**Interfaces:**

```csharp
Task<CreateDocumentResult> ExecuteAsync(
    string fileName,
    string contentType,
    Stream content,
    Guid? ownerTeamId,
    CancellationToken cancellationToken);
```

The existing overload without `ownerTeamId` remains and creates a personal document.

- [ ] **Step 1: Write failing application tests**

Assert personal upload sets `OwnerUserId` only; team upload sets `OwnerTeamId` only. Keep direct membership validation at the API/application boundary.

- [ ] **Step 2: Verify RED**

```powershell
dotnet test tests/CloudKnowledge.Application.Tests/CloudKnowledge.Application.Tests.csproj --configuration Release
```

- [ ] **Step 3: Implement team ownership**

When `ownerTeamId` exists call `AssignTeamOwner`; otherwise resolve current user and call `AssignUserOwner`. Storage, DB insert and queue publishing remain unchanged.

- [ ] **Step 4: Change POST `/api/documents`**

Keep direct-team membership validation, pass `TeamId` into `CreateDocumentUseCase`, and remove the automatic call to `ShareDocumentWithTeamUseCase`. For a team upload response `IsOwner` is false for the uploading user because the team is the owner.

- [ ] **Step 5: Add API integration assertions**

Assert team upload stores `OwnerTeamId`, leaves `OwnerUserId` null, and creates no `DocumentTeamAccess` row for the owner team. Assert personal upload remains user-owned.

---

### Task 3: Extend permission-aware retrieval and realtime notifications

**Files:**
- Modify: `src/CloudKnowledge.Infrastructure/Documents/DocumentAccessQueryExtensions.cs`
- Modify: `src/CloudKnowledge.Infrastructure/Documents/EfDocumentAccessRepository.cs`
- Modify: `src/CloudKnowledge.Infrastructure/Notifications/EfDocumentReadyNotificationQuery.cs`
- Modify: `src/CloudKnowledge.Application/Notifications/DocumentReady/DocumentReadyNotificationAudience.cs`
- Modify: `src/CloudKnowledge.Application/Notifications/DocumentReady/CreateDocumentReadyNotificationsUseCase.cs`
- Modify: `tests/CloudKnowledge.Infrastructure.Tests/Documents/DocumentLibraryFiltersTests.cs`
- Modify/add notification tests

**Interfaces:**

```csharp
public sealed record DocumentReadyNotificationAudience(
    string FileName,
    Guid? OwnerUserId,
    string OwnerDisplayName,
    IReadOnlyList<Guid> RecipientUserIds);
```

- [ ] **Step 1: Add failing database-backed access tests**

Add a DeskSharing-owned document and a Booking-owned document. A user directly in DeskSharing but not Booking must see the former in `all`, direct team and authorized parent-descendant scope, and never see the latter. Provenance for the team-owned document must include the owner-team path.

- [ ] **Step 2: Extend the SQL authorization predicate**

A document is accessible when the current user owns it, directly belongs to its owner team, or directly belongs to an explicitly shared team.

- [ ] **Step 3: Extend team scope and provenance**

Team filters include `OwnerTeamId` for allowed team ids OR explicit shares. `GetVisibleTeamAccessAsync` unions owner-team provenance with explicit-share provenance without exposing inaccessible paths or duplicates.

- [ ] **Step 4: Preserve document-ready notifications**

For a user-owned document, retain current behavior. For a team-owned document, use the owner team as source label and send to direct owner-team members plus explicit-share-team members, deduplicated. Exclude the user owner only when `OwnerUserId` has a value.

- [ ] **Step 5: Run application/infrastructure tests**

```powershell
dotnet test tests/CloudKnowledge.Application.Tests/CloudKnowledge.Application.Tests.csproj --configuration Release
dotnet test tests/CloudKnowledge.Infrastructure.Tests/CloudKnowledge.Infrastructure.Tests.csproj --configuration Release
```

---

### Task 4: Implement safe leaf-team deletion and blob cleanup

**Files:**
- Create: `src/CloudKnowledge.Application/Teams/DeleteTeam/DeleteTeamStatus.cs`
- Create: `src/CloudKnowledge.Application/Teams/DeleteTeam/DeleteTeamPersistenceResult.cs`
- Create: `src/CloudKnowledge.Application/Teams/DeleteTeam/ITeamDeletionRepository.cs`
- Create: `src/CloudKnowledge.Application/Teams/DeleteTeam/DeleteTeamUseCase.cs`
- Create: `src/CloudKnowledge.Infrastructure/Teams/EfTeamDeletionRepository.cs`
- Modify: `src/CloudKnowledge.Api/Controllers/TeamsController.cs`
- Modify: `src/CloudKnowledge.Api/Program.cs`
- Add application, PostgreSQL, and API integration tests

**Interfaces:**

```csharp
public enum DeleteTeamStatus { Deleted = 1, NotFound = 2, Forbidden = 3, HasChildren = 4 }
public enum DeleteTeamPersistenceStatus { Deleted = 1, NotFound = 2, HasChildren = 3 }
public sealed record DeleteTeamPersistenceResult(
    DeleteTeamPersistenceStatus Status,
    IReadOnlyList<Guid> OwnedDocumentIds);
```

- [ ] **Step 1: Write failing application tests**

Cover Owner leaf success, Admin/Member forbidden, missing membership/team not found, child-team conflict, and blob cleanup of all returned team-owned document ids.

- [ ] **Step 2: Implement the use case**

Resolve current user; exact team; exact membership; require `Owner`; call deletion repository; on successful DB delete call idempotent `IDocumentDeletionStorage.DeleteAsync` for each returned id.

- [ ] **Step 3: Write PostgreSQL deletion tests**

Verify team-owned documents/chunks/embeddings/shares/memberships disappear; user-owned documents shared to deleted team survive; their deleted-team share disappears; other shares remain; parent with child is rejected.

- [ ] **Step 4: Implement `EfTeamDeletionRepository`**

Use a DB transaction: tracked leaf lookup, child check, capture `OwnerTeamId` document ids, remove team, save, commit. Database cascades perform relational cleanup.

- [ ] **Step 5: Add API endpoint**

`DELETE /api/teams/{teamId}` maps `Deleted -> 204`, `NotFound -> 404`, `Forbidden -> 403`, `HasChildren -> 409` with an explanatory message.

- [ ] **Step 6: Add API integration coverage**

Exercise forbidden/not-found/child conflict and successful leaf deletion. Verify user-owned shared content remains accessible to its owner after deletion.

---

### Task 5: Expose ownership semantics and safe team delete in Angular

**Files:**
- Modify: `src/CloudKnowledge.Web/src/app/features/documents/documents-page/documents-page.ts`
- Modify: `src/CloudKnowledge.Web/src/app/features/documents/documents-page/documents-page.html`
- Modify: `src/CloudKnowledge.Web/src/app/features/teams/teams.ts`
- Modify: `src/CloudKnowledge.Web/src/app/features/teams/teams.spec.ts`
- Modify: `src/CloudKnowledge.Web/src/app/features/administration/administration-page/administration-page.ts`
- Modify: `src/CloudKnowledge.Web/src/app/features/administration/administration-page/administration-page.html`
- Modify SCSS only as required by the new action layout

- [ ] **Step 1: Write failing frontend tests**

Add `canDeleteTeam(team)` coverage: only direct `Owner` is true; Admin, Member and structural ancestor are false.

- [ ] **Step 2: Update upload wording**

Use `Personal document` when no team is selected and `Owned by <team>` when a team is selected. Success copy must not say a team upload was merely shared.

- [ ] **Step 3: Add team delete client/UI**

Add `Teams.deleteTeam`. Show Delete only for direct Owner. Confirmation must say team-owned docs are permanent deletions, user-owned shared docs survive, and subteams block deletion. Handle HTTP 409 with a friendly message and refresh data after success.

- [ ] **Step 4: Run Angular tests/build**

```powershell
cd src\CloudKnowledge.Web
npm ci
npm test -- --watch=false
npm run build
```

---

### Task 6: Full verification and manual E2E gate

- [ ] **Step 1: Run the complete backend suite**

```powershell
cd E:\Dev\CloudKnowledge
dotnet restore CloudKnowledge.slnx
dotnet build CloudKnowledge.slnx --configuration Release
dotnet test CloudKnowledge.slnx --configuration Release --no-build
```

- [ ] **Step 2: Verify CI containers**

GitHub Actions must build API, Worker and Web images successfully.

- [ ] **Step 3: Manual E2E acceptance**

Create a leaf team, upload one team-owned PDF/DOCX/TXT, share a separate user-owned document to it, verify a direct member can retrieve the team-owned content, then delete the team as Owner. Confirm the team-owned document disappears, the user-owned shared document remains for its owner, and deleting a parent with a child returns conflict.

- [ ] **Step 4: Keep PR draft until all evidence is green**

Only after full CI and manual acceptance should PR #5 be marked ready for merge.
