import {
  BrowserCacheLocation,
  Configuration,
  LogLevel
} from '@azure/msal-browser';

export const apiScope =
  'api://3553ddee-92f1-464e-a409-4395bddb3898/access_as_user';

export const apiBaseUrl =
  'https://localhost:7293';

export const msalConfig: Configuration = {
  auth: {
    clientId:
      '2c8cfeda-2494-4888-a7f0-4750fa472aba',

    authority:
      'https://cloudknowledgecustomers.ciamlogin.com/cloudknowledgecustomers.onmicrosoft.com/',

    knownAuthorities: [
      'cloudknowledgecustomers.ciamlogin.com',
      '24761888-7338-4aac-9cca-eede0c9651b2.ciamlogin.com'
    ],

    redirectUri:
    'http://localhost:4200/redirect',

    postLogoutRedirectUri:
      'http://localhost:4200/redirect'
  },

  cache: {
    cacheLocation:
      BrowserCacheLocation.SessionStorage
  },

  system: {
    loggerOptions: {
      loggerCallback:
        (level: LogLevel, message: string) =>
        {
          if (level <= LogLevel.Warning)
          {
            console.log(message);
          }
        },

      logLevel:
        LogLevel.Warning,

      piiLoggingEnabled:
        false
    }
  }
};

export const loginRequest = {
  scopes: [
    apiScope
  ]
};