import {
  ChangeDetectorRef,
  Component,
  OnInit
} from '@angular/core';

import {
  buildTeamTreeRows,
  TeamItem,
  TeamTreeRow,
  Teams
} from '../teams';

@Component({
  selector: 'app-teams-page',
  standalone: false,
  templateUrl: './teams-page.html',
  styleUrl: './teams-page.scss'
})
export class TeamsPage
  implements OnInit
{
  teams: TeamItem[] = [];
  loading = false;
  errorMessage = '';

  constructor(
    private readonly teamsService: Teams,
    private readonly cdr: ChangeDetectorRef)
  {
  }

  get teamRows(): TeamTreeRow[]
  {
    return buildTeamTreeRows(
      this.teams);
  }

  ngOnInit(): void
  {
    this.loadTeams();
  }

  loadTeams(): void
  {
    this.loading = true;
    this.errorMessage = '';

    this.teamsService
      .getTeams()
      .subscribe({
        next: teams =>
        {
          this.teams = teams;
          this.loading = false;
          this.cdr.detectChanges();
        },
        error: error =>
        {
          this.errorMessage =
            `Unable to load teams (HTTP ${error.status}).`;
          this.loading = false;
          this.cdr.detectChanges();
        }
      });
  }

  roleDescription(
    team: TeamItem):
    string
  {
    if (!team.isMember)
    {
      return 'Structural ancestor shown only to preserve the team path. It grants no membership or document access.';
    }

    switch (team.role)
    {
      case 'Owner':
        return 'You own this team and can manage membership and create sub-teams.';
      case 'Admin':
        return 'You can manage this team, its members and create sub-teams.';
      case 'Member':
        return 'Documents explicitly shared with this team are available to you.';
      default:
        return 'Direct team membership.';
    }
  }
}
