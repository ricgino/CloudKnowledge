import {
  ChangeDetectorRef,
  Component,
  OnInit
} from '@angular/core';

import {
  DocumentItem,
  Documents
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
  teams: TeamItem[] = [];

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
    this.loadDocuments();
    this.loadTeams();
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
          this.cdr.detectChanges();
        },
        error: () =>
        {
          this.teams = [];
          this.cdr.detectChanges();
        }
      });
  }

  onFileSelected(
    event: Event):
    void
  {
    const input =
      event.target as HTMLInputElement;

    this.selectedFile =
      input.files?.[0] ?? null;

    this.successMessage = '';
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
          this.successMessage = teamName
            ? `${fileName} uploaded and shared with ${teamName}. Processing continues in the background.`
            : `${fileName} uploaded as a personal document. Processing continues in the background.`;
          this.cdr.detectChanges();
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
