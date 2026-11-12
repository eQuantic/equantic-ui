/**
 * WHAT THE BROWSER KEEPS FOR A NODE BETWEEN RENDERS, kept on the node's position
 * (`NodeElement.state`): a scroll view's adoption and its observer, a table's last report, an in-view
 * observer, a filling canvas's, the code editor's last reveal. The twin of Photon's `NodeStates`
 * (src/eQuantic.UI.Native.Framework/NodeState), asked the same two questions with the same refusal.
 *
 * It belongs to the POSITION, so it leaves the tree with it, and the next node drawn at its place
 * starts with its own (docs/ELEMENT-TREE-PLAN.md, 3.1 and 3.7). Before, each lived in a module map
 * keyed by the node's lowering path or as a property on the DOM element, and a node that took
 * another's path, or another's DOM element, inherited what the one before it had kept.
 *
 * A position keeps ONE state, because the kinds are disjoint by node type: a scroll view keeps a
 * scroll, a code surface its reveal. A position asked for a kind other than the one it keeps is the
 * engine gone wrong, and is refused naming both.
 *
 * A state that holds something to let go of (an observer) has a `dispose`, which the engine calls when
 * the position leaves the tree, after the states below it, as Flutter disposes a `State` on unmount. The
 * engine asks it as .NET does, whether the state IS an `IDisposable` (#1464), so each state's class says
 * so (`static $interfaces = DISPOSABLE`), as the twins the compiler writes do.
 */

// TYPES only: the lowering imports this module, and a value read through the barrel the engine's
// modules are reached by would load every generated module while the lowering's own imports are
// still being evaluated, so a component class would extend a base not yet defined.
import type { Element, NodeElement } from './runtime-exports';
import type { ElementFrame } from './element-frame';

/** The interfaces a node state's class declares, as a compiled twin declares them: it is disposable. */
export const DISPOSABLE: readonly string[] = ['System.IDisposable'];

/** What every node state is: it lets go of what it holds when its position leaves the tree. */
export interface NodeState {
  dispose(): void;
}

/**
 * The position `node` stands at in `frame`: what a node's identity and its state are read from.
 * Refused when there is no frame or it holds no position for the node, as the frame's `at` refuses a
 * component: every tree the browser lowers is built through an owner first, and a node lowered
 * outside one would key its state by nothing and lose it in silence.
 */
export function positionOf(frame: ElementFrame | undefined, node: object): Element {
  const element = frame?.elementAt(node);
  if (element) return element;
  throw new Error(
    `A ${nameOf(node)} was lowered that ${frame ? 'this frame did not build' : 'no element tree built'}: ` +
      'its identity and its state belong to its position.',
  );
}

/** The state of `kind` that `at` keeps, or null when it keeps none yet. */
export function findNodeState<T extends NodeState>(at: Element, kind: abstract new () => T): T | null {
  const state = asNode(at).state as NodeState | null;
  if (state == null) return null;
  if (state instanceof kind) return state;
  throw mismatch(at, state, kind);
}

/** The state of `kind` that `at` keeps, made the first time it is asked for. */
export function nodeStateFor<T extends NodeState>(at: Element, kind: new () => T): T {
  const node = asNode(at);
  const state = node.state as NodeState | null;
  if (state == null) {
    const made = new kind();
    node.state = made;
    return made;
  }
  if (state instanceof kind) return state;
  throw mismatch(at, state, kind);
}

/** A node's position, told from a component's by the slot only a node's carries. */
function asNode(at: Element): NodeElement {
  if ('state' in at) return at as NodeElement;
  throw new Error(`A ${nameOf(at.configuration)}'s position keeps no node state: it is a component's.`);
}

function mismatch(at: Element, state: object, kind: { name: string }): Error {
  return new Error(
    `The position of a ${nameOf(at.configuration)} keeps a ${nameOf(state)} and was asked for a ` +
      `${kind.name}: a position keeps one kind of state.`,
  );
}

function nameOf(value: object): string {
  return (value as { constructor?: { name?: string } }).constructor?.name ?? 'node';
}
