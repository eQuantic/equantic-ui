/**
 * An anchored panel its placement would put past the viewport's start or end edge is moved back inside, a fixed inset
 * from the edge (#1933), as Photon's realizer moves it once its layer is laid out. The web places the panel with CSS
 * alone, an absolute box in its anchor's host, and CSS cannot know where that box lands in the viewport, so the move is
 * measured here: after each pass is written for the panels the page holds open, when the pointer or the focus reaches
 * a hover-revealed one, and for all of them when the viewport changes width. A date picker's calendar, hung under a
 * field that spans its column, left the viewport at every width and the page scrolled sideways.
 *
 * The move is the panel's `translate`, a property of its own beside the `transform` a centred placement and an
 * open/close glide already write, so neither is undone by it.
 */

/** The C# `Anchored.ViewportInset` twin: the distance a panel keeps from the viewport's start and end edges. */
export const ANCHOR_VIEWPORT_INSET = 8;

const PANEL = '.eq-anchor-panel';
const HOVER_HOST = '.eq-hoverreveal';

/**
 * The horizontal move that brings a box spanning `left` to `right` inside `inset` of a viewport `viewport` wide: past
 * the end edge it comes back to end there, and past the start edge, or wider than the room between them, it starts at
 * the start edge's inset. The rule Photon's `PhotonRealizer.KeepInside` applies.
 */
export function insetShift(left: number, right: number, viewport: number, inset = ANCHOR_VIEWPORT_INSET): number {
  const width = right - left;
  let x = right > viewport - inset ? viewport - inset - width : left;
  if (x < inset) x = inset;
  return x - left;
}

/** Moves one panel back inside the viewport, measured from where its placement put it. */
export function keepInside(panel: HTMLElement): void {
  panel.style.translate = '';
  const rect = panel.getBoundingClientRect();
  // A panel that lays out nothing (not shown, or measured off-browser) has nothing to move.
  if (rect.width === 0 && rect.height === 0) return;
  const viewport = document.documentElement.clientWidth || window.innerWidth;
  const shift = insetShift(rect.left, rect.right, viewport);
  if (Math.abs(shift) > 0.5) panel.style.translate = `${shift}px 0`;
}

/** The panels the page holds open: every anchored panel but a hover-revealed one, which moves when it is reached. */
function keepOpenPanelsInside(): void {
  for (const panel of document.querySelectorAll<HTMLElement>(PANEL))
    if (!panel.parentElement?.closest(HOVER_HOST)) keepInside(panel);
}

function keepReachedPanelInside(event: Event): void {
  const host = (event.target as Element | null)?.closest?.(HOVER_HOST);
  const panel = host?.querySelector<HTMLElement>(`:scope > ${PANEL}`);
  if (panel) keepInside(panel);
}

let installed = false;

/** Called from the commit path once a pass is written: the open panels move inside, and the page-wide listeners
 * (the viewport's width, a hover-revealed panel reached) are installed once. */
export function commitAnchoredPanels(): void {
  if (typeof document === 'undefined') return;
  keepOpenPanelsInside();
  if (installed) return;
  installed = true;
  window.addEventListener('resize', () => {
    for (const panel of document.querySelectorAll<HTMLElement>(PANEL)) keepInside(panel);
  });
  document.addEventListener('pointerover', keepReachedPanelInside, true);
  document.addEventListener('focusin', keepReachedPanelInside, true);
}
