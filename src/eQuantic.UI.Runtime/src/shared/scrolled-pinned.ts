/**
 * Which pinned headers are SCROLLED: the browser half of `Pinned.ScrolledStyle` (#506).
 *
 * A header is scrolled while the surface it pins to has scrolled past the threshold, and that
 * surface is the one `position: sticky` itself pins to: the nearest ancestor that scrolls, or the
 * page when there is none. Photon answers the same question from the nearest ScrollView, so the
 * same tree frosts its header at the same moment on both targets.
 *
 * The state rides the HEADER, as an attribute the lowering never writes: the reconciler diffs the
 * tree it lowered, so it never takes away what it never wrote, and a re-render keeps the state. It
 * lived on `<html>` as one class for the whole page, which a header inside a scrolling panel could
 * not use: the window does not move when the panel does, and the header never frosted.
 */

import { PINNED_MARKER, SCROLLED_MARKER } from './markers';

/** `Pinned.ScrolledThreshold`, in dp: how far a header's surface scrolls before it is scrolled. */
export const SCROLLED_THRESHOLD = 8;

let installed = false;
let pending = false;

/**
 * Listens once per page. In the CAPTURE phase, since a scroll event does not bubble: one listener on
 * the document hears the page and every scrolling element alike, a code editor's and a sheet's among
 * them, so the work is coalesced to one sync per frame however many scroll events the frame brought.
 */
export function installScrolledController(): void {
  if (installed || typeof document === 'undefined') return;
  installed = true;
  document.addEventListener('scroll', onScroll, { capture: true, passive: true });
}

/** For a suite: forget the listener, as a new page would. */
export function resetScrolledController(): void {
  if (typeof document === 'undefined') return;
  installed = false;
  pending = false;
  document.removeEventListener('scroll', onScroll, { capture: true });
}

function onScroll(): void {
  if (pending) return;
  pending = true;
  const sync = (): void => {
    pending = false;
    syncScrolledPinned();
  };
  if (typeof requestAnimationFrame === 'function') requestAnimationFrame(sync);
  else sync();
}

/**
 * After the pass's DOM is written, every header is set from its surface: a header mounted on a page
 * that is already scrolled (a reload that restores the position, a navigation that keeps it) is
 * scrolled from its first frame, without waiting for the next scroll event.
 */
export function scheduleScrolledSync(): void {
  if (!installed) return;
  if (typeof queueMicrotask !== 'function') {
    syncScrolledPinned();
    return;
  }
  queueMicrotask(syncScrolledPinned);
}

/** Sets every pinned header from the surface it pins to. Writes only when the answer changed. */
export function syncScrolledPinned(): void {
  if (typeof document === 'undefined') return;
  for (const header of document.querySelectorAll<HTMLElement>(`[${PINNED_MARKER}]`)) {
    const scrolled = surfaceOffset(header) > SCROLLED_THRESHOLD;
    if (header.hasAttribute(SCROLLED_MARKER) !== scrolled) header.toggleAttribute(SCROLLED_MARKER, scrolled);
  }
}

/**
 * How far the surface a header pins to has scrolled, along the axis a header pins on. The surface is
 * the nearest ancestor that scrolls on EITHER axis, which is what a ScrollView lowers to (`auto` on
 * the axis it scrolls), or the page when there is none. A horizontal one is a surface too, the one
 * `position: sticky` pins to and Photon's nearest scroll view, and it never scrolls vertically, so a
 * header inside it is never scrolled, whatever the page under it does (found by Copilot on #688). A
 * box that only clips is hidden on both axes, and is not one.
 */
function surfaceOffset(header: HTMLElement): number {
  for (let element = header.parentElement; element; element = element.parentElement) {
    if (element === document.body || element === document.documentElement) break;
    const style = getComputedStyle(element);
    const vertical = scrolls(style.overflowY);
    if (vertical || scrolls(style.overflowX)) return vertical ? element.scrollTop : 0;
  }
  return window.scrollY;
}

const scrolls = (overflow: string): boolean => overflow === 'auto' || overflow === 'scroll';
