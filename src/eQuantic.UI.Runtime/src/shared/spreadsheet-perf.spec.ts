/**
 * What a spreadsheet of 100 000 rows by 100 columns costs on the web: a keystroke, which builds the
 * sheet again, lowers it and reconciles the window, a scroll step inside the cells already built,
 * which moves the bands' layers and lowers nothing, and the status bar's summary of the whole sheet
 * selected over a million cells in use, which a key that moves nothing reads from the cache. The ceilings are an order of magnitude loose, as
 * Photon's harness keeps its own (SpreadsheetPerfTests), because CI machines are shared; what they catch
 * is a sheet that lowers on every step, or builds what it does not show.
 */

import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { Spreadsheet } from './components/Spreadsheet';
import { SheetController } from './components/SheetController';
import { CellRef } from './components/CellRef';
import { SizeValue } from './value-types';

const KEYSTROKE_CEILING_MS = 150;
const STEP_CEILING_MS = 5;
const SUMMARY_CEILING_MS = 3000;
const CACHED_CEILING_MS = 5;

/** The frames the runtime asks for, run when the spec says: a flush timed by itself, with no wait. */
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

async function open(): Promise<{ grid: HTMLElement; view: HTMLElement; sheet: SheetController }> {
  const sheet = new SheetController(100_000, 100);
  for (let row = 0; row < 200; row++)
    for (let column = 0; column < 4; column++) sheet.document.setCell(new CellRef(row, column), `r${row}c${column}`);
  const container = document.createElement('div');
  document.body.appendChild(container);
  new Spreadsheet(sheet, { width: SizeValue.fill, height: SizeValue.fill }).mount(container);
  const view = container.querySelector('[data-eq-table]') as HTMLElement;
  Object.defineProperty(view, 'clientWidth', { value: 1440, configurable: true });
  Object.defineProperty(view, 'clientHeight', { value: 900, configurable: true });
  await settle();
  // The keys reach the sheet through its door, the textarea that holds its stop of the Tab order.
  const grid = container.querySelector('[role="grid"] textarea.eq-code-input') as HTMLElement;
  grid.focus = () => {};
  return { grid, view: container.querySelector('[data-eq-table]') as HTMLElement, sheet };
}

/** The cells the body's band holds: the same elements when nothing was lowered again. */
const cells = (view: HTMLElement) => [...view.children[0].firstElementChild!.children];

function key(grid: HTMLElement, name: string): void {
  grid.dispatchEvent(new KeyboardEvent('keydown', { key: name, bubbles: true, cancelable: true }));
}

function wheel(view: HTMLElement, deltaX: number, deltaY: number): void {
  view.dispatchEvent(new WheelEvent('wheel', { bubbles: true, cancelable: true, deltaX, deltaY }));
}

describe('a spreadsheet of 100 000 × 100 on the web', () => {
  const requestFrame = window.requestAnimationFrame;

  beforeEach(() => {
    document.body.innerHTML = '';
    frames.length = 0;
    window.requestAnimationFrame = ((callback: FrameRequestCallback) => frames.push(callback)) as never;
    window.matchMedia = ((query: string) => ({ matches: query.includes('reduce'), media: query })) as never;
  });

  afterEach(() => {
    window.requestAnimationFrame = requestFrame;
  });

  it('builds the window again on a keystroke, and nothing past it', async () => {
    const { grid, view, sheet } = await open();
    for (let i = 0; i < 5; i++) {
      key(grid, i % 2 === 0 ? 'ArrowRight' : 'ArrowLeft');
      await settle();
    }

    const keys = 40;
    const started = performance.now();
    for (let i = 0; i < keys; i++) {
      key(grid, i % 2 === 0 ? 'ArrowRight' : 'ArrowLeft');
      await settle();
    }
    const each = (performance.now() - started) / keys;
    const built = cells(view).length;

    console.log(`web: a keystroke at 100,000 × 100: ${each.toFixed(2)} ms, ${built} cells in the window (alarm ${KEYSTROKE_CEILING_MS} ms)`);
    expect(sheet.activeCell).toEqual(new CellRef(0, 1));
    expect(built, 'the window and its overscan, never a row or a column whole').toBeLessThan(1500);
    expect(built, 'the window was built').toBeGreaterThan(100);
    expect(each, 'a keystroke builds a screenful').toBeLessThan(KEYSTROKE_CEILING_MS);
  });

  it('builds the window and its editor again on a character typed into an entry', async () => {
    const { grid, sheet } = await open();
    const type = (text: string) =>
      grid.dispatchEvent(new InputEvent('beforeinput', { inputType: 'insertText', data: text, bubbles: true, cancelable: true }));
    for (let i = 0; i < 5; i++) {
      type('w');
      await settle();
    }

    const keys = 40;
    const started = performance.now();
    for (let i = 0; i < keys; i++) {
      type(i % 2 === 0 ? 'a' : 'b');
      await settle();
    }
    const each = (performance.now() - started) / keys;

    console.log(`web: a character into an entry at 100,000 × 100: ${each.toFixed(2)} ms (alarm ${KEYSTROKE_CEILING_MS} ms)`);
    expect(sheet.editing).toBe(true);
    expect(sheet.entry.document.text.length).toBe(45);
    expect(each, 'a character builds a screenful and its editor').toBeLessThan(KEYSTROKE_CEILING_MS);
  });

  it('moves the layers on a step inside the built cells, lowering nothing', async () => {
    const { view } = await open();
    const before = cells(view);

    const steps = 200;
    const started = performance.now();
    for (let i = 0; i < steps; i++) {
      wheel(view, i % 2 === 0 ? 2 : -2, i % 2 === 0 ? 2 : -2);
      runFrames();
    }
    const each = (performance.now() - started) / steps;
    await settle();
    const after = cells(view);

    console.log(`web: a scroll step inside the window at 100,000 × 100: ${each.toFixed(3)} ms, ${after.length} cells (alarm ${STEP_CEILING_MS} ms)`);
    expect(after.length).toBe(before.length);
    expect(after.every((cell, index) => cell === before[index]), 'a step inside the built cells lowers none').toBe(true);
    expect(each, 'a step inside the window only moves layers').toBeLessThan(STEP_CEILING_MS);
  });

  it('sums a million cells in use with the whole sheet selected, and reads the sum again from the cache', () => {
    const sheet = new SheetController(100_000, 100);
    for (let row = 0; row < 100_000; row++)
      for (let column = 0; column < 10; column++) sheet.document.setCell(new CellRef(row, column), '1.5');
    sheet.selectAll();

    const started = performance.now();
    const summary = sheet.summary;
    const took = performance.now() - started;
    sheet.handleKey('Backspace', 4, 'standard', null);
    const again = performance.now();
    const cached = sheet.summary;
    const tookAgain = performance.now() - again;

    console.log(`web: the summary of a million cells: ${took.toFixed(1)} ms, again ${tookAgain.toFixed(3)} ms (alarm ${SUMMARY_CEILING_MS} ms)`);
    expect(summary.count).toBe(1_000_000);
    expect(summary.sum.toString()).toBe('1500000.0');
    expect(cached, 'a key that moved nothing answers the summary it kept').toBe(summary);
    expect(took, 'the summary reads the cells in use, never the sheet').toBeLessThan(SUMMARY_CEILING_MS);
    expect(tookAgain, 'nothing moved, so nothing is read again').toBeLessThan(CACHED_CEILING_MS);
  });
});
