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
  selectedFiles: File[] = [];
  selectedTeamId = '';
  deletingDocumentId = '';
  downloadingDocumentId = '';
  retryingDocumentId = '';

  errorMessage = '';
  successMessage = '';

  private selectedFileInput:
    HTMLInputElement | null = null;

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

  get selectedFilesLabel(): string
  {
    if (this.selectedFiles.length === 0)
    {
      return 'Choose PDF, DOCX or TXT';
    }

    if (this.selectedFiles.length === 1)
    {
      return this.selectedFiles[0].name;
    }

    return `${this.selectedFiles.length} files selected`;
  }

  get uploadButtonLabel(): string
  {
    const count =
      this.selectedFiles.length;

    if (this.uploading)
    {
      return count === 1
        ? 'Uploading document...'
        : `Uploading ${count} documents...`;
    }

    return count <= 1
      ? 'Upload document'
      : `Upload ${count} documents`;
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

  get paginationItems(): Array<number | null>
  {
    if (this.totalPages <= 0)
    {
      return [];
    }

    if (this.totalPages <= 7)
    {
      return Array.from(
        { length: this.totalPages },
        (_, index) => index + 1);
    }

    let start =
      Math.max(
        2,
        this.page - 2);

    let end =
      Math.min(
        this.totalPages - 1,
        this.page + 2);

    if (this.page <= 4)
    {
      start = 2;
      end = 6;
    }
    else if (this.page >= this.totalPages - 3)
    {
      start = this.totalPages - 5;
      end = this.totalPages - 1;
    }

    const items: Array<number | null> = [1];

    if (start > 2)
    {
      items.push(null);
    }

    for (let pageNumber = start;
      pageNumber <= end;
      pageNumber++)
    {
      items.push(pageNumber);
    }

    if (end < this.totalPages - 1)
    {
      items.push(null);
    }

    items.push(this.totalPages);

    return items;
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

  goToPage(
    targetPage: number):
    void
  {
    if (
      this.loading ||
      targetPage < 1 ||
      targetPage > this.totalPages ||
      targetPage === this.page)
    {
      return;
    }

    this.page = targetPage;
    this.loadDocuments();
  }

  previousPage(): void
  {
    this.goToPage(
      this.page - 1);
  }

  nextPage(): void
  {
    this.goToPage(
      this.page + 1);
  }

  onFileSelected(
    event: Event):
    void
  {
    const input =
      event.target as HTMLInputElement;

    const files =
      Array.from(
        input.files ?? []);

    this.selectedFileInput = input;
    this.successMessage = '';

    const unsupportedFiles =
      files.filter(
        file =>
          !isSupportedDocumentFileName(
            file.name));

    if (unsupportedFiles.length > 0)
    {
      this.selectedFiles = [];
      this.errorMessage =
        `Unsupported document${unsupportedFiles.length === 1 ? '' : 's'}: ${unsupportedFiles.map(file => file.name).join(', ')}. Supported formats are PDF, DOCX and TXT.`;
      input.value = '';
      return;
    }

    this.selectedFiles = files;
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
    if (this.selectedFiles.length === 0)
    {
      return;
    }

    const unsupportedFiles =
      this.selectedFiles.filter(
        file =>
          !isSupportedDocumentFileName(
            file.name));

    if (unsupportedFiles.length > 0)
    {
      this.errorMessage =
        'Supported document formats are PDF, DOCX and TXT.';
      return;
    }

    const files =
      [...this.selectedFiles];

    const teamName =
      this.teams.find(
        team => team.id === this.selectedTeamId)
        ?.name;

    this.uploading = true;
    this.errorMessage = '';
    this.successMessage = '';

    this.documentsService
      .uploadDocuments(
        files,
        this.selectedTeamId || undefined)
      .subscribe({
        next: outcomes =>
        {
          const succeeded =
            outcomes.filter(
              outcome => outcome.succeeded);

          const failed =
            outcomes.filter(
              outcome => !outcome.succeeded);

          this.uploading = false;
          this.selectedFiles = [];

          if (this.selectedFileInput)
          {
            this.selectedFileInput.value = '';
          }

          if (succeeded.length === 1 && outcomes.length === 1)
          {
            this.successMessage =
              buildUploadSuccessMessage(
                succeeded[0].fileName,
                teamName);
          }
          else if (succeeded.length > 0)
          {
            const ownership =
              teamName
                ? `as team-owned documents in ${teamName}`
                : 'as personal documents';

            this.successMessage =
              `${succeeded.length}${failed.length > 0 ? ` of ${outcomes.length}` : ''} documents uploaded ${ownership}. Processing continues in the background.`;
          }

          if (failed.length > 0)
          {
            this.errorMessage =
              `Unable to upload ${failed.length} document${failed.length === 1 ? '' : 's'}: ${failed.map(outcome => outcome.fileName).join(', ')}.`;
          }

          if (succeeded.length > 0)
          {
            this.page = 1;
            this.loadDocuments();
          }
          else
          {
            this.cdr.detectChanges();
          }
        },
        error: error =>
        {
          this.errorMessage =
            error.error?.message ??
            `Unable to upload documents (HTTP ${error.status}).`;
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

  retryDocument(
    document: DocumentItem):
    void
  {
    if (
      !document.isOwner ||
      document.status !== 'Failed' ||
      this.retryingDocumentId === document.id)
    {
      return;
    }

    this.retryingDocumentId =
      document.id;
    this.errorMessage = '';
    this.successMessage = '';

    this.documentsService
      .retryDocument(document.id)
      .subscribe({
        next: () =>
        {
          this.retryingDocumentId = '';
          this.successMessage =
            `${document.fileName} queued for retry.`;
          this.loadDocuments();
        },
        error: error =>
        {
          this.errorMessage =
            error.status === 404
              ? 'This document cannot be retried.'
              : `Unable to retry document (HTTP ${error.status}).`;
          this.retryingDocumentId = '';
          this.cdr.detectChanges();
        }
      });
  }

  deleteDocument(
    document: DocumentItem):
    void
  {
    if (!document.canDelete)
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
              ? 'You are not allowed to delete this document.'
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
