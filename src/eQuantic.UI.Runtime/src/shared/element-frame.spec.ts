/**
 * The frame the browser lowers a live element tree through (the twin of the C# `ElementFrame`),
 * asked its three questions and its lookup against a tree the transpiled engine built: the instance
 * a component's position keeps and what it built, the arm an adaptive node's position holds, and a
 * layer shown in a holder's deferred slot. Each refuses a node the frame has no position for, as the
 * C# frame does, rather than letting the lowering draw a fresh instance's defaults in silence.
 */

import { describe, expect, it } from 'vitest';
import { StatefulComponent } from '../core/component';
import type { RenderContext } from '../core/types';
import { BuildOwner } from './components/BuildOwner';
import { ElementFrame } from './element-frame';
import { photonComponentContext } from './photon-context';
import { AdaptiveNode, Column, Overlay, Text, type VisualNode } from './vocabulary';

class Counter extends StatefulComponent {
  label: string;
  count = 0;

  constructor(label: string) {
    super();
    this.label = label;
  }

  adoptConfig(next: Counter): void {
    this.label = next.label;
  }

  build(): VisualNode {
    return new Text(`${this.label}: ${this.count}`, 'bodyM');
  }
}

const context = photonComponentContext() as unknown as RenderContext;
// The web's owner: it holds every arm of an adaptive node, as the browser's owners do.
const owner = () =>
  new BuildOwner((component, built) => component.build(built) as unknown as VisualNode, true);
const column = (...children: VisualNode[]): Column => {
  const node = new Column();
  node.children.push(...children);
  return node;
};

describe('the frame of a built tree', () => {
  it('answers the instance a position keeps, not the fresh one the latest build holds', () => {
    const tree = owner();
    const first = new Counter('a');
    tree.build(column(first as unknown as VisualNode), context, 400, true);
    tree.endFrame();
    first.count = 3;

    const fresh = new Counter('b');
    const root = tree.build(column(fresh as unknown as VisualNode), context, 400, true);
    const position = new ElementFrame(root).at(fresh);

    expect(position.kept).toBe(first);
    expect((position.built as Text).content).toBe('b: 3');
  });

  it('looks up the element a node stands at, with the id the engine gave it at mount', () => {
    const text = new Text('plain', 'bodyM');
    const root = owner().build(column(text), context, 400, true);
    const frame = new ElementFrame(root);

    expect(frame.elementAt(text)).toBe(root.childAt(0));
    expect(frame.elementAt(text)!.id).toBeGreaterThan(0);
    expect(frame.elementAt(new Text('elsewhere', 'bodyM'))).toBeNull();
  });

  it('refuses a component it did not build', () => {
    const frame = new ElementFrame(owner().build(column(), context, 400, true));

    expect(() => frame.at(new Counter('stray'))).toThrow(/did not build/);
  });

  it('holds every arm of an adaptive node, each at the slot of its size class, whatever the window', () => {
    const compact = new Text('compact', 'bodyM');
    const expanded = new Text('expanded', 'bodyM');
    const adaptive = new AdaptiveNode(compact, null, expanded);
    const frame = new ElementFrame(owner().build(adaptive, context, 400, true));

    // The web draws every arm behind its own gate, so each one the node declares is a position.
    expect(frame.elementAt(compact)?.slot).toBe(0);
    expect(frame.elementAt(expanded)?.slot).toBe(2);
  });

  it('builds a layer when it is shown, and knows the positions inside it from then on', () => {
    const layer = new Counter('layer');
    const overlay = new Overlay(layer as unknown as VisualNode);
    const tree = owner();
    const frame = new ElementFrame(tree.build(overlay, context, 400, true));

    // The build phase leaves a layer to the realizer: nothing in it has a position yet.
    expect(frame.elementAt(layer)).toBeNull();

    frame.show(overlay, layer as unknown as VisualNode);

    expect(frame.at(layer).kept).toBe(layer);
    expect(() => frame.show(new Overlay(new Text('x', 'bodyM')), layer as unknown as VisualNode)).toThrow(
      /no position/,
    );
  });
});
