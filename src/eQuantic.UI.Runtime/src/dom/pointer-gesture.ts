/**
 * What every pointer gesture on the web shares (#1535): one document-level `pointerdown` delegate,
 * installed lazily by the lowering that needs it, and, from a press, a gesture that ARMS once the pointer
 * means it (it travelled past the slop, or a finger held still), follows the pointer while it is armed,
 * and ends on the release or when the system takes the pointer away.
 *
 * `draggable.ts` and `drag-dismiss.ts` each wrote this skeleton by hand, and had drifted: the sheet never
 * listened for `pointercancel`, so a browser that claimed the pointer left its listeners behind. The
 * sortable controller is the third gesture, and the reason the skeleton is one module now. What a gesture
 * MEANS (how far it travels, what it moves, what it reports) stays in its own module.
 */

/** Touch.PressCancelSlop: the travel, in screen pixels, past which a press is a drag (cross-pinned with
 * the C# host). */
export const SLOP = 12;

/** One gesture, from the press that may start it. */
export interface PointerGesture {
  /** Whether the pointer, this far from its press, starts the gesture: asked on every move until it does.
   * Left out, only a press held for {@link holdMs} arms it. */
  arms?(ev: PointerEvent): boolean;
  /** A press held still this long arms the gesture without travel, a finger's long press; and travel past
   * the slop before it is a scroll, which ends the watch. Left out, holding arms nothing. */
  holdMs?: number;
  /** The gesture armed, by travel (the move that armed it) or by holding (null). */
  armed?(ev: PointerEvent | null): void;
  /** Every move once armed. The browser's own reaction, a scroll or a selection, is prevented. */
  move(ev: PointerEvent): void;
  /** The pointer lifted on an armed gesture. The click the release ends in is swallowed: a tap on an
   * inner button that was really a drag never presses it. */
  release(ev: PointerEvent): void;
  /** The armed gesture ended with nothing decided: the system took the pointer, or the gesture was
   * cancelled (Escape). */
  cancel(): void;
}

/** A gesture in progress, which its owner may end with nothing decided. */
export interface FollowedPointer {
  cancel(): void;
}

/**
 * A document-level `pointerdown` delegate, installed once however many lowerings ask for it, and a test
 * seam that uninstalls it.
 */
export function pointerDownDelegate(onDown: (down: PointerEvent) => void): {
  install(): void;
  reset(): void;
} {
  let installed = false;
  const listener = (down: Event): void => onDown(down as PointerEvent);
  return {
    install(): void {
      if (installed || typeof document === 'undefined') return;
      installed = true;
      document.addEventListener('pointerdown', listener);
    },
    reset(): void {
      if (typeof document === 'undefined') return;
      installed = false;
      document.removeEventListener('pointerdown', listener);
    },
  };
}

/** Follows one pointer from its press: see {@link PointerGesture} for what each moment means. */
export function followPointer(down: PointerEvent, gesture: PointerGesture): FollowedPointer {
  // Listeners and timers hold the DOCUMENT they registered on rather than reading the global when they
  // fire: a torn-down test environment removed the global while a cleanup was pending, and the callback
  // threw `document is not defined` where nothing could catch it.
  const host = document;
  const startX = down.clientX;
  const startY = down.clientY;
  let armed = false;
  let over = false;
  let hold: ReturnType<typeof setTimeout> | null = null;

  const arm = (ev: PointerEvent | null): void => {
    armed = true;
    if (hold !== null) clearTimeout(hold);
    hold = null;
    // A finger's drag must not scroll the page under it, and only a touch listener that is not passive
    // can say so: pointermove's preventDefault does not stop a touch scroll.
    host.addEventListener('touchmove', preventTouch, { passive: false });
    gesture.armed?.(ev);
  };

  const detach = (): void => {
    over = true;
    if (hold !== null) clearTimeout(hold);
    hold = null;
    host.removeEventListener('pointermove', move);
    host.removeEventListener('pointerup', up);
    host.removeEventListener('pointercancel', cancelled);
    host.removeEventListener('touchmove', preventTouch);
  };

  const move = (ev: Event): void => {
    const pointer = ev as PointerEvent;
    if (!armed) {
      if (gesture.holdMs !== undefined && gesture.arms === undefined) {
        // Watching for a hold: travel first is a scroll, and the watch ends there.
        if (Math.hypot(pointer.clientX - startX, pointer.clientY - startY) > SLOP) detach();
        return;
      }
      if (!gesture.arms?.(pointer)) return;
      arm(pointer);
    }
    ev.preventDefault();
    gesture.move(pointer);
  };

  const up = (ev: Event): void => {
    detach();
    if (!armed) return;
    host.addEventListener('click', squashClick, { capture: true, once: true });
    setTimeout(() => host.removeEventListener('click', squashClick, { capture: true }), 50);
    gesture.release(ev as PointerEvent);
  };

  const cancelled = (): void => {
    detach();
    if (armed) gesture.cancel();
  };

  if (gesture.holdMs !== undefined)
    hold = setTimeout(() => {
      hold = null;
      if (!over && !armed) arm(null);
    }, gesture.holdMs);

  host.addEventListener('pointermove', move, { passive: false });
  host.addEventListener('pointerup', up);
  host.addEventListener('pointercancel', cancelled);

  return {
    cancel(): void {
      if (over) return;
      cancelled();
    },
  };
}

function preventTouch(ev: Event): void {
  ev.preventDefault();
}

function squashClick(e: Event): void {
  e.stopPropagation();
  e.preventDefault();
}
