/**
 * The after-pass half of the ScrollView's out-channels (the shortcut/camera pattern): lowering
 * DECLARES what each scroll view wants — its initial offset, its viewport callback — on the state its
 * POSITION keeps (`ScrollViewportState`), whose id is stamped on the element as `data-eq-scroll`;
 * `commitScrollViewports()` runs once the pass has been WRITTEN, finds the real elements, applies the
 * offset ONCE (the browser owns the position after adoption — re-applying every pass would fight the
 * user's own scrolling) and each request the app made since once per version, and
 * reports the measured viewport whenever it changed. A windowed list is (offset, viewport) and
 * neither is knowable before layout — this is where layout has happened.
 *
 * What it remembers between passes (that the offset was applied, the request honoured, the viewport
 * reported, the observer) belongs to the position and to the element it adopted, together. It lived
 * on the DOM element, which the reconciler hands to whatever node is lowered at its place: a scroll
 * view that took another's place inherited its offset, and a request of the same version as the one
 * before it was never honoured. Now an element handed from one position to another starts at the new
 * one's offset, and a position that leaves the tree lets go of its observer (docs/ELEMENT-TREE-PLAN.md
 * 3.7, #1454).
 *
 * Two things measured the wrong viewport, or none. The commit ran as the pass ENDED, while its tree
 * was still a value the render manager had yet to write, so it measured the tree before: a scroll
 * view seen for the first time was measured only by the next pass, and a pass that changed a
 * viewport's size reported the old one. And a viewport that changes with no pass at all (the window
 * resized, a splitter dragged, a panel opened beside it) was never measured again: a code editor
 * that fills an IDE's pane kept building the rows it built for the old height, and the rest of the
 * pane stayed blank until something scrolled. So the commit waits for the write, the way the in-view
 * commit does, and each scroll view that reports its viewport is watched by a `ResizeObserver` of
 * its own. Photon measures every frame and needed neither.
 */

import type { Element as Position } from './runtime-exports';
import { nodeStateFor, type NodeState } from './node-states';

export interface ScrollViewportDeclaration {
  horizontal: boolean;
  /** Programmatic position to apply on ADOPTION only. */
  offset?: number;
  /** Where the app asks it to go after that: applied once per version (C# `ScrollRequest`). */
  request?: { readonly version: number; readonly offset: number };
  onViewportChanged?: (extent: number) => void;
}

/** The element a scroll view's position adopted, and which position that was. */
interface AdoptedScrollElement extends HTMLElement {
  __eqScroll?: ScrollViewportState;
}

/**
 * What a scroll view's position keeps between passes: the latest declaration, the element it adopted
 * and what it did there once (the offset, the last request, the last viewport), and the observer
 * watching that element's size. Adopting another element starts all of it again, and so does a
 * position the element is handed to.
 */
export class ScrollViewportState implements NodeState {
  /** What the latest pass declared: a rebuilt tree hands over a new closure over new state every
   * pass, and the observer reads whichever is current when the size changes. */
  declaration: ScrollViewportDeclaration | null = null;
  /** The element this position adopted, or null before the first commit and after it let go. */
  view: AdoptedScrollElement | null = null;
  /** The version of the last request honoured: the same request built again moves nothing. */
  requested: number | undefined = undefined;
  viewport: number | undefined = undefined;
  observer: ResizeObserver | undefined = undefined;

  /** Adopts `view`: a new element is scrolled to the declared offset, and one another position held
   * before is scrolled there even when none is declared, so it never shows where it was left. */
  adopt(view: AdoptedScrollElement, declaration: ScrollViewportDeclaration): void {
    const before = view.__eqScroll;
    const handedOver = before !== undefined && before !== this;
    if (handedOver && before.view === view) before.letGo();
    this.letGo();
    this.view = view;
    view.__eqScroll = this;
    this.requested = undefined;
    this.viewport = undefined;
    const offset = declaration.offset ?? 0;
    if (offset || handedOver) scrollTo(view, declaration.horizontal, offset);
  }

  /** Stops watching the element's size: it left the document, or nothing asks for its viewport. */
  stopWatching(): void {
    this.observer?.disconnect();
    this.observer = undefined;
    watched.delete(this);
  }

  /** Lets go of the element it adopted. */
  letGo(): void {
    this.stopWatching();
    this.view = null;
  }

  dispose(): void {
    this.letGo();
    this.declaration = null;
  }
}

/** The scroll views the latest passes declared, by their position's id, until the commit reads them. */
const declared = new Map<string, ScrollViewportState>();

/**
 * Declares what the scroll view at `position` wants, on the state the position keeps, and answers
 * the id the lowering stamps on its element as `data-eq-scroll`.
 */
export function declareScrollViewport(position: Position, declaration: ScrollViewportDeclaration): string {
  const state = nodeStateFor(position, ScrollViewportState);
  state.declaration = declaration;
  const id = String(position.id);
  declared.set(id, state);
  return id;
}

/**
 * The scroll views watched for their size. One is let go when it leaves the document or stops
 * asking, at once rather than on the next resize, which an element that was simply unmounted never
 * has, and when its position leaves the tree. Never because a pass did not lower it: a page has a
 * root per page and per bridge, each with passes of its own, and one root's pass says nothing about
 * another root's scroll views.
 */
const watched = new Set<ScrollViewportState>();

/**
 * Lets go of every watched scroll view that has left the document. A root unmounts without a pass
 * (`RenderManager.unmount`), so no commit follows to notice, and the observer would hold the element
 * and its component's callback for as long as the page lived.
 */
export function releaseDetachedScrollViewports(): void {
  for (const state of [...watched]) if (!state.view?.isConnected) state.stopWatching();
}

function scrollTo(view: HTMLElement, horizontal: boolean, offset: number): void {
  if (horizontal) view.scrollLeft = offset;
  else view.scrollTop = offset;
}

/** Whether a commit is already waiting for the write. */
let scheduled = false;

/**
 * Commits AFTER the pass's DOM has been written: a microtask is the first moment after the write,
 * and it keeps the commit itself synchronous for tests that drive it directly (see
 * `scheduleInViewCommit`, the same move for the same reason).
 */
export function scheduleScrollViewportCommit(): void {
  if (scheduled || (declared.size === 0 && watched.size === 0)) return;
  scheduled = true;
  // ONE commit for however many passes end before it runs: two roots mounted in one task scheduled
  // two, the first consumed every declaration, and the second read an empty tree and let go of
  // every scroll view on the page.
  const commit = () => {
    scheduled = false;
    commitScrollViewports();
  };
  // After the write on every engine: committing on the spot where there is no queueMicrotask
  // measured the tree before, which is the defect this deferral exists to remove.
  if (typeof queueMicrotask === 'function') queueMicrotask(commit);
  else void Promise.resolve().then(commit);
}

export function commitScrollViewports(): void {
  if (declared.size === 0 && watched.size === 0) return;
  if (typeof document === 'undefined') {
    declared.clear();
    return;
  }
  const mounted = document.querySelectorAll<AdoptedScrollElement>('[data-eq-scroll]');
  for (const view of mounted) {
    const state = declared.get(view.getAttribute('data-eq-scroll') ?? '');
    const declaration = state?.declaration;
    if (!state || !declaration) continue;

    if (state.view !== view) state.adopt(view, declaration);
    // A request the app made since: honoured once per version, after the write, so the content it
    // scrolls over is there. The browser clamps the position to what the content can scroll, and the
    // scroll event it raises reports where the view went, as a reader's scroll does.
    const request = declaration.request;
    if (request && request.version !== state.requested) {
      state.requested = request.version;
      scrollTo(view, declaration.horizontal, request.offset);
    }

    reportViewport(state);
    // Watched from here on, once: its size can change with no pass to measure it. The observer
    // is the position's, and watches the element it adopted, so it goes when either does. Its first
    // answer, as it starts watching, is the size just reported, and stays silent. A scroll view that
    // keeps its offset and stops asking for its viewport stops being watched, rather than being told
    // of every resize for nobody.
    if (!declaration.onViewportChanged) {
      state.stopWatching();
      continue;
    }
    if (typeof ResizeObserver !== 'function') continue;
    if (!state.observer) {
      const observer = new ResizeObserver(() => reportViewport(state));
      observer.observe(view);
      state.observer = observer;
      watched.add(state);
    }
  }
  for (const state of [...watched]) {
    const view = state.view;
    if (!view?.isConnected || view.getAttribute('data-eq-scroll') === null) state.stopWatching();
  }
  declared.clear();
}

/** Reports the viewport's extent on its axis when it changed since it was last reported. */
function reportViewport(state: ScrollViewportState): void {
  // A scroll view that stopped asking lost its marker with its declaration: its old callback is
  // not called for a size nobody wants any more.
  const view = state.view;
  if (!view?.isConnected || !view.hasAttribute('data-eq-scroll')) {
    state.stopWatching();
    return;
  }
  const declaration = state.declaration;
  const extent = declaration?.horizontal ? view.clientWidth : view.clientHeight;
  if (extent > 0 && extent !== state.viewport) {
    state.viewport = extent;
    // A client extent is an integer, exact as a single, but the seam rounds like every other
    // float-typed one rather than lean on that fact about the DOM.
    declaration?.onViewportChanged?.(Math.fround(extent));
  }
}
