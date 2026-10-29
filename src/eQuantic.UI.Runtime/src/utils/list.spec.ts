import { describe, expect, it } from 'vitest';
import {
  addRange,
  arrayFind,
  arrayIndexOf,
  arrayLastIndexOf,
  binarySearch,
  comparerOrder,
  copyRangeTo,
  copyTo,
  find,
  findIndex,
  findLast,
  findLastIndex,
  indexOf,
  insertRange,
  lastIndexOf,
  listCapacity,
  listCopy,
  listSort,
  listSortBy,
  order,
  removeAll,
  stringOrder,
} from './list';
import { dec } from './decimal';
import { hashSetOf } from './hash-set';
import { linkedList } from './collections';
import { tupleEquality } from './key-equality';

// Every answer and every message below was measured on .NET 10 (#488, #425).
const fails = (run: () => unknown): string => {
  try {
    run();
  } catch (error) {
    return (error as Error).message;
  }
  return 'no';
};

describe("List<T>'s and Array's searches, by the element type's equality (#425)", () => {
  it('finds a NaN, a decimal by value, and a tuple by its elements', () => {
    expect(indexOf([NaN], NaN)).toBe(0);
    expect(lastIndexOf([NaN, 1, NaN], NaN)).toBe(2);
    expect(arrayIndexOf([NaN], NaN)).toBe(0);
    expect(indexOf([dec('1.0')], dec('1.00'), true)).toBe(0);
    expect(indexOf([[1, 2]], [1, 2], true)).toBe(0);
    expect(lastIndexOf([[1, 2], [1, 2]], [1, 2], true)).toBe(1);
  });

  it("compares a tuple's array element by reference", () => {
    const a = [1];
    const same = tupleEquality(false, false);
    expect(indexOf([[a, 1]], [a, 1], same)).toBe(0);
    expect(indexOf([[a, 1]], [[1], 1], same)).toBe(-1);
  });

  it('asks an element that does not decide for its own equality', () => {
    class Q {
      constructor(readonly v: number) {}
      equals(other: unknown): boolean {
        return other instanceof Q && other.v === this.v;
      }
    }
    class C {}
    expect(indexOf([new Q(1)], new Q(1), 'own')).toBe(0);
    expect(indexOf([new C()], new C(), 'own')).toBe(-1);
  });

  it('searches a range, refusing one that leaves the list in .NET\'s words', () => {
    const list = [5, 6, 5];
    expect(indexOf(list, 5, false, 1)).toBe(2);
    expect(indexOf(list, 5, false, 3)).toBe(-1);
    expect(indexOf(list, 5, false, 1, 1)).toBe(-1);
    expect(lastIndexOf(list, 5, false, 1)).toBe(0);
    expect(lastIndexOf(list, 5, false, 1, 2)).toBe(0);
    expect(lastIndexOf([], 5, false, -1, 0)).toBe(-1);
    expect(fails(() => indexOf(list, 5, false, 4))).toBe(
      "Index was out of range. Must be non-negative and less than or equal to the size of the collection. (Parameter 'index')",
    );
    expect(fails(() => indexOf(list, 5, false, -1))).toBe(
      "Index was out of range. Must be non-negative and less than or equal to the size of the collection. (Parameter 'startIndex')",
    );
    expect(fails(() => indexOf(list, 5, false, 1, 3))).toBe(
      "Count must be positive and count must refer to a location within the string/array/collection. (Parameter 'count')",
    );
    expect(fails(() => lastIndexOf(list, 5, false, 3))).toBe(
      "Index was out of range. Must be non-negative and less than the size of the collection. (Parameter 'index')",
    );
    expect(fails(() => lastIndexOf(list, 5, false, 2, 4))).toBe("Larger than collection size. (Parameter 'count')");
    expect(fails(() => lastIndexOf([1], 5, false, -1, 1))).toBe("Non-negative number required. (Parameter 'index')");
    expect(arrayIndexOf([1, 2, 1], 1, false, 1)).toBe(2);
    expect(arrayLastIndexOf([1, 2, 1], 1, false, 1)).toBe(0);
    expect(fails(() => arrayIndexOf(null, 1))).toBe("Value cannot be null. (Parameter 'array')");
  });
});

describe("List<T>'s finds (#488)", () => {
  it("answers the element type's default for no match", () => {
    expect(find([1, 2], (x) => x > 5, 0)).toBe(0);
    expect(findLast([1, 2], (x) => x > 5, 0)).toBe(0);
    expect(find([1, 2, 3, 2], (x) => x > 1, 0)).toBe(2);
    expect(findLast([1, 2, 3, 2], (x) => x < 3, 0)).toBe(2);
    expect(arrayFind([1], (x) => x > 5, 0)).toBe(0);
    expect(fails(() => find([1], null, 0))).toBe("Value cannot be null. (Parameter 'match')");
    expect(fails(() => arrayFind(null, (x) => x > 5, 0))).toBe("Value cannot be null. (Parameter 'array')");
  });

  it('takes a range, which findIndex took for its predicate', () => {
    expect(findIndex([5, 6, 7], (x) => x > 4, 1)).toBe(1);
    expect(findIndex([5, 6, 7], () => true, 3)).toBe(-1);
    expect(findIndex([5, 6, 7], (x) => x === 7, 1, 2)).toBe(2);
    expect(findLastIndex([5, 6, 7], (x) => x > 4, 1)).toBe(1);
    expect(findLastIndex([5, 6, 7], (x) => x === 5, 2, 2)).toBe(-1);
    expect(findLastIndex([], () => true, -1)).toBe(-1);
    expect(fails(() => findIndex([5, 6, 7], () => true, 4))).toBe(
      "Index was out of range. Must be non-negative and less than or equal to the size of the collection. (Parameter 'startIndex')",
    );
    expect(fails(() => findIndex([5, 6, 7], (x) => x === 7, 1, 3))).toBe(
      "Count must be positive and count must refer to a location within the string/array/collection. (Parameter 'count')",
    );
    expect(fails(() => findLastIndex([5, 6, 7], () => true, 3))).toBe(
      "Index was out of range. Must be non-negative and less than the size of the collection. (Parameter 'startIndex')",
    );
    expect(fails(() => findLastIndex([], () => true, 0))).toBe(
      "Index was out of range. Must be non-negative and less than the size of the collection. (Parameter 'startIndex')",
    );
  });
});

describe("List<T>'s AddRange and InsertRange, of a range of any size (#1060)", () => {
  // A million: past the cap V8 (near 125,000) and JavaScriptCore (near 640,000) set on a call's arguments,
  // which `push(...items)` and `splice(index, 0, ...items)` passed.
  const million = Array.from({ length: 1_000_000 }, (_, i) => i);

  it('adds and inserts a million elements, in their order', () => {
    const added = [-1];
    addRange(added, million);
    expect([added.length, added[0], added[1], added[1_000_000]]).toEqual([1_000_001, -1, 0, 999_999]);
    const inserted = [-1, -2];
    insertRange(inserted, 1, million);
    expect([inserted.length, inserted[0], inserted[1], inserted[1_000_000], inserted[1_000_001]]).toEqual([
      1_000_002, -1, 0, 999_999, -2,
    ]);
  });

  it('takes what a list held before the call when it is the range, and any collection in its order', () => {
    const self = [1, 2];
    addRange(self, self);
    expect(self).toEqual([1, 2, 1, 2]);
    const into = [1, 2, 3];
    insertRange(into, 1, into);
    expect(into).toEqual([1, 1, 2, 3, 2, 3]);
    const text: string[] = [];
    addRange(text, 'a\uD83D\uDE00');
    expect(text).toEqual(['a', '\uD83D', '\uDE00']);
    const fromASet = [0];
    insertRange(fromASet, 0, hashSetOf([7, 8]));
    expect(fromASet).toEqual([7, 8, 0]);
  });

  it('refuses a null collection before the index, and an index outside 0 to the count', () => {
    const range = 'Index was out of range. Must be non-negative and less than or equal to the size of the collection.';
    expect(fails(() => addRange([], null))).toBe("Value cannot be null. (Parameter 'collection')");
    expect(fails(() => insertRange([], 5, null))).toBe("Value cannot be null. (Parameter 'collection')");
    expect(fails(() => insertRange([1], 2, [9]))).toBe(`${range} (Parameter 'index')`);
    expect(fails(() => insertRange([1], -1, [9]))).toBe(`${range} (Parameter 'index')`);
    const end = [1];
    insertRange(end, 1, [9]);
    expect(end).toEqual([1, 9]);
  });

  it('refuses a null list first, and an index before the collection is read', () => {
    const throughNull = 'Object reference not set to an instance of an object.';
    expect(fails(() => addRange(null as unknown as number[], []))).toBe(throughNull);
    expect(fails(() => insertRange(null as unknown as number[], -1, null))).toBe(throughNull);
    let read = false;
    const asked: Iterable<number> = {
      *[Symbol.iterator]() {
        read = true;
        yield 1;
      },
    };
    expect(fails(() => insertRange([], 5, asked))).toContain("(Parameter 'index')");
    expect(read).toBe(false);
  });
});

describe("List<T>'s RemoveAll and CopyTo (#488)", () => {
  it('removes in one pass, asking the predicate once per element, and answers how many', () => {
    const list = [1, 2, 3, 4];
    expect(removeAll(list, (x) => x % 2 === 0)).toBe(2);
    expect(list).toEqual([1, 3]);
    const seen: number[] = [];
    const asked = [1, 2, 3];
    removeAll(asked, (x) => {
      seen.push(x);
      return x === 2;
    });
    expect(seen).toEqual([1, 2, 3]);
    expect(asked).toEqual([1, 3]);
    expect(removeAll([1, 2], (x) => x > 5)).toBe(0);
  });

  it('writes into the array it is handed, checked as Array.Copy checks it', () => {
    const a = [0, 0, 0];
    copyTo([1, 2], a);
    expect(a).toEqual([1, 2, 0]);
    const b = [0, 0, 0];
    copyTo([1, 2], b, 1);
    expect(b).toEqual([0, 1, 2]);
    const c = [0, 0, 0];
    copyRangeTo([1, 2], 1, c, 0, 1);
    expect(c).toEqual([2, 0, 0]);
    expect(fails(() => copyTo([1, 2], [0]))).toBe(
      "Destination array was not long enough. Check the destination index, length, and the array's lower bounds. (Parameter 'destinationArray')",
    );
    expect(fails(() => copyTo([1, 2], [0, 0, 0], -1))).toBe(
      "destinationIndex ('-1') must be greater than or equal to '0'. (Parameter 'destinationIndex')\nActual value was -1.",
    );
    expect(fails(() => copyRangeTo([1, 2], 1, [0, 0, 0], 0, 2))).toBe(
      'Offset and length were out of bounds for the array or count is greater than the number of elements from index to the end of the source collection.',
    );
    expect(fails(() => copyRangeTo([1, 2], -1, [0, 0, 0], 0, 1))).toBe(
      "sourceIndex ('-1') must be greater than or equal to '0'. (Parameter 'sourceIndex')\nActual value was -1.",
    );
    expect(fails(() => copyRangeTo([1, 2], 0, [0, 0, 0], 0, -1))).toBe(
      "length ('-1') must be a non-negative value. (Parameter 'length')\nActual value was -1.",
    );
    expect(fails(() => copyTo([1, 2], null))).toBe("Value cannot be null. (Parameter 'destinationArray')");
  });

  it('copies a collection an ICollection<T> holds when the call runs: a set by its own CopyTo, any other in its order', () => {
    const a = [0, 0, 0];
    copyTo(hashSetOf([1, 2]), a, 1);
    expect(a).toEqual([0, 1, 2]);
    const b = [0, 0, 0];
    copyTo(linkedList([3, 4]), b);
    expect(b).toEqual([3, 4, 0]);
  });
});

describe("List<T>'s Sort and BinarySearch, by the comparer the compiler names (#488)", () => {
  it('sorts by the type, a StringComparer and an IComparer of the app', () => {
    const numbers = [10, 9, 1];
    listSort(numbers, order('value', 'comparable'));
    expect(numbers).toEqual([1, 9, 10]);
    const ordinal = ['b', 'a', 'B', 'A'];
    listSort(ordinal, stringOrder('ordinal'));
    expect(ordinal).toEqual(['A', 'B', 'a', 'b']);
    const ignoringCase = ['b', 'a', 'B', 'A'];
    listSort(ignoringCase, stringOrder('ordinalIgnoreCase'));
    expect(ignoringCase).toEqual(['a', 'A', 'b', 'B']);
    const descending = [3, 1, 2];
    listSort(descending, comparerOrder({ compare: (a: number, b: number) => b - a }, order('value', 'comparable'), 'App.Descending'));
    expect(descending).toEqual([3, 2, 1]);
    const fallback = [3, 1, 2];
    listSort(fallback, comparerOrder<number>(null, order('value', 'comparable'), 'App.Descending'));
    expect(fallback).toEqual([1, 2, 3]);
  });

  it('checks a range and a comparison as .NET checks them', () => {
    const list = [5, 4, 3, 2, 1];
    listSort(list, order('value', 'comparable'), 1, 3);
    expect(list).toEqual([5, 2, 3, 4, 1]);
    expect(fails(() => listSort([5, 4, 3, 2, 1], order('value', 'comparable'), 3, 3))).toBe(
      'Offset and length were out of bounds for the array or count is greater than the number of elements from index to the end of the source collection.',
    );
    expect(fails(() => listSort([5], order('value', 'comparable'), -1, 3))).toBe("Non-negative number required. (Parameter 'index')");
    expect(fails(() => listSortBy([5, 4], null, ''))).toBe("Value cannot be null. (Parameter 'comparison')");
  });

  it('searches by the comparer, a range checked', () => {
    expect(binarySearch([1, 3, 5], 4, order('value', 'comparable'))).toBe(-3);
    expect(binarySearch([1, 3, 5, 7, 9], 9, order('value', 'comparable'), 1, 3)).toBe(-5);
    expect(binarySearch(['a', 'B', 'c'], 'b', order('text', 'comparable'))).toBe(-2);
    expect(fails(() => binarySearch([1, 3], 5, order('value', 'comparable'), 0, -1))).toBe(
      "Non-negative number required. (Parameter 'count')",
    );
  });

  // `new List<T>(capacity)` hands its capacity here before the list's elements: one the constructor
  // takes passes, a negative one is refused in .NET's words.
  it("refuses a negative capacity as List<T>'s constructor does", () => {
    expect(() => listCapacity(0)).not.toThrow();
    expect(() => listCapacity(16)).not.toThrow();
    expect(fails(() => listCapacity(-1))).toBe("Non-negative number required. (Parameter 'capacity')");
  });
});

describe('new List<T>(collection), as .NET copies and refuses (#726)', () => {
  it('copies what the collection enumerates, a string by its UTF-16 chars', () => {
    const source = [1, 2];
    const copy = listCopy(source);
    expect(copy).toEqual([1, 2]);
    expect(copy).not.toBe(source);
    expect(listCopy(hashSetOf([3, 4]))).toEqual([3, 4]);
    expect(listCopy('a\u{1F600}b')).toHaveLength(4);
  });

  it('refuses a null collection by its name', () => {
    expect(() => listCopy(null)).toThrow("Value cannot be null. (Parameter 'collection')");
  });
});
