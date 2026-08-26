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
}
