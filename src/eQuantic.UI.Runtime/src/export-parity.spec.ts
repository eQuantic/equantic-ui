import { describe, expect, it } from 'vitest';
import * as indexExports from './index';
import * as runtimeExports from './shared/runtime-exports';

/**
 * The runtime has TWO public doors: `src/index.ts` (what the BOOT bundle — the embedded
 * runtime.js every site serves — re-exports) and `shared/runtime-exports.ts` (what the vite dist
 * exposes). They are maintained as separate lists, and index.ts's own header demands parity.
 * WebFrame proved what happens when one list is updated and not the other: the dist had the
 * export, the served bundle didn't, and every page importing it died at mount — found only in a
 * browser. This spec makes that drift a unit-test failure instead.
 */
describe('runtime export parity', () => {
  it('everything runtime-exports exposes also resolves through index', () => {
    const missing = Object.keys(runtimeExports).filter(name => !(name in indexExports));
    expect(missing).toEqual([]);
  });
});
