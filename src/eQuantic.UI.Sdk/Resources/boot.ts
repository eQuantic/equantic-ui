// eQuantic.UI Client Runtime
// Handles dynamic imports and component mounting

export * from '../../eQuantic.UI.Runtime/src/index';

import { installErrorOverlay } from '../../eQuantic.UI.Runtime/src/dev/error-overlay';
import {
  getReconciler,
  Router,
  matchRoute,
  setCurrentRouteFrom,
  registerDeviceCapabilities,
  detectPhotonDensity,
  setPhotonTheme,
  materializeTheme,
  type EqConfig,
  type HtmlNode,
  type NavigationGuard,
  type ThemeData,
} from '../../eQuantic.UI.Runtime/src/index';
import { installCulture } from '../../eQuantic.UI.Runtime/src/utils/culture';

// --- Constants ---
const APP_ROOT_ID = 'app';
const MODULE_PATH_PREFIX = '/_equantic/';

// --- Types ---
interface MountableComponent {
  mount?(root: HTMLElement): void;
  render?(root: HTMLElement): void;
  getVirtualNode?(): HtmlNode;
  /** Hydrate SSR markup and take ownership of the tree (so SPA-nav diffs can use getCurrentTree). */
  hydrate?(root: HTMLElement): void;
  /** SPA-nav mount that reconciles against the outgoing page's tree (preserves a shared shell). */
  mountReconcile?(root: HTMLElement, previousNode: HtmlNode | null): HtmlNode;
  /** The tree currently reflected in the DOM — the diff baseline when navigating away. */
  getCurrentTree?(): HtmlNode | null;
  /** Release lifecycle ownership on nav-away without touching the (now-reconciled) DOM. */
  disposeQuietly?(): void;
}

// --- Helpers ---
function isDev(): boolean {
  return typeof window !== 'undefined' && window.__EQ_DEV__ === true;
}

// --- Initialization ---
let initialized = false;

/**
 * The page component currently mounted in the root. SPA navigation diffs the next page against this one's
 * tree (so a shared layout shell is preserved) and disposes it without tearing down the reconciled DOM.
 */
let currentComponent: MountableComponent | null = null;

/** True when THIS boot re-entered through a hot-reload refresh — see initHotReload. */
let hmrReplay = false;

/**
 * Bootstraps the eQuantic application
 */
export async function boot(): Promise<void> {
  if (initialized) return;
  initialized = true;

  // Phase 3 hot reload replay: state captured just before the HMR reload re-enters through the
  // ORDINARY SSR-hydration mechanic (window.__INITIAL_STATE__ + hydrateValue) — zero new paths.
  try {
    const saved = sessionStorage.getItem('__eq_hmr__');
    if (saved) {
      sessionStorage.removeItem('__eq_hmr__');
      hmrReplay = true;
      const parsed = JSON.parse(saved) as { url: string; state: Record<string, unknown> };
      if (parsed.url === location.href) {
        (window as unknown as { __INITIAL_STATE__?: object }).__INITIAL_STATE__ = {
          ...((window as unknown as { __INITIAL_STATE__?: object }).__INITIAL_STATE__ ?? {}),
          ...parsed.state,
        };
      }
    }
  } catch {
    /* best effort */
  }

  initHotReload();
  // The Next.js-style error modal, in the only language the developer wrote: an uncaught error's
  // JS stack walks back through the two source maps to the C# file and line, code frame included.
  if (isDev()) installErrorOverlay();

  // What a BROWSER can do, under the same names the C# interfaces have — the web's answer to the
  // native shells' IPhotonCapabilities.
  registerDeviceCapabilities();
  // A desktop browser is driven by a POINTER: the controls tighten to the density a native
  // desktop app has. Read once at boot, from what the browser says about the pointer.
  detectPhotonDensity();

  if (isDev()) {
    console.log('eQuantic.UI Runtime initializing...');
  }

  const root = document.getElementById(APP_ROOT_ID);
  if (!root) {
    console.error(`Root element #${APP_ROOT_ID} not found`);
    return;
  }

  try {
    const config = window.__EQ_CONFIG ?? {};
    const pageName = resolvePageName(config);

    if (!pageName) {
      renderNoPage(root);
      return;
    }

    // Apply the app-selected theme (SSR→client bridge) BEFORE hydration, so client renders and
    // subsequent setState re-renders resolve the same colors/shape the server baked into the markup.
    // The server emits window.__EQ_THEME__ = ThemeBridge.SerializeJson(options.Theme); absent it, the
    // runtime keeps its default photonTheme.
    const themeData = (window as unknown as { __EQ_THEME__?: ThemeData }).__EQ_THEME__;
    if (themeData) {
      if (isDev()) {
        console.log('[eQuantic.UI] Applying app theme from __EQ_THEME__');
      }
      setPhotonTheme(materializeTheme(themeData));
    }

    // Track L D4: install the request's culture and its string catalog BEFORE hydration — the
    // client must resolve exactly the strings the server rendered, or the SSR-identity contract
    // breaks on any translated page. The server emits window.__EQ_CULTURE__ next to the theme.
    const cultureData = (window as unknown as {
      __EQ_CULTURE__?: { name?: string; formatName?: string; strings?: Record<string, string> };
    }).__EQ_CULTURE__;
    if (cultureData) {
      installCulture(
        cultureData.name ?? '',
        cultureData.formatName ?? cultureData.name ?? '',
        cultureData.strings ?? {},
      );
    }


    // Seed the active route from the initial URL before mounting, so SSR hydration sees the same
    // route params/query the server rendered with (a param page would otherwise mismatch).
    if (config.routes && config.routes.length > 0) {
      const initialUrl = new URL(window.location.href);
      const initialMatch = matchRoute(config.routes, initialUrl.pathname);
      if (initialMatch) setCurrentRouteFrom(initialMatch, initialUrl);
    }

    await loadAndMountPage(root, pageName, config);

    // Phase 2: when the server provided a route table, enable client-side (SPA) navigation —
    // internal link clicks swap the page bundle in place instead of triggering a full reload.
    if (config.routes && config.routes.length > 0) {
      // Apps register navigation guards by pushing them onto window.__eqGuards (a guard can cancel a
      // navigation by returning false, or redirect by returning an href) — e.g. an auth gate on a
      // protected route. Read once at boot; runtime additions go through router.addGuard.
      const guards = (window as unknown as { __eqGuards?: NavigationGuard[] }).__eqGuards;

      const router = new Router({
        routes: config.routes,
        onNavigate: (match, url, isCurrent) =>
          // The same STRING the hover warmed under, or the click looks in the wrong drawer and
          // fetches a second time — which made a warmed navigation the slower one.
          navigateToPage(root, match.page, config, isCurrent, url.pathname + url.search),
        // Hover/focus prefetch: warm the page bundle so a click navigates instantly. loadPageModule's
        // dynamic import is cached by the browser, so the later navigation resolves without a round-trip.
        onPrefetch: (match, url) => {
          void loadPageModule(match.page, config);
          // …and the page's server data with it. Both halves of a navigation warm together, or the
          // click still waits for the half that did not.
          warmPageState(url.pathname + url.search);
        },
        guards: Array.isArray(guards) ? guards : undefined,
      });
      router.start();
      (window as unknown as { __eqRouter?: Router }).__eqRouter = router;
    }
  } catch (error) {
    renderError(root, error as Error);
  }
}

/**
 * Resolves the page name from config or query string
 */
function resolvePageName(config: EqConfig): string | null {
  // 1. Server injection (preferred)
  if (config.page) {
    return config.page;
  }

  return null;
}

/**
 * Dynamically imports a page bundle and returns its exported component class. Returns `null` when the
 * bundle can't be loaded (missing route → 404); throws when the bundle loads but doesn't export the
 * expected class (a build/contract error).
 */
async function loadPageModule(
  pageName: string,
  config: EqConfig,
): Promise<(new () => MountableComponent) | null> {
  const cacheBuster = config.version ? `?v=${config.version}` : '';
  const modulePath = `${MODULE_PATH_PREFIX}${pageName}.js${cacheBuster}`;

  if (isDev()) {
    console.log(`Loading module: ${modulePath}`);
  }

  let module: Record<string, unknown>;
  try {
    module = await import(/* @vite-ignore */ modulePath);
  } catch (error) {
    // A missing page module and a BROKEN one are different failures that used to look identical:
    // swallowing the error rendered "404 — the resource does not exist" for a module that exists
    // and threw while evaluating (a missing runtime companion member, say), sending the developer
    // hunting for a routing bug. Only a genuine 404 is a missing page; anything else is re-thrown
    // so renderError shows the real cause.
    // Match the MESSAGE, never the type: a module that throws while evaluating usually throws a
    // TypeError too ("X is not a function"), so `instanceof TypeError` would misfile exactly the
    // case this distinction exists for. Only the fetch/resolve failures mean "no such page".
    const missing = /failed to fetch dynamically imported module|error loading dynamically imported module|failed to resolve module/i
      .test(String((error as Error)?.message ?? error));
    if (missing) return null;
    throw error;
  }

  const ComponentClass = module[pageName] as new () => MountableComponent;
  if (!ComponentClass) {
    throw new Error(`Module '${pageName}' does not export class '${pageName}'`);
  }
  return ComponentClass;
}

/** Full client mount (no hydration): clears the root and renders the component into it. */
function mountComponent(root: HTMLElement, component: MountableComponent, pageName: string): void {
  root.innerHTML = '';
  if (typeof component.mount === 'function') {
    component.mount(root);
  } else if (typeof component.render === 'function') {
    component.render(root);
  } else {
    renderMountError(root, pageName);
  }
  if (isDev()) {
    console.log(`Mounted: ${pageName}`);
  }
}

/**
 * Initial load: hydrates the SSR HTML when present (attaching event listeners to the server-rendered
 * markup), otherwise does a full client mount.
 */
async function loadAndMountPage(
  root: HTMLElement,
  pageName: string,
  config: EqConfig,
): Promise<void> {
  const hasSSRContent = root.children.length > 0;
  if (!hasSSRContent) {
    root.innerHTML = '<div class="eq-loading">Loading...</div>';
  }

  const ComponentClass = await loadPageModule(pageName, config);
  if (!ComponentClass) {
    render404(root, pageName);
    return;
  }

  const component = new ComponentClass();

  // Hydration: attach events to existing SSR HTML. Prefer the component's own hydrate() so its render
  // manager owns the tree — that lets the first SPA navigation away diff against it (getCurrentTree) and
  // preserve a shared shell. Fall back to a direct reconciler hydrate for older component shapes.
  // After a hot-reload refresh the SSR HTML is the one thing KNOWN to be stale: it was rendered
  // by the server's still-running old assembly, while the page bundle that just loaded is the new
  // code. Hydration ADOPTS the DOM it finds — which kept the pre-edit pixels on screen and made
  // the whole feature read as broken ("I saved, it reloaded, nothing changed"). A replay boot
  // renders CLIENT-side instead: the new code paints, and the captured state re-enters through
  // the same __INITIAL_STATE__ door the SSR mechanic already uses.
  if (hasSSRContent && config.ssr !== false && component.getVirtualNode && !hmrReplay) {
    if (isDev()) {
      console.log(`Hydrating: ${pageName}`);
    }
    if (typeof component.hydrate === 'function') {
      component.hydrate(root);
    } else {
      const result = getReconciler().hydrateRoot(root, component.getVirtualNode());
      if (!result.success && isDev()) {
        console.warn('Hydration warnings:', result.warnings);
      }
    }
    currentComponent = component;
    return;
  }

  mountComponent(root, component, pageName);
  currentComponent = component;
}

/**
 * Client-side (SPA) navigation: loads the target page bundle and reconciles it into the root against the
 * outgoing page's tree — no hydration (there is no fresh SSR content on a client navigation), no full
 * reload. Reconciling (rather than wiping + remounting) is what makes a shared layout shell persist: the
 * reconciler keeps the shell's DOM and only patches the changed content. The outgoing page is disposed
 * *after* the swap, without tearing down the (now-reconciled) DOM.
 *
 * The outgoing page stays on screen until the new bundle has loaded and rendered — no "Loading…" flash —
 * which also means we must not blow the root away up front.
 */
async function navigateToPage(
  root: HTMLElement,
  pageName: string,
  config: EqConfig,
  isCurrent?: () => boolean,
  url?: string,
): Promise<void> {
  try {
    // The bundle and the page's SERVER DATA at the same time — the fetch is not on the critical
    // path behind the import, and neither is behind the other.
    const [ComponentClass, payload] = await Promise.all([
      loadPageModule(pageName, config),
      fetchPageState(url),
    ]);
    // A newer navigation started while this bundle was loading — don't clobber it.
    if (isCurrent && !isCurrent()) return;
    if (!ComponentClass) {
      render404(root, pageName);
      currentComponent = null;
      return;
    }

    // Seeded BEFORE the instance is built: adoptServerState reads this on the first render, which
    // is the same door the SSR payload comes through on a full load. Without it a navigated-to page
    // renders the empty state it was written to show while its data loads — and nothing loads it.
    applyPageState(payload);

    const previous = currentComponent;
    const next = new ComponentClass() as MountableComponent;

    if (typeof next.mountReconcile === 'function') {
      // Diff the new page against the outgoing tree (captured now, so it reflects any state the outgoing
      // page rendered while the bundle loaded). A shared shell is preserved; only changed content patches.
      const previousTree = previous?.getCurrentTree?.() ?? null;
      next.mountReconcile(root, previousTree);
      previous?.disposeQuietly?.();
    } else {
      // Fallback for component shapes without reconcile support: clean remount.
      mountComponent(root, next, pageName);
    }

    currentComponent = next;
    if (isDev()) {
      console.log(`Navigated: ${pageName}`);
    }
  } catch (error) {
    renderError(root, error as Error);
  }
}

interface PageStatePayload {
  title?: string;
  head?: string;
  state?: Record<string, unknown>;
}

/**
 * The page's server data and metadata, asked of the TARGET URL itself.
 *
 * Not a side endpoint: the request goes to the route being navigated to, carrying a header, and the
 * page route answers with JSON instead of a document. That is what makes the route params, the query
 * and the page resolution the ones a full load would have — they ARE a full load's, minus the HTML.
 *
 * A failure is not fatal. The page then renders exactly what it rendered before this existed.
 */
/**
 * Payloads already asked for, by href. The router warms a link on hover and dedupes per link, so
 * this holds at most one entry per link the reader pointed at — and the click that follows finds
 * the answer already on its way, or already here.
 *
 * Consumed ONCE. The data is the page's, not the session's: coming back to a page asks again, which
 * is the same thing a full load would do.
 */
const warmedState = new Map<string, Promise<PageStatePayload | null>>();

/** Starts the fetch a hover suggests is coming. Best-effort, exactly like the bundle beside it. */
function warmPageState(url: string): void {
  if (warmedState.has(url)) return;
  warmedState.set(url, fetchPageState(url));
}

async function fetchPageState(url?: string): Promise<PageStatePayload | null> {
  if (!url || typeof fetch !== 'function') return null;
  const warmed = warmedState.get(url);
  if (warmed) {
    warmedState.delete(url);
    return warmed;
  }
  try {
    const response = await fetch(url, {
      headers: { 'X-EQ-Navigate': '1' },
      credentials: 'same-origin',
    });
    if (!response.ok) return null;
    return (await response.json()) as PageStatePayload;
  } catch {
    return null;
  }
}

/**
 * Hands the payload to the two places that read it: the hydration door the SSR state comes through,
 * and the document head.
 *
 * The head is patched by IDENTITY, not by appending — a canonical is one statement about one
 * document, and a second one left over from the previous page tells a crawler the two URLs are the
 * same page. Matching on the attribute that names the tag (`name`, `property`, `rel`) is what lets
 * the SSR-rendered tags of the FIRST page be replaced rather than duplicated.
 */
function applyPageState(payload: PageStatePayload | null): void {
  if (!payload) return;

  if (payload.state && typeof payload.state === 'object') {
    (window as unknown as { __INITIAL_STATE__?: Record<string, unknown> }).__INITIAL_STATE__ =
      payload.state;
  }
  if (typeof payload.title === 'string' && payload.title.length > 0) {
    document.title = payload.title;
  }
  if (typeof payload.head !== 'string' || payload.head.length === 0) return;

  const template = document.createElement('template');
  template.innerHTML = payload.head;
  for (const incoming of Array.from(template.content.children)) {
    const selector = headSelectorFor(incoming);
    const existing = selector ? document.head.querySelector(selector) : null;
    if (existing) existing.replaceWith(incoming);
    else document.head.appendChild(incoming);
  }
}

/** What makes a head tag THE one it is: the attribute that names it. */
function headSelectorFor(element: Element): string | null {
  const tag = element.tagName.toLowerCase();
  for (const attribute of ['name', 'property', 'rel']) {
    const value = element.getAttribute(attribute);
    if (value) return `${tag}[${attribute}="${CSS.escape(value)}"]`;
  }
  return null;
}

// --- UI Renderers ---
//
// These are the pages that render when there is NO app page to render — an unknown route, a boot
// crash, an app with no pages yet. They can't lean on any stylesheet (the app may not have one),
// so each payload carries its own <style>: theme tokens first (`var(--eq-color-*)`, present when
// the app selected a theme), OS-scheme fallbacks otherwise. An app takes over the 404 entirely by
// declaring `[Page("/404")]` — the server SSRs it for unknown routes and this code never runs.

const STATUS_PAGE_CSS = `
  .eq-status-page { --eq-fb-bg: #fafafa; --eq-fb-fg: #171717; --eq-fb-muted: #666; --eq-fb-line: #e5e5e5; }
  @media (prefers-color-scheme: dark) {
    .eq-status-page { --eq-fb-bg: #0a0a0a; --eq-fb-fg: #ededed; --eq-fb-muted: #999; --eq-fb-line: #2e2e2e; }
  }
  .eq-status-page {
    min-height: 100vh; margin: 0; display: flex; align-items: center; justify-content: center;
    background: var(--eq-color-background, var(--eq-fb-bg));
    color: var(--eq-color-text-primary, var(--eq-fb-fg));
    font-family: system-ui, -apple-system, 'Segoe UI', Roboto, sans-serif;
    -webkit-font-smoothing: antialiased; text-align: center; padding: 24px;
  }
  .eq-status-page__code {
    font-size: 24px; font-weight: 600; margin: 0; padding-right: 23px; line-height: 49px;
    border-right: 1px solid var(--eq-color-border, var(--eq-fb-line));
  }
  .eq-status-page__body { padding-left: 24px; text-align: left; max-width: 32rem; }
  .eq-status-page__title { font-size: 14px; font-weight: 400; margin: 0; line-height: 49px; }
  .eq-status-page__detail {
    font-size: 13px; margin: 4px 0 0;
    color: var(--eq-color-text-muted, var(--eq-fb-muted));
    overflow-wrap: anywhere;
  }
  .eq-status-page__detail code { font-family: ui-monospace, 'SF Mono', Menlo, monospace; font-size: 12px; }
  .eq-status-page__link {
    display: inline-block; margin-top: 10px; font-size: 13px;
    color: var(--eq-color-link, inherit); text-decoration: underline; text-underline-offset: 3px;
  }
  .eq-status-page pre {
    text-align: left; font-size: 12px; line-height: 1.6; margin: 10px 0 0; padding: 12px 14px;
    font-family: ui-monospace, 'SF Mono', Menlo, monospace; white-space: pre-wrap; overflow-wrap: anywhere;
    background: var(--eq-color-surface-subtle, transparent);
    border: 1px solid var(--eq-color-border, var(--eq-fb-line)); border-radius: 8px;
  }
`;

/** The one shape all status pages share: a code, a title, and optional detail/actions. */
function renderStatusPage(
  root: HTMLElement,
  code: string,
  title: string,
  detailHtml: string,
): void {
  root.innerHTML = `
    <style>${STATUS_PAGE_CSS}</style>
    <div class="eq-status-page">
      <h1 class="eq-status-page__code">${escapeHtml(code)}</h1>
      <div class="eq-status-page__body">
        <h2 class="eq-status-page__title">${escapeHtml(title)}</h2>
        ${detailHtml}
      </div>
    </div>
  `;
}

function renderNoPage(root: HTMLElement): void {
  const isHome = window.location.pathname === '/' || window.location.pathname === '';

  if (isHome) {
    renderStatusPage(
      root,
      'eQ',
      'Welcome to eQuantic.UI',
      `<p class="eq-status-page__detail">No default page configured — declare a component with <code>[Page("/")]</code> to take this spot.</p>`,
    );
  } else {
    render404(root, window.location.pathname);
  }
}

function render404(root: HTMLElement, resource: string): void {
  renderStatusPage(
    root,
    '404',
    'This page could not be found.',
    `<p class="eq-status-page__detail"><code>${escapeHtml(resource)}</code></p>
     <a href="/" class="eq-status-page__link">Go home</a>`,
  );
}

function renderError(root: HTMLElement, error: Error): void {
  console.error('Runtime Error:', error);
  renderStatusPage(
    root,
    '500',
    'Application error.',
    `<pre>${escapeHtml(error.message)}</pre>
     <a href="/" class="eq-status-page__link">Go home</a>`,
  );
}

function renderMountError(root: HTMLElement, pageName: string): void {
  renderStatusPage(
    root,
    '!',
    `'${pageName}' loaded but cannot mount.`,
    `<p class="eq-status-page__detail">The component was created but exposes no <code>mount()</code> or <code>render()</code> method.</p>`,
  );
}

function escapeHtml(unsafe: string): string {
  return unsafe
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#039;');
}

/**
 * Phase 3 hot reload (v1): listen on the DEV-only SSE endpoint; on a rebuild, capture the live
 * page state (the stateful page's data fields) and reload — the boot replays it through the
 * SSR-hydration mechanic. In production the endpoint 404s and the source closes itself.
 */
function initHotReload(): void {
  if (typeof EventSource === 'undefined') return;
  try {
    const source = new EventSource('/_equantic/hmr');
    // No close-on-error: EventSource RECONNECTS by itself after a transient drop, which is the
    // whole point of the API — closing on the first hiccup left hot reload silently dead minutes
    // into every session. In production the endpoint 404s, and the browser abandons a non-200
    // stream on its own (readyState CLOSED, no retries) — nothing leaks.
    source.onmessage = () => {
      // The marker is written UNCONDITIONALLY: it is what tells the next boot to render with the
      // NEW code instead of hydrating the stale SSR. Gating it on captured state left every
      // write-once page (which keeps no _state bag) hydrating old HTML after the reload —
      // the pixels never changed, and the whole feature read as broken.
      const data: Record<string, unknown> = {};
      try {
        const holder = currentComponent as unknown as { _state?: Record<string, unknown> } | null;
        const state = holder?._state;
        if (state) {
          for (const key of Object.keys(state)) {
            const value = state[key];
            if (typeof value === 'function') continue;
            if (key === '_component' || key === '_context' || key === '_needsRender') continue;
            data[key] = value;
          }
        }
      } catch {
        /* reload without state rather than not at all */
      }
      try {
        sessionStorage.setItem('__eq_hmr__', JSON.stringify({ url: location.href, state: data }));
      } catch {
        /* private mode etc. — the reload still shows the new code, only via hydration */
      }
      location.reload();
    };
  } catch {
    /* no SSE in this host */
  }
}
