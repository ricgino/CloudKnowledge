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
  NotificationItem,
  Notifications
} from './features/notifications/notifications';

describe('App', () => {
  const msalSubject$ =
    new Subject<never>();

  let markedNotificationIds: string[] = [];

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
    markRead: (notificationId: string) =>
    {
      markedNotificationIds.push(notificationId);
      return of(undefined);
    },
    startRealtime: () => undefined,
    stopRealtime: () => undefined
  };

  beforeEach(async () => {
    markedNotificationIds = [];

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

  it('marks currently visible unread notifications as read when the panel opens', () => {
    const fixture =
      TestBed.createComponent(App);

    const app =
      fixture.componentInstance;

    const unread: NotificationItem = {
      id: 'notification-unread',
      type: 'document-ready',
      title: 'Document ready',
      message: 'architecture.pdf is ready.',
      target: 'documents',
      createdAtUtc: '2026-08-27T20:00:00Z',
      isRead: false
    };

    const alreadyRead: NotificationItem = {
      ...unread,
      id: 'notification-read',
      isRead: true
    };

    app.notifications = [
      unread,
      alreadyRead
    ];

    app.toggleNotifications();

    expect(app.notificationsOpen).toBe(true);
    expect(markedNotificationIds)
      .toEqual([
        'notification-unread'
      ]);
    expect(app.unreadNotificationCount)
      .toBe(0);
    expect(
      app.notifications.find(item =>
        item.id === 'notification-unread')
        ?.isRead)
      .toBe(true);
  });
});
