// eQuantic.UI Client Runtime
// Handles dynamic imports and component mounting

export * from '../../eQuantic.UI.Runtime/src/index';

import {
  StyleBuilder,
  getReconciler,
  Router,
  matchRoute,
  setCurrentRouteFrom,
  type EqConfig,
  type HtmlNode,
  type NavigationGuard,
} from '../../eQuantic.UI.Runtime/src/index';

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

declare global {
  interface Window {
    StyleBuilder: typeof StyleBuilder;
  }
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

// Expose StyleBuilder globally for back-compat ($eq and other helpers are imported by name).
if (typeof window !== 'undefined') {
  window.StyleBuilder = StyleBuilder;
}

/**
 * Bootstraps the eQuantic application
 */
export async function boot(): Promise<void> {
  if (initialized) return;
  initialized = true;

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

    // Register theme before hydration (if available)
    if (typeof (window as any).__registerTheme === 'function') {
      if (isDev()) {
        console.log('[eQuantic.UI] Registering theme...');
      }
      (window as any).__registerTheme();
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
        onNavigate: (match, _url, isCurrent) => navigateToPage(root, match.page, config, isCurrent),
        // Hover/focus prefetch: warm the page bundle so a click navigates instantly. loadPageModule's
        // dynamic import is cached by the browser, so the later navigation resolves without a round-trip.
        onPrefetch: (match) => {
          void loadPageModule(match.page, config);
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

  // 2. Query string (debug/legacy)
  const params = new URLSearchParams(window.location.search);
  return params.get('page');
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
  } catch {
    return null;
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
  if (hasSSRContent && config.ssr !== false && component.getVirtualNode) {
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
): Promise<void> {
  try {
    const ComponentClass = await loadPageModule(pageName, config);
    // A newer navigation started while this bundle was loading — don't clobber it.
    if (isCurrent && !isCurrent()) return;
    if (!ComponentClass) {
      render404(root, pageName);
      currentComponent = null;
      return;
    }

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

// --- UI Renderers ---

function renderNoPage(root: HTMLElement): void {
  const isHome = window.location.pathname === '/' || window.location.pathname === '';

  if (isHome) {
    root.innerHTML = `
      <div class="eq-welcome">
        <h1>Welcome to eQuantic.UI</h1>
        <p>No default page configured.</p>
      </div>
    `;
  } else {
    render404(root, window.location.pathname);
  }
}

function render404(root: HTMLElement, resource: string): void {
  root.innerHTML = `
    <div class="eq-error-page">
      <h1 class="eq-error-code">404</h1>
      <h2>Page Not Found</h2>
      <p>The resource '<strong>${escapeHtml(resource)}</strong>' does not exist.</p>
      <a href="/" class="eq-btn">Go Home</a>
    </div>
  `;
}

function renderError(root: HTMLElement, error: Error): void {
  console.error('Runtime Error:', error);
  root.innerHTML = `
    <div class="eq-error-page eq-error-page--critical">
      <h2>Application Error</h2>
      <pre>${escapeHtml(error.message)}</pre>
    </div>
  `;
}

function renderMountError(root: HTMLElement, pageName: string): void {
  root.innerHTML = `
    <div class="eq-error-page eq-error-page--warning">
      <h2>Loaded: ${escapeHtml(pageName)}</h2>
      <p>Component created but no <code>mount()</code> or <code>render()</code> method found.</p>
    </div>
  `;
}

function escapeHtml(unsafe: string): string {
  return unsafe
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#039;');
}
