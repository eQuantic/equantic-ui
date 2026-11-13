/**
 * The positional reconciler's web mirror, executed end to end (the browser twin of the native
 * ReconcilerTests): `__fixtures__/NestedHost|NestedChild.ts` are REAL eqc output for a stateful child
 * nested inside a stateful host. The host's SetState rebuilds its tree with a FRESH child every pass;
 * the element tree the page builds through must keep the child its position holds (state survives)
 * while the transpiled `adoptConfig` carries the fresh configuration over — and the child's own
 * SetState must mark its position, whose owner asks the host for a render.
 */

import { describe, expect, it } from 'vitest';
import { StatefulComponent } from '../core/component';
import { ElementRoots, enterPass, exitPass } from './element-roots';
import { lowerVisualNode } from './lowering';
import { ambientLoweringContext } from './photon-context';
import { Column, Pressable, Text } from './vocabulary';
import { NestedHost } from './__fixtures__/NestedHost';

const nextFrame = () =>
  new Promise<void>((resolve) =>
    requestAnimationFrame(() => requestAnimationFrame(() => resolve())),
  );

describe('positional reconciler on web (transpiled NestedHost/NestedChild, real eqc output)', () => {
  it('nested state survives parent rebuilds; adoptConfig carries the fresh config', async () => {
    const container = document.createElement('div');
    document.body.appendChild(container);

    const host = new NestedHost();
    host.mount(container);

    // Initial: generation 0, child label g0, count 0.
    expect(container.textContent).toContain('g0:0');

    const buttons = () => Array.from(container.querySelectorAll('button'));
    const byLabel = (label: string) =>
      buttons().find((b) => b.getAttribute('aria-label') === label)!;

    // The child's OWN setState must reach the HOST, through the owner of the position it marked —
    // without retention this would render g0:0 forever.
    byLabel('Add').click();
    await nextFrame();
    expect(container.textContent).toContain('g0:1');

    // Parent rebuild: fresh child carries label g1; the retained instance adopts it, count survives.
    byLabel('Bump').click();
    await nextFrame();
    expect(container.textContent).toContain('g1:1');

    // And the retained child keeps responding after adoption.
    byLabel('Add').click();
    await nextFrame();
    expect(container.textContent).toContain('g1:2');

    container.remove();
  });
});

/** Hand-written nested stateful for the key/identity mechanics (mirror of the native KeyChange test). */
class TickChild extends StatefulComponent {
  ticks = 0;

  build(): Column {
    const column = new Column();
    column.add(new Text(`ticks:${this.ticks}`, 'caption'));
    column.add(
      new Pressable(new Text('tick', 'caption'), () => this.setState(() => this.ticks++), {
        label: 'tick',
      }),
    );
    return column as never;
  }
}

class KeyedHost extends StatefulComponent {
  childKey = 'a';

  swapKey(): void {
    this.setState(() => {
      this.childKey = this.childKey === 'a' ? 'b' : 'a';
    });
  }

  build(): Column {
    const column = new Column();
    const child = new TickChild();
    child.key = this.childKey;
    column.add(child as never);
    return column as never;
  }
}

describe('reconciler identity mechanics', () => {
  it('a key change at the same position resets the nested state', async () => {
    const container = document.createElement('div');
    document.body.appendChild(container);

    const host = new KeyedHost();
    host.mount(container);
    expect(container.textContent).toContain('ticks:0');

    container.querySelector('button')!.click();
    await nextFrame();
    expect(container.textContent).toContain('ticks:1');

    // Same position, same type — but a different key is a different identity: state resets.
    host.swapKey();
    await nextFrame();
    expect(container.textContent).toContain('ticks:0');

    container.remove();
  });

  /** One render of `children` in a column, in a pass of `roots`, as a page's render lowers its tree. */
  const renderIn = (roots: ElementRoots, ...children: unknown[]) => {
    const column = new Column();
    for (const child of children) column.add(child as never);
    enterPass(roots, null, true);
    try {
      return lowerVisualNode(column as never, ambientLoweringContext());
    } finally {
      exitPass();
    }
  };
  const textOf = (node: unknown): string => {
    const value = node as { children?: unknown[]; textContent?: string };
    return `${value.textContent ?? ''}${(value.children ?? []).map(textOf).join('')}`;
  };

  it('keeps an instance by position, type and key, and lets go of a position that leaves the tree', () => {
    const roots = new ElementRoots(null);
    const first = new TickChild();
    first.ticks = 5;
    renderIn(roots, first);

    // Same position, same type, next render: the KEPT instance wins over the fresh one.
    const fresh = new TickChild();
    expect(textOf(renderIn(roots, fresh))).toContain('ticks:5');
    expect(fresh.mountedAt).toBeNull();

    // A render that does not reach the position takes it out; afterwards a fresh instance starts over.
    renderIn(roots);
    expect(first.mountedAt).toBeNull();
    const replacement = new TickChild();
    expect(textOf(renderIn(roots, replacement))).toContain('ticks:0');
    expect(replacement.mountedAt).not.toBeNull();

    // A node that is not a component is held as it is.
    const text = new Text('plain', 'caption');
    expect(textOf(renderIn(roots, text))).toContain('plain');
    roots.unmountAll();
  });

  it("a kept instance's setState asks the page through the owner of its position", () => {
    let asked = 0;
    const roots = new ElementRoots(() => {
      asked++;
    });
    const child = new TickChild();
    renderIn(roots, child);

    (child as unknown as { setState(fn: () => void): void }).setState(() => {
      child.ticks = 41;
    });
    expect(asked).toBe(1);
    roots.unmountAll();
  });
});
