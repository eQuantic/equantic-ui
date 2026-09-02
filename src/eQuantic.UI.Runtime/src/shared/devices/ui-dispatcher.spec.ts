/**
 * The web realization of `IUiDispatcher` exists to say there is nothing to marshal: JavaScript runs
 * the page on one thread, so `SetState` stays inline here while the native hosts post through a
 * queue. What it still owes is a `post` that defers.
 */

import { describe, expect, it } from 'vitest';
import { WebUiDispatcher } from './ui-dispatcher';

describe('WebUiDispatcher (IUiDispatcher realization)', () => {
  it('is always on the UI thread — the page has one', () => {
    expect(new WebUiDispatcher().isOnUiThread).toBe(true);
  });

  it('defers posted work rather than running it inline', async () => {
    const order: string[] = [];
    new WebUiDispatcher().post(() => order.push('posted'));
    order.push('after the call');

    await Promise.resolve();

    expect(order).toEqual(['after the call', 'posted']);
  });

  it('runs posted work before the next paint (a microtask, not a timer)', async () => {
    let ran = false;
    new WebUiDispatcher().post(() => { ran = true; });
    // One microtask turn is enough — a setTimeout-based implementation would still be false here.
    await Promise.resolve();
    expect(ran).toBe(true);
  });
});
