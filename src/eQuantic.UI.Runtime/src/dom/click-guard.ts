/**
 * The click a gesture's end sends that was no click: the release of an armed drag, of a finger's hold that opened
 * a menu, of a pan on a table. Each of them stops that click once, before anything under the pointer hears it,
 * with a guard on the host it holds.
 *
 * A guard is OWNED (#1889): it is kept by what armed it, and it goes, timer and listener, with the click it stops,
 * at once when its owner lets go of what it holds, and otherwise after its time, so a gesture whose release sends
 * no click does not eat the next real one. It used to be one bare timer per place, which nothing cancelled. A
 * spec's page torn down before the timer fired had it call a host that had lost its methods, and the runtime
 * suite failed runs in which every file had passed. In an app, the listener outlived what armed it.
 *
 * The owners: the context controller for a hold (`context-request.ts`), each gesture controller's delegate for a
 * drag (`pointer-gesture.ts`), each table for its pan (`table-viewports.ts`).
 */

/** The guards one owner armed and still holds. */
export interface ClickGuards {
  /** Stops the next click on `host`, in the capture phase, before anything under the pointer hears it, and lets
   * it go after `ms` if no click came. The host is the one the caller holds rather than the global read when
   * the guard goes: by then a torn-down page may have lost it. */
  arm(host: EventTarget, ms: number): void;
  /** Lets go, at once, of every guard this owner armed and still holds, timer and listener. */
  release(): void;
}

/** A new owner's guards, holding none yet. */
export function clickGuards(): ClickGuards {
  const held = new Set<() => void>();
  return {
    arm(host: EventTarget, ms: number): void {
      // The click it stops is the one it waited for: it goes then, with its timer.
      const stop = (event: Event): void => {
        event.stopPropagation();
        event.preventDefault();
        release();
      };
      host.addEventListener('click', stop, { capture: true, once: true });
      const timer = setTimeout(release, ms);
      function release(): void {
        if (!held.delete(release)) return;
        clearTimeout(timer);
        host.removeEventListener('click', stop, { capture: true });
      }
      held.add(release);
    },
    release(): void {
      for (const release of [...held]) release();
    },
  };
}
