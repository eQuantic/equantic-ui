/**
 * ONE FRAME OF A LIVE ELEMENT TREE, as the browser's lowering walks it: the position each node and
 * component of the frame stands at, so a component is lowered from what its position BUILT in the
 * frame rather than built again. The twin of the C# `ElementFrame` (src/eQuantic.UI.Web), which the
 * web realizer lowers a frame of a live tree through, and asked the same three questions in the same
 * places.
 *
 * Hand-written beside the lowering, as the lowering itself is: the element engine is written once,
 * in C#, and transpiled into `components/` (docs/ELEMENT-TREE-PLAN.md 3.9), and the index over a
 * tree it built is the realizer's, on each side.
 *
 * A FIELD OF THE LOWERING'S CONTEXT, not an ambient (`LoweringContext.frame`), for the reason the C#
 * frame is a field of its visitor: the walk that lowers a tree is the one scope the frame belongs to.
 *
 * `elementAt` is the lookup the rest of the lowering reads a position through: the element a node
 * stands at, and through it the integer id the engine gave the position when it mounted, which is
 * what a node's identity becomes when the paths go (E3b-2, #1454).
 */

// The engine's types through the barrel the generated modules import from, not their files: reached
// directly, tsc meets the engine's modules in another order and reports an inference cycle inside
// the generated `Element.ts` (TS7023), which nobody may edit by hand. Its classes come through the
// seam the barrel registers them in (element-engine.ts says why).
import type { Element } from './runtime-exports';
import { elementEngine } from './element-engine';
import type { UiComponent } from '../core/component';
import type { VisualNode } from './vocabulary';

/** What a component's position holds in a frame: the instance it keeps and what that instance built. */
export interface ComponentPosition {
  /** The instance the position KEEPS, which is not the one a parent's latest build holds. */
  readonly kept: UiComponent;
  /** What the kept instance built in this frame, or null when it built nothing. */
  readonly built: VisualNode | null;
}

export class ElementFrame {
  private readonly positions = new Map<object, Element>();

  constructor(root: Element) {
    this.index(root);
  }

  /** The position `node` stands at in this frame, or null when the frame holds no position for it. */
  elementAt(node: object): Element | null {
    return this.positions.get(node) ?? null;
  }

  /**
   * The position of `component` in this frame: the instance it keeps and what that instance built.
   * The kept one is also the one the server's data was handed to, so the next frame draws it.
   *
   * Throws when the frame has no position for the component: the engine went wrong, and lowering
   * the fresh instance instead would draw its defaults in silence (the C# frame refuses alike).
   */
  at(component: object): ComponentPosition {
    const element = this.positions.get(component);
    if (element instanceof elementEngine().ComponentElement) {
      return { kept: element.component, built: element.childAt(0)?.configuration ?? null };
    }
    throw new Error(
      `A ${nameOf(component)} was lowered that this frame did not build: the element tree and the ` +
        'tree being lowered disagree.',
    );
  }

  /**
   * Shows `layer` in the deferred slot of `holder`'s position (an overlay's child, an anchored
   * panel), so the owner builds it now, with the instances it keeps there, and indexes what it built.
   * The web decides that a layer shows whenever it lowers it: one kept mounted while closed, for its
   * motion, is shown too. A frame that lowers no layer leaves the slot to be taken out at its end.
   *
   * Returns the same layer, now with a position for every component in it.
   */
  show(holder: object, layer: VisualNode): VisualNode {
    const element = this.positions.get(holder);
    if (!(element instanceof elementEngine().NodeElement)) {
      throw new Error(`A ${nameOf(holder)}'s layer was lowered that this frame has no position for.`);
    }
    this.index((element as unknown as DeferredHolder).showDeferred(layer));
    return layer;
  }

  /**
   * Takes in the positions of a tree built BESIDE the one this frame was made from: an arm the web
   * draws next to the one an adaptive node's position holds, which the C# visitor lowers with no
   * frame and the browser builds through a structural owner for one render (`lowering.ts`,
   * `holdArm`), so every component in it still has a position the lowering can ask about.
   */
  include(root: Element): void {
    this.index(root);
  }

  /**
   * Every position under `root`, by the node it holds. A node placed at two positions keeps the one
   * indexed first: a stateful instance placed twice builds the same tree at both. The same walk as
   * the C# frame's, a stack, so the two keep the same one.
   */
  private index(root: Element): void {
    const pending: Element[] = [root];
    while (pending.length > 0) {
      const element = pending.pop()!;
      if (!this.positions.has(element.configuration)) this.positions.set(element.configuration, element);
      element.visitChildren((child) => pending.push(child));
    }
  }
}

/**
 * The one member of a `NodeElement` the frame calls, declared here rather than read off the
 * generated class: its return type is inferred from `updateChild`, whose own inference runs back
 * through it, and asking for it first from outside the engine's modules is a circularity tsc reports
 * against the generated `Element.ts` (TS7023), a file nobody may annotate by hand.
 */
interface DeferredHolder {
  showDeferred(configuration: VisualNode): Element;
}

function nameOf(node: object): string {
  return (node as { constructor?: { name?: string } }).constructor?.name ?? 'node';
}
