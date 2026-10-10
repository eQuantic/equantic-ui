import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { Text } from '@equantic/runtime';
import { StatefulComponent } from '../core/component';
import { capturePageState } from './hot-reload-state';

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
    sessionStorage.setItem(marker, JSON.stringify({ url: location.href, pages: {} }));
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

/**
 * A replay hands the page what it held, and the server-data door adopts none of that again on the
 * first render. The boot used to copy every captured field into the root's server payload, so the
 * first render's adoption (`runComponentWalk` -> `applyServerFields`) wrote back a field the replay
 * had left at its initializer, a number into the string field the edit had made of it, and the
 * root's own server members, which the capture never holds, were dropped with the payload they came
 * in (Copilot's second round on #672). Driven through the real boot, a page module and a mount.
 */
describe('a hot reload replay and the server-data door', () => {
  const marker = '__eq_hmr__';
  const module = '/_equantic/Sized.js';
  const w = window as unknown as {
    __EQ_CONFIG?: unknown;
    __EQ_DEV__?: boolean;
    __INITIAL_STATE__?: Record<string, Record<string, unknown>>;
  };

  /** The page before the save: `_size` an int. */
  class Sized extends StatefulComponent {
    static $typeId = 'App.Sized';
    static get $hydration() {
      return { _size: 'declared', _count: 'declared', label: 'declared' };
    }
    _size: number | string = 3;
    _count = 0;
    build() {
      return new Text(`Size: ${String(this._size)}`, 'title');
    }
  }

  /** The same page after it: `_size` a string, and `label` a member the server alone hands it. */
  class Resized extends StatefulComponent {
    static $typeId = 'App.Sized';
    static get $hydration() {
      return { _size: 'declared', _count: 'declared', label: 'declared' };
    }
    static mounted: Resized | null = null;
    _size: number | string = 'large';
    _count = 0;
    declare label: string | undefined;
    constructor() {
      super();
      Resized.mounted = this;
    }
    build() {
      return new Text(`Size: ${String(this._size)}`, 'title');
    }
  }

  beforeEach(() => {
    vi.spyOn(console, 'log').mockImplementation(() => {});
    document.body.innerHTML = '<div id="app"></div>';
    Resized.mounted = null;
  });

  afterEach(() => {
    sessionStorage.removeItem(marker);
    delete w.__EQ_CONFIG;
    delete w.__INITIAL_STATE__;
    document.body.innerHTML = '';
    vi.doUnmock(module);
    vi.restoreAllMocks();
  });

  it('keeps a field the replay left at its initializer, and adopts the server members it did not decide', async () => {
    const before = new Sized();
    before._size = 5;
    before._count = 4;
    sessionStorage.setItem(
      marker,
      JSON.stringify({ url: location.href, pages: { 'App.Sized#0': capturePageState(before) } }),
    );
    // What the server rendered the reloaded page with: its own values for the root, and a child's.
    w.__INITIAL_STATE__ = {
      'App.Sized#0': { _size: 7, _count: 9, label: 'from server' },
      'App.Child#0': { _x: 1 },
    };
    w.__EQ_CONFIG = { page: 'Sized', hotReload: true };
    w.__EQ_DEV__ = false;
    vi.doMock(module, () => ({ Sized: Resized }));
    vi.resetModules();
    const { boot } = await import('../../../eQuantic.UI.Sdk/Resources/boot');
    await boot();

    const page = Resized.mounted!;
    expect(page._size).toBe('large');
    expect(page._count).toBe(4);
    expect(page.label).toBe('from server');
    expect(document.getElementById('app')!.textContent).toBe('Size: large');
    expect(w.__INITIAL_STATE__?.['App.Child#0']).toEqual({ _x: 1 });
  });
});
