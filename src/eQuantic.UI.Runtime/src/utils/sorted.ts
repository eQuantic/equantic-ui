/**
 * .NET-compat sorted collections — `SortedSet<T>`, `SortedDictionary<TKey, TValue>` and
 * `SortedList<TKey, TValue>`. The defining behavior is that enumeration (and `Keys`/`Values`) is in
 * **sorted key order** under the default comparer, not insertion order. The transpiler emits
 * `$eq.collections.sortedSet` / `sortedDictionary` / `sortedList`.
 *
 * The order is `Comparer<T>.Default`'s for the element type, which the compiler names when it builds
 * one and the hydration when it rebuilds one ({@link Ordering}): a string in the current culture, a
 * decimal or a date by its `compareTo`, a double with its NaN first. {@link defaultCompare} is what a
 * collection built with no ordering falls back to, which is `Comparer<T>.Default`'s only for a number,
 * a bigint and a char.
 */

import {
  collectionModified,
  containsValue,
  keyText,
  pair,
  requireKey,
  wireObject,
  type KeyEquality,
  type Pair,
} from './dictionary';
import { comparerOf, type Ordering } from './ordering';

/** The order of a collection built with no ordering named: numeric for numbers/bigint, relational otherwise. */
export function defaultCompare<T>(a: T, b: T): number {
  if (a === b) return 0;
  if (a == null) return b == null ? 0 : -1;
  if (b == null) return 1;
  // `<`/`>` give numeric order for number/bigint and code-unit order for strings.
  return a < b ? -1 : a > b ? 1 : 0;
}

/**
 * `SortedSet<T>` — a set whose enumeration is in sorted order. Backed by an array kept sorted and unique
 * (binary search for membership/insertion). `Add` returns false when the value is already present.
 */
export class SortedSet<T> implements Iterable<T> {
  private readonly items: T[] = [];
  private readonly compare: (a: T, b: T) => number;

  constructor(initial?: Iterable<T>, compare: (a: T, b: T) => number = defaultCompare) {
    this.compare = compare;
    if (initial) {
      for (const v of initial) this.add(v);
    }
  }

  get count(): number {
    return this.items.length;
  }

  /** Smallest element, or `undefined` when empty (`.NET` `Min`). */
  get min(): T | undefined {
    return this.items.length > 0 ? this.items[0] : undefined;
  }

  /** Largest element, or `undefined` when empty (`.NET` `Max`). */
  get max(): T | undefined {
    return this.items.length > 0 ? this.items[this.items.length - 1] : undefined;
  }

  /** Index of `value`, or the bitwise-complement insertion point (`~i`) when absent. */
  private indexOf(value: T): number {
    let lo = 0;
    let hi = this.items.length - 1;
    while (lo <= hi) {
      const mid = (lo + hi) >>> 1;
      const c = this.compare(this.items[mid], value);
      if (c === 0) return mid;
      if (c < 0) lo = mid + 1;
      else hi = mid - 1;
    }
    return ~lo;
  }

  add(value: T): boolean {
    const i = this.indexOf(value);
    if (i >= 0) return false;
    this.items.splice(~i, 0, value);
    return true;
  }

  remove(value: T): boolean {
    const i = this.indexOf(value);
    if (i < 0) return false;
    this.items.splice(i, 1);
    return true;
  }

  contains(value: T): boolean {
    return this.indexOf(value) >= 0;
  }

  clear(): void {
    this.items.length = 0;
  }

  [Symbol.iterator](): Iterator<T> {
    return this.items[Symbol.iterator]();
  }

  toArray(): T[] {
    return this.items.slice();
  }
}

export function sortedSet<T>(
  initial?: Iterable<T> | null,
  compare?: ((a: T, b: T) => number) | Ordering,
): SortedSet<T> {
  return new SortedSet<T>(initial ?? undefined, orderOf(compare));
}

/** The comparer a factory was handed: a function as it is, an {@link Ordering} by its comparer. */
function orderOf<T>(compare: ((a: T, b: T) => number) | Ordering | undefined): (a: T, b: T) => number {
  if (typeof compare === 'function') return compare;
  return compare === undefined ? defaultCompare : comparerOf(compare);
}

/**
 * Backing store for `SortedDictionary` and `SortedList` — a dictionary whose keys are kept sorted, so
 * the indexer/`ContainsKey`/`Add`/`Remove` work as usual while `Keys`/`Values`/`foreach` enumerate in
 * key order. Exposes the surface of the runtime's {@link Dictionary} (`get`/`set`/`has`/`delete`/
 * `clear`/`keys`/`values`/`size`, the same pairs, the same JSON) so the compiler routes both alike.
 */
export class SortedMap<K, V> implements Iterable<Pair<K, V>> {
  private readonly entries: { key: K; value: V }[] = [];
  private readonly compare: (a: K, b: K) => number;
  /** Bumped by every change, each of which .NET's sorted enumerators refuse. */
  private version = 0;

  constructor(
    initial?: Iterable<readonly [K, V]>,
    compare: (a: K, b: K) => number = defaultCompare,
    /** Which of .NET's two it stands for, whose refusals of a key already there are worded apart. */
    private readonly kind: 'dictionary' | 'list' = 'dictionary',
  ) {
    this.compare = compare;
    // Added, as a constructor and a collection initializer add: a key already there is refused (#440).
    if (initial) {
      for (const [k, v] of initial) this.add(k, v);
    }
  }

  /**
   * `Add`: a new key, or .NET's refusal of one already there, in each collection's words, measured on
   * .NET 10: a `SortedDictionary` names the pair it was handed (`Key: [2, c]`), and a `SortedList` the
   * key and its parameter (`Key: 2 (Parameter 'key')`). The indexer's write replaces instead.
   */
  add(key: K, value: V): void {
    if (this.has(key)) {
      const named = this.kind === 'list' ? `${keyText(key)} (Parameter 'key')` : `[${keyText(key)}, ${value == null ? '' : keyText(value)}]`;
      throw new Error(`An item with the same key has already been added. Key: ${named}`);
    }
    this.set(key, value);
  }

  /** An object initializer's `[key] = value` entries, written by the indexer, in order. */
  assign(entries: Iterable<readonly [K, V]>): this {
    for (const [key, value] of entries) this.set(key, value);
    return this;
  }

  get size(): number {
    return this.entries.length;
  }

  /** Index of `key`, or the bitwise-complement insertion point (`~i`) when absent. A null key is
   * refused, as .NET's sorted dictionaries refuse one. */
  private indexOf(key: K): number {
    requireKey(key);
    let lo = 0;
    let hi = this.entries.length - 1;
    while (lo <= hi) {
      const mid = (lo + hi) >>> 1;
      const c = this.compare(this.entries[mid].key, key);
      if (c === 0) return mid;
      if (c < 0) lo = mid + 1;
      else hi = mid - 1;
    }
    return ~lo;
  }

  has(key: K): boolean {
    return this.indexOf(key) >= 0;
  }

  get(key: K): V | undefined {
    const i = this.indexOf(key);
    return i >= 0 ? this.entries[i].value : undefined;
  }

  set(key: K, value: V): this {
    const i = this.indexOf(key);
    if (i >= 0) this.entries[i].value = value;
    else this.entries.splice(~i, 0, { key, value });
    this.version++;
    return this;
  }

  delete(key: K): boolean {
    const i = this.indexOf(key);
    if (i < 0) return false;
    this.entries.splice(i, 1);
    this.version++;
    return true;
  }

  /** `TryAdd`: a key that is not there is added and answers true, one that is answers false. */
  tryAdd(key: K, value: V): boolean {
    if (this.indexOf(key) >= 0) return false;
    this.set(key, value);
    return true;
  }

  /** `ContainsValue`, compared as the runtime's Dictionary compares one. */
  containsValue(value: V, byValue: KeyEquality = false): boolean {
    return containsValue(this.entries, value, byValue);
  }

  clear(): void {
    this.entries.length = 0;
    this.version++;
  }

  /** Keys in sorted order. */
  keys(): K[] {
    return this.entries.map((e) => e.key);
  }

  /** Values in key order. */
  values(): V[] {
    return this.entries.map((e) => e.value);
  }

  /**
   * The pairs in key order, destructuring as `[key, value]` and answering `.key` and `.value`. Any
   * change while they are walked ends the walk with .NET's InvalidOperationException, as a sorted
   * dictionary's and a sorted list's enumerators end it (measured): an addition, an overwrite, a
   * removal, `Clear`.
   */
  *[Symbol.iterator](): Iterator<Pair<K, V>> {
    const version = this.version;
    for (const e of this.entries) {
      yield pair(e.key, e.value);
      if (this.version !== version) throw collectionModified();
    }
  }

  /** A dictionary equals only itself, as .NET's does. */
  equals(other: unknown): boolean {
    return this === other;
  }

  /** The JSON object System.Text.Json writes for it, in key order. */
  toJSON(): Record<string, V> {
    return wireObject(this.entries);
  }
}

export function sortedDictionary<K, V>(
  initial?: Iterable<readonly [K, V]> | null,
  compare?: ((a: K, b: K) => number) | Ordering,
): SortedMap<K, V> {
  return new SortedMap<K, V>(initial ?? undefined, orderOf(compare));
}

/** `SortedList<TKey, TValue>` — same observable (key-sorted) behavior as `SortedDictionary` here. */
export function sortedList<K, V>(
  initial?: Iterable<readonly [K, V]> | null,
  compare?: ((a: K, b: K) => number) | Ordering,
): SortedMap<K, V> {
  return new SortedMap<K, V>(initial ?? undefined, orderOf(compare), 'list');
}
