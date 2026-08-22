import { describe, expect, it } from 'vitest';
import { UI } from './components/UI';
import { Column, Text, VisualNode } from './vocabulary';
import { Button } from './components/Button';

/**
 * The declarative factory twin (C# `eQuantic.UI.Components.UI`, transpiled): pages authored
 * without `new` call these statics — `UI.column(gap, [children])` must build the same tree
 * `new Column(gap)` + `add()` builds, or SSR (real C#) and the hydrated client disagree.
 */
describe('UI factories (no-new authoring twin)', () => {
  it('column collects its children array through add()', () => {
    // In C# components ARE VisualNodes (UiComponent : VisualNode); the TS twin states that
    // structurally, so a component in a children slot needs the cast only here in the spec.
    // children is TRAILING (the container contract) — the alignment knobs sit between it and gap,
    // so it is named the moment a container takes more than a gap.
    const tree = UI.column(12, 'start', 'stretch', false, null, null, [
      UI.text('Count: 0', 'display'),
      UI.button('Up', 'primary', 'medium', () => {}) as unknown as VisualNode,
    ]);

    expect(tree).toBeInstanceOf(Column);
    expect(tree.gap).toBe(12);
    expect(tree.children).toHaveLength(2);
    expect(tree.children[0]).toBeInstanceOf(Text);
    expect(tree.children[1]).toBeInstanceOf(Button);
  });

  it('an omitted children argument leaves an empty container', () => {
    expect(UI.row().children).toHaveLength(0);
    expect(UI.stack().children).toHaveLength(0);
  });

  it('box without a style falls back to the twin default, never null', () => {
    const box = UI.box(undefined, UI.text('x'));
    expect(box.style).toBeDefined();
    expect(box.style).not.toBeNull();
  });

  it('the named factories reach the statics the mirrored names shadow', () => {
    // Gap and DotBadge exist because `Spacer.Fixed` / `Badge.AsDot` stop compiling in C# wherever
    // the surface is imported. The twin has no such shadowing, so nothing here would notice if the
    // named factory quietly stopped wrapping the right static — these assert the RESULT.
    const gap = UI.gap(34);
    expect(gap.flex).toBe(0);
    expect(gap.fixedLength).toBe(34);

    const dot = UI.dotBadge('destructive');
    expect(dot.dot).toBe(true);
    expect(dot.count).toBe(0);
  });

  it('the factory tree renders through the same lowering as constructed nodes', () => {
    const factoryNode = UI.column(8, 'start', 'stretch', false, null, null, [
      UI.text('hello', 'bodyM'),
    ]).render();
    const constructed = (() => {
      const column = new Column(8);
      column.add(new Text('hello', 'bodyM'));
      return column.render();
    })();
    expect(factoryNode).toEqual(constructed);
  });
});
