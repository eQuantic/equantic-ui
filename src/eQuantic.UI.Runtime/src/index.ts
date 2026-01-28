/**
 * eQuantic.UI Runtime - Main Entry Point
 */

// Core
export { Component, HtmlElement } from './core/types';
export type { IComponent, HtmlNode, RenderContext, StyleClass, EventHandler } from './core/types';
export { StatelessComponent, StatefulComponent, ComponentState } from './core/component';
export {
  ServiceProvider,
  ServiceCollectionBuilder,
  getRootServiceProvider,
  configureServices,
  resetServiceProvider,
} from './core/service-provider';
export type { ServiceKey } from './core/service-provider';
export { ServiceLifetime } from './core/service-provider';
export { HtmlStyle } from './core/html-style';

// Server Actions
export {
  ServerActionsClient,
  getServerActionsClient,
  configureServerActions,
  resetServerActionsClient,
} from './core/server-actions';
export type { ServerActionResponse } from './core/server-actions';

// Utils
export { format } from './utils/format';
export { StyleBuilder } from './utils/style-builder';

// DOM
export { Reconciler, getReconciler, resetReconciler } from './dom/reconciler';
export type { HydrationResult } from './dom/reconciler';
export { RenderManager } from './dom/renderer';

// Components - No longer exported here. Standard components are dynamically generated.

/**
 * Mount a component to a DOM element
 */
export function mount(
  component: import('./core/component').StatefulComponent,
  selector: string,
): void {
  const container = document.querySelector(selector);
  if (!container) {
    throw new Error(`Container not found: ${selector}`);
  }
  component.mount(container as HTMLElement);
}

/**
 * Create and mount a component
 */
export function createApp<T extends import('./core/component').StatefulComponent>(
  ComponentClass: new () => T,
  selector: string,
): T {
  const component = new ComponentClass();
  mount(component, selector);
  return component;
}

/**
 * Configuration object set by the server in window.__EQ_CONFIG
 */
export interface EqConfig {
  page?: string | null;
  version?: string;
  ssr?: boolean;
}

declare global {
  interface Window {
    __EQ_CONFIG?: EqConfig;
    __EQ_DEV__?: boolean;
  }
}

/**
 * Boot the application.
 * This is the main entry point called from the HTML shell.
 *
 * The boot process:
 * 1. Reads configuration from window.__EQ_CONFIG (set by server)
 * 2. Dynamically imports the page component module
 * 3. If SSR was used (data-ssr="true"), hydrates the existing DOM
 * 4. Otherwise, does a full client-side render
 *
 * @example
 * ```html
 * <script type="module">
 *   import { boot } from "@equantic/runtime";
 *   boot();
 * </script>
 * ```
 */
export async function boot(): Promise<void> {
  const config = window.__EQ_CONFIG;

  if (!config || !config.page) {
    console.warn('[eQuantic.UI] No page configured in __EQ_CONFIG');
    return;
  }

  const container = document.getElementById('app');
  if (!container) {
    console.error('[eQuantic.UI] Container #app not found');
    return;
  }

  try {
    // Import the page component module dynamically
    // The version query string ensures cache busting on new builds
    const modulePath = `/_equantic/${config.page}.js?v=${config.version}`;
    const pageModule = await import(/* @vite-ignore */ modulePath);

    // Look for the default export or the class with the same name as the page
    const PageClass = pageModule.default || pageModule[config.page];

    if (!PageClass) {
      console.error(`[eQuantic.UI] Page class '${config.page}' not found in module`);
      return;
    }

    // Create and mount the component
    // The mount() method will automatically detect SSR and hydrate if needed
    const component = new PageClass();

    if (config.ssr) {
      console.debug(`[eQuantic.UI] Hydrating SSR page: ${config.page}`);
    } else {
      console.debug(`[eQuantic.UI] Client-side rendering page: ${config.page}`);
    }

    component.mount(container);
  } catch (error) {
    console.error(`[eQuantic.UI] Failed to boot page '${config.page}':`, error);

    // Show error to user in development
    if (container.dataset.ssr !== 'true') {
      container.innerHTML = `<div style="color: red; padding: 20px;">
        <h2>Failed to load page</h2>
        <pre>${error instanceof Error ? error.message : String(error)}</pre>
      </div>`;
    }
  }
}
