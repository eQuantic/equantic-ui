/**
 * The transpiled element engine, as the lowering and its frame reach it: registered by the runtime's
 * export barrel (`runtime-exports.ts`) once every module it exports has loaded, and read when a tree
 * is built, never while modules load.
 *
 * A seam rather than an import, for the reason the failure renderer is one (`component-boundary.ts`).
 * The engine's modules are generated, and they import the barrel, which loads every component the
 * runtime ships; some of those extend a class of the vocabulary as they load (`LeadingSpans extends
 * TableSpans`), and the vocabulary imports the lowering. A lowering that imported the engine would
 * load every component while the vocabulary it extends was still loading, and the first such class
 * fails with "Class extends value undefined". Measured: twenty-two spec files failed to load.
 *
 * The served bundle loads the barrel before anything renders, and so does the test setup
 * (`test-runtime.ts`); a lowering that runs before it says so instead of building nothing.
 */

import type {
  BuildContext,
  BuildOwner,
  ComponentElement,
  ElementIds,
  NodeElement,
  UiComponent,
  VisualNode,
} from './runtime-exports';
import { noteBuilt } from './lowered-records';

export interface ElementEngine {
  readonly BuildOwner: typeof BuildOwner;
  readonly ComponentElement: typeof ComponentElement;
  readonly ElementIds: typeof ElementIds;
  readonly NodeElement: typeof NodeElement;
}

let engine: ElementEngine | null = null;

/**
 * THE DOCUMENT'S COUNT of element ids, handed to every owner made below (#1454). A page owns a tree
 * per root it lowers, an island one of its own, and a lowering with no page around it a structural one
 * it discards; each counting alone would give two nodes of one document the same id, and the
 * attributes the after-passes find a node by carry that id. One count also keeps an id from being
 * given again across a navigation, so a page that leaves and the one that arrives never share one
 * during the commit that swaps them. Made with the first owner, since the engine loads after this
 * module.
 */
let documentIds: ElementIds | null = null;

function idsOfTheDocument(): ElementIds {
  return (documentIds ??= new (elementEngine().ElementIds)());
}

export function registerElementEngine(registered: ElementEngine): void {
  engine = registered;
}

export function elementEngine(): ElementEngine {
  if (engine === null) {
    throw new Error(
      'The element engine is not loaded: load the runtime (`@equantic/runtime`) before a tree is lowered.',
    );
  }
  return engine;
}

/** How a component builds, as an owner is handed it: its build, for a context, at a depth of components. */
export type BuildComponent = (
  component: UiComponent,
  context: BuildContext,
  depth: number,
  owner: BuildOwner,
) => VisualNode;

/**
 * THE ONE PLACE the browser makes a live owner: a tree kept across renders, which mounts and unmounts
 * what it holds and asks for a render when a position is marked. Every owner the runtime makes comes
 * from here or from `structuralOwner`, so what each one is handed is decided once.
 */
export function liveOwner(build: BuildComponent): BuildOwner {
  // The web draws every arm of an adaptive node behind its own media gate, so each is a position.
  // The owner is handed to every build it runs, so the build can ask which position it is for
  // (`owner.building`): the server's data reaches a component by where it stands.
  const owner: BuildOwner = new (elementEngine().BuildOwner)(
    (component: UiComponent, context: BuildContext, depth: number) => {
      // What a render the page's owners asked for lowers again (lowered-records.ts, #1455).
      noteBuilt(owner.building);
      return build(component, context, depth, owner);
    },
    true,
    idsOfTheDocument(),
  );
  return owner;
}

/** THE ONE PLACE the browser makes a structural owner: a tree laid out once and discarded, which calls
 *  no lifecycle hook. */
export function structuralOwner(build: BuildComponent): BuildOwner {
  const owner: BuildOwner = elementEngine().BuildOwner.structural(
    (component: UiComponent, context: BuildContext, depth: number) => build(component, context, depth, owner),
    true,
    idsOfTheDocument(),
  );
  return owner;
}
