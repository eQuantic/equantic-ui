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
 * The same DEFERRAL as `scheduleInViewCommit`, and for the same reason — but not the same shape, and
 * the difference is what let this file carry a defect those two could not have. They commit an
 * OBSERVER: an IntersectionObserver reports whenever visibility changes, a ResizeObserver redraws
 * whenever a canvas box settles. Each is a window over time. This one publishes a measurement, which
 * is a moment — so everything that arrives after it needs somewhere else to be caught, and twice now
 * that somewhere was got wrong. An observer survives being scheduled too early; a sample does not.
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
 * Which document the pending work belongs to. Bumped by the test seam, and read by every deferred
 * callback, so work booked before a reset can never land after one.
 */
let generation = 0;

/**
 * When this watch expires, and the VIEW it was booked for — module-level because both are contracts
 * of the correction itself, not of one code path to it.
 *
 * <para>
 * The deadline lived in the deferred tick's closure, which left the other door open:
 * `publishAnchorOffset` calls `realignColdLoad` directly on any later render pass whose measurement
 * changed, so a background render an hour in could still correct. Expiry belongs to the shared path.
 * </para>
 *
 * <para>
 * And the VIEW matters because this runtime is a SPA. The router pushes state on the same document
 * and applies each new fragment scroll itself; a cold-load watch still alive across a navigation
 * would read the NEW `location.hash` and scroll to somebody else's target. The watch is for the view
 * it was booked in and no other. Both found in review.
 * </para>
 */
let deadline = 0;
let watchedView = '';

/** The part of the address that decides whether this is still the same view. */
function currentView(): string {
  return typeof location === 'undefined'
    ? ''
    : location.pathname + location.search + location.hash;
}

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
 * this publishes after a render PASS, and a settled page has none. That second chance is a WINDOW
 * of frames, not an event — an earlier version of this file waited for `load`, on the reasoning
 * that `load` means the layout is final, and the measurement two paragraphs up is what disproved
 * it. Nothing names the moment of the jump, so the watch looks every frame until the target lands
 * in the band, bounded by a frame budget once the document completes and by a wall clock whatever
 * it does.
 * </para>
 */
function realignColdLoad(offset: number): void {
  if (coldLoadHandled) return;
  // The FIRST call arms the watch: it is the moment the correction became possible, and both bounds
  // are measured from it.
  if (deadline === 0) {
    deadline = now() + MaxWatchMs;
    watchedView = currentView();
  }
  // Expired, or the reader has navigated somewhere else. Either way this watch is over, and
  // retiring here covers the direct path as well as the deferred one.
  if (now() >= deadline || currentView() !== watchedView) {
    coldLoadHandled = true;
    return;
  }
  // Only a URL that ASKS for an element has anything to correct, and this is what keeps every other
  // page from booking a watch it will never use.
  if (typeof location === 'undefined' || location.hash.length <= 1) return;

  // A zero offset is a reason to come BACK, not a reason to stop. Chrome that is not there yet
  // measures zero — an image-backed header before its image, a bar whose webfont has not arrived —
  // and it can grow without any further render pass to notice. Retiring here would leave exactly
  // the page this correction exists for. Found in review; the first version returned on `offset
  // <= 0` before it had even looked for a target.
  const target = offset > 0 ? bookmarkTarget(location.hash, document) : null;
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
 * How many frames the correction keeps watching ONCE THE DOCUMENT HAS LOADED. The browser performs
 * its fragment jump when layout allows, which is not a moment any event names — so this counts
 * frames after the page has settled rather than waiting for a signal that does not exist.
 */
const FramesAfterLoad = 20;

/**
 * The wall clock the watch stops against, whatever the document says about itself.
 *
 * <para>
 * The frame budget only starts counting once `readyState` is complete, which is right — before that
 * the browser may still have a jump to perform. But a page with a stalled subresource can stay
 * non-complete for as long as it likes, and a budget that never starts is a rAF loop reading layout
 * every frame for the life of the tab. Found in review. The grace window keeps its job; this is the
 * bound that does not depend on the page cooperating.
 * </para>
 */
const MaxWatchMs = 10_000;

/**
 * A WINDOW, not a moment.
 *
 * <para>
 * The first version of this waited for `load`, on the reasoning that `load` means the layout the
 * browser jumped against is final. Measured on a live site, that is false in the direction that
 * matters: `load` can fire BEFORE the jump, and the faster the page the more reliably it does.
 * Same URL, same bytes, one variable — the cache:
 * </para>
 *
 * <para>
 * <c>cold, loadEventEnd 1310ms → target at 65, correct.</c>
 * <c>warm, loadEventEnd 36ms → target at 0, wrong.</c>
 * </para>
 *
 * <para>
 * The arithmetic of the failing case is what names the cause: it lands at the anchor's exact
 * document offset, which is where a jump with NO scroll-margin puts it. Nothing scrolled over the
 * correction — the correction never ran, because the single chance was spent at a `load` that beat
 * the jump. So a returning visitor, which is almost everyone, was always in the broken column, and
 * so is any test that loads instantly.
 * </para>
 *
 * <para>
 * Watching frames instead: the jump lands in some frame, and the frame it lands in is the one that
 * sees the target inside the band. Bounded, because a correction that watches forever would yank a
 * reader who scrolls into the band an hour later.
 * </para>
 */
function bookRecheck(): void {
  if (recheckBooked || typeof window === 'undefined') return;
  recheckBooked = true;
  // The DOCUMENT this work belongs to. A deferred callback outlives the reset that a spec performs
  // between cases, so without this a frame booked by one document runs against the next one's DOM
  // and either suppresses its correction or performs one nobody asked for — a suite that lies about
  // itself, which is worse than a suite that fails. Found in review.
  const booked = generation;
  let framesLeft = FramesAfterLoad;

  const tick = (): void => {
    if (booked !== generation || coldLoadHandled) return;

    // MEASURED AGAIN, never the number that booked this. The layout was not final — that is why we
    // are here — and the chrome is part of that layout: a bar that wraps at a narrow width, or grows
    // when a webfont finally arrives, is taller later than at first paint.
    const measured = overlappingChrome();
    publishMeasured(measured);
    realignColdLoad(measured);
    if (coldLoadHandled) return;

    // The frame budget can stay AFTER the correction: it only decides when to stop watching a page
    // that is behaving, and a tick inside the deadline is one this watch is entitled to act on.
    if (document.readyState === 'complete' && --framesLeft <= 0) {
      coldLoadHandled = true;
      return;
    }
    schedule(tick);
  };

  schedule(tick);
}

/**
 * A frame, or the nearest thing to one — RACED with a timer, whichever arrives first, exactly once.
 *
 * <para>
 * Guarding on <c>typeof requestAnimationFrame</c> is not enough, and the reason is the case this
 * watch is most likely to meet: a browser keeps the function defined in a BACKGROUND TAB and simply
 * stops calling it back. There the rAF branch is taken and nothing ever runs, so neither the frame
 * budget nor the wall clock is enforced and the watch stays armed until the tab returns — at which
 * point it could yank a reader long past its own deadline. Found in review.
 * </para>
 *
 * <para>
 * The timer is therefore a BACKSTOP rather than a fallback, which is the shape
 * <c>core/render-scheduler.ts</c> already uses for the same reason. It also keeps the property the
 * previous round was about: the callback never runs synchronously, so a loading document cannot
 * recurse and a complete one cannot spend its whole budget inside one stack frame.
 * </para>
 */
function schedule(callback: () => void): void {
  let ran = false;
  const once = (): void => {
    if (ran) return;
    ran = true;
    callback();
  };
  if (typeof requestAnimationFrame === 'function') requestAnimationFrame(once);
  setTimeout(once, BackstopMs);
}

/** Long enough not to fight rAF on a visible tab, short enough to bound a hidden one. */
const BackstopMs = 120;

/** Monotonic where it exists; the wall clock is only used to bound a watch, so Date is enough. */
function now(): number {
  return typeof performance !== 'undefined' && typeof performance.now === 'function'
    ? performance.now()
    : Date.now();
}

/** Test seam: the correction is once per document, and a spec renders many. */
export function resetColdLoadRealignmentForTests(): void {
  coldLoadHandled = false;
  recheckBooked = false;
  deadline = 0;
  watchedView = '';
  generation += 1;
}
