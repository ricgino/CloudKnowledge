# Document Library Controls Design

## Scope

Improve the existing document library and administration UI without changing the authorization invariant that hierarchy is organizational and permissions are explicit.

## Requirements

### Numeric pagination

Documents keeps Previous and Next controls and also renders directly selectable page numbers. For large result sets, the control must use a bounded window with ellipses rather than rendering every page. Selecting a page updates `page` and reloads the current server-side scope/search.

### Deleting team-owned documents

Keep ownership and deletion capability separate.

- A user-owned document remains `isOwner=true` only for its `OwnerUserId`.
- A team-owned document remains `isOwner=false` for users.
- Add `canDelete` to document-list responses.
- `canDelete=true` when the current user owns the personal document, or is a direct `Owner` member of the document's `OwnerTeamId`.
- `Admin` and `Member` roles cannot delete a team-owned document.
- Owning a team does not allow deletion of a user-owned document merely shared with that team.
- Delete authorization is enforced in the backend repository/use case, not only by the frontend button.
- Successful deletion removes the database document graph through existing EF cascade behavior and removes the blob through the existing deletion storage.

### Administration form containment

The Team name, Parent, and User email controls must remain within their grid cells on desktop and mobile. Form controls use border-box sizing and grid children may shrink below intrinsic content width. No horizontal overlap or container overflow should be introduced by long select/input content.

## Non-goals

- No inherited team delete rights.
- No Admin delete rights for team-owned documents.
- No bulk document deletion.
- No change to page size or backend pagination model.
- No redesign of Administration beyond fixing containment.
