/**
 * The SECOND hop of the error overlay's stack remap — the one that was always missing. Hop one
 * (the bundle's own map) lands in the TS intermediates, which is true and useless to a developer
 * who never wrote TS; this walks each intermediate's eqc map into the C# file, line and text.
 *
 * Fetchers are injected, exactly like the June spec for hop one — the logic runs without a
 * browser or a network, against maps shaped like the real pipeline's.
 */

import { describe, expect, it } from 'vitest';
import { remapFramesToCSharp, type RemappedFrame } from './stack-remapper';
import type { RawSourceMap } from './source-map';

// A stage-one map the way eqc writes them: one source (the C# file), its text embedded, and
// line-level mappings. "AACA" = [0,0,1,0]: generated line 1 col 0 ← source 0, line 2 (0-based 1).
const STAGE_ONE: RawSourceMap = {
  version: 3,
  sources: ['/app/Screens/PaymentsPage.cs'],
  sourcesContent: ['using eQuantic;\n\npublic class PaymentsPage\n{\n    // line five\n}\n'],
  names: [],
  // gen line 1 → cs line 1; gen line 2 → cs line 3; gen line 3 → cs line 5
  mappings: 'AAAA;AAEA;AAEA',
};

const tsFrame = (line: number, column = 0): RemappedFrame => ({
  func: 'build',
  file: '../../obj/eQuantic/ts/PaymentsPage.ts',
  line,
  column,
  mapped: true,
  sourceContent: 'const ts = true;',
});

describe('remapFramesToCSharp (hop two)', () => {
  it('walks a TS intermediate frame into the C# file, line and text', async () => {
    const frames = await remapFramesToCSharp([tsFrame(2)], async (name) => {
      expect(name).toBe('PaymentsPage.ts');
      return STAGE_ONE;
    });

    expect(frames[0].mapped).toBe(true);
    expect(frames[0].file).toBe('/app/Screens/PaymentsPage.cs');
    expect(frames[0].line).toBe(3);
    expect(frames[0].sourceContent).toContain('public class PaymentsPage');
  });

  it('leaves a frame ALONE when the stage-one map is missing', async () => {
    const frames = await remapFramesToCSharp([tsFrame(2)], async () => null);

    expect(frames[0].file).toBe('../../obj/eQuantic/ts/PaymentsPage.ts');
    expect(frames[0].mapped).toBe(true);
    // a true TS answer beats a guessed C# one
    expect(frames[0].line).toBe(2);
  });

  it('does not touch frames that never reached an intermediate', async () => {
    const jsFrame: RemappedFrame = {
      func: null,
      file: 'http://localhost/_equantic/runtime.js',
      line: 9,
      column: 4,
      mapped: false,
      sourceContent: null,
    };
    let fetches = 0;
    const frames = await remapFramesToCSharp([jsFrame], async () => {
      fetches++;
      return STAGE_ONE;
    });

    expect(frames[0]).toEqual(jsFrame);
    // nothing to fetch for a frame with no intermediate
    expect(fetches).toBe(0);
  });

  it('fetches each intermediate map ONCE however many frames share it', async () => {
    let fetches = 0;
    await remapFramesToCSharp([tsFrame(1), tsFrame(2), tsFrame(3)], async () => {
      fetches++;
      return STAGE_ONE;
    });

    expect(fetches).toBe(1);
  });
});
