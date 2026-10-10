import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

/**
 * WHAT A PRODUCTION PAGE DOES NOT DO. Hot reload is a developer facility in both halves — a stream
 * the server maps only when it streams rebuilds, and a sessionStorage marker only that stream
 * writes — and neither asked anything. Every shipped app therefore logged
 * `GET /_equantic/hmr 404` on every page load (#240), which Lighthouse's best-practices audit flags
 * and every error monitor reports.
 *
 * The question is the SERVER's decision, sent as `hotReload` in the configuration, not the
 * environment: `__EQ_DEV__` agreed with it only while the app left `HotReload` unset, and under
 * `dotnet watch` outside Development the server streams rebuilds to a page that would never have
 * listened (#627). So each case sets the two to disagree.
 *
 * The boot had no test of any kind before this, which is how a defect this visible shipped: it
 * lives outside the runtime's own `src`, so nothing here imported it. This file does, which puts
 * it inside `tsc` too.
 *
 * The listening case is not a courtesy — it is what makes the other one mean anything. A gate that
 * turned the stream off ALTOGETHER would pass the first test on its own.
 */
describe('the boot only reaches for hot reload when the server streams rebuilds', () => {
  const marker = '__eq_hmr__';
  let opened: string[];
  let askedFor: string[];

  beforeEach(() => {
    opened = [];
    askedFor = [];
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
    // Every READ is recorded, not just the removal. Leaving the marker in place is what a boot
    // that skipped the replay looks like — and also what a boot that read it and found nothing to
    // do looks like, so the removal alone pins only half the contract.
    const read = sessionStorage.getItem.bind(sessionStorage);
    vi.spyOn(sessionStorage, 'getItem').mockImplementation((key: string) => {
      askedFor.push(key);
      return read(key);
    });
    // The boot reports a missing root and returns, which is the early exit this test wants; the
    // spy keeps that expected line out of the run's output.
    vi.spyOn(console, 'error').mockImplementation(() => {});
  });

  afterEach(() => {
    sessionStorage.removeItem(marker);
    delete (window as { __EQ_CONFIG?: unknown }).__EQ_CONFIG;
    vi.restoreAllMocks();
  });

  async function boot(hotReload: boolean | undefined, dev: boolean): Promise<void> {
    window.__EQ_CONFIG = hotReload === undefined ? {} : { hotReload };
    window.__EQ_DEV__ = dev;
    // `initialized` is module state and a second boot() returns at once, so each case takes a
    // fresh module.
    vi.resetModules();
    const { boot: start } = await import('../../../eQuantic.UI.Sdk/Resources/boot');
    await start();
  }

  it.each([
    ['a production page', undefined, false],
    // An app that turned HotReload off in Development: the server maps no stream.
    ['a development page whose server streams no rebuilds', false, true],
  ])('%s opens no stream and does not even read the marker', async (_, hotReload, dev) => {
    await boot(hotReload, dev);
    // Snapshot BEFORE this test reaches for the marker itself — the assertion is about what the
    // boot asked for.
    const duringBoot = [...askedFor];

    expect(opened).toEqual([]);
    expect(duringBoot).not.toContain(marker);
    expect(sessionStorage.getItem(marker)).not.toBeNull();
  });

  it.each([
    ['a development page', true, true],
    // dotnet watch outside Development, the run of an app with no launch profile (#627).
    ['a page under dotnet watch in another environment', true, false],
  ])('%s opens exactly one, reads the marker and consumes it', async (_, hotReload, dev) => {
    await boot(hotReload, dev);
    const duringBoot = [...askedFor];

    expect(opened).toEqual(['/_equantic/hmr']);
    expect(duringBoot).toContain(marker);
    expect(sessionStorage.getItem(marker)).toBeNull();
  });
});
