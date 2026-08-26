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
  role: string;
}

export interface TeamMemberItem
{
  userId: string;
  email: string;
  role: string;
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
    name: string):
    Observable<TeamItem>
  {
    return this.http.post<TeamItem>(
      `${apiBaseUrl}/api/teams`,
      {
        name
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
