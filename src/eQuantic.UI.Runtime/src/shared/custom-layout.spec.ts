/**
 * A custom layout in the browser (#1556): the page writes the children in wrappers at the box's origin with the box
 * hidden, and the pass's commit runs the app's delegate, measures each child under the constraints it asks for and
 * places it. jsdom lays nothing out, so the measurements are the one thing faked here: the layout's box under size
 * containment answers the room the test gives it, and a child's box answers its natural size kept inside the bounds
 * the commit set on it, which is the box rule the browser applies.
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { StatefulComponent } from '../core/component';
import { CustomMultiChildLayout, LayoutId, Text } from './vocabulary';
import { BoxConstraints, Point, Size } from './value-types';
import { MultiChildLayoutDelegate, SingleChildLayout, SingleChildLayoutDelegate, type LayoutChildren } from './layout-delegates';
import type { VisualNodeValue } from './nodes';

const nextFrame = () =>
  new Promise<void>((resolve) => requestAnimationFrame(() => requestAnimationFrame(() => resolve())));

/** The width the parent gives the layout's box, which a test moves. */
let roomWidth = 320;
/** Each child's natural height, by the text it holds. */
const heights: Record<string, number> = { 'item-0': 100, 'item-1': 50, 'item-2': 70, 'item-3': 30 };
const observers: Array<() => void> = [];

const bound = (style: CSSStyleDeclaration, property: string, fallback: number) => {
  const value = style.getPropertyValue(property);
  return value ? parseFloat(value) : fallback;
};

beforeEach(() => {
  document.body.innerHTML = '';
  roomWidth = 320;
  observers.length = 0;
  vi.stubGlobal(
    'ResizeObserver',
    class {
      constructor(private readonly callback: () => void) {}
      observe(): void {
        observers.push(this.callback);
      }
      disconnect(): void {}
    },
  );
  vi.spyOn(HTMLElement.prototype, 'getBoundingClientRect').mockImplementation(function (this: HTMLElement) {
    let width = 0;
    let height = 0;
    if (this.hasAttribute('data-eq-custom-layout')) {
      const contained = this.style.getPropertyValue('contain') === 'size';
      const intrinsic = parseFloat(this.style.getPropertyValue('contain-intrinsic-size').split(' ')[1] ?? '0') || 0;
      width = contained ? roomWidth : 17;
      height = contained ? intrinsic : 23;
    } else {
      const natural = heights[this.textContent ?? ''] ?? 10;
      const clamp = (v: number, min: number, max: number) => Math.min(Math.max(v, min), max);
      width = clamp(17, bound(this.style, 'min-width', 0), bound(this.style, 'max-width', Infinity));
      height = clamp(natural, bound(this.style, 'min-height', 0), bound(this.style, 'max-height', Infinity));
    }
    return { width, height, top: 0, left: 0, right: width, bottom: height, x: 0, y: 0, toJSON: () => ({}) } as DOMRect;
  });
});

afterEach(() => {
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
});

/** Two columns of half the room's width, each child under the shorter column: sized by its children. */
class Masonry extends MultiChildLayoutDelegate {
  constructor(readonly count: number) {
    super();
  }

  override layout(constraints: BoxConstraints): Size {
    const column = constraints.maxWidth / 2;
    const columns = [0, 0];
    for (let i = 0; i < this.count; i++) {
      const into = columns[0] <= columns[1] ? 0 : 1;
      const child = this.layoutChild(i, new BoxConstraints(column, column, 0, Infinity));
      this.positionChild(i, new Point(into * column, columns[into]));
      columns[into] += child.height;
    }
    return new Size(constraints.maxWidth, Math.max(...columns));
  }
}

class Page extends StatefulComponent {
  build(): VisualNodeValue {
    return new CustomMultiChildLayout(
      new Masonry(4),
      [0, 1, 2, 3].map((i) => new LayoutId(i, new Text(`item-${i}`, 'label'))),
    ) as unknown as VisualNodeValue;
  }
}

function mount(): HTMLElement {
  const container = document.createElement('div');
  document.body.appendChild(container);
  new Page().mount(container);
  return container;
}

async function settle(): Promise<void> {
  await Promise.resolve();
  await nextFrame();
}

const layoutBox = (container: HTMLElement) => container.querySelector<HTMLElement>('[data-eq-custom-layout]')!;
const places = (box: HTMLElement) =>
  Array.from(box.children).map((wrapper) => [(wrapper as HTMLElement).style.left, (wrapper as HTMLElement).style.top]);

describe('a CustomMultiChildLayout in the browser', () => {
  it('writes every child in a wrapper at the origin, hidden until the delegate places them', async () => {
    const container = mount();
    const box = layoutBox(container);
    expect(box.children).toHaveLength(4);
    expect(box.textContent).toBe('item-0item-1item-2item-3');
    expect(box.style.visibility).not.toBe('visible');

    await settle();

    expect(box.style.visibility).toBe('visible');
  });

  it('places each child under the shorter column and takes the height of the taller one', async () => {
    const container = mount();
    await settle();
    const box = layoutBox(container);

    expect(places(box)).toEqual([
      ['0px', '0px'],
      ['160px', '0px'],
      ['160px', '50px'],
      ['0px', '100px'],
    ]);
    expect(box.style.height).toBe('130px');
    // The width is the room the parent decides, never written over.
    expect(box.style.width).toBe('');
  });

  it('lays out again when the room changes', async () => {
    const container = mount();
    await settle();

    roomWidth = 200;
    for (const observe of observers) observe();

    expect(places(layoutBox(container))[1]).toEqual(['100px', '0px']);
  });
});

/** A fake binding, for the delegate's own refusals. */
function binding(ids: unknown[]): LayoutChildren {
  return {
    has: (id) => ids.includes(id),
    layout: () => new Size(10, 10),
    position: () => {},
  };
}

describe("a layout delegate's refusals", () => {
  class Twice extends MultiChildLayoutDelegate {
    override performLayout(size: Size): void {
      this.layoutChild(0, BoxConstraints.loose(size));
      this.layoutChild(0, BoxConstraints.loose(size));
    }
  }

  class LeftOut extends MultiChildLayoutDelegate {
    override performLayout(size: Size): void {
      this.layoutChild(0, BoxConstraints.loose(size));
    }
  }

  const room = new BoxConstraints(0, 100, 0, 100);

  it('refuses a child laid out twice, naming the delegate', () => {
    expect(() => new Twice().layoutWith(binding([0]), room, [0])).toThrow(/Twice laid out the child with the id '0' twice/);
  });

  it('refuses a child left out', () => {
    expect(() => new LeftOut().layoutWith(binding([0, 1]), room, [0, 1])).toThrow(
      /LeftOut did not lay out the child with the id '1'/,
    );
  });

  it('refuses an infinite size on an axis the room leaves unbounded', () => {
    expect(() => new LeftOut().layoutWith(binding([0]), new BoxConstraints(0, 100), [0])).toThrow(/size is finite/);
  });

  it('lays a single child out with its three steps', () => {
    class Centred extends SingleChildLayoutDelegate {
      override getPositionForChild(size: Size, child: Size): Point {
        return new Point((size.width - child.width) / 2, (size.height - child.height) / 2);
      }
    }
    const placed: Point[] = [];
    const size = new SingleChildLayout(new Centred()).layoutWith(
      { has: (id) => id === SingleChildLayout.childId, layout: () => new Size(50, 20), position: (_, at) => placed.push(at) },
      new BoxConstraints(0, 200, 0, 100),
      [SingleChildLayout.childId],
    );

    expect(size).toMatchObject({ width: 200, height: 100 });
    expect(placed).toEqual([new Point(75, 40)]);
  });
});
