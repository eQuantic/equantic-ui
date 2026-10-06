/**
 * The runtime's cancellation pair, held to .NET's behaviour as measured with `dotnet fsi` (the C#
 * side of the same cases runs in CancellationConformanceTests, against this code transpiled). What
 * only the runtime can show is here too: a delay, which the conformance suite leaves alone because a
 * clock answers differently on each side.
 */

import { afterEach, describe, expect, it, vi } from 'vitest';
import { CancellationToken, CancellationTokenSource } from './cancellation';
import { is } from './exceptions';

describe('CancellationTokenSource and CancellationToken', () => {
  afterEach(() => vi.useRealTimers());

  it('runs the callbacks once, the last registered first', () => {
    const source = new CancellationTokenSource();
    let log = '';
    source.token.register(() => (log += 'a'));
    source.token.register(() => (log += 'b'));
    source.token.register(() => (log += 'c'));

    source.cancel();
    source.cancel();

    expect(log).toBe('cba');
    expect(source.isCancellationRequested).toBe(true);
    expect(source.token.isCancellationRequested).toBe(true);
  });

  it('runs a callback registered after the cancellation at once, and gives back the default registration', () => {
    const source = new CancellationTokenSource();
    source.cancel();
    let ran = false;

    const registration = source.token.register(() => (ran = true));

    expect(ran).toBe(true);
    // .NET's default(CancellationTokenRegistration), whose token is None and cannot be cancelled.
    expect(registration.token.equals(CancellationToken.none)).toBe(true);
    expect(registration.token.canBeCanceled).toBe(false);
    expect(registration.unregister()).toBe(false);
  });

  it('runs no callback whose registration was disposed, and unregisters once', () => {
    const source = new CancellationTokenSource();
    let log = '';
    const registration = source.token.register(() => (log += 'x'));

    expect(registration.token.equals(source.token)).toBe(true);
    expect(registration.unregister()).toBe(true);
    expect(registration.unregister()).toBe(false);
    source.cancel();

    expect(log).toBe('');
  });

  it('gathers the callbacks that throw into one AggregateException, after the others ran', () => {
    const source = new CancellationTokenSource();
    let log = '';
    source.token.register(() => (log += '1'));
    source.token.register(() => {
      throw new Error('boom');
    });
    source.token.register(() => (log += '3'));

    let thrown: unknown;
    try {
      source.cancel();
    } catch (error) {
      thrown = error;
    }

    expect(log).toBe('31');
    expect(is(thrown, 'System.AggregateException')).toBe(true);
    expect((thrown as Error).message).toBe('One or more errors occurred. (boom)');
  });

  it('throws an OperationCanceledException that names its token', () => {
    const source = new CancellationTokenSource();
    source.token.throwIfCancellationRequested();
    source.cancel();

    let thrown: unknown;
    try {
      source.token.throwIfCancellationRequested();
    } catch (error) {
      thrown = error;
    }

    expect(is(thrown, 'System.OperationCanceledException')).toBe(true);
    expect(is(thrown, 'System.SystemException')).toBe(true);
    expect((thrown as Error).message).toBe('The operation was canceled.');
    expect((thrown as { cancellationToken: CancellationToken }).cancellationToken).toBe(source.token);
  });

  it('refuses to cancel, or to hand out its token, once disposed', () => {
    const source = new CancellationTokenSource();
    source.dispose();

    expect(() => source.cancel()).toThrow('The CancellationTokenSource has been disposed.');
    expect(() => source.token).toThrow('The CancellationTokenSource has been disposed.');
  });

  it('has one token that never cancels, and one that always has', () => {
    expect(CancellationToken.none.isCancellationRequested).toBe(false);
    expect(CancellationToken.none.canBeCanceled).toBe(false);
    expect(CancellationToken.of(false)).toBe(CancellationToken.none);
    expect(CancellationToken.of(true)).toBe(CancellationToken.of(true));
    expect(CancellationToken.of(true).isCancellationRequested).toBe(true);
    expect(CancellationToken.of(true).canBeCanceled).toBe(true);
    expect(new CancellationTokenSource().token.equals(new CancellationTokenSource().token)).toBe(false);
  });

  it('links a source to the tokens it was made from', () => {
    const outer = new CancellationTokenSource();
    let log = '';
    const linked = CancellationTokenSource.createLinkedTokenSource(outer.token, CancellationToken.none);
    linked.token.register(() => (log += 'L'));
    outer.token.register(() => (log += 'A'));

    outer.cancel();

    expect(linked.isCancellationRequested).toBe(true);
    expect(log).toBe('AL');
    expect(CancellationTokenSource.createLinkedTokenSource([CancellationToken.of(true)]).isCancellationRequested).toBe(true);
    expect(() => CancellationTokenSource.createLinkedTokenSource([])).toThrow('No tokens were supplied.');
  });

  it('lets go of the tokens a linked source follows once it is disposed, and keeps no slot for them', () => {
    // A token that lives as long as the app, and a linked source per request, disposed after it.
    const app = new CancellationTokenSource();
    for (let i = 0; i < 1000; i++) CancellationTokenSource.createLinkedTokenSource(app.token).dispose();
    const kept = app.token.register(() => {});
    kept.dispose();

    // What the app's source still holds: nothing, as .NET's linked source unregisters when disposed.
    expect((app as unknown as { _callbacks: Map<number, unknown> })._callbacks.size).toBe(0);
    let ran = false;
    const live = CancellationTokenSource.createLinkedTokenSource(app.token);
    live.token.register(() => (ran = true));
    app.cancel();
    expect(ran).toBe(true);
  });

  it('cancels after a delay, and a newer delay replaces the older', () => {
    vi.useFakeTimers();
    const source = new CancellationTokenSource(100);
    source.cancelAfter(300);

    vi.advanceTimersByTime(299);
    expect(source.isCancellationRequested).toBe(false);
    vi.advanceTimersByTime(1);
    expect(source.isCancellationRequested).toBe(true);

    const never = new CancellationTokenSource();
    never.cancelAfter({ totalMilliseconds: 50 });
    never.cancelAfter(-1);
    vi.advanceTimersByTime(1000);
    expect(never.isCancellationRequested).toBe(false);
    expect(() => never.cancelAfter(-2)).toThrow("(Parameter 'millisecondsDelay')");
  });
});
