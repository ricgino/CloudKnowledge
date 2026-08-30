import {
  ChangeDetectorRef,
  Component,
  OnDestroy,
  OnInit
} from '@angular/core';

import {
  HttpClient
} from '@angular/common/http';

import {
  EventMessage,
  EventType,
  InteractionStatus
} from '@azure/msal-browser';

import {
  MsalBroadcastService,
  MsalService
} from '@azure/msal-angular';

import {
  Subject,
  filter,
  takeUntil
} from 'rxjs';

import {
  loginRequest
} from './auth-config';

import {
  NotificationItem,
  Notifications
} from './features/notifications/notifications';

type AppSection =
  'knowledge' |
  'documents' |
  'teams' |
  'administration';

type VersionResponse = {
  version: string;
};

@Component({
  selector: 'app-root',
  templateUrl: './app.html',
  styleUrl: './app.scss',
  standalone: false
})
export class App
  implements OnInit, OnDestroy
{
  loggedIn = false;
  activeSection: AppSection = 'knowledge';
  accountName = '';
  accountUsername = '';
  appVersion = 'dev/local';

  notifications: NotificationItem[] = [];
  notificationsOpen = false;
  notificationsLoading = false;
  notificationError = '';

  private notificationsStarted = false;

  private readonly destroy$ =
    new Subject<void>();

  constructor(
    private readonly auth: MsalService,
    private readonly broadcast: MsalBroadcastService,
    private readonly notificationsService: Notifications,
    private readonly http: HttpClient,
    private readonly cdr: ChangeDetectorRef)
  {
  }

  ngOnInit(): void
  {
    this.loadAppVersion();

    if (
      window.location.pathname ===
      '/redirect')
    {
      return;
    }

    this.auth
      .handleRedirectObservable()
      .subscribe({
        next: result =>
        {
          if (result?.account)
          {
            this.auth.instance
              .setActiveAccount(
                result.account);
          }

          this.updateLoginState();
          this.cdr.detectChanges();
        },
        error: error =>
        {
          console.error(
            'MSAL redirect error:',
            error);
        }
      });

    this.broadcast.msalSubject$
      .pipe(
        filter(
          (message: EventMessage) =>
            message.eventType ===
              EventType.LOGIN_SUCCESS ||
            message.eventType ===
              EventType.LOGOUT_SUCCESS),
        takeUntil(
          this.destroy$))
      .subscribe(
        () =>
        {
          this.updateLoginState();
          this.cdr.detectChanges();
        });

    this.broadcast.inProgress$
      .pipe(
        filter(
          status =>
            status ===
            InteractionStatus.None),
        takeUntil(
          this.destroy$))
      .subscribe(
        () =>
        {
          this.updateLoginState();
          this.cdr.detectChanges();
        });
  }

  get shortAppVersion(): string
  {
    return /^[0-9a-f]{40}$/i.test(
      this.appVersion)
      ? this.appVersion.slice(0, 8)
      : this.appVersion;
  }

  get unreadNotificationCount(): number
  {
    return this.notifications
      .filter(notification =>
        !notification.isRead)
      .length;
  }

  selectSection(
    section: AppSection):
    void
  {
    this.activeSection = section;
    this.notificationsOpen = false;
  }

  toggleNotifications(): void
  {
    const opening =
      !this.notificationsOpen;

    this.notificationsOpen =
      opening;

    if (opening)
    {
      this.markVisibleNotificationsRead();
    }
  }

  openNotification(
    notification: NotificationItem):
    void
  {
    if (!notification.isRead)
    {
      this.notificationsService
        .markRead(
          notification.id)
        .subscribe({
          next: () =>
          {
            this.notifications =
              this.notifications.map(item =>
                item.id === notification.id
                  ? {
                      ...item,
                      isRead: true
                    }
                  : item);

            this.cdr.detectChanges();
          },
          error: error =>
          {
            console.warn(
              'Unable to mark notification as read.',
              error);
          }
        });
    }

    if (notification.target ===
        'documents')
    {
      this.activeSection =
        'documents';
    }

    this.notificationsOpen = false;
  }

  login(): void
  {
    this.auth.loginRedirect(
      loginRequest);
  }

  logout(): void
  {
    this.shutdownNotifications();
    this.auth.logoutRedirect();
  }

  private loadAppVersion(): void
  {
    this.http
      .get<VersionResponse>(
        '/version')
      .subscribe({
        next: response =>
        {
          if (
            response &&
            typeof response.version === 'string' &&
            response.version.trim().length > 0)
          {
            this.appVersion =
              response.version.trim();

            this.cdr.detectChanges();
          }
        },
        error: error =>
        {
          console.warn(
            'Unable to load application version; using local fallback.',
            error);
        }
      });
  }

  private updateLoginState(): void
  {
    const accounts =
      this.auth.instance
        .getAllAccounts();

    if (
      accounts.length > 0 &&
      !this.auth.instance
        .getActiveAccount())
    {
      this.auth.instance
        .setActiveAccount(
          accounts[0]);
    }

    const activeAccount =
      this.auth.instance
        .getActiveAccount();

    this.loggedIn =
      accounts.length > 0;

    this.accountName =
      activeAccount?.name ??
      activeAccount?.username ??
      '';

    this.accountUsername =
      activeAccount?.username ??
      '';

    if (this.loggedIn)
    {
      this.initializeNotifications();
    }
    else
    {
      this.shutdownNotifications();
    }
  }

  private initializeNotifications(): void
  {
    if (this.notificationsStarted)
    {
      return;
    }

    this.notificationsStarted = true;
    this.notificationsLoading = true;
    this.notificationError = '';

    this.notificationsService
      .getNotifications()
      .subscribe({
        next: notifications =>
        {
          this.notifications = notifications;
          this.notificationsLoading = false;
          this.cdr.detectChanges();
        },
        error: error =>
        {
          this.notificationsLoading = false;
          this.notificationError =
            `Unable to load notifications (HTTP ${error.status}).`;
          this.cdr.detectChanges();
        }
      });

    this.notificationsService.startRealtime(
      notification =>
      {
        if (this.notifications.some(item =>
            item.id === notification.id))
        {
          return;
        }

        this.notifications = [
          notification,
          ...this.notifications
        ].slice(
          0,
          20);

        this.cdr.detectChanges();
      });
  }

  private markVisibleNotificationsRead(): void
  {
    const unreadIds =
      this.notifications
        .filter(notification =>
          !notification.isRead)
        .map(notification =>
          notification.id);

    if (unreadIds.length === 0)
    {
      return;
    }

    const unreadIdSet =
      new Set(unreadIds);

    this.notifications =
      this.notifications.map(notification =>
        unreadIdSet.has(notification.id)
          ? {
              ...notification,
              isRead: true
            }
          : notification);

    this.cdr.detectChanges();

    for (const notificationId of unreadIds)
    {
      this.notificationsService
        .markRead(notificationId)
        .subscribe({
          error: error =>
          {
            this.notifications =
              this.notifications.map(notification =>
                notification.id === notificationId
                  ? {
                      ...notification,
                      isRead: false
                    }
                  : notification);

            console.warn(
              'Unable to mark notification as read.',
              error);

            this.cdr.detectChanges();
          }
        });
    }
  }

  private shutdownNotifications(): void
  {
    if (!this.notificationsStarted)
    {
      return;
    }

    this.notificationsStarted = false;
    this.notificationsOpen = false;
    this.notifications = [];
    this.notificationError = '';
    this.notificationsService.stopRealtime();
  }

  ngOnDestroy(): void
  {
    this.shutdownNotifications();
    this.destroy$.next();
    this.destroy$.complete();
  }
}
