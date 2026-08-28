# Document Library Controls Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add direct page selection, authorize team Owners to delete directly team-owned documents, and fix overflowing Administration form controls.

**Architecture:** Keep server-side pagination unchanged and add a bounded pagination model in the Angular Documents page. Extend document list metadata with `CanDelete` while preserving `IsOwner` semantics; enforce deletion in the EF repository using personal ownership OR direct Owner membership of `OwnerTeamId`. Fix Administration containment through reusable CSS sizing rules.

**Tech Stack:** Angular 22 + Vitest, ASP.NET Core/.NET 10, EF Core + PostgreSQL, xUnit, GitHub Actions.

**Spec:** `docs/superpowers/specs/2026-08-28-document-library-controls-design.md`

## Global Constraints

- Hierarchy grants no inherited authorization.
- Team-owned delete requires direct `TeamRole.Owner`; Admin and Member are denied.
- User-owned documents shared with a team remain deletable only by their user owner.
- `IsOwner` continues to mean personal `OwnerUserId`; use separate `CanDelete` for UI authorization.
- Existing page size and server-side filtering remain unchanged.

---

### Task 1: Numeric document pagination

**Files:**
- Modify: `src/CloudKnowledge.Web/src/app/features/documents/documents-page/documents-page.ts`
- Modify: `src/CloudKnowledge.Web/src/app/features/documents/documents-page/documents-page.html`
- Modify: `src/CloudKnowledge.Web/src/app/features/documents/documents-page/documents-page.scss`
- Test: `src/CloudKnowledge.Web/src/app/features/documents/documents-page/documents-page.spec.ts`

**Interfaces:**
- Produces: `paginationItems` bounded page/ellipsis view model and `goToPage(page: number)`.

- [ ] Write tests for small page sets, large page sets around the active page, and direct navigation.
- [ ] Run frontend tests and confirm RED because pagination helpers/navigation are missing.
- [ ] Implement the bounded page window and numeric buttons while keeping Previous/Next.
- [ ] Run frontend tests/build and confirm GREEN.
- [ ] Commit.

### Task 2: Team-owner document deletion authorization

**Files:**
- Modify: `src/CloudKnowledge.Application/Document/DeleteDocument/DeleteDocumentUseCase.cs`
- Modify: `src/CloudKnowledge.Infrastructure/Documents/EfDocumentDeletionRepository.cs`
- Modify: `src/CloudKnowledge.Application/Document/GetDocuments/GetDocumentsResult.cs`
- Modify: `src/CloudKnowledge.Application/Document/GetDocuments/GetDocumentsUseCase.cs`
- Modify: `src/CloudKnowledge.Api/Contracts/Documents/DocumentResponse.cs`
- Modify: `src/CloudKnowledge.Api/Controllers/DocumentController.cs`
- Modify: `src/CloudKnowledge.Web/src/app/features/documents/documents.ts`
- Modify: `src/CloudKnowledge.Web/src/app/features/documents/documents-page/documents-page.ts`
- Modify: `src/CloudKnowledge.Web/src/app/features/documents/documents-page/documents-page.html`
- Test: `tests/CloudKnowledge.Application.Tests/Documents/DeleteDocument/DeleteDocumentUseCaseTests.cs`
- Test: `tests/CloudKnowledge.Infrastructure.Tests/Documents/DocumentDeletionAuthorizationTests.cs`
- Test: existing GetDocuments/API/frontend tests as needed.

**Interfaces:**
- `IDocumentDeletionRepository.DeleteAuthorizedAsync(Guid userId, Guid documentId, CancellationToken)` deletes when personal owner OR direct Owner of the owning team.
- `GetDocumentsItem.CanDelete` / API `DocumentResponse.CanDelete` / Angular `DocumentItem.canDelete` expose the backend-derived capability.

- [ ] Write failing application/infrastructure tests covering personal owner, team Owner, team Admin, team Member, unrelated user, and user-owned-but-shared document.
- [ ] Run backend tests and confirm RED on missing authorization contract/behavior.
- [ ] Implement repository authorization and preserve blob deletion only after DB deletion succeeds.
- [ ] Add `CanDelete` to list metadata and map it through API/frontend without changing `IsOwner`.
- [ ] Update Delete button/guard to use `canDelete` and retain destructive confirmation.
- [ ] Run application, infrastructure, API and frontend tests/build; confirm GREEN.
- [ ] Commit.

### Task 3: Administration form containment

**Files:**
- Modify: `src/CloudKnowledge.Web/src/app/features/administration/administration-page/administration-page.scss`
- Test: `src/CloudKnowledge.Web/src/app/features/administration/administration-page/administration-page.spec.ts` if component tests exist; otherwise add a focused source-level style regression test next to the page.

**Interfaces:**
- Form grid labels/children can shrink (`min-width: 0`) and controls use `box-sizing: border-box; max-width: 100%`.

- [ ] Add a regression test asserting the containment rules exist on Administration form controls/grid children.
- [ ] Run frontend tests and confirm RED.
- [ ] Add the minimal reusable CSS sizing rules.
- [ ] Run frontend tests/build and confirm GREEN.
- [ ] Commit.

### Task 4: Full verification

- [ ] Run the complete GitHub CI for backend, frontend and containers.
- [ ] Confirm all jobs succeed and inspect any warnings/failures before claiming completion.
- [ ] Keep PR #5 draft until manual E2E verifies page navigation, team-owner delete, Admin/Member denial, and Administration layout in browser.
