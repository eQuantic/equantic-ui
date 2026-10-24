/**
 * A table whose axes shrink IN PLACE, the same objects answering fewer rows and columns, as a
 * spreadsheet's document does after a delete. The window built before reaches past the new end, and
 * the table chooses it again rather than asking its builder for cells the grid no longer has. The C#
 * twin is TableViewportTests.AGridWhoseAxesShrinkInPlace_IsAskedOnlyForTheCellsItHas.
 */

import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { photonTheme } from './design-system.generated';
import { TableView } from './components/TableView';
import { TableSpans, Text } from './vocabulary';

/** An axis whose count changes in place. */
class LiveSpans extends TableSpans {
  constructor(public size: number, private readonly extent: number) {
    super();
  }
  get count(): number {
    return this.size;
  }
  get total(): number {
    return this.size * this.extent;
  }
  extentOf(): number {
    return this.extent;
  }
  offsetOf(index: number): number {
    return Math.min(Math.max(index, 0), this.size) * this.extent;
  }
  indexAt(offset: number): number {
    return this.size === 0 ? 0 : Math.min(Math.max(Math.trunc(offset / this.extent), 0), this.size - 1);
  }
}

const frames: FrameRequestCallback[] = [];
async function settle(): Promise<void> {
  for (let i = 0; i < 4; i++) {
    await Promise.resolve();
    while (frames.length > 0) frames.shift()!(performance.now());
  }
}

describe('a table whose axes shrink in place', () => {
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

  it('is asked only for the cells it has', async () => {
    const rows = new LiveSpans(11, 28);
    const columns = new LiveSpans(11, 96);
    const asked: string[] = [];
    const table = new TableView(
      rows,
      columns,
      (row: number, column: number) => {
        if (row >= rows.count || column >= columns.count) asked.push(`${row}:${column}`);
        return new Text(`${row}:${column}`, 'caption', photonTheme.textPrimary);
      },
      { pinnedRows: 1, pinnedColumns: 1 },
    );
    const container = document.createElement('div');
    document.body.appendChild(container);
    table.mount(container);
    const view = container.querySelector('[data-eq-table]') as HTMLElement;
    Object.defineProperty(view, 'clientWidth', { value: 1440, configurable: true });
    Object.defineProperty(view, 'clientHeight', { value: 900, configurable: true });
    await settle();
    expect([...container.querySelectorAll('span')].some((span) => span.textContent === '10:10')).toBe(true);

    rows.size = 7;
    columns.size = 7;
    (table as unknown as { setState(update: () => void): void }).setState(() => {});
    await settle();

    expect(asked, 'no cell past the end of an axis').toEqual([]);
    expect([...container.querySelectorAll('span')].some((span) => span.textContent === '6:6')).toBe(true);
  });
});
