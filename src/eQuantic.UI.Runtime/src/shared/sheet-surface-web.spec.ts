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
import { Point } from './runtime-exports';
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

/** A stand-in grid element: zeroed rect (so clientX/Y ARE grid coordinates) and a focus sink. */
function makeTarget(): HTMLElement {
  const target = document.createElement('div');
  target.focus = () => {};
  return target;
}

function key(node: HtmlNode, keyName: string, init: KeyboardEventInit = {}): void {
  events(node)['keydown'](new KeyboardEvent('keydown', { key: keyName, ...init }));
}

describe('SheetSurface on the web', () => {
  it('is a focusable grid whose keyboard is the shared keymap', () => {
    const sheet = new SheetController(50, 8);
    const node = surfaceFor(sheet);

    expect((node as { attributes: Record<string, string> }).attributes['tabindex']).toBe('0');
    expect((node as { attributes: Record<string, string> }).attributes['role']).toBe('grid');
    // A selection drag must paint the band, not the browser's blue text sweep.
    expect(effectiveStyle(node)).toContain('user-select: none');

    key(node, 'ArrowDown');
    key(node, 'ArrowRight', { shiftKey: true });

    expect(sheet.activeCell).toEqual(new CellRef(1, 1));
    expect(sheet.selection.colCount).toBe(2);
  });

  it('types the Excel way: replace, append, Enter commits stepping down', () => {
    const sheet = new SheetController(50, 8);
    sheet.document.setCell(new CellRef(0, 0), 'old');
    const node = surfaceFor(sheet);

    key(node, '4');
    key(node, '2');
    expect(sheet.editing).toBe(true);
    expect(sheet.draft).toBe('42');
    expect(sheet.document.getCell(new CellRef(0, 0))).toBe('old');

    key(node, 'Enter');
    expect(sheet.document.getCell(new CellRef(0, 0))).toBe('42');
    expect(sheet.activeCell).toEqual(new CellRef(1, 0));
  });

  it('Escape discards; ⌘Z undoes a committed cell', () => {
    const sheet = new SheetController(50, 8);
    const node = surfaceFor(sheet);

    key(node, 'x');
    key(node, 'Escape');
    expect(sheet.document.getCell(new CellRef(0, 0))).toBe('');

    key(node, 'a');
    key(node, 'Enter');
    key(node, 'z', { metaKey: true });
    expect(sheet.document.getCell(new CellRef(0, 0))).toBe('');
  });

  it('shift+click stretches from the anchor; a plain drag sweeps a range', () => {
    const sheet = new SheetController(50, 8);
    const node = surfaceFor(sheet);
    const mousedown = (init: MouseEventInit) => {
      const event = new MouseEvent('mousedown', init);
      Object.defineProperty(event, 'currentTarget', { value: makeTarget() });
      events(node)['mousedown'](event);
    };

    // jsdom's getBoundingClientRect is all zeros, so clientX/Y ARE grid coordinates.
    mousedown({ clientX: 48, clientY: 14 }); // A1
    mousedown({ clientX: 96 * 2 + 48, clientY: 28 * 2 + 14, shiftKey: true }); // shift+click C3
    expect(sheet.selection.topRow).toBe(0);
    expect(sheet.selection.bottomRow).toBe(2);
    expect(sheet.selection.rightCol).toBe(2);
    expect(sheet.activeCell).toEqual(new CellRef(0, 0));

    // Plain drag: mousedown A1, document-level mousemove to B2 — the anchor stands.
    mousedown({ clientX: 48, clientY: 14 });
    document.dispatchEvent(new MouseEvent('mousemove', { clientX: 96 + 48, clientY: 28 + 14 }));
    expect(sheet.selection.bottomRow).toBe(1);
    expect(sheet.selection.rightCol).toBe(1);
    document.dispatchEvent(new MouseEvent('mouseup', {}));
  });

  it('grabbing the fill handle pours the source, one undo for the whole drag', () => {
    const sheet = new SheetController(50, 8);
    sheet.document.setCell(new CellRef(0, 0), 'seed');
    const node = surfaceFor(sheet);

    // A1 selected by default; its bottom-right corner is at (96, 28).
    const grab = new MouseEvent('mousedown', { clientX: 95, clientY: 27 });
    Object.defineProperty(grab, 'currentTarget', { value: makeTarget() });
    events(node)['mousedown'](grab);
    expect(sheet.filling).toBe(true);

    document.dispatchEvent(new MouseEvent('mousemove', { clientX: 95, clientY: 28 * 2 + 14 }));
    expect(sheet.fillTarget).not.toBeNull();
    document.dispatchEvent(new MouseEvent('mouseup', {}));

    expect(sheet.document.getCell(new CellRef(1, 0))).toBe('seed');
    expect(sheet.document.getCell(new CellRef(2, 0))).toBe('seed');
    sheet.undo();
    expect(sheet.document.getCell(new CellRef(1, 0))).toBe('');
  });

  it('a double click edits the cell under it, starting from its value', () => {
    const sheet = new SheetController(50, 8);
    sheet.document.setCell(new CellRef(1, 1), '42');
    const node = surfaceFor(sheet);
    // The platform's click count rides the press: the second press of a double click says 2.
    const press = (detail: number) => {
      const event = new MouseEvent('mousedown', { clientX: 96 + 48, clientY: 28 + 14, detail });
      Object.defineProperty(event, 'currentTarget', { value: makeTarget() });
      events(node)['mousedown'](event);
      document.dispatchEvent(new MouseEvent('mouseup', { clientX: 96 + 48, clientY: 28 + 14 }));
    };

    press(1);
    press(2);

    expect(sheet.activeCell).toEqual(new CellRef(1, 1));
    expect(sheet.editing).toBe(true);
    expect(sheet.draft).toBe('42');
  });

  it('reads a press against the window the engine was handed', () => {
    const sheet = new SheetController(200, 8);
    // A scrolled window: the surface's first row draws the sheet's row 40.
    sheet.grid = new SheetGrid(Point.zero, 40, 0);
    const node = surfaceFor(sheet);

    const event = new MouseEvent('mousedown', { clientX: 96 + 48, clientY: 28 * 2 + 14 });
    Object.defineProperty(event, 'currentTarget', { value: makeTarget() });
    events(node)['mousedown'](event);
    document.dispatchEvent(new MouseEvent('mouseup', {}));

    expect(sheet.activeCell).toEqual(new CellRef(42, 1));
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
    target.focus = (options?: FocusOptions) => {
      focusOptions.push(options);
      if (!options?.preventScroll) top -= 127;
    };

    const event = new MouseEvent('mousedown', { clientX: 96 + 48, clientY: 28 * 2 + 14 });
    Object.defineProperty(event, 'currentTarget', { value: target });
    events(node)['mousedown'](event);
    document.dispatchEvent(new MouseEvent('mouseup', {}));

    expect(sheet.activeCell).toEqual(new CellRef(2, 1));
    expect(focusOptions).toEqual([{ preventScroll: true }]);
  });

  it('leaves ⌘C to the browser, whose copy event carries the text', () => {
    const sheet = new SheetController(50, 8);
    const node = surfaceFor(sheet);

    const copyKey = new KeyboardEvent('keydown', { key: 'c', metaKey: true, cancelable: true });
    events(node)['keydown'](copyKey);

    expect(copyKey.defaultPrevented).toBe(false);
    expect(sheet.editing).toBe(false);
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
    events(node)['copy'](copyEvent);
    expect(written['text/plain']).toContain('a\tb');

    const target = new SheetController(50, 8);
    const targetNode = surfaceFor(target);
    const pasteEvent = new Event('paste') as ClipboardEvent;
    Object.defineProperty(pasteEvent, 'clipboardData', {
      value: { getData: () => 'x\ty' },
    });
    events(targetNode)['paste'](pasteEvent);
    expect(target.document.getCell(new CellRef(0, 0))).toBe('x');
    expect(target.document.getCell(new CellRef(0, 1))).toBe('y');
  });
});
