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
      public constructor(url: string) {
        opened.push(url);
      }
      public addEventListener(): void {}
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
    const { boot: start } = await import('../../../eQuantic.UI.Sdk/Resources/boot.dev');
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
    const { boot } = await import('../../../eQuantic.UI.Sdk/Resources/boot.dev');
    await boot();

    const page = Resized.mounted!;
    expect(page._size).toBe('large');
    expect(page._count).toBe(4);
    expect(page.label).toBe('from server');
    expect(document.getElementById('app')!.textContent).toBe('Size: large');
    expect(w.__INITIAL_STATE__?.['App.Child#0']).toEqual({ _x: 1 });
  });
});

/**
 * The stream is the page's ONE source of reloads (#1275): dotnet watch's own refresh, which reloaded
 * each page a second time with the old modules, is kept out of it. So what it says decides
 * everything: which run of the server the page talks to, when the modules are new and whether the
 * server's markup holds the edit, and why a rebuild failed. Driven through the real boot, with a
 * stand-in stream the test speaks for.
 */
describe('what the rebuild stream says, and what the page does about it', () => {
  const marker = '__eq_hmr__';

  /** The stream the boot opened, which the test speaks for. */
  class Stream {
    static opened: Stream | null = null;
    private readonly listeners = new Map<string, ((event: MessageEvent<string>) => void)[]>();
    public constructor() {
      Stream.opened = this;
    }
    public addEventListener(name: string, listener: (event: MessageEvent<string>) => void): void {
      this.listeners.set(name, [...(this.listeners.get(name) ?? []), listener]);
    }
    public say(name: string, data: string): void {
      for (const listener of this.listeners.get(name) ?? [])
        listener(new MessageEvent(name, { data }));
    }
    public close(): void {}
  }

  let reload: ReturnType<typeof vi.spyOn>;

  beforeEach(async () => {
    (globalThis as { EventSource?: unknown }).EventSource = Stream;
    Stream.opened = null;
    reload = vi.spyOn(window.location, 'reload').mockImplementation(() => {});
    // The boot reports a missing root and returns, after it opened the stream.
    vi.spyOn(console, 'error').mockImplementation(() => {});
    window.__EQ_CONFIG = { hotReload: true };
    window.__EQ_DEV__ = false;
    vi.resetModules();
    const { boot } = await import('../../../eQuantic.UI.Sdk/Resources/boot.dev');
    await boot();
  });

  afterEach(() => {
    sessionStorage.removeItem(marker);
    document.getElementById('equantic-error-overlay')?.remove();
    delete (window as { __EQ_CONFIG?: unknown }).__EQ_CONFIG;
    vi.restoreAllMocks();
  });

  const left = () =>
    JSON.parse(sessionStorage.getItem(marker) ?? 'null') as { hydrate?: boolean } | null;

  it.each([
    ['hydrate', true],
    ['mount', false],
  ])('a rebuild that says %s reloads the page once, and tells the next boot', (said, hydrate) => {
    Stream.opened!.say('rebuilt', said);

    expect(reload).toHaveBeenCalledTimes(1);
    expect(left()?.hydrate).toBe(hydrate);
  });

  it('reloads when it reconnects to another run of the server, and only then', () => {
    Stream.opened!.say('server', 'first');
    // The same run again: a transient drop the stream recovered from.
    Stream.opened!.say('server', 'first');
    expect(reload).not.toHaveBeenCalled();

    // Another run: the server restarted, and built the markup and the modules it serves.
    Stream.opened!.say('server', 'second');
    expect(reload).toHaveBeenCalledTimes(1);
    expect(left()?.hydrate).toBe(true);
  });

  it('asks for one reload however many reasons arrive before it lands', () => {
    Stream.opened!.say('rebuilt', 'hydrate');
    Stream.opened!.say('server', 'a');
    Stream.opened!.say('server', 'b');
    Stream.opened!.say('rebuilt', 'mount');

    expect(reload).toHaveBeenCalledTimes(1);
    expect(left()?.hydrate).toBe(true);
  });

  it('shows the errors of a rebuild that failed over the page, and does not reload it', () => {
    Stream.opened!.say(
      'failed',
      JSON.stringify(['Screens/A.cs(3,7): error CS1002: ; expected', '<b>x</b>']),
    );

    const overlay = document.getElementById('equantic-error-overlay')!;
    expect(overlay.textContent).toContain('Build Failed');
    expect(overlay.textContent).toContain('Screens/A.cs(3,7): error CS1002: ; expected');
    // Text, never markup: the compiler quotes the source it read.
    expect(overlay.textContent).toContain('<b>x</b>');
    expect(reload).not.toHaveBeenCalled();
  });
});

/**
 * The boot that follows a rebuild's reload HYDRATES when the server's markup holds the edit, and
 * replays the page's fields after it, as a change the next render draws (#1275). The server rendered
 * the page with its own state, so fields restored before the build would make a tree the markup does
 * not hold. When the markup is from before the edit, the page is drawn over it with the fields
 * restored first, as before.
 */
describe('the boot after a rebuild, against the markup the server sent', () => {
  const marker = '__eq_hmr__';
  const module = '/_equantic/Counted.js';

  class Counted extends StatefulComponent {
    static $typeId = 'App.Counted';
    static get $hydration() {
      return { _count: 'declared' };
    }
    _count = 0;
    build() {
      return new Text(`Count: ${this._count}`, 'title');
    }
  }

  let warned: unknown[][];

  beforeEach(() => {
    warned = [];
    vi.spyOn(console, 'log').mockImplementation(() => {});
    vi.spyOn(console, 'warn').mockImplementation((...args: unknown[]) => {
      warned.push(args);
    });
    // The markup the server rendered: the page with its own state, as the new modules draw it.
    const server = document.createElement('div');
    new Counted().mount(server);
    document.body.innerHTML = `<div id="app" data-ssr="true">${server.innerHTML}</div>`;
  });

  afterEach(() => {
    sessionStorage.removeItem(marker);
    delete (window as { __EQ_CONFIG?: unknown }).__EQ_CONFIG;
    document.body.innerHTML = '';
    vi.doUnmock(module);
    vi.restoreAllMocks();
  });

  async function bootAfterRebuild(hydrate: boolean): Promise<void> {
    const before = new Counted();
    before._count = 4;
    sessionStorage.setItem(
      marker,
      JSON.stringify({
        url: location.href,
        pages: { 'App.Counted#0': capturePageState(before) },
        hydrate,
      }),
    );
    window.__EQ_CONFIG = { page: 'Counted', hotReload: true };
    window.__EQ_DEV__ = false;
    vi.doMock(module, () => ({ Counted }));
    vi.resetModules();
    const { boot } = await import('../../../eQuantic.UI.Sdk/Resources/boot.dev');
    await boot();
    // The render the replay asked for.
    await new Promise((resolve) => setTimeout(resolve, 50));
  }

  it('hydrates markup that holds the edit, then draws the fields the reload carried', async () => {
    const served = document.getElementById('app')!.firstElementChild;

    await bootAfterRebuild(true);

    expect(document.getElementById('app')!.firstElementChild).toBe(served);
    expect(document.getElementById('app')!.textContent).toBe('Count: 4');
    expect(warned.flat().join(' ')).not.toContain('Hydration');
  });

  it('draws over markup from before the edit, with the fields restored first', async () => {
    const served = document.getElementById('app')!.firstElementChild;

    await bootAfterRebuild(false);

    expect(document.getElementById('app')!.firstElementChild).not.toBe(served);
    expect(document.getElementById('app')!.textContent).toBe('Count: 4');
  });
});
