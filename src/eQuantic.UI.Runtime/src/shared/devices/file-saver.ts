/**
 * The browser's answer to `IFileSaver`: the content as a blob of its media type, an object URL, and
 * an anchor whose `download` is the file's name, clicked inside the press that asked. The browser then
 * saves it the way it saves every download, and tells the page nothing more, so the answer is
 * `offered` (#1022).
 *
 * Nothing is awaited before the click: a browser decides what a click may do from the gesture that is
 * still in progress, and an `await` ends it.
 */

import type { CancellationToken } from '../../utils/cancellation';
import { exception } from '../../utils/exceptions';
import type { FileSaveOutcomeValue } from '../enums.generated';

/** How long the object URL outlives the click. A browser reads the blob after the click returns,
 * and revoking it at once cancels the download in some of them. */
export const REVOKE_AFTER_MS = 30_000;

export class WebFileSaver {
  get isAvailable(): boolean {
    return typeof document !== 'undefined';
  }

  saveTextAsync(
    name: string,
    text: string,
    mimeType?: string | null,
    cancellationToken?: CancellationToken | null,
  ): Promise<FileSaveOutcomeValue> {
    return this.offer(name, text, mimeType, cancellationToken);
  }

  saveBytesAsync(
    name: string,
    content: Uint8Array,
    mimeType?: string | null,
    cancellationToken?: CancellationToken | null,
  ): Promise<FileSaveOutcomeValue> {
    return this.offer(name, content, mimeType, cancellationToken);
  }

  private offer(
    name: string,
    content: string | Uint8Array,
    mimeType: string | null | undefined,
    cancellationToken: CancellationToken | null | undefined,
  ): Promise<FileSaveOutcomeValue> {
    try {
      if (!this.isAvailable) {
        throw exception(
          'System.InvalidOperationException',
          'A file is offered to a person from a page in their browser, and this target has no document.',
        );
      }
      // As .NET does, and as the desktop does: a token cancelled before the file is offered throws.
      cancellationToken?.throwIfCancellationRequested();
    } catch (error) {
      return Promise.reject(error);
    }
    // A Blob encodes a string as UTF-8, which is what SaveTextAsync promises.
    const blob = new Blob([content as BlobPart], { type: mimeType || mediaTypeOf(name) });
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = name;
    anchor.rel = 'noopener';
    anchor.style.display = 'none';
    document.body.appendChild(anchor);
    try {
      anchor.click();
    } finally {
      anchor.remove();
      setTimeout(() => URL.revokeObjectURL(url), REVOKE_AFTER_MS);
    }
    return Promise.resolve('offered');
  }
}

/** The media types a file name's extension says, for the kinds a page most often makes. */
const MEDIA_TYPES: Readonly<Record<string, string>> = {
  txt: 'text/plain',
  csv: 'text/csv',
  tsv: 'text/tab-separated-values',
  md: 'text/markdown',
  html: 'text/html',
  htm: 'text/html',
  css: 'text/css',
  ics: 'text/calendar',
  json: 'application/json',
  xml: 'application/xml',
  pdf: 'application/pdf',
  zip: 'application/zip',
  png: 'image/png',
  jpg: 'image/jpeg',
  jpeg: 'image/jpeg',
  gif: 'image/gif',
  webp: 'image/webp',
  svg: 'image/svg+xml',
};

/** What a file is, from its name: `codes.txt` is `text/plain`, and an extension this does not know is
 * bytes and nothing more. */
export function mediaTypeOf(name: string): string {
  const dot = name.lastIndexOf('.');
  const extension = dot < 0 ? '' : name.slice(dot + 1).toLowerCase();
  return MEDIA_TYPES[extension] ?? 'application/octet-stream';
}
