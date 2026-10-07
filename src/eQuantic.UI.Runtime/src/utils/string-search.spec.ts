import { describe, it, expect } from 'vitest';
import {
  compareTo,
  contains,
  endsWith,
  indexOf,
  indexOfChar,
  instanceEquals,
  lastIndexOf,
  lastIndexOfChar,
  replace,
  startsWith,
} from './string-search';

// Every answer below was measured on .NET 10 with the invariant culture; the conformance suite runs
// the same calls on both sides.
const kelvin = '\u212a';
const longS = '\u017f';
const dotless = '\u0131';
const lower = '\ud801\udc28'; // U+10428, whose upper case is U+10400
const upper = '\ud801\udc00';
const notSupported =
  "The string comparison type passed in is currently not supported. (Parameter 'comparisonType')";

describe("an ordinal search that ignores case, as .NET's OrdinalCasing", () => {
  it("keeps .NET's exceptions to the upper case", () => {
    expect(startsWith(kelvin + 'x', 'k', 'ordinalIgnoreCase')).toBe(false);
    expect(startsWith('Kx', 'k', 'ordinalIgnoreCase')).toBe(true);
    expect(endsWith('x' + longS, 'S', 'ordinalIgnoreCase')).toBe(false);
    expect(startsWith(dotless, 'I', 'ordinalIgnoreCase')).toBe(false);
    expect(contains('x' + kelvin, 'k', 'ordinalIgnoreCase')).toBe(false);
    expect(indexOf('a' + kelvin + 'k', 'K', 'ordinalIgnoreCase')).toBe(2);
    expect(indexOf(kelvin + 'k', 'K', 'ordinalIgnoreCase')).toBe(1);
    expect(indexOf('x' + longS, 'S', 'ordinalIgnoreCase')).toBe(-1);
    expect(startsWith('\u1f80x', '\u1f88', 'ordinalIgnoreCase')).toBe(true); // iota subscript to title case
  });

  it('reads a pair by its code point, and finds a lone half inside one', () => {
    expect(startsWith(lower + 'x', upper, 'ordinalIgnoreCase')).toBe(true);
    expect(indexOf('x' + lower + 'y', upper, 'ordinalIgnoreCase')).toBe(1);
    expect(indexOf('x' + lower, '\udc28', 'ordinalIgnoreCase')).toBe(2);
    expect(indexOf('x' + lower, '\ud801', 'ordinalIgnoreCase')).toBe(1);
    expect(lastIndexOf(lower + lower, '\ud801', 'ordinalIgnoreCase')).toBe(2);
    expect(indexOf('a\ud801', 'A\ud801', 'ordinalIgnoreCase')).toBe(0);
    expect(indexOf('A' + lower, 'a' + upper, 'ordinalIgnoreCase')).toBe(0);
    expect(indexOf(lower + 'A', upper + 'a', 'ordinalIgnoreCase')).toBe(0);
  });

  it('replaces every match, left to right, without overlap', () => {
    expect(replace('aAa', 'a', 'x', 'ordinalIgnoreCase')).toBe('xxx');
    expect(replace('aAaA', 'AA', 'x', 'ordinalIgnoreCase')).toBe('xx');
    expect(replace('x' + kelvin, 'k', 'y', 'ordinalIgnoreCase')).toBe('x' + kelvin);
    expect(replace('aBc', 'b', null, 'ordinalIgnoreCase')).toBe('ac');
  });
});

describe('the start and the count, checked and normalized as .NET does', () => {
  it('searches forward inside the range', () => {
    expect(indexOf('abc', '', 2, 'ordinalIgnoreCase')).toBe(2);
    expect(indexOf('abc', '', 3, 'ordinalIgnoreCase')).toBe(3);
    expect(indexOf('abcb', 'B', 2, 'ordinalIgnoreCase')).toBe(3);
    expect(indexOf('abcb', 'B', 1, 1, 'ordinalIgnoreCase')).toBe(1);
    expect(indexOf('abcb', 'B', 2, 1, 'ordinalIgnoreCase')).toBe(-1);
    expect(indexOf('abcb', 'b', 2, 1, 'ordinal')).toBe(-1);
  });

  it('searches back from the start, which may be one past the end', () => {
    expect(lastIndexOf('kAk', 'a', 'ordinalIgnoreCase')).toBe(1);
    expect(lastIndexOf('abc', '', 'ordinalIgnoreCase')).toBe(3);
    expect(lastIndexOf('abc', '', 'ordinal')).toBe(3);
    expect(lastIndexOf('abcb', 'B', 2, 'ordinalIgnoreCase')).toBe(1);
    expect(lastIndexOf('abcb', 'B', 3, 2, 'ordinalIgnoreCase')).toBe(3);
    expect(lastIndexOf('abcb', 'B', 0, 'ordinalIgnoreCase')).toBe(-1);
    expect(lastIndexOf('abcb', 'BC', 2, 'ordinalIgnoreCase')).toBe(1);
    expect(lastIndexOf('abcb', 'BC', 1, 'ordinalIgnoreCase')).toBe(-1);
    expect(lastIndexOf('', '', 'ordinalIgnoreCase')).toBe(0);
    expect(lastIndexOf('abc', '', 1, 'ordinalIgnoreCase')).toBe(2);
    expect(lastIndexOf('abc', 'b', 3, 'ordinalIgnoreCase')).toBe(1);
    expect(lastIndexOf('', 'a', 'ordinalIgnoreCase')).toBe(-1);
    expect(lastIndexOf('', 'a', -1, 'ordinalIgnoreCase')).toBe(-1);
    expect(lastIndexOf('', 'a', 0, 'ordinalIgnoreCase')).toBe(-1);
  });

  it("throws .NET's words for a range outside the string", () => {
    const lessOrEqual =
      "Index was out of range. Must be non-negative and less than or equal to the size of the collection. (Parameter 'startIndex')";
    const less =
      "Index was out of range. Must be non-negative and less than the size of the collection. (Parameter 'startIndex')";
    const count =
      "Count must be positive and count must refer to a location within the string/array/collection. (Parameter 'count')";
    expect(() => indexOf('abc', 'b', 4, 'ordinalIgnoreCase')).toThrow(lessOrEqual);
    expect(() => indexOf('abc', 'b', -1, 'ordinalIgnoreCase')).toThrow(lessOrEqual);
    expect(() => indexOf('abcb', 'B', 2, 5, 'ordinalIgnoreCase')).toThrow(count);
    expect(() => indexOf('abc', 'b', 1, -1, 'ordinalIgnoreCase')).toThrow(count);
    expect(() => lastIndexOf('abc', 'b', 4, 'ordinalIgnoreCase')).toThrow(less);
    expect(() => lastIndexOf('abc', 'b', -1, 'ordinalIgnoreCase')).toThrow(less);
    expect(() => lastIndexOf('abc', 'b', 2, 4, 'ordinalIgnoreCase')).toThrow(count);
  });
});

describe("a char's search with a start and a count, as .NET 10's String.Searching.cs", () => {
  const atMost =
    "Index was out of range. Must be non-negative and less than or equal to the size of the collection. (Parameter 'startIndex')";
  const below =
    "Index was out of range. Must be non-negative and less than the size of the collection. (Parameter 'startIndex')";
  const count =
    "Count must be positive and count must refer to a location within the string/array/collection. (Parameter 'count')";

  it('searches forward inside the count, from a start that may be the end', () => {
    expect(indexOfChar('abcabc', 'c', 0, 2)).toBe(-1);
    expect(indexOfChar('abcabc', 'c', 1)).toBe(2);
    expect(indexOfChar('abcabc', 'c', 3)).toBe(5);
    expect(indexOfChar('abcabc', 'c', 6)).toBe(-1);
    expect(indexOfChar('abcabc', 'c', 6, 0)).toBe(-1);
    expect(indexOfChar('', 'c', 0)).toBe(-1);
    expect(indexOfChar('x\ud83d\ude00', '\ud83d', 0)).toBe(1);
  });

  it('searches back count chars from a start that stands on one', () => {
    expect(lastIndexOfChar('abcabc', 'a', 5, 2)).toBe(-1);
    expect(lastIndexOfChar('abcabc', 'a', 5)).toBe(3);
    expect(lastIndexOfChar('abcabc', 'a', 5, 6)).toBe(3);
    expect(lastIndexOfChar('abcabc', 'a', 2)).toBe(0);
    expect(lastIndexOfChar('abcabc', 'a', 5, 0)).toBe(-1);
  });

  it('answers -1 for an empty string, whatever the start and the count', () => {
    expect(lastIndexOfChar('', 'c', 0)).toBe(-1);
    expect(lastIndexOfChar('', 'c', 5)).toBe(-1);
    expect(lastIndexOfChar('', 'c', -1)).toBe(-1);
    expect(lastIndexOfChar('', 'c', 0, 3)).toBe(-1);
  });

  it("throws .NET's words, the start before the count", () => {
    expect(() => indexOfChar('abcabc', 'c', 7)).toThrow(atMost);
    expect(() => indexOfChar('abcabc', 'c', -1)).toThrow(atMost);
    expect(() => indexOfChar('abcabc', 'c', 7, 0)).toThrow(atMost);
    expect(() => indexOfChar('', 'c', 1)).toThrow(atMost);
    expect(() => indexOfChar('abcabc', 'c', 2, 5)).toThrow(count);
    expect(() => indexOfChar('abcabc', 'c', 2, -1)).toThrow(count);
    expect(() => lastIndexOfChar('abcabc', 'a', 6)).toThrow(below);
    expect(() => lastIndexOfChar('abcabc', 'a', -1)).toThrow(below);
    expect(() => lastIndexOfChar('abcabc', 'a', 5, 7)).toThrow(count);
    expect(() => lastIndexOfChar('abcabc', 'a', 5, -1)).toThrow(count);
  });

  it('refuses a null receiver as .NET does before the method runs', () => {
    expect(() => indexOfChar(null, 'c', 0)).toThrow('Object reference not set to an instance of an object.');
    expect(() => lastIndexOfChar(undefined, 'c', 0)).toThrow(
      'Object reference not set to an instance of an object.',
    );
  });
});

describe('the arguments, in the order .NET checks them', () => {
  it('names a null value before the comparison, where .NET does', () => {
    expect(() => startsWith('abc', null, 'nope')).toThrow(
      "Value cannot be null. (Parameter 'value')",
    );
    expect(() => indexOf('abc', null, 'nope')).toThrow("Value cannot be null. (Parameter 'value')");
    expect(() => lastIndexOf('abc', null, 'nope')).toThrow(
      "Value cannot be null. (Parameter 'value')",
    );
    expect(() => startsWith('abc', '', 'nope')).toThrow(notSupported);
    expect(() => indexOf('abc', 'b', 'nope')).toThrow(notSupported);
    expect(() => lastIndexOf('abc', 'b', 'nope')).toThrow(notSupported);
    expect(() => instanceEquals('abc', null, 'nope')).toThrow(notSupported);
  });

  it('checks the comparison before the old value of a replacement', () => {
    expect(() => replace('abc', null, 'x', 'nope')).toThrow(notSupported);
    expect(() => replace('abc', null, 'y', 'ordinalIgnoreCase')).toThrow(
      "Value cannot be null. (Parameter 'oldValue')",
    );
    expect(() => replace('abc', '', 'y', 'ordinal')).toThrow(
      "The value cannot be an empty string. (Parameter 'oldValue')",
    );
  });

  it('writes the replacement as text, never as a pattern', () => {
    expect(replace('abc', 'b', '$&$&', 'ordinal')).toBe('a$&$&c');
    expect(replace('abc', 'b', null, 'ordinal')).toBe('ac');
    expect(replace('aaa', 'aa', 'b', 'ordinal')).toBe('ba');
  });

  it('refuses a call on no instance', () => {
    expect(() => indexOf(null, 'a', 'ordinal')).toThrow(
      'Object reference not set to an instance of an object.',
    );
    expect(() => instanceEquals(null, 'a', 'ordinal')).toThrow(
      'Object reference not set to an instance of an object.',
    );
    expect(() => compareTo(undefined, 'a')).toThrow(
      'Object reference not set to an instance of an object.',
    );
  });
});

describe('a culture comparison', () => {
  it('answers what .NET answers before it consults the collation', () => {
    expect(startsWith('ab', '', 'currentCulture')).toBe(true);
    expect(startsWith('ab', 'ab', 'invariantCulture')).toBe(true);
    expect(endsWith('ab', '', 'currentCultureIgnoreCase')).toBe(true);
    expect(indexOf('ab', '', 1, 'invariantCulture')).toBe(1);
    expect(lastIndexOf('ab', '', 'currentCulture')).toBe(2);
  });

  it('throws where a search would need it', () => {
    expect(() => startsWith('\u00adab', 'ab', 'invariantCulture')).toThrow(
      /StartsWith by StringComparison\.InvariantCulture has no search in the browser/,
    );
    expect(() => indexOf('a\r\nb', '\n', 'currentCulture')).toThrow(
      /IndexOf by StringComparison\.CurrentCulture/,
    );
    expect(() => replace('aBa', 'b', 'x', 'invariantCultureIgnoreCase')).toThrow(/Replace by/);
  });

  it("compares whole strings by the statics' rule", () => {
    expect(instanceEquals('aB', 'ab', 'ordinalIgnoreCase')).toBe(true);
    expect(instanceEquals(kelvin, 'k', 'ordinalIgnoreCase')).toBe(false);
    expect(instanceEquals('abc', null, 'ordinalIgnoreCase')).toBe(false);
    expect(compareTo('a', 'B')).toBe(-1);
    expect(compareTo('B', 'a')).toBe(1);
    expect(compareTo('a', null)).toBe(1);
    expect(['b', 'B', 'a', 'A'].sort(compareTo).join(',')).toBe('a,A,b,B');
  });
});
