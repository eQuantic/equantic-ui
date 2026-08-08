import { describe, expect, it } from 'vitest';
import * as indexExports from './index';
import * as runtimeExports from './shared/runtime-exports';
import * as vocabulary from './shared/vocabulary';

/**
 * The runtime has TWO public doors — `src/index.ts` (what the BOOT bundle, the embedded
 * runtime.js every site serves, re-exports) and `shared/runtime-exports.ts` (what the vite dist
 * exposes, and what every TRANSPILED component twin imports from). They are maintained as
 * separate lists, and drift between them is invisible until something fails far away:
 * WebFrame missing from one served a bundle without the export; AdaptiveNode/Sticky/Vector
 * missing from the other broke the bun build the moment a twin referenced them. Both directions
 * are checked here, so the failure lands in this file instead.
 */
describe('runtime export parity', () => {
  it('everything runtime-exports exposes also resolves through index', () => {
    const missing = Object.keys(runtimeExports).filter(name => !(name in indexExports));
    expect(missing).toEqual([]);
  });

  it('every vocabulary NODE the twins can reference is exported by runtime-exports', () => {
    // A transpiled twin imports the vocabulary from runtime-exports by name. Anything the
    // vocabulary exports as a node class must therefore be reachable there.
    const nodes = Object.entries(vocabulary)
      .filter(([, value]) => typeof value === 'function'
        && Object.prototype.isPrototypeOf.call(vocabulary.VisualNode, value))
      .map(([name]) => name);
    const missing = nodes.filter(name => !(name in runtimeExports));
    expect(missing).toEqual([]);
  });
});
