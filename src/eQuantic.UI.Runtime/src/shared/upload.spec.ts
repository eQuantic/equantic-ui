import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { PickedFile } from './picked-file';
import { FileFilter, UploadPolicy, UploadedFile } from './primitive-values';
import { Upload, type UploadOptions } from './upload';
import { WebUploads } from './devices/uploads';

/**
 * The browser's `Upload`, against a tus server of its own in memory. What a page relies on is the
 * CONTRACT: the file reaches the server whole, in parts; a part that fails is sent again from where the
 * server stands; a reloaded page goes on with the upload it remembered; a cancel removes what arrived;
 * and a refusal reaches the page in the server's words, or the policy's before anything is sent.
 */

interface Received {
  method: string;
  url: string;
  headers: Record<string, string>;
  offset?: number;
  length?: number;
}

/** The server: its uploads by id, every request it received, and failures a test plans. */
class TusServer {
  readonly uploads = new Map<string, { length: number; bytes: number[] }>();
  readonly received: Received[] = [];
  /** Answers the next requests of a method with this, before the server reads them. */
  readonly planned: { method: string; status: number; body?: string }[] = [];
  private next = 0;

  answer(xhr: FakeXhr, body: Blob | null): void {
    const { method, url, headers } = xhr;
    const offset = headers['Upload-Offset'];
    this.received.push({
      method,
      url,
      headers,
      offset: offset === undefined ? undefined : Number(offset),
      length: body?.size,
    });
    const plan = this.planned.findIndex((planned) => planned.method === method);
    if (plan >= 0) {
      const [planned] = this.planned.splice(plan, 1);
      if (planned.status === 0) return xhr.fail();
      return xhr.reply(planned.status, {}, planned.body ?? '');
    }

    const id = url.split('/').pop() as string;
    switch (method) {
      case 'POST': {
        const created = `${(++this.next).toString(16).padStart(32, '0')}`;
        this.uploads.set(created, { length: Number(headers['Upload-Length']), bytes: [] });
        return xhr.reply(201, { Location: `http://localhost/api/_equantic/uploads/${created}` });
      }
      case 'HEAD': {
        const upload = this.uploads.get(id);
        if (!upload) return xhr.reply(404, {});
        return xhr.reply(200, {
          'Upload-Offset': String(upload.bytes.length),
          'Upload-Length': String(upload.length),
        });
      }
      case 'PATCH': {
        const upload = this.uploads.get(id);
        if (!upload) return xhr.reply(404, {});
        if (Number(offset) !== upload.bytes.length) return xhr.reply(409, {});
        void (body as Blob).arrayBuffer().then((buffer) => {
          const bytes = Array.from(new Uint8Array(buffer));
          xhr.progress(bytes.length);
          upload.bytes.push(...bytes);
          xhr.reply(204, { 'Upload-Offset': String(upload.bytes.length) });
        });
        return;
      }
      case 'DELETE':
        this.uploads.delete(id);
        return xhr.reply(204, {});
    }
  }
}

let server: TusServer;

/** An XMLHttpRequest whose requests the in-memory server answers, a tick later. */
class FakeXhr {
  method = '';
  url = '';
  headers: Record<string, string> = {};
  status = 0;
  responseText = '';
  withCredentials = false;
  private responseHeaders: Record<string, string> = {};
  private done = false;
  readonly upload: { onprogress: ((event: ProgressEvent) => void) | null } = { onprogress: null };
  onload: (() => void) | null = null;
  onerror: (() => void) | null = null;
  ontimeout: (() => void) | null = null;
  onabort: (() => void) | null = null;

  open(method: string, url: string): void {
    this.method = method;
    this.url = url;
  }

  setRequestHeader(name: string, value: string): void {
    this.headers[name] = value;
  }

  getResponseHeader(name: string): string | null {
    return this.responseHeaders[name] ?? null;
  }

  send(body: Blob | null): void {
    setTimeout(() => {
      if (!this.done) server.answer(this, body);
    }, 0);
  }

  abort(): void {
    if (this.done) return;
    this.done = true;
    this.onabort?.();
  }

  progress(loaded: number): void {
    if (!this.done) this.upload.onprogress?.({ loaded } as ProgressEvent);
  }

  reply(status: number, headers: Record<string, string>, text = ''): void {
    if (this.done) return;
    this.done = true;
    this.status = status;
    this.responseHeaders = headers;
    this.responseText = text;
    this.onload?.();
  }

  fail(): void {
    if (this.done) return;
    this.done = true;
    this.onerror?.();
  }
}

const models = new UploadPolicy('models', [new FileFilter('3D models', 'stl', 'obj')], 1000n);
const quick: UploadOptions = { partSize: 8, retryDelays: [0, 0, 0] };

function file(name: string, length: number, lastModified = 1): PickedFile {
  const bytes = Uint8Array.from({ length }, (_, index) => index % 251);
  return PickedFile.fromFile(new File([bytes], name, { type: 'model/stl', lastModified }));
}

/** Resolves when the upload reaches one of these states. */
function settled(upload: Upload, ...states: string[]): Promise<Upload> {
  return new Promise((resolve) => {
    if (states.includes(upload.state)) return resolve(upload);
    const subscription = upload.subscribe((changed) => {
      if (!states.includes(changed.state)) return;
      subscription.dispose();
      resolve(changed);
    });
  });
}

const patches = () => server.received.filter((request) => request.method === 'PATCH');

describe('Upload', () => {
  beforeEach(() => {
    server = new TusServer();
    vi.stubGlobal('XMLHttpRequest', FakeXhr);
    localStorage.clear();
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    localStorage.clear();
  });

  it('sends the file in parts, and completes with the UploadedFile a Server Action receives', async () => {
    const upload = new Upload(file('bracket.stl', 20), models, quick).begin();
    const seen: bigint[] = [];
    upload.subscribe((changed) => seen.push(changed.sent));

    await settled(upload, 'completed', 'failed');

    expect(upload.state).toBe('completed');
    const create = server.received[0];
    expect(create.method).toBe('POST');
    expect(create.headers['Tus-Resumable']).toBe('1.0.0');
    expect(create.headers['Upload-Length']).toBe('20');
    expect(create.headers['Upload-Metadata']).toBe(
      `policy ${btoa('models')},filename ${btoa('bracket.stl')},filetype ${btoa('model/stl')}`,
    );
    expect(patches().map((request) => [request.offset, request.length])).toEqual([
      [0, 8],
      [8, 8],
      [16, 4],
    ]);
    expect(patches()[0].headers['Content-Type']).toBe('application/offset+octet-stream');
    const [id, stored] = [...server.uploads.entries()][0];
    expect(stored.bytes).toEqual(Array.from({ length: 20 }, (_, index) => index));
    expect(upload.sent).toBe(20n);
    expect(seen).toContain(8n);
    expect(upload.result).toBeInstanceOf(UploadedFile);
    expect(upload.result).toMatchObject({ id, name: 'bracket.stl', mimeType: 'model/stl', size: 20n, policy: 'models' });
    expect(localStorage.length).toBe(0);
  });

  it('sends a part that failed again, from the offset the server reports', async () => {
    server.planned.push({ method: 'PATCH', status: 0 });
    server.planned.push({ method: 'PATCH', status: 503 });

    const upload = new Upload(file('bracket.stl', 12), models, quick).begin();
    await settled(upload, 'completed', 'failed');

    expect(upload.state).toBe('completed');
    expect(server.received.map((request) => request.method)).toEqual([
      'POST',
      'PATCH', // nothing answered
      'HEAD',
      'PATCH', // 503
      'HEAD',
      'PATCH',
      'PATCH',
    ]);
    expect([...server.uploads.values()][0].bytes).toHaveLength(12);
  });

  it('fails, in its own words, when the connection does not come back', async () => {
    for (let i = 0; i < 4; i++) server.planned.push({ method: 'POST', status: 0 });

    const upload = new Upload(file('bracket.stl', 12), models, quick).begin();
    await settled(upload, 'completed', 'failed');

    expect(upload.state).toBe('failed');
    expect(upload.error).toBe('The connection to the server was lost.');
    expect(server.received.filter((request) => request.method === 'POST')).toHaveLength(4);
  });

  it('goes on after a reload with the upload it remembered for the same file', async () => {
    const first = new Upload(file('bracket.stl', 20), models, quick).begin();
    await new Promise<void>((resolve) => {
      const subscription = first.subscribe((changed) => {
        if (changed.sent < 8n) return;
        subscription.dispose();
        resolve();
      });
    });
    // The page goes away mid-upload: nothing of it survives but what it remembered.
    first.pause();
    expect(localStorage.length).toBe(1);
    const [id] = [...server.uploads.keys()];
    const held = server.uploads.get(id)!.bytes.length;
    expect(held).toBeGreaterThan(0);
    server.received.length = 0;

    const again = new Upload(file('bracket.stl', 20), models, quick).begin();
    await settled(again, 'completed', 'failed');

    expect(again.state).toBe('completed');
    expect(server.received[0].method).toBe('HEAD');
    expect(server.received.some((request) => request.method === 'POST')).toBe(false);
    expect(patches()[0].offset).toBe(held);
    expect(server.uploads.get(id)!.bytes).toEqual(Array.from({ length: 20 }, (_, index) => index));
    expect(again.result?.id).toBe(id);
    expect(localStorage.length).toBe(0);
  });

  it('starts over when the upload it remembered is gone from the server', async () => {
    localStorage.setItem(
      `eq-upload:${JSON.stringify(['models', 'bracket.stl', '4', 1])}`,
      'http://localhost/api/_equantic/uploads/expired',
    );

    const upload = new Upload(file('bracket.stl', 4), models, quick).begin();
    await settled(upload, 'completed', 'failed');

    expect(upload.state).toBe('completed');
    expect(server.received.map((request) => request.method)).toEqual(['HEAD', 'POST', 'PATCH']);
  });

  it('pauses, and resumes from where the server stands', async () => {
    const upload = new Upload(file('bracket.stl', 20), models, quick).begin();
    await new Promise<void>((resolve) => {
      const subscription = upload.subscribe((changed) => {
        if (changed.sent < 8n) return;
        subscription.dispose();
        resolve();
      });
    });

    upload.pause();
    expect(upload.state).toBe('paused');
    const before = server.received.length;
    await new Promise((resolve) => setTimeout(resolve, 10));
    expect(server.received.length).toBe(before);

    upload.resume();
    await settled(upload, 'completed', 'failed');
    expect(upload.state).toBe('completed');
    expect(server.received[before].method).toBe('HEAD');
    expect([...server.uploads.values()][0].bytes).toHaveLength(20);
  });

  it('cancels: the request in flight stops, and the server removes what arrived', async () => {
    const upload = new Upload(file('bracket.stl', 20), models, quick).begin();
    await new Promise<void>((resolve) => {
      const subscription = upload.subscribe((changed) => {
        if (changed.sent < 8n) return;
        subscription.dispose();
        resolve();
      });
    });

    upload.cancel();
    expect(upload.state).toBe('cancelled');
    await new Promise((resolve) => setTimeout(resolve, 10));

    const removal = server.received.find((request) => request.method === 'DELETE');
    expect(removal?.url).toMatch(/\/api\/_equantic\/uploads\/[0-9a-f]{32}$/);
    expect(server.uploads.size).toBe(0);
    expect(localStorage.length).toBe(0);
    upload.resume();
    expect(upload.state).toBe('cancelled');
  });

  it("shows the server's reason for a refusal, and does not try again", async () => {
    server.planned.push({ method: 'POST', status: 429, body: 'At most 3 uploads may be unfinished at a time.' });

    const upload = new Upload(file('bracket.stl', 4), models, quick).begin();
    await settled(upload, 'completed', 'failed');

    expect(upload.state).toBe('failed');
    expect(upload.error).toBe('At most 3 uploads may be unfinished at a time.');
    expect(server.received).toHaveLength(1);
  });

  it('refuses before sending what the policy does not accept, in the words the server would use', () => {
    const wrong = new Upload(file('setup.exe', 4), models, quick).begin();
    const large = new Upload(file('bracket.STL', 1001), models, quick).begin();

    expect(wrong.state).toBe('failed');
    expect(wrong.error).toBe('Only .stl, .obj files are accepted here.');
    expect(large.error).toBe('The file is larger than the 1000 bytes accepted here.');
    expect(server.received).toHaveLength(0);
  });

  it('is what the browser IUploads starts', async () => {
    const uploads = new WebUploads();
    expect(uploads.isAvailable).toBe(true);

    const upload = uploads.start(file('lid.obj', 3), models);
    expect(upload).toBeInstanceOf(Upload);
    await settled(upload, 'completed', 'failed');
    expect(upload.state).toBe('completed');
  });
});

describe('UploadPolicy', () => {
  it('accepts by the end of the name, without case, and a policy without filters accepts anything', () => {
    const archives = new UploadPolicy('archives', [new FileFilter('Archives', '.tar.gz', 'ZIP')], 10n);

    expect(archives.accepts('backup.TAR.GZ')).toBe(true);
    expect(archives.accepts('backup.gz')).toBe(false);
    expect(archives.accepts('photos.zip')).toBe(true);
    expect(new UploadPolicy('any', [], 1n).accepts('anything')).toBe(true);
    expect(archives.refusalOf('a.rar', 1n)).toBe('Only .tar.gz, .ZIP files are accepted here.');
    expect(archives.refusalOf('a.zip', 10n)).toBeNull();
  });
});
