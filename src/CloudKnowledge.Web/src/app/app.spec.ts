import {
  TestBed
} from '@angular/core/testing';

import {
  RouterModule
} from '@angular/router';

import {
  MsalBroadcastService,
  MsalService
} from '@azure/msal-angular';

import {
  InteractionStatus
} from '@azure/msal-browser';

import {
  of,
  Subject
} from 'rxjs';

import {
  App
} from './app';

import {
  Notifications
} from './features/notifications/notifications';

describe('App', () => {
  const msalSubject$ =
    new Subject<never>();

  const authMock = {
    handleRedirectObservable: () =>
      of(null),
    loginRedirect: () => undefined,
    logoutRedirect: () => undefined,
    instance: {
      getAllAccounts: () => [],
      getActiveAccount: () => null,
      setActiveAccount: () => undefined
    }
  };

  const broadcastMock = {
    msalSubject$,
    inProgress$:
      of(InteractionStatus.None)
  };

  const notificationsMock = {
    getNotifications: () =>
      of([]),
    markRead: () =>
      of(undefined),
    startRealtime: () => undefined,
    stopRealtime: () => undefined
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [
        RouterModule.forRoot([])
      ],
      declarations: [
        App
      ],
      providers: [
        {
          provide: MsalService,
          useValue: authMock
        },
        {
          provide: MsalBroadcastService,
          useValue: broadcastMock
        },
        {
          provide: Notifications,
          useValue: notificationsMock
        }
      ]
    })
      .compileComponents();
  });

  it('should create the app', () => {
    const fixture =
      TestBed.createComponent(App);

    fixture.detectChanges();

    expect(
      fixture.componentInstance)
      .toBeTruthy();
  });

  it('should render the signed-out landing page', () => {
    const fixture =
      TestBed.createComponent(App);

    fixture.detectChanges();

    const compiled =
      fixture.nativeElement as HTMLElement;

    expect(
      compiled.querySelector('h1')
        ?.textContent)
      .toContain(
        'Private knowledge, securely searchable.');
  });
});
