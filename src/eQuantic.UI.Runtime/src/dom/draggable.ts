/**
 * The web half of the C# `Draggable` — a continuous gesture along one axis.
 *
 * ONE document-level pointerdown delegate, installed lazily by the lowering and idempotent, exactly
 * like the drag-dismiss controller it sits beside. The gesture's RULES travel with the element as
 * data attributes, so the controller stays a mechanism and never learns what any particular gesture
 * means: it moves the element between the limits and reports where the finger left it.
 *
 * The travel that counts is the one along the gesture's OWN axis. A sideways swipe must not arm on
 * a vertical scroll and a sheet must not arm on a sideways one — without that, every list with a
 * swipeable row becomes impossible to scroll.
 *
 * The surface moves by the individual `translate` property, the one the lowering writes the rest
 * offset to, so the box keeps its own `transform` and its hover's for the whole gesture (#511). The
 * inline value is the gesture's only while it lasts: a release or a cancel takes it away again and
 * the markup's rest, with the markup's glide, decides where the surface ends.
 *
 * `translate` resolves in the surface's parent's space, so that is where the finger is read
 * (`gestureSpace`): along the track as it is drawn under a turned ancestor, and in the surface's own
 * pixels under a scaled one, as Photon reads it (#1195, #1204).
 *
 * A horizontal surface is measured toward the END of the direction where it sits (#1569): at pointerdown
 * it reads whether its surface reads right to left, and then the finger's travel and the painted
 * `translate` are both multiplied by -1, after `gestureSpace` has settled the screen. Min, Max, the
 * rest and the offset it reports are all in that logical space, so a component that wrote them for
 * "toward the end" works in both directions with no test of its own.
 */
import { gestureSpace } from './gesture-space';
import { followPointer, pointerDownDelegate, SLOP } from './pointer-gesture';
import { readsRightToLeft } from './reading-direction';

const delegate = pointerDownDelegate(onPointerDown);

export function installDraggableController(): void {
  delegate.install();
}

export function resetDraggableController(): void {
  delegate.reset();
}

/**
 * The surface's untransformed extent along the axis: its layout box, or, for an SVG element, which
 * has no layout box of its own, the extent of what it draws in its own units. Neither holds a
 * transform, the surface's own or an ancestor's.
 */
function ownExtent(surface: Element, horizontal: boolean): number {
  if (surface instanceof HTMLElement) return horizontal ? surface.offsetWidth : surface.offsetHeight;
  const graphics = surface as Partial<SVGGraphicsElement>;
  if (typeof graphics.getBBox !== 'function') return 0;
  const box = graphics.getBBox();
  return horizontal ? box.width : box.height;
}

function onPointerDown(down: Event): void {
  const target = down.target as Element | null;
  const surface = target?.closest?.('[data-eq-drag]') as HTMLElement | null;
  if (!surface) return;

  const horizontal = surface.getAttribute('data-eq-drag') === 'x';
  const min = parseFloat(surface.getAttribute('data-eq-drag-min') || '0');
  const max = parseFloat(surface.getAttribute('data-eq-drag-max') || '0');
  const rest = parseFloat(surface.getAttribute('data-eq-drag-rest') || '0');
  const normalized = surface.getAttribute('data-eq-drag-normalized') === '1';
  const follows = surface.getAttribute('data-eq-drag-follows') !== '0';
  const moves = surface.getAttribute('data-eq-drag-moves') === '1';

  // What a NORMALIZED gesture divides by: the surface's own extent, which only the client can
  // measure — a slider's track is fluid, so the component cannot know its width and the DOM can. It
  // is the untransformed box, the one a `translate` percentage resolves against, which is how the
  // lowering writes a normalized rest, so a fraction is read and written back in the same pixels.
  // It used to be the rect, which holds every transform the surface is drawn under: divided by a
  // rect at its own `scale(.5)`, a 200px surface read its rest of 0.25 as 25px, not the 50px it sits
  // at, and jumped on the first move (#702), and under a quarter turn the rect's width is the
  // track's thickness (#1204).
  const extent = ownExtent(surface, horizontal);

  const space = gestureSpace(surface);
  const pointer = down as PointerEvent;
  const startX = pointer.clientX;
  const startY = pointer.clientY;
  const slop = space.onScreen(horizontal, SLOP);

  // Which way "toward the end" points on the screen, read where the gesture starts and held to its
  // release, as the space is. A vertical surface has no end to the side: it is always 1.
  const sign = horizontal && readsRightToLeft(surface) ? -1 : 1;

  // How far the finger has carried the surface along its own axis, in the space its offset is in.
  const travelled = (ev: PointerEvent): number =>
    sign * space.along(horizontal, ev.clientX - startX, ev.clientY - startY);

  // Where the gesture now IS, measured from the rest it started at.
  const travelOf = (ev: PointerEvent): number => {
    const raw = travelled(ev);
    const at = rest + (normalized && extent > 0 ? raw / extent : raw);
    return Math.min(max, Math.max(min, at));
  };

  followPointer(pointer, {
    arms: (ev) => Math.abs(travelled(ev)) > slop,
    armed: () => {
      if (follows) surface.style.transition = 'none';
    },
    move: (ev) => {
      const offset = travelOf(ev);
      if (follows) {
        const px = normalized ? offset * extent : offset;
        // Through setProperty: the property is newer than the CSSOM's named accessors, and the
        // generic one writes it on every engine. The offset is toward the end, the paint is physical.
        surface.style.setProperty('translate', horizontal ? `${sign * px}px` : `0 ${px}px`);
      }
      if (moves) surface.dispatchEvent(new CustomEvent('eq-drag-moved', { detail: offset }));
    },
    // Report, then let the RE-RENDER place it: the caller's new RestOffset is the truth about where
    // this belongs, and gliding to a guess here would fight the frame that follows. The gesture's
    // own offset is handed back AFTER the report, so a re-render the report runs has already written
    // the new rest by then, and the surface glides from the finger to it in one move. A caller that
    // leaves the rest where it was (a swipe short of opening) changes no markup at all, and the
    // surface still goes home: it used to stay where the finger left it, since nothing re-wrote the
    // inline offset the drag had set.
    release: (ev) => {
      surface.dispatchEvent(new CustomEvent('eq-drag-released', { detail: travelOf(ev) }));
      if (follows) handBack(surface);
    },
    // The system can take a gesture away without ever lifting the finger — a browser claiming the
    // scroll, a call arriving. Nothing was decided, so nothing is reported: the surface returns to
    // where the caller last put it, which is the rest its markup carries.
    cancel: () => {
      if (follows) handBack(surface);
    },
  });
}

/**
 * Gives the surface back to its markup: the drag's inline offset and its `transition: none` go, so
 * the rest the lowering wrote applies again, under the glide it declared beside it.
 */
function handBack(surface: HTMLElement): void {
  surface.style.removeProperty('translate');
  surface.style.removeProperty('transition');
}
