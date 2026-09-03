/**
 * The client half of the C# `Canvas`, and the reason it exists is a real difference between the
 * targets rather than a convenience.
 *
 * A canvas draws inside the box the LAYOUT gives it, and asks the painter how big that box is —
 * `p.FillCircle(p.Width / 2, p.Height / 2, …)` is the ordinary shape of a visualization. On Photon
 * the answer is known before the draw: the frame lays out and then paints, every frame. In a
 * browser the box is decided by CSS AFTER the markup exists, so a canvas that FILLS its space has
 * no size to report while its own SVG is being built.
 *
 * So a filling canvas is NOT drawn at lowering at all: it is declared, and drawn the moment the
 * element has been measured — and again after every resize, which is the same redraw Photon
 * performs for free by rebuilding each frame. Drawing it at lowering would mean drawing it at zero,
 * which puts every `p.width / 2` in the top-left corner. A canvas with a FIXED size skips all of
 * this: its box was knowable, so SSR draws the final picture and hydration changes nothing.
 *
 * The declaration is collected during the pass and committed after the DOM is written, the same
 * shape `in-view` uses and for the same reason: an element cannot be measured before it exists.
 */

import { paintCanvas } from './canvas-painter';
import { num } from './css-values';
import type { ICanvasPainter } from './nodes';

interface CanvasDeclaration {
  draw: (painter: ICanvasPainter) => void;
}

const declared = new Map<string, CanvasDeclaration>();

/** Marker properties live on the ELEMENT: the reconciler reuses it across passes, so the observer
 * attached last pass is still the right one and must not be attached twice. */
interface MeasuredCanvas extends SVGElement {
  __eqCanvasObserver?: ResizeObserver;
  __eqCanvasDraw?: (painter: ICanvasPainter) => void;
  __eqCanvasSize?: string;
}

/** Declares the filling canvas at `path` as needing measurement. Called by the lowering, once per pass. */
export function declareCanvas(path: string, declaration: CanvasDeclaration): void {
  declared.set(path, declaration);
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
function repaint(element: MeasuredCanvas): void {
  const draw = element.__eqCanvasDraw;
  if (!draw) return;
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
  if (element.__eqCanvasSize === size) return;
  element.__eqCanvasSize = size;

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
    const measured = element as MeasuredCanvas;
    const declaration = declared.get(element.getAttribute('data-eq-canvas-fill') ?? '');
    if (!declaration) continue;

    // The callback is replaced every pass (new state, new closure); the OBSERVER is not.
    measured.__eqCanvasDraw = declaration.draw;
    // The state behind the drawing changed even if the box did not, so the remembered size is
    // cleared: this is the re-render's own repaint, not a resize.
    measured.__eqCanvasSize = undefined;
    repaint(measured);

    if (measured.__eqCanvasObserver || typeof ResizeObserver === 'undefined') continue;
    const observer = new ResizeObserver(() => repaint(measured));
    observer.observe(element);
    measured.__eqCanvasObserver = observer;
  }

  declared.clear();
}
