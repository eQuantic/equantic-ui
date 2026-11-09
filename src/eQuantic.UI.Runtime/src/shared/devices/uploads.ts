/**
 * The browser's answer to `IUploads`: each file starts an `Upload`, which sends it to the app's own
 * server over the upload endpoint the server maps when the app accepts uploads (`UseUploads`).
 *
 * Available wherever there is an `XMLHttpRequest` to send with: a page always is, since the endpoint is
 * on the server that served it. Whether the server ACCEPTS the policy is the server's answer, which
 * arrives as the upload's error.
 */

import type { PickedFile } from '../picked-file';
import type { UploadPolicy } from '../primitive-values';
import { Upload } from '../upload';

export class WebUploads {
  get isAvailable(): boolean {
    return typeof XMLHttpRequest !== 'undefined';
  }

  start(file: PickedFile, policy: UploadPolicy): Upload {
    return new Upload(file, policy).begin();
  }
}
