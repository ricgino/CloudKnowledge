import {
  ChangeDetectorRef,
  Component,
  OnInit
} from '@angular/core';

import {
  DocumentItem,
  Documents
} from '../../documents/documents';

import {
  buildTeamTreeRows,
  canDeleteTeam as canDeleteTeamItem,
  TeamItem,
  TeamTreeRow,
  Teams
} from '../../teams/teams';

type AdministrationTab =
  'users' |
  'teams' |
  'documents';

@Component({
  selector: 'app-administration-page',
  standalone: false,
  templateUrl: './administration-page.html',
  styleUrl: './administration-page.scss'
})
export class AdministrationPage
  implements OnInit
{
  activeTab: AdministrationTab = 'users';

  teams: TeamItem[] = [];
  documents: DocumentItem[] = [];

  selectedMemberTeamId = '';
  memberEmail = '';
  addingMember = false;

  newTeamName = '';
  newTeamParentId = '';
  creatingTeam = false;
  deletingTeamId = '';

  selectedDocumentId = '';
  selectedShareTeamId = '';
  updatingShare = false;

  loading = false;
  successMessage = '';
  errorMessage = '';

  constructor(
    private readonly teamsService: Teams,
    private readonly documentsService: Documents,
    private readonly cdr: ChangeDetectorRef)
  {
  }

  ngOnInit(): void
  {
    this.refresh();
  }

  get teamRows(): TeamTreeRow[]
  {
    return buildTeamTreeRows(
      this.teams);
  }

  get manageableTeams(): TeamItem[]
  {
    return this.teams
      .filter(team => team.canManage)
      .sort(
        (left, right) =>
          left.name.localeCompare(
            right.name,
            undefined,
            { sensitivity: 'base' }));
  }

  get readyDocuments(): DocumentItem[]
  {
    return this.documents
      .filter(document => document.status === 'Ready');
  }

  selectTab(
    tab: AdministrationTab):
    void
  {
    this.activeTab = tab;
    this.successMessage = '';
    this.errorMessage = '';
  }

  refresh(): void
  {
    this.loading = true;
    this.errorMessage = '';

    let pendingRequests = 2;

    const completed = () =>
    {
      pendingRequests--;

      if (pendingRequests === 0)
      {
        this.loading = false;
        this.ensureSelections();
        this.cdr.detectChanges();
      }
    };

    this.teamsService
      .getTeams()
      .subscribe({
        next: teams =>
        {
          this.teams = teams;
          completed();
        },
        error: error =>
        {
          this.errorMessage =
            `Unable to load teams (HTTP ${error.status}).`;
          completed();
        }
      });

    this.documentsService
      .getDocuments()
      .subscribe({
        next: response =>
        {
          this.documents = response.items;
          completed();
        },
        error: error =>
        {
          this.errorMessage =
            `Unable to load documents (HTTP ${error.status}).`;
          completed();
        }
      });
  }

  onMemberTeamChanged(
    event: Event):
    void
  {
    this.selectedMemberTeamId =
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
      !email ||
      !this.selectedMemberTeamId)
    {
      return;
    }

    this.addingMember = true;
    this.clearMessages();

    this.teamsService
      .addMember(
        this.selectedMemberTeamId,
        email)
      .subscribe({
        next: member =>
        {
          this.memberEmail = '';
          this.addingMember = false;
          this.successMessage =
            `${member.email} now has ${member.role} access to the selected team.`;
          this.cdr.detectChanges();
        },
        error: error =>
        {
          this.addingMember = false;
          this.errorMessage =
            error.error?.message ??
            `Unable to add user (HTTP ${error.status}).`;
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

  onTeamParentChanged(
    event: Event):
    void
  {
    this.newTeamParentId =
      (event.target as HTMLSelectElement).value;
  }

  createTeam(): void
  {
    const name =
      this.newTeamName.trim();

    if (!name)
    {
      return;
    }

    const parent =
      this.teams.find(
        team => team.id === this.newTeamParentId);

    this.creatingTeam = true;
    this.clearMessages();

    this.teamsService
      .createTeam(
        name,
        this.newTeamParentId || undefined)
      .subscribe({
        next: team =>
        {
          this.newTeamName = '';
          this.creatingTeam = false;
          this.successMessage = parent
            ? `${team.name} created under ${parent.name}. You are the owner of the new team.`
            : `${team.name} created as a root team. You are the owner.`;
          this.refresh();
        },
        error: error =>
        {
          this.creatingTeam = false;
          this.errorMessage =
            error.error?.message ??
            `Unable to create team (HTTP ${error.status}).`;
          this.cdr.detectChanges();
        }
      });
  }

  canDeleteTeam(
    team: TeamItem):
    boolean
  {
    return canDeleteTeamItem(team);
  }

  deleteTeam(
    team: TeamItem):
    void
  {
    if (!this.canDeleteTeam(team))
    {
      return;
    }

    const confirmed =
      window.confirm(
        `Delete ${team.name}? Team-owned documents and their search data will be permanently removed. Personal documents only shared with this team will be preserved.`);

    if (!confirmed)
    {
      return;
    }

    this.deletingTeamId = team.id;
    this.clearMessages();

    this.teamsService
      .deleteTeam(team.id)
      .subscribe({
        next: () =>
        {
          this.deletingTeamId = '';
          this.successMessage =
            `${team.name} deleted together with its team-owned documents.`;
          this.refresh();
        },
        error: error =>
        {
          this.deletingTeamId = '';

          if (error.status === 409)
          {
            this.errorMessage =
              error.error?.message ??
              'Delete child teams before deleting this team.';
          }
          else if (error.status === 403)
          {
            this.errorMessage =
              'Only a direct team owner can delete this team.';
          }
          else if (error.status === 404)
          {
            this.errorMessage =
              'Team not found or no longer visible to your account.';
          }
          else
          {
            this.errorMessage =
              `Unable to delete team (HTTP ${error.status}).`;
          }

          this.cdr.detectChanges();
        }
      });
  }

  onDocumentChanged(
    event: Event):
    void
  {
    this.selectedDocumentId =
      (event.target as HTMLSelectElement).value;
  }

  onShareTeamChanged(
    event: Event):
    void
  {
    this.selectedShareTeamId =
      (event.target as HTMLSelectElement).value;
  }

  shareDocument(): void
  {
    this.updateDocumentAccess(true);
  }

  unshareDocument(): void
  {
    this.updateDocumentAccess(false);
  }

  private updateDocumentAccess(
    share: boolean):
    void
  {
    if (
      !this.selectedDocumentId ||
      !this.selectedShareTeamId)
    {
      return;
    }

    this.updatingShare = true;
    this.clearMessages();

    const request =
      share
        ? this.documentsService.shareWithTeam(
            this.selectedDocumentId,
            this.selectedShareTeamId)
        : this.documentsService.unshareFromTeam(
            this.selectedDocumentId,
            this.selectedShareTeamId);

    request.subscribe({
      next: () =>
      {
        this.updatingShare = false;
        this.successMessage =
          share
            ? 'Document access granted to the selected team.'
            : 'Document access removed from the selected team.';
        this.cdr.detectChanges();
      },
      error: error =>
      {
        this.updatingShare = false;
        this.errorMessage =
          error.status === 404
            ? 'This operation is only available to the document owner for an eligible team.'
            : `Unable to update document access (HTTP ${error.status}).`;
        this.cdr.detectChanges();
      }
    });
  }

  private ensureSelections(): void
  {
    const manageableTeamIds =
      new Set(
        this.manageableTeams.map(team => team.id));

    if (
      !manageableTeamIds.has(
        this.selectedMemberTeamId))
    {
      this.selectedMemberTeamId =
        this.manageableTeams[0]?.id ?? '';
    }

    if (
      this.newTeamParentId &&
      !manageableTeamIds.has(
        this.newTeamParentId))
    {
      this.newTeamParentId = '';
    }

    if (
      !manageableTeamIds.has(
        this.selectedShareTeamId))
    {
      this.selectedShareTeamId =
        this.manageableTeams[0]?.id ?? '';
    }

    if (
      !this.documents.some(
        document =>
          document.id ===
          this.selectedDocumentId))
    {
      this.selectedDocumentId =
        this.documents[0]?.id ?? '';
    }
  }

  private clearMessages(): void
  {
    this.successMessage = '';
    this.errorMessage = '';
  }
}
