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
    const tree = UI.column(12, [
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

  it('the factory tree renders through the same lowering as constructed nodes', () => {
    const factoryNode = UI.column(8, [UI.text('hello', 'bodyM')]).render();
    const constructed = (() => {
      const column = new Column(8);
      column.add(new Text('hello', 'bodyM'));
      return column.render();
    })();
    expect(factoryNode).toEqual(constructed);
  });
});
