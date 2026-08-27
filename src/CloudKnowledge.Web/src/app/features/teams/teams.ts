import {
  Injectable
} from '@angular/core';

import {
  HttpClient
} from '@angular/common/http';

import {
  Observable
} from 'rxjs';

import {
  apiBaseUrl
} from '../../auth-config';

export interface TeamItem
{
  id: string;
  name: string;
  parentTeamId: string | null;
  isMember: boolean;
  role: string | null;
  canManage: boolean;
}

export interface TeamTreeRow extends TeamItem
{
  depth: number;
  hasChildren: boolean;
}

export interface TeamMemberItem
{
  userId: string;
  email: string;
  role: string;
}

export function buildTeamTreeRows(
  teams: readonly TeamItem[]):
  TeamTreeRow[]
{
  const byParent =
    new Map<string | null, TeamItem[]>();

  const knownIds =
    new Set(
      teams.map(team => team.id));

  for (const team of teams)
  {
    const parentKey =
      team.parentTeamId &&
      knownIds.has(team.parentTeamId)
        ? team.parentTeamId
        : null;

    const siblings =
      byParent.get(parentKey) ?? [];

    siblings.push(team);
    byParent.set(parentKey, siblings);
  }

  for (const siblings of byParent.values())
  {
    siblings.sort(
      (left, right) =>
        left.name.localeCompare(
          right.name,
          undefined,
          { sensitivity: 'base' }));
  }

  const rows: TeamTreeRow[] = [];
  const visited = new Set<string>();

  const appendChildren =
    (parentId: string | null, depth: number): void =>
    {
      for (const team of byParent.get(parentId) ?? [])
      {
        if (visited.has(team.id))
        {
          continue;
        }

        visited.add(team.id);

        rows.push({
          ...team,
          depth,
          hasChildren:
            (byParent.get(team.id)?.length ?? 0) > 0
        });

        appendChildren(
          team.id,
          depth + 1);
      }
    };

  appendChildren(null, 0);

  // Defensive fallback for malformed/cyclic API data: keep every node visible
  // without recursing forever.
  for (const team of teams)
  {
    if (!visited.has(team.id))
    {
      rows.push({
        ...team,
        depth: 0,
        hasChildren:
          (byParent.get(team.id)?.length ?? 0) > 0
      });
    }
  }

  return rows;
}

@Injectable({
  providedIn: 'root'
})
export class Teams
{
  constructor(
    private readonly http: HttpClient)
  {
  }

  getTeams():
    Observable<TeamItem[]>
  {
    return this.http.get<TeamItem[]>(
      `${apiBaseUrl}/api/teams`);
  }

  createTeam(
    name: string,
    parentTeamId?: string):
    Observable<TeamItem>
  {
    return this.http.post<TeamItem>(
      `${apiBaseUrl}/api/teams`,
      {
        name,
        parentTeamId:
          parentTeamId || null
      });
  }

  addMember(
    teamId: string,
    email: string):
    Observable<TeamMemberItem>
  {
    return this.http.post<TeamMemberItem>(
      `${apiBaseUrl}/api/teams/${teamId}/members`,
      {
        email
      });
  }
}
