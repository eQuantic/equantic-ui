/**
 * How many arms of an adaptive node the browser holds, and the width it resolves one against (#1783).
 *
 * A page is SERVED with every arm of each adaptive node, each behind the media gate that shows it, and
 * it HYDRATES so, adopting the markup as the server wrote it. Then the document holds one arm per node,
 * the one the window's width resolves, as Photon holds it: the arms the window hides are no longer in the
 * page, so their components do not run, and a control in the page is there once rather than once per arm.
 * A window crossing a threshold renders the page again, and the arm it now shows takes the place of the
 * other; what a global key carries crosses with it (#797).
 *
 * A tree laid out once and drawn by nothing again (`structuralOwner`) keeps every arm behind its gate,
 * since nothing renders it when the window changes, and so does a document before it has hydrated.
 *
 * A LEAF: the lowering reads it, and the boot flips it once the page has hydrated.
 */
import { adaptiveThresholdMedia } from './style-atomizer';

let everyArm = true;
let invalidate: (() => void) | null = null;

/** The thresholds the page's adaptive nodes switch at, each with the media query that crosses it (null
 *  where the environment has no `matchMedia`). */
const thresholds = new Map<number, MediaQueryList | null>();

/** Whether the document holds every arm of an adaptive node: until the page has hydrated. */
export function documentHoldsEveryArm(): boolean {
  return everyArm;
}

/**
 * From now on the document holds one arm per adaptive node. The boot calls it once the served page has
 * hydrated, and renders the page again, which takes the hidden arms out; a page drawn in the browser from
 * the start holds one arm from its first render.
 */
export function holdOneArm(): void {
  everyArm = false;
}

/** How the page renders again when the window crosses a threshold: the boot hands the page's render. */
export function setAdaptiveInvalidator(render: (() => void) | null): void {
  invalidate = render;
}

/** Whether an adaptive node of more than one arm has been drawn, so holding one arm changes anything. */
export function anyThresholdNoted(): boolean {
  return thresholds.size > 0;
}

/** Records `dp` as a threshold an adaptive node of the page switches at, and listens for the window
 *  crossing it. */
export function noteThreshold(dp: number): void {
  if (thresholds.has(dp)) return;
  if (typeof window === 'undefined' || typeof window.matchMedia !== 'function') {
    thresholds.set(dp, null);
    return;
  }
  const query = window.matchMedia(adaptiveThresholdMedia(dp));
  thresholds.set(dp, query);
  const crossed = (): void => {
    if (!everyArm) invalidate?.();
  };
  if (typeof query.addEventListener === 'function') query.addEventListener('change', crossed);
  else query.addListener?.(crossed);
}

/**
 * The window's width in dp, as an adaptive node resolves its arm against it, kept on the side of each
 * known threshold that the threshold's media query says: `innerWidth` is a whole number, and a zoomed
 * window is wide by a fraction the gates' media queries see, so near a threshold the two could pick
 * different arms, and the arm held would be one no gate shows.
 */
export function adaptiveWindowWidth(): number {
  if (typeof window === 'undefined') return Number.POSITIVE_INFINITY;
  let width = window.innerWidth;
  let atLeast = Number.NEGATIVE_INFINITY;
  let below = Number.POSITIVE_INFINITY;
  for (const [dp, query] of thresholds) {
    if (!query) continue;
    if (query.matches) atLeast = Math.max(atLeast, dp);
    else below = Math.min(below, dp);
  }
  if (width < atLeast) width = atLeast;
  if (width >= below) width = Math.max(atLeast, below - 0.00001);
  return width;
}

/** Back to how a document starts: every arm held, no threshold known. For tests. */
export function resetAdaptiveWindow(): void {
  everyArm = true;
  invalidate = null;
  thresholds.clear();
}
