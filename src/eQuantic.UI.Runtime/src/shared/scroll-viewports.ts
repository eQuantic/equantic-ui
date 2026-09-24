/**
 * The after-pass half of the ScrollView's out-channels (the shortcut/camera pattern): lowering
 * DECLARES what each scroll view wants — its initial offset, its viewport callback — keyed by the
 * stable path stamped on the element as `data-eq-scroll`; `commitScrollViewports()` runs once the
 * pass has been WRITTEN, finds the real elements, applies the offset ONCE (the browser owns the
 * position after adoption — re-applying every pass would fight the user's own scrolling), and
 * reports the measured viewport whenever it changed. A windowed list is (offset, viewport) and
 * neither is knowable before layout — this is where layout has happened.
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

export interface ScrollViewportDeclaration {
  horizontal: boolean;
  /** Programmatic position to apply on ADOPTION only. */
  offset?: number;
  onViewportChanged?: (extent: number) => void;
}

const declared = new Map<string, ScrollViewportDeclaration>();

/** Marker properties live on the ELEMENT — the reconciler reuses it, so adoption survives passes. */
interface AdoptedScrollElement extends HTMLElement {
  __eqScrollAdopted?: boolean;
  __eqViewport?: number;
  /** The callback and the axis of the latest pass: a rebuilt tree hands over a new closure over new
   * state every pass, and the observer below reads whichever is current when the size changes. */
  __eqViewportChanged?: (extent: number) => void;
  __eqHorizontal?: boolean;
  __eqResizeObserver?: ResizeObserver;
}

export function declareScrollViewport(path: string, declaration: ScrollViewportDeclaration): void {
  declared.set(path, declaration);
}

/**
 * Commits AFTER the pass's DOM has been written: a microtask is the first moment after the write,
 * and it keeps the commit itself synchronous for tests that drive it directly (see
 * `scheduleInViewCommit`, the same move for the same reason).
 */
export function scheduleScrollViewportCommit(): void {
  if (declared.size === 0) return;
  if (typeof queueMicrotask !== 'function') {
    commitScrollViewports();
    return;
  }
  queueMicrotask(commitScrollViewports);
}

export function commitScrollViewports(): void {
  if (declared.size === 0) return;
  if (typeof document === 'undefined') {
    declared.clear();
    return;
  }
  const mounted = document.querySelectorAll<AdoptedScrollElement>('[data-eq-scroll]');
  for (const view of mounted) {
    const declaration = declared.get(view.getAttribute('data-eq-scroll') ?? '');
    if (!declaration) continue;

    if (!view.__eqScrollAdopted) {
      view.__eqScrollAdopted = true;
      if (declaration.offset) {
        if (declaration.horizontal) view.scrollLeft = declaration.offset;
        else view.scrollTop = declaration.offset;
      }
    }

    view.__eqHorizontal = declaration.horizontal;
    view.__eqViewportChanged = declaration.onViewportChanged;
    reportViewport(view);
    // Watched from here on, once: its size can change with no pass to measure it. The observer
    // is the element's own, so it goes when the element does. Its first answer, as it starts
    // watching, is the size just reported, and stays silent.
    if (declaration.onViewportChanged && !view.__eqResizeObserver && typeof ResizeObserver === 'function') {
      const observer = new ResizeObserver(() => reportViewport(view));
      observer.observe(view);
      view.__eqResizeObserver = observer;
    }
  }
  declared.clear();
}

/** Reports the viewport's extent on its axis when it changed since it was last reported. */
function reportViewport(view: AdoptedScrollElement): void {
  // A scroll view that stopped asking lost its marker with its declaration: its old callback is
  // not called for a size nobody wants any more.
  if (!view.hasAttribute('data-eq-scroll')) {
    view.__eqResizeObserver?.disconnect();
    view.__eqResizeObserver = undefined;
    return;
  }
  const extent = view.__eqHorizontal ? view.clientWidth : view.clientHeight;
  if (extent > 0 && extent !== view.__eqViewport) {
    view.__eqViewport = extent;
    // A client extent is an integer, exact as a single, but the seam rounds like every other
    // float-typed one rather than lean on that fact about the DOM.
    view.__eqViewportChanged?.(Math.fround(extent));
  }
}
