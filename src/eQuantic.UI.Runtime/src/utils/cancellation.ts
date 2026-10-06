/**
 * C#'s `CancellationTokenSource` and `CancellationToken`, for write-once code that asks for something
 * and may stop wanting the answer: a completion list asked again as the word changes, a search box,
 * a request a page leaves behind. JavaScript has no threads to interrupt, so cancelling is what it is
 * in .NET too: a flag the work reads, and callbacks registered on it.
 *
 * Every behaviour is .NET's, measured: callbacks run in the reverse of the order they were registered
 * in, once, at the first `cancel()`; a callback registered after the cancellation runs at once; the
 * callbacks that throw are gathered into one `AggregateException` after the others have run; a
 * disposed source refuses to cancel or to hand out its token; `new CancellationToken(true)` is the
 * same token every time; and `throwIfCancellationRequested()` throws an `OperationCanceledException`
 * whose `cancellationToken` is the token that threw it.
 */

import { exception } from './exceptions';

type Callback = () => void;

/** A delay in milliseconds, or a `TimeSpan`. */
type Delay = number | { readonly totalMilliseconds: number };

/** `CancellationTokenSource`: the side that cancels. */
export class CancellationTokenSource {
  private _cancelled = false;
  private _disposed = false;
  /** In registration order; a removed one is left as null, so the others keep their place. */
  private _callbacks: (Callback | null)[] = [];
  private _timer: ReturnType<typeof setTimeout> | null = null;
  private readonly _token: CancellationToken;

  constructor(delay?: Delay) {
    this._token = new CancellationToken(this);
    if (delay !== undefined) this.cancelAfter(delay);
  }

  /** The token this source cancels: the same one on every read. */
  get token(): CancellationToken {
    this.throwIfDisposed();
    return this._token;
  }

  get isCancellationRequested(): boolean {
    return this._cancelled;
  }

  /** Cancels, once: every callback still registered runs, the last registered first. */
  cancel(): void {
    this.throwIfDisposed();
    if (this._cancelled) return;
    this._cancelled = true;
    this.stopTimer();
    const callbacks = this._callbacks;
    this._callbacks = [];
    const errors: unknown[] = [];
    for (let i = callbacks.length - 1; i >= 0; i--) {
      const callback = callbacks[i];
      if (callback === null) continue;
      try {
        callback();
      } catch (error) {
        errors.push(error);
      }
    }
    if (errors.length === 0) return;
    const aggregate = exception(
      'System.AggregateException',
      `One or more errors occurred. ${errors.map((error) => `(${messageOf(error)})`).join(' ')}`,
    );
    Object.defineProperty(aggregate, 'innerExceptions', { value: errors });
    throw aggregate;
  }

  /** Cancels after `delay`, replacing any delay set before; -1 cancels none. */
  cancelAfter(delay: Delay): void {
    this.throwIfDisposed();
    const milliseconds = typeof delay === 'number' ? delay : delay.totalMilliseconds;
    if (milliseconds < -1) {
      throw exception(
        'System.ArgumentOutOfRangeException',
        `Specified argument was out of the range of valid values. (Parameter '${typeof delay === 'number' ? 'millisecondsDelay' : 'delay'}')`,
      );
    }
    if (this._cancelled) return;
    this.stopTimer();
    if (milliseconds === -1) return;
    this._timer = setTimeout(() => {
      this._timer = null;
      if (!this._disposed) this.cancel();
    }, milliseconds);
  }

  dispose(): void {
    this._disposed = true;
    this.stopTimer();
  }

  /**
   * Registers a callback, or runs it at once when the cancellation has already happened. A callback
   * that ran at once is given back the default registration, as .NET gives it, whose token is `none`:
   * it carried the source's token, so `registration.token.canBeCanceled` said true where .NET says false.
   */
  register(callback: Callback): CancellationTokenRegistration {
    if (this._cancelled) {
      callback();
      return new CancellationTokenRegistration(CancellationToken.none, null, -1);
    }
    this._callbacks.push(callback);
    return new CancellationTokenRegistration(this._token, this, this._callbacks.length - 1);
  }

  /** Takes back the callback registered at `at`, or answers false when it ran or was taken. */
  unregister(at: number): boolean {
    if (at < 0 || at >= this._callbacks.length || this._callbacks[at] === null) return false;
    this._callbacks[at] = null;
    return true;
  }

  /** A source cancelled as soon as any of `tokens` is. */
  static createLinkedTokenSource(...tokens: (CancellationToken | CancellationToken[])[]): CancellationTokenSource {
    const all = tokens.flat();
    if (all.length === 0) throw exception('System.ArgumentException', 'No tokens were supplied.');
    const linked = new CancellationTokenSource();
    for (const token of all) {
      token.register(() => {
        if (!linked._disposed) linked.cancel();
      });
    }
    return linked;
  }

  private stopTimer(): void {
    if (this._timer === null) return;
    clearTimeout(this._timer);
    this._timer = null;
  }

  private throwIfDisposed(): void {
    if (this._disposed) {
      throw exception('System.ObjectDisposedException', 'The CancellationTokenSource has been disposed.');
    }
  }
}

/** `CancellationToken`: the side that is told. */
export class CancellationToken {
  private readonly _source: CancellationTokenSource | null;

  /** @internal Built by its source, or by `CancellationToken.none` and `canceled`. */
  constructor(source: CancellationTokenSource | null) {
    this._source = source;
  }

  /** `CancellationToken.None`, which is also `default(CancellationToken)`: it never cancels. */
  static readonly none = new CancellationToken(null);

  private static _canceled: CancellationToken | null = null;

  /** `new CancellationToken(canceled)`: the one cancelled token, or `none`. */
  static of(canceled: boolean): CancellationToken {
    if (!canceled) return CancellationToken.none;
    if (CancellationToken._canceled === null) {
      const source = new CancellationTokenSource();
      source.cancel();
      CancellationToken._canceled = source.token;
    }
    return CancellationToken._canceled;
  }

  get isCancellationRequested(): boolean {
    return this._source?.isCancellationRequested ?? false;
  }

  get canBeCanceled(): boolean {
    return this._source !== null;
  }

  throwIfCancellationRequested(): void {
    if (!this.isCancellationRequested) return;
    const canceled = exception('System.OperationCanceledException', 'The operation was canceled.');
    Object.defineProperty(canceled, 'cancellationToken', { value: this });
    throw canceled;
  }

  register(callback: Callback): CancellationTokenRegistration {
    if (this._source === null) return new CancellationTokenRegistration(this, null, -1);
    return this._source.register(callback);
  }

  equals(other: unknown): boolean {
    return other instanceof CancellationToken && other._source === this._source;
  }

  getHashCode(): number {
    return this._source === null ? 0 : hashOf(this._source);
  }
}

/** `CancellationTokenRegistration`: a callback's place on a token, given back by `dispose()`. */
export class CancellationTokenRegistration {
  constructor(
    readonly token: CancellationToken,
    private readonly _source: CancellationTokenSource | null,
    private readonly _at: number,
  ) {}

  unregister(): boolean {
    return this._source?.unregister(this._at) ?? false;
  }

  dispose(): void {
    this.unregister();
  }
}

/** A thrown value's message, as an AggregateException quotes its inner ones. */
function messageOf(error: unknown): string {
  return error instanceof Error ? error.message : String(error);
}

const hashes = new WeakMap<object, number>();
let nextHash = 1;

/** A source's hash: its identity, numbered on first ask, as a reference type's default hash is. */
function hashOf(source: object): number {
  let hash = hashes.get(source);
  if (hash === undefined) {
    hash = nextHash++;
    hashes.set(source, hash);
  }
  return hash;
}
