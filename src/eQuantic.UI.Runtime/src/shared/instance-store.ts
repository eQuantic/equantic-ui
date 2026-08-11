/**
 * The positional reconciler's WEB mirror (plan W6, slice 2 — the exact identity contract of the C#
 * `ComponentInstanceStore`): nested SHARED-STATEFUL component instances retain identity — and
 * therefore state — across page re-renders. Identity = lowering PATH + constructor name + optional
 * key. A parent's build() constructs fresh instances every pass; a retained match ADOPTS the fresh
 * configuration through the transpiled `adoptConfig(next)` and keeps its state fields. Identity
 * mismatches drop retention (state resets).
 *
 * Ownership differs from native by necessity: PhotonHost is one per surface, but the web has SPA
 * navigation and many render roots. Each PAGE INSTANCE owns a store (a new page starts clean — no
 * cross-page state bleed), and a render wraps a PASS on the ambient stack: nested lowerings — e.g.
 * `VisualNodeComponent` bridges, which are rebuilt every pass and so cannot own retention — JOIN the
 * page's pass instead of opening their own. Each `lowerVisualNode` inside a pass takes a unique
 * root prefix (`r0`, `r1`, …, stable while the page structure is stable) so several bridges on one
 * page cannot collide on identity paths.
 */

import { commitShortcuts } from '../dom/shortcuts';
import { attachCameraStreams } from './devices/camera';
import { commitScrollViewports } from './scroll-viewports';
import { commitInViewObservers } from './in-view';

/** The duck-typed surface of a transpiled shared-stateful instance (marker set by the base class). */
interface SharedStatefulLike {
  _sharedStateful?: boolean;
  _invalidationHook?: (() => void) | null;
  key?: string | null;
  adoptConfig?(next: unknown): void;
  /** Idempotent lifecycle delivery — the C# `NotifyMounted`/`NotifyUnmounted` twins. */
  notifyMounted?(): void;
  notifyUnmounted?(): void;
  build(context: unknown): unknown;
}

export class ComponentInstanceStore {
  private retained = new Map<string, SharedStatefulLike>();
  private visited = new Map<string, SharedStatefulLike>();
  private rootCounter = 0;
  // Mounts are QUEUED and delivered when the pass closes, never mid-build: a hook running inside
  // reconcile() runs while its own parent is still building, so a setState from it would mutate a
  // tree half-way through being produced.
  private mounting: SharedStatefulLike[] = [];

  /** Starts a pass: root prefixes restart so bridge N maps to the same identity space every render. */
  beginPass(): void {
    this.rootCounter = 0;
  }

  /**
   * Ends the pass: entries not visited are DROPPED — their position left the tree, which is exactly
   * when `onUnmount` is owed. The instances that ENTERED this pass mount here, once the tree they
   * belong to is finished (C# `ComponentInstanceStore.EndPass` twin).
   */
  endPass(): void {
    const previous = this.retained;
    this.retained = this.visited;
    this.visited = previous;

    // `previous` is the PREVIOUS pass: anything in it this pass did not keep is gone.
    for (const [identity, instance] of previous) {
      if (this.retained.get(identity) !== instance) instance.notifyUnmounted?.();
    }

    this.visited.clear();

    // Mounted LAST, so a component's onMount sees a tree in which everything that left has already
    // unsubscribed — the order that lets two components share one exclusive resource across a swap.
    for (const instance of this.mounting) instance.notifyMounted?.();
    this.mounting.length = 0;
  }

  /** Tears the store down — every retained instance leaves the tree at once (a surface closing). */
  unmountAll(): void {
    for (const instance of this.retained.values()) instance.notifyUnmounted?.();
    this.retained.clear();
    this.visited.clear();
    this.mounting.length = 0;
  }

  /** The unique path root for the next `lowerVisualNode` of this pass. */
  nextRootPath(): string {
    return `r${this.rootCounter++}`;
  }

  /**
   * Resolves the instance at `path`: retained on identity match (adopting the fresh config),
   * otherwise the fresh instance starts retention. Either way the instance's invalidation hook is
   * pointed at the pass owner. Non-stateful nodes pass through untouched.
   */
  reconcile(path: string, fresh: unknown, invalidator: (() => void) | null): unknown {
    const candidate = fresh as SharedStatefulLike;
    if (candidate._sharedStateful !== true) return fresh;

    const identity = `${path}#${(fresh as object).constructor.name}#${candidate.key ?? ''}`;
    const retained = this.retained.get(identity);
    if (retained && retained !== candidate) {
      retained.adoptConfig?.(fresh);
      retained._invalidationHook = invalidator;
      this.visited.set(identity, retained);
      return retained;
    }

    candidate._invalidationHook = invalidator;
    this.visited.set(identity, candidate);
    this.mounting.push(candidate);
    return candidate;
  }
}

// ---- ambient pass ---------------------------------------------------------------------------------

interface ActivePass {
  store: ComponentInstanceStore;
  invalidator: (() => void) | null;
  depth: number;
}

let activePass: ActivePass | null = null;

/**
 * Opens a reconciler pass owned by `owner` (a page instance's store), or JOINS the pass already in
 * flight — the outermost owner's store and invalidator stay in charge, so bridges and low-level
 * components rendering inside a page all reconcile into the page's retention. Pair with `exitPass`
 * in a try/finally.
 */
export function enterPass(owner: ComponentInstanceStore, invalidator: (() => void) | null): void {
  if (activePass) {
    activePass.depth++;
    return;
  }
  owner.beginPass();
  activePass = { store: owner, invalidator, depth: 1 };
}

export function exitPass(): void {
  if (!activePass) return;
  activePass.depth--;
  if (activePass.depth > 0) return;
  activePass.store.endPass();
  // Spec S8: the pass's Shortcut declarations become the live set — bindings whose subtree was not
  // re-lowered drop out, which is how unmounting unsubscribes.
  commitShortcuts();
  // A CameraPreview lowered this pass has a fresh <video>; the stream re-attaches here, the same
  // after-the-pass moment the shortcut set commits.
  attachCameraStreams();
  // Scroll views measure their viewport (and adopt an initial offset) here too — a windowed list
  // is (offset, viewport), and neither is knowable before the pass has mounted.
  commitScrollViewports();
  // An element cannot be observed before it exists — this is the moment it does.
  commitInViewObservers();
  activePass = null;
}

/** The pass in flight, if any — lowering consults it to reconcile component positions. */
export function getActivePass(): {
  store: ComponentInstanceStore;
  invalidator: (() => void) | null;
} | null {
  return activePass;
}
