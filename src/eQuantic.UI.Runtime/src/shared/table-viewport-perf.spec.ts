/**
 * What a table costs on the web per scroll, over Excel's whole sheet: a step inside the cells already
 * built, which moves the bands' layers and lowers nothing, and a step that leaves them, which builds,
 * lowers and reconciles the window again. The ceilings are an order of magnitude loose, as Photon's
 * harness keeps its own (TableViewportPerfTests), because CI machines are shared; what they catch is a
 * table that lowers on every step, or builds what it does not show.
 */

import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { photonTheme } from './design-system.generated';
import { TableView } from './components/TableView';
import { UniformTableSpans } from './components/UniformTableSpans';
import { Text } from './vocabulary';

const STEP_CEILING_MS = 5;
const REBUILD_CEILING_MS = 150;

/** The frames the runtime asks for, run when the spec says: a flush timed by itself, with no wait. */
const frames: FrameRequestCallback[] = [];
const runFrames = () => {
  while (frames.length > 0) frames.shift()!(performance.now());
};

function wheel(view: HTMLElement, deltaX: number, deltaY: number): void {
  view.dispatchEvent(new WheelEvent('wheel', { bubbles: true, cancelable: true, deltaX, deltaY }));
}

async function open(): Promise<{ view: HTMLElement; built: { count: number } }> {
  const built = { count: 0 };
  const container = document.createElement('div');
  document.body.appendChild(container);
  new TableView(
    new UniformTableSpans(1_048_576 + 1, 28),
    new UniformTableSpans(16_384 + 1, 96),
    (row: number, column: number) => {
      built.count++;
      return new Text(`${row}:${column}`, 'caption', photonTheme.textPrimary);
    },
    { pinnedRows: 1, pinnedColumns: 1 },
  ).mount(container);
  const view = container.querySelector('[data-eq-table]') as HTMLElement;
  Object.defineProperty(view, 'clientWidth', { value: 1440, configurable: true });
  Object.defineProperty(view, 'clientHeight', { value: 900, configurable: true });
  // The commit measures the window and reports it; the table builds for it on the next frame.
  await Promise.resolve();
  runFrames();
  await Promise.resolve();
  return { view, built };
}

describe('a table on the web, per scroll, over Excel’s whole sheet', () => {
  const requestFrame = window.requestAnimationFrame;

  beforeEach(() => {
    document.body.innerHTML = '';
    frames.length = 0;
    window.requestAnimationFrame = ((callback: FrameRequestCallback) => frames.push(callback)) as never;
    // Reduced motion, so a step lands at once rather than gliding over frames.
    window.matchMedia = ((query: string) => ({ matches: query.includes('reduce'), media: query })) as never;
  });

  afterEach(() => {
    window.requestAnimationFrame = requestFrame;
  });

  it('moves the layers on a step inside the built cells, lowering nothing', async () => {
    const { view, built } = await open();
    const before = built.count;
    const cells = view.querySelectorAll('span').length;

    const steps = 200;
    const started = performance.now();
    for (let i = 0; i < steps; i++) {
      wheel(view, i % 2 === 0 ? 2 : -2, i % 2 === 0 ? 2 : -2);
      runFrames();
    }
    const each = (performance.now() - started) / steps;
    await Promise.resolve();

    console.log(`web: a scroll step inside the window at 1,048,576 × 16,384: ${each.toFixed(3)} ms (alarm ${STEP_CEILING_MS} ms)`);
    expect(built.count, 'a step inside the built cells builds none').toBe(before);
    expect(view.querySelectorAll('span').length).toBe(cells);
    expect(each, 'a step inside the window only moves layers').toBeLessThan(STEP_CEILING_MS);
    // And the steps reached the model: back at the start, one more moves the body's layer by it.
    wheel(view, 0, 2);
    expect((view.children[0].firstElementChild as HTMLElement).style.transform).toBe('translate(0px, -2px)');
  });

  it('rebuilds only the window on a step that leaves the built cells', async () => {
    const { view, built } = await open();
    const before = built.count;

    const steps = 10;
    const started = performance.now();
    for (let i = 0; i < steps; i++) {
      // A page down and across: every visible row and column leaves the cells built.
      wheel(view, 1500, 900);
      runFrames();
      await Promise.resolve();
    }
    const each = (performance.now() - started) / steps;
    const cellsEach = (built.count - before) / steps;

    console.log(
      `web: a window rebuild at 1,048,576 × 16,384: ${each.toFixed(2)} ms, ${cellsEach.toFixed(0)} cells built ` +
        `(alarm ${REBUILD_CEILING_MS} ms)`,
    );
    expect(cellsEach, 'a rebuild builds the window and its overscan, never a row or a column whole').toBeLessThan(1500);
    expect(cellsEach, 'the step has to leave the built cells, or nothing was rebuilt').toBeGreaterThan(100);
    expect(each, 'a rebuild costs a screenful').toBeLessThan(REBUILD_CEILING_MS);
  });
});
