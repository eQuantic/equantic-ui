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
