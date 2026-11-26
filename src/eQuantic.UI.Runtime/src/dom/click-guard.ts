/**
 * The click a gesture's end sends that was no click: the release of an armed drag, of a finger's hold that opened
 * a menu, of a pan on a table. Each of them stops that click once, before anything under the pointer hears it,
 * with a guard on the host it holds.
 *
 * A guard is OWNED (#1889): it is kept by what armed it, and it goes, timer and listener, with the click it stops,
 * at once when its owner lets go of what it holds, and otherwise after its time, so a gesture whose release sends
 * no click does not eat the next real one. Each place used to remove its guard on a bare timer that nothing
 * cancelled (the table's became its own in #1876). A spec's page torn down before the timer fired had it call
 * a host that had lost its methods, and the runtime suite failed runs in which every file had passed. In an
 * app, the listener outlived what armed it.
 *
 * The owners: the context controller for a hold (`context-request.ts`), each gesture controller's delegate for a
 * drag (`pointer-gesture.ts`), the gesture arena for a press that lost (`gesture-arena.ts`), each table for its pan
 * (`table-viewports.ts`).
 */

/** One guard, which its owner may let go of, or start the time of, after arming it. */
export interface ClickGuard {
  /** Lets go `ms` from now if no click came first. A guard armed with no time waits for this: the arena's press
   * lost before its up, and the click it stops comes only after the up. */
  expireAfter(ms: number): void;
  /** Lets go at once, timer and listener. */
  release(): void;
}

/** The guards one owner armed and still holds. */
export interface ClickGuards {
  /**
   * Stops the next click on `host` that `accepts` takes (every click when it is left out), in the capture phase,
   * before anything under the pointer hears it, and lets go after `ms` if no such click came, or, with no `ms`,
   * once its time is started ({@link ClickGuard.expireAfter}). The host is the one the caller holds rather than the
   * global read when the guard goes: by then a torn-down page may have lost it.
   */
  arm(host: EventTarget, ms?: number, accepts?: (event: Event) => boolean): ClickGuard;
  /** Lets go, at once, of every guard this owner armed and still holds, timer and listener. */
  release(): void;
}

/** A new owner's guards, holding none yet. */
export function clickGuards(): ClickGuards {
  const held = new Set<() => void>();
  return {
    arm(host: EventTarget, ms?: number, accepts?: (event: Event) => boolean): ClickGuard {
      let timer: ReturnType<typeof setTimeout> | undefined;
      // The click it stops is the one it waited for: it goes then, with its timer.
      const stop = (event: Event): void => {
        if (accepts && !accepts(event)) return;
        event.stopPropagation();
        event.preventDefault();
        release();
      };
      function release(): void {
        if (!held.delete(release)) return;
        if (timer !== undefined) clearTimeout(timer);
        host.removeEventListener('click', stop, { capture: true });
      }
      const guard: ClickGuard = {
        expireAfter(after: number): void {
          if (!held.has(release)) return;
          if (timer !== undefined) clearTimeout(timer);
          timer = setTimeout(release, after);
        },
        release,
      };
      host.addEventListener('click', stop, { capture: true });
      held.add(release);
      if (ms !== undefined) guard.expireAfter(ms);
      return guard;
    },
    release(): void {
      for (const release of [...held]) release();
    },
  };
}
