/**
 * eQuantic.UI Runtime - Main Entry Point
 */

// Core
export { Component, HtmlElement } from './core/types';
export type { IComponent, HtmlNode, RenderContext, StyleClass, EventHandler } from './core/types';
export {
  StatelessComponent,
  StatefulComponent,
  SharedStatefulComponent,
  ComponentState,
} from './core/component';
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
export { format, stringFormat, parseEnum } from './utils/format';
export { round } from './utils/dotnet-math';
export { Decimal, dec } from './utils/decimal';
export { long } from './utils/long';
export { DateTime, dateTime, TimeSpan, timeSpan, DateOnly, dateOnly, TimeOnly, timeOnly, DateTimeOffset, dateTimeOffset } from './utils/datetime';
export { StringBuilder, stringBuilder } from './utils/string-builder';
export {
  Queue,
  queue,
  // The VOCABULARY Stack (spec A3) owns the bare name — the data structure stays reachable as
  // CollectionStack and through $eq.collections (the form emitted code actually uses).
  Stack as CollectionStack,
  stack,
  ValueMap,
  valueMap,
  LinkedList,
  LinkedListNode,
  linkedList,
} from './utils/collections';
export {
  SortedSet,
  sortedSet,
  SortedMap,
  sortedDictionary,
  sortedList,
  defaultCompare,
} from './utils/sorted';
export { liftArith, liftCmp } from './utils/nullable';
export { equals } from './utils/equals';
export { $eq } from './eq';

// Shared abstract vocabulary (write-once components) — client-side lowering to HtmlNode.
export { lowerVisualNode, tokenValue } from './shared/lowering';
export type { LoweringContext } from './shared/lowering';
export type {
  VisualNodeValue,
  BoxNode,
  FlexNodeValue,
  TextNode,
  PressableNode,
  FlexibleNode,
  SpacerNode,
  ComponentNode,
  ColorTokenValue,
  ColorValue,
} from './shared/nodes';
// Vocabulary classes — what eqc-transpiled shared components instantiate (imports from
// "@equantic/runtime" routed by the compiler's runtime-provided-type discovery).
export {
  VisualNode,
  Box,
  BoxStyle,
  Row,
  Column,
  Text,
  Pressable,
  Flexible,
  Spacer,
  Stack,
  Positioned,
  Icon,
} from './shared/vocabulary';
export {
  ColorToken,
  SizeValue,
  EdgeInsets,
  CornerRadii,
  TypeStyle,
  VariantColors,
} from './shared/value-types';
export type { AppTheme, ShadowSpec } from './shared/value-types';
export {
  ComponentContext,
  setPhotonTheme,
  getPhotonTheme,
  photonComponentContext,
  ambientLoweringContext,
} from './shared/photon-context';
export { VisualNodeComponent } from './shared/visual-node-component';
// The shared component LIBRARY (write-once) — transpiled modules embedded in the runtime, byte-pinned
// to the live eqc output (SharedComponentTranspilationTests). `using eQuantic.UI.Components.Shared`
// in app code routes imports here; the standard web components keep their per-app ./modules, so the
// deliberate name reuse never collides.
export { Button } from './shared/components/Button';
export { Card } from './shared/components/Card';
export { Divider } from './shared/components/Divider';
export { Badge } from './shared/components/Badge';
export { Chip } from './shared/components/Chip';
export { ProgressBar } from './shared/components/ProgressBar';
export { Avatar } from './shared/components/Avatar';
export { Banner } from './shared/components/Banner';
// Generated design system (tokens + theme — values from the C# single source, never hand-written).
export {
  Space,
  Radius,
  IconSize,
  Touch,
  Motion,
  ButtonStyles,
  photonTheme,
  PhotonTheme,
} from './shared/design-system.generated';
export { StyleBuilder } from './utils/style-builder';
export { ClassBuilder, joinClasses, whenClass } from './utils/class-builder';

// DOM
export { Router } from './router/router';
export type {
  RouterOptions,
  NavigateHandler,
  PrefetchHandler,
  NavigationGuard,
  PendingNavigation,
  GuardResult,
} from './router/router';
export { matchRoute, matchPattern } from './router/route-table';
export type { RouteEntry, RouteMatch } from './router/route-table';
export {
  routeData,
  getCurrentRoute,
  setCurrentRoute,
  setCurrentRouteFrom,
} from './router/current-route';
export type { RouteData } from './router/current-route';
export { Reconciler, getReconciler, resetReconciler } from './dom/reconciler';
export type { HydrationResult } from './dom/reconciler';
export { RenderManager } from './dom/renderer';

// Components - No longer exported here. Standard components are dynamically generated.

/**
 * Mount a component to a DOM element
 */
export function mount(component: { mount(container: HTMLElement): void }, selector: string): void {
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
  /** Client route table (generated from `[Page]` attributes) — enables SPA navigation. */
  routes?: import('./router/route-table').RouteEntry[];
}

declare global {
  interface Window {
    __EQ_CONFIG?: EqConfig;
    __EQ_DEV__?: boolean;
    __registerTheme?: () => void;
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
  let overlay: typeof import('./dev/error-overlay') | null = null;
  const isDev = window.__EQ_DEV__;

  // Import error overlay in development
  if (isDev) {
    overlay = await import('./dev/error-overlay');
  }

  const { logger } = await import('./utils/logger');

  logger.debug('Starting boot process...');
  const config = window.__EQ_CONFIG;

  if (!config || !config.page) {
    logger.warn('No page configured in __EQ_CONFIG');
    return;
  }

  const container = document.getElementById('app');
  if (!container) {
    logger.error('Container #app not found');
    return;
  }

  // Register theme before hydration (if available)
  if (typeof (window as any).__registerTheme === 'function') {
    logger.debug('Registering theme...');
    (window as any).__registerTheme();
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
    if (isDev && overlay && container.dataset.ssr !== 'true') {
      overlay.errorOverlay.show({
        message: error instanceof Error ? error.message : String(error),
        stack: error instanceof Error ? error.stack : undefined,
      });
    } else if (container.dataset.ssr !== 'true') {
      container.innerHTML = `<div style="color: red; padding: 20px;">
        <h2>Failed to load page</h2>
        <pre>${error instanceof Error ? error.message : String(error)}</pre>
      </div>`;
    }
  }
}
