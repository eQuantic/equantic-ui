import { describe, expect, it } from 'vitest';
import { binarySearchIn, introSort, type SortOrder } from './sort';
import { comparerOf } from './ordering';

// Every answer below was measured on .NET 10, by the List<T>.Sort or BinarySearch its test names.
const byNumber: SortOrder<number> = { compare: comparerOf('value'), kind: 'comparable', name: '' };
const comparison = <T>(compare: (a: T, b: T) => number): SortOrder<T> => ({
  compare,
  kind: 'comparer',
  name: 'System.Comparison`1[System.Int32]',
});

describe(".NET's introspective sort (#488)", () => {
  it('sorts numbers by value, where sort() compared their text', () => {
    const items = [10, 9, 1];
    introSort(items, 0, items.length, byNumber);
    expect(items).toEqual([1, 9, 10]);
  });

  it('leaves equal elements where .NET\'s unstable sort leaves them', () => {
    const twenty = Array.from({ length: 20 }, (_, i) => ({ x: i % 3, y: i }));
    introSort(twenty, 0, twenty.length, comparison((a, b) => a.x - b.x));
    expect(twenty.map((p) => p.y).join(' ')).toBe('0 15 12 18 6 9 3 4 7 10 13 1 16 19 8 11 2 14 17 5');
    const five = Array.from({ length: 5 }, (_, i) => ({ x: i % 2, y: i }));
    introSort(five, 0, five.length, comparison((a, b) => a.x - b.x));
    expect(five.map((p) => p.y).join(' ')).toBe('0 2 4 1 3');
  });

  it("moves a double's NaNs to the front first, which decides where -0 lands beside 0", () => {
    const items = [0, -0, NaN, 1, -0, 0];
    introSort(items, 0, items.length, { compare: comparerOf('real'), kind: 'real', name: '' });
    expect(items.map((value) => (Object.is(value, -0) ? '-0' : String(value)))).toEqual(['NaN', '-0', '0', '-0', '0', '1']);
  });

  it('sorts a range and nothing outside it', () => {
    const items = [5, 4, 3, 2, 1];
    introSort(items, 1, 3, byNumber);
    expect(items).toEqual([5, 2, 3, 4, 1]);
  });

  it('wraps what a comparison throws in an InvalidOperationException, the thrown one its cause', () => {
    const boom = new Error('boom');
    let caught: unknown;
    try {
      introSort([3, 1, 2], 0, 3, comparison(() => {
        throw boom;
      }));
    } catch (error) {
      caught = error;
    }
    expect((caught as Error).message).toBe('Failed to compare two elements in the array.');
    expect((caught as Error & { cause?: unknown }).cause).toBe(boom);
  });

  it('reports a comparison that sends the partition past its span as .NET reports it', () => {
    const items = Array.from({ length: 20 }, (_, i) => i);
    expect(() => introSort(items, 0, items.length, comparison(() => -1))).toThrow(
      "Unable to sort because the IComparer.Compare() method returns inconsistent results. Either a value does not compare equal to itself, or one value repeatedly compared to another value yields different results. IComparer: 'System.Comparison`1[System.Int32]'.",
    );
    const seventeen = Array.from({ length: 17 }, (_, i) => i);
    introSort(seventeen, 0, seventeen.length, comparison(() => 1));
    expect(seventeen).toEqual([16, 14, 13, 12, 11, 10, 9, 15, 8, 6, 5, 4, 3, 2, 1, 7, 0]);
  });

  it("refuses to compare the values of a type .NET's default comparer cannot order", () => {
    const order: SortOrder<object> = { compare: null, kind: 'comparer', name: '' };
    introSort([{}], 0, 1, order);
    expect(() => introSort([{}, {}], 0, 2, order)).toThrow('Failed to compare two elements in the array.');
  });

  it('runs its heap sort on an input that exhausts the depth, comparison for comparison as .NET', () => {
    // McIlroy's adversary, played against this sort, built the input; the trace is .NET's.
    const killer = [
      0, 59, 5, 58, 7, 57, 9, 56, 11, 55, 13, 54, 15, 53, 17, 52, 19, 51, 21, 50, 23, 49, 25, 48, 27, 47, 28, 29, 63, 61, 62, 3,
      2, 4, 6, 8, 10, 12, 14, 16, 18, 20, 22, 24, 26, 46, 45, 44, 43, 42, 41, 40, 39, 38, 37, 36, 35, 34, 33, 32, 31, 30, 60, 1,
    ];
    let calls = 0;
    let trace = 0;
    const items = killer.slice();
    introSort(items, 0, items.length, comparison((x: number, y: number) => {
      calls++;
      trace = (trace * 31 + x * 64 + y) % 1000003;
      return x - y;
    }));
    expect(`${calls}:${trace}`).toBe('984:470213');
    expect(items).toEqual(Array.from({ length: 64 }, (_, i) => i));
  });
});

describe(".NET's binary search", () => {
  it('answers the complement of the insertion point for a value it does not find', () => {
    expect(binarySearchIn([1, 3, 5], 0, 3, 3, byNumber)).toBe(1);
    expect(binarySearchIn([1, 3, 5], 0, 3, 4, byNumber)).toBe(-3);
    expect(binarySearchIn([1, 3, 5], 0, 3, 0, byNumber)).toBe(-1);
    expect(binarySearchIn([1, 3, 5], 0, 3, 9, byNumber)).toBe(-4);
  });

  it('finds, among equal elements, the one its midpoint meets first', () => {
    expect(binarySearchIn([1, 2, 2, 2, 2, 2, 3], 0, 7, 2, byNumber)).toBe(3);
    expect(binarySearchIn([1, 3, 5, 7, 9], 1, 3, 1, byNumber)).toBe(-2);
  });
});
