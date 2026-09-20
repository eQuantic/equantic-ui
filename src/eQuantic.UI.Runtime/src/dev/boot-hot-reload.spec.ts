import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

/**
 * WHAT A PRODUCTION PAGE DOES NOT DO. Hot reload is a developer facility in both halves — a stream
 * the server maps only in development, and a sessionStorage marker only that stream writes — and
 * neither asked `isDev()`. Every shipped app therefore logged `GET /_equantic/hmr 404` on every
 * page load (#240), which Lighthouse's best-practices audit flags and every error monitor reports.
 *
 * The boot had no test of any kind before this, which is how a defect this visible shipped: it
 * lives outside the runtime's own `src`, so nothing here imported it. This file does, which puts
 * it inside `tsc` too.
 *
 * The development case is not a courtesy — it is what makes the production one mean anything. A
 * gate that turned the stream off ALTOGETHER would pass the first test on its own.
 */
describe('the boot only reaches for hot reload when a developer is watching', () => {
  const marker = '__eq_hmr__';
  let opened: string[];

  beforeEach(() => {
    opened = [];
    // A stub rather than happy-dom's own: the assertion is about the URL a boot ASKS FOR, and the
    // environment provides no EventSource at all — so without this the gate would look kept by a
    // boot that never had the chance to open anything.
    (globalThis as { EventSource?: unknown }).EventSource = class {
      public onmessage: unknown = null;
      public onerror: unknown = null;
      public constructor(url: string) {
        opened.push(url);
      }
      public close(): void {}
    };
    sessionStorage.setItem(marker, JSON.stringify({ url: location.href, state: {} }));
    // The boot reports a missing root and returns, which is the early exit this test wants; the
    // spy keeps that expected line out of the run's output.
    vi.spyOn(console, 'error').mockImplementation(() => {});
  });

  afterEach(() => {
    sessionStorage.removeItem(marker);
    vi.restoreAllMocks();
  });

  async function bootAsDev(dev: boolean): Promise<void> {
    (window as { __EQ_DEV__?: boolean }).__EQ_DEV__ = dev;
    // `initialized` is module state and a second boot() returns at once, so each case takes a
    // fresh module.
    vi.resetModules();
    const { boot } = await import('../../../eQuantic.UI.Sdk/Resources/boot');
    await boot();
  }

  it('a production boot opens no stream and does not even read the marker', async () => {
    await bootAsDev(false);

    expect(opened).toEqual([]);
    expect(sessionStorage.getItem(marker)).not.toBeNull();
  });

  it('a development boot opens exactly one, and consumes the marker', async () => {
    await bootAsDev(true);

    expect(opened).toEqual(['/_equantic/hmr']);
    expect(sessionStorage.getItem(marker)).toBeNull();
  });
});
