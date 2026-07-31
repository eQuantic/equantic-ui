/**
 * eQuantic.UI Runtime - Stateful Component Support
 */

import { Component, HtmlNode, RenderContext } from './types';
import type { VisualNodeValue } from '../shared/nodes';
import { RenderManager } from '../dom/renderer';
import { getRootServiceProvider, ServiceProvider } from './service-provider';
import { hydrateValue } from '../utils/hydrate-value';
import { getCurrentRoute } from '../router/current-route';
import { ComponentInstanceStore, enterPass, exitPass } from '../shared/instance-store';
import { getPhotonTheme } from '../shared/photon-context';
import { scheduleRenderFlush } from './render-scheduler';

/**
 * Base class for stateless components
 */
export abstract class StatelessComponent extends Component {
  private _renderManager: RenderManager = new RenderManager();
  private _instances = new ComponentInstanceStore();

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
      theme: getPhotonTheme(),
    };
    // Reconciler pass fence (W6 slice 2): a stateless page cannot re-render, so nested shared
    // stateful get no invalidator — their state persists but repaints only with the next render.
    enterPass(this._instances, null);
    try {
      const component = this.build(context) as Component;
      return component.render();
    } finally {
      exitPass();
    }
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
  }

  /**
   * SPA-navigation mount: reconcile into a root that already holds the outgoing page's DOM (described by
   * `previousNode`) so a shared layout shell is preserved instead of torn down. Returns the rendered tree
   * — the host tracks it as the new "current" tree for the next navigation's diff.
   */
  mountReconcile(container: HTMLElement, previousNode: HtmlNode | null): HtmlNode {
    const node = this.render();
    this._renderManager.adopt(node, container, previousNode);
    return node;
  }

  /** The virtual tree currently reflected in the DOM (the diff baseline for the next navigation). */
  getCurrentTree(): HtmlNode | null {
    return this._renderManager.getCurrentNode();
  }

  /** Stateless components hold no lifecycle/DOM ownership to release on navigation away. */
  disposeQuietly(): void {}

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
        // Fallback legacy name check
        if (typeof (this._state as any)._setComponent === 'function') {
          (this._state as any)._setComponent(this);
        }
      }

      // Hydrate state from SSR if available
      let hydrated = false;
      if (typeof window !== 'undefined' && (window as any).__INITIAL_STATE__ && this._state) {
        const initialState = (window as any).__INITIAL_STATE__;

        // Copy data properties from SSR state to client state.
        // Server preserves original field names (including underscore prefix).
        // The existing field's default value reveals its runtime type, so values that crossed the
        // wire as strings (decimal -> Decimal, long -> bigint) are coerced back to that type —
        // see hydrateValue. Plain fields are assigned verbatim.
        const state = this._state as unknown as Record<string, unknown>;
        Object.keys(initialState).forEach((key) => {
          if (this._state && typeof initialState[key] !== 'function' && key in this._state) {
            state[key] = hydrateValue(state[key], initialState[key]);
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
      theme: getPhotonTheme(),
    };
    this.state._context = context;

    // Reconciler pass (W6 slice 2): everything lowered during this page render — including
    // VisualNodeComponent bridges — reconciles against this page's one retention pass; nested
    // shared stateful retained inside it invalidate by re-rendering THIS page.
    enterPass(this._instances, () => this._scheduleRender());
    try {
      const component = this.state.build(context) as Component;
      return component.render();
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
   * the lowering expands it through the positional store (retention) instead of the legacy mixing
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

  render(): HtmlNode {
    const context: RenderContext = {
      getService: <T>(key: import('./types').ServiceKey<T>) =>
        this.serviceProvider.getService<T>(key),
      serviceProvider: this.serviceProvider,
      route: getCurrentRoute(),
      theme: getPhotonTheme(),
    };
    // Reconciler pass (W6 slice 2): as a page root this component persists by itself; its store
    // retains the nested shared stateful its build creates. When hosted inside another page's
    // render this JOINS the outer pass instead (the host page owns retention).
    enterPass(this._instances, () => this._scheduleRender());
    try {
      return (this.build(context) as Component).render();
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
  }

  hydrate(container: HTMLElement): void {
    this._renderManager.hydrate(this.render(), container);
    this._mounted = true;
  }

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

  /** Marking `_mounted=false` makes any queued re-render flush a no-op (see StatefulComponent). */
  disposeQuietly(): void {
    this._mounted = false;
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
