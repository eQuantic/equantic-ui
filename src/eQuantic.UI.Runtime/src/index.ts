/**
 * eQuantic.UI Runtime - Main Entry Point
 */

// Core
export { Component, HtmlElement } from './core/types';
export { DynamicElement } from './core/dynamic-element';
export type { IComponent, HtmlNode, RenderContext, StyleClass, EventHandler } from './core/types';
// The C# `BuildContext` mirror — transpiled components declare `build(context: BuildContext)`, so the
// name must resolve here exactly as it does in shared/runtime-exports.
export type { RenderContext as BuildContext } from './core/types';
export {
  StatelessComponent,
  StatefulComponent,
  SharedStatefulComponent,
  ComponentState,
} from './core/component';
// The C# `eQuantic.UI.Primitives.UiComponent` base surfaces in transpiled signatures (e.g. the
// reconciler's `AdoptConfig(UiComponent next)`) — on the runtime it aliases the Component base.
export { Component as UiComponent } from './core/types';
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
export {
  DateTime,
  dateTime,
  TimeSpan,
  timeSpan,
  DateOnly,
  dateOnly,
  TimeOnly,
  timeOnly,
  DateTimeOffset,
  dateTimeOffset,
} from './utils/datetime';
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
  Grid,
  GridTrack,
  AdaptiveNode,
  Anchored,
  StyleDiff,
  ShadowSpec,
  TextRun,
  TransitionSpec,
  StyleChannels,
  Shortcut,
  KeyChord,
  KeyModifiers,
  Sticky,
  Text,
  Pressable,
  CodeSurface,
  Adjustable,
  CameraPreview,
  Hoverable,
  Flexible,
  Spacer,
  Stack,
  Positioned,
  Icon,
  Vector,
  IconGlyph,
  Image,
  ScrollView,
  LoopMotion,
  GridPattern,
  LinearGradient,
  RadialGradient,
  TextEntry,
  Overlay,
  Presence,
  DragDismiss,
  Link,
  Spinner,
  CuratedIcons,
  Draggable,
  SafeArea,
} from './shared/vocabulary';
export {
  Color,
  ColorToken,
  SizeValue,
  EdgeInsets,
  CornerRadii,
  Transform2D,
  TypeStyle,
  VariantColors,
} from './shared/value-types';
export type { AppTheme } from './shared/value-types';
export {
  ComponentContext,
  setPhotonTheme,
  getPhotonTheme,
  setPhotonDensity,
  getPhotonDensity,
  detectPhotonDensity,
  photonComponentContext,
  ambientLoweringContext,
} from './shared/photon-context';
export { materializeTheme } from './shared/theme-bridge';
export type { ThemeData } from './shared/theme-bridge';
export { VisualNodeComponent } from './shared/visual-node-component';
export { installErrorOverlay } from './dev/error-overlay';
// The shared component LIBRARY (write-once) — transpiled modules embedded in the runtime, byte-pinned
// to the live eqc output (SharedComponentTranspilationTests). `using eQuantic.UI.Components`
// in app code routes imports here; the standard web components keep their per-app ./modules, so the
// deliberate name reuse never collides.
export * from './shared/components';
// Generated design system (tokens + theme — values from the C# single source, never hand-written).
export {
  Space,
  Radius,
  IconSize,
  Touch,
  Motion,
  Curve,
  ButtonStyles,
  Sizing,
  photonTheme,
  PhotonTheme,
} from './shared/design-system.generated';
export { Navigator } from './router/navigator';
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
  }
}


// What a browser can do, registered under the C# interface names — see shared/devices.
export { registerDeviceCapabilities } from './shared/devices/register';
export { WebPhotoLibrary } from './shared/devices/photo-library';
export { WebBiometrics } from './shared/devices/biometrics';
export type { PickedImage } from './shared/devices/photo-library';
