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

  const start = horizontal ? (down as PointerEvent).clientX : (down as PointerEvent).clientY;
  let active = false;

  const travelOf = (ev: Event): number => {
    const now = horizontal ? (ev as PointerEvent).clientX : (ev as PointerEvent).clientY;
    return Math.min(max, Math.max(min, rest + (now - start)));
  };

  const move = (ev: Event): void => {
    const raw = (horizontal ? (ev as PointerEvent).clientX : (ev as PointerEvent).clientY) - start;
    if (!active && Math.abs(raw) > SLOP) {
      active = true;
      surface.style.transition = 'none';
    }
    if (!active) return;

    // The browser's own scroll must not fight the gesture once it has armed.
    ev.preventDefault();
    const offset = travelOf(ev);
    surface.style.transform = horizontal ? `translateX(${offset}px)` : `translateY(${offset}px)`;
  };

  const up = (ev: Event): void => {
    document.removeEventListener('pointermove', move);
    document.removeEventListener('pointerup', up);
    if (!active) return;

    // An activated drag swallows the click the browser fires after pointerup — a row's own buttons
    // must not receive a tap that was really a swipe.
    document.addEventListener('click', squashClick, { capture: true, once: true });
    setTimeout(() => document.removeEventListener('click', squashClick, { capture: true }), 50);

    // Report, then let the RE-RENDER place it: the caller's new RestOffset is the truth about where
    // this belongs, and gliding to a guess here would fight the frame that follows.
    surface.style.transition = `transform ${GLIDE_MS}ms`;
    surface.dispatchEvent(new CustomEvent('eq-drag-released', { detail: travelOf(ev) }));
  };

  document.addEventListener('pointermove', move, { passive: false });
  document.addEventListener('pointerup', up);
}

function squashClick(e: Event): void {
  e.stopPropagation();
  e.preventDefault();
}
