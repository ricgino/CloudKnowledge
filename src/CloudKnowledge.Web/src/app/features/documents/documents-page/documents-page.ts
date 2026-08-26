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

  searchQuery = '';
  searchResults: SearchDocumentResult[] = [];
  searching = false;

  question = '';
  answer = '';
  answerSources: AskDocumentSource[] = [];
  asking = false;

  constructor(
    private readonly documentsService: Documents,
    private readonly cdr: ChangeDetectorRef)
  {
  }

  ngOnInit(): void
  {
    this.loadDocuments();
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
