import { describe, expect, it } from 'vitest';
import { HashSet, hashSet, hashSetOf } from './hash-set';
import { dec } from './decimal';
import { dateTime } from './datetime';
import { tupleEquality } from './key-equality';

// Every order below was measured on .NET 10: a set enumerates by slot, an insertion takes the slot a
// removal freed last, and a copy keeps the source's free slots while its capacity allows (#438).
const items = <T>(set: HashSet<T>): T[] => [...set];
const of = (...values: number[]): HashSet<number> => hashSetOf(values);

describe('HashSet — elements by slot, as .NET holds them (#438)', () => {
  it('gives a new element the slot a removal freed', () => {
    const s = of(3, 1);
    s.delete(3);
    s.add(5);
    expect(items(s)).toEqual([5, 1]);
  });

  it('reuses the slot freed last first', () => {
    const s = of(1, 2, 3, 4);
    s.delete(2);
    s.delete(4);
    s.add(5).add(6).add(7);
    expect(items(s)).toEqual([1, 6, 3, 5, 7]);
  });

  it('copies a set with its free slots while the copy needs no larger arrays', () => {
    const a = of(3, 1);
    a.delete(3);
    const b = hashSet(false, a);
    b.add(5).add(6);
    expect(items(b)).toEqual([5, 1, 6]);
  });

  it('packs a copy of a set much larger than it holds, and a copy of anything else', () => {
    const a = hashSet<number>();
    for (let i = 0; i < 20; i++) a.add(i);
    for (let i = 0; i < 18; i++) a.delete(i);
    const b = hashSet(false, a);
    b.add(100);
    expect(items(b)).toEqual([18, 19, 100]);
    const fromArray = hashSet(false, [...of(1)]);
    fromArray.add(5);
    expect(items(fromArray)).toEqual([1, 5]);
  });

  it('frees and takes slots as each of .NET set operations does', () => {
    const where = of(1, 2, 3, 4, 5);
    expect(where.removeWhere((x) => x % 2 === 1)).toBe(3);
    where.add(10).add(11).add(12).add(13);
    expect(items(where)).toEqual([12, 2, 11, 4, 10, 13]);

    const except = of(1, 2, 3, 4, 5);
    except.exceptWith([4, 2]);
    except.add(10).add(11).add(12);
    expect(items(except)).toEqual([1, 10, 3, 11, 5, 12]);

    const intersect = of(1, 2, 3, 4, 5);
    intersect.intersectWith([5, 3]);
    intersect.add(10).add(11).add(12);
    expect(items(intersect)).toEqual([12, 11, 3, 10, 5]);

    const symmetric = of(1, 2, 3, 4, 5);
    symmetric.symmetricExceptWith([6, 4, 2, 7, 2]);
    symmetric.add(10).add(11);
    expect(items(symmetric)).toEqual([1, 11, 3, 10, 5, 6, 7]);

    const symmetricSet = of(1, 2, 3, 4, 5);
    symmetricSet.symmetricExceptWith(of(6, 4, 2, 7));
    symmetricSet.add(10).add(11);
    expect(items(symmetricSet)).toEqual([1, 7, 3, 10, 5, 6, 11]);

    const union = of(1, 2, 3);
    union.delete(2);
    union.unionWith([9, 1, 8]);
    expect(items(union)).toEqual([1, 9, 3, 8]);
  });

  it('clears to the first slot, and trims only past the capacity it needs', () => {
    const cleared = of(1, 2, 3);
    cleared.delete(2);
    cleared.clear();
    cleared.add(7).add(8);
    expect(items(cleared)).toEqual([7, 8]);

    const small = of(1, 2, 3);
    small.delete(1);
    small.trimExcess();
    small.add(7);
    expect(items(small)).toEqual([7, 2, 3]);

    const large = hashSet<number>();
    for (let i = 0; i < 10; i++) large.add(i);
    for (let i = 0; i < 8; i++) large.delete(i);
    large.trimExcess();
    large.add(7);
    expect(items(large)).toEqual([8, 9, 7]);
  });

  it('answers its capacity as .NET grows it', () => {
    const s = hashSet<number>();
    expect(s.ensureCapacity(5)).toBe(7);
    expect(s.ensureCapacity(2)).toBe(7);
    expect(hashSet(false, 10).ensureCapacity(0)).toBe(11);
    expect(() => hashSet(false, -1)).toThrow("Specified argument was out of the range of valid values. (Parameter 'capacity')");
  });

  it('ends a walk an addition changed, and keeps one a removal changed going', () => {
    const adding = of(1, 2);
    expect(() => {
      for (const x of adding) adding.add(x + 10);
    }).toThrow('Collection was modified; enumeration operation may not execute.');
    const removing = of(1, 2, 3);
    const seen: number[] = [];
    for (const x of removing) {
      seen.push(x);
      removing.delete(2);
    }
    expect(seen).toEqual([1, 3]);
    const again = of(1, 2);
    for (const _ of again) again.add(1);
    expect(items(again)).toEqual([1, 2]);
  });
});

describe('HashSet — elements found as the default comparer finds them (#531)', () => {
  it('finds a date, a decimal and a tuple by value where its equality says so', () => {
    expect(hashSetOf([dateTime(2026, 1, 1)], true).has(dateTime(2026, 1, 1))).toBe(true);
    expect(hashSetOf([dec('1.0')], true).has(dec('1.00'))).toBe(true);
    const decimals = hashSetOf([dec('1.0'), dec('1.00')], true);
    expect(decimals.size).toBe(1);
    expect(String([...decimals][0])).toBe('1.0');
    expect(hashSetOf([[1, 2]], true).has([1, 2])).toBe(true);
    expect(hashSetOf([[1, 2]]).has([1, 2])).toBe(false);
  });

  it("asks an element of a type that does not decide for its own equality", () => {
    expect(hashSetOf([dec('1.0')], 'own').has(dec('1.00'))).toBe(true);
    expect(hashSetOf([1, 'a'], 'own').has('a')).toBe(true);
  });

  it("compares a tuple's array element by reference, through the generated comparison", () => {
    const a = [1];
    const s = hashSetOf([[a, 1]], tupleEquality(false, false));
    expect(s.has([a, 1])).toBe(true);
    expect(s.has([[1], 1])).toBe(false);
  });

  it('holds a NaN once, and -0 as the 0 already there', () => {
    expect(hashSetOf([NaN, NaN]).size).toBe(1);
    const zero = hashSetOf([0, -0]);
    expect(zero.size).toBe(1);
    expect(Object.is([...zero][0], 0)).toBe(true);
    expect(Object.is([...hashSetOf([-0, 0])][0], -0)).toBe(true);
  });

  it('holds a null as an element, undefined being the same null', () => {
    const s = hashSetOf<string | null | undefined>([null, 'a', undefined]);
    expect(s.size).toBe(2);
    expect(s.has(undefined)).toBe(true);
    expect(s.delete(null)).toBe(true);
    expect(s.size).toBe(1);
  });

  it('answers whether an element was new', () => {
    const s = hashSet<number>();
    expect(s.tryAdd(1)).toBe(true);
    expect(s.tryAdd(1)).toBe(false);
    expect(s.size).toBe(1);
  });
});

describe("HashSet — .NET's own members", () => {
  it('answers the set predicates', () => {
    const s = of(1, 2, 3);
    expect(s.isSubsetOf([1, 2, 3, 4])).toBe(true);
    expect(s.isProperSubsetOf([1, 2, 3])).toBe(false);
    expect(s.isSupersetOf([1])).toBe(true);
    expect(s.isProperSupersetOf(of(1, 2, 3))).toBe(false);
    expect(s.overlaps([9, 3])).toBe(true);
    expect(s.setEquals([3, 2, 1, 1])).toBe(true);
    expect(s.setEquals([3, 2])).toBe(false);
    expect(hashSet<number>().isProperSubsetOf([1])).toBe(true);
  });

  it('copies into an array, refusing one too short', () => {
    const a = new Array<number>(5).fill(0);
    of(1, 2, 3).copyTo(a, 1);
    of(1, 2, 3).copyTo(a, 3, 1);
    expect(a).toEqual([0, 1, 2, 1, 0]);
    expect(() => of(1, 2, 3).copyTo([0, 0])).toThrow(
      'Destination array is not long enough to copy all the items in the collection. Check array index and length.',
    );
    expect(() => of(1).copyTo([0], -1)).toThrow("arrayIndex ('-1') must be a non-negative value.");
  });

  it('refuses a null collection by name', () => {
    expect(() => hashSet(false, null)).toThrow("Value cannot be null. (Parameter 'collection')");
    expect(() => of(1).unionWith(null as unknown as number[])).toThrow("Value cannot be null. (Parameter 'other')");
  });

  it("is a Set to whatever reads one, and JSON's array", () => {
    const s = of(2, 1);
    expect([...s.keys()]).toEqual([2, 1]);
    expect([...s.entries()]).toEqual([
      [2, 2],
      [1, 1],
    ]);
    const visited: number[] = [];
    s.forEach((value) => visited.push(value));
    expect(visited).toEqual([2, 1]);
    expect(JSON.stringify(s)).toBe('[2,1]');
    expect(Object.prototype.toString.call(s)).toBe('[object HashSet]');
  });

  it('equals only itself', () => {
    const s = of(1);
    expect(s.equals(s)).toBe(true);
    expect(s.equals(of(1))).toBe(false);
  });

  it('reads a string by its UTF-16 code units, as IEnumerable<char> does', () => {
    expect(items(hashSet(false, 'a\u{1F600}a'))).toEqual(['a', '\ud83d', '\ude00']);
  });
});
