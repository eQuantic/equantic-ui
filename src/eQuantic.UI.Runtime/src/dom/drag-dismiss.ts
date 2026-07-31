/**
 * Gestures v2 — the WEB drag-to-dismiss controller behind `DragDismiss` (the sheet contract).
 * ONE document-level pointerdown delegate (installed lazily by the lowering, idempotent): a press
 * inside a `[data-eq-drag-dismiss]` surface arms a drag that ACTIVATES past the slop (taps inside
 * keep working — an activated drag also squashes the trailing click so inner buttons never fire),
 * follows the pointer with a paint-only translate (downward, clamped at 0), and on release either
 * dispatches `eq-drag-dismiss` (a custom event the reconciler-attached handler resolves into the
 * component's onDismiss — state then removes the subtree and the presence EXIT completes from the
 * dragged position, the CSS animation's implicit `from` being the current transform) or glides the
 * surface back over Motion.Base. Exiting copies (`[data-eq-exiting]`) never re-arm.
 * v1 fences: detents, flick-velocity dismissal, horizontal axis, nested-scroll interplay.
 */

/** Touch.PressCancelSlop — the same 12dp that cancels a native press into a drag. */
const SLOP = 12;

/** Motion.Base glide-back + clearance for the transition to land before styles reset. */
const GLIDE_MS = 200;

let installed = false;

/** Idempotent: the first `DragDismiss` lowering installs the delegate for the whole document. */
export function installDragDismissController(): void {
  if (installed || typeof document === 'undefined') return;
  installed = true;
  document.addEventListener('pointerdown', onPointerDown);
}

/** Test seam: uninstalls and forgets, so specs exercise a fresh install. */
export function resetDragDismissController(): void {
  if (!installed) return;
  installed = false;
  document.removeEventListener('pointerdown', onPointerDown);
}

function onPointerDown(down: Event): void {
  const target = down.target as Element | null;
  const sheet = target?.closest?.('[data-eq-drag-dismiss]') as HTMLElement | null;
  if (!sheet || sheet.closest('[data-eq-exiting]')) return;

  const threshold = parseFloat(sheet.getAttribute('data-eq-drag-dismiss') || '96');
  const startY = (down as PointerEvent).clientY;
  let active = false;

  const move = (ev: Event): void => {
    const dy = (ev as PointerEvent).clientY - startY;
    if (!active && dy > SLOP) {
      active = true;
      sheet.style.transition = 'none';
    }
    if (active) {
      sheet.style.transform = `translateY(${Math.max(0, dy)}px)`;
    }
  };

  const up = (ev: Event): void => {
    document.removeEventListener('pointermove', move);
    document.removeEventListener('pointerup', up);
    if (!active) return;

    // An activated drag swallows the click the browser fires after pointerup — inner buttons
    // (the sheet's own actions) must not receive a tap that was really a drag.
    document.addEventListener('click', squashClick, { capture: true, once: true });
    setTimeout(() => document.removeEventListener('click', squashClick, { capture: true }), 50);

    const dy = (ev as PointerEvent).clientY - startY;
    if (dy >= threshold) {
      sheet.dispatchEvent(new CustomEvent('eq-drag-dismiss'));
      return;
    }

    // Short of the threshold: glide back over Motion.Base, then clear the inline styles.
    sheet.style.transition = `transform ${GLIDE_MS}ms ease-out`;
    sheet.style.transform = '';
    setTimeout(() => {
      sheet.style.transition = '';
    }, GLIDE_MS + 50);
  };

  document.addEventListener('pointermove', move);
  document.addEventListener('pointerup', up);
}

function squashClick(e: Event): void {
  e.stopPropagation();
  e.preventDefault();
}
