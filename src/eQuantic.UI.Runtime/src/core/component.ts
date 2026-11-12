/**
 * eQuantic.UI Runtime - Stateful Component Support
 */

import { Component, HtmlNode, RenderContext } from './types';
import type { VisualNodeValue } from '../shared/nodes';
import type { CrossAlignValue } from '../shared/enums.generated';
import type { ComponentElement } from '../shared/components/ComponentElement';
import { RenderManager } from '../dom/renderer';
import { getRootServiceProvider, ServiceProvider } from './service-provider';
import { hydrateValue } from '../utils/hydrate-value';
import { hydrate, type HydrationSpec } from '../utils/hydrate';
import { adoptMember } from '../utils/adopt-member';
import { getCurrentRoute } from '../router/current-route';
import { ElementRoots, enterPass, exitPass, lowerRoot, relowerRoot, type Relowered } from '../shared/element-roots';
import { partialCommitIsExact } from '../dom/shortcuts';
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
 * gave it (a page's own component `Type#0`, every other one where it stands, `r{tree}/{type}@{slots}`,
 * read off its position in `lowering.ts`), each landing BEFORE that component builds, so the client's first tree is
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
 * The name the server gave a component a walk names by count: `Type#ordinal` in depth-first expansion
 * order, which leaves a page's own component `Type#0`. Every component a page composes is named by its
 * position instead (#1477), since the order the browser builds in is not the server's.
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
  treesLowered = 0;
}

/**
 * How many trees of the vocabulary the walk in flight has lowered: a page's own tree, or each bridge
 * of an escape-hatch page in the order the page renders them. The server counts its trees per render
 * the same way, and a component's server data key starts with its tree (`r0`, `r1`, …, #1477).
 */
let treesLowered = 0;

/** The next tree of the walk in flight, as its server data keys name it. */
export function nextTree(): string {
  return `r${treesLowered++}`;
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

/**
 * Whether the server may still hold data for `target`: the document carries some, and this instance
 * has not taken its share. Asked before a key is formed, because naming a position walks it to the
 * root, and most builds (every one on a page the server loaded nothing for, every later build of a
 * kept instance) have nothing to adopt.
 */
export function awaitsServerState(target: object): boolean {
  if (typeof window === 'undefined' || alreadyAdopted.has(target)) return false;
  const payload = (window as unknown as { __INITIAL_STATE__?: Record<string, unknown> }).__INITIAL_STATE__;
  return payload !== undefined && payload !== null && Object.keys(payload).length > 0;
}

export function adoptServerStateFor(target: object, key: string): boolean {
  if (typeof window === 'undefined') return false;

  // ONCE PER INSTANCE, and the payload staying is exactly why this is needed. The lowering resolves
  // a stateful child to the instance its position keeps, the SAME object on every render, so
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
  // THE CLASS'S MAP is the one description of what crosses: the compiler emits `static $hydration`
  // from the hydration manifest, the same entries the server writes its payload from, so it lists
  // every value the server carries here. A value with a wire spec is coerced by what it IS; one
  // marked `'declared'` keeps the witness path (the default value reveals the type).
  const specs = (target.constructor as { $hydration?: Record<string, HydrationSpec | 'declared'> })
    .$hydration;
  if (specs === undefined) return false;
  let adopted = false;
  for (const key of Object.keys(payload)) {
    // LISTED, not held. A captured constructor parameter the router did not pass is never assigned,
    // so the instance holds no such property until the server's value lands, and a rule that asked
    // the instance refused it. A key the map does not list is stale payload, never a new field.
    // OWN keys of the map only: every object answers `in` for `__proto__` and `constructor`, and
    // assigning the first swapped the component's prototype for the payload's object.
    if (!Object.prototype.hasOwnProperty.call(specs, key) || typeof payload[key] === 'function') continue;
    const spec = specs[key];
    adoptMember(
      target,
      key,
      spec === 'declared'
        ? hydrateValue((target as Record<string, unknown>)[key], payload[key])
        : hydrate(payload[key], spec),
    );
    adopted = true;
  }
  // NOT DELETED once read, which the flat payload did: it was a single-render handoff to one owner,
  // and there is now one entry per component. A page composing three prefetchers would have had the
  // first one swallow the whole payload for the other two.
  return adopted;
}

/**
 * The C# `UiComponent`: a component IS a node of the vocabulary (`UiComponent : VisualNode`), as a
 * Flutter `StatelessWidget` is a `Widget`. Its kind is `component`, it carries the node's key, and
 * nested inside an abstract tree the lowering expands it through the positional store, the server
 * data its ordinal names and the error boundary: one door for every component, with or without state.
 * <p>
 * The web family (`HtmlElement`, the DOM escape hatch) is a `Component` and not one of these, as in
 * C#, where `HtmlElement` is an `IComponent` and no node: it carries no `nodeKind`, and the lowering
 * lets it render itself. `component-is-a-node.type-test.ts` holds this class to every member the
 * vocabulary's `VisualNode` declares, so the two cannot drift apart in silence.
 */
export abstract class UiComponent extends Component {
  /** The shared-vocabulary discriminator. As a page ROOT it is inert: a page mounts through render(). */
  readonly nodeKind = 'component' as const;

  /** The C# `VisualNode.Key` mirror: part of the reconciler identity (path + type + key). */
  key: string | null = null;

  /**
   * The C# `VisualNode.Bookmark`, `AlignSelf` and `GridSpan`, which a component carries as any node
   * does. Fields rather than `declare`d types: the lowering reads them off the component, and a field
   * is what eqc's list of runtime members sees, so an app member of one of these names is refused
   * at build time instead of becoming the element's id or its alignment.
   */
  bookmark?: string | null;
  alignSelf?: CrossAlignValue | null;
  gridSpan?: number;

  abstract build(context: RenderContext): Component | VisualNodeValue;

  abstract getVirtualNode(): HtmlNode;

  /**
   * The C# `UiComponent.AdoptConfig`: a position that keeps its instance hands it the configuration of
   * the fresh one its parent built. The default copies nothing, as C#'s does, and a component with
   * configuration of its own overrides it; the element engine calls it on every instance it keeps.
   */
  adoptConfig(_next: UiComponent): void {}
}

/**
 * The context a page builds with: the services, the route and the SAME density and scale the lowering
 * hands every component it builds. Without these two a page and its own subtree render at different
 * sizes: the page's context defaulted to comfortable while everything the lowering expanded took the
 * ambient (compact on a pointer device), so a code editor drew 19dp lines and placed its caret on a
 * 17dp grid — a caret that drifts a line and a half down a screenful of code, for no reason anyone
 * can see.
 */
function pageContext(serviceProvider: ServiceProvider): RenderContext {
  return {
    getService: <T>(key: import('./types').ServiceKey<T>) => serviceProvider.getService<T>(key),
    serviceProvider,
    route: getCurrentRoute(),
    inFlow: getInFlow(),
    theme: getPhotonTheme(),
    density: getPhotonDensity(),
    typeScale: getPhotonTypeScale(),
    measureText: measurePhotonText,
    monoAdvance: photonMonoAdvance,
  };
}

/**
 * One render of a PAGE, the same for both bases: inside the server-data walk the page is named in,
 * and inside the pass its element trees are built in (`ElementRoots`).
 *
 * A render the page's owners asked for (a nested component's `setState` marked its own position)
 * lowers the tree the page built LAST, so every root hands its owner the node it handed it before and
 * the owner builds the marked positions and nothing else; building the page again would hand every
 * position a fresh configuration, and the engine rebuilds a component whose configuration changed.
 * Any other render builds the page and rebuilds every position.
 *
 * What the page's build returned is lowered as a vocabulary tree when it is one, a component included,
 * so a component returned AS THE ROOT is held at the root of an owner like any other and keeps its
 * state across the render its own `setState` asks for. A Core tree renders itself, and the bridges in
 * it lower their own roots through the same pass.
 */
function renderPage(
  root: object,
  mounted: boolean,
  roots: ElementRoots,
  invalidate: () => void,
  build: () => Component | VisualNodeValue,
): HtmlNode {
  const reuse = roots.takeReuse();
  // AROUND THE WHOLE RENDER, because what `build()` returns is rendered through this same walk:
  // outside it that child would restart the count and claim `#0` for itself, which for a component
  // that builds another of its own type gave every level of a recursive tree the ROOT's data.
  return runComponentWalk(root, !mounted, () => {
    enterPass(roots, invalidate, !reuse);
    try {
      const built = reuse ? (roots.last as Component | VisualNodeValue) : build();
      roots.remember(built);
      return (built as { nodeKind?: string }).nodeKind === undefined
        ? (built as Component).render()
        : lowerRoot(built as VisualNodeValue);
    } catch (error) {
      // A PAGE has no parent to contain it. Its own throw used to leave the root unwritten, which
      // is the white screen the boundary exists to end. Nothing is kept: the next render builds.
      roots.remember(undefined);
      return renderComponentFailure(root.constructor.name, error);
    } finally {
      exitPass();
    }
  });
}

/**
 * A render the page's OWNERS asked for, answered by what they built (E3b-3, #1455): the owner builds
 * the marked positions, each component that built is lowered alone, and only the DOM it drew is
 * patched. The page's own build does not run, its other components are not lowered, and the
 * reconciler reads none of the rest of the page.
 *
 * False when the render cannot be answered so, and `render()` then lowers the page whole as before:
 * a render that is not only the owners' (the page's own state, a theme, a navigation), a page that
 * lowered more than one root (a Core page's bridges, found only by walking it), a page whose build is
 * a Core tree, or a component the lowering cannot lower alone (`relowerBuilt`). The owner's build is
 * not repeated by that render: what it built is no longer marked.
 */
function patchPage(root: object, roots: ElementRoots, invalidate: () => void, manager: RenderManager): boolean {
  const last = roots.last as { nodeKind?: string } | undefined;
  if (!roots.willReuse || roots.rootCount !== 1 || !last || last.nodeKind === undefined) return false;
  let answer: Relowered | null = null;
  runComponentWalk(root, false, () => {
    enterPass(roots, invalidate, false);
    try {
      answer = relowerRoot(last as VisualNodeValue);
      // Bindings with no place in the list yet are placed by a whole render.
      if (answer && !partialCommitIsExact((answer as Relowered).declaredBy)) answer = null;
    } finally {
      // The bindings the components lowered again declared give way to the ones they declared now;
      // a render that falls back replaces them all in the whole render that follows.
      const relowered = answer as Relowered | null;
      exitPass(relowered ? relowered.declaredBy : () => false);
    }
  });
  const relowered = answer as Relowered | null;
  if (!relowered) return false;
  for (const patch of relowered.patches) {
    if (!manager.patch(patch.drawn, patch.next)) return false;
  }
  roots.takeReuse();
  return true;
}

export abstract class StatelessComponent extends UiComponent {
  private _renderManager: RenderManager = new RenderManager();
  /** The element trees this page builds through, which keep the stateful components it composes. */
  private _instances = new ElementRoots(() => this._scheduleRender(true));
  /** The page's own invalidator, ONE for its life: what a measure in a shipped face holds on to, weakly,
   *  to rebuild the page should the face be taken back (#1056). A function made per pass is a new one
   *  every render, so nothing could hold it without holding every render. */
  private readonly _invalidate = (): void => this._scheduleRender();
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
    // A stateless page IS re-renderable: build is pure and its element trees keep the nested stateful
    // components across renders, so a nested `setState` renders this page again, exactly like a
    // stateful host. (The old "no invalidator" fence made every stateful child of a stateless page
    // render-once: the site's mega menu opened its state and nothing on screen ever changed.)
    const context = pageContext(this.serviceProvider);
    return renderPage(walkRoots.get(this) ?? this, this._mounted, this._instances, this._invalidate, () =>
      this.build(context),
    );
  }

  /**
   * Asks for a render. `onlyMarked` says the page's owners asked, because a nested component marked
   * its position: that render lowers the page's last tree again. Anything else (the page's own
   * invalidator, a theme, a culture) builds the page.
   */
  _scheduleRender(onlyMarked = false): void {
    if (!onlyMarked) this._instances.requestPageBuild();
    if (this._renderScheduled) return;
    this._renderScheduled = true;

    scheduleRenderFlush(() => {
      this._renderScheduled = false;
      if (!this._mounted) return;
      this._instances.beginScheduledRender();
      if (patchPage(walkRoots.get(this) ?? this, this._instances, this._invalidate, this._renderManager)) return;
      this._renderManager.update(this.render());
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
   * A stateless page has no lifecycle of its own. It does own ELEMENT TREES, and that changed what
   * this method owes: a stateless page keeps the nested stateful components it built, so what it
   * holds on navigation away is a set of components that each have an `onMount` that ran and an
   * `onUnmount` that is owed.
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
export abstract class StatefulComponent extends UiComponent {
  private _renderManager: RenderManager = new RenderManager();
  private _mounted = false;
  private _renderScheduled = false;
  /** The element trees this component builds through as a PAGE, which keep the stateful components
   *  it composes. A component nested in another page builds in that page's trees instead. */
  private _instances = new ElementRoots(() => this._scheduleRender(true));
  /** The page's own invalidator, ONE for its life: what a measure in a shipped face holds on to, weakly,
   *  to rebuild the page should the face be taken back (#1056). A function made per pass is a new one
   *  every render, so nothing could hold it without holding every render. */
  private readonly _invalidate = (): void => this._scheduleRender();

  protected get serviceProvider(): ServiceProvider {
    return getRootServiceProvider();
  }

  // The union documents today's two authoring worlds compiling to the SAME runtime base: Core
  // pages build Component trees (this render path), write-once components build vocabulary
  // values (expanded by the lowering — they never reach render() below).
  abstract build(context: RenderContext): Component | VisualNodeValue;

  /**
   * The position the element engine mounted this instance at (the C# `StatefulComponent.MountedAt`):
   * the first one, while a second position that holds the same instance waits in `_alsoMountedAt`.
   * Null while no live owner holds it: a page root, or a component laid out once.
   */
  mountedAt: ComponentElement | null = null;

  private _alsoMountedAt: ComponentElement[] | null = null;

  /** Taken by a position (the C# `EnterPosition`): true when it is the first, which is the one that mounts it. */
  enterPosition(at: ComponentElement): boolean {
    if (this.mountedAt === null) {
      this.mountedAt = at;
      return true;
    }
    (this._alsoMountedAt ??= []).push(at);
    return false;
  }

  /** Left by a position (the C# `LeavePosition`): true when it was the last, which is the one that unmounts it. */
  leavePosition(at: ComponentElement): boolean {
    const others = this._alsoMountedAt;
    if (this.mountedAt !== at) {
      const index = others?.indexOf(at) ?? -1;
      if (index >= 0) others!.splice(index, 1);
      return false;
    }
    if (others !== null && others.length > 0) {
      this.mountedAt = others.pop()!;
      return false;
    }
    this.mountedAt = null;
    return true;
  }

  /**
   * The C# `SetState(mutate)` contract: run the mutation, then ask for this instance to build again.
   * Held by an element tree, it marks the positions that hold it, and their owner asks its page for a
   * render that builds those positions and no other. A page root, which no position holds, renders
   * itself.
   */
  protected setState(fn: () => void): void {
    fn();
    const at = this.mountedAt;
    if (at === null) {
      this._scheduleRender();
      return;
    }
    at.markNeedsBuild();
    for (const also of this._alsoMountedAt ?? []) also.markNeedsBuild();
  }

  /**
   * Runs ONCE, when this instance has entered a live tree (C# `OnMount`) — the transpiled subclass
   * overrides it. NOT "the pixels exist": Photon has no DOM to read geometry from, so a hook
   * promising it could not be write-once.
   */
  onMount(): void {}

  /** Runs when this instance's position LEAVES the tree (C# `OnUnmount`) — unsubscribe here. */
  onUnmount(): void {}

  /**
   * Runs before this instance builds again because a value it read through `context.dependOn`
   * changed (C# `OnDependenciesChanged`, Flutter's `didChangeDependencies`) — the transpiled subclass
   * overrides it.
   */
  onDependenciesChanged(): void {}

  /** Called by the element engine before a build a changed dependency asked for (C#
      `NotifyDependenciesChanged`). */
  notifyDependenciesChanged(): void {
    this.onDependenciesChanged();
  }

  /** Called by the HOST — the element engine's owner, or `mount`/`hydrate` for a page root. Idempotent. */
  notifyMounted(): void {
    if (this._lifecycleMounted) return;
    this._lifecycleMounted = true;
    this.onMount();
  }

  /**
   * The pair — also idempotent, and a no-op for an instance that never entered the tree.
   *
   * What this component built leaves with its position: the engine takes a position out children
   * first and tells each component below, whatever its depth, that it left, before this one hears it
   * (Flutter's order: a component lets go of what it holds before the one that built it does).
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
    // As a page root this component keeps its own element trees; rendered inside another page's
    // render (a Core tree composing it), it JOINS that page's pass and builds into its trees.
    const context = pageContext(this.serviceProvider);
    return renderPage(this, this._mounted, this._instances, this._invalidate, () => this.build(context));
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
    // here, and its nested components leave with the element trees that held them.
    this.notifyUnmounted();
    this._instances.unmountAll();
  }

  /**
   * Asks for a render. `onlyMarked` says the page's owners asked, because a nested component marked
   * its position: that render lowers the page's last tree again. Anything else (the page's own
   * `setState`, a theme, a culture) builds the page.
   */
  _scheduleRender(onlyMarked = false): void {
    if (!onlyMarked) this._instances.requestPageBuild();
    if (this._renderScheduled) return;
    this._renderScheduled = true;
    // Frame-or-timer (see scheduleRenderFlush): a hidden tab must still repaint on setState.
    scheduleRenderFlush(() => {
      this._renderScheduled = false;
      if (!this._mounted) return;
      this._instances.beginScheduledRender();
      if (patchPage(walkRoots.get(this) ?? this, this._instances, this._invalidate, this._renderManager)) return;
      this._renderManager.update(this.render());
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

