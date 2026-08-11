/**
 * The client half of the C# `InView`: tells a component when its child is on screen.
 *
 * An `IntersectionObserver`, not a scroll listener. A scroll listener runs on every frame the page
 * moves and then has to measure to decide whether anything changed; the observer runs only when
 * something actually crosses, and does the measuring off the main thread. For a table of contents
 * watching thirty headings that is the difference between a page that scrolls and one that stutters.
 *
 * The declaration is collected during the pass and committed after it mounts — the same shape the
 * scroll viewports use, because an element cannot be observed before it exists.
 */

interface InViewDeclaration {
  threshold: number;
  onChanged: (visible: boolean) => void;
}

const declared = new Map<string, InViewDeclaration>();

/** Marker properties live on the ELEMENT: the reconciler reuses it across passes, so the observer
 * attached last pass is still the right one and must not be attached twice. */
interface ObservedElement extends HTMLElement {
  __eqInViewObserver?: IntersectionObserver;
  __eqInViewVisible?: boolean;
  __eqInViewChanged?: (visible: boolean) => void;
}

/** Declares the element at `path` as observed. Called by the lowering, once per pass. */
export function declareInView(path: string, declaration: InViewDeclaration): void {
  declared.set(path, declaration);
}

/**
 * Attaches what the pass declared. Re-running is cheap and idempotent: an element that already has
 * its observer keeps it and only its callback is refreshed, because a rebuilt tree hands over a new
 * closure over new state every pass.
 */
export function commitInViewObservers(): void {
  if (declared.size === 0) return;
  if (typeof document === 'undefined' || typeof IntersectionObserver === 'undefined') {
    declared.clear();
    return;
  }

  for (const element of document.querySelectorAll<ObservedElement>('[data-eq-inview]')) {
    const declaration = declared.get(element.getAttribute('data-eq-inview') ?? '');
    if (!declaration) continue;

    // The callback is replaced every pass; the OBSERVER is not. Re-observing would fire an initial
    // callback on every render, which for a table of contents means the highlight jumping back to
    // whatever is on screen each time any state changes.
    element.__eqInViewChanged = declaration.onChanged;
    if (element.__eqInViewObserver) continue;

    const observer = new IntersectionObserver(
      (entries) => {
        for (const entry of entries) {
          const target = entry.target as ObservedElement;
          const visible = entry.isIntersecting;
          // TRANSITIONS only — the observer reports on every crossing, and a caller that wanted a
          // stream would have written one.
          if (target.__eqInViewVisible === visible) continue;
          target.__eqInViewVisible = visible;
          target.__eqInViewChanged?.(visible);
        }
      },
      { threshold: declaration.threshold },
    );
    observer.observe(element);
    element.__eqInViewObserver = observer;
  }

  declared.clear();
}

/** Drops every observer — a surface closing, or a test starting from a clean sheet. */
export function resetInViewForTests(): void {
  declared.clear();
  if (typeof document === 'undefined') return;
  for (const element of document.querySelectorAll<ObservedElement>('[data-eq-inview]')) {
    element.__eqInViewObserver?.disconnect();
    element.__eqInViewObserver = undefined;
    element.__eqInViewVisible = undefined;
  }
}
