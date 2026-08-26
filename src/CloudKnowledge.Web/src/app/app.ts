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

    this.loggedIn =
      accounts.length > 0;
  }

  ngOnDestroy(): void
  {
    this.destroy$.next();
    this.destroy$.complete();
  }
}
