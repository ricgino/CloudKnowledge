import {
  Injectable
} from '@angular/core';

import {
  HttpClient
} from '@angular/common/http';

import {
  catchError,
  concatMap,
  from,
  map,
  Observable,
  of,
  toArray
} from 'rxjs';

import {
  apiBaseUrl
} from '../../auth-config';

import {
  KnowledgeRetrievalScope
} from '../knowledge/knowledge-scope';

export type DocumentListScope =
  'all' |
  'owned' |
  'team';

export type RetrievalQueryKind =
  'original' |
  'focused';

export type HybridRetrievalChannel =
  'semantic' |
  'lexical' |
  'both';

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
  canDelete: boolean;
  sharedTeams?: DocumentAccessTeam[] | null;
}

export interface DocumentUploadOutcome
{
  fileName: string;
  succeeded: boolean;
  document?: DocumentItem;
  errorStatus?: number;
  errorMessage?: string;
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

export interface AskDocumentSource
{
  label: string;
  documentId: string;
  chunkId: string;
  position: number;
  content: string;
  similarity: number | null;
}

export interface AskRetrievalCandidate
{
  documentId: string;
  chunkId: string;
  rank?: number | null;
  semanticRank?: number | null;
  lexicalRank?: number | null;
  fusedScore?: number | null;
  adjustedFusedScore?: number | null;
  channel?: HybridRetrievalChannel | null;
  navigationPenalty?: boolean | null;
  selected?: boolean | null;
}

export interface AskRetrievalQueryDiagnostics
{
  kind: RetrievalQueryKind;
  query: string;
  semanticCandidates: AskRetrievalCandidate[];
  lexicalCandidates: AskRetrievalCandidate[];
  hybridCandidates: AskRetrievalCandidate[];
}

export interface AskDocumentsResponse
{
  answer: string;
  sources: AskDocumentSource[];
  retrievalQueries: string[];
  retrievalDiagnostics?: AskRetrievalQueryDiagnostics[];
}

export function isSupportedDocumentFileName(
  fileName: string):
  boolean
{
  return /\.(pdf|docx|txt)$/i.test(
    fileName.trim());
}

export function buildUploadSuccessMessage(
  fileName: string,
  teamName?: string):
  string
{
  return teamName
    ? `${fileName} uploaded as a team-owned document in ${teamName}. Processing continues in the background.`
    : `${fileName} uploaded as a personal document. Processing continues in the background.`;
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

  uploadDocuments(
    files: readonly File[],
    teamId?: string):
    Observable<DocumentUploadOutcome[]>
  {
    return from(files)
      .pipe(
        concatMap(
          file =>
            this.uploadDocument(
              file,
              teamId)
              .pipe(
                map(
                  document =>
                    ({
                      fileName: file.name,
                      succeeded: true,
                      document
                    }) satisfies DocumentUploadOutcome),
                catchError(
                  error =>
                    of(
                      ({
                        fileName: file.name,
                        succeeded: false,
                        errorStatus: error?.status,
                        errorMessage: error?.error?.message
                      }) satisfies DocumentUploadOutcome))
              )),
        toArray());
  }

  deleteDocument(
    documentId: string):
    Observable<void>
  {
    return this.http.delete<void>(
      `${apiBaseUrl}/api/documents/${documentId}`);
  }

  retryDocument(
    documentId: string):
    Observable<void>
  {
    return this.http.post<void>(
      `${apiBaseUrl}/api/documents/${documentId}/retry`,
      null);
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
    take = 5,
    scope?: KnowledgeRetrievalScope):
    Observable<SearchDocumentResult[]>
  {
    const retrievalScope: KnowledgeRetrievalScope =
      scope ?? {
        scope: 'all',
        teamId: null,
        includeDescendants: false
      };

    return this.http.post<SearchDocumentResult[]>(
      `${apiBaseUrl}/api/search`,
      {
        query,
        take,
        ...retrievalScope
      });
  }

  ask(
    question: string,
    take = 5,
    scope?: KnowledgeRetrievalScope):
    Observable<AskDocumentsResponse>
  {
    const retrievalScope: KnowledgeRetrievalScope =
      scope ?? {
        scope: 'all',
        teamId: null,
        includeDescendants: false
      };

    return this.http.post<AskDocumentsResponse>(
      `${apiBaseUrl}/api/ask`,
      {
        question,
        take,
        ...retrievalScope
      });
  }
}
