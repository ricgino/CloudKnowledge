import {
  of,
  Subject,
  throwError
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

  it('uploads a selected batch sequentially with the same team access', () => {
    const firstResponse = new Subject<unknown>();
    const secondResponse = new Subject<unknown>();
    const requestBodies: FormData[] = [];

    const http = {
      post: (_url: string, body: FormData) =>
      {
        requestBodies.push(body);

        return requestBodies.length === 1
          ? firstResponse
          : secondResponse;
      }
    };

    const documents =
      new Documents(http as never);

    const firstFile =
      new File(
        ['first'],
        'first.pdf',
        { type: 'application/pdf' });

    const secondFile =
      new File(
        ['second'],
        'second.txt',
        { type: 'text/plain' });

    documents
      .uploadDocuments(
        [firstFile, secondFile],
        'team-dota')
      .subscribe();

    expect(requestBodies.length).toBe(1);
    expect(requestBodies[0].get('File')).toBe(firstFile);
    expect(requestBodies[0].get('TeamId')).toBe('team-dota');

    firstResponse.next({ id: 'first' });
    firstResponse.complete();

    expect(requestBodies.length).toBe(2);
    expect(requestBodies[1].get('File')).toBe(secondFile);
    expect(requestBodies[1].get('TeamId')).toBe('team-dota');
  });

  it('continues the batch after an upload failure and reports each outcome', () => {
    const attemptedFiles: string[] = [];

    const http = {
      post: (_url: string, body: FormData) =>
      {
        const file = body.get('File') as File;
        attemptedFiles.push(file.name);

        return file.name === 'broken.pdf'
          ? throwError(() => ({ status: 500 }))
          : of({
              id: 'ok',
              fileName: file.name,
              contentType: file.type,
              status: 'Pending',
              isOwner: true
            });
      }
    };

    const documents =
      new Documents(http as never);

    const results: unknown[] = [];

    documents
      .uploadDocuments(
        [
          new File(['bad'], 'broken.pdf'),
          new File(['good'], 'good.docx')
        ],
        'team-dota')
      .subscribe(result => results.push(result));

    expect(attemptedFiles)
      .toEqual([
        'broken.pdf',
        'good.docx'
      ]);

    expect(results)
      .toEqual([
        [
          expect.objectContaining({
            fileName: 'broken.pdf',
            succeeded: false
          }),
          expect.objectContaining({
            fileName: 'good.docx',
            succeeded: true
          })
        ]
      ]);
  });
});
