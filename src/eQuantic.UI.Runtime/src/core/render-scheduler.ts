/**
 * eQuantic.UI Runtime - Re-render scheduling that survives a hidden tab
 *
 * `requestAnimationFrame` is the right batching primitive while the page is visible: one flush per
 * frame, aligned with paint. But browsers stop firing frames entirely in a background tab, so a
 * rAF-ONLY schedule leaves a setState driven by a timer, a socket or any other background event
 * mutating state while the DOM stays frozen — and worse, it latches the caller's "already scheduled"
 * flag forever, swallowing every later setState into the one frame that never arrives. The DOM then
 * stays stale even after the tab comes back.
 *
 * So each schedule races two sources, and the first one to fire wins:
 *  - rAF, skipped outright when the document is already hidden (it would never fire);
 *  - a `setTimeout` backstop, which also covers the tab going hidden AFTER the frame was requested.
 *
 * One shared `visibilitychange` listener drains whatever is still pending the moment the tab comes
 * back, because background timers are throttled (Chrome: >= 1s, and >= 1min once a tab has been
 * hidden long enough) and the catch-up repaint shouldn't have to wait out a throttled timer.
 */

/** Backstop delay while visible — comfortably longer than a ~16ms frame, so rAF wins the race. */
const VISIBLE_BACKSTOP_MS = 100;

/** Hidden: no frame is coming, so flush on the next macrotask (subject to browser throttling). */
const HIDDEN_FLUSH_MS = 0;

interface PendingFlush {
  /** Idempotent: whichever source fires first flushes, the losers of the race become no-ops. */
  run: () => void;
}

const pending = new Set<PendingFlush>();
let visibilityHooked = false;

function isHidden(): boolean {
  return typeof document !== 'undefined' && document.visibilityState === 'hidden';
}

function hookVisibility(): void {
  if (visibilityHooked || typeof document === 'undefined') return;
  visibilityHooked = true;
  document.addEventListener('visibilitychange', () => {
    if (isHidden()) return;
    // Frames flow again: flush now rather than waiting out a throttled background timer.
    for (const entry of [...pending]) entry.run();
  });
}

/**
 * Schedule `flush` for the next frame, falling back to a timer whenever frames aren't coming
 * (hidden tab). Callers keep their own "already scheduled" flag; `flush` runs exactly once per call,
 * so that flag can be cleared inside it.
 */
export function scheduleRenderFlush(flush: () => void): void {
  hookVisibility();

  const entry: PendingFlush = {
    run: () => {
      // Set membership IS the once-only guard: the losers of the race find themselves already gone.
      if (!pending.delete(entry)) return;
      flush();
    },
  };
  pending.add(entry);

  const hidden = isHidden();
  if (!hidden && typeof requestAnimationFrame === 'function') {
    requestAnimationFrame(entry.run);
  }
  setTimeout(entry.run, hidden ? HIDDEN_FLUSH_MS : VISIBLE_BACKSTOP_MS);
}
