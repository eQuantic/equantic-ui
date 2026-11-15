/**
 * The element trees a PAGE keeps, and the pass a render runs them in (docs/ELEMENT-TREE-PLAN.md 3.7,
 * the browser's line): every component a page composes is built through the transpiled engine's
 * `BuildOwner`, which keeps a stateful instance at its position across renders, and a nested
 * `setState` marks that position and nothing else.
 *
 * ONE OWNER PER ROOT the page's render lowers, not one for the page. The roots are found while the
 * render walks a Core tree (`HtmlElement`s, which the engine does not hold), so they are not known
 * before the walk: each `lowerVisualNode` inside a pass takes the next slot (`r0`, `r1`…, which stay
 * its path prefix), as the store this replaces kept one identity space per root. E3c's shell moves
 * the owner above the page, where one owner per document belongs.
 *
 * Each owner of a page answers its `BuildRequested` to the page, which renders again lowering the
 * tree it built last (`takeReuse`), so the owners build only what was marked.
 *
 * A LEAF as far as values go: the component base imports this, and the engine reaches the runtime's
 * export barrel, which reaches every component class, which extends that base. The lowering makes
 * the owners, through the one factory beside the engine's seam (`element-engine.ts`), and hands them
 * in here.
 */

import type { BuildOwner } from './components/BuildOwner';
import type { HtmlNode } from '../core/types';
import type { VisualNodeValue } from './nodes';
import { commitShortcuts, type ShortcutDeclarer } from '../dom/shortcuts';
import { commitFocusTraps } from '../dom/focus-trap';
import { installHoverRevealSuppression } from '../dom/hover-reveal';
import { attachCameraStreams } from './devices/camera';
import { scheduleScrollViewportCommit } from './scroll-viewports';
import { scheduleTableViewportCommit } from './table-viewports';
import { scheduleInViewCommit } from './in-view';
import { scheduleCanvasCommit } from './canvas-surface';
import { scheduleAnchorOffset } from './sticky-offset';
import { scheduleScrolledSync } from './scrolled-pinned';
import { setPassInvalidator } from './pass-invalidator';

/** What a page asked of its next render: nothing, only what its owners marked, or a build of itself. */
type Wanted = 'nothing' | 'marked' | 'build';

export class ElementRoots {
  /** The owner of each root slot, by the order this page's render reaches them. */
  private readonly slots: (BuildOwner | undefined)[] = [];
  private slotCount = 0;
  private wanted: Wanted = 'nothing';
  private reuse = false;
  /** What the page's own build returned last, which a render its owners asked for lowers again. */
  private lastBuilt: { readonly tree: unknown } | null = null;

  /**
   * @param onBuildRequested How the page hears that one of its owners has a marked position: the
   *   page's render scheduler. Null for an island rendered outside a page, which nothing renders
   *   again but its own parent, as before.
   */
  constructor(private readonly onBuildRequested: (() => void) | null) {}

  /** Starts a pass: the slots restart, so the bridge N of a render maps to the same owner every render. */
  beginPass(): void {
    this.slotCount = 0;
  }

  /** The next root of this pass: its path prefix, and the owner that builds it (made on first use). */
  nextRoot(create: () => BuildOwner): { path: string; owner: BuildOwner } {
    const slot = this.slotCount++;
    const owner = (this.slots[slot] ??= this.made(create));
    return { path: `r${slot}`, owner };
  }

  /**
   * Ends the pass. A root the render did not reach left the page, so its owner is taken down first,
   * then each root it reached ends its frame in slot order: what leaves an owner unmounts before what
   * arrives in it mounts (`BuildOwner.EndFrame`), and a tree that left whole has unmounted before any
   * of them. Across two roots that both changed, the earlier root's arrivals mount before the later
   * root's departures unmount: the engine orders one tree, and the browser does not reorder it.
   */
  endPass(): void {
    for (let slot = this.slots.length - 1; slot >= this.slotCount; slot--) this.slots[slot]?.unmountAll();
    this.slots.length = Math.min(this.slots.length, this.slotCount);
    for (const owner of this.slots) owner?.endFrame();
  }

  /** Takes every tree down: each component still in one hears, once, that it left. A page leaving. */
  unmountAll(): void {
    for (const owner of this.slots) owner?.unmountAll();
    this.slots.length = 0;
    this.lastBuilt = null;
    this.wanted = 'nothing';
  }

  /** An owner marked a position: the page renders again, lowering what it built last. */
  requestBuild(): void {
    if (this.wanted === 'nothing') this.wanted = 'marked';
    this.onBuildRequested?.();
  }

  /** Something other than an owner asked for the render (the page's own state, a theme, a face). */
  requestPageBuild(): void {
    this.wanted = 'build';
  }

  /**
   * Called when the render the page scheduled is about to run: whether only its owners asked, so the
   * render may lower the tree the page built last instead of building the page again. Read once,
   * by `takeReuse`, in the render that follows.
   */
  beginScheduledRender(): void {
    this.reuse = this.wanted === 'marked' && this.lastBuilt !== null;
    this.wanted = 'nothing';
  }

  /** Whether the render about to run lowers the page's last tree again, without taking the answer. */
  get willReuse(): boolean {
    return this.reuse;
  }

  /** How many roots the page's last render lowered: a render lowered by what its owners built answers
   *  a page of one root alone, since a bridge's root is found only by walking the page (#1455). */
  get rootCount(): number {
    return this.slots.length;
  }

  /** Whether this render lowers the page's last tree again. Any render not scheduled builds the page. */
  takeReuse(): boolean {
    const reuse = this.reuse;
    this.reuse = false;
    return reuse;
  }

  /** The tree the page built last, for a render its owners asked for. */
  get last(): unknown {
    return this.lastBuilt?.tree;
  }

  /** What the page's build returned, kept for the next render its owners ask for (null: nothing to keep). */
  remember(tree: unknown): void {
    this.lastBuilt = tree === undefined ? null : { tree };
  }

  private made(create: () => BuildOwner): BuildOwner {
    const owner = create();
    owner.buildRequested = () => this.requestBuild();
    return owner;
  }
}

// ---- ambient pass ---------------------------------------------------------------------------------

export interface ActivePass {
  readonly roots: ElementRoots;
  readonly invalidator: (() => void) | null;
  /** Whether every position builds (the page built itself), or only the marked ones. */
  readonly rebuildAll: boolean;
  depth: number;
}

let activePass: ActivePass | null = null;

/**
 * Opens a pass owned by `roots` (a page's, or an island's outside a page), or JOINS the pass already
 * in flight: the outermost owner's roots, invalidator and mode stay in charge, so bridges and
 * components rendering inside a page all build into the page's trees. Pair with `exitPass` in a
 * try/finally.
 */
export function enterPass(
  roots: ElementRoots,
  invalidator: (() => void) | null,
  rebuildAll: boolean,
): void {
  if (activePass) {
    activePass.depth++;
    return;
  }
  roots.beginPass();
  activePass = { roots, invalidator, rebuildAll, depth: 1 };
  setPassInvalidator(invalidator);
}

export function exitPass(shortcutsDeclaredBy?: ShortcutDeclarer): void {
  if (!activePass) return;
  activePass.depth--;
  if (activePass.depth > 0) return;
  activePass.roots.endPass();
  // Spec S8: the pass's Shortcut declarations become the live set — bindings whose subtree was not
  // re-lowered drop out, which is how unmounting unsubscribes. A pass that lowered some components
  // alone replaces only the bindings they declared (#1455).
  commitShortcuts(shortcutsDeclaredBy);
  // §10: the same moment reconciles the modal focus traps against the DOM the pass just produced —
  // a layer that appeared takes focus (recording its invoker), one that vanished gives it back.
  commitFocusTraps();
  installHoverRevealSuppression();
  // A CameraPreview lowered this pass has a fresh <video>; the stream re-attaches here, the same
  // after-the-pass moment the shortcut set commits.
  attachCameraStreams();
  // Scroll views measure their viewport (and adopt an initial offset) once the pass is WRITTEN —
  // a windowed list is (offset, viewport), and neither is knowable before the pass has mounted.
  // Measured here, it was the tree before this one (see scroll-viewports.ts).
  scheduleScrollViewportCommit();
  // A table viewport is measured, arranged and moved there too, and reports what it found.
  scheduleTableViewportCommit();
  // An element cannot be observed before it exists — and at THIS moment it still does not. The
  // pass produced a tree; the render manager writes it once the pass has returned. So the commit
  // waits for the microtask after the write (see scheduleInViewCommit).
  scheduleInViewCommit();
  scheduleCanvasCommit();
  // Republished per pass, and DEFERRED for the same reason the in-view commit above is: the chrome
  // is not in the document until the render manager writes this pass's tree, so measuring here
  // would find nothing on a client-only render. Costs a write only when the number moved.
  scheduleAnchorOffset();
  // A pinned header mounted this pass is set from its surface once the pass is WRITTEN, so one on a
  // page that is already scrolled is scrolled from its first frame (#506).
  scheduleScrolledSync();
  activePass = null;
  setPassInvalidator(null);
}

/** The pass in flight, if any: the lowering builds each root it lowers through the pass's owners. */
export function getActivePass(): ActivePass | null {
  return activePass;
}

/**
 * How a page lowers a vocabulary node its build returned AS ITS ROOT: through `lowerVisualNode`, so
 * a component there is held at the root of an owner like any other, and keeps its state across the
 * render its own `setState` asks for. Registered by the lowering rather than imported from it, for
 * the reason the failure renderer is (`component-boundary.ts`): the lowering reaches the engine,
 * which reaches the component base this module is imported by.
 */
type RootLowering = (node: VisualNodeValue) => HtmlNode;
let rootLowering: RootLowering | null = null;

export function setRootLowering(lowering: RootLowering): void {
  rootLowering = lowering;
}

export function lowerRoot(node: VisualNodeValue): HtmlNode {
  if (!rootLowering) throw new Error('A page lowered a vocabulary node before the lowering loaded.');
  return rootLowering(node);
}

/** One component lowered again: the root the page drew, and the one that takes its place. */
export interface SubtreePatch {
  readonly drawn: HtmlNode;
  readonly next: HtmlNode | null;
}

/** A render answered by what the page's owners built (#1455): what to patch, and which of the
 *  components lowered again declared a shortcut, if one did. */
export interface Relowered {
  readonly patches: readonly SubtreePatch[];
  readonly declaredBy: ShortcutDeclarer;
}

/**
 * How a page answers a render its owners asked for by what they built (`relowerBuilt`), registered by
 * the lowering for the reason `setRootLowering` is. Null: the page lowers whole.
 */
type RootRelowering = (node: VisualNodeValue) => Relowered | null;
let rootRelowering: RootRelowering | null = null;

export function setRootRelowering(relowering: RootRelowering): void {
  rootRelowering = relowering;
}

export function relowerRoot(node: VisualNodeValue): Relowered | null {
  return rootRelowering ? rootRelowering(node) : null;
}
