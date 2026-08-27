import {
  of
} from 'rxjs';

import {
  buildDocumentsQueryString,
  buildUploadSuccessMessage,
  Documents,
  isSupportedDocumentFileName
} from './documents';

describe('document library filters', () => {
  it('serializes team branch and filename search server-side', () => {
    const query =
      buildDocumentsQueryString({
        page: 2,
        pageSize: 10,
        scope: 'team',
        teamId: 'team-123',
        includeDescendants: true,
        query: '  handbook  '
      });

    expect(query)
      .toBe(
        'page=2&pageSize=10&scope=team&teamId=team-123&includeDescendants=true&query=handbook');
  });

  it('omits team-only parameters for owned documents', () => {
    const query =
      buildDocumentsQueryString({
        page: 1,
        pageSize: 20,
        scope: 'owned'
      });

    expect(query)
      .toBe(
        'page=1&pageSize=20&scope=owned');
  });
});

describe('knowledge request scope', () => {
  it('serializes the same selected team scope for search and ask', () => {
    const requests: unknown[] = [];

    const http = {
      post: (_url: string, body: unknown) =>
      {
        requests.push(body);
        return of([]);
      }
    };

    const documents =
      new Documents(http as never);

    const scope = {
      scope: 'team' as const,
      teamId: 'desk-sharing',
      includeDescendants: true
    };

    documents
      .search(
        'architecture',
        5,
        scope)
      .subscribe();

    documents
      .ask(
        'What is the architecture?',
        5,
        scope)
      .subscribe();

    expect(requests)
      .toEqual([
        {
          query: 'architecture',
          take: 5,
          scope: 'team',
          teamId: 'desk-sharing',
          includeDescendants: true
        },
        {
          question: 'What is the architecture?',
          take: 5,
          scope: 'team',
          teamId: 'desk-sharing',
          includeDescendants: true
        }
      ]);
  });

  it('defaults omitted search and ask scope to all accessible knowledge', () => {
    const requests: unknown[] = [];

    const http = {
      post: (_url: string, body: unknown) =>
      {
        requests.push(body);
        return of([]);
      }
    };

    const documents =
      new Documents(http as never);

    documents.search('architecture').subscribe();
    documents.ask('What is the architecture?').subscribe();

    expect(requests)
      .toEqual([
        {
          query: 'architecture',
          take: 5,
          scope: 'all',
          teamId: null,
          includeDescendants: false
        },
        {
          question: 'What is the architecture?',
          take: 5,
          scope: 'all',
          teamId: null,
          includeDescendants: false
        }
      ]);
  });
});

describe('document upload formats', () => {
  it('accepts pdf, docx and txt case-insensitively', () => {
    expect(isSupportedDocumentFileName('manual.pdf')).toBe(true);
    expect(isSupportedDocumentFileName('handbook.DOCX')).toBe(true);
    expect(isSupportedDocumentFileName('notes.Txt')).toBe(true);
  });

  it('rejects unsupported extensions', () => {
    expect(isSupportedDocumentFileName('archive.zip')).toBe(false);
    expect(isSupportedDocumentFileName('legacy.doc')).toBe(false);
    expect(isSupportedDocumentFileName('payload.exe')).toBe(false);
  });

  it('describes personal and team ownership accurately after upload', () => {
    expect(
      buildUploadSuccessMessage(
        'guide.pdf'))
      .toBe(
        'guide.pdf uploaded as a personal document. Processing continues in the background.');

    expect(
      buildUploadSuccessMessage(
        'guide.pdf',
        'Engineering'))
      .toBe(
        'guide.pdf uploaded as a team-owned document in Engineering. Processing continues in the background.');
  });
});
