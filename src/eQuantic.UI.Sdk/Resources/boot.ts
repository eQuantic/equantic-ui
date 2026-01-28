// eQuantic.UI Client Runtime
// Handles dynamic imports and component mounting

export * from '../../eQuantic.UI.Runtime/src/index';

import {
  StyleBuilder,
  getReconciler,
  type EqConfig,
  type HtmlNode,
} from '../../eQuantic.UI.Runtime/src/index';

// --- Constants ---
const APP_ROOT_ID = 'app';
const MODULE_PATH_PREFIX = '/_equantic/';

// --- Types ---
interface MountableComponent {
  mount?(root: HTMLElement): void;
  render?(root: HTMLElement): void;
  getVirtualNode?(): HtmlNode;
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

// Expose StyleBuilder globally for generated code
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

    await loadAndMountPage(root, pageName, config);
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
 * Loads the page module and mounts the component
 */
async function loadAndMountPage(
  root: HTMLElement,
  pageName: string,
  config: EqConfig,
): Promise<void> {
  const hasSSRContent = root.children.length > 0;

  // Show loading only if no SSR content
  if (!hasSSRContent) {
    root.innerHTML = '<div class="eq-loading">Loading...</div>';
  }

  const cacheBuster = config.version ? `?v=${config.version}` : '';
  const modulePath = `${MODULE_PATH_PREFIX}${pageName}.js${cacheBuster}`;

  if (isDev()) {
    console.log(`Loading module: ${modulePath}`);
  }

  let module: Record<string, unknown>;
  try {
    module = await import(/* @vite-ignore */ modulePath);
  } catch {
    render404(root, pageName);
    return;
  }

  const ComponentClass = module[pageName] as new () => MountableComponent;
  if (!ComponentClass) {
    throw new Error(`Module '${pageName}' does not export class '${pageName}'`);
  }

  const component = new ComponentClass();

  // Hydration: attach events to existing SSR HTML
  if (hasSSRContent && config.ssr !== false && component.getVirtualNode) {
    if (isDev()) {
      console.log(`Hydrating: ${pageName}`);
    }
    const reconciler = getReconciler();
    const virtualNode = component.getVirtualNode();
    const result = reconciler.hydrateRoot(root, virtualNode);

    if (!result.success && isDev()) {
      console.warn('Hydration warnings:', result.warnings);
    }
    return;
  }

  // Full mount
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
