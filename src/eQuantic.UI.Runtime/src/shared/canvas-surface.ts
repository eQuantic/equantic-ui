/**
 * The client half of the C# `Canvas`, and the reason it exists is a real difference between the
 * targets rather than a convenience.
 *
 * A canvas draws inside the box the LAYOUT gives it, and asks the painter how big that box is —
 * `p.FillCircle(new Point(p.Size.Width / 2, p.Size.Height / 2), …)` is the ordinary shape of a
 * visualization. On Photon
 * the answer is known before the draw: the frame lays out and then paints, every frame. In a
 * browser the box is decided by CSS AFTER the markup exists, so a canvas that FILLS its space has
 * no size to report while its own SVG is being built.
 *
 * So a filling canvas is NOT drawn at lowering at all: it is declared, and drawn the moment the
 * element has been measured — and again after every resize, which is the same redraw Photon
 * performs for free by rebuilding each frame. Drawing it at lowering would mean drawing it at zero,
 * which puts every `p.size.width / 2` in the top-left corner. A canvas with a FIXED size skips all of
 * this: its box was knowable, so SSR draws the final picture and hydration changes nothing.
 *
 * The declaration is collected during the pass, on the state the canvas's POSITION keeps, and
 * committed after the DOM is written, the same shape `in-view` uses and for the same reason: an
 * element cannot be measured before it exists. The observer and the size last drawn at belong to the
 * position and the element it measures, together, and go when the position leaves the tree (#1454).
 */

import { paintCanvas } from './canvas-painter';
import { num } from './css-values';
import type { ICanvasPainter } from './nodes';
import type { Element as Position } from './runtime-exports';
import { nodeStateFor, type NodeState } from './node-states';

interface CanvasDeclaration {
  draw: (painter: ICanvasPainter) => void;
}

/** The element a filling canvas's position measures, and which position that is. */
interface MeasuredCanvas extends SVGElement {
  __eqCanvas?: CanvasState;
}

/**
 * What a filling canvas's position keeps between passes: the latest drawing, the element it measures,
 * its observer, and the size it last drew at. Measuring another element starts all of it again.
 */
export class CanvasState implements NodeState {
  /** The drawing of the latest pass: a new closure over new state every pass. */
  draw: ((painter: ICanvasPainter) => void) | null = null;
  view: MeasuredCanvas | null = null;
  observer: ResizeObserver | undefined = undefined;
  /** The box last drawn at, so a resize that is not a new size redraws nothing. */
  size: string | undefined = undefined;

  /** Measures `view`, letting go of whatever measured it before, this position or another. */
  measure(view: MeasuredCanvas): void {
    const before = view.__eqCanvas;
    if (before && before !== this && before.view === view) before.letGo();
    this.letGo();
    this.view = view;
    view.__eqCanvas = this;
    if (typeof ResizeObserver === 'undefined') return;
    const observer = new ResizeObserver(() => repaint(this));
    observer.observe(view);
    this.observer = observer;
  }

  /** Stops measuring the element. */
  letGo(): void {
    this.observer?.disconnect();
    this.observer = undefined;
    this.size = undefined;
    this.view = null;
  }

  dispose(): void {
    this.letGo();
    this.draw = null;
  }
}

/** The filling canvases the latest passes declared, by their position's id, until the commit reads them. */
const declared = new Map<string, CanvasState>();

/**
 * Declares the filling canvas at `position` as needing measurement, on the state the position keeps,
 * and answers the id the lowering stamps on its element as `data-eq-canvas-fill`. Called once per pass.
 */
export function declareCanvas(position: Position, declaration: CanvasDeclaration): string {
  const state = nodeStateFor(position, CanvasState);
  state.draw = declaration.draw;
  const id = String(position.id);
  declared.set(id, state);
  return id;
}

/** Commits AFTER the pass's DOM has been written — see `in-view` for why a microtask. */
export function scheduleCanvasCommit(): void {
  if (declared.size === 0) return;
  if (typeof queueMicrotask !== 'function') {
    commitCanvasSurfaces();
    return;
  }
  queueMicrotask(commitCanvasSurfaces);
}

/** Draws one canvas at the size it currently occupies, replacing what it drew before. */
function repaint(state: CanvasState): void {
  const element = state.view;
  const draw = state.draw;
  if (!element || !draw) return;
  const box = element.getBoundingClientRect();
  if (box.width <= 0 || box.height <= 0) return;

  // ROUNDED to the same convention the painter writes its coordinates in. A bounding rect carries
  // long, unstable decimals — a scrollbar settling changes the box by 0.0001 — and two costs
  // follow: the cache below never matches, so every observer callback redraws the whole picture,
  // and the viewBox disagrees with the shapes inside it by a sub-pixel that shows as a hairline.
  const width = parseFloat(num(box.width));
  const height = parseFloat(num(box.height));

  // Nothing to do when the box has not changed: a resize observer fires for reasons that are not
  // a new size (a re-observe, a scrollbar settling), and redrawing then would throw away the DOM
  // the reconciler just diffed.
  const size = `${width}x${height}`;
  if (state.size === size) return;
  state.size = size;

  const painter = paintCanvas(width, height);
  draw(painter);
  element.setAttribute('viewBox', `0 0 ${num(width)} ${num(height)}`);
  element.replaceChildren(...painter.elements());
}

/**
 * Attaches what the pass declared. Re-running is cheap and idempotent: an element that already has
 * its observer keeps it and only its callback is refreshed, because a rebuilt tree hands over a new
 * closure over new state every pass.
 */
export function commitCanvasSurfaces(): void {
  if (declared.size === 0) return;
  if (typeof document === 'undefined') {
    declared.clear();
    return;
  }

  for (const element of document.querySelectorAll<SVGElement>('[data-eq-canvas-fill]')) {
    const state = declared.get(element.getAttribute('data-eq-canvas-fill') ?? '');
    if (!state?.draw) continue;

    // The drawing is replaced every pass (new state, new closure); the OBSERVER is not.
    if (state.view !== element) state.measure(element as MeasuredCanvas);
    // The state behind the drawing changed even if the box did not, so the remembered size is
    // cleared: this is the re-render's own repaint, not a resize.
    state.size = undefined;
    repaint(state);
  }

  declared.clear();
}
