import {
  Injectable
} from '@angular/core';

import {
  HttpClient
} from '@angular/common/http';

import {
  Observable
} from 'rxjs';

import {
  apiBaseUrl
} from '../../auth-config';

export interface DocumentItem
{
  id: string;
  fileName: string;
  contentType: string;
  status: string;
}

export interface DocumentsPageResponse
{
  items: DocumentItem[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface SearchDocumentResult
{
  documentId: string;
  chunkId: string;
  position: number;
  content: string;
  similarity: number;
}

export interface AskDocumentSource extends SearchDocumentResult
{
  label: string;
}

export interface AskDocumentsResponse
{
  answer: string;
  sources: AskDocumentSource[];
}

@Injectable({
  providedIn: 'root'
})
export class Documents
{
  constructor(
    private readonly http:
      HttpClient)
  {
  }

  getDocuments(
    page = 1,
    pageSize = 20):
    Observable<DocumentsPageResponse>
  {
    return this.http.get<DocumentsPageResponse>(
      `${apiBaseUrl}/api/documents?page=${page}&pageSize=${pageSize}`);
  }

  uploadDocument(
    file: File):
    Observable<DocumentItem>
  {
    const formData =
      new FormData();

    formData.append(
      'File',
      file);

    return this.http.post<DocumentItem>(
      `${apiBaseUrl}/api/documents`,
      formData);
  }

  shareWithTeam(
    documentId: string,
    teamId: string):
    Observable<void>
  {
    return this.http.put<void>(
      `${apiBaseUrl}/api/documents/${documentId}/teams/${teamId}`,
      null);
  }

  unshareFromTeam(
    documentId: string,
    teamId: string):
    Observable<void>
  {
    return this.http.delete<void>(
      `${apiBaseUrl}/api/documents/${documentId}/teams/${teamId}`);
  }

  search(
    query: string,
    take = 5):
    Observable<SearchDocumentResult[]>
  {
    return this.http.post<SearchDocumentResult[]>(
      `${apiBaseUrl}/api/search`,
      {
        query,
        take
      });
  }

  ask(
    question: string,
    take = 5):
    Observable<AskDocumentsResponse>
  {
    return this.http.post<AskDocumentsResponse>(
      `${apiBaseUrl}/api/ask`,
      {
        question,
        take
      });
  }
}
