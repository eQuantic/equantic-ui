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
 */
const SLOP = 12; // Touch.PressCancelSlop — cross-pinned with the C# host
const GLIDE_MS = 200; // Motion.BaseMs

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
      surface.style.transform = horizontal ? `translateX(${px}px)` : `translateY(${px}px)`;
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
  // where the caller last put it.
  const cancel = (): void => {
    detach();
    if (!active) return;
    active = false;
    if (!follows) return;
    surface.style.transition = `transform ${GLIDE_MS}ms`;
    const px = normalized ? rest * extent : rest;
    surface.style.transform = horizontal ? `translateX(${px}px)` : `translateY(${px}px)`;
  };

  const up = (ev: Event): void => {
    detach();
    if (!active) return;

    // An activated drag swallows the click the browser fires after pointerup — a row's own buttons
    // must not receive a tap that was really a swipe.
    document.addEventListener('click', squashClick, { capture: true, once: true });
    setTimeout(() => document.removeEventListener('click', squashClick, { capture: true }), 50);

    // Report, then let the RE-RENDER place it: the caller's new RestOffset is the truth about where
    // this belongs, and gliding to a guess here would fight the frame that follows.
    if (follows) surface.style.transition = `transform ${GLIDE_MS}ms`;
    surface.dispatchEvent(new CustomEvent('eq-drag-released', { detail: travelOf(ev) }));
  };

  document.addEventListener('pointermove', move, { passive: false });
  document.addEventListener('pointerup', up);
  document.addEventListener('pointercancel', cancel);
}

function squashClick(e: Event): void {
  e.stopPropagation();
  e.preventDefault();
}
