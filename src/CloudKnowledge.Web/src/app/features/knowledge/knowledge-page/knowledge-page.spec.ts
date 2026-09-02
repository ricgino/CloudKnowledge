import {
  TestBed
} from '@angular/core/testing';

import {
  of
} from 'rxjs';

import {
  describe,
  expect,
  it,
  vi
} from 'vitest';

import {
  Documents
} from '../../documents/documents';

import {
  Teams
} from '../../teams/teams';

import {
  KnowledgePage
} from './knowledge-page';

describe(
  'KnowledgePage hybrid retrieval diagnostics',
  () =>
  {
    it(
      'renders retrieval channels and does not invent similarity for lexical-only sources',
      async () =>
      {
        const documentsService = {
          getDocuments: vi.fn().mockReturnValue(
            of({
              items: [],
              page: 1,
              pageSize: 20,
              totalCount: 0,
              totalPages: 0
            })),
          ask: vi.fn().mockReturnValue(
            of({
              answer: 'Grounded answer.',
              sources: [
                {
                  label: 'S1',
                  documentId: '11111111-1111-1111-1111-111111111111',
                  chunkId: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
                  position: 0,
                  content: 'Lexical-only technical evidence.',
                  similarity: null
                }
              ],
              retrievalQueries: [
                'rated output current altitude derating'
              ],
              retrievalDiagnostics: [
                {
                  kind: 'original',
                  query: 'rated output current altitude derating',
                  semanticCandidates: [
                    {
                      documentId: '11111111-1111-1111-1111-111111111111',
                      chunkId: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
                      rank: 1
                    }
                  ],
                  lexicalCandidates: [
                    {
                      documentId: '11111111-1111-1111-1111-111111111111',
                      chunkId: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
                      rank: 1
                    }
                  ],
                  hybridCandidates: [
                    {
                      documentId: '11111111-1111-1111-1111-111111111111',
                      chunkId: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
                      semanticRank: 1,
                      lexicalRank: null,
                      fusedScore: 0.016,
                      adjustedFusedScore: 0.016,
                      channel: 'semantic',
                      navigationPenalty: false,
                      selected: true
                    },
                    {
                      documentId: '11111111-1111-1111-1111-111111111111',
                      chunkId: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
                      semanticRank: null,
                      lexicalRank: 1,
                      fusedScore: 0.015,
                      adjustedFusedScore: 0.012,
                      channel: 'lexical',
                      navigationPenalty: true,
                      selected: true
                    },
                    {
                      documentId: '22222222-2222-2222-2222-222222222222',
                      chunkId: 'cccccccc-cccc-cccc-cccc-cccccccccccc',
                      semanticRank: 2,
                      lexicalRank: 2,
                      fusedScore: 0.03,
                      adjustedFusedScore: 0.03,
                      channel: 'both',
                      navigationPenalty: false,
                      selected: false
                    }
                  ]
                }
              ]
            })),
          downloadDocument: vi.fn()
        };

        const teamsService = {
          getTeams: vi.fn().mockReturnValue(
            of([]))
        };

        await TestBed.configureTestingModule({
          declarations: [
            KnowledgePage
          ],
          providers: [
            {
              provide: Documents,
              useValue: documentsService
            },
            {
              provide: Teams,
              useValue: teamsService
            }
          ]
        }).compileComponents();

        const fixture =
          TestBed.createComponent(
            KnowledgePage);

        const component =
          fixture.componentInstance;

        fixture.detectChanges();

        component.question =
          'What are the technical limits?';

        component.ask();
        fixture.detectChanges();

        const text =
          fixture.nativeElement.textContent as string;

        expect(text).toContain(
          'Semantic');
        expect(text).toContain(
          'Lexical');
        expect(text).toContain(
          'Both');
        expect(text).toContain(
          'Navigation penalty');
        expect(text).not.toContain(
          '0.0%');
      });
  });
