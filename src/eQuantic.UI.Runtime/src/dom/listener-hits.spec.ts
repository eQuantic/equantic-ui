import { describe, expect, it } from 'vitest';
import fixture from '../shared/listener-hits.fixture.json';
import type { HitTestBehaviorValue } from '../shared/enums.generated';
import { listenerHits, type UnderEntry } from './listener-hits';

/**
 * The listeners' hit rule (#1562) runs every case of the fixture the C# `ListenerHits` runs too
 * (`ListenerHitsFixtureTests`): the two writings of one rule cannot drift.
 */
interface CaseEntry {
  id?: string;
  listener?: string;
  solid?: boolean;
  children?: CaseEntry[];
}

/** The case's tree in paint order: an entry, then what it holds, then what is painted after it. */
function flatten(entries: readonly CaseEntry[], into: { entry: UnderEntry; id?: string }[] = []) {
  for (const entry of entries) {
    const at = into.length;
    into.push({
      id: entry.id,
      entry: {
        start: at,
        end: -1,
        solid: entry.solid === true,
        behavior: (entry.listener as HitTestBehaviorValue | undefined) ?? null,
      },
    });
    flatten(entry.children ?? [], into);
    into[at].entry.end = into.length;
  }
  return into;
}

describe('the listeners hit rule, every case of the fixture', () => {
  for (const testCase of fixture.cases) {
    it(testCase.name, () => {
      const flat = flatten(testCase.under as CaseEntry[]);
      const heard = listenerHits(flat.map((item) => item.entry)).map((index) => flat[index].id);
      expect(heard).toEqual(testCase.hear);
    });
  }

  it('holds the cases the issue names', () => {
    expect(fixture.cases.length).toBeGreaterThanOrEqual(16);
  });
});
