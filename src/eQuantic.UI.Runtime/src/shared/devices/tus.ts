/**
 * The wire under `Upload`: tus 1.0, the resumable protocol the server's upload endpoint speaks
 * (`/api/_equantic/uploads`). A few requests and nothing else — no library, no CDN — because the part
 * of tus a page needs is four verbs and five headers:
 *
 * - `POST` creates an upload, stating its length and naming its policy, file and type in
 *   `Upload-Metadata`; the answer's `Location` is the upload's address, whose last segment is its id.
 * - `HEAD` asks how many bytes the server holds (`Upload-Offset`).
 * - `PATCH` sends the bytes from that offset on.
 * - `DELETE` removes the upload.
 *
 * Every request is made with `XMLHttpRequest` rather than `fetch`, because only an XHR reports the
 * progress of a body it is SENDING, which is the progress inside one part of a large file.
 */

/** The protocol's version, sent on every request but the options. */
export const TUS_VERSION = '1.0.0';

/** What a request answered: its status (0 when nothing answered), a header, and its body as text. */
export interface TusAnswer {
  readonly status: number;
  header(name: string): string | null;
  readonly text: string;
}

/** Thrown when a request was aborted on purpose: a pause or a cancel. */
export class TusAborted extends Error {
  constructor() {
    super('The request was aborted.');
  }
}

/** One request in flight, which the upload can abort. */
export interface TusRequest {
  readonly answer: Promise<TusAnswer>;
  abort(): void;
}

/**
 * Sends one request. A network failure answers status 0 rather than throwing, so the caller decides
 * whether to try again; an abort rejects with `TusAborted`.
 */
export function tusRequest(
  method: 'POST' | 'HEAD' | 'PATCH' | 'DELETE',
  url: string,
  headers: Record<string, string>,
  body?: Blob,
  onProgress?: (loaded: number) => void,
): TusRequest {
  const xhr = new XMLHttpRequest();
  const answer = new Promise<TusAnswer>((resolve, reject) => {
    xhr.open(method, url, true);
    // Same-origin, where the browser sends its cookies anyway: the owner of a visitor's upload is the
    // HttpOnly cookie the server wrote when the upload started.
    xhr.withCredentials = true;
    xhr.setRequestHeader('Tus-Resumable', TUS_VERSION);
    for (const [name, value] of Object.entries(headers)) xhr.setRequestHeader(name, value);
    if (onProgress && xhr.upload)
      xhr.upload.onprogress = (event: ProgressEvent) => onProgress(event.loaded);
    xhr.onload = () =>
      resolve({
        status: xhr.status,
        header: (name) => xhr.getResponseHeader(name),
        text: typeof xhr.responseText === 'string' ? xhr.responseText : '',
      });
    xhr.onerror = () => resolve({ status: 0, header: () => null, text: '' });
    xhr.ontimeout = xhr.onerror;
    xhr.onabort = () => reject(new TusAborted());
    xhr.send(body ?? null);
  });
  return { answer, abort: () => xhr.abort() };
}

/**
 * `Upload-Metadata`: each key, a space, and its value in base64 of its UTF-8 bytes, joined by commas.
 * The server reads `policy`, `filename` and `filetype`.
 */
export function tusMetadata(entries: Record<string, string>): string {
  return Object.entries(entries)
    .map(([key, value]) => `${key} ${base64(value)}`)
    .join(',');
}

function base64(text: string): string {
  let binary = '';
  for (const byte of new TextEncoder().encode(text)) binary += String.fromCharCode(byte);
  return btoa(binary);
}
