/**
 * A table whose columns a person reorders and resizes (#1538), driven as a mouse and a keyboard drive it:
 * through the transpiled `DataTable`, so the reports reach the handlers an app gives it, and the app keeps
 * the order and the widths the way a page does. jsdom lays nothing out, so each heading is given the box a
 * header row would draw, and `elementsFromPoint` answers from those boxes. The server's half is
 * `DataTableColumnsTests`, Photon's `DataTableColumnsHostTests`.
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { StatefulComponent } from '../core/component';
import { photonTheme } from '../shared/design-system.generated';
import { setPhotonTheme } from '../shared/photon-context';
import { GridTrack, Text } from '../shared/vocabulary';
import { DataColumn } from '../shared/components/DataColumn';
import { DataRow } from '../shared/components/DataRow';
import { DataTable } from '../shared/components/DataTable';
import { SdkStrings } from '../shared/components/SdkStrings';
import { resetSortableController } from './sortable';
import { resetDraggableController } from './draggable';

setPhotonTheme(photonTheme);

const reordered: number[][] = [];
const resized: [number, number][] = [];

/** A page that keeps the table's order and widths: Name fluid, Size 120 and Kind 100 wide. */
class Page extends StatefulComponent {
  columns = [
    new DataColumn('Name', GridTrack.flex()),
    new DataColumn('Size', GridTrack.fixed(120)),
    new DataColumn('Kind', GridTrack.fixed(100)),
  ];
  order: number[] | null = null;

  build(): DataTable {
    return new DataTable(
      this.columns,
      [new DataRow('a', [new Text('alpha'), new Text('1'), new Text('doc')])],
      {
        columnOrder: this.order,
        onColumnsReordered: (order: number[]) => {
          reordered.push([...order]);
          this.setState(() => (this.order = [...order]));
        },
        onColumnResized: (column: number, width: number) => {
          resized.push([column, width]);
          this.setState(() => (this.columns[column] = this.columns[column].with({ track: GridTrack.fixed(width) })));
        },
      },
    );
  }
}

type Box = { left: number; top: number; width: number; height: number };
const boxes = new Map<Element, Box>();

function give(element: Element, box: Box): void {
  boxes.set(element, box);
  (element as HTMLElement).getBoundingClientRect = () =>
    ({ ...box, right: box.left + box.width, bottom: box.top + box.height, x: box.left, y: box.top, toJSON: () => box }) as DOMRect;
}

/** The headings laid out from x 0, Name 200 wide and the others their widths, 40 tall. */
function mount(): HTMLElement {
  const container = document.createElement('div');
  document.body.appendChild(container);
  new Page().mount(container);
  const list = container.querySelector('[data-eq-sort]') as HTMLElement;
  give(list, { left: 0, top: 0, width: 420, height: 40 });
  const widths = [200, 120, 100];
  let left = 0;
  Array.from(list.querySelectorAll('[data-eq-sort-item]')).forEach((item, at) => {
    give(item, { left, top: 0, width: widths[at], height: 40 });
    left += widths[at];
  });
  (document as unknown as { elementsFromPoint: (x: number, y: number) => Element[] }).elementsFromPoint = (x, y) =>
    Array.from(boxes.entries())
      .filter(([, b]) => x >= b.left && x < b.left + b.width && y >= b.top && y < b.top + b.height)
      .map(([element]) => element)
      .sort((p, q) => (p.contains(q) ? 1 : q.contains(p) ? -1 : 0));
  return container;
}

const heading = (container: HTMLElement, index: number) =>
  container.querySelector(`[data-eq-sort-item="${index}"]`) as HTMLElement;

const handle = (container: HTMLElement, header: string) =>
  container.querySelector(`[role="separator"][aria-label="${SdkStrings.resizeColumn(header)}"]`) as HTMLElement;

function pointer(target: EventTarget, type: string, x: number, y: number): void {
  target.dispatchEvent(
    new PointerEvent(type, { clientX: x, clientY: y, bubbles: true, cancelable: true, pointerType: 'mouse', button: 0 }),
  );
}

/** The page's state change drawn: past the render's backstop, since the frames are stubbed out. */
const settle = (): Promise<void> => new Promise((resolve) => setTimeout(resolve, 120));

function press(key: string): KeyboardEvent {
  const event = new KeyboardEvent('keydown', { key, bubbles: true, cancelable: true });
  (document.activeElement ?? document.body).dispatchEvent(event);
  return event;
}

describe('a table whose columns a person reorders and resizes', () => {
  beforeEach(() => {
    vi.stubGlobal('requestAnimationFrame', () => 0);
    vi.stubGlobal('cancelAnimationFrame', () => undefined);
  });

  afterEach(async () => {
    // A drag's swallowed click is waited for for 50 ms: the next case's first click is not its.
    await new Promise((resolve) => setTimeout(resolve, 60));
    resetSortableController();
    resetDraggableController();
    vi.unstubAllGlobals();
    document.body.innerHTML = '';
    boxes.clear();
    reordered.length = 0;
    resized.length = 0;
  });

  it('reports the order a heading dragged past the next one leaves, and the page draws it', async () => {
    const container = mount();
    // From Name (0..200) past Size's middle (260) and short of Kind's (370).
    pointer(heading(container, 0), 'pointerdown', 100, 20);
    pointer(document, 'pointermove', 120, 20);
    pointer(document, 'pointermove', 300, 20);
    pointer(document, 'pointerup', 300, 20);

    expect(reordered).toEqual([[1, 0, 2]]);
    await settle();
    const drawn = Array.from(container.querySelectorAll('[data-eq-sort-item]')).map((item) =>
      item.getAttribute('data-eq-sort-item'),
    );
    expect(drawn).toEqual(['1', '0', '2']);
  });

  it('moves a heading by the keyboard, saying each step in the SDK words for columns', () => {
    const container = mount();
    heading(container, 2).focus();
    expect(press(' ').defaultPrevented).toBe(true);
    press('ArrowLeft');
    press(' ');

    expect(reordered).toEqual([[0, 2, 1]]);
    // Named by its column, in the SDK's words, the header row by the SDK's word for columns.
    expect(document.querySelector('[data-eq-sort-announcer]')?.textContent).toBe(
      `Dropped Kind at position 2 of 3 in ${SdkStrings.tableColumns}.`,
    );
  });

  it('reports the width a handle is dragged to, live, and never picks the column up', async () => {
    const container = mount();
    const edge = handle(container, 'Size').querySelector('[data-eq-drag]') as HTMLElement;
    pointer(edge, 'pointerdown', 316, 20);
    pointer(document, 'pointermove', 336, 20);
    pointer(document, 'pointermove', 356, 20);
    pointer(document, 'pointerup', 356, 20);

    expect(resized.length).toBeGreaterThan(0);
    expect(resized[resized.length - 1]).toEqual([1, 160]);
    expect(reordered).toEqual([]);
    await settle();
    expect(handle(container, 'Size').getAttribute('aria-valuenow')).toBe('160');
  });

  it('steps a handle by the arrows from the width the page kept', async () => {
    const container = mount();
    handle(container, 'Size').focus();
    expect(press('ArrowRight').defaultPrevented).toBe(true);
    expect(resized).toEqual([[1, 136]]);
    await settle();
    handle(container, 'Size').focus();
    press('ArrowLeft');
    await settle();
    handle(container, 'Size').focus();
    press('ArrowLeft');
    expect(resized).toEqual([[1, 136], [1, 120], [1, 104]]);
    expect(reordered).toEqual([]);
  });
});
