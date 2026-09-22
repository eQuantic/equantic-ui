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
import { componentIdentity } from './component-identity';
import { scheduleRenderFlush } from './render-scheduler';

/**
 * Base class for stateless components
 */
/**
 * SERVER DATA (the C# `IServerPrefetch` twin): the fields the server's prefetch filled arrive as
 * `window.__INITIAL_STATE__` — one field map PER COMPONENT, under the name the server's realizer
 * gave it (`Type#ordinal`), each landing BEFORE that component builds, so the client's first tree is
 * the one the server already wrote as HTML and hydration matches instead of flashing the field
 * defaults. Inside a component's entry the keys are the field names it declares.
 *
 * NOT consumed once, which the flat single-owner payload was: it stays for the life of the document
 * because the walk below runs on EVERY render of the root, and a page composing three prefetchers
 * would otherwise have had the first one swallow the payload for the other two. What stops it being
 * re-applied is the root adopting only while unmounted, not the entry going away.
 *
 * Shared by both write-once page bases (stateless and stateful pages prefetch identically).
 */
const componentOrdinals = new Map<string, number>();

/**
 * The name the server gave this component: `Type#ordinal` in depth-first expansion order.
 *
 * BOTH SIDES COUNT, and that is the whole identity mechanism. The client does not receive
 * components — it re-runs `build()` and constructs fresh ones — so the payload has to say which
 * component each field map belongs to. The server names them as the realizer expands them
 * (`ComponentExpansionScope`), and the same walk here produces the same names.
 *
 * The type half is the component's IDENTITY — the CLR full name eqc writes onto every component
 * class as `static $typeId`, and the server's `ComponentIdentity.Of` answers for the same type. It
 * was `constructor.name`: the simple name, shared by every `Row` in every namespace, and alive only
 * while neither bundler passed `--minify-identifiers` (#278). A string the compiler wrote survives
 * any bundler. Pinned by `server-state.spec.ts`.
 */
export function nextComponentKey(typeName: string): string {
  const seen = componentOrdinals.get(typeName) ?? 0;
  componentOrdinals.set(typeName, seen + 1);
  return `${typeName}#${seen}`;
}

export { componentIdentity };

/** Starts a fresh count — one render is one walk, and the numbering restarts with it. */
export function resetComponentKeys(): void {
  componentOrdinals.clear();
}

/**
 * Hands a component the fields the server loaded FOR IT, by key. Returns whether anything landed.
 *
 * The type half of the key is what makes a drift safe: if the two sides ever expand different
 * trees, the ordinal alone would hand a component whatever sat at that number. Matching on the type
 * means a disagreement leaves the component with its own defaults — a missing value rather than a
 * wrong one. The type is the full identity (`componentIdentity`), so two components called `Row`
 * from different namespaces are two types to that check, as they are to the compiler.
 */
const alreadyAdopted = new WeakSet<object>();

export function adoptServerStateFor(target: object, key: string): boolean {
  if (typeof window === 'undefined') return false;

  // ONCE PER INSTANCE, and the payload staying is exactly why this is needed. The lowering resolves
  // a stateful child against the instance store and gets the SAME object back on every pass, so
  // every re-render of anything above it wrote the server's original fields over whatever the
  // reader had changed — a filter that reset itself each time the page redrew. A fresh instance is
  // a different object and still restores. The root has said this since the first version, as
  // `adopt: !this._mounted`; this is the same rule for everything below it.
  if (alreadyAdopted.has(target)) return false;

  const w = window as unknown as {
    __INITIAL_STATE__?: Record<string, Record<string, unknown>>;
  };
  const payload = w.__INITIAL_STATE__?.[key];
  if (!payload) return false;

  alreadyAdopted.add(target);
  return applyServerFields(target, payload);
}

/**
 * How deep in the walk this render is. Zero means nothing is in progress, so the next thing to run
 * owns the count and restarts it.
 *
 * THE WALK IS BRACKETED rather than marked, and that is worth stating because three separate
 * markers were tried first and each was a guess made at a call site. `render()` is one method and
 * everything reaches it — a page root, the component a `build()` returned, a component the lowering
 * met at its mixing seam — so whether a call OWNS the count is a property of when it is called,
 * which only a bracket can answer.
 *
 * What is NOT a property of when it is called is whether the component gets a key. It always does,
 * because the server's realizer enters every `UiComponent` it expands and these classes are that
 * twin; a Core element transpiles to `HtmlElement` and never reaches this file. Reading one of
 * those questions off the other is what produced the markers.
 */
let walkDepth = 0;

/**
 * Opens the walk around ONE lowering, and NAMES NOTHING: a lowering is not a component.
 *
 * Every lowering goes through `lowerVisualNode`, so this is where an outermost one restarts the
 * count. A Core page's bridge to a write-once subtree reaches the lowering with no component render
 * of its own, and nothing else would restart it — the same child was named `Type#0` on one render
 * and `Type#1` on the next, so every render after the first left it with its defaults.
 *
 * Around the whole lowering rather than around each component inside it, which is the difference
 * that makes this its own function: restarting per component would restart the count between two
 * SIBLINGS and hand both of them `#0`.
 *
 * An escape-hatch page renders inside ONE component walk too (`EscapeHatchPage`), so the bridges
 * it holds are nested lowerings that continue its count, exactly as the server counts one page
 * render across all of them (#279).
 */
export function runLoweringWalk<T>(run: () => T): T {
  if (walkDepth === 0) resetComponentKeys();

  walkDepth++;
  try {
    return run();
  } finally {
    walkDepth--;
  }
}

/**
 * Runs ONE component's render inside the walk: it takes its key, adopts what the server loaded for
 * it while it is unmounted, and restarts the count first if nothing else is in progress.
 *
 * EVERY RENDER, not only the first. The count lives across renders, and a root that re-rendered
 * without restarting it handed the components below `Type#1`, `Type#2`, … — keys the payload does
 * not name — so a composed component silently reverted to its defaults on the second pass.
 *
 * `adopt` is false once the root is mounted: the payload is the first render's answer, and applying
 * it again would undo whatever the page has done since. The key is taken either way, or everything
 * after it shifts by one on exactly the renders where adoption is off.
 */
export function runComponentWalk<T>(root: object, adopt: boolean, run: () => T): T {
  // THE OUTERMOST RENDER RESTARTS THE COUNT, and only that one: a render inside a walk joins it,
  // because the count it would have restarted belongs to the root above.
  if (walkDepth === 0) resetComponentKeys();

  // EVERY COMPONENT CONSUMES ITS KEY, nested or not, adopting or not. The realizer enters every
  // `UiComponent` it expands and these classes ARE that twin — a Core element transpiles to
  // `HtmlElement` and never arrives here — so a component that took no ordinal here would shift
  // every one after it. Not adopting is a separate question: the payload is the first render's
  // answer, and applying it again would undo whatever the page has done since.
  const key = nextComponentKey(componentIdentity(root));
  if (adopt) adoptServerStateFor(root, key);

  walkDepth++;
  try {
    return run();
  } finally {
    walkDepth--;
  }
}

function applyServerFields(target: object, payload: Record<string, unknown>): boolean {
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
  // NOT DELETED once read, which the flat payload did: it was a single-render handoff to one owner,
  // and there is now one entry per component. A page composing three prefetchers would have had the
  // first one swallow the whole payload for the other two.
  return adopted;
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
    // AROUND THE WHOLE RENDER, because what `build()` returns is rendered through this same method:
    // outside the walk that child would restart the count and claim `#0` for itself, which for a
    // component that builds another of its own type gave every level of a recursive tree the ROOT's
    // data.
    const root = walkRoots.get(this) ?? this;
    return runComponentWalk(root, !this._mounted, () => {
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
        return renderComponentFailure(root.constructor.name, error);
      } finally {
        exitPass();
      }
    });
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
 * The component a page hands the walk as ITS root, when the page is not the component rendering.
 * Kept off the class so no member name joins the list a C# component may not shadow (EQ2011).
 */
const walkRoots = new WeakMap<object, object>();

/**
 * The client half of an ESCAPE-HATCH page: a page written as DOM (`Web.IComponent`, routed with
 * `MapPage<T>`) rather than as a write-once component.
 *
 * SSR has always drawn such a page, run its prefetch and shipped its state; the browser did nothing
 * with it. The boot script mounts through `mount`, `hydrate` or `mountReconcile`, declared on the
 * two component bases only, so a transpiled `HtmlElement` page fell through to a `render()` that
 * attaches nothing — no hydration, no handlers, no client-side navigation to or from it (#279).
 *
 * It is hosted here by a stateless page whose build IS the element, so it hydrates, mounts,
 * reconciles and disposes through exactly the code every other page uses. The walk's root is the
 * element itself: it takes the key the server reserved for it and adopts the state the server
 * shipped for it, and every write-once bridge it holds is a nested lowering that continues the
 * page's one count — the count the server keeps across the whole page render.
 */
export class EscapeHatchPage extends StatelessComponent {
  /** The page this hosts — what the walk names, and what hot reload captures. */
  constructor(readonly page: Component) {
    super();
    walkRoots.set(this, page);
  }

  build(): Component {
    return this.page;
  }
}


/**
 * The stateful component — the twin of `eQuantic.UI.Primitives.StatefulComponent`: state lives as
 * fields on the component itself and `setState` triggers the rebuild directly. eqc emits every
 * stateful component against this base.
 */
export abstract class StatefulComponent extends Component {
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
    // AROUND THE WHOLE RENDER, for the reason the stateless root states.
    return runComponentWalk(this, !this._mounted, () => {
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
    });
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

