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
import { Column, Pressable, Text } from './vocabulary';

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
