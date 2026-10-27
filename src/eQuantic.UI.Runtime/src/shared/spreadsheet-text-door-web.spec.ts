/**
 * The spreadsheet's text door on the web (#983, D5), with the events a browser sends to it: the
 * composition of a dead key and of an input method, an emoji's input, a character typed with AltGr, the
 * clipboard while a cell is edited, the browser's own reload key, the Go To chord, Escape releasing Tab
 * inside a dialog's trap (#732), and the formula bar and the name box mounted beside the grid. The C#
 * twins are SheetEntryTests, SpreadsheetTextDoorTests and SpreadsheetBarsTests, on Photon.
 */

import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { lowerVisualNode, type LoweringContext } from './lowering';
import { photonTheme } from './design-system.generated';
import { SheetController } from './components/SheetController';
import { CellRef } from './components/CellRef';
import { SheetRange } from './components/SheetRange';
import { Spreadsheet } from './components/Spreadsheet';
import { FormulaBar } from './components/FormulaBar';
import { NameBox } from './components/NameBox';
import { SizeValue } from './value-types';
import { Reconciler } from '../dom/reconciler';
import { commitFocusTraps, resetFocusTraps } from '../dom/focus-trap';
import type { HtmlNode } from '../core/types';

type Handler = (event: Event) => void;

const B2 = new CellRef(1, 1);

function surfaceFor(sheet: SheetController): HtmlNode {
  return lowerVisualNode(
    { nodeKind: 'sheetSurface', child: { nodeKind: 'column', children: [] }, model: sheet, onChanged: () => {} } as never,
    { textPrimary: photonTheme.textPrimary } as LoweringContext,
  );
}

/** The surface's door: the textarea its keys, text, compositions and clipboard come through. */
const door = (node: HtmlNode) => node.children.find((child) => child.tag === 'textarea') as HtmlNode;
const on = (node: HtmlNode) => (door(node) as { events: Record<string, Handler> }).events;

function fire<T extends Event>(node: HtmlNode, type: string, event: T): T {
  on(node)[type](event);
  return event;
}

const key = (node: HtmlNode, name: string, init: KeyboardEventInit = {}) =>
  fire(node, 'keydown', new KeyboardEvent('keydown', { key: name, cancelable: true, ...init }));

const input = (node: HtmlNode, data: string, inputType = 'insertText') =>
  fire(node, 'beforeinput', new InputEvent('beforeinput', { inputType, data, cancelable: true }));

/** A composition's step, and its end, as Chromium sends them: the textarea keeps nothing of either. Plain
 * objects, as the code editor's spec passes them: happy-dom's CompositionEvent drops the `data` it is
 * given, which a browser's never does. */
function composing(node: HtmlNode, data: string): void {
  (on(node)['compositionupdate'] as unknown as (event: unknown) => void)({ data });
}

function composed(node: HtmlNode, data: string): void {
  (on(node)['compositionend'] as unknown as (event: unknown) => void)({ data, target: { value: data } });
}

function sheetAtB2(): { sheet: SheetController; node: HtmlNode } {
  const sheet = new SheetController(50, 8);
  sheet.selection = new SheetRange(B2);
  return { sheet, node: surfaceFor(sheet) };
}

describe("the spreadsheet's text door", () => {
  it('takes dead keys, AltGr and an emoji as the text they type', () => {
    const { sheet, node } = sheetAtB2();

    // ´ then a: the accent is marked, the letter commits, and Chromium may echo the commit as input.
    composing(node, '´');
    composed(node, 'á');
    input(node, 'á');
    composing(node, '˜');
    composed(node, 'ã');
    // AltGr+C on a Portuguese layout: a keydown with Ctrl and Alt the sheet does not claim, then the text.
    const altGr = key(node, 'ç', { ctrlKey: true, altKey: true });
    expect(altGr.defaultPrevented, 'AltGr is no command').toBe(false);
    input(node, 'ç');
    input(node, '\u{1F44D}\u{1F3FD}');

    expect(sheet.editing).toBe(true);
    expect(sheet.entry.document.text).toBe('áãç\u{1F44D}\u{1F3FD}');
    key(node, 'Backspace');
    expect(sheet.entry.document.text, 'one Backspace takes the whole thumbs-up').toBe('áãç');
  });

  it('shows a Japanese composition in the entry and commits it as one edit', () => {
    const { sheet, node } = sheetAtB2();

    composing(node, 'に');
    composing(node, 'にほんご');
    expect(sheet.entry.document.text).toBe('にほんご');
    expect(sheet.entry.composition).not.toBeNull();
    composed(node, '日本語');

    expect(sheet.entry.document.text).toBe('日本語');
    expect(sheet.entry.composition).toBeNull();
    key(node, 'z', { metaKey: true });
    expect(sheet.entry.document.text).toBe('');
  });

  it('copies and pastes the entry while a cell is edited', () => {
    const { sheet, node } = sheetAtB2();
    input(node, 'hello world');
    key(node, 'F2');
    key(node, 'ArrowLeft', { shiftKey: true, ctrlKey: true });

    const written: Record<string, string> = {};
    const copy = new Event('copy', { cancelable: true }) as ClipboardEvent;
    Object.defineProperty(copy, 'clipboardData', { value: { setData: (type: string, value: string) => (written[type] = value) } });
    on(node)['copy'](copy);
    expect(written['text/plain']).toBe('world');
    expect(sheet.editing).toBe(true);

    key(node, 'End');
    const paste = new Event('paste', { cancelable: true }) as ClipboardEvent;
    Object.defineProperty(paste, 'clipboardData', { value: { getData: () => '!\t?' } });
    on(node)['paste'](paste);
    expect(sheet.entry.document.text).toBe('hello world!\t?');
    expect(sheet.document.getCell(new CellRef(1, 2)), 'no other cell changed').toBe('');
  });

  it('leaves the browser its reload key, and takes the Go To chord for a name box', () => {
    const { sheet, node } = sheetAtB2();
    sheet.nameBox.placed();
    const version = sheet.nameBox.focusVersion;

    const reload = key(node, 'F5');
    expect(reload.defaultPrevented, 'F5 stays the browser’s').toBe(false);
    expect(sheet.nameBox.focusVersion).toBe(version);

    const goTo = key(node, 'g', { ctrlKey: true });
    const claimed = goTo.defaultPrevented || sheet.nameBox.focusVersion !== version;
    expect(claimed, 'Ctrl+G, or ⌃G on a Mac, takes the keyboard to the name box').toBe(true);
  });

  it('leaves the Tab after Escape to the browser, and keeps the active cell', () => {
    const { sheet, node } = sheetAtB2();

    expect(key(node, 'Escape').defaultPrevented).toBe(false);
    expect(key(node, 'Tab').defaultPrevented, 'the browser moves the keyboard on').toBe(false);
    expect(sheet.activeCell).toEqual(B2);

    key(node, 'ArrowRight');
    expect(key(node, 'Tab').defaultPrevented, 'another key took Tab back').toBe(true);
  });
});

/**
 * A sheet at either end of a dialog (#732): the trap cycles a Tab the sheet left to it after Escape, as
 * it does a code editor's (`code-editor.spec.ts`), and never one the sheet took.
 */
describe('a sheet at either end of a dialog', () => {
  afterEach(() => {
    document.body.innerHTML = '';
    resetFocusTraps();
  });

  function inDialog(sheetFirst: boolean) {
    document.body.innerHTML = '';
    resetFocusTraps();
    const sheet = new SheetController(50, 8);
    sheet.selection = new SheetRange(B2);
    const layer = document.createElement('div');
    layer.setAttribute('role', 'dialog');
    layer.setAttribute('aria-modal', 'true');
    layer.setAttribute('tabindex', '-1');
    layer.setAttribute('data-eq-trap', '');
    const host = document.createElement('div');
    const button = document.createElement('button');
    button.textContent = 'Close';
    if (sheetFirst) layer.append(host, button);
    else layer.append(button, host);
    document.body.appendChild(layer);
    new Reconciler().reconcile(host, null, surfaceFor(sheet));
    commitFocusTraps();
    const textarea = host.querySelector('textarea')!;
    textarea.focus();
    return { sheet, textarea, button };
  }

  const press = (target: HTMLElement, name: string, shift = false) =>
    target.dispatchEvent(new KeyboardEvent('keydown', { key: name, shiftKey: shift, bubbles: true, cancelable: true }));

  it('keeps Tab in the grid until Escape releases it, then the trap cycles it', () => {
    const last = inDialog(false);
    press(last.textarea, 'Tab');
    expect(document.activeElement, 'the sheet keeps the keyboard').toBe(last.textarea);
    expect(last.sheet.activeCell).toEqual(new CellRef(1, 2));

    press(last.textarea, 'Escape');
    press(last.textarea, 'Tab');
    expect(document.activeElement, 'Tab from the last control wraps to the first').toBe(last.button);

    const first = inDialog(true);
    press(first.textarea, 'Escape');
    press(first.textarea, 'Tab', true);
    expect(document.activeElement, 'Shift+Tab from the first control wraps to the last').toBe(first.button);
    expect(first.sheet.activeCell).toEqual(B2);
  });
});

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

describe('the formula bar and the name box beside a sheet', () => {
  const requestFrame = window.requestAnimationFrame;

  beforeEach(() => {
    document.body.innerHTML = '';
    frames.length = 0;
    window.requestAnimationFrame = ((callback: FrameRequestCallback) => frames.push(callback)) as never;
  });

  afterEach(() => {
    window.requestAnimationFrame = requestFrame;
  });

  async function mount() {
    const sheet = new SheetController(50, 8);
    sheet.selection = new SheetRange(B2);
    const box = document.createElement('div');
    const bar = document.createElement('div');
    const grid = document.createElement('div');
    document.body.append(box, bar, grid);
    new NameBox(sheet).mount(box);
    new FormulaBar(sheet).mount(bar);
    new Spreadsheet(sheet, { width: SizeValue.fill, height: SizeValue.fill }).mount(grid);
    await settle();
    const textarea = grid.querySelector('.eq-sheet-surface textarea.eq-code-input') as HTMLTextAreaElement;
    return { sheet, box, bar, grid, textarea };
  }

  it('shows the entry in the bar keystroke by keystroke, and the address in the box', async () => {
    const { sheet, box, bar, textarea } = await mount();
    expect(box.textContent).toContain('B2');

    for (const typed of ['a', 'ab', 'abc']) {
      textarea.dispatchEvent(
        new InputEvent('beforeinput', { inputType: 'insertText', data: typed[typed.length - 1], bubbles: true, cancelable: true }),
      );
      await settle();
      expect(sheet.entry.document.text).toBe(typed);
      expect(bar.textContent, `the bar shows ${typed}`).toContain(typed);
    }
  });
});
