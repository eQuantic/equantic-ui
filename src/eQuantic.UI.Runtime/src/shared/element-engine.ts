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

import type { BuildOwner, ComponentElement, NodeElement } from './runtime-exports';

export interface ElementEngine {
  readonly BuildOwner: typeof BuildOwner;
  readonly ComponentElement: typeof ComponentElement;
  readonly NodeElement: typeof NodeElement;
}

let engine: ElementEngine | null = null;

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
