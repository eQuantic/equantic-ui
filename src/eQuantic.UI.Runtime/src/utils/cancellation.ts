/**
 * C#'s `CancellationTokenSource` and `CancellationToken`, for write-once code that asks for something
 * and may stop wanting the answer: a completion list asked again as the word changes, a search box,
 * a request a page leaves behind. JavaScript has no threads to interrupt, so cancelling is what it is
 * in .NET too: a flag the work reads, and callbacks registered on it.
 *
 * Every behaviour is .NET's, measured: callbacks run in the reverse of the order they were registered
 * in, once, at the first `cancel()`; a callback registered after the cancellation runs at once; the
 * callbacks that throw are gathered into one `AggregateException` after the others have run; a
 * disposed source refuses to cancel or to hand out its token; a linked source, disposed, lets go of
 * the tokens it follows; a delay is taken up to the longest .NET takes, and waited out past what a
 * browser's timer holds; `new CancellationToken(true)` is the same token every time; and
 * `throwIfCancellationRequested()` throws an `OperationCanceledException` whose `cancellationToken`
 * is the token that threw it.
 */

import { exception } from './exceptions';

type Callback = () => void;

/**
 * What a disposed source keeps in place of a callback: the registration still answers `unregister`
 * as .NET's does, true once for a callback that never ran, and what the callback captured can be
 * collected.
 */
const released: Callback = () => {};

/** A delay in milliseconds, or a `TimeSpan`. */
type Delay = number | { readonly totalMilliseconds: number };

/** The longest delay .NET accepts as a `TimeSpan` (`Timer.MaxSupportedTimeout`), in milliseconds. */
const LONGEST_DELAY = 4_294_967_294;

/** The longest a timer waits in one go: a browser's holds a 32-bit signed delay, and fires at once past it. */
const LONGEST_STEP = 2_147_483_647;

/** `CancellationTokenSource`: the side that cancels. */
export class CancellationTokenSource {
  private _cancelled = false;
  private _disposed = false;
  /**
   * In registration order, by the number its registration holds. One taken back is deleted: kept as
   * a null, every registration a long-lived token ever took back stayed in its list.
   */
  private readonly _callbacks = new Map<number, Callback>();
  private _next = 0;
  private _timer: ReturnType<typeof setTimeout> | null = null;
  private readonly _token: CancellationToken;
  /** A linked source's callbacks on the tokens it follows, taken back when it is disposed. */
  private _links: CancellationTokenRegistration[] = [];

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
    const callbacks = [...this._callbacks.values()];
    this._callbacks.clear();
    const errors: unknown[] = [];
    for (let i = callbacks.length - 1; i >= 0; i--) {
      try {
        callbacks[i]();
      } catch (error) {
        errors.push(error);
      }
    }
    if (errors.length === 0) return;
    // Built as `new AggregateException(errors)` is, so its InnerException is the first of them.
    throw exception('System.AggregateException', 'One or more errors occurred.', { innerExceptions: errors });
  }

  /**
   * Cancels after `delay`, replacing any delay set before; -1 cancels none. A `TimeSpan` counts its
   * whole milliseconds, as .NET's `(long)delay.TotalMilliseconds` does, up to the longest .NET takes.
   */
  cancelAfter(delay: Delay): void {
    this.throwIfDisposed();
    const milliseconds = typeof delay === 'number' ? delay : Math.trunc(delay.totalMilliseconds);
    if (milliseconds < -1 || milliseconds > LONGEST_DELAY) {
      throw exception(
        'System.ArgumentOutOfRangeException',
        `Specified argument was out of the range of valid values. (Parameter '${typeof delay === 'number' ? 'millisecondsDelay' : 'delay'}')`,
      );
    }
    if (this._cancelled) return;
    this.stopTimer();
    if (milliseconds === -1) return;
    this.wait(milliseconds);
  }

  /**
   * Waits `milliseconds`, in steps a timer can hold, and cancels. A delay past a browser timer's range
   * (about 24.8 days) fired at once, where .NET waits it out.
   */
  private wait(milliseconds: number): void {
    const step = Math.min(milliseconds, LONGEST_STEP);
    this._timer = setTimeout(() => {
      this._timer = null;
      if (this._disposed) return;
      if (milliseconds > step) this.wait(milliseconds - step);
      else this.cancel();
    }, step);
  }

  dispose(): void {
    this._disposed = true;
    this.stopTimer();
    // A linked source lets go of the tokens it follows, as .NET's does: their callbacks held it until
    // those tokens cancelled, which a token that lives as long as the app never does.
    for (const link of this._links) link.dispose();
    this._links = [];
    // .NET lets go of the callbacks of a source it disposes (`_registrations = null`): a token or a
    // registration kept somewhere held the disposed source, and every capture of its callbacks.
    for (const at of this._callbacks.keys()) this._callbacks.set(at, released);
  }

  /**
   * Registers a callback, or runs it at once when the cancellation has already happened. A callback
   * that ran at once is given back the default registration, as .NET gives it, whose token is `none`:
   * it carried the source's token, so `registration.token.canBeCanceled` said true where .NET says false.
   */
  register(callback: Callback): CancellationTokenRegistration {
    if (this._cancelled) {
      callback();
      return CancellationTokenRegistration.none;
    }
    const at = this._next++;
    this._callbacks.set(at, callback);
    return new CancellationTokenRegistration(this._token, this, at);
  }

  /** Takes back the callback registered as `at`, or answers false when it ran or was taken. */
  unregister(at: number): boolean {
    return this._callbacks.delete(at);
  }

  /** A source cancelled as soon as any of `tokens` is. */
  static createLinkedTokenSource(...tokens: (CancellationToken | CancellationToken[])[]): CancellationTokenSource {
    const all = tokens.flat();
    if (all.length === 0) throw exception('System.ArgumentException', 'No tokens were supplied.');
    const linked = new CancellationTokenSource();
    for (const token of all) {
      linked._links.push(
        token.register(() => {
          if (!linked._disposed) linked.cancel();
        }),
      );
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
    if (this._source === null) return CancellationTokenRegistration.none;
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
  /**
   * `default(CancellationTokenRegistration)`: the registration of nothing, whose token is `none`. It
   * unregisters nothing and disposes as nothing, as .NET's does, where a field of the type that was
   * never assigned was `undefined` and threw on its first `dispose()`.
   */
  static readonly none: CancellationTokenRegistration = new CancellationTokenRegistration(CancellationToken.none, null, -1);

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
