import {
  ChangeDetectorRef,
  Component,
  OnInit
} from '@angular/core';

import {
  DocumentItem,
  Documents
} from '../documents';

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
  selectedFile: File | null = null;

  errorMessage = '';
  successMessage = '';

  constructor(
    private readonly documentsService: Documents,
    private readonly cdr: ChangeDetectorRef)
  {
  }

  ngOnInit(): void
  {
    this.loadDocuments();
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

  upload(): void
  {
    if (!this.selectedFile)
    {
      return;
    }

    const fileName =
      this.selectedFile.name;

    this.uploading = true;
    this.errorMessage = '';
    this.successMessage = '';

    this.documentsService
      .uploadDocument(this.selectedFile)
      .subscribe({
        next: () =>
        {
          this.uploading = false;
          this.selectedFile = null;
          this.successMessage =
            `${fileName} uploaded. Processing continues in the background.`;
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
