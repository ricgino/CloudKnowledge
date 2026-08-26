import {
  NgModule
} from '@angular/core';

import {
  RouterModule,
  Routes
} from '@angular/router';

import {
  BrowserUtils
} from '@azure/msal-browser';

import {
  RedirectComponent
} from './redirect/redirect';

const routes: Routes = [
  {
    path:
      'redirect',

    component:
      RedirectComponent
  }
];

@NgModule({
  imports: [
    RouterModule.forRoot(
      routes,
      {
        initialNavigation:
          !BrowserUtils.isInIframe()
            ? 'enabledNonBlocking'
            : 'disabled'
      })
  ],

  exports: [
    RouterModule
  ]
})
export class AppRoutingModule
{
}