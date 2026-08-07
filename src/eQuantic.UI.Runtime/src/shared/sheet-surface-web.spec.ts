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
