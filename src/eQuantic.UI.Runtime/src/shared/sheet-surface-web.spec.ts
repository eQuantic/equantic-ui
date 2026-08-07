/**
 * The spreadsheet's WEB half, end to end: the lowered surface's keydown drives the SAME transpiled
 * SheetKeymap the native host calls, typing edits Excel's way, and copy/paste speak TSV through
 * the browser's own clipboard events. A path nobody runs is a path that does not work — this runs
 * it with real DOM events.
 */

import { describe, expect, it } from 'vitest';
import { lowerVisualNode, type LoweringContext } from './lowering';
import { effectiveStyle } from './style-atomizer';
import { photonTheme } from './design-system.generated';
import { SheetController } from './__transpiled__/SheetController';
import { CellRef } from './__transpiled__/CellRef';
import { SheetRange } from './__transpiled__/SheetRange';
import type { HtmlNode } from '../core/types';

type Handler = (event: Event) => void;

function surfaceFor(sheet: SheetController): HtmlNode {
  return lowerVisualNode(
    {
      nodeKind: 'sheetSurface',
      child: { nodeKind: 'column', children: [] },
      controller: sheet,
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
    expect(sheet.selection.colCount).toBe(2, );
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
    mousedown({ clientX: 48, clientY: 14 });                       // A1
    mousedown({ clientX: 96 * 2 + 48, clientY: 28 * 2 + 14, shiftKey: true });   // shift+click C3
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
