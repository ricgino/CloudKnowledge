import {
  buildDocumentsQueryString
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
