import { describe, it, expect } from 'vitest';
import { compare, compareRange, compareRangeBy, equals, joinRange } from './string-statics';

// Every answer below was measured on .NET 10 with the invariant culture; the conformance suite runs
// the same calls on both sides.
describe('compare (string.Compare)', () => {
  it('orders a null first', () => {
    expect(compare(null, 'a', 'currentCulture')).toBe(-1);
    expect(compare('a', null, 'ordinal')).toBe(1);
    expect(compare(null, null, 'ordinalIgnoreCase')).toBe(0);
  });

  it("answers the culture's order, case-blind where asked", () => {
    expect(compare('a', 'B', 'currentCulture')).toBe(-1);
    expect(compare('a', 'A', 'currentCulture')).toBe(-1);
    expect(compare('a', 'A', 'currentCultureIgnoreCase')).toBe(0);
    expect(compare('ab', 'a\u00adb', 'currentCulture')).toBe(0);
  });

  it("answers an ordinal comparison's difference", () => {
    expect(compare('a', 'c', 'ordinal')).toBe(-2);
    expect(compare('abc', 'ab', 'ordinal')).toBe(1);
    expect(compare('a', 'C', 'ordinalIgnoreCase')).toBe(-2);
    expect(compare('\u00e9', 'E', 'ordinalIgnoreCase')).toBe(132);
  });

  it('checks the comparison before anything else', () => {
    expect(() => compare(null, null, 'nope' as never)).toThrow(
      "The string comparison type passed in is currently not supported. (Parameter 'comparisonType')",
    );
  });
});

describe('compareRange (string.Compare over two ranges)', () => {
  it('clamps each range to its string', () => {
    expect(compareRange('xabc', 1, 'abd', 0, 2, false)).toBe(0);
    expect(compareRange('ab', 0, 'abc', 0, 5, false)).toBe(-1);
    expect(compareRange('xABc', 1, 'abD', 0, 2, true)).toBe(0);
  });

  it('gives a null no range, and checks one as CompareInfo does', () => {
    expect(compareRange(null, 0, 'a', 0, 0, false)).toBe(-1);
    expect(() => compareRange(null, 0, 'a', 0, 1, false)).toThrow(
      "Offset and length must refer to a position in the string. (Parameter 'string1')",
    );
    expect(() => compareRange('ab', 3, 'ab', 0, 1, false)).toThrow(
      "length1 ('-1') must be a non-negative value.",
    );
  });

  it('answers a null before reading a range when a comparison is named', () => {
    expect(compareRangeBy(null, 0, 'a', 0, 5, 'ordinal')).toBe(-1);
    expect(compareRangeBy('ab', 2, 'cd', 0, 0, 'ordinal')).toBe(0);
    expect(() => compareRangeBy('ab', 3, 'ab', 0, 0, 'ordinal')).toThrow("(Parameter 'indexA')");
  });
});

describe('equals (string.Equals with a comparison)', () => {
  it('equals a null only to a null', () => {
    expect(equals(null, null, 'ordinalIgnoreCase')).toBe(true);
    expect(equals(null, 'a', 'ordinalIgnoreCase')).toBe(false);
  });

  it("upper-cases as .NET's ordinal comparison does", () => {
    expect(equals('\u00e9', '\u00c9', 'ordinalIgnoreCase')).toBe(true);
    expect(equals('\u212a', 'k', 'ordinalIgnoreCase')).toBe(false); // the Kelvin sign
    expect(equals('\u017f', 's', 'ordinalIgnoreCase')).toBe(false); // the long s
    expect(equals('\u0131', 'I', 'ordinalIgnoreCase')).toBe(false); // the dotless i
    expect(equals('\u00df', 'SS', 'ordinalIgnoreCase')).toBe(false);
    expect(equals('\u1f80', '\u1f88', 'ordinalIgnoreCase')).toBe(true); // iota subscript, title case
  });

  it('equals canonically equal text in a culture', () => {
    expect(equals('a\u0301', '\u00e1', 'invariantCulture')).toBe(true);
    expect(equals('a\u0301', '\u00e1', 'ordinal')).toBe(false);
  });
});

describe('joinRange (string.Join over a range)', () => {
  it('joins the range, a null element as nothing', () => {
    expect(joinRange(',', ['a', 'b', 'c'], 1, 2)).toBe('b,c');
    expect(joinRange('-', ['a', null, 'c'], 0, 3)).toBe('a--c');
    expect(joinRange(',', ['a', 'b'], 2, 0)).toBe('');
  });

  it('checks the range', () => {
    expect(() => joinRange(',', ['a'], 1, 1)).toThrow(
      "startIndex ('1') must be less than or equal to '0'.",
    );
    expect(() => joinRange(',', ['a'], -1, 1)).toThrow(
      "startIndex ('-1') must be a non-negative value.",
    );
    expect(() => joinRange(',', null, 0, 0)).toThrow("Value cannot be null. (Parameter 'value')");
  });
});
