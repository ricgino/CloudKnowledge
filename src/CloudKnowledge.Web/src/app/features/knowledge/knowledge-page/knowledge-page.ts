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
} from '../../documents/documents';

@Component({
  selector: 'app-knowledge-page',
  standalone: false,
  templateUrl: './knowledge-page.html',
  styleUrl: './knowledge-page.scss'
})
export class KnowledgePage
  implements OnInit
{
  documents: DocumentItem[] = [];

  searchQuery = '';
  searchResults: SearchDocumentResult[] = [];
  searching = false;
  searchSubmitted = false;

  question = '';
  answer = '';
  answerSources: AskDocumentSource[] = [];
  asking = false;
  downloadingDocumentId = '';

  errorMessage = '';

  constructor(
    private readonly documentsService: Documents,
    private readonly cdr: ChangeDetectorRef)
  {
  }

  ngOnInit(): void
  {
    this.documentsService
      .getDocuments()
      .subscribe({
        next: response =>
        {
          this.documents = response.items;
          this.cdr.detectChanges();
        },
        error: () =>
        {
          this.documents = [];
          this.cdr.detectChanges();
        }
      });
  }

  onSearchQueryChanged(
    event: Event):
    void
  {
    this.searchQuery =
      (event.target as HTMLInputElement).value;

    this.searchSubmitted = false;
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
    this.searchSubmitted = true;
    this.searchResults = [];
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

  onQuestionChanged(
    event: Event):
    void
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

  downloadDocument(
    documentId: string):
    void
  {
    const fileName =
      this.documentName(documentId);

    this.downloadingDocumentId =
      documentId;
    this.errorMessage = '';

    this.documentsService
      .downloadDocument(documentId)
      .subscribe({
        next: blob =>
        {
          const url =
            URL.createObjectURL(blob);

          const anchor =
            window.document.createElement('a');

          anchor.href = url;
          anchor.download = fileName;
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

  documentName(
    documentId: string):
    string
  {
    return this.documents
      .find(document => document.id === documentId)
      ?.fileName ?? documentId;
  }

  relevanceLabel(
    similarity: number):
    string
  {
    if (similarity >= 0.6)
    {
      return 'High';
    }

    if (similarity >= 0.3)
    {
      return 'Medium';
    }

    return 'Low';
  }
}
