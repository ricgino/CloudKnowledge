# Hierarchical Teams and Document Library Design

## Goal

Extend CloudKnowledge so teams can be organised as an arbitrary hierarchy while keeping authorization explicit, and turn the Documents area into a scalable library that can filter by ownership, team hierarchy, and filename without exposing unauthorized documents.

## Current State

CloudKnowledge currently models teams as flat entities with `Id`, `Name`, and `CreatedAtUtc`. Membership is explicit through `TeamMember`, with roles `Member`, `Admin`, and `Owner`. Documents can be owned by one user and shared with one or more teams. The current document list returns all documents accessible to the current user and only indicates whether each document is owned by the current user.

The current team API returns only teams to which the current user belongs directly. The current document list does not expose the team path or team relationship that grants access to a shared document.

## Core Design Decision

Team hierarchy is organizational only. It does not grant authorization by itself.

The system must preserve this invariant:

```text
Team hierarchy != membership inheritance
Team membership = explicit authorization
Document sharing = explicit access grant
```

Being a member of `Rai` must not automatically make the user a member of `Rai / DeskSharing`. Being a member of `Stellantis / Finance` must not automatically grant access to `Stellantis / Finance / Budgeting`.

This prevents a broad parent membership from silently granting access to future child teams.

## Team Hierarchy Model

`Team` gains a nullable self-reference:

```text
ParentTeamId: Guid?
```

A root team has `ParentTeamId = null`. A child team references another team as its parent.

Example:

```text
Rai
├── DeskSharing
├── Booking
└── HR Portal

Stellantis
├── Finance
│   ├── Reporting
│   └── Budgeting
└── Manufacturing
    ├── Plant A
    │   ├── QualityApp
    │   └── MaintenanceApp
    └── Plant B
```

The hierarchy supports arbitrary depth. No fixed levels such as Customer, Department, or Application are introduced.

### Persistence

The `teams` table gains:

```text
parent_team_id UUID NULL
```

with a foreign key to `teams.id`.

The relation uses a restrictive delete policy. Deleting or moving teams is outside this feature and must not be introduced implicitly.

The first implementation uses an adjacency-list model (`ParentTeamId`) rather than a closure table. This is sufficient for the expected scale and keeps writes and migrations simple. A closure table may be introduced later only if hierarchy traversal becomes a measured performance bottleneck.

## Hierarchy Validation

The domain/application layer must enforce:

- A team cannot be its own parent.
- A parent team must exist.
- Creation cannot introduce cycles.
- Root teams are created with `ParentTeamId = null`.
- Child teams may have any valid existing team as parent.

Because this feature only creates teams and does not move existing teams, cycle prevention is straightforward at creation time. A future move-team feature must revalidate the full ancestor chain.

## Team Creation and Management Rules

### Root team

Creating a root team keeps the current behavior:

- Any authenticated CloudKnowledge user may create a root team.
- The creator becomes `Owner` of that team.

### Child team

Creating a child team requires explicit management permission on the parent:

- The current user must be a direct `Admin` or `Owner` member of the parent team.
- The creator becomes `Owner` of the new child team.
- Membership is not copied from the parent.
- Parent owners/admins do not automatically become members of existing child teams unless explicitly added.

This keeps hierarchy and access independent.

## Team Visibility

The team tree returned to a user must include:

1. Teams for which the user is a direct member.
2. Structural ancestors required to display the path to those teams.

An ancestor returned only for structural navigation does not grant document access or management rights.

Each returned team node must expose enough information for the client to distinguish access from structure:

```text
id
name
parentTeamId
isMember
role              // nullable when isMember = false
canManage
```

`canManage` is true only when the current user is a direct `Admin` or `Owner` of that team.

The API must never treat `isMember = false` structural ancestors as authorized teams for document retrieval.

## Document Library Scopes

The Documents page becomes a library with these server-side scopes:

### All documents

Returns every document currently accessible to the user through either ownership or explicit team sharing.

### My documents

Returns only documents where:

```text
OwnerUserId == currentUserId
```

### Team

Returns documents shared with a selected team.

When `includeDescendants = false`, only documents explicitly shared with that team are returned.

When `includeDescendants = true`, the selected team acts as an organizational container. The query may include documents from descendants, but only for descendant teams where the current user is a direct member.

Example:

```text
Rai
├── DeskSharing      user is member
├── Booking          user is not member
└── HR Portal        user is member
```

Selecting `Rai` with descendants enabled may return documents from `DeskSharing` and `HR Portal`, but never from `Booking` unless the user also has another valid access path to the same document.

A user does not need to be a member of the selected structural parent in order to use it as a navigation container, provided the API returned that parent as an ancestor of one or more accessible descendant teams.

## Document Search

The Documents endpoint adds a server-side filename search.

The search:

- is optional;
- is case-insensitive;
- applies after authorization constraints are established in the query;
- combines with ownership/team scope;
- is paginated server-side.

No client-side filtering over a preloaded full document set is used.

## Document Access Metadata

Each document list item must explain why the current user can see it.

The API response must include:

```text
id
fileName
contentType
status
isOwner
sharedTeams[]
```

Each `sharedTeams` entry contains:

```text
id
name
path
```

`path` is a user-facing hierarchy path such as:

```text
Rai / DeskSharing
Stellantis / Finance / Budgeting
```

Only teams relevant to the current user's valid access should be returned in this access metadata. A document shared with several teams may therefore expose multiple visible team paths.

For an owned document, `isOwner = true`. The UI may show both `Mine` and team chips when the owner also shared the document with teams.

## API Design

### GET /api/teams

Returns the navigable hierarchy for the current user, including direct memberships plus structural ancestors.

Response nodes include:

```text
id
name
parentTeamId
isMember
role
canManage
```

The client builds the tree from these flat nodes.

### POST /api/teams

The request becomes:

```json
{
  "name": "DeskSharing",
  "parentTeamId": "optional-guid"
}
```

Behavior:

- `parentTeamId = null`: create root team and make creator owner.
- `parentTeamId != null`: require direct Admin/Owner membership on the parent, then create child team and make creator owner.

Expected failures:

- `404` when the requested parent does not exist or is not visible to the user in a way that may be safely disclosed.
- `403` when the parent exists and the user is a direct member without management rights.
- `400` for invalid hierarchy input.

### GET /api/documents

The endpoint is extended with:

```text
page
pageSize
scope=all|owned|team
teamId=<guid>
includeDescendants=true|false
query=<filename-fragment>
```

Rules:

- `scope=all`: `teamId` is ignored or rejected consistently; implementation must choose rejection for invalid combinations so callers get deterministic feedback.
- `scope=owned`: only owned documents are returned.
- `scope=team`: `teamId` is required.
- `includeDescendants` is valid only for `scope=team`.
- `query` applies to the authorized result set.
- Pagination and total count apply after all filters.

Recommended validation behavior:

```text
scope=team without teamId              -> 400
scope!=team with teamId                -> 400
scope!=team with includeDescendants    -> 400
invalid/unknown scope                  -> 400
```

## Authorization Query Design

Authorization must be enforced inside the database retrieval query, not after loading documents.

The flow remains:

```text
UI filter
   ↓
API request
   ↓
permission-aware query
   ↓
PostgreSQL returns only authorized documents
```

For `scope=all`, the existing ownership-or-team-membership access predicate remains the base.

For `scope=owned`, the query filters directly by owner user ID.

For `scope=team`, the query must first derive the allowed team IDs from explicit memberships and the requested hierarchy scope, then retrieve only documents shared with those allowed team IDs.

When descendant aggregation is requested, the query must intersect:

```text
requested team's descendants
INTERSECT
current user's direct team memberships
```

before joining document-team sharing.

This preserves the principle used by permission-aware semantic search: authorization happens during retrieval, before protected content leaves persistence.

## Frontend: Documents Page

The Documents page gains a persistent library navigation area.

Desktop layout:

```text
┌──────────────────────┬──────────────────────────────────────┐
│ All documents        │ Search documents...                  │
│ My documents         │                                      │
│                      │ Document rows/cards                   │
│ Teams                │                                      │
│ ▼ Rai                │                                      │
│   DeskSharing        │                                      │
│   Booking            │                                      │
│ ▼ Stellantis         │                                      │
│   ▼ Finance          │                                      │
│      Reporting       │                                      │
└──────────────────────┴──────────────────────────────────────┘
```

On narrow screens the same navigation may collapse into a drawer or selector, but it must preserve hierarchy and selected scope.

### Team node behavior

- Clicking a direct member team selects that team scope.
- Clicking a structural ancestor selects descendant aggregation across the descendant teams that the user is authorized to access.
- Nodes must visually distinguish structural-only ancestors from direct memberships if necessary to avoid implying membership.
- Expand/collapse is independent of selecting a filter.

### Document row/card metadata

The UI shows access provenance:

```text
architecture.pdf
Ready · Mine

manuale.pdf
Ready · Rai / DeskSharing

budget-2027.pdf
Ready · Stellantis / Finance / Budgeting
```

For multiple paths, use compact chips and avoid repeating long paths when space is limited.

### Search and pagination

Changing scope or search query resets pagination to page 1.

The server remains the source of truth for pagination, counts, and search results.

## Frontend: Team Administration

The existing Teams/Administration area is changed from a flat list into a tree-oriented management view.

Supported actions in this feature:

- Create root team.
- Select a team.
- Create child team under the selected team when `canManage = true`.
- Continue existing member-management operations for teams where the user has permission.

Not included:

- drag and drop;
- moving a team between parents;
- recursive deletion;
- inherited memberships;
- inherited roles;
- bulk membership propagation.

## Performance

The initial hierarchy uses adjacency-list traversal.

Indexes required:

```text
teams(parent_team_id)
team_members(user_id, team_id)
document_team_access(team_id, document_id)
documents(owner_user_id)
```

Existing useful indexes should be reused rather than duplicated.

For the expected portfolio/demo scale, recursive hierarchy traversal in PostgreSQL or bounded application-side hierarchy resolution is acceptable. The implementation plan must prefer a single permission-aware database query for document retrieval and avoid N+1 queries for team paths.

If future measurements show hierarchy traversal dominates latency at large scale, a closure table or materialized path can be evaluated with benchmark evidence.

## Security Properties

The following must remain true:

- A parent membership never grants child membership.
- A child membership never grants parent membership.
- Structural ancestor visibility never grants document access.
- Team tree visibility must not be reused as an authorization predicate.
- Document filters must never widen the existing access set.
- Search is performed only within the authorized scope.
- A team selected by ID cannot be used to retrieve documents from teams the user is not authorized to access.
- The server computes current-user authorization; the client cannot assert membership through request metadata.

## Testing Strategy

### Domain/Application tests

Cover:

- root team creation;
- child team creation by parent Owner;
- child team creation by parent Admin;
- child team creation rejected for Member;
- child team creation rejected for non-member;
- no membership inheritance;
- hierarchy validation;
- team navigation contains required structural ancestors;
- structural ancestor has `isMember = false` and no role.

### Infrastructure tests

Cover:

- persistence of `ParentTeamId`;
- hierarchy traversal;
- team tree retrieval for a user with memberships at different depths;
- owned document filtering;
- direct team filtering;
- descendant aggregation constrained by direct memberships;
- filename search combined with authorization;
- pagination/count correctness after filtering;
- no duplicate document rows when one document is accessible through several teams;
- team access metadata returns all visible access paths without unauthorized team paths.

### API integration tests

Cover:

- create root team;
- create child team with valid management role;
- forbidden child creation;
- GET teams hierarchy contract;
- GET documents all/owned/team scopes;
- invalid filter combinations return 400;
- structural-parent selection cannot expose unauthorized descendants.

### Frontend tests

Cover:

- tree construction from flat team nodes;
- scope selection;
- structural ancestor descendant aggregation request;
- My documents request;
- search resets page to 1;
- team filter resets page to 1;
- access-path chips render correctly;
- structural-only ancestors are not shown as direct memberships;
- child-team creation is available only when `canManage` is true.

## Migration and Compatibility

Existing teams are migrated as root teams with:

```text
parent_team_id = NULL
```

Existing memberships and document shares remain unchanged.

The default Documents request without new filters must retain the current `All documents` behavior so existing frontend/API consumers can migrate incrementally.

The frontend and API changes should ship together in the same feature branch because the team and document response contracts change.

## Out of Scope

This feature explicitly excludes:

- automatic membership inheritance;
- automatic document-sharing inheritance;
- moving/reparenting teams;
- deleting a team hierarchy;
- per-level semantic types such as Customer/Department/Application;
- drag-and-drop hierarchy editing;
- organization-wide/global administrators;
- closure-table optimization;
- notification-bell debugging or realtime notification fixes.

The notification issue must remain a separate defect/feature concern so it is not hidden inside this architectural change.

## Acceptance Criteria

The feature is accepted when all of the following are verified:

1. Users can create root teams and authorized users can create arbitrary-depth child teams.
2. Membership remains explicit at every team level.
3. The team tree displays accessible teams in their structural hierarchy, including required ancestors.
4. The Documents page can filter between All documents, My documents, and a selected team branch.
5. Selecting a parent aggregates only descendant teams for which the current user has explicit membership.
6. Filename search works server-side within the selected authorized scope.
7. Document rows show ownership and visible team access paths.
8. Unauthorized team descendants cannot be exposed by manipulating query parameters.
9. Existing teams and sharing relationships remain valid after migration.
10. Backend, frontend, integration, and container CI remain green.
