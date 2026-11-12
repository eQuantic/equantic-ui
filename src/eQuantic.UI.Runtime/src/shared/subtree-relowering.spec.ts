/**
 * A render the page's owners asked for is answered by what they built (E3b-3, #1455): a nested
 * component's `setState` lowers that component alone and patches the DOM it drew, and the DOM it
 * leaves is the DOM a whole-page lowering leaves. Lowerings are COUNTED, since what the change moves
 * is how much of the page lowers.
 */

import { beforeEach, describe, expect, it } from 'vitest';
import { StatefulComponent } from '../core/component';
import { activeShortcuts } from '../dom/shortcuts';
import { componentLowerings } from './lowered-records';
import { RenderManager } from '../dom/renderer';
import type { HtmlNode } from '../core/types';
import { Column, Flexible, Presence, Pressable, Row, Shortcut, Text } from './vocabulary';
import type { VisualNodeValue } from './nodes';

const nextFrame = () =>
  new Promise<void>((resolve) => requestAnimationFrame(() => requestAnimationFrame(() => resolve())));

/** The instance each label's position keeps: the first made, which the engine keeps. */
const kept: Record<string, Counter> = {};

/** A counter with a press: the smallest thing whose state shows. */
class Counter extends StatefulComponent {
  label: string;
  count = 0;

  constructor(label: string) {
    super();
    this.label = label;
    kept[label] ??= this;
  }

  bump(): void {
    this.setState(() => this.count++);
  }

  adoptConfig(next: Counter): void {
    this.label = next.label;
  }

  build(): VisualNodeValue {
    return new Pressable(new Text(`${this.label}: ${this.count}`, 'label'), () => this.setState(() => this.count++), {
      label: `bump ${this.label}`,
    }) as unknown as VisualNodeValue;
  }
}

/** A page whose tree is what `tree` answers, and which can be told to build itself again. */
class Page extends StatefulComponent {
  constructor(private readonly tree: () => VisualNodeValue) {
    super();
  }

  build(): VisualNodeValue {
    return this.tree();
  }

  /** The page's own state changes: it builds and lowers whole. */
  rebuild(): void {
    this.setState(() => {});
  }
}

function column(...children: unknown[]): VisualNodeValue {
  const node = new Column(0);
  for (const child of children) node.add(child as never);
  return node as unknown as VisualNodeValue;
}

function row(...children: unknown[]): VisualNodeValue {
  const node = new Row(0);
  for (const child of children) node.add(child as never);
  return node as unknown as VisualNodeValue;
}

function mount(page: Page): HTMLElement {
  const container = document.createElement('div');
  document.body.appendChild(container);
  page.mount(container);
  return container;
}

async function press(container: HTMLElement, label: string): Promise<void> {
  const trigger = Array.from(container.querySelectorAll('button')).find(
    (button) => button.getAttribute('aria-label') === label,
  ) as HTMLElement;
  trigger.click();
  await nextFrame();
}

/**
 * Presses `label`, counting what lowered, then has the page lower whole and answers whether the DOM
 * the patch left is the DOM the whole lowering leaves.
 */
async function pressAndCompare(page: Page, container: HTMLElement, label: string) {
  const before = componentLowerings();
  await press(container, label);
  const lowered = componentLowerings() - before;
  const patched = container.innerHTML;
  page.rebuild();
  await nextFrame();
  return { lowered, patched, whole: container.innerHTML };
}

beforeEach(() => {
  document.body.innerHTML = '';
  for (const label of Object.keys(kept)) delete kept[label];
});

/** A component holding a counter of its own, both with a state that shows. */
class Holder extends StatefulComponent {
  count = 0;

  bump(): void {
    this.setState(() => this.count++);
  }

  build(): VisualNodeValue {
    holder ??= this;
    return column(new Text(`holder: ${this.count}`, 'label'), new Counter('inner'));
  }
}
let holder: Holder | null = null;

describe('a dirty element lowers its own subtree', () => {
  it('lowers the component that built and patches its DOM, and the DOM is the whole lowering’s', async () => {
    const page = new Page(() => column(new Counter('a'), new Counter('b')));
    const container = mount(page);

    const { lowered, patched, whole } = await pressAndCompare(page, container, 'bump a');

    expect(lowered, 'one component lowered: the one that built').toBe(1);
    expect(container.textContent).toContain('a: 1');
    expect(container.textContent).toContain('b: 0');
    expect(patched).toBe(whole);
  });

  it('does again what a parent did to its root: a presence names its motion on what a component drew', async () => {
    const page = new Page(() => row(new Flexible(new Presence(new Counter('a'))), new Counter('b')));
    const container = mount(page);

    const { lowered, patched, whole } = await pressAndCompare(page, container, 'bump a');

    expect(lowered).toBe(1);
    expect(patched, 'the motion class and the exit marker are on the patched element').toContain('eq-presence-fade');
    expect(patched).toContain('data-eq-exit="fade"');
    expect(patched).toBe(whole);
  });

  it('lowers the page whole when a parent reads what the component lowered to', async () => {
    // A shortcut joins its marker to the one its child already carries: the child is not its own.
    const page = new Page(() =>
      column(new Shortcut(new Counter('a'), { key: 'k', modifiers: 4 } as never, () => {}), new Counter('b')),
    );
    const container = mount(page);

    const { lowered, patched, whole } = await pressAndCompare(page, container, 'bump a');

    expect(lowered, 'every component of the page lowered').toBe(2);
    expect(container.textContent).toContain('a: 1');
    expect(patched).toBe(whole);
  });

  it('lowers each of two components that built, and nothing between them', async () => {
    const page = new Page(() => column(new Counter('a'), new Text('between', 'label'), new Counter('b')));
    const container = mount(page);
    const before = componentLowerings();

    kept.a.bump();
    kept.b.bump();
    await nextFrame();

    expect(componentLowerings() - before).toBe(2);
    const patched = container.innerHTML;
    expect(container.textContent).toContain('a: 1');
    expect(container.textContent).toContain('b: 1');
    page.rebuild();
    await nextFrame();
    expect(patched).toBe(container.innerHTML);
  });

  it('lowers a component and one inside it that both built as one subtree, from the outer', async () => {
    holder = null;
    const page = new Page(() => column(new Holder(), new Counter('other')));
    const container = mount(page);
    const before = componentLowerings();

    holder!.bump();
    kept.inner.bump();
    await nextFrame();

    expect(componentLowerings() - before, 'the holder and the counter inside it').toBe(2);
    const patched = container.innerHTML;
    expect(container.textContent).toContain('holder: 1');
    expect(container.textContent).toContain('inner: 1');
    page.rebuild();
    await nextFrame();
    expect(patched).toBe(container.innerHTML);
  });

  it('keeps the shortcuts the components it did not lower declared', async () => {
    const page = new Page(() =>
      column(
        new Shortcut(new Text('saves', 'label'), { key: 's', modifiers: 4 } as never, () => {}),
        new Counter('a'),
      ),
    );
    const container = mount(page);
    expect(activeShortcuts().map((binding) => binding.chord)).toEqual(['command+s']);

    const before = componentLowerings();
    await press(container, 'bump a');

    expect(componentLowerings() - before).toBe(1);
    expect(activeShortcuts().map((binding) => binding.chord)).toEqual(['command+s']);
  });

  it('lowers the page whole when the component starts declaring a shortcut it had no place for', async () => {
    class Saver extends StatefulComponent {
      armed = false;

      arm(): void {
        this.setState(() => (this.armed = true));
      }

      build(): VisualNodeValue {
        saver ??= this;
        const body = new Text(this.armed ? 'armed' : 'idle', 'label');
        return (this.armed
          ? new Shortcut(body, { key: 'k', modifiers: 4 } as never, () => {})
          : body) as unknown as VisualNodeValue;
      }
    }
    let saver: Saver | null = null;
    const page = new Page(() =>
      column(new Saver(), new Shortcut(new Text('saves', 'label'), { key: 's', modifiers: 4 } as never, () => {})),
    );
    const container = mount(page);
    const before = componentLowerings();

    saver!.arm();
    await nextFrame();

    expect(componentLowerings() - before, 'lowered alone, then the page whole, to list the new binding in its place').toBe(2);
    const listed = activeShortcuts().map((binding) => binding.chord);
    page.rebuild();
    await nextFrame();
    expect(listed).toEqual(activeShortcuts().map((binding) => binding.chord));
    expect(container.textContent).toContain('armed');
  });
});

describe('a patch finds the DOM it patches', () => {
  it('refuses a patch whose element is not where the tree says, as after a hydration that wrote no empty text', () => {
    const text = (content: string): HtmlNode => ({ tag: '#text', attributes: {}, events: {}, children: [], textContent: content });
    const span: HtmlNode = { tag: 'span', attributes: {}, events: {}, children: [text('a')] };
    const tail: HtmlNode = { tag: 'em', attributes: {}, events: {}, children: [text('b')] };
    const tree: HtmlNode = { tag: 'div', attributes: {}, events: {}, children: [text(''), span, tail] };
    const container = document.createElement('div');
    document.body.appendChild(container);
    const manager = new RenderManager();
    manager.mount(tree, container);
    // The markup the server wrote for an empty text has no node for it.
    container.firstChild!.removeChild(container.firstChild!.firstChild!);

    const next: HtmlNode = { tag: 'span', attributes: { class: 'patched' }, events: {}, children: [text('a!')] };
    expect(manager.patch(span, next)).toBe(false);
    expect(container.innerHTML).toBe('<div><span>a</span><em>b</em></div>');
  });
});

