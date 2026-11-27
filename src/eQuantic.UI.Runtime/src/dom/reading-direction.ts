/**
 * The one place the runtime reads a reading direction from the page (#1569).
 *
 * What the SDK draws or hears by itself is bound to the direction where its element SITS, at the moment
 * the event fires: a key, a pointerdown, a pickup, a scroll read. It is never baked at lowering,
 * because a `Directionality` that changes inside a nested component rebuilds only the components that
 * read it, and a kept child that had baked `ltr` would keep it while the `dir` above it says `rtl`.
 *
 * The direction is the nearest `[dir]` ancestor-or-self that says `ltr` or `rtl`, which is exactly what
 * the SDK writes (the culture on `<html>`, a `Directionality` on its child, a fence on the host of a
 * table, a sheet or a code surface), and exactly what the token sheet's `--eq-inline` follows
 * (`[dir=ltr]` and `[dir=rtl]`), so what a handler decides and what the stylesheet paints cannot
 * disagree. It is not `getComputedStyle(element).direction`: that answer is the browser's, and a DOM
 * with no layout engine computes none from `dir`, so a spec of it would assert nothing.
 */

const DECLARES_DIRECTION = '[dir=rtl],[dir=ltr]';

/** Whether `element` reads right to left: its nearest `[dir]` ancestor-or-self says `rtl`. */
export function readsRightToLeft(element: Element | null | undefined): boolean {
  return element?.closest?.(DECLARES_DIRECTION)?.getAttribute('dir')?.toLowerCase() === 'rtl';
}
