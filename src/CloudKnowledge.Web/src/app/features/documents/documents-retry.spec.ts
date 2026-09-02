import {
  Observable,
  of
} from 'rxjs';

import {
  Documents
} from './documents';

describe('document retry', () => {
  it('posts a retry request for one document', () => {
    const requests: Array<{
      url: string;
      body: unknown;
    }> = [];

    const http = {
      post: (url: string, body: unknown) =>
      {
        requests.push({
          url,
          body
        });

        return of(undefined);
      }
    };

    const documents =
      new Documents(
        http as never);

    const retryDocument =
      (documents as unknown as {
        retryDocument?: (
          documentId: string) =>
          Observable<void>;
      }).retryDocument;

    expect(retryDocument)
      .toBeDefined();

    if (!retryDocument)
    {
      return;
    }

    retryDocument
      .call(
        documents,
        'document-123')
      .subscribe();

    expect(requests.length)
      .toBe(1);
    expect(requests[0].url)
      .toContain(
        '/api/documents/document-123/retry');
    expect(requests[0].body)
      .toBeNull();
  });
});
