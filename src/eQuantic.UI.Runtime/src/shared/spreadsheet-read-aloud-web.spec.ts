/**
 * The spreadsheet read aloud on the web (#1081): a real Spreadsheet mounted through the runtime, its table
 * a grid named after the sheet, its headers heading their columns and rows, the ranges' cells selected and
 * the active cell current, and the hidden status inside the surface saying each move and each growing range.
 * The C# twins are SurfaceSsrTests and SpreadsheetComponentTests; the parity fixture pins the two lowerings.
 */

import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { SheetController } from './components/SheetController';
import { CellRef } from './components/CellRef';
import { SheetRange } from './components/SheetRange';
import { Spreadsheet } from './components/Spreadsheet';
import { SizeValue } from './value-types';

/** The frames the runtime asks for, run when the spec says. */
const frames: FrameRequestCallback[] = [];
const runFrames = () => {
  while (frames.length > 0) frames.shift()!(performance.now());
};

async function settle(): Promise<void> {
  for (let i = 0; i < 3; i++) {
    await Promise.resolve();
    runFrames();
  }
}

describe('a spreadsheet read aloud on the web (#1081)', () => {
  const requestFrame = window.requestAnimationFrame;

  beforeEach(() => {
    document.body.innerHTML = '';
    frames.length = 0;
    window.requestAnimationFrame = ((callback: FrameRequestCallback) => frames.push(callback)) as never;
  });

  afterEach(() => {
    window.requestAnimationFrame = requestFrame;
  });

  /** A sheet of 50 rows by 8 columns with 14,02 in B3, its active cell B2, mounted and settled. */
  async function mount(selection = new SheetRange(new CellRef(1, 1))) {
    const sheet = new SheetController(50, 8);
    sheet.document.setCell(new CellRef(2, 1), '14,02');
    sheet.selection = selection;
    const host = document.createElement('div');
    document.body.append(host);
    new Spreadsheet(sheet, { width: SizeValue.fill, height: SizeValue.fill }).mount(host);
    await settle();
    const grid = host.querySelector('[role="grid"]') as HTMLElement;
    const textarea = host.querySelector('.eq-sheet-surface textarea.eq-code-input') as HTMLTextAreaElement;
    const said = () => host.querySelector('.eq-announcement')?.textContent ?? '';
    return { sheet, host, grid, textarea, said };
  }

  /** The element of the table's cell at a row and a column of the sheet, one past its headers. */
  const cellOf = (host: HTMLElement, row: number, column: number) =>
    host.querySelector(`[data-eq-cell="${row + 1}:${column + 1}"]`) as HTMLElement;

  const press = async (textarea: HTMLTextAreaElement, name: string, init: KeyboardEventInit = {}) => {
    textarea.dispatchEvent(new KeyboardEvent('keydown', { key: name, bubbles: true, cancelable: true, ...init }));
    await settle();
  };

  it('reads as a grid named after the sheet, its headers heading and its cells in place', async () => {
    const { host, grid } = await mount();
    expect(grid).not.toBeNull();
    expect(grid.getAttribute('aria-label')).toBe('Spreadsheet');
    expect(grid.getAttribute('aria-rowcount')).toBe('51');
    expect(grid.getAttribute('aria-colcount')).toBe('9');
    expect(host.querySelector('.eq-sheet-surface')!.getAttribute('role')).toBeNull();

    const columnB = host.querySelector('[data-eq-cell="0:2"]') as HTMLElement;
    expect(columnB.getAttribute('role')).toBe('columnheader');
    expect(columnB.textContent).toBe('B');
    const row3 = host.querySelector('[data-eq-cell="3:0"]') as HTMLElement;
    expect(row3.getAttribute('role')).toBe('rowheader');
    expect(row3.textContent).toBe('3');
    const b3 = cellOf(host, 2, 1);
    expect(b3.getAttribute('role')).toBe('gridcell');
    expect(b3.textContent).toBe('14,02');
    expect(b3.getAttribute('aria-colindex')).toBe('3');
    const row = host.querySelector(`[aria-owns~="${b3.id}"]`) as HTMLElement;
    expect(row.getAttribute('role')).toBe('row');
    expect(row.getAttribute('aria-rowindex')).toBe('4');
    // The corner heads the row numbers, empty, and keeps its place: every row owns its cells in the order
    // the headers' row owns its headers, which is how a browser places them.
    const corner = host.querySelector('[data-eq-cell="0:0"]') as HTMLElement;
    expect(corner.getAttribute('role')).toBe('columnheader');
    expect(corner.getAttribute('aria-hidden')).toBeNull();
    const columnsOf = (owner: Element) =>
      owner.getAttribute('aria-owns')!.split(' ').map((id) => host.querySelector(`[id="${id}"]`)!.getAttribute('aria-colindex'));
    const rows = [...host.querySelectorAll('[role="row"]')];
    for (const each of rows) expect(columnsOf(each)).toEqual(columnsOf(rows[0]));
  });

  it('marks the cells of the range selected and the active cell current', async () => {
    const { host } = await mount(new SheetRange(new CellRef(1, 1), new CellRef(3, 1)));
    for (const row of [1, 2, 3]) expect(cellOf(host, row, 1).getAttribute('aria-selected')).toBe('true');
    expect(cellOf(host, 4, 1).getAttribute('aria-selected')).toBe('false');
    expect(cellOf(host, 1, 1).getAttribute('aria-current')).toBe('true');
    expect(host.querySelectorAll('[aria-current="true"]')).toHaveLength(1);
  });

  it("answers a reader's activation through the page: a click on a cell, then on a header", async () => {
    // Review of #1081 by Edgar: a click on a column's header threw in the browser, because the page hands
    // a click handler no event (C# declares OnClick as an Action) and the sheet's read it. The spec above
    // called the handler itself, with an event, and never went through the page.
    const { sheet, host, said } = await mount();
    const surface = host.querySelector('.eq-sheet-surface') as HTMLElement;
    surface.getBoundingClientRect = () => ({ left: 0, top: 0, width: 900, height: 600 }) as DOMRect;
    const lead = Spreadsheet.headerWidth;
    const top = Spreadsheet.headerHeight;
    /** A click a reader sends on the table's cell at a row and a column, drawn where the grid has it. */
    const activate = async (row: number, column: number, x: number, y: number, width: number, height: number) => {
      const cell = host.querySelector(`[data-eq-cell="${row}:${column}"]`) as HTMLElement;
      cell.getBoundingClientRect = () => ({ left: x, top: y, width, height }) as DOMRect;
      cell.dispatchEvent(new MouseEvent('click', { bubbles: true, cancelable: true }));
      await settle();
    };

    await activate(6, 4, lead + 3 * 96, top + 5 * 28, 96, 28);
    expect(sheet.activeCell).toEqual(new CellRef(5, 3));
    await activate(0, 3, lead + 2 * 96, 0, 96, top);
    expect(sheet.ranges).toEqual([new SheetRange(new CellRef(0, 2), new CellRef(sheet.document.rows - 1, 2))]);
    expect(said(), 'a band a header selects is said as the range').toBe('C1:C50, 50 rows by 1 column');
  });

  it('says each move and each growing range in a polite status', async () => {
    const { textarea, said, host } = await mount();
    const status = host.querySelector('.eq-announcement') as HTMLElement;
    expect(status.getAttribute('role')).toBe('status');
    expect(status.getAttribute('aria-live')).toBe('polite');

    await press(textarea, 'ArrowDown');
    expect(said()).toBe('B3, 14,02');
    await press(textarea, 'ArrowRight');
    expect(said()).toBe('C3, blank');
    await press(textarea, 'ArrowRight', { shiftKey: true });
    await press(textarea, 'ArrowDown', { shiftKey: true });
    await press(textarea, 'ArrowDown', { shiftKey: true });
    expect(said()).toBe('C3:D5, 3 rows by 2 columns');
    await press(textarea, 'ArrowLeft');
    expect(said()).toBe('B3, 14,02');
  });

  it('says nothing of its own while an entry is open, and the cell it closed on after', async () => {
    const { textarea, said, sheet } = await mount();
    await press(textarea, 'ArrowDown');
    expect(said()).toBe('B3, 14,02');

    await press(textarea, 'F2');
    expect(sheet.editing).toBe(true);
    expect(said()).toBe('');
    await press(textarea, 'Escape');
    expect(sheet.editing).toBe(false);
    expect(said()).toBe('B3, 14,02');
  });
});
