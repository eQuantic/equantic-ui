/**
 * How much room a bookmark keeps above itself, measured from the sticky chrome that would otherwise
 * cover it.
 *
 * A browser scrolls a link's target to the very top of the viewport, so under a fixed header the
 * target arrives BEHIND the header: the link works and the page looks broken, which is the more
 * expensive of the two bugs. CSS fixes it with `scroll-margin-top`, and the number is the header's
 * height — which nobody should have to write down, because the app already said where its chrome is
 * by putting a `Sticky` in the tree.
 *
 * So it is MEASURED, not declared. One site here has a 60dp nav on its landing and a 56dp topbar in
 * its docs; neither states a number, and a global constant would land one of them four points off.
 * Measuring also survives what a constant cannot: a bar that wraps at a narrow width, or grows when
 * the type scale does.
 *
 * Published as `--eq-anchor-offset` on the document root, which is where every bookmark reads it
 * from — the two are in different subtrees, so a variable on the sticky itself would not reach.
 */

import { bookmarkTarget } from './bookmark-target';
import { PINNED_MARKER } from './markers';

const VARIABLE = '--eq-anchor-offset';

/** The sticky chrome that overlaps the top of the viewport, tallest first. */
function overlappingChrome(): number {
  if (typeof document === 'undefined') return 0;
  let tallest = 0;
  for (const element of document.querySelectorAll<HTMLElement>(`[${PINNED_MARKER}]`)) {
    const box = element.getBoundingClientRect();
    // Only what actually sits AT the top: a sticky that has not pinned yet, or one pinned to the
    // bottom, covers nothing a bookmark would land under.
    if (box.top > 1 || box.height <= 0) continue;
    tallest = Math.max(tallest, box.bottom);
  }
  // CEIL, not round: a bar measured at 55.4 device pixels rounds down to 55 and leaves the target
  // four tenths of a pixel behind it — under-offsetting is visible and over-offsetting by less than
  // one pixel is not, so the error is spent on the harmless side.
  return Math.max(0, Math.ceil(tallest));
}

/**
 * Publishes AFTER the pass's DOM has been written.
 *
 * The pass ends while the tree it produced is still a value — the render manager writes it once
 * `exitPass` has returned. Measuring inside the pass therefore looks for chrome that does not
 * exist yet and finds none: on a client-only render the variable would sit at 0px until some
 * later pass happened to run, and every bookmark in between would land under the header.
 *
 * The same shape, and the same reason, as `scheduleInViewCommit` — whose comment three lines from
 * the call site said exactly this, which is where I should have read it.
 */
export function scheduleAnchorOffset(): void {
  if (typeof queueMicrotask !== 'function') {
    publishAnchorOffset();
    return;
  }
  queueMicrotask(publishAnchorOffset);
}

/**
 * Measures and publishes. Idempotent and cheap enough to call after every pass — it writes only
 * when the number changed, so it never invalidates style for nothing.
 */
export function publishAnchorOffset(): void {
  if (typeof document === 'undefined') return;
  const measured = overlappingChrome();
  if (publishMeasured(measured)) realignColdLoad(measured);
}

/**
 * Writes the variable, and answers whether it CHANGED. Separate from measuring because the deferred
 * re-check needs the number without the early return: the common case at `load` is chrome that has
 * not moved, and a correction that is skipped because the variable already says 65px is the same
 * class of miss this whole function exists to undo.
 */
function publishMeasured(measured: number): boolean {
  const root = document.documentElement;
  const next = `${measured}px`;
  if (root.style.getPropertyValue(VARIABLE) === next) return false;
  root.style.setProperty(VARIABLE, next);
  return true;
}

/** Whether a cold load's fragment jump has been corrected, or has run out of chances. */
let coldLoadHandled = false;
/** Whether the one deferred re-check is already booked, so a burst of passes books it once. */
let recheckBooked = false;

/**
 * A COLD load with a fragment lands the target UNDER the chrome, and this is the one place that can
 * undo it.
 *
 * The browser performs the fragment jump while the document is still being set up, so
 * `--eq-anchor-offset` is unset and `scroll-margin-top` resolves to its `0px` fallback: the target
 * arrives at the very top of the viewport, behind the header, and nothing re-applies it once the
 * real number is published a moment later. Measured on a fresh `/privacy#rights`: the page scrolled,
 * `targetTop` 0 against an offset that had by then become 65px.
 *
 * Corrected ONCE, and only when the target really is behind the chrome — its top inside `[0, offset)`
 * is exactly the broken state and nothing else. A reader who has already scrolled somewhere else
 * leaves the band.
 *
 * <para>
 * The chance is spent on the CORRECTION, never on the measurement, and that distinction is the
 * whole defect this shape replaces. The browser re-runs its fragment jump as late content settles
 * the layout, so a measurement can easily land while the target is still far down the page — out of
 * the band, nothing to do. Retiring there spent the only chance on a moment when there was nothing
 * to correct, and the jump that followed had no one left to undo it. Reported from a live site:
 * `/privacy#rights` cold, `scrollY 3992`, `targetTop 0` on every sample over four seconds, with the
 * variable and `scroll-margin-top` both reading 65px — and the same site's WARM navigation to the
 * same fragment landing correctly, which is what said the mechanism was right and the ORDER was not.
 * </para>
 *
 * <para>
 * A second chance is booked rather than assumed, because there may be no second measurement at all:
 * this publishes after a render PASS, and a settled page has none. So the re-check rides the
 * document's own `load`, which is the event that says the layout the browser jumped against is
 * final.
 * </para>
 */
function realignColdLoad(offset: number): void {
  if (coldLoadHandled || offset <= 0) return;
  if (typeof location === 'undefined') return;
  const target = bookmarkTarget(location.hash, document);
  if (!target) {
    bookRecheck();
    return;
  }
  const top = target.getBoundingClientRect().top;
  if (top < 0 || top >= offset) {
    bookRecheck();
    return;
  }
  coldLoadHandled = true;
  target.scrollIntoView();
}

/**
 * One deferred chance, after the browser's own jump has settled. `load` is the signal because it is
 * the one that means the layout is final; a document already complete gets the next frame instead,
 * which is still after any jump the browser has queued.
 */
function bookRecheck(): void {
  if (recheckBooked || typeof window === 'undefined') return;
  recheckBooked = true;
  const recheck = (): void => {
    if (coldLoadHandled) return;
    // MEASURED AGAIN, never the number that booked this. The whole point of deferring is that the
    // layout was not final, and the chrome is part of that layout: a bar that wraps at a narrow
    // width, or grows when a webfont finally arrives, is taller at `load` than at first paint.
    // Correcting against the stale number would leave the target under the header it actually has.
    const measured = overlappingChrome();
    publishMeasured(measured);
    // Retire here whatever the answer: this WAS the second chance, and a page that is still wrong
    // after its own load event is not something a later scroll should be yanked for.
    realignColdLoad(measured);
    coldLoadHandled = true;
  };
  if (document.readyState === 'complete') {
    if (typeof requestAnimationFrame === 'function') requestAnimationFrame(recheck);
    else recheck();
    return;
  }
  window.addEventListener('load', () => recheck(), { once: true });
}

/** Test seam: the correction is once per document, and a spec renders many. */
export function resetColdLoadRealignmentForTests(): void {
  coldLoadHandled = false;
  recheckBooked = false;
}
