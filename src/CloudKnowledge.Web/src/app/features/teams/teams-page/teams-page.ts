import {
  ChangeDetectorRef,
  Component,
  OnInit
} from '@angular/core';

import {
  TeamItem,
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
    role: string):
    string
  {
    switch (role)
    {
      case 'Owner':
        return 'You own this team and can manage its membership.';
      case 'Admin':
        return 'You can manage this team and its members.';
      case 'Member':
        return 'Shared team documents are available to your knowledge search.';
      default:
        return 'Team membership';
    }
  }
}
