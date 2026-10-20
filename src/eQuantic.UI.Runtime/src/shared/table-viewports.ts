/**
 * The after-pass half of a table viewport on the web (the C# twin is Photon's host and emit): lowering
 * DECLARES each viewport, keyed by the path stamped on its element as `data-eq-table`, with the
 * offset its cells were placed at; the commit, once the pass is written, finds the element and drives
 * the scroll model the node carries.
 *
 * The model owns the offset (the browser scrolls nothing: an element is at most 16 777 214 px tall,
 * which a million rows pass). So this half reads the window's size, hands the model the wheel, the
 * pointer and the clock, and paints what it answers: each band's layer moved by the distance scrolled
 * since its cells were placed (a scroll step inside the built cells rewrites no cell), the scrollbars
 * where the model puts them, and the bands short of them. It tells the owner where the grid stands and
 * how large the window is, each once per change, and gives a viewport asked to fill an axis with no
 * bound the visible extent of the scroller around it.
 */

import { logger } from '../utils/logger';
import { modifiersOf } from './css-values';
import type { TableScrollModelLike, TableViewportNode } from './nodes';

/** What the latest pass said about one viewport. */
export interface TableViewportDeclaration {
  node: TableViewportNode;
  /** The offset the pass placed the cells at: the layers move by the distance from it. */
  base: { across: number; down: number };
  pinnedAcross: number;
  pinnedDown: number;
}

/** A wheel's line, in points: the C# `Touch.WheelLine`. */
const WHEEL_LINE = 16;

const declared = new Map<string, TableViewportDeclaration>();

/** Marker properties live on the ELEMENT: the reconciler reuses it, so what was attached survives. */
interface TableElement extends HTMLElement {
  __eqTable?: TableViewportDeclaration;
  __eqTableObserver?: ResizeObserver;
  __eqTableListening?: boolean;
  __eqTableFrame?: number;
  __eqTableReported?: { across: number; down: number; width: number; height: number };
  __eqTableLogged?: number;
  /** The axes whose extent this half set from a scroller (1 across, 2 down), and the scroller. */
  __eqTableFitted?: number;
  __eqTableScroller?: HTMLElement | null;
}

export function declareTableViewport(path: string, declaration: TableViewportDeclaration): void {
  declared.set(path, declaration);
}

const watched = new Set<TableElement>();

function stopWatching(view: TableElement): void {
  view.__eqTableObserver?.disconnect();
  view.__eqTableObserver = undefined;
  if (view.__eqTableFrame !== undefined) cancelAnimationFrame(view.__eqTableFrame);
  view.__eqTableFrame = undefined;
  watched.delete(view);
}

/** Lets go of every viewport that left the document: an unmount runs no pass that would notice. */
export function releaseDetachedTableViewports(): void {
  for (const view of [...watched]) if (!view.isConnected) stopWatching(view);
}

let scheduled = false;

/** Commits after the pass's DOM is written, once for however many passes end before it runs (the
 * scroll viewports' move, for the same reasons). */
export function scheduleTableViewportCommit(): void {
  // A pass that declared none may still have removed one: the commit is where a table that left the
  // document lets go of its observer, so it runs while any is watched, as the scroll views' does.
  if (scheduled || (declared.size === 0 && watched.size === 0)) return;
  scheduled = true;
  const commit = () => {
    scheduled = false;
    commitTableViewports();
  };
  if (typeof queueMicrotask === 'function') queueMicrotask(commit);
  else void Promise.resolve().then(commit);
}

export function commitTableViewports(): void {
  if (declared.size === 0 && watched.size === 0) return;
  if (typeof document === 'undefined') {
    declared.clear();
    return;
  }
  for (const view of document.querySelectorAll<TableElement>('[data-eq-table]')) {
    const declaration = declared.get(view.getAttribute('data-eq-table') ?? '');
    if (!declaration) continue;
    view.__eqTable = declaration;
    listen(view);
    arrange(view, true);
    const { node } = declaration;
    if (node.scroll.apply(node.request ?? null, node.rows, node.columns, pinnedRowsOf(node), pinnedColumnsOf(node))) {
      arrange(view, false);
    }
    paint(view);
    report(view);
    if (typeof ResizeObserver === 'function' && !view.__eqTableObserver) {
      // A resize is answered without touching the element's own size: re-deciding it here resized
      // what is being observed inside its own notification, which the browser reports as a loop.
      const observer = new ResizeObserver(() => {
        arrange(view, false);
        paint(view);
        report(view);
      });
      observer.observe(view);
      view.__eqTableObserver = observer;
      watched.add(view);
    }
    // A viewport that took its scroller's extent follows that scroller's size, which is what changes
    // when the window does.
    if (view.__eqTableScroller && view.__eqTableObserver) view.__eqTableObserver.observe(view.__eqTableScroller);
  }
  for (const view of [...watched]) if (!view.isConnected) stopWatching(view);
  declared.clear();
}

function pinnedRowsOf(node: TableViewportNode): number {
  return Math.min(Math.max(node.pinnedRows ?? 0, 0), node.rows.count);
}

function pinnedColumnsOf(node: TableViewportNode): number {
  return Math.min(Math.max(node.pinnedColumns ?? 0, 0), node.columns.count);
}

/**
 * The window the model is told, after the unbounded axes took their extent. A viewport's box comes out
 * empty on an axis whose parent has no definite size, because its cells are positioned and say
 * nothing about it; there it takes the visible extent of the nearest scroller around it, or the
 * window's, as Photon's layout does, and the development log says so once.
 */
function arrange(view: TableElement, decide: boolean): void {
  const declaration = view.__eqTable;
  if (!declaration) return;
  const { node } = declaration;
  const width = fit(view, node, 'width', decide);
  const height = fit(view, node, 'height', decide);
  const reduceMotion =
    typeof matchMedia === 'function' && matchMedia('(prefers-reduced-motion: reduce)').matches;
  node.scroll.arrange(
    { width, height },
    node.columns.total,
    node.rows.total,
    declaration.pinnedAcross,
    declaration.pinnedDown,
    reduceMotion,
  );
}

/**
 * The extent of one axis, taking the visible extent of a scroller when the axis has none. Deciding
 * (`decide`, after a pass) lets go of the extent set before and measures the box as the page lays it
 * out; otherwise an axis set from a scroller follows that scroller, and one the page sizes is read.
 */
function fit(view: TableElement, node: TableViewportNode, axis: 'width' | 'height', decide: boolean): number {
  const declared = axis === 'width' ? node.width : node.height;
  const own = () => (axis === 'width' ? view.clientWidth : view.clientHeight);
  if (declared && (declared as { kind?: string }).kind === 'fixed') return own();
  const bit = axis === 'width' ? 1 : 2;
  const fitted = ((view.__eqTableFitted ?? 0) & bit) !== 0;
  if (decide || !fitted) {
    if (fitted) view.style.removeProperty(axis);
    view.__eqTableFitted = (view.__eqTableFitted ?? 0) & ~bit;
    const laid = own();
    if (laid > 0) return laid;
  }
  const scroller = scrollerAround(view, axis);
  view.__eqTableScroller = scroller;
  const visible = scroller
    ? axis === 'width'
      ? scroller.clientWidth
      : scroller.clientHeight
    : axis === 'width'
      ? window.innerWidth
      : window.innerHeight;
  const spelled = `${visible}px`;
  if (view.style.getPropertyValue(axis) !== spelled) view.style.setProperty(axis, spelled);
  view.__eqTableFitted = (view.__eqTableFitted ?? 0) | bit;
  if (((view.__eqTableLogged ?? 0) & bit) === 0) {
    view.__eqTableLogged = (view.__eqTableLogged ?? 0) | bit;
    const label = node.label ? ` (${node.label})` : '';
    logger.info(
      `A table viewport${label} was asked to fill its ${axis}, which had no bound; it takes the ` +
        `${visible} points the ${scroller ? 'scroller around it' : 'window'} shows, and scrolls its grid inside them.`,
    );
  }
  return visible;
}

/** The nearest ancestor that scrolls on an axis, or none. */
function scrollerAround(view: HTMLElement, axis: 'width' | 'height'): HTMLElement | null {
  for (let at = view.parentElement; at; at = at.parentElement) {
    const style = getComputedStyle(at);
    const overflow = axis === 'width' ? style.overflowX : style.overflowY;
    if (overflow === 'auto' || overflow === 'scroll') return at;
  }
  return null;
}

/** The bands' layers moved to the model's offset, the bands short of the scrollbars, the scrollbars
 * where the model puts them. */
function paint(view: TableElement): void {
  const declaration = view.__eqTable;
  if (!declaration) return;
  const { node, base } = declaration;
  const scroll = node.scroll;
  const across = scroll.offset.across - base.across;
  const down = scroll.offset.down - base.down;
  const viewport = scroll.viewport;
  const right = Math.max(0, view.clientWidth - viewport.width);
  const bottom = Math.max(0, view.clientHeight - viewport.height);
  const bands = view.children;
  for (let i = 0; i < 8 && i < bands.length; i++) {
    const band = bands[i] as HTMLElement;
    const which = i % 4;
    const movesAcross = which === 0 || which === 2;
    const movesDown = which === 0 || which === 1;
    if (movesAcross) band.style.right = `${right}px`;
    if (movesDown) band.style.bottom = `${bottom}px`;
    const layer = band.firstElementChild as HTMLElement | null;
    if (!layer) continue;
    const x = movesAcross ? -across : 0;
    const y = movesDown ? -down : 0;
    layer.style.transform = x === 0 && y === 0 ? '' : `translate(${x}px, ${y}px)`;
  }
  placeScrollbar(bands[8] as HTMLElement | undefined, scroll, 'vertical');
  placeScrollbar(bands[9] as HTMLElement | undefined, scroll, 'horizontal');
}

function placeScrollbar(track: HTMLElement | undefined, scroll: TableScrollModelLike, axis: string): void {
  if (!track) return;
  const strip = scroll.trackOf(axis);
  const knob = scroll.thumbOf(axis);
  if (!strip || !knob) {
    track.style.display = 'none';
    return;
  }
  track.style.display = 'block';
  track.style.left = `${strip.x}px`;
  track.style.top = `${strip.y}px`;
  track.style.width = `${strip.width}px`;
  track.style.height = `${strip.height}px`;
  const thumb = track.firstElementChild as HTMLElement | null;
  if (!thumb) return;
  // The thumb sits inside the strip, a little in from its edges, as Photon draws it.
  const inset = 2;
  thumb.style.left = `${knob.x - strip.x + inset}px`;
  thumb.style.top = `${knob.y - strip.y + inset}px`;
  thumb.style.width = `${Math.max(0, knob.width - 2 * inset)}px`;
  thumb.style.height = `${Math.max(0, knob.height - 2 * inset)}px`;
}

/** Tells the owner where the grid stands and how large the window is, each once per change. */
function report(view: TableElement): void {
  const declaration = view.__eqTable;
  if (!declaration) return;
  const { node } = declaration;
  const offset = node.scroll.offset;
  const viewport = node.scroll.viewport;
  const last = view.__eqTableReported;
  view.__eqTableReported = {
    across: offset.across,
    down: offset.down,
    width: viewport.width,
    height: viewport.height,
  };
  if (!last || last.width !== viewport.width || last.height !== viewport.height) {
    node.onViewportChanged?.(viewport);
  }
  if (!last || last.across !== offset.across || last.down !== offset.down) {
    node.onScrolled?.(offset);
  }
}

/** Keeps a glide or a fling moving, a frame at a time, while the model says it is. */
function animate(view: TableElement): void {
  if (view.__eqTableFrame !== undefined || typeof requestAnimationFrame !== 'function') return;
  const step = () => {
    view.__eqTableFrame = undefined;
    const declaration = view.__eqTable;
    if (!declaration || !view.isConnected) return;
    const moving = declaration.node.scroll.advance(performance.now());
    paint(view);
    report(view);
    if (moving) view.__eqTableFrame = requestAnimationFrame(step);
  };
  view.__eqTableFrame = requestAnimationFrame(step);
}

/** The wheel and the pointer, attached once: each reads the latest declaration off the element. */
function listen(view: TableElement): void {
  if (view.__eqTableListening) return;
  view.__eqTableListening = true;

  view.addEventListener(
    'wheel',
    (event: WheelEvent) => {
      const declaration = view.__eqTable;
      if (!declaration) return;
      const scroll = declaration.node.scroll;
      const unit =
        event.deltaMode === 1 ? WHEEL_LINE : event.deltaMode === 2 ? scroll.viewport.height : 1;
      const across = event.deltaX * unit;
      const down = event.deltaY * unit;
      // A wheel's notch arrives in lines, or as a large whole number of pixels; a trackpad's movement
      // arrives as the small, often fractional, pixels it moved. Only the notch glides.
      const stepped =
        event.deltaMode !== 0 ||
        (Number.isInteger(event.deltaY) && Math.abs(event.deltaY) >= 50 && event.deltaX === 0);
      const left = scroll.wheel(across, down, stepped, modifiersOf(event));
      // What the viewport could not take passes to the page: the event is cancelled only when the
      // model took something of it.
      if (left.across !== across || left.down !== down) event.preventDefault();
      paint(view);
      report(view);
      animate(view);
    },
    { passive: false },
  );

  const local = (event: PointerEvent) => {
    const box = view.getBoundingClientRect();
    return { x: event.clientX - box.left, y: event.clientY - box.top };
  };
  const kindOf = (event: PointerEvent) =>
    event.pointerType === 'touch' ? 'touch' : event.pointerType === 'pen' ? 'stylus' : 'mouse';

  // The press in flight. On a scrollbar it is the scroll's from the start; a finger on the cells is a
  // CANDIDATE, as on Photon: the press goes on to the cell under it, and the scroll takes it only
  // once it travels past the slop, so a tap on a cell is still a tap.
  let press: { id: number; start: { x: number; y: number }; kind: string; time: number; active: boolean } | null =
    null;

  const onScrollbar = (scroll: TableScrollModelLike, at: { x: number; y: number }) =>
    [scroll.trackOf('vertical'), scroll.trackOf('horizontal')].some(
      (track) => track !== null && at.x >= track.x && at.x < track.x + track.width && at.y >= track.y && at.y < track.y + track.height,
    );

  view.addEventListener('pointerdown', (event: PointerEvent) => {
    const declaration = view.__eqTable;
    if (!declaration) return;
    const scroll = declaration.node.scroll;
    const at = local(event);
    const kind = kindOf(event);
    if (onScrollbar(scroll, at)) {
      if (!scroll.pointer('down', at, kind, performance.now())) return;
      press = { id: event.pointerId, start: at, kind, time: performance.now(), active: true };
      capture(view, event.pointerId, 'take');
      event.preventDefault();
      paint(view);
      report(view);
      return;
    }
    if (kind !== 'mouse') press = { id: event.pointerId, start: at, kind, time: performance.now(), active: false };
  });

  view.addEventListener('pointermove', (event: PointerEvent) => {
    if (!press || press.id !== event.pointerId) return;
    const declaration = view.__eqTable;
    if (!declaration) return;
    const scroll = declaration.node.scroll;
    const at = local(event);
    if (!press.active) {
      if (Math.max(Math.abs(at.x - press.start.x), Math.abs(at.y - press.start.y)) <= PRESS_SLOP) return;
      if (!scroll.pointer('down', press.start, press.kind, press.time)) {
        press = null;
        return;
      }
      press.active = true;
      capture(view, event.pointerId, 'take');
      swallowNextClick();
    }
    scroll.pointer('move', at, press.kind, performance.now());
    event.preventDefault();
    paint(view);
    report(view);
  });

  const end = (phase: 'up' | 'cancel') => (event: PointerEvent) => {
    if (!press || press.id !== event.pointerId) return;
    const ended = press;
    press = null;
    if (!ended.active) return;
    const declaration = view.__eqTable;
    if (!declaration) return;
    declaration.node.scroll.pointer(phase, local(event), ended.kind, performance.now());
    capture(view, event.pointerId, 'release');
    paint(view);
    report(view);
    if (phase === 'up') animate(view);
  };
  view.addEventListener('pointerup', end('up'));
  view.addEventListener('pointercancel', end('cancel'));

  // The focus landing on a control in a cell out of view: the model brings it in, on the axes its band
  // moves along, before the browser would scroll a clipping box to it. Its cells past the window are
  // built, and Tab reaches them.
  view.addEventListener('focusin', (event: FocusEvent) => {
    const declaration = view.__eqTable;
    const target = event.target as Element | null;
    if (!declaration || !target) return;
    let band = -1;
    for (let i = 0; i < 4 && band < 0; i++) if (view.children[i]?.contains(target)) band = i;
    if (band < 0) return;
    const box = view.getBoundingClientRect();
    const at = target.getBoundingClientRect();
    const area = { x: at.left - box.left, y: at.top - box.top, width: at.width, height: at.height };
    if (declaration.node.scroll.reveal(area, band === 0 || band === 2, band === 0 || band === 1)) {
      paint(view);
      report(view);
    }
  });
  // A box the browser scrolled anyway (a focus it revealed itself, a find in the page) is put back:
  // the bands move by transform, and a clipping box scrolled under them misplaces every cell in it.
  view.addEventListener(
    'scroll',
    (event: Event) => {
      const scrolled = event.target as HTMLElement;
      if (scrolled.scrollTop !== 0) scrolled.scrollTop = 0;
      if (scrolled.scrollLeft !== 0) scrolled.scrollLeft = 0;
    },
    true,
  );
}

/** Takes or lets go of a pointer's capture. A pointer the browser no longer tracks (one already
 * cancelled, or a script's event, which has none) throws `NotFoundError`; the gesture goes on without
 * the capture rather than stopping half-way through its handler. */
function capture(view: HTMLElement, pointerId: number, action: 'take' | 'release'): void {
  try {
    if (action === 'take') view.setPointerCapture?.(pointerId);
    else view.releasePointerCapture?.(pointerId);
  } catch {
    // Nothing to capture: the pointer is gone.
  }
}

/** How far a finger travels before it is a pan rather than a press: the C# `Touch.PressCancelSlop`. */
const PRESS_SLOP = 12;

/** The click a browser sends where a pan ended was no click: it is stopped once, before anything
 * under the finger hears it. */
function swallowNextClick(): void {
  const stop = (event: Event) => {
    event.stopPropagation();
    event.preventDefault();
  };
  // Held before the timer: by the time it fires the page may be gone (deferred-cleanup.spec.ts).
  const target = window;
  target.addEventListener('click', stop, { capture: true, once: true });
  // A pan whose lift sends no click must not eat the next real one.
  setTimeout(() => target.removeEventListener('click', stop, { capture: true }), 400);
}

