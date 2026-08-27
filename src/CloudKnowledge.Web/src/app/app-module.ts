import { NgModule } from '@angular/core';
import { HTTP_INTERCEPTORS, HttpClientModule } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { BrowserModule } from '@angular/platform-browser';

import {
  InteractionType,
  IPublicClientApplication,
  PublicClientApplication,
} from '@azure/msal-browser';

import { RedirectComponent } from './redirect/redirect';

import {
  MSAL_GUARD_CONFIG,
  MSAL_INSTANCE,
  MSAL_INTERCEPTOR_CONFIG,
  MsalBroadcastService,
  MsalGuard,
  MsalGuardConfiguration,
  MsalInterceptor,
  MsalInterceptorConfiguration,
  MsalModule,
  MsalService,
} from '@azure/msal-angular';

import { AppRoutingModule } from './app-routing-module';
import { App } from './app';

import { apiBaseUrl, apiScope, loginRequest, msalConfig } from './auth-config';
import { AdministrationPage } from './features/administration/administration-page/administration-page';
import { DocumentsPage } from './features/documents/documents-page/documents-page';
import { KnowledgePage } from './features/knowledge/knowledge-page/knowledge-page';
import { TeamsPage } from './features/teams/teams-page/teams-page';

export function MSALInstanceFactory(): IPublicClientApplication {
  return new PublicClientApplication(msalConfig);
}

export function MSALGuardConfigFactory(): MsalGuardConfiguration {
  return {
    interactionType: InteractionType.Redirect,

    authRequest: loginRequest,
  };
}

export function MSALInterceptorConfigFactory(): MsalInterceptorConfiguration {
  const protectedResourceMap = new Map<string, Array<string>>();

  protectedResourceMap.set(`${apiBaseUrl}/api/*`, [apiScope]);

  return {
    interactionType: InteractionType.Redirect,

    protectedResourceMap,

    strictMatching: true,
  };
}

@NgModule({
  declarations: [
    App,
    RedirectComponent,
    KnowledgePage,
    DocumentsPage,
    TeamsPage,
    AdministrationPage,
  ],

  imports: [BrowserModule, CommonModule, AppRoutingModule, HttpClientModule, MsalModule],

  providers: [
    {
      provide: MSAL_INSTANCE,

      useFactory: MSALInstanceFactory,
    },

    {
      provide: MSAL_GUARD_CONFIG,

      useFactory: MSALGuardConfigFactory,
    },

    {
      provide: MSAL_INTERCEPTOR_CONFIG,

      useFactory: MSALInterceptorConfigFactory,
    },

    {
      provide: HTTP_INTERCEPTORS,

      useClass: MsalInterceptor,

      multi: true,
    },

    MsalService,
    MsalGuard,
    MsalBroadcastService,
  ],

  bootstrap: [App],
})
export class AppModule {}
