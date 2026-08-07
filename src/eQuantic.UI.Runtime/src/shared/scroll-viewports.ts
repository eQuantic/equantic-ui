/**
 * The after-pass half of the ScrollView's out-channels (the shortcut/camera pattern): lowering
 * DECLARES what each scroll view wants — its initial offset, its viewport callback — keyed by the
 * stable path stamped on the element as `data-eq-scroll`; `commitScrollViewports()` runs after the
 * pass mounts, finds the real elements, applies the offset ONCE (the browser owns the position
 * after adoption — re-applying every pass would fight the user's own scrolling), and reports the
 * measured viewport whenever it changed. A windowed list is (offset, viewport) and neither is
 * knowable before layout — this is where layout has happened.
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
}

export function declareScrollViewport(path: string, declaration: ScrollViewportDeclaration): void {
  declared.set(path, declaration);
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

    const extent = declaration.horizontal ? view.clientWidth : view.clientHeight;
    if (extent > 0 && extent !== view.__eqViewport) {
      view.__eqViewport = extent;
      declaration.onViewportChanged?.(extent);
    }
  }
  declared.clear();
}
