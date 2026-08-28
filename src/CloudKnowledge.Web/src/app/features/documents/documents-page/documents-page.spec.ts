import {
  of
} from 'rxjs';

import {
  DocumentsPage
} from './documents-page';

describe('DocumentsPage multi-file upload', () => {
  it('keeps every supported file selected from the picker', () => {
    const page =
      createPage();

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

    page.onFileSelected({
      target: {
        files: [
          firstFile,
          secondFile
        ],
        value: 'selected'
      }
    } as unknown as Event);

    expect(page.selectedFiles)
      .toEqual([
        firstFile,
        secondFile
      ]);
  });

  it('uploads the whole batch with the selected team and refreshes once', () => {
    const uploadCalls: Array<{
      files: readonly File[];
      teamId?: string;
    }> = [];

    let loadCalls = 0;

    const documentsService = {
      uploadDocuments:
        (files: readonly File[], teamId?: string) =>
        {
          uploadCalls.push({
            files,
            teamId
          });

          return of(
            files.map(
              file =>
                ({
                  fileName: file.name,
                  succeeded: true
                })));
        },
      getDocuments: () =>
      {
        loadCalls++;

        return of({
          items: [],
          page: 1,
          pageSize: 20,
          totalCount: 0,
          totalPages: 0
        });
      }
    };

    const page =
      createPage(documentsService);

    const files = [
      new File(['first'], 'first.pdf'),
      new File(['second'], 'second.docx')
    ];

    page.selectedFiles = files;
    page.selectedTeamId = 'team-dota';

    page.upload();

    expect(uploadCalls)
      .toEqual([
        {
          files,
          teamId: 'team-dota'
        }
      ]);

    expect(loadCalls).toBe(1);
    expect(page.selectedFiles).toEqual([]);
  });
});

describe('DocumentsPage pagination', () => {
  it('shows every page when the result set is small', () => {
    const page =
      createPage();

    page.page = 3;
    page.totalPages = 5;

    expect(page.paginationItems)
      .toEqual([
        1,
        2,
        3,
        4,
        5
      ]);
  });

  it('keeps first last and a bounded window around the current page', () => {
    const page =
      createPage();

    page.page = 9;
    page.totalPages = 18;

    expect(page.paginationItems)
      .toEqual([
        1,
        null,
        7,
        8,
        9,
        10,
        11,
        null,
        18
      ]);
  });

  it('loads a directly selected page once', () => {
    let loadCalls = 0;

    const documentsService = {
      getDocuments: () =>
      {
        loadCalls++;

        return of({
          items: [],
          page: 4,
          pageSize: 20,
          totalCount: 200,
          totalPages: 10
        });
      }
    };

    const page =
      createPage(documentsService);

    page.page = 2;
    page.totalPages = 10;

    page.goToPage(4);

    expect(loadCalls).toBe(1);
    expect(page.page).toBe(4);
  });
});

function createPage(
  documentsService: unknown =
    {
      getDocuments: () =>
        of({
          items: [],
          page: 1,
          pageSize: 20,
          totalCount: 0,
          totalPages: 0
        })
    }):
  DocumentsPage
{
  const teamsService = {};

  const cdr = {
    detectChanges: () => undefined
  };

  return new DocumentsPage(
    documentsService as never,
    teamsService as never,
    cdr as never);
}
