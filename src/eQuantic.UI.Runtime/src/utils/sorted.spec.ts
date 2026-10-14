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
  // System.Collections' words, where the two sorted dictionaries live, and not CoreLib's (#864).
  const modified = 'Collection was modified after the enumerator was instantiated.';
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

  it('ends a walk of its keys or values as it ends one of its pairs', () => {
    const keys = two();
    expect(() => {
      for (const key of keys.keys()) keys.set(key + 10, 0);
    }).toThrow(modified);
    const values = two();
    expect(() => {
      for (const _ of values.values()) values.delete(1);
    }).toThrow(modified);
  });

  it('bumps a sorted dictionary on a missing key removed and a key added twice, and a sorted list on neither', () => {
    const missed = two();
    expect(() => {
      for (const _ of missed) missed.delete(42);
    }).toThrow(modified);
    const twice = two();
    expect(() => {
      for (const [key] of twice) {
        try {
          twice.add(key, 0);
        } catch {
          // refused, after the tree turned
        }
      }
    }).toThrow(modified);
    const list = sortedList<number, number>([
      [1, 1],
      [2, 2],
    ]);
    const walked: number[] = [];
    for (const [key] of list) {
      list.delete(42);
      try {
        list.add(key, 0);
      } catch {
        // refused before anything moved
      }
      walked.push(key);
    }
    expect(walked).toEqual([1, 2]);
  });
});

describe('SortedMap — its keys and values are live views (#864)', () => {
  const map = () =>
    sortedDictionary<number, string>([
      [3, 'c'],
      [1, 'a'],
    ]);

  it("reads the collection when read, as .NET's KeyCollection and ValueCollection do", () => {
    const m = map();
    const keys = m.keys();
    const values = m.values();
    m.set(2, 'b');
    expect([...keys]).toEqual([1, 2, 3]);
    expect(values.length).toBe(3);
    expect(values[1]).toBe('b');
    expect(m.keys()).toBe(keys);
    expect((keys as unknown as { contains(k: number): boolean }).contains(2)).toBe(true);
    expect((keys as unknown as { isReadOnly: boolean }).isReadOnly).toBe(true);
    expect(JSON.stringify(values)).toBe('["a","b","c"]');
  });

  it("refuses a change in each collection's words", () => {
    const dictionaryWords = 'Mutating a key collection derived from a dictionary is not allowed.';
    expect(() => map().keys().push(4)).toThrow(dictionaryWords);
    expect(() => {
      map().values().length = 0;
    }).toThrow('Mutating a value collection derived from a dictionary is not allowed.');
    const nested =
      'This operation is not supported on SortedList nested types because they require modifying the original SortedList.';
    const list = () =>
      sortedList<number, string>([
        [1, 'a'],
        [2, 'b'],
      ]);
    expect(() => list().keys().push(3)).toThrow(nested);
    expect(() => {
      list().keys()[0] = 9;
    }).toThrow(dictionaryWords);
    expect(() => {
      list().values()[0] = 'z';
    }).toThrow(nested);
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

describe('SortedSet — a change while it is walked (#765)', () => {
  const modified = 'Collection was modified after the enumerator was instantiated.';
  const set = () => sortedSet([1, 3, 5]);

  /** What a walk saw before it ended, and what it ended with; a walk that runs on is cut at 20. */
  function walk(source: Iterable<number>, step: (value: number) => void): string {
    let seen = '';
    try {
      for (const value of source) {
        seen += String(value);
        if (seen.length > 20) return `${seen}: ran on`;
        step(value);
      }
      return seen;
    } catch (error) {
      return `${seen}|${(error as Error).message}`;
    }
  }

  it('ends the walk at any Add or Remove, one that changes nothing included', () => {
    const added = set();
    expect(walk(added, (v) => added.add(v + 10))).toBe(`1|${modified}`);
    const again = set();
    expect(walk(again, (v) => again.add(v))).toBe(`1|${modified}`);
    const missed = set();
    expect(walk(missed, () => missed.remove(9))).toBe(`1|${modified}`);
    const cleared = set();
    expect(walk(cleared, (v) => v === 5 && cleared.clear())).toBe(`135|${modified}`);
  });

  it('counts a set operation as .NET counts it: a sequence asked with Contains, a sorted set not', () => {
    const united = set();
    expect(walk(united, (v) => united.unionWith([v]))).toBe('135');
    const excepted = set();
    expect(walk(excepted, () => excepted.exceptWith([2]))).toBe('135');
    const exceptedBySet = set();
    expect(walk(exceptedBySet, (v) => v === 1 && exceptedBySet.exceptWith(sortedSet([2])))).toBe(
      `1|${modified}`,
    );
    const intersected = set();
    expect(
      walk(intersected, (v) => v === 1 && intersected.intersectWith(sortedSet([1, 3, 5]))),
    ).toBe(`1|${modified}`);
    const itself = set();
    expect(walk(itself, () => itself.intersectWith(itself))).toBe('135');
    const unmatched = set();
    expect(walk(unmatched, () => unmatched.removeWhere((v) => v > 9))).toBe('135');
  });

  it('walks backwards with Reverse, when walked and each time, and ends as the forward walk ends', () => {
    const s = set();
    const backwards = s.reverse();
    s.add(7);
    expect([...backwards]).toEqual([7, 5, 3, 1]);
    expect([...backwards]).toEqual([7, 5, 3, 1]);
    expect(walk(s.reverse(), (v) => s.add(v + 10))).toBe(`7|${modified}`);
  });
});

describe('SortedSet — TryGetValue (#866)', () => {
  it('answers the element the set holds, or undefined', () => {
    const s = sortedSet([3, 1]);
    expect([s.tryGetValue(3), s.tryGetValue(2)]).toEqual([3, undefined]);
  });
});

describe('SortedSet — GetViewBetween (#865)', () => {
  const view = () => {
    const set = sortedSet([1, 3, 5, 7]);
    return { set, view: set.getViewBetween(2, 6) };
  };
  const outOfRange = (parameter: string) =>
    `Specified argument was out of the range of valid values. (Parameter '${parameter}')`;

  it('holds the range of its set, live both ways', () => {
    const { set, view: v } = view();
    expect([...v]).toEqual([3, 5]);
    expect([v.count, v.min, v.max]).toEqual([2, 3, 5]);
    set.add(4);
    set.add(9);
    expect([...v]).toEqual([3, 4, 5]);
    expect(v.add(6)).toBe(true);
    expect(v.remove(3)).toBe(true);
    expect(set.toArray()).toEqual([1, 4, 5, 6, 7, 9]);
    expect([v.contains(7), v.contains(5), v.tryGetValue(7), v.tryGetValue(5)]).toEqual([
      false,
      true,
      undefined,
      5,
    ]);
    v.clear();
    expect(set.toArray()).toEqual([1, 7, 9]);
    expect(JSON.stringify(v)).toBe('[]');
  });

  it('refuses an element outside its range before it changes anything, and its bounds as .NET does', () => {
    const { set, view: v } = view();
    expect(() => v.add(9)).toThrow(outOfRange('item'));
    expect(v.remove(9)).toBe(false);
    expect(set.toArray()).toEqual([1, 3, 5, 7]);
    expect(() => set.getViewBetween(6, 2)).toThrow(
      "Must be less than or equal to upperValue. (Parameter 'lowerValue')",
    );
    expect(() => v.getViewBetween(1, 5)).toThrow(outOfRange('lowerValue'));
    expect(() => v.getViewBetween(3, 9)).toThrow(outOfRange('upperValue'));
    expect([...v.getViewBetween(3, 4)]).toEqual([3]);
  });

  it('ends a walk at any change to its set, and a walk of its set at a change made through it', () => {
    const { set, view: v } = view();
    expect(() => {
      for (const _ of v) set.add(100);
    }).toThrow('Collection was modified after the enumerator was instantiated.');
    const other = view();
    const walked: number[] = [];
    for (const value of other.set) {
      walked.push(value);
      other.view.remove(9);
    }
    expect(walked).toEqual([1, 3, 5, 7]);
  });

  it("removes nothing through ExceptWith until its count is read, as .NET's stale count does", () => {
    const never = view();
    never.view.exceptWith([3]);
    expect(never.set.toArray()).toEqual([1, 3, 5, 7]);
    const counted = view();
    expect(counted.view.count).toBe(2);
    counted.view.exceptWith([3]);
    expect(counted.set.toArray()).toEqual([1, 5, 7]);
  });

  it("asks a set it is compared with for a view of that set's range", () => {
    const { set, view: v } = view();
    expect([v.isSubsetOf(set), set.isSupersetOf(v), set.isProperSupersetOf(v)]).toEqual([
      true,
      true,
      true,
    ]);
    expect(() => v.isSupersetOf(sortedSet([1, 9]))).toThrow(outOfRange('lowerValue'));
  });
});
