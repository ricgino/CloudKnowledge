import {
  TestBed
} from '@angular/core/testing';

import {
  of
} from 'rxjs';

import {
  Documents
} from '../../documents/documents';

import {
  Teams
} from '../../teams/teams';

import {
  AdministrationPage
} from './administration-page';

describe('AdministrationPage form containment', () => {
  beforeEach(async () => {
    await TestBed
      .configureTestingModule({
        declarations: [
          AdministrationPage
        ],
        providers: [
          {
            provide: Teams,
            useValue: {
              getTeams: () => of([])
            }
          },
          {
            provide: Documents,
            useValue: {
              getDocuments: () =>
                of({
                  items: [],
                  page: 1,
                  pageSize: 20,
                  totalCount: 0,
                  totalPages: 0
                })
            }
          }
        ]
      })
      .compileComponents();
  });

  it('keeps user email within its form grid cell', () => {
    const fixture =
      TestBed.createComponent(
        AdministrationPage);

    fixture.detectChanges();

    const input =
      fixture.nativeElement.querySelector(
        'input[type="email"]') as HTMLInputElement;

    const label =
      input.closest('label') as HTMLLabelElement;

    expect(getComputedStyle(input).boxSizing)
      .toBe('border-box');

    expect(getComputedStyle(label).minWidth)
      .toBe('0px');
  });

  it('keeps team name and parent controls within their form grid cells', () => {
    const fixture =
      TestBed.createComponent(
        AdministrationPage);

    fixture.componentInstance.selectTab('teams');
    fixture.detectChanges();

    const teamNameInput =
      fixture.nativeElement.querySelector(
        'input[type="text"]') as HTMLInputElement;

    const parentSelect =
      fixture.nativeElement.querySelector(
        '.form-grid select') as HTMLSelectElement;

    expect(getComputedStyle(teamNameInput).boxSizing)
      .toBe('border-box');

    expect(getComputedStyle(parentSelect).boxSizing)
      .toBe('border-box');

    expect(
      getComputedStyle(
        teamNameInput.closest('label') as HTMLLabelElement)
        .minWidth)
      .toBe('0px');

    expect(
      getComputedStyle(
        parentSelect.closest('label') as HTMLLabelElement)
        .minWidth)
      .toBe('0px');
  });
});
