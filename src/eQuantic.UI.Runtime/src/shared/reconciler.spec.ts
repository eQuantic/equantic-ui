/**
 * The positional reconciler's web mirror, executed end to end (plan W6 slice 2 — the browser twin of
 * the native ReconcilerTests): `__transpiled__/NestedHost|NestedChild.ts` are REAL eqc output for a
 * stateful child nested inside a stateful host. The host's SetState rebuilds its tree with a FRESH
 * child every pass; the per-page instance store must return the RETAINED child (state survives) while
 * the transpiled `adoptConfig` carries the fresh configuration over — and the child's own SetState
 * must bubble through the invalidation hook into a host re-render.
 */

import { describe, expect, it } from 'vitest';
import { StatefulComponent } from '../core/component';
import { ComponentInstanceStore } from './instance-store';
import { Column, Pressable, Text } from './vocabulary';
import { NestedHost } from './__transpiled__/NestedHost';

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

    // The child's OWN setState must invalidate the HOST (hook), whose re-render rebuilds a fresh
    // child — without retention this would render g0:0 forever.
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

  it('the store retains by path+type+key and sweeps positions that leave the tree', () => {
    const store = new ComponentInstanceStore();
    const first = new TickChild();

    store.beginPass();
    expect(store.reconcile('r0/1', first, null)).toBe(first);
    store.endPass();

    // Same identity next pass: the RETAINED instance wins over the fresh one.
    store.beginPass();
    expect(store.reconcile('r0/1', new TickChild(), null)).toBe(first);
    store.endPass();

    // A pass that never visits the position drops it; afterwards the fresh instance starts over.
    store.beginPass();
    store.endPass();
    const replacement = new TickChild();
    store.beginPass();
    expect(store.reconcile('r0/1', replacement, null)).toBe(replacement);
    store.endPass();

    // Non-stateful nodes pass through untouched.
    const text = new Text('plain', 'caption');
    store.beginPass();
    expect(store.reconcile('r0/2', text as never, null)).toBe(text);
    store.endPass();
  });

  it('reconcile points the retained instance at the pass invalidator', () => {
    const store = new ComponentInstanceStore();
    const child = new TickChild();
    let invalidated = 0;
    const invalidator = () => {
      invalidated++;
    };

    store.beginPass();
    store.reconcile('r0/1', child, invalidator);
    store.endPass();

    child.ticks = 41;
    (child as unknown as { _scheduleRender(): void })._scheduleRender();
    expect(invalidated).toBe(1);
  });
});
