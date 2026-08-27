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

export type DocumentListScope =
  'all' |
  'owned' |
  'team';

export interface DocumentsQuery
{
  page: number;
  pageSize: number;
  scope: DocumentListScope;
  teamId?: string;
  includeDescendants?: boolean;
  query?: string;
}

export interface DocumentAccessTeam
{
  id: string;
  name: string;
  path: string;
}

export interface DocumentItem
{
  id: string;
  fileName: string;
  contentType: string;
  status: string;
  isOwner: boolean;
  sharedTeams?: DocumentAccessTeam[] | null;
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

export function isSupportedDocumentFileName(
  fileName: string):
  boolean
{
  return /\.(pdf|docx|txt)$/i.test(
    fileName.trim());
}

export function buildDocumentsQueryString(
  query: DocumentsQuery):
  string
{
  const parameters =
    new URLSearchParams();

  parameters.set(
    'page',
    query.page.toString());
  parameters.set(
    'pageSize',
    query.pageSize.toString());
  parameters.set(
    'scope',
    query.scope);

  if (
    query.scope === 'team' &&
    query.teamId)
  {
    parameters.set(
      'teamId',
      query.teamId);

    if (query.includeDescendants)
    {
      parameters.set(
        'includeDescendants',
        'true');
    }
  }

  const filenameQuery =
    query.query?.trim();

  if (filenameQuery)
  {
    parameters.set(
      'query',
      filenameQuery);
  }

  return parameters.toString();
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
    options: Partial<DocumentsQuery> = {}):
    Observable<DocumentsPageResponse>
  {
    const query: DocumentsQuery = {
      page: options.page ?? 1,
      pageSize: options.pageSize ?? 20,
      scope: options.scope ?? 'all',
      teamId: options.teamId,
      includeDescendants:
        options.includeDescendants ?? false,
      query: options.query
    };

    return this.http.get<DocumentsPageResponse>(
      `${apiBaseUrl}/api/documents?${buildDocumentsQueryString(query)}`);
  }

  uploadDocument(
    file: File,
    teamId?: string):
    Observable<DocumentItem>
  {
    const formData =
      new FormData();

    formData.append(
      'File',
      file);

    if (teamId)
    {
      formData.append(
        'TeamId',
        teamId);
    }

    return this.http.post<DocumentItem>(
      `${apiBaseUrl}/api/documents`,
      formData);
  }

  deleteDocument(
    documentId: string):
    Observable<void>
  {
    return this.http.delete<void>(
      `${apiBaseUrl}/api/documents/${documentId}`);
  }

  downloadDocument(
    documentId: string):
    Observable<Blob>
  {
    return this.http.get(
      `${apiBaseUrl}/api/documents/${documentId}/download`,
      {
        responseType: 'blob'
      });
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
