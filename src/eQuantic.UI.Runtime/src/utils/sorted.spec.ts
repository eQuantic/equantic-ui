import { describe, it, expect } from 'vitest';
import { SortedSet, sortedSet, SortedMap, sortedDictionary, sortedList } from './sorted';

describe('SortedSet<T> — sorted, unique', () => {
  it('enumerates in sorted order regardless of insertion', () => {
    const s = sortedSet<number>();
    s.add(3);
    s.add(1);
    s.add(2);
    expect(s.toArray()).toEqual([1, 2, 3]);
  });

  it('add dedups and reports whether it was new', () => {
    const s = new SortedSet<number>([5, 5, 1]);
    expect(s.count).toBe(2);
    expect(s.add(5)).toBe(false);
    expect(s.add(9)).toBe(true);
  });

  it('min / max', () => {
    const s = new SortedSet<number>([4, 2, 8, 1]);
    expect(s.min).toBe(1);
    expect(s.max).toBe(8);
  });

  it('contains / remove', () => {
    const s = new SortedSet<number>([1, 2, 3]);
    expect(s.contains(2)).toBe(true);
    expect(s.remove(2)).toBe(true);
    expect(s.remove(2)).toBe(false);
    expect(s.toArray()).toEqual([1, 3]);
  });

  it('orders strings ordinally', () => {
    expect(new SortedSet<string>(['banana', 'apple', 'cherry']).toArray()).toEqual([
      'apple',
      'banana',
      'cherry',
    ]);
  });
});

describe('SortedMap<K, V> — key-sorted dictionary (SortedDictionary / SortedList)', () => {
  it('keys/values enumerate in key order', () => {
    const m = sortedDictionary<number, string>();
    m.set(3, 'c');
    m.set(1, 'a');
    m.set(2, 'b');
    expect(m.keys()).toEqual([1, 2, 3]);
    expect(m.values()).toEqual(['a', 'b', 'c']);
  });

  it('indexer get/set, has, delete, size', () => {
    const m = new SortedMap<number, number>();
    m.set(2, 20);
    m.set(1, 10);
    expect(m.get(1)).toBe(10);
    expect(m.has(2)).toBe(true);
    expect(m.size).toBe(2);
    expect(m.delete(1)).toBe(true);
    expect(m.has(1)).toBe(false);
  });

  it('set overwrites an existing key without reordering', () => {
    const m = new SortedMap<number, number>([
      [1, 1],
      [2, 2],
    ]);
    m.set(1, 100);
    expect(m.size).toBe(2);
    expect(m.get(1)).toBe(100);
    expect(m.keys()).toEqual([1, 2]);
  });

  it('iterates KeyValuePair-shaped entries in key order', () => {
    const m = sortedList<number, number>();
    m.set(2, 20);
    m.set(1, 10);
    expect([...m].map((kvp) => kvp.key * 100 + kvp.value)).toEqual([110, 220]);
    expect([...m].map(([key, value]) => key * 100 + value)).toEqual([110, 220]);
  });

  it('refuses a null key in every member, as .NET\'s sorted dictionaries do', () => {
    const m = sortedDictionary<string | null, number>([['a', 1]]);
    const refusal = "Value cannot be null. (Parameter 'key')";
    expect(() => m.has(null)).toThrow(refusal);
    expect(() => m.set(null, 1)).toThrow(refusal);
    expect(() => m.delete(null)).toThrow(refusal);
    expect(m.size).toBe(1);
  });

  it('writes its pairs in key order, and equals only itself', () => {
    const m = sortedDictionary<string, number>([
      ['b', 2],
      ['a', 1],
    ]);
    expect(JSON.stringify(m)).toBe('[["a",1],["b",2]]');
    // A JSON object would have listed "9" before "10", ahead of the order the keys sort in (#437).
    const digits = sortedDictionary<string, number>([
      ['b', 3],
      ['9', 1],
      ['10', 2],
    ]);
    expect(JSON.stringify(digits)).toBe('[["10",2],["9",1],["b",3]]');
    expect(m.equals(m)).toBe(true);
    expect(m.equals(sortedDictionary<string, number>([['a', 1], ['b', 2]]))).toBe(false);
  });
});

describe('SortedMap — a change while its pairs are walked', () => {
  const modified = 'Collection was modified; enumeration operation may not execute.';
  const two = () =>
    sortedDictionary<number, number>([
      [1, 1],
      [2, 2],
    ]);

  it('ends the walk any change breaks into, as a sorted dictionary\'s enumerator does', () => {
    const added = two();
    let steps = 0;
    expect(() => {
      for (const [key] of added) {
        added.set(key + 10, 0);
        if (++steps > 50) break;
      }
    }).toThrow(modified);
    const overwritten = two();
    expect(() => {
      for (const [key] of overwritten) overwritten.set(key, 5);
    }).toThrow(modified);
    const removed = two();
    expect(() => {
      for (const [key] of removed) removed.delete(key);
    }).toThrow(modified);
    const cleared = two();
    expect(() => {
      for (const _ of cleared) cleared.clear();
    }).toThrow(modified);
  });
});

describe('SortedSet — the set operations, questions and CopyTo .NET has (#723)', () => {
  /** A decimal-like value: equal by its amount, told apart by its text, as `1.0m` and `1.00m` are. */
  const amount = (value: number, text: string) => ({ value, text });
  type Amount = ReturnType<typeof amount>;
  const byAmount = (a: Amount, b: Amount) => a.value - b.value;
  const texts = (set: SortedSet<Amount>) => set.toArray().map((value) => value.text);

  it('copies in order, from an index, at most a count, and refuses as .NET does', () => {
    const s = sortedSet([3, 1, 2]);
    const into = [0, 0, 0, 0];
    s.copyTo(into, 1, 2);
    expect(into).toEqual([0, 1, 2, 0]);
    expect(() => s.copyTo(null)).toThrow("(Parameter 'array')");
    expect(() => s.copyTo([0, 0, 0, 0], -1)).toThrow("(Parameter 'index')");
    expect(() => s.copyTo([0, 0, 0, 0], 0, -1)).toThrow("(Parameter 'count')");
    expect(() => s.copyTo([0, 0, 0, 0], 2)).toThrow('Destination array is not long enough');
  });

  it('keeps the other set’s instance where two sets of one comparer merge, its own where it adds', () => {
    const merged = new SortedSet([amount(1, '1.0'), amount(2, '2')], byAmount);
    merged.unionWith(new SortedSet([amount(1, '1.00'), amount(3, '3')], byAmount));
    expect(texts(merged)).toEqual(['1.00', '2', '3']);

    const added = new SortedSet([amount(1, '1.0'), amount(2, '2')], byAmount);
    added.unionWith([amount(1, '1.00'), amount(3, '3')]);
    expect(texts(added)).toEqual(['1.0', '2', '3']);

    const intersected = new SortedSet([amount(1, '1.0'), amount(2, '2')], byAmount);
    intersected.intersectWith([amount(1, '1.00'), amount(2, '2.0')]);
    expect(texts(intersected)).toEqual(['1.00', '2.0']);
  });

  it('toggles each distinct element of the other, the first of equal ones', () => {
    const s = sortedSet([1, 2, 3]);
    s.symmetricExceptWith([4, 2, 4, 2, 5]);
    expect(s.toArray()).toEqual([1, 3, 4, 5]);
    const t = new SortedSet([amount(1, '1.0')], byAmount);
    t.symmetricExceptWith([amount(2, '2.0'), amount(2, '2.00')]);
    expect(texts(t)).toEqual(['1.0', '2.0']);
  });

  it('answers the questions over repeated elements, the empty set and itself', () => {
    const s = sortedSet([1, 2]);
    expect([s.isSubsetOf([2, 1, 1]), s.isProperSubsetOf([2, 1, 1]), s.isProperSupersetOf([1, 1])]).toEqual([true, false, true]);
    expect([s.overlaps([5, 2]), s.setEquals([2, 1, 2]), s.setEquals([2, 1, 3])]).toEqual([true, true, false]);
    const empty = sortedSet<number>();
    expect([empty.isProperSubsetOf([]), empty.isProperSubsetOf([1]), empty.isProperSupersetOf([])]).toEqual([false, true, false]);
    expect([s.isProperSubsetOf(s), s.isSupersetOf(s), s.setEquals(s)]).toEqual([false, true, true]);
  });

  it('removes what the predicate matched only after asking of every element', () => {
    const s = sortedSet([3, 1, 2]);
    expect(() =>
      s.removeWhere((x) => {
        if (x === 3) throw new Error('x');
        return x === 1;
      }),
    ).toThrow('x');
    expect(s.toArray()).toEqual([1, 2, 3]);
    expect(s.removeWhere((x) => x % 2 === 1)).toBe(2);
    expect(s.toArray()).toEqual([2]);
  });

  it('refuses a null argument by its name, an empty set too', () => {
    const empty = sortedSet<number>();
    expect(() => empty.unionWith(null as never)).toThrow("(Parameter 'other')");
    expect(() => empty.isSubsetOf(null as never)).toThrow("(Parameter 'other')");
    expect(() => empty.removeWhere(null)).toThrow("(Parameter 'match')");
  });
});

describe('the sorted factories refuse a null source by the name the compiler hands them (#726)', () => {
  it('refuses a named null, and reads an unnamed one as no source', () => {
    expect(() => sortedSet(null, 'value', 'collection')).toThrow("Value cannot be null. (Parameter 'collection')");
    expect(() => sortedDictionary(null, 'text', 'dictionary')).toThrow("(Parameter 'dictionary')");
    expect(() => sortedList(undefined, undefined, 'dictionary')).toThrow("(Parameter 'dictionary')");
    expect(sortedSet(null, 'value').count).toBe(0);
    expect(sortedSet('ba', undefined, 'collection').toArray()).toEqual(['a', 'b']);
  });
});
