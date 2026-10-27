/**
 * The development overlay as a developer meets it (#1108): titled by what was raised, silent on the
 * browser's ResizeObserver notice, and never showing a JavaScript frame — while the maps load, after
 * they fail, or for a frame inside the SDK, which it names as the SDK's and asks to be reported.
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ErrorOverlay, errorOverlay, installErrorOverlay } from './error-overlay';
import type { RawSourceMap } from './source-map';

const ORIGIN = 'http://localhost:5000';
const RUNTIME = `${ORIGIN}/_equantic/runtime.js?v=1`;

/** The served runtime's map as the Server's build composes it: from src/, no text. Column 0 of line 1
 * leads to SheetController.cs:1, column 10 to the hand-written lowering. */
const RUNTIME_MAP: RawSourceMap = {
  version: 3,
  sources: [
    'eQuantic.UI.Sheet/Editing/SheetController.cs',
    'eQuantic.UI.Runtime/src/shared/lowering.ts',
  ],
  names: [],
  mappings: 'AAAA,UCAA',
};

/** A page's module map, as eqc composes it in a Debug build: line 1 leads to Pages/Home.cs:12. */
const HOME_MAP: RawSourceMap = {
  version: 3,
  sources: ['Pages/Home.cs'],
  sourcesContent: [
    Array.from({ length: 14 }, (_, i) =>
      i === 11 ? '        _grid.Select(null!);' : `// ${i + 1}`,
    ).join('\n'),
  ],
  names: [],
  mappings: 'AAWA',
};

const SDK_STACK = [
  "TypeError: Cannot read properties of undefined (reading 'defaultPrevented')",
  `    at notTheSheets (${RUNTIME}:1:11)`,
  `    at SheetController.moveTo (${RUNTIME}:1:1)`,
  `    at HTMLDivElement.<anonymous> (${RUNTIME}:1:11)`,
].join('\n');

const maps: Record<string, RawSourceMap> = {
  [`${ORIGIN}/_equantic/runtime.js.map`]: RUNTIME_MAP,
  [`${ORIGIN}/_equantic/Home.js.map`]: HOME_MAP,
};

const answering = (served: Record<string, RawSourceMap>) => (url: string) =>
  Promise.resolve(
    url in served
      ? { ok: true, json: () => Promise.resolve(served[url]) }
      : { ok: false, json: () => Promise.reject(new Error('404')) },
  );

const card = () => document.getElementById('equantic-error-overlay');
const stackText = () => document.getElementById('eq-stack-trace')?.textContent ?? '';
const named = () =>
  vi.waitFor(() => expect(document.querySelector('#eq-stack-trace .stack-line')).not.toBeNull());

beforeEach(() => {
  (window as unknown as { happyDOM?: { setURL?: (u: string) => void } }).happyDOM?.setURL?.(
    `${ORIGIN}/sheets?token=secret`,
  );
  window.__EQ_DEV__ = true;
  window.__EQ_CONFIG = { sdkVersion: '0.2.0-preview.66+4f1b2c3' };
  vi.stubGlobal('fetch', answering(maps));
});

afterEach(() => {
  errorOverlay.clear();
  document.getElementById('equantic-error-overlay')?.remove();
  vi.unstubAllGlobals();
  window.__EQ_DEV__ = undefined;
  window.__EQ_CONFIG = undefined;
});

describe('the overlay names every frame by its owner', () => {
  it("names an exception raised inside the SDK by the SDK's C# and module, and says the fault is the SDK's", async () => {
    const overlay = new ErrorOverlay(RUNTIME);
    overlay.show({ kind: 'exception', message: 'TypeError: boom', stack: SDK_STACK });
    await named();

    const stack = stackText();
    expect(stack).toContain('notTheSheets (eQuantic.UI runtime › shared/lowering)');
    expect(stack).toContain(
      'SheetController.moveTo (eQuantic.UI.Sheet/Editing/SheetController.cs:1)',
    );
    expect(stack).not.toMatch(/\.js\b|:1:11/);

    const fault = document.getElementById('eq-fault')?.textContent ?? '';
    expect(fault).toContain('This error was raised inside eQuantic.UI');
    expect(fault).toContain("No code of your app is on the call stack: the fault is the SDK's");
    overlay.clear();
  });

  it('hands over a report naming the SDK, the page without its query, and every frame', async () => {
    const writeText = vi.fn(() => Promise.resolve());
    vi.stubGlobal('navigator', {
      ...navigator,
      userAgent: 'TestBrowser/1',
      clipboard: { writeText },
    });
    const overlay = new ErrorOverlay(RUNTIME);
    overlay.show({ kind: 'exception', message: 'TypeError: boom', stack: SDK_STACK });
    await named();

    document.querySelector<HTMLButtonElement>('[data-eq-copy]')!.click();

    expect(writeText).toHaveBeenCalledTimes(1);
    const report = (writeText.mock.calls[0] as unknown as [string])[0];
    expect(report).toContain('SDK: 0.2.0-preview.66+4f1b2c3');
    expect(report).toContain('Page: /sheets');
    expect(report).not.toContain('secret');
    expect(report).toContain('Browser: TestBrowser/1');
    expect(report).toContain('Unhandled Runtime Error: TypeError: boom');
    expect(report).toContain(
      '  at notTheSheets — eQuantic.UI runtime › shared/lowering:1 [eQuantic.UI]',
    );
    expect(report).toContain(
      '  at SheetController.moveTo — eQuantic.UI.Sheet/Editing/SheetController.cs:1 [eQuantic.UI]',
    );
    expect(report).not.toMatch(/\.js\b/);
    overlay.clear();
  });

  it("names the app's call as the first thing to check when the SDK threw inside it, and shows its C#", async () => {
    const overlay = new ErrorOverlay(RUNTIME);
    const stack = [
      'TypeError: boom',
      `    at SheetController.moveTo (${RUNTIME}:1:1)`,
      `    at Home.onPressed (${ORIGIN}/_equantic/Home.js?v=1:1:1)`,
    ].join('\n');
    overlay.show({ kind: 'exception', message: 'TypeError: boom', stack });
    await named();

    expect(document.getElementById('eq-fault')?.textContent).toContain(
      'your code at Pages/Home.cs:12: check that call first',
    );
    expect(stackText()).toContain('Home.onPressed (Pages/Home.cs:12)');
    const source = document.getElementById('eq-source')?.textContent ?? '';
    expect(source).toContain('Source Context Pages/Home.cs:12');
    expect(source).toContain('_grid.Select(null!);');
    overlay.clear();
  });

  it("raises no SDK banner for an exception thrown in the app's own C#", async () => {
    const overlay = new ErrorOverlay(RUNTIME);
    overlay.show({
      kind: 'exception',
      message: 'Error: mine',
      stack: `Error: mine\n    at Home.onPressed (${ORIGIN}/_equantic/Home.js?v=1:1:1)`,
    });
    await named();

    expect(stackText()).toContain('Home.onPressed (Pages/Home.cs:12)');
    expect(document.getElementById('eq-fault')?.textContent?.trim()).toBe('');
    overlay.clear();
  });

  it('shows no frame before it is named, and names every frame by its owner when every map fails', async () => {
    let release: () => void = () => {};
    const gate = new Promise<void>((resolve) => (release = resolve));
    vi.stubGlobal('fetch', () => gate.then(() => Promise.reject(new TypeError('offline'))));
    const overlay = new ErrorOverlay(RUNTIME);
    overlay.show({
      kind: 'exception',
      message: 'TypeError: boom',
      stack: `${SDK_STACK}\n    at Dashboard.build (${ORIGIN}/_equantic/Dashboard.js?v=1:3:20)`,
    });

    // While the maps load, the stack the browser handed over is not drawn.
    expect(stackText()).toBe('Reading the call stack…');
    expect(card()?.textContent).not.toMatch(/runtime\.js|:1:11/);

    release();
    await named();

    const stack = stackText();
    expect(stack).toContain('notTheSheets (eQuantic.UI runtime)');
    expect(stack).toContain('Dashboard.build (Dashboard)');
    expect(card()?.textContent).not.toMatch(/\.js\b|:\d+:\d+/);
    overlay.clear();
  });

  it('drops the answer of an earlier error whose maps were still loading', async () => {
    let release: () => void = () => {};
    const gate = new Promise<void>((resolve) => (release = resolve));
    let first = true;
    vi.stubGlobal('fetch', (url: string) => {
      if (first) {
        first = false;
        return gate.then(() => answering(maps)(url));
      }
      return answering(maps)(url);
    });
    const overlay = new ErrorOverlay(RUNTIME);
    overlay.show({ kind: 'exception', message: 'first', stack: SDK_STACK });
    overlay.show({
      kind: 'exception',
      message: 'second',
      stack: `Error: second\n    at Home.onPressed (${ORIGIN}/_equantic/Home.js?v=1:1:1)`,
    });
    await named();
    release();
    await new Promise((resolve) => setTimeout(resolve, 0));

    expect(stackText()).toContain('Pages/Home.cs:12');
    expect(stackText()).not.toContain('notTheSheets');
    overlay.clear();
  });
});

describe('the overlay is opened by what was raised', () => {
  installErrorOverlay();

  it('titles an uncaught exception as one, and never as a build error', () => {
    window.dispatchEvent(
      new ErrorEvent('error', { message: 'Uncaught Error: boom', error: new Error('boom') }),
    );

    expect(card()?.querySelector('.header-title')?.textContent).toContain(
      'Unhandled Runtime Error',
    );
    expect(card()?.textContent).not.toContain('Build Error');
  });

  it('says an exception nothing awaited came from an asynchronous call, naming no promise', () => {
    const event = new Event('unhandledrejection');
    Object.defineProperty(event, 'reason', { value: new Error('late') });
    window.dispatchEvent(event);

    expect(card()?.querySelector('.header-title')?.textContent).toContain(
      'Unhandled Runtime Error',
    );
    expect(card()?.textContent).toContain('Raised by an asynchronous call that nothing awaited.');
    expect(card()?.querySelector('.error-message')?.textContent).toBe('Error: late');
    expect(card()?.textContent).not.toMatch(/promise/i);
  });

  it("opens nothing for the browser's ResizeObserver loop notice", () => {
    window.dispatchEvent(
      new ErrorEvent('error', {
        message: 'ResizeObserver loop completed with undelivered notifications.',
        error: null,
      }),
    );
    window.dispatchEvent(
      new ErrorEvent('error', { message: 'ResizeObserver loop limit exceeded' }),
    );

    expect(card()).toBeNull();
  });

  it('closes on its button, and opens again for the next error', () => {
    window.dispatchEvent(new ErrorEvent('error', { message: 'first', error: new Error('first') }));
    card()!.querySelector<HTMLButtonElement>('[data-eq-dismiss]')!.click();
    expect(card()).toBeNull();

    window.dispatchEvent(
      new ErrorEvent('error', { message: 'second', error: new Error('second') }),
    );
    expect(card()?.querySelector('.error-message')?.textContent).toBe('second');
  });
});
