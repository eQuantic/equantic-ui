/**
 * The client twin of `ComponentLifecycleTests` (C#): the two moments a component owns that a
 * constructor cannot — entering a live tree, and leaving it. The store is what knows which instance
 * stayed across a pass, so the store is what delivers the hooks; the contract has to be identical on
 * both sides, because the SAME transpiled component runs on both.
 */

import { describe, expect, it } from 'vitest';
import { ComponentInstanceStore } from './instance-store';
import { Box, BoxStyle, Column, Positioned, Row, Stack } from './vocabulary';
import { SizeValue } from './value-types';
import { lowerVisualNode } from './lowering';
import { ambientLoweringContext } from './photon-context';
import { effectiveStyle } from './style-atomizer';
import type { HtmlNode } from '../core/types';

class Tracked {
  readonly _sharedStateful = true;
  key: string | null = null;
  mounts = 0;
  unmounts = 0;
  private mounted = false;

  notifyMounted(): void {
    if (this.mounted) return;
    this.mounted = true;
    this.mounts++;
  }

  notifyUnmounted(): void {
    if (!this.mounted) return;
    this.mounted = false;
    this.unmounts++;
  }

  build(): unknown {
    return null;
  }
}

describe('component lifecycle (instance store)', () => {
  it('mounts a component entering the tree, once', () => {
    const store = new ComponentInstanceStore();
    const component = new Tracked();

    store.reconcile('/0', component, null);
    store.endPass();

    expect(component.mounts).toBe(1);
  });

  // Mid-build a hook would run while its own parent is still building, so a setState from it would
  // mutate a tree half-way through being produced.
  it('waits for the pass to close before mounting', () => {
    const store = new ComponentInstanceStore();
    const component = new Tracked();

    store.reconcile('/0', component, null);

    expect(component.mounts).toBe(0);
  });

  // Mounting per pass would re-run every subscription on every keystroke that re-rendered the page.
  it('does not mount a retained instance again', () => {
    const store = new ComponentInstanceStore();
    const first = new Tracked();
    store.reconcile('/0', first, null);
    store.endPass();

    const second = new Tracked();
    const resolved = store.reconcile('/0', second, null);
    store.endPass();

    expect(resolved).toBe(first);
    expect(first.mounts).toBe(1);
    expect(second.mounts).toBe(0);
    expect(second.unmounts).toBe(0);
  });

  // Without this every mount is a leak, which is why the pair ships together.
  it('unmounts a position that left the tree', () => {
    const store = new ComponentInstanceStore();
    const component = new Tracked();
    store.reconcile('/0', component, null);
    store.endPass();

    store.endPass();

    expect(component.unmounts).toBe(1);
  });

  it('unmounts everything still in when the store is torn down', () => {
    const store = new ComponentInstanceStore();
    const a = new Tracked();
    const b = new Tracked();
    store.reconcile('/0', a, null);
    store.reconcile('/1', b, null);
    store.endPass();

    store.unmountAll();

    expect(a.unmounts).toBe(1);
    expect(b.unmounts).toBe(1);
  });

  // The order that lets two components share one exclusive resource across a swap.
  it('unmounts the outgoing before mounting the incoming', () => {
    const order: string[] = [];
    const named = (name: string) => {
      const c = new Tracked();
      const mount = c.notifyMounted.bind(c);
      const unmount = c.notifyUnmounted.bind(c);
      c.notifyMounted = () => {
        const before = c.mounts;
        mount();
        if (c.mounts !== before) order.push(`mount:${name}`);
      };
      c.notifyUnmounted = () => {
        const before = c.unmounts;
        unmount();
        if (c.unmounts !== before) order.push(`unmount:${name}`);
      };
      return c;
    };

    const store = new ComponentInstanceStore();
    store.reconcile('/0', named('leaving'), null);
    store.endPass();

    store.reconcile('/1', named('arriving'), null);
    store.endPass();

    expect(order).toEqual(['mount:leaving', 'unmount:leaving', 'mount:arriving']);
  });
});

/**
 * The flex constructor takes TWO shapes and has to tell them apart. C# takes the layout knobs as
 * parameters (a declarative author has no `new`, so no object initializer) and eqc calls them
 * positionally; hand-written runtime code has always passed a config object. Getting this wrong is
 * silent — the string lands in Object.assign and nothing happens.
 */
describe('flex constructor shapes', () => {
  it('reads a positional alignment, the way transpiled code calls it', () => {
    const row = new Row(12, 'spaceBetween', 'start');

    expect(row.main).toBe('spaceBetween');
    expect(row.cross).toBe('start');
  });

  it('still reads a config object in the same position', () => {
    const row = new Row(12, { cross: 'start', wrap: true });

    expect(row.cross).toBe('start');
    expect(row.wrap).toBe(true);
    expect(row.main).toBe('start');
  });

  // `new Row(12, …) { Cross = … }` — the initializer runs after the constructor in C#, and eqc
  // emits it as the trailing argument, past every constructor parameter.
  it('lets the trailing initializer win over a constructor parameter', () => {
    const row = new Row(12, 'start', 'center', false, null, null, { cross: 'end' });

    expect(row.cross).toBe('end');
  });

  it('keeps each subclass default when nothing is passed', () => {
    expect(new Row(4).cross).toBe('center');
    expect(new Column(4).cross).toBe('stretch');
  });
});

/**
 * The client half of the C# `PositionedSpanTests` / `PositionedThroughComponentTests`. It has to
 * agree with the server to the character: SSR emits the markup and the client re-lowers it on
 * hydration, so a rule applied on one side only is a mismatch the reconciler repairs by replacing
 * the subtree — a visible flash, and the bug back after hydration.
 */
describe('a positioned child of a stack', () => {
  const corner = (child: unknown) => {
    const stack = new Stack('topStart');
    stack.add(new Box(new BoxStyle({ width: SizeValue.fill, height: 100 })));
    stack.add(new Positioned(child as never, 0, 0));
    return lowerVisualNode(stack as never, ambientLoweringContext());
  };

  const fillingRow = () => {
    const row = new Row(0, { width: SizeValue.fill });
    row.add(new Box(new BoxStyle({ width: 32, height: 32 })));
    return row;
  };

  const styleOf = (node: HtmlNode, index: number) =>
    effectiveStyle(node.children[index] as { attributes: Record<string, string | undefined> });

  it('spans between both edges when its child fills', () => {
    const style = styleOf(corner(fillingRow()), 1);

    expect(style).toContain('left: 0');
    expect(style).toContain('right: 0');
  });

  it('stays where it was put when its child hugs', () => {
    const style = styleOf(corner(new Box(new BoxStyle({ width: 32, height: 32 }))), 1);

    expect(style).not.toContain('left: 0');
  });
});
