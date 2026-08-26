import {
  ChangeDetectorRef,
  Component,
  OnInit
} from '@angular/core';

import {
  AskDocumentSource,
  DocumentItem,
  Documents,
  SearchDocumentResult
} from '../documents';

import {
  TeamItem,
  Teams
} from '../../teams/teams';

@Component({
  selector: 'app-documents-page',
  standalone: false,
  styleUrl: './documents-page.scss',
  templateUrl: './documents-page.html'
})
export class DocumentsPage
  implements OnInit
{
  documents: DocumentItem[] = [];
  loading = false;
  uploading = false;
  errorMessage = '';
  selectedFile: File | null = null;

  teams: TeamItem[] = [];
  selectedDocumentId = '';
  selectedTeamId = '';
  sharing = false;
  sharingMessage = '';

  searchQuery = '';
  searchResults: SearchDocumentResult[] = [];
  searching = false;

  question = '';
  answer = '';
  answerSources: AskDocumentSource[] = [];
  asking = false;

  constructor(
    private readonly documentsService: Documents,
    private readonly teamsService: Teams,
    private readonly cdr: ChangeDetectorRef)
  {
  }

  ngOnInit(): void
  {
    this.loadDocuments();
    this.loadTeams();
  }

  loadDocuments(): void
  {
    this.loading = true;
    this.errorMessage = '';

    this.documentsService
      .getDocuments()
      .subscribe({
        next: response =>
        {
          this.documents = response.items;

          if (
            !this.selectedDocumentId &&
            this.documents.length > 0)
          {
            this.selectedDocumentId =
              this.documents[0].id;
          }

          this.loading = false;
          this.cdr.detectChanges();
        },
        error: error =>
        {
          this.errorMessage =
            `Unable to load documents (HTTP ${error.status}).`;
          this.loading = false;
          this.cdr.detectChanges();
        }
      });
  }

  loadTeams(): void
  {
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

          this.cdr.detectChanges();
        },
        error: error =>
        {
          this.errorMessage =
            `Unable to load teams for sharing (HTTP ${error.status}).`;
          this.cdr.detectChanges();
        }
      });
  }

  onFileSelected(event: Event): void
  {
    const input =
      event.target as HTMLInputElement;

    this.selectedFile =
      input.files?.[0] ?? null;
  }

  upload(): void
  {
    if (!this.selectedFile)
    {
      return;
    }

    this.uploading = true;
    this.errorMessage = '';

    this.documentsService
      .uploadDocument(this.selectedFile)
      .subscribe({
        next: () =>
        {
          this.uploading = false;
          this.selectedFile = null;
          this.cdr.detectChanges();
          this.loadDocuments();
        },
        error: error =>
        {
          this.errorMessage =
            `Unable to upload document (HTTP ${error.status}).`;
          this.uploading = false;
          this.cdr.detectChanges();
        }
      });
  }

  onSelectedDocumentChanged(event: Event): void
  {
    this.selectedDocumentId =
      (event.target as HTMLSelectElement).value;
  }

  onSelectedTeamChanged(event: Event): void
  {
    this.selectedTeamId =
      (event.target as HTMLSelectElement).value;
  }

  shareSelected(): void
  {
    if (
      !this.selectedDocumentId ||
      !this.selectedTeamId)
    {
      return;
    }

    this.sharing = true;
    this.sharingMessage = '';
    this.errorMessage = '';

    this.documentsService
      .shareWithTeam(
        this.selectedDocumentId,
        this.selectedTeamId)
      .subscribe({
        next: () =>
        {
          this.sharing = false;
          this.sharingMessage =
            'Document shared with the selected team.';
          this.cdr.detectChanges();
        },
        error: error =>
        {
          this.sharing = false;
          this.errorMessage =
            error.status === 404
              ? 'Only the document owner can change sharing for this team.'
              : `Unable to share document (HTTP ${error.status}).`;
          this.cdr.detectChanges();
        }
      });
  }

  unshareSelected(): void
  {
    if (
      !this.selectedDocumentId ||
      !this.selectedTeamId)
    {
      return;
    }

    this.sharing = true;
    this.sharingMessage = '';
    this.errorMessage = '';

    this.documentsService
      .unshareFromTeam(
        this.selectedDocumentId,
        this.selectedTeamId)
      .subscribe({
        next: () =>
        {
          this.sharing = false;
          this.sharingMessage =
            'Document access removed from the selected team.';
          this.cdr.detectChanges();
        },
        error: error =>
        {
          this.sharing = false;
          this.errorMessage =
            error.status === 404
              ? 'Only the document owner can change sharing for this team.'
              : `Unable to unshare document (HTTP ${error.status}).`;
          this.cdr.detectChanges();
        }
      });
  }

  onSearchQueryChanged(event: Event): void
  {
    this.searchQuery =
      (event.target as HTMLInputElement).value;
  }

  search(): void
  {
    const query =
      this.searchQuery.trim();

    if (!query)
    {
      return;
    }

    this.searching = true;
    this.errorMessage = '';

    this.documentsService
      .search(query)
      .subscribe({
        next: results =>
        {
          this.searchResults = results;
          this.searching = false;
          this.cdr.detectChanges();
        },
        error: error =>
        {
          this.errorMessage =
            `Unable to search documents (HTTP ${error.status}).`;
          this.searching = false;
          this.cdr.detectChanges();
        }
      });
  }

  onQuestionChanged(event: Event): void
  {
    this.question =
      (event.target as HTMLTextAreaElement).value;
  }

  ask(): void
  {
    const question =
      this.question.trim();

    if (!question)
    {
      return;
    }

    this.asking = true;
    this.answer = '';
    this.answerSources = [];
    this.errorMessage = '';

    this.documentsService
      .ask(question)
      .subscribe({
        next: response =>
        {
          this.answer = response.answer;
          this.answerSources = response.sources;
          this.asking = false;
          this.cdr.detectChanges();
        },
        error: error =>
        {
          this.errorMessage =
            `Unable to answer question (HTTP ${error.status}).`;
          this.asking = false;
          this.cdr.detectChanges();
        }
      });
  }

  documentName(documentId: string): string
  {
    return this.documents
      .find(document => document.id === documentId)
      ?.fileName ?? documentId;
  }
}
