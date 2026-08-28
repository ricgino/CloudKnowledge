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
