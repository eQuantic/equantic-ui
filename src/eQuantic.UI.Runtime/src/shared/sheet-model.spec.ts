/**
 * The spreadsheet model's TS twin, EXECUTED — the same eqc emission an app ships, driven through
 * the moves the C# suite pins (a path nobody runs is a path that does not work). If a controller
 * behavior diverges between targets, one of these two suites breaks first.
 */

import { describe, expect, it } from 'vitest';
import { CellRef } from './__transpiled__/CellRef';
import { SheetRange } from './__transpiled__/SheetRange';
import { SheetController } from './__transpiled__/SheetController';

describe('SheetController (transpiled twin)', () => {
  it('speaks A1', () => {
    expect(new CellRef(0, 0).toA1()).toBe('A1');
    expect(new CellRef(11, 1).toA1()).toBe('B12');
    expect(new CellRef(0, 26).toA1()).toBe('AA1');
    expect(CellRef.parseA1('AA10')).toEqual(new CellRef(9, 26));
    expect(CellRef.parseA1('1A')).toBeNull();
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

    sheet.step(0, 1);
    expect(sheet.activeCell).toEqual(new CellRef(0, 0));
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

  it('TSV round-trips through copy and paste', () => {
    const source = new SheetController(100, 26);
    source.document.setCell(new CellRef(0, 0), 'name');
    source.document.setCell(new CellRef(0, 1), 'say "hi"\twith tab');
    source.selection = new SheetRange(new CellRef(0, 0), new CellRef(0, 1));

    const tsv = source.copyTsv();

    const target = new SheetController(100, 26);
    target.selection = new SheetRange(new CellRef(5, 5));
    target.pasteTsv(tsv);

    expect(target.document.getCell(new CellRef(5, 5))).toBe('name');
    expect(target.document.getCell(new CellRef(5, 6))).toBe('say "hi"\twith tab');
    expect(target.selection).toEqual(new SheetRange(new CellRef(5, 5), new CellRef(5, 6)));
  });
});
