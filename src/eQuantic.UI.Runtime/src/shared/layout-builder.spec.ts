/**
 * A `LayoutBuilder` in the browser (#1555): the page draws its box empty, the pass's commit measures the
 * room the box is given as if it held nothing, and the component above builds the child for it. jsdom
 * lays nothing out, so the box's measurement is the one thing faked here: a box under size containment
 * answers the room the test gives it, and an axis sized by content (the height, here) answers zero.
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { StatefulComponent } from '../core/component';
import { Column, LayoutBuilder, Pressable, Row, Text } from './vocabulary';
import type { BoxConstraints } from './value-types';
import type { VisualNodeValue } from './nodes';

const nextFrame = () =>
  new Promise<void>((resolve) => requestAnimationFrame(() => requestAnimationFrame(() => resolve())));

/** The width the parent gives the builder's box, which the test moves. */
let roomWidth = 600;
/** What the observers watch, so a test can say the box was resized. */
const observers: Array<() => void> = [];

beforeEach(() => {
  document.body.innerHTML = '';
  roomWidth = 600;
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
  // Size containment makes the box as large as its parent decides: the room's width, and no height,
  // since the page's column sizes its height from what it holds. Without containment the box is its
  // content's size, which a builder must never be told.
  vi.spyOn(HTMLElement.prototype, 'getBoundingClientRect').mockImplementation(function (this: HTMLElement) {
    const contained = this.style.getPropertyValue('contain') === 'size';
    const width = contained ? roomWidth : 17;
    const height = contained ? 0 : 23;
    return { width, height, top: 0, left: 0, right: width, bottom: height, x: 0, y: 0, toJSON: () => ({}) } as DOMRect;
  });
});

afterEach(() => {
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
});

const told: BoxConstraints[] = [];
const log: string[] = [];

/** A counter with a press, and a lifecycle the test reads. */
class Counter extends StatefulComponent {
  count = 0;

  constructor(readonly label: string) {
    super();
  }

  adoptConfig(): void {}

  onUnmount(): void {
    log.push(`${this.label} unmounted`);
  }

  build(): VisualNodeValue {
    return new Pressable(new Text(`${this.label}: ${this.count}`, 'label'), () => this.setState(() => this.count++), {
      label: `bump ${this.label}`,
    }) as unknown as VisualNodeValue;
  }
}

function column(...children: unknown[]): VisualNodeValue {
  const node = new Column(0);
  for (const child of children) node.add(child as never);
  return node as unknown as VisualNodeValue;
}

/** A page whose builder lays out side by side from 480 and stacks below it, beside a counter of its own. */
class Page extends StatefulComponent {
  build(): VisualNodeValue {
    const builder = new LayoutBuilder((room) => {
      told.push(room);
      const wide = room.maxWidth >= 480;
      const layout = wide ? new Row(0) : new Column(0);
      layout.add(new Text(wide ? 'side by side' : 'stacked', 'label') as never);
      layout.add(new Counter('inside') as never);
      return layout as never;
    });
    return column(new Counter('beside'), builder);
  }
}

function mount(): HTMLElement {
  const container = document.createElement('div');
  document.body.appendChild(container);
  new Page().mount(container);
  return container;
}

async function settle(): Promise<void> {
  // The commit is a microtask after the pass is written; the build it asks for is the next frame.
  await Promise.resolve();
  await nextFrame();
}

async function press(label: string): Promise<void> {
  const trigger = Array.from(document.querySelectorAll('button')).find(
    (button) => button.getAttribute('aria-label') === label,
  ) as HTMLElement;
  trigger.click();
  await nextFrame();
}

const builderBox = (container: HTMLElement) => container.querySelector<HTMLElement>('[data-eq-layout-builder]')!;

describe('a LayoutBuilder in the browser', () => {
  beforeEach(() => {
    told.length = 0;
    log.length = 0;
  });

  it('draws its box empty until it has measured the room, then builds its child for it', async () => {
    const container = mount();
    expect(builderBox(container).children).toHaveLength(0);
    expect(told).toHaveLength(0);

    await settle();

    expect(told[told.length - 1]).toMatchObject({ minWidth: 0, maxWidth: 600, minHeight: 0, maxHeight: Infinity });
    expect(builderBox(container).textContent).toContain('side by side');
  });

  it('builds another child when the room narrows', async () => {
    const container = mount();
    await settle();

    roomWidth = 360;
    for (const resized of observers) resized();
    await nextFrame();

    expect(told[told.length - 1]!.maxWidth).toBe(360);
    expect(builderBox(container).textContent).toContain('stacked');
  });

  it('runs the builder again only when the room changes', async () => {
    mount();
    await settle();
    const runs = told.length;

    for (const resized of observers) resized();
    await nextFrame();

    expect(told).toHaveLength(runs);
  });

  it('keeps its components when a component beside it builds alone (#1575)', async () => {
    const container = mount();
    await settle();

    await press('bump beside');
    await press('bump inside');

    expect(log).toEqual([]);
    expect(builderBox(container).textContent).toContain('inside: 1');
  });
});
