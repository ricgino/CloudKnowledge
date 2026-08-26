import {
  Component,
  OnInit
} from '@angular/core';

import {
  broadcastResponseToMainFrame
} from '@azure/msal-browser/redirect-bridge';

@Component({
  selector: 'app-msal-redirect-bridge',
  template:
    '<p>Processing authentication...</p>',
  standalone: false
})
export class RedirectComponent
  implements OnInit
{
  ngOnInit():
    void
  {
    broadcastResponseToMainFrame()
      .catch(
        (error: Error) =>
        {
          console.error(
            'Error broadcasting authentication response:',
            error);
        });
  }
}