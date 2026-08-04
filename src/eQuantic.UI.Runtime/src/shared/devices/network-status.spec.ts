import { afterEach, describe, expect, it, vi } from 'vitest';
import { WebNetworkStatus } from './network-status';

/**
 * The browser realization of INetworkStatus. `navigator.onLine` is the whole source, so the kind
 * is deliberately never guessed — `other` while online, `none` while not.
 */
describe('WebNetworkStatus', () => {
  afterEach(() => vi.unstubAllGlobals());

  it('reads the current state without waiting', () => {
    vi.stubGlobal('navigator', { onLine: true });
    expect(new WebNetworkStatus().current).toEqual({ online: true, kind: 'other' });

    vi.stubGlobal('navigator', { onLine: false });
    expect(new WebNetworkStatus().current).toEqual({ online: false, kind: 'none' });
  });

  it('streams the flip when the browser says so', () => {
    const status = new WebNetworkStatus();
    const seen: boolean[] = [];
    status.subscribe((state) => seen.push(state.online));

    vi.stubGlobal('navigator', { onLine: false });
    window.dispatchEvent(new Event('offline'));
    vi.stubGlobal('navigator', { onLine: true });
    window.dispatchEvent(new Event('online'));

    expect(seen).toEqual([false, true]);
  });

  it('a disposed subscription hears nothing more', () => {
    const status = new WebNetworkStatus();
    const seen: boolean[] = [];
    const subscription = status.subscribe((state) => seen.push(state.online));

    subscription.dispose();
    subscription.dispose(); // twice is allowed, matching the C# contract

    window.dispatchEvent(new Event('offline'));
    expect(seen).toEqual([]);
  });
});
