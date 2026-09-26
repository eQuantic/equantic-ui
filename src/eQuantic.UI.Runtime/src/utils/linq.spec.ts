import { describe, it, expect } from 'vitest';
import { max, min, toDictionary } from './linq';
import { Dictionary } from './dictionary';

// Every answer below was measured on .NET 10; the conformance suite runs the same calls on both sides.
describe('max and min (LINQ Max/Min)', () => {
  it('throws for an empty sequence of a value type, and answers null for one that takes a null', () => {
    expect(() => max([], undefined, 'value', false)).toThrow('Sequence contains no elements');
    expect(max([], undefined, 'value', true)).toBe(null);
    expect(max([null, null], undefined, 'value', true)).toBe(null);
    expect(min([null, 2, 5], undefined, 'value', true)).toBe(2);
  });

  it("follows .NET's rules for NaN", () => {
    expect(max([1, NaN, 3], undefined, 'real', false)).toBe(3);
    expect(max([NaN, NaN], undefined, 'real', false)).toBeNaN();
    expect(min([1, NaN, 3], undefined, 'real', false)).toBeNaN();
    let read = 0;
    min([1, NaN, 3], (x: number) => (read++, x), 'real', false);
    expect(read).toBe(2); // Min stops at the NaN
  });

  it('keeps the first of two equal values', () => {
    expect(Object.is(max([-0, 0], undefined, 'real', false), -0)).toBe(true);
    expect(Object.is(min([0, -0], undefined, 'real', false), 0)).toBe(true);
  });

  it('orders text in the culture, longs as BigInts, and the rest by compareTo', () => {
    expect(max(['a', 'B'], undefined, 'text', true)).toBe('B');
    expect(min(['a', 'B'], undefined, 'text', true)).toBe('a');
    expect(max([1n, 5n], undefined, 'value', false)).toBe(5n);
    const at = (n: number) => ({ n, compareTo: (o: { n: number }) => n - o.n });
    expect(max([at(1), at(3), at(2)], undefined, 'comparable', false).n).toBe(3);
  });
});

describe('toDictionary (LINQ ToDictionary)', () => {
  it('maps each element into the dictionary class, in the order of its source', () => {
    const d = toDictionary(
      [3, 1, 2],
      (x) => x,
      (x) => x * 10,
    );
    expect(d).toBeInstanceOf(Dictionary);
    expect(d.keys()).toEqual([3, 1, 2]);
    expect(d.values()).toEqual([30, 10, 20]);
  });

  it('holds "__proto__" as an entry of its own', () => {
    const d = toDictionary(['__proto__', 'a'], (x) => x);
    expect(d.keys()).toEqual(['__proto__', 'a']);
    expect(() => toDictionary(['__proto__', '__proto__'], (x) => x)).toThrow(
      'An item with the same key has already been added. Key: __proto__',
    );
  });

  it('keys a structural value by its value when asked to', () => {
    const point = (x: number, y: number) => ({ x, y });
    const d = toDictionary(
      [point(1, 2), point(3, 4)],
      (p) => p,
      (p) => p.x,
      true,
    );
    expect(d.size).toBe(2);
    expect(d.get(point(3, 4))).toBe(3);
    expect(() => toDictionary([point(1, 2), point(1, 2)], (p) => p, null, true)).toThrow(
      'An item with the same key has already been added.',
    );
  });

  it('writes a bool key in its message as .NET does', () => {
    expect(() => toDictionary([true, true], (x) => x)).toThrow(
      'An item with the same key has already been added. Key: True',
    );
  });

  it("refuses a key twice and a null key, with .NET's words", () => {
    expect(() => toDictionary([1, 2, 1], (x) => x)).toThrow(
      'An item with the same key has already been added. Key: 1',
    );
    expect(() => toDictionary(['a', null], (x) => x)).toThrow(
      "Value cannot be null. (Parameter 'key')",
    );
  });
});
