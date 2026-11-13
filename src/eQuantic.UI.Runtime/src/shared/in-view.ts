/**
 * The client half of the C# `InView`: tells a component when its child is on screen.
 *
 * An `IntersectionObserver`, not a scroll listener. A scroll listener runs on every frame the page
 * moves and then has to measure to decide whether anything changed; the observer runs only when
 * something actually crosses, and does the measuring off the main thread. For a table of contents
 * watching thirty headings that is the difference between a page that scrolls and one that stutters.
 *
 * The declaration is collected during the pass, on the state the InView's POSITION keeps, and
 * committed after it mounts — the same shape the scroll viewports use, because an element cannot be
 * observed before it exists.
 *
 * The observer and the last visibility it reported belong to the position and to the element it
 * watches, together. They lived on the DOM element, which the reconciler hands to whatever node is
 * lowered at its place, so an InView that took another's place inherited its observer and its last
 * report, and was never told it was on screen if the one before it already had been (#1454).
 */

import type { Element as Position } from './runtime-exports';
import { DISPOSABLE, nodeStateFor, type NodeState } from './node-states';

interface InViewDeclaration {
  threshold: number;
  onChanged: (visible: boolean) => void;
}

/** The element an InView's position watches, and which position that is. */
interface ObservedElement extends HTMLElement {
  __eqInView?: InViewState;
}

/**
 * What an InView's position keeps between passes: the latest declaration, the element it watches,
 * the observer, and the visibility last reported. Watching another element starts all of it again.
 */
export class InViewState implements NodeState {
  /** Disposed when its position leaves the tree (`node-states.ts`). */
  static readonly $interfaces = DISPOSABLE;

  /** What the latest pass declared: the callback is a new closure over new state every pass. */
  declaration: InViewDeclaration | null = null;
  view: ObservedElement | null = null;
  observer: IntersectionObserver | undefined = undefined;
  visible: boolean | undefined = undefined;

  /** Watches `view`, letting go of whatever watched it before, this position or another. */
  watch(view: ObservedElement, threshold: number): void {
    const before = view.__eqInView;
    if (before && before !== this && before.view === view) before.letGo();
    this.letGo();
    this.view = view;
    view.__eqInView = this;
    const observer = new IntersectionObserver(
      (entries) => {
        for (const entry of entries) {
          const visible = entry.isIntersecting;
          // TRANSITIONS only — the observer reports on every crossing, and a caller that wanted a
          // stream would have written one.
          if (this.visible === visible) continue;
          this.visible = visible;
          this.declaration?.onChanged(visible);
        }
      },
      { threshold },
    );
    observer.observe(view);
    this.observer = observer;
  }

  /** Stops watching the element. */
  letGo(): void {
    this.observer?.disconnect();
    this.observer = undefined;
    this.visible = undefined;
    this.view = null;
  }

  dispose(): void {
    this.letGo();
    this.declaration = null;
  }
}

/** The InViews the latest passes declared, by their position's id, until the commit reads them. */
const declared = new Map<string, InViewState>();

/**
 * Declares the InView at `position` as observed, on the state the position keeps, and answers the id
 * the lowering stamps on the observed element as `data-eq-inview`. Called once per pass.
 */
export function declareInView(position: Position, declaration: InViewDeclaration): string {
  const state = nodeStateFor(position, InViewState);
  state.declaration = declaration;
  const id = String(position.id);
  declared.set(id, state);
  return id;
}

/**
 * Commits AFTER the pass's DOM has been written.
 *
 * The pass ends while the tree it produced is still a value — the render manager writes it once
 * `render()` has returned. Committing inside the pass therefore looked for elements that did not
 * exist yet, found none, and cleared the declarations: a freshly hydrated page carried every
 * `data-eq-inview` node with no observer on it, and only a LATER re-render (opening a menu,
 * anything) attached them. On a page where nothing else ever changes, that is never.
 *
 * A microtask is the first moment after the write, and it keeps the commit itself synchronous for
 * tests that drive it directly.
 */
export function scheduleInViewCommit(): void {
  if (declared.size === 0) return;
  // After the write on every engine: committing on the spot where there is no queueMicrotask
  // looked for the elements before they existed, which is the defect this deferral removes.
  if (typeof queueMicrotask === 'function') queueMicrotask(commitInViewObservers);
  else void Promise.resolve().then(commitInViewObservers);
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
    const state = declared.get(element.getAttribute('data-eq-inview') ?? '');
    const declaration = state?.declaration;
    if (!state || !declaration) continue;

    // The callback is replaced every pass (the state reads the latest); the OBSERVER is not.
    // Re-observing would fire an initial callback on every render, which for a table of contents
    // means the highlight jumping back to whatever is on screen each time any state changes.
    if (state.view !== element || !state.observer) state.watch(element, declaration.threshold);
  }

  declared.clear();
}

/** Drops every observer — a surface closing, or a test starting from a clean sheet. */
export function resetInViewForTests(): void {
  declared.clear();
  if (typeof document === 'undefined') return;
  for (const element of document.querySelectorAll<ObservedElement>('[data-eq-inview]')) {
    element.__eqInView?.letGo();
    element.__eqInView = undefined;
  }
}
