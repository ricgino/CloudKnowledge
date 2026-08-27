import {
  ChangeDetectorRef,
  Component,
  OnInit
} from '@angular/core';

import {
  buildUploadSuccessMessage,
  DocumentItem,
  DocumentListScope,
  Documents,
  isSupportedDocumentFileName
} from '../documents';

import {
  buildTeamTreeRows,
  TeamItem,
  TeamTreeRow,
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
  teams: TeamItem[] = [];
  teamRows: TeamTreeRow[] = [];

  scope: DocumentListScope = 'all';
  selectedLibraryTeamId = '';
  includeDescendants = false;
  searchInput = '';
  activeSearchQuery = '';

  page = 1;
  pageSize = 20;
  totalCount = 0;
  totalPages = 0;

  loading = false;
  uploading = false;
  selectedFile: File | null = null;
  selectedTeamId = '';
  deletingDocumentId = '';
  downloadingDocumentId = '';

  errorMessage = '';
  successMessage = '';

  constructor(
    private readonly documentsService: Documents,
    private readonly teamsService: Teams,
    private readonly cdr: ChangeDetectorRef)
  {
  }

  ngOnInit(): void
  {
    this.loadTeams();
    this.loadDocuments();
  }

  get uploadTeams(): TeamItem[]
  {
    return this.teams
      .filter(team => team.isMember)
      .sort(
        (left, right) =>
          left.name.localeCompare(
            right.name,
            undefined,
            { sensitivity: 'base' }));
  }

  get readyCount(): number
  {
    return this.documents
      .filter(document => document.status === 'Ready')
      .length;
  }

  get processingCount(): number
  {
    return this.documents
      .filter(
        document =>
          document.status === 'Pending' ||
          document.status === 'Processing')
      .length;
  }

  get failedCount(): number
  {
    return this.documents
      .filter(document => document.status === 'Failed')
      .length;
  }

  get selectedScopeLabel(): string
  {
    if (this.scope === 'owned')
    {
      return 'My documents';
    }

    if (this.scope === 'team')
    {
      return this.teams.find(
        team => team.id === this.selectedLibraryTeamId)
        ?.name ?? 'Team documents';
    }

    return 'All documents';
  }

  loadDocuments(): void
  {
    this.loading = true;
    this.errorMessage = '';

    this.documentsService
      .getDocuments({
        page: this.page,
        pageSize: this.pageSize,
        scope: this.scope,
        teamId:
          this.scope === 'team'
            ? this.selectedLibraryTeamId
            : undefined,
        includeDescendants:
          this.scope === 'team'
            ? this.includeDescendants
            : false,
        query:
          this.activeSearchQuery || undefined
      })
      .subscribe({
        next: response =>
        {
          this.documents = response.items;
          this.page = response.page;
          this.pageSize = response.pageSize;
          this.totalCount = response.totalCount;
          this.totalPages = response.totalPages;
          this.loading = false;
          this.cdr.detectChanges();
        },
        error: error =>
        {
          this.errorMessage =
            error.error?.message ??
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
          this.teamRows =
            buildTeamTreeRows(teams);
          this.cdr.detectChanges();
        },
        error: () =>
        {
          this.teams = [];
          this.teamRows = [];
          this.cdr.detectChanges();
        }
      });
  }

  selectAllDocuments(): void
  {
    this.scope = 'all';
    this.selectedLibraryTeamId = '';
    this.includeDescendants = false;
    this.page = 1;
    this.loadDocuments();
  }

  selectMyDocuments(): void
  {
    this.scope = 'owned';
    this.selectedLibraryTeamId = '';
    this.includeDescendants = false;
    this.page = 1;
    this.loadDocuments();
  }

  selectTeamScope(
    team: TeamTreeRow):
    void
  {
    this.scope = 'team';
    this.selectedLibraryTeamId = team.id;
    this.includeDescendants = team.hasChildren;
    this.page = 1;
    this.loadDocuments();
  }

  onSearchInput(
    event: Event):
    void
  {
    this.searchInput =
      (event.target as HTMLInputElement).value;
  }

  applySearch(): void
  {
    this.activeSearchQuery =
      this.searchInput.trim();
    this.page = 1;
    this.loadDocuments();
  }

  clearSearch(): void
  {
    if (!this.searchInput && !this.activeSearchQuery)
    {
      return;
    }

    this.searchInput = '';
    this.activeSearchQuery = '';
    this.page = 1;
    this.loadDocuments();
  }

  previousPage(): void
  {
    if (this.page <= 1 || this.loading)
    {
      return;
    }

    this.page--;
    this.loadDocuments();
  }

  nextPage(): void
  {
    if (
      this.loading ||
      this.totalPages === 0 ||
      this.page >= this.totalPages)
    {
      return;
    }

    this.page++;
    this.loadDocuments();
  }

  onFileSelected(
    event: Event):
    void
  {
    const input =
      event.target as HTMLInputElement;

    const file =
      input.files?.[0] ?? null;

    this.successMessage = '';

    if (
      file &&
      !isSupportedDocumentFileName(file.name))
    {
      this.selectedFile = null;
      this.errorMessage =
        'Supported document formats are PDF, DOCX and TXT.';
      input.value = '';
      return;
    }

    this.selectedFile = file;
    this.errorMessage = '';
  }

  onSelectedTeamChanged(
    event: Event):
    void
  {
    this.selectedTeamId =
      (event.target as HTMLSelectElement).value;
  }

  upload(): void
  {
    if (!this.selectedFile)
    {
      return;
    }

    if (!isSupportedDocumentFileName(
        this.selectedFile.name))
    {
      this.errorMessage =
        'Supported document formats are PDF, DOCX and TXT.';
      return;
    }

    const fileName =
      this.selectedFile.name;

    const teamName =
      this.teams.find(
        team => team.id === this.selectedTeamId)
        ?.name;

    this.uploading = true;
    this.errorMessage = '';
    this.successMessage = '';

    this.documentsService
      .uploadDocument(
        this.selectedFile,
        this.selectedTeamId || undefined)
      .subscribe({
        next: () =>
        {
          this.uploading = false;
          this.selectedFile = null;
          this.successMessage =
            buildUploadSuccessMessage(
              fileName,
              teamName);
          this.page = 1;
          this.loadDocuments();
        },
        error: error =>
        {
          const apiMessage =
            error.error?.message;

          this.errorMessage =
            apiMessage ??
            `Unable to upload document (HTTP ${error.status}).`;
          this.uploading = false;
          this.cdr.detectChanges();
        }
      });
  }

  downloadDocument(
    document: DocumentItem):
    void
  {
    this.downloadingDocumentId =
      document.id;
    this.errorMessage = '';

    this.documentsService
      .downloadDocument(document.id)
      .subscribe({
        next: blob =>
        {
          const url =
            URL.createObjectURL(blob);

          const anchor =
            window.document.createElement('a');

          anchor.href = url;
          anchor.download = document.fileName;
          anchor.click();

          URL.revokeObjectURL(url);
          this.downloadingDocumentId = '';
          this.cdr.detectChanges();
        },
        error: error =>
        {
          this.errorMessage =
            `Unable to download document (HTTP ${error.status}).`;
          this.downloadingDocumentId = '';
          this.cdr.detectChanges();
        }
      });
  }

  deleteDocument(
    document: DocumentItem):
    void
  {
    if (!document.isOwner)
    {
      return;
    }

    const confirmed =
      window.confirm(
        `Delete ${document.fileName}? This removes the document, its search chunks and team access.`);

    if (!confirmed)
    {
      return;
    }

    this.deletingDocumentId =
      document.id;
    this.errorMessage = '';
    this.successMessage = '';

    this.documentsService
      .deleteDocument(document.id)
      .subscribe({
        next: () =>
        {
          this.deletingDocumentId = '';
          this.successMessage =
            `${document.fileName} deleted.`;

          if (
            this.documents.length === 1 &&
            this.page > 1)
          {
            this.page--;
          }

          this.loadDocuments();
        },
        error: error =>
        {
          this.errorMessage =
            error.status === 404
              ? 'Only the document owner can delete this document.'
              : `Unable to delete document (HTTP ${error.status}).`;
          this.deletingDocumentId = '';
          this.cdr.detectChanges();
        }
      });
  }

  documentTypeLabel(
    fileName: string):
    string
  {
    const extension =
      fileName
        .split('.')
        .pop()
        ?.toUpperCase();

    return extension || 'DOC';
  }

  statusDescription(
    status: string):
    string
  {
    switch (status)
    {
      case 'Ready':
        return 'Searchable and available to AI retrieval';
      case 'Processing':
        return 'Extracting text and generating embeddings';
      case 'Pending':
        return 'Waiting for background processing';
      case 'Failed':
        return 'Processing failed';
      default:
        return status;
    }
  }
}
