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
  apiBaseUrl,
  apiScope,
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
  loggedIn =
    false;

  apiResult =
    '';

  tokenClaims:
    Record<string, unknown> | null =
    null;

  private readonly destroy$ =
    new Subject<void>();

  constructor(
    private readonly auth:
      MsalService,

    private readonly broadcast:
      MsalBroadcastService,

    private readonly http:
      HttpClient,

    private readonly cdr:
      ChangeDetectorRef)
  {
  }

  ngOnInit():
    void
  {
    // /redirect deve essere gestita solo dalla redirect bridge.
    if (
      window.location.pathname ===
      '/redirect')
    {
      return;
    }

    this.auth
      .handleRedirectObservable()
      .subscribe({
        next:
          result =>
          {
            if (result?.account)
            {
              this.auth.instance
                .setActiveAccount(
                  result.account);
            }

            this.updateLoginState();

            console.log(
              'MSAL redirect handled. Accounts:',
              this.auth.instance
                .getAllAccounts()
                .length);

            this.cdr.detectChanges();
          },

        error:
          error =>
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

  login():
    void
  {
    this.auth.loginRedirect(
      loginRequest);
  }

  logout():
    void
  {
    this.auth.logoutRedirect();
  }

  loadDocuments():
    void
  {
    this.http
      .get(
        `${apiBaseUrl}/api/documents?page=1&pageSize=20`)
      .subscribe({
        next:
          result =>
          {
            this.apiResult =
              JSON.stringify(
                result,
                null,
                2);
          },

        error:
          error =>
          {
            this.apiResult =
              `HTTP ${error.status}\n` +
              JSON.stringify(
                error.error,
                null,
                2);
          }
      });
  }

  createTestTeam():
    void
  {
    this.http
      .post(
        `${apiBaseUrl}/api/teams`,
        {
          name:
            'Engineering'
        })
      .subscribe({
        next:
          result =>
          {
            this.apiResult =
              JSON.stringify(
                result,
                null,
                2);
          },

        error:
          error =>
          {
            this.apiResult =
              `HTTP ${error.status}\n` +
              JSON.stringify(
                error.error,
                null,
                2);
          }
      });
  }

  addTestMember():
    void
  {
    this.http
      .post(
        `${apiBaseUrl}/api/teams/08446f1f-678d-4057-946c-a2df7637ea9a/members`,
        {
          email:
            'rgino33@gmail.com'
        })
      .subscribe({
        next:
          result =>
          {
            this.apiResult =
              JSON.stringify(
                result,
                null,
                2);
          },

        error:
          error =>
          {
            this.apiResult =
              `HTTP ${error.status}\n` +
              JSON.stringify(
                error.error,
                null,
                2);
          }
      });
  }

  showAccessTokenClaims():
    void
  {
    const account =
      this.auth.instance
        .getActiveAccount();

    if (!account)
    {
      return;
    }

    this.auth
      .acquireTokenSilent({
        account,
        scopes: [
          apiScope
        ]
      })
      .subscribe({
        next:
          result =>
          {
            const payload =
              this.decodeJwtPayload(
                result.accessToken);

            this.tokenClaims =
            {
              iss:
                payload['iss'],

              sub:
                payload['sub'],

              aud:
                payload['aud'],

              scp:
                payload['scp'],

              tid:
                payload['tid'],

              email:
                payload['email'],

              preferred_username:
                payload['preferred_username'],

              name:
                payload['name']
            };

            this.cdr.detectChanges();
          },

        error:
          error =>
          {
            console.error(
              'Token acquisition error:',
              error);
          }
      });
  }

  private updateLoginState():
    void
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

  private decodeJwtPayload(
    token: string):
    Record<string, unknown>
  {
    const payload =
      token.split('.')[1];

    const normalized =
      payload
        .replace(/-/g, '+')
        .replace(/_/g, '/');

    const padded =
      normalized.padEnd(
        Math.ceil(
          normalized.length / 4) * 4,
        '=');

    return JSON.parse(
      atob(
        padded));
  }

  ngOnDestroy():
    void
  {
    this.destroy$.next();
    this.destroy$.complete();
  }

  selectedFile:
    File | null =
    null;

  onFileSelected(
    event: Event):
    void
  {
    const input =
      event.target as HTMLInputElement;

    this.selectedFile =
      input.files?.[0] ?? null;
  }

  uploadDocument():
    void
  {
    if (!this.selectedFile)
    {
      this.apiResult =
        'Select a file first.';

      return;
    }

    const formData =
      new FormData();

    formData.append(
      'File',
      this.selectedFile);

    this.http
      .post(
        `${apiBaseUrl}/api/documents`,
        formData)
      .subscribe({
        next:
          result =>
          {
            this.apiResult =
              JSON.stringify(
                result,
                null,
                2);
          },

        error:
          error =>
          {
            this.apiResult =
              `HTTP ${error.status}\n` +
              JSON.stringify(
                error.error,
                null,
                2);
          }
      });
  }  

  shareTestDocument():
    void
  {
    const documentId =
      'ab01e593-05d5-4fc9-bae8-ca3d50d8ab1b';

    const teamId =
      '08446f1f-678d-4057-946c-a2df7637ea9a';

    this.http
      .put(
        `${apiBaseUrl}/api/documents/${documentId}/teams/${teamId}`,
        null,
        {
          observe:
            'response'
        })
      .subscribe({
        next:
          response =>
          {
            this.apiResult =
              `HTTP ${response.status}\n` +
              'Document shared with Engineering.';
          },

        error:
          error =>
          {
            this.apiResult =
              `HTTP ${error.status}\n` +
              JSON.stringify(
                error.error,
                null,
                2);
          }
      });
  }  

  unshareTestDocument():
    void
  {
    const documentId =
      'ab01e593-05d5-4fc9-bae8-ca3d50d8ab1b';

    const teamId =
      '08446f1f-678d-4057-946c-a2df7637ea9a';

    this.http
      .delete(
        `${apiBaseUrl}/api/documents/${documentId}/teams/${teamId}`,
        {
          observe:
            'response'
        })
      .subscribe({
        next:
          response =>
          {
            this.apiResult =
              `HTTP ${response.status}\n` +
              'Document unshared from Engineering.';
          },

        error:
          error =>
          {
            this.apiResult =
              `HTTP ${error.status}\n` +
              JSON.stringify(
                error.error,
                null,
                2);
          }
      });
  }

}