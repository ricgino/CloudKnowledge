# Team Document Ownership and Deletion Design

## Goal

Introduce real team-owned documents and safe team deletion semantics so that:

- a personal upload is owned by a user;
- an upload performed with a team selected is owned by that team;
- sharing a user-owned document with a team never transfers ownership;
- deleting a team deletes only documents owned by that team;
- user-owned documents shared with the deleted team survive and only lose that team share.

## Core Ownership Model

A document has two nullable ownership columns:

- `OwnerUserId`
- `OwnerTeamId`

Application-created documents must have exactly one logical owner:

```text
Personal document:
OwnerUserId = user
OwnerTeamId = null

Team document:
OwnerUserId = null
OwnerTeamId = team

User-owned document shared to team:
OwnerUserId = user
OwnerTeamId = null
+ DocumentTeamAccess(document, team)
```

`DocumentTeamAccess` remains a sharing/access relationship only. It never represents ownership.

Existing documents keep their current `OwnerUserId`; the migration only adds nullable `owner_team_id` and does not reinterpret existing ownership.

## Upload Semantics

### Personal upload

`POST /api/documents` without `TeamId`:

- current user becomes `OwnerUserId`;
- `OwnerTeamId` is null;
- no team share is created automatically.

### Team upload

`POST /api/documents` with `TeamId`:

- current user must be a direct member of the selected team;
- the selected team becomes `OwnerTeamId`;
- `OwnerUserId` is null;
- the document is accessible through team membership because it is team-owned;
- no redundant `DocumentTeamAccess` row is required for the owner team.

A team-owned document may later be shared with another team through the existing sharing model. Ownership remains with the original owner team.

## Authorization and Retrieval

Existing permission-aware retrieval must be extended so a user can access a document when any of these conditions is true:

1. `OwnerUserId == currentUserId`;
2. `OwnerTeamId` belongs to one of the current user's direct team memberships;
3. the document has a `DocumentTeamAccess` row for one of the current user's direct team memberships.

Hierarchy remains organizational only. Parent membership does not grant access to a child-owned document and structural ancestors do not grant document access.

Document list scopes keep the same meaning:

- `all`: every document the current user can access by ownership or sharing;
- `owned`: documents owned by the current user only;
- `team`: documents owned by the selected authorized team plus documents shared to that team; with descendants enabled, aggregate only descendant teams for which the user has direct membership.

Provenance should distinguish ownership from sharing where useful in the UI, but authorization is always enforced server-side.

## Team Deletion Rules

Endpoint:

```text
DELETE /api/teams/{teamId}
```

Authorization and validation:

- team not visible/directly accessible to caller -> `404`;
- caller is not an `Owner` of that exact team -> `403`;
- team has one or more direct or indirect child teams -> `409 Conflict`;
- otherwise deletion proceeds.

Only a leaf team can be deleted. There is no recursive/cascade deletion of subteams in this iteration.

## Team Deletion Data Lifecycle

When a leaf team is deleted:

### Team-owned documents

Every document where `OwnerTeamId == deletedTeamId` is deleted completely.

This includes, through existing document cascades/lifecycle:

- document database row;
- chunks;
- chunk embeddings;
- document-team shares, including shares to other teams;
- related document access rows;
- blob content in document storage.

Ownership is authoritative: if a document is owned by the deleted team, it is deleted even if that document had also been shared with another team.

### User-owned documents shared to the deleted team

Every document where `OwnerUserId` is set remains intact.

Only `DocumentTeamAccess` rows pointing to the deleted team are removed by cascade. The document, blob, chunks, embeddings, owner and any shares to other teams remain.

### Memberships

`TeamMember` rows for the deleted team are removed by cascade.

## Persistence Changes

`documents` gains:

```text
owner_team_id uuid NULL
```

with:

- FK to `teams(id)`;
- `ON DELETE CASCADE` so team-owned documents are deleted with the owner team;
- index on `owner_team_id`.

The existing `owner_user_id` FK remains `RESTRICT`.

No database check constraint is added in this migration because legacy rows created before ownership existed may legitimately still contain no owner. The domain/application layer enforces mutually exclusive ownership for all newly created documents.

## Domain Invariants

`Document` gains `OwnerTeamId` and explicit assignment behavior:

- assigning a user owner when a team owner already exists is invalid;
- assigning a team owner when a user owner already exists is invalid;
- assigning the same owner twice is idempotent;
- empty GUID owners are rejected.

Existing `AssignOwner(Guid userId)` may remain as a compatibility wrapper for user ownership if useful, but new code should make ownership type explicit.

## Team Deletion Application Boundary

A dedicated `DeleteTeamUseCase` coordinates:

1. current user resolution;
2. exact team lookup;
3. exact membership/role lookup;
4. child existence check;
5. discovery of IDs of team-owned documents;
6. database deletion of the leaf team in one persistence operation/transaction;
7. blob cleanup for the deleted team-owned document IDs.

The use case returns a typed status such as:

- `Deleted`
- `NotFound`
- `Forbidden`
- `HasChildren`

The API maps these to `204`, `404`, `403`, and `409` respectively.

Blob deletion follows the same consistency model already used by `DeleteDocumentUseCase`: database deletion is authoritative, then blob cleanup is attempted. Blob deletion must be idempotent so retrying cleanup is safe.

## UI Changes

### Upload

The Documents upload form keeps the existing team selector, but its meaning becomes explicit:

- no team selected: `Personal document`;
- team selected: `Owned by <team path>`.

The UI text must not describe a team upload as merely "shared" with that team.

### Team administration

A delete action is shown only for teams where the current user is a direct `Owner`.

Before calling the API, the UI asks for confirmation and explains:

- team-owned documents will be permanently deleted;
- user-owned documents merely shared with this team will remain;
- subteams prevent deletion.

After successful deletion, the team tree and document views are refreshed.

## Testing Requirements

### Domain

- user ownership is mutually exclusive with team ownership;
- team ownership is mutually exclusive with user ownership;
- idempotent same-owner assignment;
- empty owner IDs rejected.

### Application

- personal upload assigns user owner;
- team upload assigns team owner and no user owner;
- team upload requires direct membership;
- Owner can delete leaf team;
- Admin/Member cannot delete team;
- team with children returns `HasChildren`;
- deletion returns IDs of team-owned documents for blob cleanup.

### Infrastructure / PostgreSQL

- `owner_team_id` persists;
- permission-aware queries include team-owned documents only for direct members;
- deleting leaf team removes team-owned docs/chunks/embeddings/shares/memberships;
- deleting leaf team preserves user-owned docs and shares to other teams;
- user-owned share to deleted team is removed;
- FK prevents accidental deletion of a parent with children.

### API integration

- team upload creates a team-owned document;
- personal upload still creates a user-owned document;
- DELETE team maps statuses correctly;
- unauthorized delete is forbidden/not found as designed;
- delete with children returns `409`;
- deleted team's owned blobs are removed;
- user-owned shared document remains downloadable by its owner after team deletion.

### Frontend

- upload helper/UI communicates personal vs team ownership correctly;
- delete button only appears for direct Owner teams;
- delete confirmation text reflects destructive semantics;
- successful delete refreshes tree/document state.

## Out of Scope

- recursive deletion of team hierarchies;
- moving/reparenting teams;
- ownership transfer between users and teams;
- changing ownership after upload;
- organization-global administrators;
- soft-delete/restore bin;
- automatic permission inheritance;
- bulk team deletion.

## Acceptance Criteria

1. Upload without a team creates a user-owned document.
2. Upload with a team creates a team-owned document, not a user-owned document merely shared to that team.
3. Direct team members can retrieve team-owned documents; structural ancestors cannot widen access.
4. Only a direct Team Owner can delete that team.
5. A team with children cannot be deleted.
6. Deleting a leaf team permanently deletes its team-owned documents and stored blobs.
7. Deleting a leaf team preserves user-owned documents shared to it.
8. Shares from preserved user-owned documents to the deleted team disappear while other shares remain.
9. Existing user-owned documents continue to behave as before.
10. Backend tests, frontend tests/build, and all container builds pass before the feature is considered complete.
