/**
 * A stateful component returned AS THE ROOT of a build keeps its state.
 *
 * The lowering resolves every component it meets against the positional store, so a stateful
 * component nested anywhere inside a tree is retained across passes. The ROOT a build returns
 * never went through the lowering — the render paths call `.render()` on it — so it was rebuilt
 * from scratch on every pass, and its own `setState` was the thing that threw its state away.
 *
 * A composed component with one control at its root is ordinary — `CultureSwitcher` returns a
 * `Menu`, a search box returns a combo — and the failure is invisible on the server, which renders
 * once and never presses anything. It only shows in a browser or a Photon window, as a control
 * that does not respond.
 */

import { describe, expect, it } from 'vitest';
import { SharedStatefulComponent, StatelessComponent } from '../core/component';
import { Column, Positioned, Pressable, Stack, Text } from './vocabulary';

const nextFrame = () =>
  new Promise<void>((resolve) =>
    requestAnimationFrame(() => requestAnimationFrame(() => resolve())),
  );

/** A counter with a press: the smallest thing whose state is visible in the DOM. */
class Counter extends SharedStatefulComponent {
  _count = 0;

  build(): Column {
    const column = new Column(0);
    column.add(new Pressable(new Text(`count ${this._count}`, 'label'), () =>
      this.setState(() => this._count++), { label: 'bump' }));
    return column;
  }
}

class RootIsTheCounter extends StatelessComponent {
  build(): Counter {
    return new Counter();
  }
}

class CounterNestedInAColumn extends StatelessComponent {
  build(): Column {
    const column = new Column(0);
    column.add(new Counter() as unknown as Text);
    return column;
  }
}

async function bump(container: HTMLElement) {
  const trigger = Array.from(container.querySelectorAll('button,[role="button"]')).find(
    (element) => element.getAttribute('aria-label') === 'bump',
  ) as HTMLElement;
  trigger.click();
  await nextFrame();
}

/** A stateless WRAPPER with a stateful child — the ordinary "section of a page" shape. */
class Panel extends StatelessComponent {
  build(): Column {
    const column = new Column(0);
    column.add(new Counter() as unknown as Text);
    return column;
  }
}

/** A host that rebuilds on its own press, producing a FRESH Panel each time, as a real one does. */
class HostAroundAPanel extends SharedStatefulComponent {
  _ticks = 0;

  build(): Column {
    const column = new Column(0);
    column.add(
      new Pressable(new Text(`ticks ${this._ticks}`, 'label'), () => this.setState(() => this._ticks++), {
        label: 'tick',
      }),
    );
    column.add(new Panel() as unknown as Text);
    return column;
  }
}

async function press(container: HTMLElement, label: string) {
  const trigger = Array.from(container.querySelectorAll('button,[role="button"]')).find(
    (element) => element.getAttribute('aria-label') === label,
  ) as HTMLElement;
  trigger.click();
  await nextFrame();
}

describe('a STATELESS wrapper around a stateful child', () => {
  it('keeps the child state when the page around it re-renders', async () => {
    const container = document.createElement('div');
    document.body.appendChild(container);
    new HostAroundAPanel().mount(container);
    await nextFrame();

    await press(container, 'bump');
    expect(container.textContent).toContain('count 1');

    // The host rebuilds and hands down a NEW Panel — same position, same type, so the store is
    // meant to reconcile onto the retained one and the counter inside goes on counting.
    await press(container, 'tick');
    expect(container.textContent).toContain('ticks 1');
    expect(container.textContent).toContain('count 1');

    container.remove();
  });
});

/** The same wrapper, STATEFUL — the control for the case below. */
class PinnedCornerStateful extends SharedStatefulComponent {
  build(): Positioned {
    return new Positioned(new Text('badge', 'label'), 4, 4);
  }
}

/** A stateless wrapper whose build returns a POSITIONED node — the Stack case. */
class PinnedCorner extends StatelessComponent {
  build(): Positioned {
    return new Positioned(new Text('badge', 'label'), 4, 4);
  }
}

describe('a Positioned returned by a component, inside a Stack', () => {
  /** The badge's CELL, as the stack laid it out. */
  const badgeCell = async (layer: () => unknown): Promise<string> => {
    const container = document.createElement('div');
    document.body.appendChild(container);

    class Host extends StatelessComponent {
      build(): Stack {
        const stack = new Stack();
        stack.add(new Text('under', 'body') as unknown as Text);
        stack.add(layer() as unknown as Text);
        return stack;
      }
    }

    new Host().mount(container);
    await nextFrame();
    const cells = Array.from(container.firstElementChild?.children ?? []) as HTMLElement[];
    const cell = cells.find((element) => element.textContent?.includes('badge'))!;
    container.remove();
    return cell.className;
  };

  const badge = () => new Text('badge', 'label');

  it('lays out exactly as a bare one does, through either component family', async () => {
    // A Stack asks each child whether it is POSITIONED, and a component has to be walked through
    // to answer: the offsets live in the node it BUILDS. The assertion is against the bare case
    // rather than against a style string, because the rule rides an atomic class — and it holds
    // the badge at the same index in all three, since the cell's z-index is its position.
    const bare = await badgeCell(() => new Positioned(badge(), 4, 4));
    const stateful = await badgeCell(() => new PinnedCornerStateful());
    const stateless = await badgeCell(() => new PinnedCorner());

    expect(stateful).toBe(bare);
    // The one that was NOT walked through: the layer landed in flow, in a plain grid cell, so a
    // badge pinned to a corner sat under the text instead.
    expect(stateless).toBe(bare);
  });
});

describe('a stateful component as a build ROOT', () => {
  it('keeps its state across the render its own setState asks for', async () => {
    const container = document.createElement('div');
    document.body.appendChild(container);
    new RootIsTheCounter().mount(container);
    await nextFrame();

    expect(container.textContent).toContain('count 0');
    await bump(container);
    expect(container.textContent).toContain('count 1');
    await bump(container);
    expect(container.textContent).toContain('count 2');

    container.remove();
  });

  it('behaves the same nested one level down (the position that always worked)', async () => {
    const container = document.createElement('div');
    document.body.appendChild(container);
    new CounterNestedInAColumn().mount(container);
    await nextFrame();

    await bump(container);
    expect(container.textContent).toContain('count 1');

    container.remove();
  });

  it('two sibling stateless hosts, each rooted on a counter, count independently', async () => {
    // Root identity is a path from a per-pass counter. Two hosts must not collapse onto one entry
    // — a shared identity would make one switcher echo the other's state.
    const first = document.createElement('div');
    const second = document.createElement('div');
    document.body.append(first, second);
    new RootIsTheCounter().mount(first);
    new RootIsTheCounter().mount(second);
    await nextFrame();

    await bump(first);
    await bump(first);
    await bump(second);

    expect(first.textContent).toContain('count 2');
    expect(second.textContent).toContain('count 1');

    first.remove();
    second.remove();
  });
});
