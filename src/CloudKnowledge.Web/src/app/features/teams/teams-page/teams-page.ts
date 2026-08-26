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
  selectedTeamId = '';
  newTeamName = '';
  memberEmail = '';

  loading = false;
  creating = false;
  addingMember = false;

  errorMessage = '';
  successMessage = '';

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

          if (
            !this.selectedTeamId &&
            teams.length > 0)
          {
            this.selectedTeamId =
              teams[0].id;
          }

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

  onTeamNameChanged(
    event: Event):
    void
  {
    this.newTeamName =
      (event.target as HTMLInputElement).value;
  }

  createTeam(): void
  {
    const name =
      this.newTeamName.trim();

    if (!name)
    {
      return;
    }

    this.creating = true;
    this.errorMessage = '';
    this.successMessage = '';

    this.teamsService
      .createTeam(name)
      .subscribe({
        next: team =>
        {
          this.newTeamName = '';
          this.selectedTeamId = team.id;
          this.successMessage =
            `Team ${team.name} created.`;

          this.creating = false;
          this.loadTeams();
        },
        error: error =>
        {
          this.errorMessage =
            `Unable to create team (HTTP ${error.status}).`;

          this.creating = false;
          this.cdr.detectChanges();
        }
      });
  }

  onSelectedTeamChanged(
    event: Event):
    void
  {
    this.selectedTeamId =
      (event.target as HTMLSelectElement).value;
  }

  onMemberEmailChanged(
    event: Event):
    void
  {
    this.memberEmail =
      (event.target as HTMLInputElement).value;
  }

  addMember(): void
  {
    const email =
      this.memberEmail.trim();

    if (
      !this.selectedTeamId ||
      !email)
    {
      return;
    }

    this.addingMember = true;
    this.errorMessage = '';
    this.successMessage = '';

    this.teamsService
      .addMember(
        this.selectedTeamId,
        email)
      .subscribe({
        next: member =>
        {
          this.memberEmail = '';
          this.successMessage =
            `${member.email} added as ${member.role}.`;

          this.addingMember = false;
          this.cdr.detectChanges();
        },
        error: error =>
        {
          const apiMessage =
            error.error?.message;

          this.errorMessage =
            apiMessage ??
            `Unable to add member (HTTP ${error.status}).`;

          this.addingMember = false;
          this.cdr.detectChanges();
        }
      });
  }
}
