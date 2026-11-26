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
 *
 * The finger is read in the space the sheet's `translateY` moves it in, its parent's
 * (`gestureSpace`), so the threshold is counted in the sheet's own pixels and the sheet stays under
 * the finger whatever it is drawn under, as Photon counts it since #734 (#1492).
 */
import { gestureSpace } from './gesture-space';
import { followPointer, pointerDownDelegate, SLOP } from './pointer-gesture';

/** Motion.Base glide-back + clearance for the transition to land before styles reset. */
const GLIDE_MS = 200;

const delegate = pointerDownDelegate(onPointerDown);

/** Idempotent: the first `DragDismiss` lowering installs the delegate for the whole document. */
export function installDragDismissController(): void {
  delegate.install();
}

/** Test seam: uninstalls and forgets, so specs exercise a fresh install. */
export function resetDragDismissController(): void {
  delegate.reset();
}

function onPointerDown(down: PointerEvent): void {
  const target = down.target as Element | null;
  const sheet = target?.closest?.('[data-eq-drag-dismiss]') as HTMLElement | null;
  if (!sheet || sheet.closest('[data-eq-exiting]')) return;

  const threshold = parseFloat(sheet.getAttribute('data-eq-drag-dismiss') || '96');
  const space = gestureSpace(sheet);
  const startX = down.clientX;
  const startY = down.clientY;
  const slop = space.onScreen(false, SLOP);

  // How far down the finger has carried the sheet, in the space its offset is in.
  const travelled = (ev: PointerEvent): number => space.along(false, ev.clientX - startX, ev.clientY - startY);

  // Short of the threshold, or taken away: glide back over Motion.Base, then clear the inline styles.
  const glideBack = (): void => {
    sheet.style.transition = `transform ${GLIDE_MS}ms ease-out`;
    sheet.style.transform = '';
    setTimeout(() => {
      sheet.style.transition = '';
    }, GLIDE_MS + 50);
  };

  followPointer(down, {
    element: sheet,
    arms: (ev) => travelled(ev) > slop,
    armed: () => {
      sheet.style.transition = 'none';
    },
    move: (ev) => {
      sheet.style.transform = `translateY(${Math.max(0, travelled(ev))}px)`;
    },
    release: (ev) => {
      if (travelled(ev) >= threshold) {
        sheet.dispatchEvent(new CustomEvent('eq-drag-dismiss'));
        return;
      }
      glideBack();
    },
    cancel: glideBack,
  }, delegate.guards);
}
