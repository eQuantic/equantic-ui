/**
 * The spreadsheet's WEB half, end to end: the lowered surface hands its keys, presses and clipboard
 * events to the SAME transpiled model the native host hands them to, typing edits Excel's way, and
 * copy/paste speak TSV through the browser's own clipboard events. A path nobody runs is a path that
 * does not work — this runs it with real DOM events.
 */

import { describe, expect, it } from 'vitest';
import { lowerVisualNode, type LoweringContext } from './lowering';
import { effectiveStyle } from './style-atomizer';
import { photonTheme } from './design-system.generated';
import { SheetController } from './components/SheetController';
import { CellRef } from './components/CellRef';
import { SheetRange } from './components/SheetRange';
import { SheetGrid } from './components/SheetGrid';
import { TableScroll } from './components/TableScroll';
import { Size, TableOffset, TableScrollRequest } from './runtime-exports';
import type { HtmlNode } from '../core/types';

type Handler = (event: Event) => void;

function surfaceFor(sheet: SheetController): HtmlNode {
  return lowerVisualNode(
    {
      nodeKind: 'sheetSurface',
      child: { nodeKind: 'column', children: [] },
      model: sheet,
      onChanged: () => {},
    } as never,
    { textPrimary: photonTheme.textPrimary } as LoweringContext,
  );
}

const events = (node: HtmlNode) => (node as { events: Record<string, Handler> }).events;

/** The surface's TEXT DOOR: the textarea its keys, its text and its clipboard come through. */
const door = (node: HtmlNode) => node.children.find((child) => child.tag === 'textarea') as HtmlNode;
const doorEvents = (node: HtmlNode) => events(door(node));

/** A stand-in grid element: zeroed rect (so clientX/Y ARE grid coordinates), and its door, a focus sink. */
function makeTarget(): HTMLElement {
  const target = document.createElement('div');
  const input = document.createElement('textarea');
  input.className = 'eq-code-input';
  input.focus = () => {};
  target.appendChild(input);
  return target;
}

function key(node: HtmlNode, keyName: string, init: KeyboardEventInit = {}): void {
  doorEvents(node)['keydown'](new KeyboardEvent('keydown', { key: keyName, cancelable: true, ...init }));
}

/** Text the platform decided the user typed: what `beforeinput` carries, never the keydown. */
function type(node: HtmlNode, text: string): void {
  for (const c of text) {
    doorEvents(node)['beforeinput'](new InputEvent('beforeinput', { inputType: 'insertText', data: c, cancelable: true }));
  }
}

type PointerInit = MouseEventInit & { pointerType?: string };

function on<T extends Event>(event: T, target: HTMLElement): T {
  Object.defineProperty(event, 'currentTarget', { value: target });
  return event;
}

/** A press as a browser sends it to the surface: the pointerdown, then a mouse's mousedown, which
 * carries the platform's click count. */
function press(node: HtmlNode, target: HTMLElement, init: PointerInit = {}): void {
  const pointerType = init.pointerType ?? 'mouse';
  events(node)['pointerdown'](on(new PointerEvent('pointerdown', { pointerId: 1, cancelable: true, buttons: 1, ...init, pointerType }), target));
  if (pointerType === 'mouse') events(node)['mousedown'](on(new MouseEvent('mousedown', { cancelable: true, ...init }), target));
}

function move(node: HtmlNode, target: HTMLElement, init: PointerInit = {}): void {
  events(node)['pointermove'](on(new PointerEvent('pointermove', { pointerId: 1, buttons: 1, pointerType: 'mouse', ...init }), target));
}

function lift(node: HtmlNode, target: HTMLElement, init: PointerInit = {}): void {
  events(node)['pointerup'](on(new PointerEvent('pointerup', { pointerId: 1, cancelable: true, pointerType: 'mouse', ...init }), target));
}

describe('SheetSurface on the web', () => {
  it('is a field whose door holds its stop of the Tab order, and whose keyboard is the shared keymap', () => {
    const sheet = new SheetController(50, 8);
    const node = surfaceFor(sheet);

    expect((door(node) as { attributes: Record<string, string> }).attributes['tabindex']).toBe('0');
    expect((node as { attributes: Record<string, string> }).attributes['tabindex']).toBeUndefined();
    // The table it holds is the grid a reader walks (#1081): the host bears no role of its own.
    expect((node as { attributes: Record<string, string> }).attributes['role']).toBeUndefined();
    // A selection drag must paint the band, not the browser's blue text sweep.
    expect(effectiveStyle(node)).toContain('user-select: none');

    key(node, 'ArrowDown');
    key(node, 'ArrowRight', { shiftKey: true });

    expect(sheet.activeCell, 'Shift stretches the range and keeps the active cell').toEqual(new CellRef(1, 0));
    expect(sheet.selection.focus).toEqual(new CellRef(1, 1));
    expect(sheet.selection.colCount).toBe(2);
  });

  it('types the Excel way: replace, append, Enter commits stepping down', () => {
    const sheet = new SheetController(50, 8);
    sheet.document.setCell(new CellRef(0, 0), 'old');
    const node = surfaceFor(sheet);

    type(node, '4');
    type(node, '2');
    expect(sheet.editing).toBe(true);
    expect(sheet.entry.document.text).toBe('42');
    expect(sheet.document.getCell(new CellRef(0, 0))).toBe('old');

    key(node, 'Enter');
    expect(sheet.document.getCell(new CellRef(0, 0))).toBe('42');
    expect(sheet.activeCell).toEqual(new CellRef(1, 0));
  });

  it('Escape discards; ⌘Z undoes a committed cell', () => {
    const sheet = new SheetController(50, 8);
    const node = surfaceFor(sheet);

    type(node, 'x');
    key(node, 'Escape');
    expect(sheet.document.getCell(new CellRef(0, 0))).toBe('');

    type(node, 'a');
    key(node, 'Enter');
    key(node, 'z', { metaKey: true });
    expect(sheet.document.getCell(new CellRef(0, 0))).toBe('');
  });

  it('shift+click stretches from the anchor; a plain drag sweeps a range', () => {
    const sheet = new SheetController(50, 8);
    const node = surfaceFor(sheet);
    const target = makeTarget();

    // happy-dom's getBoundingClientRect is all zeros, so clientX/Y ARE grid coordinates.
    press(node, target, { clientX: 48, clientY: 14 }); // A1
    lift(node, target, { clientX: 48, clientY: 14 });
    press(node, target, { clientX: 96 * 2 + 48, clientY: 28 * 2 + 14, shiftKey: true }); // shift+click C3
    lift(node, target, { clientX: 96 * 2 + 48, clientY: 28 * 2 + 14 });
    expect(sheet.selection.topRow).toBe(0);
    expect(sheet.selection.bottomRow).toBe(2);
    expect(sheet.selection.rightCol).toBe(2);
    expect(sheet.activeCell).toEqual(new CellRef(0, 0));

    // Plain drag: a press on A1, the captured pointer moving to B2 — the anchor stands.
    press(node, target, { clientX: 48, clientY: 14 });
    move(node, target, { clientX: 96 + 48, clientY: 28 + 14 });
    expect(sheet.selection.bottomRow).toBe(1);
    expect(sheet.selection.rightCol).toBe(1);
    lift(node, target, { clientX: 96 + 48, clientY: 28 + 14 });
  });

  it('grabbing the fill handle pours the source, one undo for the whole drag', () => {
    const sheet = new SheetController(50, 8);
    sheet.document.setCell(new CellRef(0, 0), 'seed');
    const node = surfaceFor(sheet);

    // A1 selected by default; its bottom-right corner is at (96, 28).
    const target = makeTarget();
    press(node, target, { clientX: 95, clientY: 27 });
    expect(sheet.filling).toBe(true);

    move(node, target, { clientX: 95, clientY: 28 * 2 + 14 });
    expect(sheet.fillTarget).not.toBeNull();
    lift(node, target, { clientX: 95, clientY: 28 * 2 + 14 });

    expect(sheet.document.getCell(new CellRef(1, 0))).toBe('seed');
    expect(sheet.document.getCell(new CellRef(2, 0))).toBe('seed');
    sheet.undo();
    expect(sheet.document.getCell(new CellRef(1, 0))).toBe('');
  });

  it('a double click edits the cell under it, starting from its value', () => {
    const sheet = new SheetController(50, 8);
    sheet.document.setCell(new CellRef(1, 1), '42');
    const node = surfaceFor(sheet);
    // The platform's click count rides the mousedown: the second press of a double click says 2,
    // where its pointerdown says 0 in Chrome, measured.
    const target = makeTarget();
    const click = (detail: number) => {
      press(node, target, { clientX: 96 + 48, clientY: 28 + 14, detail });
      lift(node, target, { clientX: 96 + 48, clientY: 28 + 14 });
    };

    click(1);
    click(2);

    expect(sheet.activeCell).toEqual(new CellRef(1, 1));
    expect(sheet.editing).toBe(true);
    expect(sheet.entry.document.text).toBe('42');
  });

  it('reads a press against the window the engine was handed', () => {
    const sheet = new SheetController(200, 8);
    // A scrolled window: the grid stands 40 rows down, so the surface's first row draws row 40.
    const scroll = new TableScroll();
    scroll.arrange(new Size(900, 600), sheet.document.columnSpans.total, sheet.document.rowSpans.total, 0, 0, true);
    scroll.apply(new TableScrollRequest(1, { offset: new TableOffset(0, 40 * 28) }), sheet.document.rowSpans,
      sheet.document.columnSpans, 0, 0);
    sheet.grid = new SheetGrid(scroll, 0, 0);
    const node = surfaceFor(sheet);

    const target = makeTarget();
    press(node, target, { clientX: 96 + 48, clientY: 28 * 2 + 14 });
    lift(node, target, { clientX: 96 + 48, clientY: 28 * 2 + 14 });

    expect(sheet.activeCell).toEqual(new CellRef(42, 1));
  });

  it("answers a reader's activation of a cell or a header, a click no press came before (#1081)", () => {
    const sheet = new SheetController(200, 8);
    // The table's pinned bands: row numbers 44 wide, column headers 26 tall.
    const scroll = new TableScroll();
    scroll.arrange(new Size(900, 600), 44 + sheet.document.columnSpans.total, 26 + sheet.document.rowSpans.total, 44, 26, true);
    sheet.grid = new SheetGrid(scroll, 44, 26);
    const node = surfaceFor(sheet);
    const target = makeTarget();
    target.getBoundingClientRect = () => ({ left: 0, top: 0, width: 900, height: 600 }) as DOMRect;
    /** A cell or a header of the table, where the table draws it. */
    const drawn = (x: number, y: number, width: number, height: number) => {
      const cell = document.createElement('div');
      cell.setAttribute('data-eq-cell', '0:0');
      cell.getBoundingClientRect = () => ({ left: x, top: y, width, height }) as DOMRect;
      target.appendChild(cell);
      return cell;
    };
    const activate = (cell: HTMLElement) => {
      const click = new MouseEvent('click', { bubbles: true, cancelable: true });
      Object.defineProperty(click, 'target', { value: cell });
      events(node)['click'](on(click, target));
    };

    // D6, then column C's header.
    activate(drawn(44 + 3 * 96, 26 + 5 * 28, 96, 28));
    expect(sheet.activeCell).toEqual(new CellRef(5, 3));
    expect(sheet.ranges).toEqual([new SheetRange(new CellRef(5, 3), new CellRef(5, 3))]);
    activate(drawn(44 + 2 * 96, 0, 96, 26));
    expect(sheet.ranges).toEqual([new SheetRange(new CellRef(0, 2), new CellRef(sheet.document.rows - 1, 2))]);

    // The click that follows a press the sheet answered is that press's: B3 stays the selection.
    press(node, target, { clientX: 44 + 96 + 48, clientY: 26 + 2 * 28 + 14 });
    lift(node, target, { clientX: 44 + 96 + 48, clientY: 26 + 2 * 28 + 14 });
    activate(drawn(44 + 5 * 96, 26 + 7 * 28, 96, 28));
    expect(sheet.activeCell).toEqual(new CellRef(2, 1));

    // A finger's pan, which the table marked handled, is followed by no click: the next reader's
    // activation is answered still.
    press(node, target, { clientX: 300, clientY: 300, pointerType: 'touch' });
    const panned = new PointerEvent('pointerup', { pointerId: 1, cancelable: true, pointerType: 'touch', clientX: 300, clientY: 200 });
    panned.preventDefault();
    events(node)['pointerup'](on(panned, target));
    activate(drawn(44 + 3 * 96, 26 + 5 * 28, 96, 28));
    expect(sheet.activeCell).toEqual(new CellRef(5, 3));
  });

  it('reads a press on the headers with its modifiers, as Photon does (#984)', () => {
    const sheet = new SheetController(200, 8);
    // The table's pinned bands: row numbers 44 wide, column headers 26 tall.
    const scroll = new TableScroll();
    scroll.arrange(new Size(900, 600), 44 + sheet.document.columnSpans.total, 26 + sheet.document.rowSpans.total, 44, 26, true);
    sheet.grid = new SheetGrid(scroll, 44, 26);
    const node = surfaceFor(sheet);
    const target = makeTarget();
    const columnHeader = (col: number) => ({ clientX: 44 + col * 96 + 48, clientY: 13 });
    const rowNumber = (row: number) => ({ clientX: 22, clientY: 26 + row * 28 + 14 });
    const lastRow = sheet.document.rows - 1;
    const lastCol = sheet.document.cols - 1;

    // B3, then ⌘ (Ctrl elsewhere) on column E's header adds the column.
    press(node, target, { clientX: 44 + 96 + 48, clientY: 26 + 2 * 28 + 14 });
    lift(node, target, { clientX: 44 + 96 + 48, clientY: 26 + 2 * 28 + 14 });
    press(node, target, { ...columnHeader(4), metaKey: true, ctrlKey: true });
    lift(node, target, { ...columnHeader(4), metaKey: true, ctrlKey: true });
    expect(sheet.ranges).toEqual([new SheetRange(new CellRef(2, 1), new CellRef(2, 1)),
      new SheetRange(new CellRef(0, 4), new CellRef(lastRow, 4))]);

    // Shift on row 6's number from B3: rows 3 to 6, whole, B3 still active.
    press(node, target, { clientX: 44 + 96 + 48, clientY: 26 + 2 * 28 + 14 });
    lift(node, target, { clientX: 44 + 96 + 48, clientY: 26 + 2 * 28 + 14 });
    press(node, target, { ...rowNumber(5), shiftKey: true });
    lift(node, target, { ...rowNumber(5), shiftKey: true });
    expect(sheet.ranges).toEqual([new SheetRange(new CellRef(2, 0), new CellRef(5, lastCol))]);
    expect(sheet.activeCell).toEqual(new CellRef(2, 1));

    // A drag across column headers, from B's to D's.
    press(node, target, columnHeader(1));
    move(node, target, columnHeader(3));
    lift(node, target, columnHeader(3));
    expect(sheet.ranges).toEqual([new SheetRange(new CellRef(0, 1), new CellRef(lastRow, 3))]);
  });

  it("answers Excel's chords through its door, and Shift+Space types nothing (#984)", () => {
    const sheet = new SheetController(1000, 26);
    const scroll = new TableScroll();
    scroll.arrange(new Size(900, 600), 44 + sheet.document.columnSpans.total, 26 + sheet.document.rowSpans.total, 44, 26, true);
    sheet.grid = new SheetGrid(scroll, 44, 26);
    for (const [row, col] of [[1, 1], [1, 2], [2, 1], [2, 2], [6, 4]]) sheet.document.setCell(new CellRef(row, col), 'x');
    sheet.selection = new SheetRange(new CellRef(1, 1), new CellRef(1, 1));
    const node = surfaceFor(sheet);

    const shiftSpace = new KeyboardEvent('keydown', { key: ' ', shiftKey: true, cancelable: true });
    doorEvents(node)['keydown'](shiftSpace);
    expect(shiftSpace.defaultPrevented).toBe(true);
    expect(sheet.selection).toEqual(new SheetRange(new CellRef(1, 0), new CellRef(1, 25)));

    // From B2 again: after Shift+Space the active range is the whole row, whose columns are all of them.
    sheet.selection = new SheetRange(new CellRef(1, 1), new CellRef(1, 1));
    key(node, ' ', { ctrlKey: true });
    expect(sheet.selection).toEqual(new SheetRange(new CellRef(0, 1), new CellRef(999, 1)));

    key(node, 'ArrowDown');
    key(node, 'a', { ctrlKey: true });
    expect(sheet.selection).toEqual(new SheetRange(new CellRef(1, 1), new CellRef(2, 2)));

    key(node, 'End', { ctrlKey: true });
    expect(sheet.activeCell).toEqual(new CellRef(6, 4));

    key(node, 'Home', { ctrlKey: true });
    key(node, 'PageDown', { altKey: true });
    expect(sheet.activeCell).toEqual(new CellRef(0, 8));
  });

  it('reads the press where it landed, before the keyboard moves the page', () => {
    const sheet = new SheetController(50, 8);
    const node = surfaceFor(sheet);
    // A surface whose focus scrolls it 127px up, as a browser's default focus() did the
    // dashboard's sheet: a point read after focusing lands five rows below the one pressed.
    let top = 0;
    const focusOptions: (FocusOptions | undefined)[] = [];
    const target = document.createElement('div');
    target.getBoundingClientRect = () => ({ left: 0, top, x: 0, y: top, width: 768, height: 5600, right: 768, bottom: top + 5600, toJSON: () => ({}) }) as DOMRect;
    const input = document.createElement('textarea');
    input.className = 'eq-code-input';
    input.focus = (options?: FocusOptions) => {
      focusOptions.push(options);
      if (!options?.preventScroll) top -= 127;
    };
    target.appendChild(input);

    press(node, target, { clientX: 96 + 48, clientY: 28 * 2 + 14 });
    lift(node, target, { clientX: 96 + 48, clientY: 28 * 2 + 14 });

    expect(sheet.activeCell).toEqual(new CellRef(2, 1));
    expect(focusOptions).toEqual([{ preventScroll: true }]);
  });

  it('leaves ⌘C to the browser, whose copy event carries the text', () => {
    const sheet = new SheetController(50, 8);
    const node = surfaceFor(sheet);

    const copyKey = new KeyboardEvent('keydown', { key: 'c', metaKey: true, cancelable: true });
    doorEvents(node)['keydown'](copyKey);

    expect(copyKey.defaultPrevented).toBe(false);
    expect(sheet.editing).toBe(false);
  });

  it('a fill whose window loses focus pours nothing, and what follows changes nothing', () => {
    const sheet = new SheetController(50, 8);
    sheet.document.setCell(new CellRef(0, 0), 'seed');
    const node = surfaceFor(sheet);
    const target = makeTarget();
    press(node, target, { clientX: 95, clientY: 27 });
    move(node, target, { clientX: 95, clientY: 28 * 2 + 14 });
    expect(sheet.fillTarget).not.toBeNull();

    window.dispatchEvent(new Event('blur'));
    move(node, target, { clientX: 95, clientY: 28 * 4 + 14 });
    lift(node, target, { clientX: 95, clientY: 28 * 4 + 14 });

    expect(sheet.filling).toBe(false);
    expect(sheet.fillTarget).toBeNull();
    expect(sheet.document.getCell(new CellRef(1, 0))).toBe('');
    expect(sheet.history.canUndo).toBe(false);
  });

  it('keeps a press across the renders its own drag causes', () => {
    // A sheet that follows a held drag renders on every frame of it: the release arrives at the
    // handlers of a render that never saw the press.
    const sheet = new SheetController(50, 8);
    sheet.document.setCell(new CellRef(0, 0), 'seed');
    const target = makeTarget();
    press(surfaceFor(sheet), target, { clientX: 95, clientY: 27 });
    expect(sheet.filling).toBe(true);

    const rendered = surfaceFor(sheet);
    move(rendered, target, { clientX: 95, clientY: 28 * 2 + 14 });
    lift(rendered, target, { clientX: 95, clientY: 28 * 2 + 14 });

    expect(sheet.filling).toBe(false);
    expect(sheet.document.getCell(new CellRef(2, 0))).toBe('seed');
  });

  it('a move with no button held ends the press its release never reached', () => {
    const sheet = new SheetController(50, 8);
    sheet.document.setCell(new CellRef(0, 0), 'seed');
    const node = surfaceFor(sheet);
    const target = makeTarget();
    press(node, target, { clientX: 95, clientY: 27 });
    move(node, target, { clientX: 95, clientY: 28 * 2 + 14 });

    // Released where the page never heard it: only moves with nothing held.
    move(node, target, { clientX: 95, clientY: 28 * 3 + 14, buttons: 0 });
    lift(node, target, { clientX: 95, clientY: 28 * 3 + 14 });

    expect(sheet.filling).toBe(false);
    expect(sheet.document.getCell(new CellRef(1, 0))).toBe('');
    expect(sheet.document.getCell(new CellRef(2, 0))).toBe('');
  });

  it('a cut whose clipboard write fails empties nothing', () => {
    const sheet = new SheetController(50, 8);
    sheet.document.setCell(new CellRef(0, 0), 'kept');
    const node = surfaceFor(sheet);

    const cutEvent = new Event('cut', { cancelable: true }) as ClipboardEvent;
    Object.defineProperty(cutEvent, 'clipboardData', {
      value: {
        setData: () => {
          throw new Error('the clipboard refused the write');
        },
      },
    });
    expect(() => doorEvents(node)['cut'](cutEvent)).toThrow();

    expect(sheet.document.getCell(new CellRef(0, 0))).toBe('kept');
    expect(sheet.history.canUndo).toBe(false);
  });

  it('a cut event that carries no clipboard empties nothing', () => {
    const sheet = new SheetController(50, 8);
    sheet.document.setCell(new CellRef(0, 0), 'kept');
    const node = surfaceFor(sheet);

    const cutEvent = new Event('cut', { cancelable: true }) as ClipboardEvent;
    doorEvents(node)['cut'](cutEvent);

    expect(sheet.document.getCell(new CellRef(0, 0))).toBe('kept');
  });

  it('⌘D pours the row above through the shared keymap', () => {
    const sheet = new SheetController(50, 8);
    sheet.document.setCell(new CellRef(0, 0), 'top');
    sheet.selection = new SheetRange(new CellRef(1, 0));
    const node = surfaceFor(sheet);

    key(node, 'd', { metaKey: true });

    expect(sheet.document.getCell(new CellRef(1, 0))).toBe('top');
  });

  it('copy and paste speak TSV through the browser clipboard events', () => {
    const sheet = new SheetController(50, 8);
    sheet.document.setCell(new CellRef(0, 0), 'a');
    sheet.document.setCell(new CellRef(0, 1), 'b');
    sheet.selectAll();
    const node = surfaceFor(sheet);

    const written: Record<string, string> = {};
    const copyEvent = new Event('copy') as ClipboardEvent;
    Object.defineProperty(copyEvent, 'clipboardData', {
      value: { setData: (type: string, value: string) => (written[type] = value) },
    });
    doorEvents(node)['copy'](copyEvent);
    expect(written['text/plain']).toContain('a\tb');

    const target = new SheetController(50, 8);
    const targetNode = surfaceFor(target);
    const pasteEvent = new Event('paste') as ClipboardEvent;
    Object.defineProperty(pasteEvent, 'clipboardData', {
      value: { getData: () => 'x\ty' },
    });
    doorEvents(targetNode)['paste'](pasteEvent);
    expect(target.document.getCell(new CellRef(0, 0))).toBe('x');
    expect(target.document.getCell(new CellRef(0, 1))).toBe('y');
  });
});
