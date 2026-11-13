/**
 * FOR SPECS: positions an owner mounted, one per node, so a spec can drive what keeps state on a
 * node's position (the after-pass registries, `node-states.ts`) without lowering a whole tree.
 * Nothing in the runtime imports it, so no bundle carries it.
 */

import type { RenderContext } from '../core/types';
import { BuildOwner } from './components/BuildOwner';
import { ElementFrame } from './element-frame';
import { ElementRoots, enterPass, exitPass } from './element-roots';
import { photonComponentContext } from './photon-context';
import type { Element } from './runtime-exports';
import { Column, type VisualNode } from './vocabulary';

/** The owner a spec's tree is built through: a structural one, which calls no lifecycle hook. */
export function specOwner(): BuildOwner {
  return BuildOwner.structural((component, built) => component.build(built) as unknown as VisualNode);
}

/** Builds `root` through `owner` (a new structural one when none is handed) and answers its frame. */
export function frameOf(root: VisualNode, owner: BuildOwner = specOwner()): ElementFrame {
  return new ElementFrame(
    owner.build(root, photonComponentContext() as unknown as RenderContext, 'compact', true),
  );
}

/** The position each of `nodes` stands at, mounted side by side under one column. */
export function positionsOf(...nodes: VisualNode[]): Element[] {
  const column = new Column();
  column.children.push(...nodes);
  const frame = frameOf(column);
  return nodes.map((node) => {
    const position = frame.elementAt(node);
    if (!position) throw new Error('The owner mounted no position for a node handed to it.');
    return position;
  });
}

/**
 * Renders as a page does, one pass after another of the same roots, so what is lowered again stands
 * at the position it stood at and keeps its id and its state (#1454). A lowering outside a pass is a
 * tree drawn once, whose every node is new each time: a spec that models a render after a render
 * lowers through this.
 */
export function rendersOfOnePage(): <T>(render: () => T) => T {
  const roots = new ElementRoots(null);
  return (render) => {
    enterPass(roots, null, true);
    try {
      return render();
    } finally {
      exitPass();
    }
  };
}
