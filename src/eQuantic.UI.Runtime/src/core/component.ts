/**
 * eQuantic.UI Runtime - Stateful Component Support
 */

import { Component, HtmlNode, RenderContext } from './types';
import type { VisualNodeValue } from '../shared/nodes';
import { RenderManager } from '../dom/renderer';
import { getRootServiceProvider, ServiceProvider } from './service-provider';
import { hydrateValue } from '../utils/hydrate-value';
import { hydrate, type HydrationSpec } from '../utils/hydrate';
import { getCurrentRoute } from '../router/current-route';
import {
  ComponentInstanceStore,
  enterPass,
  exitPass,
  reconcileBuildRoot,
} from '../shared/instance-store';
import {
  getPhotonDensity,
  getInFlow,
  getPhotonTheme,
  getPhotonTypeScale,
  measurePhotonText,
  photonMonoAdvance,
} from '../shared/photon-context';
import { renderComponentFailure } from '../shared/component-boundary';
import { scheduleRenderFlush } from './render-scheduler';

/**
 * Base class for stateless components
 */
/**
 * SERVER DATA (the C# `IServerPrefetch` twin): the fields the server's prefetch filled arrive as
 * `window.__INITIAL_STATE__`, keyed by the SAME field names the page declares, and land BEFORE its
 * first render — so the client's first tree is the one the server already wrote as HTML and
 * hydration matches instead of flashing the field defaults. Consumed once: the payload is a
 * single-render handoff, not a store.
 *
 * Shared by both write-once page bases (stateless and stateful pages prefetch identically).
 */
export function adoptServerState(target: object): void {
  if (typeof window === 'undefined') return;
  const w = window as unknown as { __INITIAL_STATE__?: Record<string, unknown> };
  const payload = w.__INITIAL_STATE__;
  if (!payload) return;
  const self = target as Record<string, unknown>;
  // The class's TYPED boundary: the compiler emits `static $hydration` naming every field whose
  // wire form differs from its runtime type. A spec'd field is coerced by what it IS; the rest
  // keep the witness path (the default value reveals the type) for compat.
  const specs = (target.constructor as { $hydration?: Record<string, HydrationSpec> }).$hydration;
  let adopted = false;
  for (const key of Object.keys(payload)) {
    // Only fields the component actually declares: an unknown key is stale payload, never a new
    // field (assigning it would silently create one no build ever reads).
    if (!(key in self) || typeof payload[key] === 'function') continue;
    const spec = specs?.[key];
    self[key] = spec !== undefined ? hydrate(payload[key], spec) : hydrateValue(self[key], payload[key]);
    adopted = true;
  }
  // Leave the payload for its real owner when nothing here matched — a Core page reads it from its
  // own state object, and a shared component rendering first must not swallow it.
  if (adopted) delete w.__INITIAL_STATE__;
}

export abstract class StatelessComponent extends Component {
  private _renderManager: RenderManager = new RenderManager();
  private _instances = new ComponentInstanceStore();
  private _mounted = false;
  private _renderScheduled = false;

  protected get serviceProvider(): ServiceProvider {
    return getRootServiceProvider();
  }

  // The union documents today's two authoring worlds compiling to the SAME runtime base: Core
  // pages build Component trees (this render path), write-once components build vocabulary
  // values (expanded by the lowering — they never reach render() below).
  abstract build(context: RenderContext): Component | VisualNodeValue;

  render(): HtmlNode {
    const context: RenderContext = {
      getService: <T>(key: import('./types').ServiceKey<T>) =>
        this.serviceProvider.getService<T>(key),
      serviceProvider: this.serviceProvider,
      route: getCurrentRoute(),
      inFlow: getInFlow(),
      theme: getPhotonTheme(),
      // The SAME density and scale the lowering hands every component it expands. Without these two
      // a page and its own subtree render at different sizes: the page's context defaulted to
      // comfortable while everything the lowering expanded took the ambient (compact on a pointer
      // device), so a code editor drew 19dp lines and placed its caret on a 17dp grid — a caret
      // that drifts a line and a half down a screenful of code, for no reason anyone can see.
      density: getPhotonDensity(),
      typeScale: getPhotonTypeScale(),
      measureText: measurePhotonText,
      monoAdvance: photonMonoAdvance,
    };
    if (!this._mounted) adoptServerState(this);
    // Reconciler pass (W6): a stateless page IS re-renderable — build is pure and the instance
    // store retains nested shared stateful across passes — so those children invalidate by
    // re-rendering this page, exactly like a stateful host. (The old "no invalidator" fence made
    // every stateful child of a stateless page render-once: the site's mega menu opened its state
    // and nothing on screen ever changed.)
    enterPass(this._instances, () => this._scheduleRender());
    try {
      const component = reconcileBuildRoot(this.build(context)) as Component;
      return component.render();
    } catch (error) {
      // A PAGE has no parent to contain it. Its own throw used to leave the root unwritten, which
      // is the white screen the boundary exists to end.
      return renderComponentFailure(this.constructor.name, error);
    } finally {
      exitPass();
    }
  }

  _scheduleRender(): void {
    if (this._renderScheduled) return;
    this._renderScheduled = true;

    scheduleRenderFlush(() => {
      this._renderScheduled = false;
      if (this._mounted) this._renderManager.update(this.render());
    });
  }

  mount(container: HTMLElement): void {
    // Check if we should hydrate (SSR content exists)
    if (this._renderManager.canHydrate(container)) {
      const node = this.render();
      this._renderManager.hydrate(node, container);
    } else {
      const node = this.render();
      this._renderManager.mount(node, container);
    }
    this._mounted = true;
  }

  /**
   * Hydrate existing SSR markup in `container`: attach listeners and take ownership of the tree through
   * the render manager, so a later SPA navigation can diff against it ({@link getCurrentTree}) and keep a
   * shared shell. The caller has already determined SSR content is present, so this bypasses the
   * `data-ssr` gate that {@link mount} uses.
   */
  hydrate(container: HTMLElement): void {
    const node = this.render();
    this._renderManager.hydrate(node, container);
    this._mounted = true;
  }

  /**
   * SPA-navigation mount: reconcile into a root that already holds the outgoing page's DOM (described by
   * `previousNode`) so a shared layout shell is preserved instead of torn down. Returns the rendered tree
   * — the host tracks it as the new "current" tree for the next navigation's diff.
   */
  mountReconcile(container: HTMLElement, previousNode: HtmlNode | null): HtmlNode {
    const node = this.render();
    this._renderManager.adopt(node, container, previousNode);
    this._mounted = true;
    return node;
  }

  /** The virtual tree currently reflected in the DOM (the diff baseline for the next navigation). */
  getCurrentTree(): HtmlNode | null {
    return this._renderManager.getCurrentNode();
  }

  /**
   * A stateless page has no lifecycle of its own. It does own a STORE, and that changed what this
   * method owes: since the reconciler pass (W6) a stateless page retains the nested stateful
   * components it built, so what it holds on navigation away is a set of components that each have
   * an `onMount` that ran and an `onUnmount` that is owed.
   *
   * Left unsaid, the page went and its components stayed: a section subscribed to a device kept
   * its subscription, and every `setState` from that dead component drew it back into the page the
   * visitor had navigated to. So the previous page's section reappeared over the new one, its title
   * running into the new title, and one more timer stayed alive for every visit.
   *
   * `_mounted` goes false for the same reason the stateful page sets it: anything already queued
   * must flush into nothing rather than into a page that is no longer on screen.
   */
  disposeQuietly(): void {
    this._mounted = false;
    this._instances.unmountAll();
  }

  getVirtualNode(): HtmlNode {
    return this.render();
  }
}

/**
 * Base class for stateful components
 */
export abstract class StatefulComponent extends Component {
  private _instances = new ComponentInstanceStore();
  private _state: ComponentState | null = null;
  private _mounted = false;
  private _renderScheduled = false;
  private _renderManager: RenderManager = new RenderManager();
  protected get serviceProvider(): ServiceProvider {
    return getRootServiceProvider();
  }

  abstract createState(): ComponentState;

  get state(): ComponentState {
    if (!this._state) {
      if (typeof this.createState !== 'function') {
        throw new Error(`Component ${this.constructor.name} does not implement createState()`);
      }
      this._state = this.createState();

      if (!this._state) {
        throw new Error(`createState() returned null/undefined for ${this.constructor.name}`);
      }

      if (typeof this._state.setComponent === 'function') {
        this._state.setComponent(this);
      } else {
        console.warn(
          `State object for ${this.constructor.name} missing setComponent method.`,
          this._state,
        );
      }

      // Hydrate state from SSR if available
      let hydrated = false;
      if (typeof window !== 'undefined' && (window as any).__INITIAL_STATE__ && this._state) {
        const initialState = (window as any).__INITIAL_STATE__;

        // Copy data properties from SSR state to client state.
        // Server preserves original field names (including underscore prefix).
        // The state class's `static $hydration` (emitted by the compiler) says what each field IS,
        // so values that crossed the wire as strings (decimal -> Decimal, long -> bigint, records
        // -> their prototypes) are coerced ONCE here. Fields without a spec keep the witness path
        // (the default value reveals the type — see hydrateValue). Plain fields assign verbatim.
        const state = this._state as unknown as Record<string, unknown>;
        const specs = (this._state.constructor as { $hydration?: Record<string, HydrationSpec> })
          .$hydration;
        Object.keys(initialState).forEach((key) => {
          if (this._state && typeof initialState[key] !== 'function' && key in this._state) {
            const spec = specs?.[key];
            state[key] =
              spec !== undefined
                ? hydrate(initialState[key], spec)
                : hydrateValue(state[key], initialState[key]);
          }
        });
        // Clear initial state to prevent reuse
        delete (window as any).__INITIAL_STATE__;
        hydrated = true;
      }

      // Only call onInit if we didn't hydrate (client-only render or SSR on server)
      if (!hydrated) {
        this._state.onInit();
      }
    }
    return this._state;
  }

  render(): HtmlNode {
    const context: RenderContext = {
      getService: <T>(key: import('./types').ServiceKey<T>) =>
        this.serviceProvider.getService<T>(key),
      serviceProvider: this.serviceProvider,
      route: getCurrentRoute(),
      inFlow: getInFlow(),
      theme: getPhotonTheme(),
      // The SAME density and scale the lowering hands every component it expands. Without these two
      // a page and its own subtree render at different sizes: the page's context defaulted to
      // comfortable while everything the lowering expanded took the ambient (compact on a pointer
      // device), so a code editor drew 19dp lines and placed its caret on a 17dp grid — a caret
      // that drifts a line and a half down a screenful of code, for no reason anyone can see.
      density: getPhotonDensity(),
      typeScale: getPhotonTypeScale(),
      measureText: measurePhotonText,
      monoAdvance: photonMonoAdvance,
    };
    this.state._context = context;

    // Reconciler pass (W6 slice 2): everything lowered during this page render — including
    // VisualNodeComponent bridges — reconciles against this page's one retention pass; nested
    // shared stateful retained inside it invalidate by re-rendering THIS page.
    enterPass(this._instances, () => this._scheduleRender());
    try {
      const component = reconcileBuildRoot(this.state.build(context)) as Component;
      return component.render();
    } catch (error) {
      return renderComponentFailure(this.constructor.name, error);
    } finally {
      exitPass();
    }
  }

  mount(container: HTMLElement): void {
    // Check if we should hydrate (SSR content exists)
    if (this._renderManager.canHydrate(container)) {
      // For hydration, render to get the virtual DOM for event attachment
      const node = this.render();
      const result = this._renderManager.hydrate(node, container);
      if (result.success) {
        console.debug(
          `[eQuantic.UI] Hydrated ${this.constructor.name} with ${result.attachedListeners} event listeners`,
        );
      }
      // After successful hydration, set this._mounted BEFORE onMount to prevent render loops
      this._mounted = true;
      this.state.onMount();
    } else {
      // For client-only rendering, render first then mount
      const node = this.render();
      this._renderManager.mount(node, container);
      this._mounted = true;
      this.state.onMount();
    }
  }

  /**
   * Hydrate existing SSR markup in `container`: attach listeners, take ownership of the tree through the
   * render manager (so later setState re-renders and SPA-nav diffs work), and run onMount. The caller has
   * already determined SSR content is present, so this bypasses the `data-ssr` gate that {@link mount} uses.
   */
  hydrate(container: HTMLElement): void {
    const node = this.render();
    this._renderManager.hydrate(node, container);
    this._mounted = true;
    this.state.onMount();
  }

  /**
   * SPA-navigation mount: reconcile into a root that already holds the outgoing page's DOM (described by
   * `previousNode`) so a shared layout shell is preserved instead of torn down. Wires this component's
   * render manager to the root (so later setState re-renders reconcile correctly) and runs onMount.
   * Returns the rendered tree — the host tracks it as the new "current" tree for the next nav's diff.
   */
  mountReconcile(container: HTMLElement, previousNode: HtmlNode | null): HtmlNode {
    const node = this.render();
    this._renderManager.adopt(node, container, previousNode);
    this._mounted = true;
    this.state.onMount();
    return node;
  }

  /** The virtual tree currently reflected in the DOM (the diff baseline for the next navigation). */
  getCurrentTree(): HtmlNode | null {
    return this._renderManager.getCurrentNode();
  }

  /**
   * Release lifecycle ownership when navigating away WITHOUT touching the DOM. SPA navigation already
   * reconciled the root to the next page (preserving any shared shell); calling {@link unmount} here
   * would reconcile this component's tree to null and delete that shared DOM. Marking `_mounted=false`
   * also makes any already-queued `_scheduleRender` flush a no-op, so a stale outgoing page can't
   * clobber the freshly mounted one.
   */
  disposeQuietly(): void {
    if (this._mounted) {
      this._state?.onDispose();
      this._mounted = false;
    }
    // Its own state was released above; the components it RETAINED are a separate debt. A page
    // keeps nested stateful components across its re-renders, and each of them has an onMount that
    // ran, so leaving the tree is owed to every one of them — not only to the page. Left unpaid, a
    // section that subscribed to a device keeps drawing itself into the page the visitor moved to.
    // Outside `_mounted` on purpose: a page disposed twice must not skip the store the second time
    // because the first pass already flipped the flag.
    this._instances.unmountAll();
  }

  _scheduleRender(): void {
    if (this._renderScheduled) return;
    this._renderScheduled = true;

    // Next frame while visible, timer fallback while hidden — a background tab gets no frames, and a
    // rAF that never fires would also latch _renderScheduled, swallowing every later setState.
    scheduleRenderFlush(() => {
      this._renderScheduled = false;
      if (this._mounted) {
        // Efficient update using reconciler
        const node = this.render();
        this._renderManager.update(node);

        // Call lifecycle hook
        this.state.onUpdate();
      }
    });
  }

  unmount(): void {
    if (this._mounted && this._state) {
      this._state.onDispose();
      this._renderManager.unmount();
      this._mounted = false;
    }
  }
}

/**
 * Base class for SHARED stateful components — the `eQuantic.UI.Primitives.StatefulComponent` shape:
 * state lives as fields on the component itself and `setState` triggers the rebuild directly (no
 * `createState`/`ComponentState` split). eqc routes shared components here when their base resolves
 * to the Primitives namespace. Deliberately parallel to {@link StatefulComponent} rather than
 * refactored into a common base — the planned Core unification (SHARED-COMPONENTS-PLAN) owns that
 * consolidation; duplicating the mount plumbing today keeps the battle-tested path untouched.
 */
export abstract class SharedStatefulComponent extends Component {
  private _renderManager: RenderManager = new RenderManager();
  private _mounted = false;
  private _renderScheduled = false;
  private _instances = new ComponentInstanceStore();

  /**
   * The shared-vocabulary discriminator: nested inside an abstract tree this IS a component node —
   * the lowering expands it through the positional store (retention) instead of the plain mixing
   * seam (an embedded self-render with a dead render manager). As a page ROOT the field is inert.
   */
  readonly nodeKind = 'component';

  /** The C# `VisualNode.Key` mirror — part of the reconciler identity (path + type + key). */
  key: string | null = null;

  /** Duck-type marker the instance store keys on (avoids an import cycle with shared/). */
  readonly _sharedStateful = true;

  /**
   * Set by the instance store when this instance is retained NESTED inside a host page (W6 slice 2):
   * setState then re-renders the HOST (which reconciles back onto this same retained instance)
   * instead of this component's own — never mounted — render manager.
   */
  _invalidationHook: (() => void) | null = null;

  protected get serviceProvider(): ServiceProvider {
    return getRootServiceProvider();
  }

  // The union documents today's two authoring worlds compiling to the SAME runtime base: Core
  // pages build Component trees (this render path), write-once components build vocabulary
  // values (expanded by the lowering — they never reach render() below).
  abstract build(context: RenderContext): Component | VisualNodeValue;

  /** The C# `SetState(mutate)` contract: run the mutation, then schedule a rebuild. */
  protected setState(fn: () => void): void {
    fn();
    this._scheduleRender();
  }

  /**
   * Runs ONCE, when this instance has entered a live tree (C# `OnMount`) — the transpiled subclass
   * overrides it. NOT "the pixels exist": Photon has no DOM to read geometry from, so a hook
   * promising it could not be write-once.
   */
  onMount(): void {}

  /** Runs when this instance's position LEAVES the tree (C# `OnUnmount`) — unsubscribe here. */
  onUnmount(): void {}

  /** Called by the HOST — the instance store, or `mount`/`hydrate` for a page root. Idempotent. */
  notifyMounted(): void {
    if (this._lifecycleMounted) return;
    this._lifecycleMounted = true;
    this.onMount();
  }

  /**
   * The pair — also idempotent, and a no-op for an instance that never entered the tree.
   *
   * Everything this component RETAINED leaves with it. On Photon one store belongs to the host, so
   * a pass that drops a subtree unmounts every instance in it whatever its depth; on the web each
   * component keeps its own store, and without this line the teardown reached only the direct
   * children. A component two levels down never heard that it left: whatever it subscribed to in
   * `onMount` went on running, and its `setState` drew it back into the page the visitor had
   * navigated to. One timer per visit, kept alive by the subscription that outlived its component.
   *
   * The component's own `onUnmount` runs FIRST, which is the order a single store produces too:
   * parents are resolved before their children in a pass, so they leave in that order as well.
   */
  notifyUnmounted(): void {
    if (!this._lifecycleMounted) return;
    this._lifecycleMounted = false;
    this.onUnmount();
  }

  // Separate from `_mounted`, which is the RENDER MANAGER's flag (whether a queued re-render may
  // flush). This one is the lifecycle's, and a nested instance has the second without the first.
  private _lifecycleMounted = false;

  render(): HtmlNode {
    const context: RenderContext = {
      getService: <T>(key: import('./types').ServiceKey<T>) =>
        this.serviceProvider.getService<T>(key),
      serviceProvider: this.serviceProvider,
      route: getCurrentRoute(),
      inFlow: getInFlow(),
      theme: getPhotonTheme(),
      // The SAME density and scale the lowering hands every component it expands. Without these two
      // a page and its own subtree render at different sizes: the page's context defaulted to
      // comfortable while everything the lowering expanded took the ambient (compact on a pointer
      // device), so a code editor drew 19dp lines and placed its caret on a 17dp grid — a caret
      // that drifts a line and a half down a screenful of code, for no reason anyone can see.
      density: getPhotonDensity(),
      typeScale: getPhotonTypeScale(),
      measureText: measurePhotonText,
      monoAdvance: photonMonoAdvance,
    };
    if (!this._mounted) adoptServerState(this);
    // Reconciler pass (W6 slice 2): as a page root this component persists by itself; its store
    // retains the nested shared stateful its build creates. When hosted inside another page's
    // render this JOINS the outer pass instead (the host page owns retention).
    enterPass(this._instances, () => this._scheduleRender());
    try {
      return (reconcileBuildRoot(this.build(context)) as Component).render();
    } catch (error) {
      return renderComponentFailure(this.constructor.name, error);
    } finally {
      exitPass();
    }
  }

  mount(container: HTMLElement): void {
    const node = this.render();
    if (this._renderManager.canHydrate(container)) {
      this._renderManager.hydrate(node, container);
    } else {
      this._renderManager.mount(node, container);
    }
    this._mounted = true;
    // The root has no parent to mount it — see notifyMounted.
    this.notifyMounted();
  }

  hydrate(container: HTMLElement): void {
    this._renderManager.hydrate(this.render(), container);
    this._mounted = true;
    // The root has no parent to mount it — see notifyMounted.
    this.notifyMounted();
  }

  mountReconcile(container: HTMLElement, previousNode: HtmlNode | null): HtmlNode {
    const node = this.render();
    this._renderManager.adopt(node, container, previousNode);
    this._mounted = true;
    // The root has no parent to mount it — see notifyMounted.
    this.notifyMounted();
    return node;
  }

  /** The virtual tree currently reflected in the DOM (the diff baseline for the next navigation). */
  getCurrentTree(): HtmlNode | null {
    return this._renderManager.getCurrentNode();
  }

  /** Marking `_mounted=false` makes any queued re-render flush a no-op (see StatefulComponent). */
  disposeQuietly(): void {
    this._mounted = false;
    // Navigating away is this page leaving the tree — whatever onMount subscribed to unsubscribes
    // here, and its nested components leave with the store that held them.
    this.notifyUnmounted();
    this._instances.unmountAll();
  }

  _scheduleRender(): void {
    // Retained-nested path: bubble the invalidation to the host page; the host's re-render
    // reconciles this position back onto this same instance (state intact, config re-adopted).
    if (this._invalidationHook) {
      this._invalidationHook();
      return;
    }
    if (this._renderScheduled) return;
    this._renderScheduled = true;
    // Frame-or-timer (see scheduleRenderFlush): a hidden tab must still repaint on setState.
    scheduleRenderFlush(() => {
      this._renderScheduled = false;
      if (this._mounted) {
        this._renderManager.update(this.render());
      }
    });
  }

  unmount(): void {
    if (this._mounted) {
      this._renderManager.unmount();
      this._mounted = false;
    }
  }

  getVirtualNode(): HtmlNode {
    return this.render();
  }
}

/**
 * Base class for component state
 */
export abstract class ComponentState<TComponent extends StatefulComponent = StatefulComponent> {
  private _component: TComponent | null = null;
  _context: RenderContext | null = null;
  _needsRender = false;

  get component(): TComponent {
    if (!this._component) {
      throw new Error('State not initialized');
    }
    return this._component;
  }

  setComponent(component: TComponent): void {
    this._component = component;
  }

  /**
   * Update state and trigger re-render
   */
  protected setState(fn: () => void): void {
    fn();
    this._needsRender = true;
    this.component._scheduleRender();
  }

  /**
   * Build the component tree
   */
  // The union documents today's two authoring worlds compiling to the SAME runtime base: Core
  // pages build Component trees (this render path), write-once components build vocabulary
  // values (expanded by the lowering — they never reach render() below).
  abstract build(context: RenderContext): Component | VisualNodeValue;

  // Lifecycle hooks
  onInit(): void {}
  onMount(): void {}
  onUpdate(): void {}
  onDispose(): void {}
}
