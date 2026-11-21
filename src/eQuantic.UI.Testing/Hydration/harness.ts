/**
 * A served page, drawn as a browser draws it, for a .NET test (#1775): `HydrationAssert` starts this
 * with the embedded bun the app was built with, and reads what it prints.
 *
 * The document is fetched from the app's server and parsed into happy-dom, the DOM the runtime's own tests
 * run in, registered as this process's globals the way Vitest's happy-dom environment registers it. Its
 * classic scripts run (the page's configuration, its culture, its server data), then its module scripts:
 * the runtime, the boot and the page's module, loaded by bun's own ES modules from the same server through
 * a plugin, with the import map applied as a browser applies it. Hydration then adopts the server's markup.
 * happy-dom's own module loader is not used: it parses modules itself, and the runtime's bundle is beyond it.
 *
 * Once the boot has written what hydration did (`window.__EQ_HYDRATION__`) and what runs right after it has
 * run, this prints one JSON object: the address, that record (null when the boot never wrote one), and
 * everything the page said in its console. It judges nothing: the .NET side does.
 *
 * Arguments: the page's address, the time to wait for the boot in milliseconds, and the window's width.
 */
import { GlobalWindow } from 'happy-dom';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { unlink } from 'node:fs/promises';
import { pathToFileURL } from 'node:url';

type Level = 'log' | 'info' | 'warn' | 'error';

const [address, timeoutText = '30000', widthText = '1280'] = process.argv.slice(2);
if (!address) {
  process.stderr.write('usage: harness <address> [timeout ms] [width]\n');
  process.exit(2);
}

const said: { level: Level; text: string }[] = [];

function text(part: unknown): string {
  if (typeof part === 'string') return part;
  // An error of the page's realm need not be an Error of this one, so it is read by its shape.
  if (part !== null && typeof part === 'object' && 'message' in part) {
    const error = part as { name?: string; message?: string; stack?: string };
    return error.stack ?? `${error.name ?? 'Error'}: ${error.message}`;
  }
  try {
    return JSON.stringify(part);
  } catch {
    return String(part);
  }
}

function say(level: Level, parts: unknown[]): void {
  said.push({ level, text: parts.map(text).join(' ') });
}

// What the page says is kept, never printed: this process's output is the report alone.
const recorder = {
  log: (...parts: unknown[]) => say('log', parts),
  info: (...parts: unknown[]) => say('info', parts),
  debug: (...parts: unknown[]) => say('log', parts),
  warn: (...parts: unknown[]) => say('warn', parts),
  error: (...parts: unknown[]) => say('error', parts),
  trace: (...parts: unknown[]) => say('log', parts),
  group: () => {},
  groupCollapsed: () => {},
  groupEnd: () => {},
  table: (...parts: unknown[]) => say('log', parts),
  dir: (...parts: unknown[]) => say('log', parts),
  assert: (condition: unknown, ...parts: unknown[]) => {
    if (!condition) say('error', ['Assertion failed:', ...parts]);
  },
  count: () => {},
  time: () => {},
  timeEnd: () => {},
};
Object.defineProperty(globalThis, 'console', { value: recorder, configurable: true, writable: true });
process.on('uncaughtException', (error) => say('error', [error]));
process.on('unhandledRejection', (reason) => say('error', [reason]));

let page = address;

/** Prints the report and exits once it is written: a pipe's write may still be pending when it returns. */
function report(record: unknown): Promise<never> {
  return new Promise<never>(() => {
    process.stdout.write(JSON.stringify({ address: page, record, console: said }), () => process.exit(0));
  });
}

const fetchFromServer = globalThis.fetch;
let origin = new URL(address).origin;

const served = await fetchFromServer(address).catch((error: unknown) => error);
if (!(served instanceof Response) || !served.ok) {
  say('error', [`The page did not load: ${served instanceof Response ? `HTTP ${served.status}` : text(served)}`]);
  await report(null);
}
// Where the page was served from, after any redirect (a culture's prefix, say): the window stands there.
page = (served as Response).url || address;
origin = new URL(page).origin;
const html = await (served as Response).text();

// ---- the window, as this process's globals -------------------------------------------------------

const window = new GlobalWindow({
  url: page,
  width: Number(widthText),
  height: 900,
  console: recorder as unknown as Console,
  settings: {
    disableJavaScriptFileLoading: true,
    disableCSSFileLoading: true,
    disableIframePageLoading: true,
    suppressInsecureJavaScriptEnvironmentWarning: true,
    navigation: { disableChildFrameNavigation: true, disableChildPageNavigation: true },
  },
});

/** The web's classes that bun has its own of: the page's are the DOM's, as a browser's are. */
const DOM_OWNED = new Set([
  'Event', 'EventTarget', 'CustomEvent', 'MessageEvent', 'ErrorEvent', 'CloseEvent', 'ProgressEvent',
  'Request', 'Response', 'Headers', 'fetch', 'FormData', 'AbortController', 'AbortSignal', 'URL',
  'URLSearchParams', 'Blob', 'File', 'FileReader', 'DOMException', 'WebSocket', 'MessagePort', 'navigator',
]);
const SKIPPED = new Set(['window', 'self', 'top', 'parent', 'globalThis', 'constructor', 'console']);

const names = new Set<string>();
for (let at: object | null = window; at && at !== Object.prototype; at = Object.getPrototypeOf(at)) {
  for (const name of Object.getOwnPropertyNames(at)) names.add(name);
}
const global = globalThis as unknown as Record<string, unknown>;
const fields = window as unknown as Record<string, unknown>;
for (const name of names) {
  if (SKIPPED.has(name) || (name in global && !DOM_OWNED.has(name))) continue;
  let bound: unknown;
  Object.defineProperty(global, name, {
    get() {
      const value = fields[name];
      if (typeof value === 'function' && name[0] === name[0].toLowerCase()) return (bound ??= value.bind(window));
      return value;
    },
    set(value) {
      fields[name] = value;
    },
    configurable: true,
  });
}
global.window = globalThis;
global.self = globalThis;
global.top = globalThis;
global.parent = globalThis;
Object.defineProperty(window.document, 'defaultView', { get: () => globalThis, configurable: true });

// Parsed, never run: the scripts are run below, in the order a browser runs them.
window.document.write(html);

// ---- the scripts ---------------------------------------------------------------------------------

const scripts = Array.from(window.document.querySelectorAll('script'));
const importMap: Record<string, string> = {};
for (const script of scripts.filter((script) => script.getAttribute('type') === 'importmap')) {
  try {
    Object.assign(importMap, JSON.parse(script.textContent ?? '{}').imports ?? {});
  } catch (error) {
    say('error', ['The import map is not JSON:', error]);
  }
}

const SPECIFIER = /(\bfrom\s*|\bimport\s*\(\s*|\bimport\s+)(["'])([^"']+)\2/g;

/** A module's text with each import a browser would resolve against the page written as the absolute path
 *  it resolves to: the import map applied to a bare name, a relative path resolved against the module. */
function resolvedImports(source: string, moduleUrl: string): string {
  return source.replace(SPECIFIER, (whole, lead: string, quote: string, specifier: string) => {
    const mapped = importMap[specifier] ?? specifier;
    if (!mapped.startsWith('/') && !mapped.startsWith('.')) return whole;
    const resolved = new URL(mapped, moduleUrl);
    return resolved.origin === origin ? `${lead}${quote}${resolved.pathname}${resolved.search}${quote}` : whole;
  });
}

// What the page imports from its server reaches bun's loader as an absolute path, which this plugin fetches.
const paths = new Set(Object.values(importMap).map((target) => new URL(target, address).pathname.split('/')[1]));
paths.add('_equantic');
const servedPath = new RegExp(`^/(${[...paths].map((path) => path.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')).join('|')})/`);
Bun.plugin({
  name: 'served-page',
  setup(build) {
    build.onResolve({ filter: servedPath }, (args) => ({ path: origin + args.path, namespace: 'served' }));
    build.onLoad({ filter: /.*/, namespace: 'served' }, async (args) => {
      const response = await fetchFromServer(args.path);
      if (!response.ok) throw new Error(`${args.path} answered HTTP ${response.status}`);
      return { contents: resolvedImports(await response.text(), args.path), loader: 'js' };
    });
  },
});

for (const script of scripts) {
  const type = script.getAttribute('type');
  if (script.hasAttribute('src') || (type !== null && type !== '' && type !== 'text/javascript')) continue;
  try {
    (0, eval)(script.textContent ?? '');
  } catch (error) {
    say('error', [error]);
  }
}

for (const [index, script] of scripts.filter((script) => script.getAttribute('type') === 'module').entries()) {
  const file = join(tmpdir(), `eq-hydration-${process.pid}-${index}.mjs`);
  await Bun.write(file, resolvedImports(script.textContent ?? '', page));
  try {
    await import(pathToFileURL(file).href);
  } catch (error) {
    say('error', [error]);
  } finally {
    await unlink(file).catch(() => {});
  }
}

// ---- the record ----------------------------------------------------------------------------------

const deadline = Date.now() + Number(timeoutText);
while (global.__EQ_HYDRATION__ === undefined && Date.now() < deadline) {
  await new Promise((resolve) => setTimeout(resolve, 20));
}
// What runs right after hydration (the mount hooks, the render the boot asks for once it holds one arm) says
// what it throws too, and what the page asks of the server then (a catalog fetched for a string it was not
// handed, which is only then said to be missing) is answered first.
await new Promise((resolve) => setTimeout(resolve, 300));
await Promise.race([window.happyDOM.waitUntilComplete(), new Promise((resolve) => setTimeout(resolve, 5000))]);

await report(global.__EQ_HYDRATION__ === undefined ? null : JSON.parse(JSON.stringify(global.__EQ_HYDRATION__)));
