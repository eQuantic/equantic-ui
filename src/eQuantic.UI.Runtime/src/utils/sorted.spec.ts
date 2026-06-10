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
  });
});
