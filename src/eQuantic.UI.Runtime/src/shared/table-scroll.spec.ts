/**
 * The table viewport's scroll model, TRANSPILED, driven through the script `TableScrollTests` runs in
 * .NET, with the same numbers: a wheel, a finger, a scrollbar and a request mean the same distance in a
 * browser as in a native window, or one of the two suites says so.
 */

import { describe, expect, it } from 'vitest';
import { SparseTableSpans } from './components/SparseTableSpans';
import { TableScroll } from './components/TableScroll';
import { UniformTableSpans } from './components/UniformTableSpans';
import { Dictionary } from '../utils/dictionary';
import { Point, Rect, Size, TableOffset, TableRange, TableScrollRequest } from './value-types';

const EXCEL_ROWS = 1_048_576;
const EXCEL_COLUMNS = 16_384;
const rows = new UniformTableSpans(EXCEL_ROWS, 28);
const columns = new UniformTableSpans(EXCEL_COLUMNS, 96);
const SHIFT = 1;

function arranged(reduceMotion = true, rowCount = EXCEL_ROWS): TableScroll {
  const scroll = new TableScroll();
  scroll.arrange(new Size(900, 600), columns.total, rowCount * 28, 96, 28, reduceMotion);
  return scroll;
}

function offsetOf(scroll: TableScroll): [number, number] {
  return [scroll.offset.across, scroll.offset.down];
}

describe('TableScroll (transpiled twin)', () => {
  it("reaches Excel's last row exactly on a uniform axis", () => {
    expect(rows.total).toBe(29_360_128);
    expect(rows.offsetOf(EXCEL_ROWS - 1)).toBe(29_360_100);
    expect(rows.indexAt(29_360_127.5)).toBe(EXCEL_ROWS - 1);
    expect(rows.indexAt(-5)).toBe(0);
    // Past what an int counts: held inside the axis before it is narrowed, as .NET answers (#1015).
    expect(rows.indexAt(1e12)).toBe(EXCEL_ROWS - 1);
    expect(rows.indexAt(70_000_000_000)).toBe(EXCEL_ROWS - 1);
  });

  it('builds a range of one cell from a row and a column, and four zeros bare, as .NET does', () => {
    const one = new TableRange(2, 3);
    expect([one.row, one.column, one.rows, one.columns]).toEqual([2, 3, 1, 1]);
    const zero = new TableRange();
    expect([zero.row, zero.column, zero.rows, zero.columns]).toEqual([0, 0, 0, 0]);
    const offset = new TableOffset();
    expect([offset.across, offset.down]).toEqual([0, 0]);
  });

  it('adds a sparse axis its overrides at both ends', () => {
    const extents = new Dictionary<number, number>();
    extents.set(0, 40);
    extents.set(10, 100);
    extents.set(EXCEL_ROWS - 1, 60);
    extents.set(EXCEL_ROWS + 5, 999);
    const spans = new SparseTableSpans(EXCEL_ROWS, 28, extents);
    expect(spans.extentOf(0)).toBe(40);
    expect(spans.extentOf(1)).toBe(28);
    expect(spans.offsetOf(11)).toBe(40 + 9 * 28 + 100);
    expect(spans.total).toBe(EXCEL_ROWS * 28 + 12 + 72 + 32);
    expect(spans.indexAt(spans.offsetOf(11) - 0.5)).toBe(10);
  });

  it('takes a strip for each scrollbar, and a million rows leave a thumb to grab', () => {
    const scroll = arranged();
    expect([scroll.viewport.width, scroll.viewport.height]).toEqual([888, 588]);
    const track = scroll.trackOf('vertical')!;
    expect([track.x, track.y, track.width, track.height]).toEqual([888, 0, 12, 588]);
    expect(scroll.thumbOf('vertical')!.height).toBe(24);
  });

  it('scrolls down with the wheel, and across with Shift', () => {
    const scroll = arranged();
    scroll.wheel(0, 84, false, 0);
    expect(offsetOf(scroll)).toEqual([0, 84]);
    scroll.wheel(0, 48, true, SHIFT);
    expect(offsetOf(scroll)).toEqual([48, 84]);
  });

  it('leaves a shifted wheel it could not take on the axis it came on', () => {
    const scroll = arranged();
    scroll.wheel(16_384 * 96, 0, false, 0);
    const left = scroll.wheel(0, 100, false, SHIFT);
    expect([left.across, left.down]).toEqual([0, 100]);
    expect(scroll.offset.across).toBe(16_384 * 96 - scroll.viewport.width);
  });

  it('reveals an area out of view clear of the pinned bands, on the axes asked', () => {
    const scroll = arranged();
    scroll.wheel(300, 0, false, 0);
    expect(scroll.reveal(new Rect(50, 700, 96, 28), false, true)).toBe(true);
    expect(offsetOf(scroll)).toEqual([300, 140]);
    expect(scroll.reveal(new Rect(50, 300, 96, 28), true, true)).toBe(true);
    expect(offsetOf(scroll)).toEqual([254, 140]);
    expect(scroll.reveal(new Rect(400, 300, 96, 28), true, true)).toBe(false);
  });

  it('leaves what it could not take at the end of the grid', () => {
    const scroll = arranged();
    const end = EXCEL_ROWS * 28 - scroll.viewport.height;
    scroll.wheel(0, end, false, 0);
    const left = scroll.wheel(0, 100, false, 0);
    expect(scroll.offset.down).toBe(end);
    expect([left.across, left.down]).toEqual([0, 100]);
  });

  it('carries the grid with a finger and flings it when the finger lifts moving', () => {
    const scroll = arranged(false);
    expect(scroll.pointer('down', new Point(400, 400), 'touch', 0)).toBe(true);
    scroll.pointer('move', new Point(390, 360), 'touch', 16);
    scroll.pointer('move', new Point(380, 320), 'touch', 32);
    expect(offsetOf(scroll)).toEqual([20, 80]);
    scroll.pointer('up', new Point(380, 320), 'touch', 40);
    expect(scroll.advance(100)).toBe(true);
    let t = 116;
    while (scroll.advance(t)) t += 16;
    // The same landing .NET computes: 80 plus the release speed's reach.
    expect(scroll.offset.down).toBe(296);
  });

  it('puts the last row at the bottom when the thumb is dragged to the end', () => {
    const scroll = arranged();
    const thumb = scroll.thumbOf('vertical')!;
    expect(scroll.pointer('down', new Point(thumb.x + 5, thumb.y + 5), 'mouse', 0)).toBe(true);
    scroll.pointer('move', new Point(thumb.x + 5, 5000), 'mouse', 16);
    scroll.pointer('up', new Point(thumb.x + 5, 5000), 'mouse', 32);
    expect(scroll.offset.down + scroll.viewport.height).toBe(EXCEL_ROWS * 28);
  });

  it('leaves a mouse press on the cells to the cells', () => {
    expect(arranged().pointer('down', new Point(400, 300), 'mouse', 0)).toBe(false);
  });

  it('reveals a row below the window by its bottom edge, once per version', () => {
    const scroll = arranged();
    const request = new TableScrollRequest(1, { reveal: new TableRange(40, 3) });
    expect(scroll.apply(request, rows, columns, 1, 1)).toBe(true);
    expect(scroll.offset.down + scroll.viewport.height).toBe(41 * 28);
    scroll.wheel(0, -500, false, 0);
    const away = offsetOf(scroll);
    expect(scroll.apply(request, rows, columns, 1, 1)).toBe(false);
    expect(offsetOf(scroll)).toEqual(away);
  });

  it('clamps a request to an offset into the grid', () => {
    const scroll = arranged(true, 100);
    scroll.apply(new TableScrollRequest(1, { offset: new TableOffset(-10, 1e9) }), rows, columns, 1, 1);
    expect(offsetOf(scroll)).toEqual([0, 100 * 28 - scroll.viewport.height]);
  });
});
