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
 */
const SLOP = 12; // Touch.PressCancelSlop — cross-pinned with the C# host

let installed = false;

export function installDraggableController(): void {
  if (installed || typeof document === 'undefined') return;
  installed = true;
  document.addEventListener('pointerdown', onPointerDown);
}

export function resetDraggableController(): void {
  if (typeof document === 'undefined') return;
  installed = false;
  document.removeEventListener('pointerdown', onPointerDown);
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
  // measure — a slider's track is fluid, so the component cannot know its width and the DOM can.
  const box = surface.getBoundingClientRect();
  const extent = horizontal ? box.width : box.height;

  const start = horizontal ? (down as PointerEvent).clientX : (down as PointerEvent).clientY;
  let active = false;

  // Where the gesture now IS, measured from the rest it started at.
  const travelOf = (ev: Event): number => {
    const now = horizontal ? (ev as PointerEvent).clientX : (ev as PointerEvent).clientY;
    const raw = now - start;
    const at = rest + (normalized && extent > 0 ? raw / extent : raw);
    return Math.min(max, Math.max(min, at));
  };

  const move = (ev: Event): void => {
    const raw = (horizontal ? (ev as PointerEvent).clientX : (ev as PointerEvent).clientY) - start;
    if (!active && Math.abs(raw) > SLOP) {
      active = true;
      if (follows) surface.style.transition = 'none';
    }
    if (!active) return;

    // The browser's own scroll must not fight the gesture once it has armed.
    ev.preventDefault();
    const offset = travelOf(ev);
    if (follows) {
      const px = normalized ? offset * extent : offset;
      // Through setProperty: the property is newer than the CSSOM's named accessors, and the
      // generic one writes it on every engine.
      surface.style.setProperty('translate', horizontal ? `${px}px` : `0 ${px}px`);
    }
    if (moves) surface.dispatchEvent(new CustomEvent('eq-drag-moved', { detail: offset }));
  };

  const detach = (): void => {
    document.removeEventListener('pointermove', move);
    document.removeEventListener('pointerup', up);
    document.removeEventListener('pointercancel', cancel);
  };

  // The system can take a gesture away without ever lifting the finger — a browser claiming the
  // scroll, a call arriving. Nothing was decided, so nothing is reported: the surface returns to
  // where the caller last put it, which is the rest its markup carries.
  const cancel = (): void => {
    detach();
    if (!active) return;
    active = false;
    if (follows) handBack(surface);
  };

  const up = (ev: Event): void => {
    detach();
    if (!active) return;

    // An activated drag swallows the click the browser fires after pointerup — a row's own buttons
    // must not receive a tap that was really a swipe.
    //
    // The timer holds the DOCUMENT it registered on rather than reading the global when it fires,
    // and the difference is the fifty milliseconds between the two. A page cannot lose `document`
    // in that window, but a torn-down environment can: the suite's own teardown removed the global
    // while this was pending, the callback threw `document is not defined` where nothing could
    // catch it, and vitest failed a run in which all 134 files had passed. Deferred cleanup carries
    // what it cleans up.
    const host = document;
    host.addEventListener('click', squashClick, { capture: true, once: true });
    setTimeout(() => host.removeEventListener('click', squashClick, { capture: true }), 50);

    // Report, then let the RE-RENDER place it: the caller's new RestOffset is the truth about where
    // this belongs, and gliding to a guess here would fight the frame that follows. The gesture's
    // own offset is handed back AFTER the report, so a re-render the report runs has already written
    // the new rest by then, and the surface glides from the finger to it in one move. A caller that
    // leaves the rest where it was (a swipe short of opening) changes no markup at all, and the
    // surface still goes home: it used to stay where the finger left it, since nothing re-wrote the
    // inline offset the drag had set.
    surface.dispatchEvent(new CustomEvent('eq-drag-released', { detail: travelOf(ev) }));
    if (follows) handBack(surface);
  };

  document.addEventListener('pointermove', move, { passive: false });
  document.addEventListener('pointerup', up);
  document.addEventListener('pointercancel', cancel);
}

/**
 * Gives the surface back to its markup: the drag's inline offset and its `transition: none` go, so
 * the rest the lowering wrote applies again, under the glide it declared beside it.
 */
function handBack(surface: HTMLElement): void {
  surface.style.removeProperty('translate');
  surface.style.removeProperty('transition');
}

function squashClick(e: Event): void {
  e.stopPropagation();
  e.preventDefault();
}
