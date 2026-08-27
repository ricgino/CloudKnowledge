import {
  Injectable,
  OnDestroy
} from '@angular/core';

import {
  HttpClient
} from '@angular/common/http';

import {
  MsalService
} from '@azure/msal-angular';

import {
  Observable,
  firstValueFrom
} from 'rxjs';

import {
  apiBaseUrl,
  apiScope
} from '../../auth-config';

export interface NotificationItem
{
  id: string;
  type: string;
  title: string;
  message: string;
  target: string | null;
  createdAtUtc: string;
  isRead: boolean;
}

@Injectable({
  providedIn: 'root'
})
export class Notifications
  implements OnDestroy
{
  private abortController:
    AbortController | null = null;

  private running = false;

  constructor(
    private readonly http: HttpClient,
    private readonly auth: MsalService)
  {
  }

  getNotifications(
    take = 20):
    Observable<NotificationItem[]>
  {
    return this.http.get<NotificationItem[]>(
      `${apiBaseUrl}/api/notifications?take=${take}`);
  }

  markRead(
    notificationId: string):
    Observable<void>
  {
    return this.http.put<void>(
      `${apiBaseUrl}/api/notifications/${notificationId}/read`,
      null);
  }

  startRealtime(
    onNotification:
      (notification: NotificationItem) => void):
    void
  {
    if (this.running)
    {
      return;
    }

    this.running = true;
    this.abortController =
      new AbortController();

    void this.runRealtimeLoop(
      onNotification,
      this.abortController.signal);
  }

  stopRealtime(): void
  {
    this.running = false;
    this.abortController?.abort();
    this.abortController = null;
  }

  ngOnDestroy(): void
  {
    this.stopRealtime();
  }

  private async runRealtimeLoop(
    onNotification:
      (notification: NotificationItem) => void,
    signal: AbortSignal):
    Promise<void>
  {
    while (!signal.aborted && this.running)
    {
      try
      {
        await this.openStream(
          onNotification,
          signal);
      }
      catch (error)
      {
        if (signal.aborted)
        {
          return;
        }

        console.warn(
          'Notification stream disconnected. Reconnecting...',
          error);
      }

      if (!signal.aborted && this.running)
      {
        await this.delay(
          2000,
          signal);
      }
    }
  }

  private async openStream(
    onNotification:
      (notification: NotificationItem) => void,
    signal: AbortSignal):
    Promise<void>
  {
    const account =
      this.auth.instance.getActiveAccount() ??
      this.auth.instance.getAllAccounts()[0];

    if (!account)
    {
      throw new Error(
        'No authenticated account is available for notifications.');
    }

    const tokenResult =
      await firstValueFrom(
        this.auth.acquireTokenSilent(
          {
            scopes: [apiScope],
            account
          }));

    const response =
      await fetch(
        `${apiBaseUrl}/api/notifications/stream`,
        {
          method: 'GET',
          headers: {
            Accept: 'text/event-stream',
            Authorization:
              `Bearer ${tokenResult.accessToken}`
          },
          cache: 'no-store',
          signal
        });

    if (!response.ok || !response.body)
    {
      throw new Error(
        `Notification stream failed with HTTP ${response.status}.`);
    }

    const reader =
      response.body.getReader();

    const decoder =
      new TextDecoder();

    let buffer = '';

    while (!signal.aborted)
    {
      const result =
        await reader.read();

      if (result.done)
      {
        break;
      }

      buffer +=
        decoder.decode(
          result.value,
          {
            stream: true
          });

      let boundary =
        buffer.indexOf('\n\n');

      while (boundary >= 0)
      {
        const eventBlock =
          buffer.slice(
            0,
            boundary);

        buffer =
          buffer.slice(
            boundary + 2);

        this.handleEventBlock(
          eventBlock,
          onNotification);

        boundary =
          buffer.indexOf('\n\n');
      }
    }
  }

  private handleEventBlock(
    block: string,
    onNotification:
      (notification: NotificationItem) => void):
    void
  {
    const normalized =
      block.replaceAll(
        '\r',
        '');

    if (!normalized.includes(
        'event: notification'))
    {
      return;
    }

    const data =
      normalized
        .split('\n')
        .filter(line =>
          line.startsWith('data:'))
        .map(line =>
          line.slice(5).trimStart())
        .join('\n');

    if (!data)
    {
      return;
    }

    onNotification(
      JSON.parse(data) as NotificationItem);
  }

  private delay(
    milliseconds: number,
    signal: AbortSignal):
    Promise<void>
  {
    return new Promise<void>(
      resolve =>
      {
        const timeout =
          window.setTimeout(
            resolve,
            milliseconds);

        signal.addEventListener(
          'abort',
          () =>
          {
            window.clearTimeout(
              timeout);

            resolve();
          },
          {
            once: true
          });
      });
  }
}
