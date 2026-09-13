/**
 * The family rule, read from the SAME fixture the C# side reads.
 *
 * A cross-pin rather than two hand-written tables: SSR emits `font-family` through
 * `FaceName.IsWellFormed` and hydration emits it through `isWellFormedFace`, so a family one side
 * accepts and the other rejects is a hydration mismatch — and the cases that matter most are the
 * ones neither side should ever accept.
 */

import { describe, expect, it } from 'vitest';
import fixture from './face-names.fixture.json';
import { FaceName, isWellFormedFace } from './value-types';

interface Case {
  family: string;
  wellFormed: boolean;
  why: string;
}

const CASES = fixture.cases as Case[];

describe('font family rule (C# ⇄ TS cross-pin)', () => {
  it.each(CASES)('$why', ({ family, wellFormed }) => {
    expect(isWellFormedFace(family)).toBe(wellFormed);
  });

  it('the exported twin is the same rule, not a second one', () => {
    for (const { family, wellFormed } of CASES) {
      expect(FaceName.isWellFormed(family)).toBe(wellFormed);
    }
  });

  // A C# `string?` crosses the wire AS null, not as undefined, and an unnamed face is the
  // documented default rather than an error — so this must answer, not throw, in the middle of
  // hydration.
  it('an absent face is an answer, not a crash', () => {
    expect(isWellFormedFace(null)).toBe(false);
    expect(isWellFormedFace(undefined)).toBe(false);
  });

  // The fixture is hand-authored, so it is worth asserting it still contains the cases it exists
  // for. A table that quietly loses its injection rows keeps passing and stops proving anything.
  it('still carries the two cases the rule was written for', () => {
    const rejected = CASES.filter((c) => !c.wellFormed).map((c) => c.family);

    expect(rejected).toContain('</script>');
    expect(rejected.some((f) => f.includes('</style>'))).toBe(true);
  });
});
