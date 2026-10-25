/**
 * The table viewport's WEB half, end to end: the lowered boxes, and the after-pass half that measures
 * the window, hands the wheel and a finger to the SAME transpiled scroll model Photon's host drives,
 * moves each band's layer by transform, and reports what it found. Real DOM events, a real model.
 */

import { beforeEach, describe, expect, it, vi } from 'vitest';
import { lowerVisualNode, type LoweringContext } from './lowering';
import { effectiveStyle } from './style-atomizer';
import { photonTheme } from './design-system.generated';
import { commitTableViewports, scheduleTableViewportCommit } from './table-viewports';
import { TableScroll } from './components/TableScroll';
import { UniformTableSpans } from './components/UniformTableSpans';
import { Point, TableRange, TableScrollRequest } from './value-types';
import { Size, TableOffset } from './runtime-exports';
import { Box, BoxStyle, Pressable, TableViewCell, TableViewport, Text } from './vocabulary';
import { TableView } from './components/TableView';
import fixture from './component-parity.fixture.json';
import type { HtmlNode } from '../core/types';

const context = { textPrimary: photonTheme.textPrimary } as LoweringContext;

interface Built {
  scroll: TableScroll;
  view: HTMLElement;
  lowered: HtmlNode;
  scrolled: { across: number; down: number }[];
  viewports: { width: number; height: number }[];
}

/** The lowered tree as DOM, attributes and children, as the reconciler would write it. */
function toDom(node: HtmlNode | string): Node {
  if (typeof node === 'string') return document.createTextNode(node);
  const element = document.createElement(node.tag);
  for (const [name, value] of Object.entries(node.attributes ?? {})) element.setAttribute(name, String(value));
  for (const child of (node.children ?? []) as (HtmlNode | string)[]) {
    if (child === null || child === undefined) continue;
    if (typeof child === 'string' || 'tag' in child) element.appendChild(toDom(child));
  }
  return element;
}

function build(options: {
  rows?: number;
  width?: number;
  height?: number;
  request?: TableScrollRequest;
  parent?: HTMLElement;
} = {}): Built {
  const scroll = new TableScroll();
  const scrolled: { across: number; down: number }[] = [];
  const viewports: { width: number; height: number }[] = [];
  const node = new TableViewport(new UniformTableSpans(options.rows ?? 1_048_576, 28), new UniformTableSpans(16_384, 96), scroll, {
    pinnedRows: 1,
    pinnedColumns: 1,
    request: options.request ?? null,
    onScrolled: (offset: { across: number; down: number }) => scrolled.push({ across: offset.across, down: offset.down }),
    onViewportChanged: (size: { width: number; height: number }) => viewports.push({ width: size.width, height: size.height }),
  });
  for (let row = 0; row < 4; row++)
    for (let column = 0; column < 4; column++) node.add(new TableViewCell(row, column, new Text(`${row}:${column}`)));
  const lowered = lowerVisualNode(node as never, context);
  const view = toDom(lowered) as HTMLElement;
  Object.defineProperty(view, 'clientWidth', { value: options.width ?? 900, configurable: true });
  Object.defineProperty(view, 'clientHeight', { value: options.height ?? 600, configurable: true });
  (options.parent ?? document.body).appendChild(view);
  commitTableViewports();
  return { scroll, view, lowered, scrolled, viewports };
}

const layer = (view: HTMLElement, band: number) => view.children[band].firstElementChild as HTMLElement;

function wheel(view: HTMLElement, init: WheelEventInit): WheelEvent {
  const event = new WheelEvent('wheel', { bubbles: true, cancelable: true, ...init });
  // happy-dom's WheelEvent drops the modifiers its init carries (measured: shiftKey is undefined while
  // a MouseEvent's is true); a browser's carries them, so the spec puts it where the browser would.
  Object.defineProperty(event, 'shiftKey', { value: init.shiftKey ?? false });
  view.dispatchEvent(event);
  return event;
}

function pointer(view: HTMLElement, type: string, x: number, y: number, pointerType = 'touch'): PointerEvent {
  const event = new PointerEvent(type, { bubbles: true, cancelable: true, clientX: x, clientY: y, pointerId: 7, pointerType });
  view.dispatchEvent(event);
  return event;
}

describe('TableViewport on the web', () => {
  beforeEach(() => {
    document.body.innerHTML = '';
    // Reduced motion, so a wheel's notch lands at once instead of gliding over frames the spec does
    // not run: the glide itself is the model's, pinned in table-scroll.spec.ts.
    window.matchMedia = ((query: string) => ({ matches: query.includes('reduce'), media: query })) as never;
  });

  /** A keyed mark over cell (2, 2) of a grid scrolled `across`, lowered with `glide`: its wrapper and style. */
  const cursorLowered = (across: number, glide: { durationMs: number; curve: { x1: number; y1: number; x2: number; y2: number } } | null) => {
    const scroll = new TableScroll();
    const rows = new UniformTableSpans(200, 28);
    const columns = new UniformTableSpans(40, 96);
    scroll.arrange(new Size(900, 600), columns.total, rows.total, 96, 28, true);
    if (across) scroll.apply(new TableScrollRequest(1, { offset: new TableOffset(across, 0) }), rows, columns, 1, 1);
    const node = new TableViewport(rows, columns, scroll, { pinnedRows: 1, pinnedColumns: 1 });
    const cursor = new TableViewCell(2, 2, new Box(new BoxStyle({})), true);
    cursor.key = 'cursor';
    cursor.glide = glide;
    node.add(cursor);
    const lowered = lowerVisualNode(node as never, context);
    // The bands' marks come after the four bands of cells; the cells' band is the first of them.
    const wrapper = (lowered.children[4] as HtmlNode).children[0].children[0] as HtmlNode;
    return { wrapper, style: effectiveStyle(wrapper) };
  };
  const cursorGlide = { durationMs: 200, curve: { x1: 0.2, y1: 0, x2: 0, y2: 1 } };

  it('glides a keyed mark where the cells put it, its scroll a transform of its own (#984)', () => {
    const glided = (across: number) => cursorLowered(across, cursorGlide);

    const still = glided(0);
    expect(still.style).toContain('left: 96px');
    expect(still.style).toContain('transition: left 200ms cubic-bezier(0.2, 0, 0, 1), top 200ms');
    expect(still.wrapper.attributes['class']).toContain('eq-glide');
    expect(still.style).not.toContain('transform');

    // Scrolled a column across: the mark stands where the cells put it, and the scroll is its transform.
    const scrolled = glided(96);
    expect(scrolled.style).toContain('left: 96px');
    expect(scrolled.style).toContain('transform: translate(-96px, 0)');
  });

  it('places a mark alike with a glide and without, so a glide turned on in a scrolled grid moves nothing', () => {
    // Review of #1102: a mark with no glide took the scroll in its left and one with a glide in a transform,
    // so turning the glide on in a grid scrolled a column across glided the mark from a column away.
    const off = cursorLowered(96, null);
    const on = cursorLowered(96, cursorGlide);
    expect(off.style).toContain('left: 96px');
    expect(off.style).toContain('transform: translate(-96px, 0)');
    expect(off.style).not.toContain('transition');
    expect(off.wrapper.attributes['class'] ?? '').not.toContain('eq-glide');
    expect(on.style).toContain('left: 96px');
    expect(on.style).toContain('transform: translate(-96px, 0)');
    expect(on.style).toContain('transition: left 200ms');
  });

  it('is a group of four bands, four bands of marks and two scrollbars, taking every gesture itself', () => {
    const { lowered, view } = build();
    expect((lowered as { attributes: Record<string, string> }).attributes['role']).toBe('group');
    expect(effectiveStyle(lowered)).toContain('touch-action: none');
    expect(effectiveStyle(lowered)).toContain('overflow: hidden');
    expect(view.children).toHaveLength(10);
    // The body's first cell sits at the band's corner; a pinned row cell keeps its own top.
    const body = lowered.children[0] as HtmlNode;
    const bodyCell = ((body.children[0] as HtmlNode).children[0] as HtmlNode);
    expect(effectiveStyle(bodyCell)).toContain('left: 0');
    expect(effectiveStyle(bodyCell)).toContain('width: 96px');
  });

  it('arranges the window, reports it once, and shows both scrollbars', () => {
    const { scroll, view, viewports, scrolled } = build();
    expect([scroll.viewport.width, scroll.viewport.height]).toEqual([888, 588]);
    expect(viewports).toEqual([{ width: 888, height: 588 }]);
    expect(scrolled).toEqual([{ across: 0, down: 0 }]);
    expect((view.children[8] as HTMLElement).style.display).toBe('block');
    expect((view.children[9] as HTMLElement).style.display).toBe('block');

    commitTableViewports();
    expect(viewports).toHaveLength(1);
  });

  it('takes the wheel, moving the cells by transform, and Shift turns it across', () => {
    const { scroll, view, scrolled } = build();
    const down = wheel(view, { deltaY: 84 });
    expect(down.defaultPrevented).toBe(true);
    expect([scroll.offset.across, scroll.offset.down]).toEqual([0, 84]);
    expect(layer(view, 0).style.transform).toBe('translate(0px, -84px)');
    expect(layer(view, 2).style.transform).toBe('');
    expect(layer(view, 1).style.transform).toBe('translate(0px, -84px)');

    wheel(view, { deltaY: 48, shiftKey: true });
    expect([scroll.offset.across, scroll.offset.down]).toEqual([48, 84]);
    expect(layer(view, 2).style.transform).toBe('translate(-48px, 0px)');
    expect(scrolled[scrolled.length - 1]).toEqual({ across: 48, down: 84 });
  });

  it('lets the page have what it cannot take at the end of the grid', () => {
    const { scroll, view } = build({ rows: 30 });
    const end = 30 * 28 - scroll.viewport.height;
    wheel(view, { deltaY: end });
    const past = wheel(view, { deltaY: 100 });
    expect(scroll.offset.down).toBe(end);
    expect(past.defaultPrevented).toBe(false);
  });

  it('pans with a finger past the slop, and swallows the click its lift sends', () => {
    const { scroll, view } = build();
    const pressed = vi.fn();
    view.addEventListener('click', pressed);

    pointer(view, 'pointerdown', 400, 400);
    pointer(view, 'pointermove', 400, 395);
    expect(scroll.offset.down).toBe(0);
    pointer(view, 'pointermove', 400, 340);
    pointer(view, 'pointerup', 400, 340);
    expect(scroll.offset.down).toBe(60);

    view.dispatchEvent(new MouseEvent('click', { bubbles: true }));
    expect(pressed).not.toHaveBeenCalled();
  });

  it('leaves a tap to the cell under it', () => {
    const { scroll, view } = build();
    const pressed = vi.fn();
    view.addEventListener('click', pressed);

    pointer(view, 'pointerdown', 400, 400);
    pointer(view, 'pointerup', 400, 400);
    view.dispatchEvent(new MouseEvent('click', { bubbles: true }));

    expect(pressed).toHaveBeenCalledTimes(1);
    expect(scroll.offset.down).toBe(0);
  });

  it('drags the vertical thumb to the end of the grid', () => {
    const { scroll, view } = build();
    const thumb = scroll.thumbOf('vertical')!;
    pointer(view, 'pointerdown', thumb.x + 4, thumb.y + 4, 'mouse');
    pointer(view, 'pointermove', thumb.x + 4, 5000, 'mouse');
    pointer(view, 'pointerup', thumb.x + 4, 5000, 'mouse');
    expect(scroll.offset.down + scroll.viewport.height).toBe(1_048_576 * 28);
  });

  it('reveals a requested row, once per version', () => {
    const { scroll } = build({ request: new TableScrollRequest(1, { reveal: new TableRange(40, 3) }) });
    expect(scroll.offset.down + scroll.viewport.height).toBe(41 * 28);
  });

  it('takes the visible height of the scroller around it when its own came out empty', () => {
    const page = document.createElement('div');
    page.style.overflowY = 'auto';
    Object.defineProperty(page, 'clientHeight', { value: 844, configurable: true });
    document.body.appendChild(page);

    const { view, scroll } = build({ height: 0, parent: page });

    expect(view.style.height).toBe('844px');
    expect(scroll.viewport.height).toBeGreaterThan(0);
  });

  it("adopts the server's boxes at hydration, then scrolls them", async () => {
    // The markup the C# server writes for the parity case `table-view`, which the parity spec pins
    // against this side's lowering, hydrated by the same component.
    interface Written {
      tag: string;
      text?: string;
      attrs: Record<string, string>;
      children: Written[];
    }
    const write = (node: Written): Node => {
      if (node.tag === '#text') return document.createTextNode(node.text ?? '');
      const element = document.createElement(node.tag);
      for (const [name, value] of Object.entries(node.attrs)) element.setAttribute(name, value);
      for (const child of node.children) element.appendChild(write(child));
      return element;
    };
    const container = document.createElement('div');
    container.appendChild(write((fixture as unknown as Record<string, Written[]>)['table-view'][0]));
    document.body.appendChild(container);
    const view = container.firstElementChild as HTMLElement;
    Object.defineProperty(view, 'clientWidth', { value: 300, configurable: true });
    Object.defineProperty(view, 'clientHeight', { value: 120, configurable: true });
    const server = [...view.querySelectorAll('span')].find((span) => span.textContent === '2:2')!;
    expect(view.hasAttribute('data-eq-table')).toBe(false);

    const scrolled: number[] = [];
    const table = new TableView(
      new UniformTableSpans(6, 28),
      new UniformTableSpans(4, 96),
      (row: number, column: number) => new Text(`${row}:${column}`, 'caption', photonTheme.textPrimary),
      {
        pinnedRows: 1,
        pinnedColumns: 1,
        label: 'grid',
        onScrolled: (offset: { down: number }) => scrolled.push(offset.down),
        marks: [
          new TableViewCell(0, 0, new Box(new BoxStyle({ borderWidth: 2, borderColor: photonTheme.focusRing })), true, {
            rows: 2,
            columns: 2,
          }),
        ],
      },
    );
    table.hydrate(container);
    await Promise.resolve();

    // Adopted, not drawn again: the server's cell is the one in the document, and the after-pass
    // found the element by the marker hydration gave it.
    expect(view.isConnected).toBe(true);
    expect(view.getAttribute('data-eq-table')).not.toBeNull();
    expect(server.isConnected).toBe(true);
    expect(scrolled).toEqual([0]);

    wheel(view, { deltaY: 40 });
    expect(scrolled[scrolled.length - 1]).toBe(40);
    expect(layer(view, 0).style.transform).toBe('translate(0px, -40px)');
    expect(server.isConnected).toBe(true);
  });

  it('lets go of the observer of a table a pass removed, though the pass declared no other', async () => {
    const observers: { disconnected: boolean }[] = [];
    const original = globalThis.ResizeObserver;
    globalThis.ResizeObserver = class {
      readonly record = { disconnected: false };
      constructor() {
        observers.push(this.record);
      }
      observe() {}
      disconnect() {
        this.record.disconnected = true;
      }
    } as never;
    try {
      const { view } = build();
      expect(observers).toHaveLength(1);

      // A rerender that dropped the table: nothing is declared, and the commit still sweeps.
      view.remove();
      scheduleTableViewportCommit();
      await Promise.resolve();

      expect(observers[0].disconnected).toBe(true);
    } finally {
      globalThis.ResizeObserver = original;
    }
  });

  it('brings a focused control in a cell past the window into view, and undoes a box the browser scrolled', () => {
    const { scroll, view } = build();
    const body = view.children[0] as HTMLElement;
    const target = body.querySelector('span') as HTMLElement;
    // Laid below the 588 points the cells scroll in.
    target.getBoundingClientRect = () => ({ left: 200, top: 700, width: 96, height: 28, right: 296, bottom: 728, x: 200, y: 700, toJSON: () => ({}) }) as DOMRect;

    target.dispatchEvent(new FocusEvent('focusin', { bubbles: true }));

    expect([scroll.offset.across, scroll.offset.down]).toEqual([0, 140]);
    expect(layer(view, 0).style.transform).toBe('translate(0px, -140px)');

    body.scrollTop = 40;
    body.dispatchEvent(new Event('scroll'));
    expect(body.scrollTop).toBe(0);
  });

  it('is a stop of the Tab order, and the keys it takes scroll it', () => {
    const { scroll, view } = build();
    expect(view.getAttribute('tabindex')).toBe('0');
    view.focus();

    const key = (name: string, init: KeyboardEventInit = {}) => {
      const event = new KeyboardEvent('keydown', { key: name, bubbles: true, cancelable: true, ...init });
      view.dispatchEvent(event);
      return event;
    };
    expect(key('PageDown').defaultPrevented).toBe(true);
    expect(scroll.offset.down).toBe(588 - 28);
    key('End', { metaKey: true });
    expect(scroll.offset.down + scroll.viewport.height).toBe(1_048_576 * 28);
    expect(scroll.offset.across + scroll.viewport.width).toBe(16_384 * 96);
    key('Home');
    expect(scroll.offset.down).toBe(0);
    expect(key('ArrowUp').defaultPrevented).toBe(false);
  });

  it("walks its cells' controls row by row across the bands they are painted in", () => {
    const node = new TableViewport(new UniformTableSpans(2, 28), new UniformTableSpans(2, 96), new TableScroll(), {
      pinnedRows: 1,
      pinnedColumns: 1,
    });
    for (let row = 0; row < 2; row++)
      for (let column = 0; column < 2; column++)
        node.add(new TableViewCell(row, column, new Pressable(new Text(`${row}:${column}`), () => {}, { label: `${row}:${column}` })));
    const view = toDom(lowerVisualNode(node as never, context)) as HTMLElement;
    Object.defineProperty(view, 'clientWidth', { value: 400, configurable: true });
    Object.defineProperty(view, 'clientHeight', { value: 200, configurable: true });
    document.body.appendChild(view);
    commitTableViewports();

    const tab = (shiftKey = false) => {
      const event = new KeyboardEvent('keydown', { key: 'Tab', shiftKey, bubbles: true, cancelable: true });
      (document.activeElement ?? view).dispatchEvent(event);
      return event;
    };
    const cellOf = () => (document.activeElement as HTMLElement).closest('[data-eq-cell]')?.getAttribute('data-eq-cell');

    view.focus();
    const visited: (string | null | undefined)[] = [];
    for (let i = 0; i < 4; i++) {
      expect(tab().defaultPrevented).toBe(true);
      visited.push(cellOf());
    }
    // Painted body first, read from the corner.
    expect(visited).toEqual(['0:0', '0:1', '1:0', '1:1']);

    tab(true);
    expect(cellOf()).toBe('1:0');
    tab(true);
    tab(true);
    tab(true);
    expect(document.activeElement).toBe(view);

    // Past the last, the browser's own Tab goes on from the last control the document holds here.
    view.focus();
    for (let i = 0; i < 4; i++) tab();
    const leaving = tab();
    expect(leaving.defaultPrevented).toBe(false);
    expect(cellOf()).toBe('0:0');
  });

  it('leaves the keyboard to what composes it when it declines the focus', () => {
    const node = new TableViewport(new UniformTableSpans(2, 28), new UniformTableSpans(2, 96), new TableScroll(), {
      pinnedRows: 1,
      pinnedColumns: 1,
      canRequestFocus: false,
    });
    for (let row = 0; row < 2; row++)
      for (let column = 0; column < 2; column++)
        node.add(new TableViewCell(row, column, new Pressable(new Text(`${row}:${column}`), () => {}, { label: `${row}:${column}` })));
    const view = toDom(lowerVisualNode(node as never, context)) as HTMLElement;
    Object.defineProperty(view, 'clientWidth', { value: 400, configurable: true });
    Object.defineProperty(view, 'clientHeight', { value: 200, configurable: true });
    document.body.appendChild(view);
    commitTableViewports();

    expect(view.hasAttribute('tabindex')).toBe(false);
    const tab = (shiftKey = false) => {
      const event = new KeyboardEvent('keydown', { key: 'Tab', shiftKey, bubbles: true, cancelable: true });
      (document.activeElement ?? view).dispatchEvent(event);
      return event;
    };
    const cellOf = () => (document.activeElement as HTMLElement).closest('[data-eq-cell]')?.getAttribute('data-eq-cell');

    // Into the cells from the corner, still in reading order; back past the first, out of the table.
    (view.querySelector('[data-eq-cell="0:0"] [tabindex], [data-eq-cell="0:0"] button') as HTMLElement).focus();
    expect(tab().defaultPrevented).toBe(true);
    expect(cellOf()).toBe('0:1');
    tab(true);
    expect(cellOf()).toBe('0:0');
    expect(tab(true).defaultPrevented).toBe(false);
  });

  it('keeps scrolling while a drag the composing component holds is past the cells', async () => {
    const { scroll, view } = build();
    scroll.autoScroll(new Point(400, scroll.viewport.height + 60));
    // A pass, as the composing component's rebuild brings, starts the clock.
    commitTableViewports();
    await new Promise((resolve) => setTimeout(resolve, 120));

    expect(scroll.offset.down).toBeGreaterThan(0);
    expect(layer(view, 0).style.transform).toContain(`-${scroll.offset.down}px`);
    scroll.autoScroll(null);
  });
});
