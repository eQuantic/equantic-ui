/**
 * The C# `Upload`: one file on its way to the app's server, as the browser sends it.
 *
 * As with `PickedFile`, the C# type is abstract and each target realizes it; here there is one
 * target, so the twin IS the realization, and a page never constructs one: `IUploads.Start` does.
 *
 * The file goes in parts of 8 MB over tus (devices/tus.ts), well under the 30 MB a server takes in one
 * request by default, and each part reports its own progress. What can go wrong in the middle is
 * answered by the protocol's offset: a part that fails is sent again, after a pause that grows (0, 1,
 * 3, 5 and 10 seconds), from the offset the server reports it holds; and a page reloaded in the middle
 * finds the upload's address in localStorage, under the file's fingerprint, when the person starts the
 * same file again, and goes on from there.
 *
 * A refusal is never tried again: the policy's own (asked before anything is sent), and the server's,
 * whose short English body is the upload's `error` as it was written. The one exception is a limit the
 * server says frees itself, with a `Retry-After` (a person's unfinished uploads, as they finish): the
 * creation waits that long and asks again, for up to ten minutes of the server's waits.
 *
 * One page sends three files at a time (`UploadSlots`): the others stay `waiting` and start, in the order
 * they were started, as one of the three completes, fails, is paused or is cancelled. Choosing fifty
 * photographs then never meets the server's limit of unfinished uploads per person.
 */

import { PickedFile } from './picked-file';
import { UploadedFile, type UploadPolicy } from './primitive-values';
import type { UploadStateValue } from './enums.generated';
import { TusAborted, tusMetadata, tusRequest, type TusAnswer, type TusRequest } from './devices/tus';

/** How the browser sends, which a test narrows: where, in how large a part, and how patiently. */
export interface UploadOptions {
  /** The server's upload endpoint. */
  readonly endpoint?: string;
  /** The largest part one request carries, in bytes. */
  readonly partSize?: number;
  /** How long to wait before each new try of a failed request, in milliseconds. Past the last, the upload fails. */
  readonly retryDelays?: readonly number[];
  /** The turns it waits for before it sends: the page's, unless a test gives its own. */
  readonly slots?: UploadSlots;
  /** How long a second of the server's `Retry-After` lasts, in milliseconds: a test shortens it. */
  readonly second?: number;
}

const ENDPOINT = '/api/_equantic/uploads';
const PART_SIZE = 8 * 1024 * 1024;
const RETRY_DELAYS: readonly number[] = [0, 1000, 3000, 5000, 10000];
/** Where an unfinished upload's address is remembered, followed by the file's fingerprint. */
const REMEMBERED = 'eq-upload:';

/** The reason the client gives when it was the connection, not the server, that failed. */
const CONNECTION_LOST = 'The connection to the server was lost.';
/** How many files one page sends at a time. */
const SENDING_AT_ONCE = 3;
/** How many seconds of the server's `Retry-After` a creation waits, in all, before it fails. */
const PATIENCE_SECONDS = 600;

/** A turn among a page's uploads: granted at once when a slot is free, or later, in order; false when withdrawn. */
export interface UploadTurn {
  readonly granted: Promise<boolean>;
  /** Leaves the line, if it is still waiting in it. */
  withdraw(): void;
}

/**
 * The turns a page's uploads take to send: a few at a time, the others waiting in the order they asked.
 * A slot is given back when its upload completes, fails, is paused or is cancelled.
 *
 * Until the server first answers one of them, one goes alone: a visitor's browser is named by the cookie
 * the server writes with its first upload, and creations sent together before it each made a visitor of
 * their own, of whom the browser kept the last, so the others belonged to nobody it could speak for.
 */
export class UploadSlots {
  private free = 1;
  private opened = false;
  private readonly line: ((granted: boolean) => void)[] = [];

  constructor(private readonly size: number) {}

  /** The server answered an upload of this page: the browser holds what names it, and the rest may go. */
  open(): void {
    if (this.opened) return;
    this.opened = true;
    for (let more = this.size - 1; more > 0; more--) this.give();
  }

  take(): UploadTurn {
    let settle!: (granted: boolean) => void;
    const granted = new Promise<boolean>((resolve) => (settle = resolve));
    if (this.free > 0) {
      this.free--;
      settle(true);
      return { granted, withdraw: () => {} };
    }
    this.line.push(settle);
    return {
      granted,
      withdraw: () => {
        const at = this.line.indexOf(settle);
        if (at < 0) return;
        this.line.splice(at, 1);
        settle(false);
      },
    };
  }

  give(): void {
    const next = this.line.shift();
    if (next) next(true);
    else this.free++;
  }
}

/** The page's own turns, which every upload a page starts takes. */
const pageSlots = new UploadSlots(SENDING_AT_ONCE);

/** A failure that trying again cannot mend: the server refused, and said why. */
class Refused extends Error {
  constructor(readonly reason: string) {
    super(reason);
  }
}

/** A failure that trying again may mend: nothing answered, or the server could not answer now. */
class Interrupted extends Error {}

/** A limit the server says frees itself: asked again after `seconds`, until the patience runs out. */
class Throttled extends Error {
  constructor(
    readonly reason: string,
    readonly seconds: number,
  ) {
    super(reason);
  }
}

interface Subscription {
  dispose(): void;
}

export class Upload {
  readonly file: PickedFile;
  readonly policy: UploadPolicy;

  private _state: UploadStateValue = 'waiting';
  private _sent = 0n;
  private _error: string | null = null;
  private _result: UploadedFile | null = null;
  private readonly listeners = new Set<(upload: Upload) => void>();
  private readonly endpoint: string;
  private readonly partSize: number;
  private readonly retryDelays: readonly number[];
  private readonly slots: UploadSlots;
  private readonly second: number;
  /** The turn it waits for, while it waits. */
  private turn: UploadTurn | null = null;
  /** Ends a wait for the server's Retry-After early, on a pause or a cancel. */
  private wake: (() => void) | null = null;
  /** The seconds the server asked it to wait since its creation was last refused, in all. */
  private waited = 0;
  /** The upload's address on the server, once it was created or found again. */
  private url: string | null = null;
  /** How many bytes the server is known to hold. */
  private offset = 0n;
  private inFlight: TusRequest | null = null;
  /** Bumped by a pause and a cancel, so a drive they interrupted stops where it stands. */
  private generation = 0;

  constructor(file: PickedFile, policy: UploadPolicy, options: UploadOptions = {}) {
    this.file = file;
    this.policy = policy;
    this.endpoint = options.endpoint ?? ENDPOINT;
    this.partSize = options.partSize ?? PART_SIZE;
    this.retryDelays = options.retryDelays ?? RETRY_DELAYS;
    this.slots = options.slots ?? pageSlots;
    this.second = options.second ?? 1000;
  }

  get state(): UploadStateValue {
    return this._state;
  }

  get sent(): bigint {
    return this._sent;
  }

  get size(): bigint {
    return this.file.size;
  }

  get error(): string | null {
    return this._error;
  }

  get result(): UploadedFile | null {
    return this._result;
  }

  /** Asks the policy first, and sends nothing it refuses; otherwise drives the upload to its end. */
  begin(): this {
    const refusal = this.policy.refusalOf(this.file.name, this.file.size);
    if (refusal !== null) {
      this._state = 'failed';
      this._error = refusal;
      return this;
    }
    void this.drive(this.generation);
    return this;
  }

  pause(): void {
    if (this._state !== 'waiting' && this._state !== 'sending') return;
    this.interrupt();
    // What the server holds is asked again on resume: the part in flight may have arrived in part.
    this._sent = this.offset;
    this.change('paused');
  }

  resume(): void {
    if (this._state !== 'paused') return;
    this.change('sending');
    void this.drive(this.generation);
  }

  cancel(): void {
    if (this._state === 'completed' || this._state === 'cancelled') return;
    this.interrupt();
    this.change('cancelled');
    this.terminate();
  }

  subscribe(onChanged: (upload: Upload) => void): Subscription {
    // Each subscription its own entry, so the same callback subscribed twice is called twice and
    // disposing one leaves the other, as two .NET subscriptions would.
    const entry = (upload: Upload) => onChanged(upload);
    this.listeners.add(entry);
    return { dispose: () => void this.listeners.delete(entry) };
  }

  private interrupt(): void {
    this.generation++;
    // A turn not yet granted is left, so the next upload in line takes it, and a wait for the server ends.
    this.turn?.withdraw();
    this.turn = null;
    this.wake?.();
    const request = this.inFlight;
    this.inFlight = null;
    request?.abort();
  }

  /**
   * The upload, from wherever it stands to its end, until a pause or a cancel bumps the generation: once
   * its turn comes, which it gives back however it ends.
   */
  private async drive(generation: number): Promise<void> {
    const turn = this.slots.take();
    this.turn = turn;
    const granted = await turn.granted;
    if (this.turn === turn) this.turn = null;
    if (!granted) return;
    if (this.generation !== generation) {
      this.slots.give();
      return;
    }
    try {
      await this.carry(generation);
    } finally {
      this.slots.give();
    }
  }

  /** Waits `milliseconds`, or until a pause or a cancel ends the wait. */
  private wait(milliseconds: number): Promise<void> {
    return new Promise((resolve) => {
      const timer = setTimeout(done, milliseconds);
      const self = this;
      function done(): void {
        clearTimeout(timer);
        if (self.wake === done) self.wake = null;
        resolve();
      }
      this.wake = done;
    });
  }

  /** The parts, from wherever the upload stands to its end, while its generation holds. */
  private async carry(generation: number): Promise<void> {
    let tries = 0;
    let askOffset = this.url !== null;
    while (this.generation === generation) {
      try {
        if (this.url === null) {
          const remembered = recall(this.fingerprint());
          if (remembered !== null) {
            this.url = remembered;
            askOffset = true;
          } else {
            await this.create(generation);
            askOffset = false;
          }
        }
        if (askOffset) {
          const held = await this.held();
          if (held === null) {
            // The server knows no such upload any more (it expired, or another browser's cookie owned
            // it): start it over.
            forget(this.fingerprint());
            this.url = null;
            this.offset = 0n;
            this.progress(0n);
            continue;
          }
          this.offset = held;
          askOffset = false;
          this.progress(held);
        }
        if (this.generation !== generation) return;
        if (this.offset >= this.file.size) {
          this.complete();
          return;
        }
        if (this._state !== 'sending') this.change('sending');
        await this.sendPart();
        tries = 0;
      } catch (error) {
        if (error instanceof TusAborted || this.generation !== generation) return;
        if (error instanceof Throttled) {
          this.waited += error.seconds;
          if (this.waited > PATIENCE_SECONDS) {
            this.fail(error.reason);
            return;
          }
          // The server knows this browser (a new visitor has nothing unfinished), so the page's turns open.
          this.slots.open();
          if (this._state !== 'waiting') this.change('waiting');
          await this.wait(error.seconds * this.second);
          continue;
        }
        if (error instanceof Refused) {
          this.fail(error.reason);
          return;
        }
        if (tries >= this.retryDelays.length) {
          this.fail(CONNECTION_LOST);
          return;
        }
        await delay(this.retryDelays[tries++]);
        // The part may have arrived in part, so the next try starts where the server stands.
        askOffset = this.url !== null;
      }
    }
  }

  private async create(generation: number): Promise<void> {
    const answer = await this.send('POST', this.endpoint, {
      'Upload-Length': this.file.size.toString(),
      'Upload-Metadata': tusMetadata({
        policy: this.policy.name,
        filename: this.file.name,
        filetype: this.file.mimeType,
      }),
    });
    const location = answer.status === 201 ? answer.header('Location') : null;
    if (location === null) throw failureOf(answer);
    this.url = new URL(location, baseAddress()).toString();
    this.offset = 0n;
    this.waited = 0;
    this.slots.open();
    remember(this.fingerprint(), this.url);
    // Cancelled while the server was creating it: what it made is removed rather than left to expire.
    if (this.generation !== generation && this._state === 'cancelled') this.terminate();
  }

  /** How many bytes the server holds, or null when it knows no such upload for this browser. */
  private async held(): Promise<bigint | null> {
    const answer = await this.send('HEAD', this.url as string, { 'Cache-Control': 'no-store' });
    if (answer.status === 403 || answer.status === 404 || answer.status === 410) return null;
    const offset = answer.header('Upload-Offset');
    if (answer.status < 200 || answer.status >= 300 || offset === null) throw failureOf(answer);
    this.slots.open();
    return BigInt(offset);
  }

  private async sendPart(): Promise<void> {
    const from = this.offset;
    const start = Number(from);
    const part = this.file.content.slice(start, Math.min(start + this.partSize, Number(this.file.size)));
    const answer = await this.send(
      'PATCH',
      this.url as string,
      { 'Upload-Offset': from.toString(), 'Content-Type': 'application/offset+octet-stream' },
      part,
      (loaded) => this.progress(from + BigInt(loaded)),
    );
    const offset = answer.header('Upload-Offset');
    if (answer.status !== 204 || offset === null) throw failureOf(answer);
    this.offset = BigInt(offset);
    this.progress(this.offset);
  }

  private async send(
    method: 'POST' | 'HEAD' | 'PATCH' | 'DELETE',
    url: string,
    headers: Record<string, string>,
    body?: Blob,
    onProgress?: (loaded: number) => void,
  ): Promise<TusAnswer> {
    const request = tusRequest(method, url, headers, body, onProgress);
    this.inFlight = request;
    try {
      return await request.answer;
    } finally {
      if (this.inFlight === request) this.inFlight = null;
    }
  }

  /** Asks the server to remove what arrived, and forgets the address. Nothing waits for the answer. */
  private terminate(): void {
    forget(this.fingerprint());
    const url = this.url;
    if (url === null) return;
    tusRequest('DELETE', url, {}).answer.catch(() => {
      // Nothing to do: an upload nobody removes is removed by the server at its expiry.
    });
  }

  private complete(): void {
    forget(this.fingerprint());
    const url = this.url as string;
    const id = decodeURIComponent(new URL(url).pathname.split('/').filter((segment) => segment).pop() ?? '');
    this._result = new UploadedFile(id, this.file.name, this.file.mimeType, this.file.size, this.policy.name);
    this._sent = this.file.size;
    this.change('completed');
  }

  private fail(reason: string): void {
    this._error = reason;
    this.change('failed');
  }

  private progress(sent: bigint): void {
    if (sent === this._sent) return;
    this._sent = sent;
    this.notify();
  }

  private change(state: UploadStateValue): void {
    this._state = state;
    this.notify();
  }

  private notify(): void {
    for (const listener of [...this.listeners]) listener(this);
  }

  /** What names the same file under the same policy across a reload: the browser keeps no path. */
  private fingerprint(): string {
    const content = this.file.content as Blob & { lastModified?: number };
    return REMEMBERED + JSON.stringify([
      this.policy.name,
      this.file.name,
      this.file.size.toString(),
      content.lastModified ?? 0,
    ]);
  }
}

/** A refusal for a 4xx the server explained, an interruption for anything trying again may mend. */
function failureOf(answer: TusAnswer): Error {
  const { status } = answer;
  // Nothing answered; the server failed; the offset moved under the part (409); the upload is busy
  // with another request (423).
  if (status === 0 || status >= 500 || status === 409 || status === 423) return new Interrupted();
  const text = answer.text.trim();
  const reason = text.length > 0 ? text : `The server refused the upload (${status}).`;
  // A limit that frees itself, which the server marks with how long to wait (the person's unfinished uploads).
  const after = status === 429 ? answer.header('Retry-After') : null;
  if (after !== null && /^\d+$/.test(after.trim())) return new Throttled(reason, Number(after.trim()));
  return new Refused(reason);
}

function delay(milliseconds: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, milliseconds));
}

function baseAddress(): string {
  return typeof location !== 'undefined' ? location.href : 'http://localhost/';
}

// localStorage can be absent, full or forbidden (a private window, a sandboxed frame): an upload that
// cannot remember itself still uploads, and only a reload in the middle starts it over.
function recall(key: string): string | null {
  try {
    return globalThis.localStorage?.getItem(key) ?? null;
  } catch {
    return null;
  }
}

function remember(key: string, url: string): void {
  try {
    globalThis.localStorage?.setItem(key, url);
  } catch {
    // See recall.
  }
}

function forget(key: string): void {
  try {
    globalThis.localStorage?.removeItem(key);
  } catch {
    // See recall.
  }
}
