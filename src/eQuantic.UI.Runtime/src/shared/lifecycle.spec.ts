/**
 * The client twin of `ComponentLifecycleTests` (C#): the two moments a component owns that a
 * constructor cannot — entering a live tree, and leaving it. The element tree a page builds through
 * is what knows which instance stayed across a render, so its owner is what delivers the hooks; the
 * contract has to be identical on both sides, because the SAME transpiled component runs on both.
 */

import { describe, expect, it } from 'vitest';
import { StatefulComponent } from '../core/component';
import { ElementRoots, enterPass, exitPass } from './element-roots';
import { Box, BoxStyle, Column, Positioned, Row, Stack, Text } from './vocabulary';
import { SizeValue } from './value-types';
import { lowerVisualNode } from './lowering';
import { ambientLoweringContext } from './photon-context';
import { effectiveStyle } from './style-atomizer';
import type { HtmlNode } from '../core/types';

class Tracked extends StatefulComponent {
  mounts = 0;
  unmounts = 0;

  constructor(
    private readonly log: string[] | null = null,
    private readonly name = '',
  ) {
    super();
  }

  onMount(): void {
    this.mounts++;
    this.log?.push(`mount:${this.name}`);
  }

  onUnmount(): void {
    this.unmounts++;
    this.log?.push(`unmount:${this.name}`);
  }

  build(): Text {
    return new Text('tracked');
  }
}

/** The tree a page's render lowers: `children` in a column, built in the owner of the pass's next root. */
function lower(...children: StatefulComponent[]): void {
  const column = new Column();
  for (const child of children) column.add(child as never);
  lowerVisualNode(column as never, ambientLoweringContext());
}

/** One render of a page that holds `roots`: a pass around the lowering, as `render()` opens. */
function render(roots: ElementRoots, ...children: StatefulComponent[]): void {
  enterPass(roots, null, true);
  try {
    lower(...children);
  } finally {
    exitPass();
  }
}

describe('component lifecycle (element tree)', () => {
  it('mounts a component entering the tree, once', () => {
    const roots = new ElementRoots(null);
    const component = new Tracked();

    render(roots, component);

    expect(component.mounts).toBe(1);
  });

  // Mid-build a hook would run while its own parent is still building, so a setState from it would
  // mutate a tree half-way through being produced.
  it('waits for the pass to close before mounting', () => {
    const roots = new ElementRoots(null);
    const component = new Tracked();

    enterPass(roots, null, true);
    try {
      lower(component);
      expect(component.mounts).toBe(0);
    } finally {
      exitPass();
    }

    expect(component.mounts).toBe(1);
  });

  // Mounting per pass would re-run every subscription on every keystroke that re-rendered the page.
  it('does not mount a kept instance again', () => {
    const roots = new ElementRoots(null);
    const first = new Tracked();
    render(roots, first);

    const second = new Tracked();
    render(roots, second);

    expect(first.mountedAt).not.toBeNull();
    expect(second.mountedAt).toBeNull();
    expect(first.mounts).toBe(1);
    expect(second.mounts).toBe(0);
    expect(second.unmounts).toBe(0);
  });

  // Without this every mount is a leak, which is why the pair ships together.
  it('unmounts a position that left the tree', () => {
    const roots = new ElementRoots(null);
    const component = new Tracked();
    render(roots, component);

    render(roots);

    expect(component.unmounts).toBe(1);
  });

  it('unmounts everything still in when the page is torn down', () => {
    const roots = new ElementRoots(null);
    const a = new Tracked();
    const b = new Tracked();
    render(roots, a, b);

    roots.unmountAll();

    expect(a.unmounts).toBe(1);
    expect(b.unmounts).toBe(1);
  });

  // The order that lets two components share one exclusive resource across a swap: another key at
  // the same position is another component, so the one there leaves and a new one arrives.
  it('unmounts the outgoing before mounting the incoming', () => {
    const order: string[] = [];
    const roots = new ElementRoots(null);
    const leaving = new Tracked(order, 'leaving');
    leaving.key = 'a';
    render(roots, leaving);

    const arriving = new Tracked(order, 'arriving');
    arriving.key = 'b';
    render(roots, arriving);

    expect(order).toEqual(['mount:leaving', 'unmount:leaving', 'mount:arriving']);
  });

  // A tree the render no longer reaches at all (a bridge the page stopped drawing) leaves whole,
  // before anything that arrives in another tree mounts.
  it('takes down a tree the render stopped reaching before mounting what arrived elsewhere', () => {
    const order: string[] = [];
    const roots = new ElementRoots(null);
    enterPass(roots, null, true);
    try {
      lower(new Tracked(order, 'first root'));
      lower(new Tracked(order, 'second root'));
    } finally {
      exitPass();
    }
    order.length = 0;

    enterPass(roots, null, true);
    try {
      lower(new Tracked(order, 'arriving'), new Tracked(order, 'also arriving'));
    } finally {
      exitPass();
    }

    expect(order).toEqual(['unmount:second root', 'mount:also arriving']);
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
