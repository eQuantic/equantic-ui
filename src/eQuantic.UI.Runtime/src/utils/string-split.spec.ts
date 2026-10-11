import { describe, expect, it } from 'vitest';
import { split, splitAny } from './string-split';

// Every answer below was measured on .NET 10; the conformance suite runs the same calls on both sides.
const ALL = 2147483647;
const REMOVE = 1;
const TRIM = 2;
const BOTH = REMOVE | TRIM;
const text = ' abcd ef ';

describe('Split by one separator, as .NET splits', () => {
  it('splits on a char or a string, and keeps the empty entries', () => {
    expect(split(text, 'd')).toEqual([' abc', ' ef ']);
    expect(split(text, 'cd')).toEqual([' ab', ' ef ']);
    expect(split('', ',')).toEqual(['']);
  });

  it('splits nothing on a null or an empty separator', () => {
    expect(split(text, null)).toEqual([text]);
    expect(split(text, '')).toEqual([text]);
    expect(split(' a ', '', undefined, TRIM)).toEqual(['a']);
    expect(split('', '', undefined, REMOVE)).toEqual([]);
  });

  it('stops at its count, the last entry taking the rest', () => {
    expect(split(text, 'd', 2)).toEqual([' abc', ' ef ']);
    expect(split('acdbcdc', 'cd', 2)).toEqual(['a', 'bcdc']);
    expect(split('a', ',', 0)).toEqual([]);
    expect(split(text, ' ', 1)).toEqual([text]);
  });

  it('trims each entry and drops the empty ones as the options say', () => {
    expect(split(text, ' ', undefined, REMOVE)).toEqual(['abcd', 'ef']);
    expect(split(text, 'd', undefined, TRIM)).toEqual(['abc', 'ef']);
    expect(split(text, ' ', 2, REMOVE)).toEqual(['abcd', 'ef ']);
    expect(split('  a  b  c  ', ' ', 3, REMOVE)).toEqual(['a', 'b', 'c  ']);
    expect(split(' a , b , c ', ',', 2, TRIM)).toEqual(['a', 'b , c']);
    expect(split(' , ,a,b', ',', 2, BOTH)).toEqual(['a', 'b']);
    expect(split('', ',', undefined, REMOVE)).toEqual([]);
    expect(split(' x ', ',', 1, TRIM)).toEqual(['x']);
  });

  it('refuses a negative count before an invalid option, and a null receiver first', () => {
    expect(() => split(text, ',', -1, 4)).toThrow(
      "count ('-1') must be a non-negative value. (Parameter 'count')\nActual value was -1.",
    );
    expect(() => split(text, ',', 2, 4)).toThrow(
      "Value of flags is invalid. (Parameter 'options')",
    );
    expect(() => split(null, ',', -1)).toThrow(
      'Object reference not set to an instance of an object.',
    );
  });
});

describe('Split by a set of separators, as .NET splits', () => {
  it('splits on any of them', () => {
    expect(splitAny(text, ['a', 'a'])).toEqual([' ', 'bcd ef ']);
    expect(splitAny(text, ['a', 'd'])).toEqual([' ', 'bc', ' ef ']);
    expect(splitAny(text, ['cd', 'b'])).toEqual([' a', '', ' ef ']);
  });

  it('takes the first separator in the array that matches at a place', () => {
    expect(splitAny('abcd', ['b', 'bc'])).toEqual(['a', 'cd']);
    expect(splitAny('abcd', ['bc', 'b'])).toEqual(['a', 'd']);
  });

  it('skips a null or an empty separator, and splits on white space where there are none', () => {
    expect(splitAny(text, [null, ''])).toEqual([text]);
    expect(splitAny(text, [null, 'c'])).toEqual([' ab', 'd ef ']);
    expect(splitAny(text, null)).toEqual(['', 'abcd', 'ef', '']);
    expect(splitAny(text, [])).toEqual(['', 'abcd', 'ef', '']);
    expect(splitAny(text)).toEqual(['', 'abcd', 'ef', '']);
    expect(splitAny(text, null, 2)).toEqual(['', 'abcd ef ']);
  });

  it("splits on .NET's white space, which JavaScript's is not", () => {
    expect(splitAny('a  b\u0085c d')).toEqual(['a', '', 'b', 'c', 'd']);
    expect(splitAny('a﻿b'), 'the byte order mark is no separator').toEqual(['a﻿b']);
    expect(splitAny('')).toEqual(['']);
  });

  it('stops at its count and applies the options', () => {
    expect(splitAny(text, ['a', 'd'], 2)).toEqual([' ', 'bcd ef ']);
    expect(splitAny(text, ['a', 'd'], 0)).toEqual([]);
    expect(splitAny(text, ['a', 'd'], 1)).toEqual([text]);
    expect(splitAny(text, [' '], ALL, REMOVE)).toEqual(['abcd', 'ef']);
    expect(splitAny(text, null, undefined, BOTH)).toEqual(['abcd', 'ef']);
  });

  it('refuses a negative count and an invalid option', () => {
    expect(() => splitAny(text, ['a'], -1)).toThrow("count ('-1') must be a non-negative value.");
    expect(() => splitAny(text, ['a'], 2, 8)).toThrow(
      "Value of flags is invalid. (Parameter 'options')",
    );
    expect(() => splitAny(text, null, 2, 4)).toThrow('Value of flags is invalid.');
  });
});
