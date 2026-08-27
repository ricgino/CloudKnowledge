import {
  ChangeDetectorRef,
  Component,
  OnDestroy,
  OnInit
} from '@angular/core';

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

type AppSection =
  'knowledge' |
  'documents' |
  'teams' |
  'administration';

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

  private readonly destroy$ =
    new Subject<void>();

  constructor(
    private readonly auth: MsalService,
    private readonly broadcast: MsalBroadcastService,
    private readonly cdr: ChangeDetectorRef)
  {
  }

  ngOnInit(): void
  {
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

  selectSection(
    section: AppSection):
    void
  {
    this.activeSection = section;
  }

  login(): void
  {
    this.auth.loginRedirect(
      loginRequest);
  }

  logout(): void
  {
    this.auth.logoutRedirect();
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
  }

  ngOnDestroy(): void
  {
    this.destroy$.next();
    this.destroy$.complete();
  }
}
