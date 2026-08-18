/**
 * The D7 formatting subset's PARITY proof, client half.
 *
 * The fixture is generated from real .NET (`FormatSubsetTests.cs`, EQ_UPDATE_FORMAT_FIXTURE=1):
 * every expected string in it is what `value.ToString(spec, culture)` actually produces on the
 * server. This spec replays the same table through the runtime's formatter with the same culture
 * installed. A `{0:N2}` written once in C# must read the same on both sides — that is the whole
 * promise of the subset, and this is where it is kept honest.
 *
 * When a case cannot be made to agree, the answer is to DROP it from the subset and have EQ2100
 * refuse it at build time. An approximation that silently disagrees with the server is the one
 * outcome the plan forbids.
 */

import { readFileSync } from 'node:fs';
import { describe, expect, it } from 'vitest';
import { installCulture } from '../utils/culture';
import { dateTime } from '../utils/datetime';
import { format, stringFormat } from '../utils/format';

const fixture = readFileSync('src/shared/__fixtures__/format-subset.txt', 'utf8');

/** ICU's non-breaking spaces, folded — the C# dumper folds the same two, and for the same reason:
 * the exact codepoint moved between ICU versions and the two runtimes may carry different builds. */
const normalize = (value: string): string => value.replace(/ /g, ' ').replace(/ /g, ' ');

interface Case {
  kind: 'num' | 'int' | 'date';
  culture: string;
  value: string;
  spec: string;
  expected: string;
}

/** A row of the INVARIANT section: no culture, because the whole claim is that it does not matter. */
interface InvariantCase {
  value: string;
  spec: string;
  expected: string;
}

/** The catalog a culture would carry in production: its currency code and its own date patterns,
 * both written by the build from the same .NET source this fixture was generated from. */
type CultureFacts = Record<string, string>;

function parseFixture(): {
  facts: Record<string, CultureFacts>;
  cases: Case[];
  invariant: InvariantCase[];
} {
  const facts: Record<string, CultureFacts> = {};
  const cases: Case[] = [];
  const invariant: InvariantCase[] = [];
  for (const line of fixture.split('\n')) {
    if (line.length === 0) continue;
    // Four fields, not five: an invariant row has no culture column, and reading it as one put the
    // VALUE where the culture belongs and shifted every field after it.
    if (line.startsWith('inv|')) {
      const [, value, spec, ...rest] = line.split('|');
      invariant.push({ value, spec, expected: rest.join('|') });
      continue;
    }
    if (line.startsWith('culture ')) {
      // Split on the FIRST space only: a pattern is full of spaces ("dddd, MMMM d, yyyy"), and
      // splitting on all of them silently truncated every culture's date facts.
      const header = line.slice('culture '.length);
      const cut = header.indexOf(' ');
      const name = header.slice(0, cut);
      const rest = header.slice(cut + 1);
      const [currency, dateShort, dateLong, timeShort, timeLong, monthDay, yearMonth] =
        rest.split('|');
      facts[name] = {
        $currency: currency,
        $dateShort: dateShort,
        $dateLong: dateLong,
        $timeShort: timeShort,
        $timeLong: timeLong,
        $monthDay: monthDay,
        $yearMonth: yearMonth,
      };
      continue;
    }
    const [kind, culture, value, spec, ...rest] = line.split('|');
    cases.push({ kind: kind as Case['kind'], culture, value, spec, expected: rest.join('|') });
  }
  return { facts, cases, invariant };
}

/** The date arrives as LOCAL parts, exactly as the C# side wrote them — a fixture that crossed a
 * time zone would be pinning the machine, not the culture. */
function localDate(iso: string): Date {
  const [date, time] = iso.split('T');
  const [y, m, d] = date.split('-').map(Number);
  const [hh, mm, ss] = time.split(':').map(Number);
  return new Date(y, m - 1, d, hh, mm, ss);
}

describe('D7 formatting subset (cross-pinned with FormatSubsetTests.cs)', () => {
  const { facts, cases, invariant } = parseFixture();

  it('covers every culture in the fixture', () => {
    expect(Object.keys(facts).length).toBeGreaterThan(2);
    expect(cases.length).toBeGreaterThan(100);
  });

  it('the compat DateTime formats through the same culture path as a native Date', () => {
    // What `new DateTime(2026, 8, 13, 15, 45, 7)` transpiles to. It used to fall through to
    // String(value) — the invariant default — so the most naturally written date on a page was
    // the one that ignored the culture.
    installCulture('pt-BR', 'pt-BR', facts['pt-BR']);
    const moment = dateTime(2026, 8, 13, 15, 45, 7);
    expect(format(moment, 'd')).toBe('13/08/2026');
    expect(format(moment, 't')).toBe('15:45');
    expect(normalize(stringFormat('{0:d}', moment))).toBe('13/08/2026');
  });

  for (const culture of Object.keys(facts)) {
    it(`formats exactly like .NET in ${culture}`, () => {
      // Exactly the catalog production carries: the currency code and the culture's own
      // patterns, written by the build from the .NET data this fixture was generated from.
      installCulture(culture, culture, facts[culture]);

      const mismatches: string[] = [];
      for (const testCase of cases.filter((c) => c.culture === culture)) {
        const value =
          testCase.kind === 'date' ? localDate(testCase.value) : Number(testCase.value);
        // An EMPTY spec is `{0}` — the no-specifier placeholder — so it must go through the
        // composite formatter, which is where .NET's per-culture default lives.
        const actual = normalize(
          testCase.spec.length === 0
            ? stringFormat('{0}', value)
            : format(value, testCase.spec),
        );
        if (actual !== testCase.expected)
          mismatches.push(`${testCase.kind} ${testCase.value}:${testCase.spec} → "${actual}" ≠ "${testCase.expected}"`);
      }
      expect(mismatches).toEqual([]);
    });
  }

  /**
   * `ToString(CultureInfo.InvariantCulture)` — what an author writes for a number a MACHINE reads,
   * so that a CSS length or a key keeps its shape whoever is reading the page. The proof is the
   * hostile one: pt-BR is installed and active, and every one of these still has to come out with
   * a dot, matching what real .NET produced on the other side of the fixture.
   */
  it('formats invariantly even with another culture installed', () => {
    installCulture('pt-BR', 'pt-BR', facts['pt-BR']);
    expect(invariant.length).toBeGreaterThan(10);

    const mismatches: string[] = [];
    for (const testCase of invariant) {
      const value = Number(testCase.value);
      // No specifier is `String(x)` — which is what the transpiler emits for the same shape, and
      // is already .NET's invariant rendering of a number.
      const actual = normalize(
        testCase.spec.length === 0 ? String(value) : format(value, testCase.spec, undefined, true),
      );
      if (actual !== testCase.expected)
        mismatches.push(`${testCase.value}:${testCase.spec} → "${actual}" ≠ "${testCase.expected}"`);
    }
    expect(mismatches).toEqual([]);
  });
});
