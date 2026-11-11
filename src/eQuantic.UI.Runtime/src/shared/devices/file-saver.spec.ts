import { describe, it, expect, vi, afterEach } from 'vitest';
import { CancellationTokenSource } from '../../utils/cancellation';
import { REVOKE_AFTER_MS, WebFileSaver, mediaTypeOf } from './file-saver';

/**
 * The browser's `IFileSaver` (#1022). What a page written once relies on: the file it made reaches the
 * browser as a download of its name, holding exactly its content under its media type, from inside the
 * press, and the page is told `offered`.
 */
describe('WebFileSaver', () => {
  afterEach(() => {
    vi.restoreAllMocks();
    vi.useRealTimers();
  });

  /** Stands in for the browser: records what each anchor was when it was clicked. */
  function whenTheBrowserDownloads() {
    const clicks: { download: string; href: string; inPage: boolean; blob: Blob }[] = [];
    const blobs = new Map<string, Blob>();
    let next = 0;
    vi.spyOn(URL, 'createObjectURL').mockImplementation((blob) => {
      const url = `blob:test/${next++}`;
      blobs.set(url, blob as Blob);
      return url;
    });
    const revoked = vi.spyOn(URL, 'revokeObjectURL').mockImplementation(() => {});
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (this: HTMLAnchorElement) {
      clicks.push({
        download: this.download,
        href: this.href,
        inPage: document.body.contains(this),
        blob: blobs.get(this.href)!,
      });
    });
    return { clicks, revoked };
  }

  it('downloads text under its name, as UTF-8, and answers offered', async () => {
    vi.useFakeTimers();
    const { clicks, revoked } = whenTheBrowserDownloads();

    const outcome = await new WebFileSaver().saveTextAsync('recovery-codes.txt', 'ABCD-1234\nÉ€');

    expect(outcome).toBe('offered');
    expect(clicks).toHaveLength(1);
    expect(clicks[0].download).toBe('recovery-codes.txt');
    expect(clicks[0].inPage).toBe(true);
    expect(clicks[0].blob.type).toBe('text/plain');
    expect(new Uint8Array(await clicks[0].blob.arrayBuffer())).toEqual(new TextEncoder().encode('ABCD-1234\nÉ€'));
    expect(document.querySelector('a[download]')).toBeNull();

    expect(revoked).not.toHaveBeenCalled();
    vi.advanceTimersByTime(REVOKE_AFTER_MS);
    expect(revoked).toHaveBeenCalledWith(clicks[0].href);
  });

  it('downloads bytes as they are, under the type the page states', async () => {
    const { clicks } = whenTheBrowserDownloads();

    const outcome = await new WebFileSaver().saveBytesAsync('scan', new Uint8Array([0, 1, 254, 255]), 'image/png');

    expect(outcome).toBe('offered');
    expect(clicks[0].blob.type).toBe('image/png');
    expect(Array.from(new Uint8Array(await clicks[0].blob.arrayBuffer()))).toEqual([0, 1, 254, 255]);
  });

  it('clicks before anything is awaited, so the press still holds', () => {
    const { clicks } = whenTheBrowserDownloads();

    // Not awaited: the click has already happened when the call returns.
    void new WebFileSaver().saveTextAsync('a.txt', 'a');

    expect(clicks).toHaveLength(1);
  });

  it('offers nothing once the page has given up, and throws as .NET does', async () => {
    const { clicks } = whenTheBrowserDownloads();
    const source = new CancellationTokenSource();
    source.cancel();

    await expect(new WebFileSaver().saveTextAsync('a.txt', 'a', null, source.token)).rejects.toMatchObject({
      name: 'OperationCanceledException',
    });
    expect(clicks).toHaveLength(0);
  });
});

describe('mediaTypeOf', () => {
  it('reads the kind a name says', () => {
    expect(mediaTypeOf('codes.txt')).toBe('text/plain');
    expect(mediaTypeOf('report.CSV')).toBe('text/csv');
    expect(mediaTypeOf('data.json')).toBe('application/json');
    expect(mediaTypeOf('photo.jpeg')).toBe('image/jpeg');
  });

  it('answers bytes for a kind it does not know, or no extension at all', () => {
    expect(mediaTypeOf('model.xyz')).toBe('application/octet-stream');
    expect(mediaTypeOf('README')).toBe('application/octet-stream');
  });
});
