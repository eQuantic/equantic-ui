import { describe, it, expect } from 'vitest';
import { StringBuilder, stringBuilder } from './string-builder';

describe('StringBuilder — .NET-compat', () => {
  it('appends strings fluently', () => {
    expect(stringBuilder().append('Hello').append(' ').append('World').toString()).toBe(
      'Hello World',
    );
  });

  it('seeds from an initial string', () => {
    expect(stringBuilder('a').append('b').append('c').toString()).toBe('abc');
  });

  it('ignores a numeric capacity argument', () => {
    expect(stringBuilder(64).append('x').toString()).toBe('x');
  });

  it('appends numbers via their string form', () => {
    expect(stringBuilder().append(1).append(2).append(3).toString()).toBe('123');
  });

  it('appends booleans as .NET does (True/False)', () => {
    expect(stringBuilder().append(true).append(false).toString()).toBe('TrueFalse');
  });

  it('appendLine uses \\n', () => {
    expect(stringBuilder().appendLine('line1').append('line2').toString()).toBe('line1\nline2');
    expect(stringBuilder().appendLine().toString()).toBe('\n');
  });

  it('inserts at an index', () => {
    expect(stringBuilder('hello').insert(0, '>>').toString()).toBe('>>hello');
    expect(stringBuilder('hello').insert(2, 'XX').toString()).toBe('heXXllo');
  });

  it('removes a range', () => {
    expect(stringBuilder('hello').remove(0, 2).toString()).toBe('llo');
  });

  it('replaces every occurrence', () => {
    expect(stringBuilder('a-b-c').replace('-', '+').toString()).toBe('a+b+c');
  });

  it('clears', () => {
    expect(stringBuilder('hello').clear().append('x').toString()).toBe('x');
  });

  it('reports Length', () => {
    expect(stringBuilder('hello').length).toBe(5);
    expect(stringBuilder().append('ab').append('cd').length).toBe(4);
  });

  it('is an instance of StringBuilder', () => {
    expect(stringBuilder()).toBeInstanceOf(StringBuilder);
  });
});

/** Each overload shape .NET has, and its refusals in .NET's words and order (#650). */
describe('StringBuilder — counted and ranged overloads', () => {
  const refusal = (act: () => unknown): string => {
    try {
      act();
      return 'no throw';
    } catch (e) {
      return (e as Error).message;
    }
  };

  it('repeats a char, and takes a range of a string or a builder', () => {
    expect(stringBuilder('12').append('x', 3).toString()).toBe('12xxx');
    expect(stringBuilder('12').append('hello', 1, 3).toString()).toBe('12ell');
    expect(stringBuilder('12').append(stringBuilder('hello'), 1, 3).toString()).toBe('12ell');
    expect(stringBuilder('12').append('abc', 10, 0).toString()).toBe('12');
  });

  it('writes a char array as its characters, whole or a range of it', () => {
    expect(stringBuilder('12').appendChars(['a', 'b']).toString()).toBe('12ab');
    expect(stringBuilder('12').appendChars(['a', 'b', 'c'], 1, 2).toString()).toBe('12bc');
    expect(stringBuilder('12').insertChars(1, ['x', 'y', 'z'], 1, 2).toString()).toBe('1yz2');
  });

  it('appends and inserts nothing for a null', () => {
    expect(stringBuilder('12').append(null).appendChars(null).insert(1, null).toString()).toBe('12');
    expect(stringBuilder('12').append(null, 0, 0).appendChars(null, 0, 0).toString()).toBe('12');
  });

  it('inserts a string count times', () => {
    expect(stringBuilder('12').insert(1, 'ab', 2).toString()).toBe('1abab2');
  });

  it('replaces inside a range only, and a null new value removes', () => {
    expect(stringBuilder('aaaa').replace('a', 'b', 0, 2).toString()).toBe('bbaa');
    expect(stringBuilder('aaaa').replace('aa', 'b', 1, 3).toString()).toBe('aba');
    expect(stringBuilder('aba').replace('a', null).toString()).toBe('b');
  });

  it('reads a range of its text', () => {
    expect(stringBuilder('12').toString(1, 1)).toBe('2');
    expect(stringBuilder('12').toString(2, 0)).toBe('');
  });

  it('refuses as .NET refuses', () => {
    const tooFar =
      'Index was out of range. Must be non-negative and less than or equal to the size of the collection.';
    expect(refusal(() => stringBuilder().append('x', -1))).toBe(
      "repeatCount ('-1') must be a non-negative value. (Parameter 'repeatCount')\nActual value was -1.",
    );
    expect(refusal(() => stringBuilder().append('abc', 2, 5))).toBe(`${tooFar} (Parameter 'startIndex')`);
    expect(refusal(() => stringBuilder().appendChars(['a'], 5, 0))).toBe(`${tooFar} (Parameter 'charCount')`);
    expect(refusal(() => stringBuilder().appendChars(null, 0, -1))).toBe(
      "charCount ('-1') must be a non-negative value. (Parameter 'charCount')\nActual value was -1.",
    );
    expect(refusal(() => stringBuilder().append(null, 0, 1))).toBe("Value cannot be null. (Parameter 'value')");
    expect(refusal(() => stringBuilder('12').insert(5, 'x'))).toBe(`${tooFar} (Parameter 'index')`);
    expect(refusal(() => stringBuilder('12').insertChars(1, null, -1, 0))).toBe(
      "Value cannot be null. (Parameter 'value')",
    );
    expect(refusal(() => stringBuilder('12').insertChars(1, ['x'], 0, 2))).toBe(
      `${tooFar} (Parameter 'startIndex')`,
    );
    expect(refusal(() => stringBuilder('aaaa').replace('', 'x'))).toBe(
      "The value cannot be an empty string. (Parameter 'oldValue')",
    );
    expect(refusal(() => stringBuilder('aaaa').replace('a', 'b', 2, 5))).toBe(`${tooFar} (Parameter 'count')`);
    expect(refusal(() => stringBuilder('12').remove(-1, -1))).toBe(
      "length ('-1') must be a non-negative value. (Parameter 'length')\nActual value was -1.",
    );
    expect(refusal(() => stringBuilder('12').toString(1, 5))).toBe(
      "Index and length must refer to a location within the string. (Parameter 'length')",
    );
    expect(refusal(() => stringBuilder('12').toString(3, 0))).toBe(
      "startIndex cannot be larger than length of string. (Parameter 'startIndex')",
    );
  });
});

/** The members a page reaches, as .NET 10 answers them (#679). */
describe('StringBuilder — capacity, indexer, length, equality and copy', () => {
  const x = (n: number) => 'x'.repeat(n);

  it("follows .NET's chunks through every edit", () => {
    expect([stringBuilder().capacity, stringBuilder(100).capacity, stringBuilder(x(20)).capacity]).toEqual([16, 100, 20]);
    expect([stringBuilder('x', 5).capacity, stringBuilder(0).capacity, stringBuilder(0, 8).capacity]).toEqual([5, 16, 8]);
    const grown = stringBuilder();
    const seen = new Set<number>();
    for (let i = 0; i < 100; i++) seen.add(grown.append('x').capacity);
    expect([...seen]).toEqual([16, 32, 64, 128]);
    expect(stringBuilder().append(x(40)).capacity).toBe(40);
    const cleared = stringBuilder();
    for (let i = 0; i < 40; i++) cleared.append('x');
    expect(cleared.clear().capacity).toBe(48);
    expect(stringBuilder().append(x(20)).remove(0, 5).capacity).toBe(27);
    expect(stringBuilder('12').insert(1, x(20)).capacity).toBe(36);
    expect(stringBuilder('ab').replace('b', 'cccc').capacity).toBe(19);
    const set = stringBuilder('12');
    set.capacity = 100;
    expect([set.capacity, set.ensureCapacity(5), set.ensureCapacity(200)]).toEqual([100, 100, 200]);
    expect([stringBuilder(4, 8).maxCapacity, stringBuilder().maxCapacity]).toEqual([8, 2147483647]);
  });

  it('reads and writes a char, cuts and fills its length, and compares and copies its text', () => {
    const b = stringBuilder('12');
    b.setItem(0, 'x');
    expect(b.item(0) + b.toString()).toBe('xx2');
    b.length = 4;
    expect(b.toString()).toBe('x2\0\0');
    b.length = 1;
    expect(b.toString()).toBe('x');
    expect(stringBuilder('12').equalsBuilder(stringBuilder('12', 100))).toBe(true);
    expect(stringBuilder('12').equalsBuilder(null)).toBe(false);
    const same = stringBuilder('12');
    expect([same.equals(same), same.equals(stringBuilder('12'))]).toEqual([true, false]);
    const copy = ['-', '-', '-', '-'];
    stringBuilder('12').copyTo(0, copy, 1, 2);
    expect(copy.join('')).toBe('-12-');
  });

  it('refuses as .NET refuses', () => {
    const refusal = (act: () => unknown): string => {
      try {
        act();
        return 'no throw';
      } catch (e) {
        return (e as Error).message;
      }
    };
    expect(refusal(() => stringBuilder('12').item(2))).toBe('Index was outside the bounds of the array.');
    expect(refusal(() => stringBuilder('12').setItem(2, 'x'))).toBe(
      "Index was out of range. Must be non-negative and less than the size of the collection. (Parameter 'index')",
    );
    expect(refusal(() => stringBuilder(9, 8))).toBe("Capacity exceeds maximum capacity. (Parameter 'capacity')");
    expect(refusal(() => stringBuilder(4, 8).append('123456789'))).toBe(
      "The length cannot be greater than the capacity. (Parameter 'valueCount')",
    );
    expect(refusal(() => stringBuilder(0, 0))).toBe(
      "maxCapacity ('0') must be a non-negative and non-zero value. (Parameter 'maxCapacity')\nActual value was 0.",
    );
    expect(refusal(() => stringBuilder('12').copyTo(0, ['-'], 0, 2))).toBe(
      'Either offset did not refer to a position in the string, or there is an insufficient length of destination character array.',
    );
  });
});

