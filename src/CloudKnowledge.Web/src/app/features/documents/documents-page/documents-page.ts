import {
  Component,
  OnInit
} from '@angular/core';

import {
  DocumentItem,
  Documents
} from '../documents';


@Component({
  selector: 'app-documents-page',
  standalone: false,
  styleUrl: './documents-page.scss',
  templateUrl: './documents-page.html'
})
export class DocumentsPage
  implements OnInit
{
  documents:
    DocumentItem[] =
    [];

  loading =
    false;

  uploading =
    false;

  errorMessage =
    '';

  selectedFile:
    File | null =
    null;


  constructor(
    private readonly documentsService:
      Documents)
  {
  }


  ngOnInit():
    void
  {
    this.loadDocuments();
  }


  loadDocuments():
    void
  {
    this.loading =
      true;

    this.errorMessage =
      '';

    this.documentsService
      .getDocuments()
      .subscribe({
        next:
          response =>
          {
            this.documents =
              response.items;

            this.loading =
              false;
          },

        error:
          error =>
          {
            this.errorMessage =
              `Unable to load documents (HTTP ${error.status}).`;

            this.loading =
              false;
          }
      });
  }


  onFileSelected(
    event: Event):
    void
  {
    const input =
      event.target as HTMLInputElement;

    this.selectedFile =
      input.files?.[0] ?? null;
  }


  upload():
    void
  {
    if (!this.selectedFile)
    {
      return;
    }

    this.uploading =
      true;

    this.errorMessage =
      '';

    this.documentsService
      .uploadDocument(
        this.selectedFile)
      .subscribe({
        next:
          () =>
          {
            this.uploading =
              false;

            this.selectedFile =
              null;

            this.loadDocuments();
          },

        error:
          error =>
          {
            this.errorMessage =
              `Unable to upload document (HTTP ${error.status}).`;

            this.uploading =
              false;
          }
      });
  }
}
