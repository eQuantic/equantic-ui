/**
 * The spreadsheet model's TS twin, EXECUTED — the same eqc emission an app ships, driven through
 * the moves the C# suite pins (a path nobody runs is a path that does not work). If a controller
 * behavior diverges between targets, one of these two suites breaks first.
 */

import { describe, expect, it } from 'vitest';
import { CellRef } from './components/CellRef';
import { SheetRange } from './components/SheetRange';
import { SheetController } from './components/SheetController';
import { SheetGrid } from './components/SheetGrid';
import { TableScroll } from './components/TableScroll';
import { Point, Size, TableOffset, TableScrollRequest } from './runtime-exports';

const HEADER_WIDTH = 44;
const HEADER_HEIGHT = 26;

/** The twin of the C# suite's SheetScrolls: a scroll laid out over the document's axes behind pinned
 * headers, at an offset, handed to the engine as its geometry. */
function place(sheet: SheetController, across = 0, down = 0, width = 900, height = 586): TableScroll {
  const scroll = new TableScroll();
  const document = sheet.document;
  scroll.arrange(new Size(width, height), HEADER_WIDTH + document.columnSpans.total,
    HEADER_HEIGHT + document.rowSpans.total, HEADER_WIDTH, HEADER_HEIGHT, true);
  if (across !== 0 || down !== 0)
    scroll.apply(new TableScrollRequest(1, { offset: new TableOffset(across, down) }), document.rowSpans,
      document.columnSpans, 0, 0);
  sheet.grid = new SheetGrid(scroll, HEADER_WIDTH, HEADER_HEIGHT);
  return scroll;
}

/** Frames of the scroll's clock, the engine following every one that moved the grid. */
function hold(sheet: SheetController, scroll: TableScroll, milliseconds: number, clock: { at: number }): void {
  for (let elapsed = 0; elapsed <= milliseconds; elapsed += 16) {
    const before = scroll.offset;
    scroll.advance(clock.at);
    clock.at += 16;
    if (scroll.offset.across !== before.across || scroll.offset.down !== before.down) sheet.follow();
  }
}

describe('SheetController (transpiled twin)', () => {
  it('speaks A1', () => {
    expect(new CellRef(0, 0).toA1()).toBe('A1');
    expect(new CellRef(11, 1).toA1()).toBe('B12');
    expect(new CellRef(0, 26).toA1()).toBe('AA1');
    expect(CellRef.parseA1('AA10')).toEqual(new CellRef(9, 26));
    expect(CellRef.parseA1('1A')).toBeNull();
  });

  it("keeps the last cell of Excel's grid: its value, its address and the key .NET packs", () => {
    const sheet = new SheetController(1_048_576, 16_384);
    const last = new CellRef(1_048_575, 16_383);

    sheet.document.setCell(last, 'x');
    sheet.document.setCell(new CellRef(0, 0), 'a');
    sheet.document.setCell(new CellRef(262_144, 0), 'b');

    expect(sheet.document.getCell(last)).toBe('x');
    expect(last.toA1()).toBe('XFD1048576');
    // A long is a BigInt in the browser, with .NET's arithmetic: the same number .NET packs.
    expect(last.key).toBe(17_179_869_183n);
    expect(CellRef.fromKey(last.key)).toEqual(last);
    expect(sheet.document.getCell(new CellRef(0, 0))).toBe('a');
    expect(sheet.document.getCell(new CellRef(262_144, 0))).toBe('b');
  });

  it('moves, extends, and Ctrl+arrow jumps like Excel', () => {
    const sheet = new SheetController(100, 26);
    sheet.document.setCell(new CellRef(1, 1), 'b');
    sheet.document.setCell(new CellRef(1, 2), 'c');
    sheet.document.setCell(new CellRef(1, 3), 'd');
    sheet.selection = new SheetRange(new CellRef(1, 1));

    sheet.move(0, 1, 'dataEdge');
    expect(sheet.activeCell).toEqual(new CellRef(1, 3));

    sheet.move(0, 1, 'dataEdge');
    expect(sheet.activeCell).toEqual(new CellRef(1, 25));

    sheet.move(1, 0, 'cell', true);
    expect(sheet.selection.rowCount).toBe(2);
  });

  it('Tab walks a multi-cell selection without collapsing it', () => {
    const sheet = new SheetController(100, 26);
    sheet.selection = new SheetRange(new CellRef(0, 0), new CellRef(1, 1));
    // The anchor, A1, is active, as the cell a drag or Shift began on is in Excel.
    expect(sheet.activeCell).toEqual(new CellRef(0, 0));

    sheet.step(0, 1);
    expect(sheet.activeCell).toEqual(new CellRef(0, 1));
    expect(sheet.selection.rowCount).toBe(2);
  });

  it('edits undo and redo', () => {
    const sheet = new SheetController(100, 26);
    sheet.setCell(new CellRef(2, 2), 'hello');
    expect(sheet.document.getCell(new CellRef(2, 2))).toBe('hello');

    sheet.undo();
    expect(sheet.document.getCell(new CellRef(2, 2))).toBe('');
    sheet.redo();
    expect(sheet.document.getCell(new CellRef(2, 2))).toBe('hello');
  });

  it('structure edits shift data and resurrect on undo', () => {
    const sheet = new SheetController(100, 26);
    sheet.document.setCell(new CellRef(0, 1), 'goes');
    sheet.document.setCell(new CellRef(0, 3), 'stays');

    sheet.delete('cols', 1, 2);
    expect(sheet.document.getCell(new CellRef(0, 1))).toBe('stays');

    sheet.undo();
    expect(sheet.document.getCell(new CellRef(0, 1))).toBe('goes');
    expect(sheet.document.getCell(new CellRef(0, 3))).toBe('stays');
  });

  it('a fill move reports a change only when the preview moves, and a cancel pours nothing', () => {
    const sheet = new SheetController(50, 8);
    sheet.document.setCell(new CellRef(1, 1), '7');
    sheet.selection = new SheetRange(new CellRef(1, 1));

    // B2's bottom-right corner, where the handle is drawn.
    sheet.handlePointer('down', new Point(192, 56), 0, 1, 'mouse');
    expect(sheet.handlePointer('move', new Point(144, 98), 0, 1, 'mouse')).toBe(true);
    // The same cell again is the same preview: compared by value, as .NET compares it.
    expect(sheet.handlePointer('move', new Point(154, 98), 0, 1, 'mouse')).toBe(false);
    expect(sheet.handlePointer('cancel', new Point(0, 0), 0, 1, 'mouse')).toBe(true);

    expect(sheet.filling).toBe(false);
    expect(sheet.document.getCell(new CellRef(2, 1))).toBe('');
  });

  it('a fill move compares an absent preview as .NET lifts !=: none and none are the same, none and one are not', () => {
    const sheet = new SheetController(50, 8);
    sheet.selection = new SheetRange(new CellRef(1, 1));

    sheet.handlePointer('down', new Point(192, 56), 0, 1, 'mouse');
    // Inside the source: no preview before the move, and none after it.
    expect(sheet.handlePointer('move', new Point(144, 42), 0, 1, 'mouse')).toBe(false);
    // Out to B4: a preview where there was none.
    expect(sheet.handlePointer('move', new Point(144, 98), 0, 1, 'mouse')).toBe(true);
    // Back into B2: the preview clears, which is a change too.
    expect(sheet.handlePointer('move', new Point(150, 40), 0, 1, 'mouse')).toBe(true);
    expect(sheet.fillTarget).toBeNull();
    expect(sheet.handlePointer('move', new Point(160, 44), 0, 1, 'mouse')).toBe(false);
  });

  it('TSV round-trips through copy and paste', () => {
    const source = new SheetController(100, 26);
    source.document.setCell(new CellRef(0, 0), 'name');
    source.document.setCell(new CellRef(0, 1), 'say "hi"\twith tab');
    source.selection = new SheetRange(new CellRef(0, 0), new CellRef(0, 1));

    const tsv = source.copyText();

    const target = new SheetController(100, 26);
    target.selection = new SheetRange(new CellRef(5, 5));
    target.paste(tsv);

    expect(target.document.getCell(new CellRef(5, 5))).toBe('name');
    expect(target.document.getCell(new CellRef(5, 6))).toBe('say "hi"\twith tab');
    expect(target.selection).toEqual(new SheetRange(new CellRef(5, 5), new CellRef(5, 6)));
  });

  it("answers its axes as spans at both ends of Excel's grid, and reads a size that changed", () => {
    const sheet = new SheetController(1_048_576, 16_384);
    sheet.document.setRowHeight(0, 40);
    sheet.document.setRowHeight(1_048_575, 50);
    const rows = sheet.document.rowSpans;

    expect(rows.count).toBe(1_048_576);
    expect(rows.offsetOf(2)).toBe(68);
    expect(rows.offsetOf(1_048_575)).toBe(40 + 1_048_574 * 28);
    expect(rows.total).toBe(40 + 1_048_574 * 28 + 50);
    expect(rows.indexAt(39.9)).toBe(0);
    expect(rows.indexAt(40)).toBe(1);
    expect(rows.indexAt(rows.total + 100)).toBe(1_048_575);

    sheet.document.setRowHeight(5, 100);
    expect(rows.offsetOf(10)).toBe(40 + 9 * 28 + 72);
  });

  it('reads a press against the scroll as it stands, past the pinned headers', () => {
    const sheet = new SheetController(200, 8);
    place(sheet, 96, 40 * 28, 400);

    sheet.handlePointer('down', new Point(HEADER_WIDTH + 96 + 48, HEADER_HEIGHT + 2 * 28 + 14), 0, 1, 'mouse');
    expect(sheet.activeCell).toEqual(new CellRef(42, 2));
    // Over the header row, the first row shown; over the row numbers, the first column shown.
    expect(sheet.cellAt(new Point(HEADER_WIDTH + 10, 5))).toEqual(new CellRef(40, 1));
    expect(sheet.cellAt(new Point(5, HEADER_HEIGHT + 10))).toEqual(new CellRef(40, 1));
  });

  it('raises the reveal version for a key, a paste, a fill, an undo and a redo, never for a press', () => {
    const sheet = new SheetController(100, 26);
    let version = sheet.revealVersion;
    const raised = () => {
      expect(sheet.revealVersion).toBeGreaterThan(version);
      version = sheet.revealVersion;
    };

    sheet.handleKey('ArrowDown', 0, 'standard', null);
    raised();
    sheet.paste('a\tb');
    raised();
    sheet.document.setCell(new CellRef(0, 0), 'x');
    sheet.selection = new SheetRange(new CellRef(0, 0), new CellRef(3, 0));
    expect(sheet.fillDown()).toBe(true);
    raised();
    sheet.undo();
    raised();
    sheet.redo();
    raised();

    sheet.handlePointer('down', new Point(200, 200), 0, 1, 'mouse');
    sheet.handlePointer('up', new Point(200, 200), 0, 1, 'mouse');
    expect(sheet.revealVersion).toBe(version);
  });

  it('reveals the cell the command left, never one a later drag reaches', () => {
    const sheet = new SheetController(100, 26);
    sheet.handleKey('ArrowDown', 0, 'standard', null);
    expect(sheet.revealCell).toEqual(new CellRef(1, 0));

    sheet.handlePointer('down', new Point(96 + 48, 28 * 3 + 14), 0, 1, 'mouse');
    sheet.handlePointer('move', new Point(96 * 3 + 48, 28 * 9 + 14), 0, 1, 'mouse');

    expect(sheet.selection.focus).toEqual(new CellRef(9, 3));
    expect(sheet.activeCell, 'the cell the drag began on stays active').toEqual(new CellRef(3, 1));
    expect(sheet.revealCell).toEqual(new CellRef(1, 0));
  });

  it('asks for frames past the edge, holds the last cell shown, and reveals a selection set from outside', () => {
    const sheet = new SheetController(200, 8);
    const scroll = place(sheet);
    const x = HEADER_WIDTH + 96 + 48;
    sheet.handlePointer('down', new Point(x, HEADER_HEIGHT + 28 + 14), 0, 1, 'mouse');
    sheet.handlePointer('move', new Point(x, scroll.viewport.height - 2), 0, 1, 'mouse');
    expect(sheet.handlePointer('move', new Point(x, scroll.viewport.height + 3), 0, 1, 'mouse')).toBe(true);
    const lastShown = sheet.document.rowSpans.indexAt(scroll.offset.down + scroll.viewport.height - HEADER_HEIGHT - 1);
    sheet.handlePointer('move', new Point(x, scroll.viewport.height + 60), 0, 1, 'mouse');
    expect(sheet.selection.focus.row).toBe(lastShown);
    sheet.handlePointer('up', new Point(x, scroll.viewport.height + 60), 0, 1, 'mouse');

    const version = sheet.revealVersion;
    sheet.selection = new SheetRange(new CellRef(150, 2));
    expect(sheet.revealVersion).toBeGreaterThan(version);
    expect(sheet.revealCell).toEqual(new CellRef(150, 2));
  });

  it('moves a page by the rows shown under the headers, and takes no page without a viewport', () => {
    const sheet = new SheetController(200, 8);
    expect(sheet.handleKey('PageDown', 0, 'standard', null)).toBe(false);

    place(sheet, 0, 0, 900, HEADER_HEIGHT + 20 * 28);
    expect(sheet.handleKey('PageDown', 0, 'standard', null)).toBe(true);
    expect(sheet.activeCell).toEqual(new CellRef(20, 0));
    expect(sheet.handleKey('PageDown', 1, 'standard', null)).toBe(true);
    expect(sheet.selection).toEqual(new SheetRange(new CellRef(20, 0), new CellRef(40, 0)));
    expect(sheet.activeCell, 'Shift keeps the active cell').toEqual(new CellRef(20, 0));
    sheet.handleKey('PageUp', 0, 'standard', null);
    expect(sheet.activeCell, 'a plain PageUp moves from the active cell').toEqual(new CellRef(0, 0));
  });

  it("selects on a finger's release, and nothing when the grid takes it as a pan", () => {
    const sheet = new SheetController(50, 8);
    sheet.selection = new SheetRange(new CellRef(1, 1));

    expect(sheet.handlePointer('down', new Point(3 * 96 + 48, 4 * 28 + 14), 0, 1, 'touch')).toBe(false);
    expect(sheet.selection).toEqual(new SheetRange(new CellRef(1, 1)));
    sheet.handlePointer('up', new Point(3 * 96 + 48, 4 * 28 + 14), 0, 1, 'touch');
    expect(sheet.selection).toEqual(new SheetRange(new CellRef(4, 3)));

    sheet.handlePointer('down', new Point(48, 14), 0, 1, 'touch');
    sheet.handlePointer('cancel', new Point(0, 0), 0, 1, 'touch');
    sheet.handlePointer('up', new Point(48, 14), 0, 1, 'touch');
    expect(sheet.selection).toEqual(new SheetRange(new CellRef(4, 3)));
  });

  it('scrolls a selection held below the grid and keeps extending it, until the release', () => {
    const sheet = new SheetController(200, 8);
    const scroll = place(sheet);
    sheet.handlePointer('down', new Point(HEADER_WIDTH + 96 + 48, HEADER_HEIGHT + 28 + 14), 0, 1, 'mouse');
    const below = new Point(HEADER_WIDTH + 96 + 48, scroll.viewport.height + 60);

    sheet.handlePointer('move', below, 0, 1, 'mouse');
    const clock = { at: 0 };
    hold(sheet, scroll, 1000, clock);

    expect(scroll.offset.down).toBeGreaterThan(0);
    expect(sheet.selection.anchor).toEqual(new CellRef(1, 1));
    expect(sheet.selection.focus.row).toBeGreaterThan(20);

    sheet.handlePointer('up', below, 0, 1, 'mouse');
    const stopped = scroll.offset;
    hold(sheet, scroll, 200, clock);
    expect(scroll.offset).toEqual(stopped);
    expect(sheet.follow()).toBe(false);
  });
});
