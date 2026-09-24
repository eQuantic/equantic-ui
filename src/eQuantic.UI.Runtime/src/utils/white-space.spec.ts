import { describe, expect, it } from 'vitest';
import { isNullOrWhiteSpace, isWhiteSpace, splitOnWhiteSpace, trim, trimEnd, trimStart } from './white-space';

/** .NET's white space, the list `char.IsWhiteSpace` answers true for (see white-space.ts). */
const DOTNET = [
  0x09, 0x0a, 0x0b, 0x0c, 0x0d, 0x20, 0x85, 0xa0, 0x1680, 0x2000, 0x2001, 0x2002, 0x2003, 0x2004,
  0x2005, 0x2006, 0x2007, 0x2008, 0x2009, 0x200a, 0x2028, 0x2029, 0x202f, 0x205f, 0x3000,
];

describe('white space is .NET\'s', () => {
  it('classifies every code unit as the list says', () => {
    const white: number[] = [];
    for (let unit = 0; unit < 0x10000; unit++) if (isWhiteSpace(String.fromCharCode(unit))) white.push(unit);
    expect(white).toEqual(DOTNET);
  });

  it('counts NEXT LINE and not the byte order mark, where JavaScript does the opposite', () => {
    expect(isWhiteSpace('\u0085'), 'NEXT LINE').toBe(true);
    expect(isWhiteSpace('\ufeff'), 'the byte order mark').toBe(false);
    expect(isWhiteSpace('\ud835\udc00'), 'a surrogate pair is never white').toBe(false);
  });

  it('trims what it counts, and only that', () => {
    const text = '\u0085 a \ufeff';
    expect(trimStart(text)).toBe('a \ufeff');
    expect(trimEnd(text)).toBe(text);
    expect(trim(text)).toBe('a \ufeff');
    expect(trim(' \t\u3000x\u2028 ')).toBe('x');
  });

  it('splits on every white character, keeping the empty entries between two', () => {
    expect(splitOnWhiteSpace('a  b\u0085c')).toEqual(['a', '', 'b', 'c']);
    expect(splitOnWhiteSpace('a\ufeffb'), 'the byte order mark is no separator').toEqual(['a\ufeffb']);
    expect(splitOnWhiteSpace('')).toEqual(['']);
  });

  it('reads null, empty and white as white', () => {
    expect(isNullOrWhiteSpace(null)).toBe(true);
    expect(isNullOrWhiteSpace(undefined)).toBe(true);
    expect(isNullOrWhiteSpace('')).toBe(true);
    expect(isNullOrWhiteSpace('\u0085 ')).toBe(true);
    expect(isNullOrWhiteSpace('\ufeff')).toBe(false);
  });

  it('trims a long line of white space in linear time', () => {
    const text = ' '.repeat(200_000) + 'x' + ' '.repeat(200_000);
    const started = performance.now();
    expect(trim(text)).toBe('x');
    expect(performance.now() - started, 'a pattern anchored at the end would backtrack for seconds').toBeLessThan(500);
  });
});
