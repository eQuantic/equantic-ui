/**
 * The spreadsheet's pointer on the web, with real events through the DOM: a sheet surface around the
 * table viewport the spreadsheet composes, the surface taking a mouse's press with its capture and a
 * finger's as a candidate the table may take as a pan, the table's scrollbar keeping its own press,
 * and the pointer resting on the fill handle drawn as the engine answers. The C# twin of every move is
 * SpreadsheetViewportTests on Photon.
 */

import { beforeEach, describe, expect, it } from 'vitest';
import { lowerVisualNode, type LoweringContext } from './lowering';
import { photonTheme } from './design-system.generated';
import { commitTableViewports } from './table-viewports';
import { TableScroll } from './components/TableScroll';
import { LeadingSpans } from './components/LeadingSpans';
import { SheetController } from './components/SheetController';
import { SheetGrid } from './components/SheetGrid';
import { CellRef } from './components/CellRef';
import { SheetRange } from './components/SheetRange';
import { SheetSurface, TableViewCell, TableViewport, Text } from './vocabulary';
import { Spreadsheet } from './components/Spreadsheet';
import { SizeValue } from './value-types';
import { UNMEASURED_MARK } from '../dom/reconciler';
import fixture from './component-parity.fixture.json';
import type { HtmlNode } from '../core/types';

const context = { textPrimary: photonTheme.textPrimary } as LoweringContext;
const HEADER_WIDTH = 44;
const HEADER_HEIGHT = 26;

/** The lowered tree as DOM, as the reconciler writes it: attributes, children and the events. */
function toDom(node: HtmlNode | string): Node {
  if (typeof node === 'string') return document.createTextNode(node);
  const element = document.createElement(node.tag);
  for (const [name, value] of Object.entries(node.attributes ?? {})) element.setAttribute(name, String(value));
  for (const [name, handler] of Object.entries((node as { events?: Record<string, EventListener> }).events ?? {}))
    element.addEventListener(name, handler);
  for (const child of (node.children ?? []) as (HtmlNode | string)[]) {
    if (child === null || child === undefined) continue;
    if (typeof child === 'string' || 'tag' in child) element.appendChild(toDom(child));
  }
  return element;
}

interface Built {
  sheet: SheetController;
  scroll: TableScroll;
  surface: HTMLElement;
  view: HTMLElement;
}

/** What the spreadsheet composes: its engine handed the scroll the table keeps, the headers pinned. */
function build(): Built {
  const sheet = new SheetController(400, 8);
  const scroll = new TableScroll();
  sheet.grid = new SheetGrid(scroll, HEADER_WIDTH, HEADER_HEIGHT);
  const table = new TableViewport(new LeadingSpans(sheet.document.rowSpans, HEADER_HEIGHT),
    new LeadingSpans(sheet.document.columnSpans, HEADER_WIDTH), scroll, { pinnedRows: 1, pinnedColumns: 1, canRequestFocus: false });
  for (let row = 0; row < 24; row++)
    for (let column = 0; column < 9; column++) table.add(new TableViewCell(row, column, new Text(`${row}:${column}`)));
  const lowered = lowerVisualNode(new SheetSurface(table, sheet) as never, context);
  const surface = toDom(lowered) as HTMLElement;
  surface.focus = () => {};
  const view = surface.firstElementChild as HTMLElement;
  Object.defineProperty(view, 'clientWidth', { value: 900, configurable: true });
  Object.defineProperty(view, 'clientHeight', { value: 600, configurable: true });
  document.body.appendChild(surface);
  commitTableViewports();
  return { sheet, scroll, surface, view };
}

/** A cell's middle, in the surface's own coordinates (happy-dom's boxes stand at the page's corner). */
const middle = (row: number, col: number) => ({ x: HEADER_WIDTH + col * 96 + 48, y: HEADER_HEIGHT + row * 28 + 14 });

function pointer(on: Element, type: string, at: { x: number; y: number }, init: PointerEventInit = {}): PointerEvent {
  const event = new PointerEvent(type, {
    bubbles: true, cancelable: true, clientX: at.x, clientY: at.y, pointerId: 3, pointerType: 'mouse', ...init,
  });
  on.dispatchEvent(event);
  return event;
}

/** A mouse's press as a browser sends it: the pointerdown, then the mousedown with the click count. */
function mousePress(on: Element, at: { x: number; y: number }, detail = 1): void {
  pointer(on, 'pointerdown', at, { buttons: 1 });
  on.dispatchEvent(new MouseEvent('mousedown', { bubbles: true, cancelable: true, clientX: at.x, clientY: at.y, detail }));
}

/** The element a point lands on: the body's cell at the table's indices, which is what a browser hits. */
const cellElement = (view: HTMLElement) => view.children[0].firstElementChild!.firstElementChild as HTMLElement;

describe('the spreadsheet pointer on the web', () => {
  beforeEach(() => {
    document.body.innerHTML = '';
    window.matchMedia = ((query: string) => ({ matches: query.includes('reduce'), media: query })) as never;
  });

  it("selects a range with a mouse's drag, captured until it lifts", () => {
    const { sheet, view } = build();
    const on = cellElement(view);

    mousePress(on, middle(1, 1));
    pointer(on, 'pointermove', middle(3, 3), { buttons: 1 });
    pointer(on, 'pointerup', middle(3, 3));

    expect(sheet.selection).toEqual(new SheetRange(new CellRef(1, 1), new CellRef(3, 3)));
  });

  it("selects the cell a finger tapped, when it lifts", () => {
    const { sheet, view } = build();
    const on = cellElement(view);

    pointer(on, 'pointerdown', middle(4, 3), { pointerType: 'touch' });
    expect(sheet.activeCell).toEqual(new CellRef(0, 0));
    pointer(on, 'pointerup', middle(4, 3), { pointerType: 'touch' });

    expect(sheet.selection).toEqual(new SheetRange(new CellRef(4, 3)));
  });

  it("scrolls the grid with a finger's pan, and selects nothing", () => {
    const { sheet, scroll, view } = build();
    sheet.selection = new SheetRange(new CellRef(1, 1));
    const on = cellElement(view);
    const start = middle(8, 3);

    pointer(on, 'pointerdown', start, { pointerType: 'touch' });
    for (let step = 1; step <= 10; step++)
      pointer(on, 'pointermove', { x: start.x, y: start.y - 20 * step }, { pointerType: 'touch' });
    const lifted = pointer(on, 'pointerup', { x: start.x, y: start.y - 200 }, { pointerType: 'touch' });

    expect(scroll.offset.down).toBeGreaterThan(0);
    expect(lifted.defaultPrevented).toBe(true);
    expect(sheet.selection).toEqual(new SheetRange(new CellRef(1, 1)));
  });

  it("leaves a press on the table's scrollbar to the table", () => {
    const { sheet, scroll, view } = build();
    sheet.selection = new SheetRange(new CellRef(1, 1));
    const track = scroll.trackOf('vertical')!;

    mousePress(view, { x: track.x + track.width / 2, y: track.y + track.height - 10 });
    pointer(view, 'pointerup', { x: track.x + track.width / 2, y: track.y + track.height - 10 });

    expect(sheet.selection).toEqual(new SheetRange(new CellRef(1, 1)));
    expect(scroll.offset.down).toBeGreaterThan(0);
  });

  it('draws the pointer resting on the fill handle as the engine answers', () => {
    const { sheet, surface, view } = build();
    sheet.selection = new SheetRange(new CellRef(1, 1));
    const on = cellElement(view);

    pointer(on, 'pointermove', { x: HEADER_WIDTH + 2 * 96 - 2, y: HEADER_HEIGHT + 2 * 28 - 2 }, { buttons: 0 });
    expect(surface.style.cursor).toBe('crosshair');
    pointer(on, 'pointermove', middle(1, 1), { buttons: 0 });
    expect(surface.style.cursor).toBe('');
  });

  it("draws the server's sheet again at hydration, as one it could not measure, and the client's takes the press", async () => {
    // The markup the C# server writes for the parity case `spreadsheet`, with the note it adds to a
    // subtree laid out on text it could not measure (the row numbers' width).
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
    const served = write((fixture as unknown as Record<string, Written[]>)['spreadsheet'][0]) as HTMLElement;
    served.setAttribute(UNMEASURED_MARK, '');
    container.appendChild(served);
    document.body.appendChild(container);

    const sheet = new SheetController(30, 6);
    new Spreadsheet(sheet, { width: SizeValue.fill, height: SizeValue.fixed(240) }).hydrate(container);
    await Promise.resolve();

    const drawn = container.firstElementChild as HTMLElement;
    expect(drawn).not.toBe(served);
    expect(drawn.hasAttribute(UNMEASURED_MARK)).toBe(false);
    expect(container.querySelectorAll('[role="grid"]')).toHaveLength(1);
    const view = container.querySelector('[data-eq-table]') as HTMLElement;
    expect(view).not.toBeNull();

    mousePress(cellElement(view), middle(2, 1));
    pointer(cellElement(view), 'pointerup', middle(2, 1));
    expect(sheet.activeCell).toEqual(new CellRef(2, 1));
  });
});
