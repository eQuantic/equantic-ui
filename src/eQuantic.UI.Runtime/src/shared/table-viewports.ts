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
 * where the model puts them, and the bands short of them. It glides a keyed mark a pass placed over
 * other cells. It tells the owner where the grid stands and how large the window is, each once per
 * change, and gives a viewport asked to fill an axis with no bound the visible extent of the scroller
 * around it.
 */

import { logger } from '../utils/logger';
import { modifiersOf } from './css-values';
import type { TableScrollModelLike, TableViewCellNode, TableViewportNode } from './nodes';

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
  /** When a pointer last went down on it: a focus that follows one is a click's, not a keyboard's. */
  __eqTablePointerAt?: number;
  /** Set while the Tab order leaves past the last cell: that focus is a step out, not one to reveal. */
  __eqTableLeaving?: boolean;
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
    glideMarks(view);
    report(view);
    // A drag the composing component holds past the edge scrolls on the clock, which a pass can start
    // without any event of this viewport's own; with nothing moving, the first frame ends it.
    animate(view);
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

/** A rectangle of the grid, in double as the grid's extents are. */
export interface GridRect {
  x: number;
  y: number;
  width: number;
  height: number;
}

/** The rectangle of the grid a cell covers, the rows and columns it spans held inside the grid (C# twin:
 * Photon's `MeasureVisitor.PlaceOf`). */
export function tableCellPlace(node: TableViewportNode, cell: TableViewCellNode): GridRect {
  const rows = node.rows;
  const columns = node.columns;
  const firstRow = Math.min(Math.max(cell.row, 0), rows.count);
  const firstColumn = Math.min(Math.max(cell.column, 0), columns.count);
  const endRow = Math.min(Math.max(cell.row + Math.max(1, cell.rows ?? 1), firstRow), rows.count);
  const endColumn = Math.min(Math.max(cell.column + Math.max(1, cell.columns ?? 1), firstColumn), columns.count);
  const x = columns.offsetOf(firstColumn);
  const y = rows.offsetOf(firstRow);
  return { x, y, width: columns.offsetOf(endColumn) - x, height: rows.offsetOf(endRow) - y };
}

/** A gliding mark's state on its element, which the reconciler keeps by the mark's key: the rectangle of
 * the grid the last pass placed it over and, while it glides, where it set out from and what carries it. */
interface GlidingMark extends HTMLElement {
  __eqGlide?: { at: GridRect; from?: GridRect; motion?: Animation };
}

/**
 * Glides each keyed mark a pass placed over other cells than the pass before (`TableViewCell.Glide`; C#
 * twin: Photon's table layout, `MeasureVisitor.Glided`). The lowering places a mark as it places a cell,
 * the scroll in its left and top, so a far row's mark and its cell round alike; a transition there would
 * glide the mark each time a scroll rebuilt the cells. The glide is the distance in the GRID from where the
 * mark stood, or from where a glide in flight had carried it, animated away together with its size: a
 * scroll never moves a mark in the grid, so it never glides one, and the size is laid out at each step, so
 * an outline keeps its thickness. A mark the last pass did not hold, and every mark under reduced motion,
 * stands where it is at once; a mark whose glide was turned off forgets where it stood, so turned on again
 * it starts where it stands.
 */
function glideMarks(view: TableElement): void {
  const declaration = view.__eqTable;
  if (!declaration) return;
  const { node } = declaration;
  const reduceMotion =
    typeof matchMedia === 'function' && matchMedia('(prefers-reduced-motion: reduce)').matches;
  const bands = view.children;
  for (let i = 4; i < 8 && i < bands.length; i++) {
    const layer = bands[i].firstElementChild;
    if (!layer) continue;
    for (const element of Array.from(layer.children)) {
      const mark = element as GlidingMark;
      const key = mark.getAttribute('data-eq-glide');
      const cell = key === null
        ? undefined
        : (node.cells ?? []).find((each) => each.mark && (each as { key?: string | null }).key === key);
      const glide = cell?.glide;
      if (!cell || !glide) {
        mark.__eqGlide?.motion?.cancel();
        mark.__eqGlide = undefined;
        continue;
      }
      const to = tableCellPlace(node, cell);
      const was = mark.__eqGlide;
      if (was && was.at.x === to.x && was.at.y === to.y && was.at.width === to.width && was.at.height === to.height)
        continue;
      const from = was?.motion?.playState === 'running' && was.from ? reached(was.from, was.at, was.motion) : was?.at;
      was?.motion?.cancel();
      mark.__eqGlide = { at: to };
      if (!from || reduceMotion || typeof mark.animate !== 'function') continue;
      const curve = glide.curve;
      const motion = mark.animate(
        [
          { translate: `${from.x - to.x}px ${from.y - to.y}px`, width: `${from.width}px`, height: `${from.height}px` },
          { translate: '0px 0px', width: `${to.width}px`, height: `${to.height}px` },
        ],
        { duration: glide.durationMs, easing: `cubic-bezier(${curve.x1}, ${curve.y1}, ${curve.x2}, ${curve.y2})` },
      );
      mark.__eqGlide = { at: to, from, motion };
    }
  }
}

/** Where a glide in flight from `from` to `to` has carried a mark: the motion's progress is its curve's. */
function reached(from: GridRect, to: GridRect, motion: Animation): GridRect {
  const progress = Number(motion.effect?.getComputedTiming().progress ?? 1);
  const at = (a: number, b: number) => a + (b - a) * progress;
  return { x: at(from.x, to.x), y: at(from.y, to.y), width: at(from.width, to.width), height: at(from.height, to.height) };
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
    view.__eqTablePointerAt = performance.now();
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
    // Handled: the release of a pan is no tap, and what composes the table reads it so (a spreadsheet's
    // surface tells its engine the press was taken away).
    event.preventDefault();
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
    if (!declaration || !target || view.__eqTableLeaving) return;
    // Shift+Tab from past the table lands on the last control the document holds in it; the last one
    // in reading order is where it belongs. A click's focus stays where the click put it.
    const from = event.relatedTarget as Node | null;
    const clicked = performance.now() - (view.__eqTablePointerAt ?? -Infinity) < 500;
    if (target !== view && from && !view.contains(from) && !clicked
      && (view.compareDocumentPosition(from) & Node.DOCUMENT_POSITION_FOLLOWING) !== 0) {
      const order = cellControls(view);
      const last = order[order.length - 1];
      if (last && last !== target) {
        last.focus();
        return;
      }
    }
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
  // On the viewport itself the keys scroll it; Tab walks its cells' controls row by row, across the
  // bands they are painted in, where the document's own order would visit them band by band.
  view.addEventListener('keydown', (event: KeyboardEvent) => {
    const declaration = view.__eqTable;
    if (!declaration) return;
    if (event.key === 'Tab') {
      tab(view, event);
      return;
    }
    if (event.target !== view || !declaration.node.scroll.key(event.key, modifiersOf(event))) return;
    event.preventDefault();
    paint(view);
    report(view);
    animate(view);
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

/** What Tab can land on. */
const TABBABLE = 'a[href], button, input, select, textarea, [tabindex]';

/** The controls in a viewport's cells in reading order: row by row, and in each cell in its own order.
 * The marks' bands are inert and hold none. */
function cellControls(view: HTMLElement): HTMLElement[] {
  const found: { element: HTMLElement; row: number; column: number; at: number }[] = [];
  for (let band = 0; band < 4; band++) {
    const box = view.children[band];
    if (!box) continue;
    for (const element of box.querySelectorAll<HTMLElement>(TABBABLE)) {
      if (element.tabIndex < 0 || (element as HTMLButtonElement).disabled) continue;
      const cell = element.closest('[data-eq-cell]')?.getAttribute('data-eq-cell');
      if (!cell) continue;
      const [row, column] = cell.split(':').map(Number);
      found.push({ element, row, column, at: found.length });
    }
  }
  found.sort((a, b) => a.row - b.row || a.column - b.column || a.at - b.at);
  return found.map((entry) => entry.element);
}

/** Tab inside a viewport: from the viewport to its first control, from control to control in reading
 * order, back to the viewport before the first, and out past the table after the last. */
function tab(view: TableElement, event: KeyboardEvent): void {
  const order = cellControls(view);
  if (order.length === 0) return;
  const active = document.activeElement;
  const at = active === view ? -1 : order.indexOf(active as HTMLElement);
  if (active !== view && at < 0) return;
  const back = event.shiftKey;
  if (back && at === -1) return;
  const next = back ? at - 1 : at + 1;
  // Before the first, the viewport itself, when it is a stop of its own.
  if (next === -1 && view.hasAttribute('tabindex')) {
    event.preventDefault();
    view.focus();
    return;
  }
  if (next >= 0 && next < order.length) {
    event.preventDefault();
    order[next].focus();
    return;
  }
  // Out of the table: from the control the document holds at that end here, the browser's own Tab
  // goes on past the table, forward from the last, back from the first.
  const inDocument = [...view.querySelectorAll<HTMLElement>(TABBABLE)].filter((element) => order.includes(element));
  const edge = next === -1 ? inDocument[0] : inDocument[inDocument.length - 1];
  if (!edge || edge === active) return;
  view.__eqTableLeaving = true;
  try {
    edge.focus({ preventScroll: true });
  } finally {
    view.__eqTableLeaving = false;
  }
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

