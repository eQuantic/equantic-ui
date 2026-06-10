/**
 * .NET-compat sorted collections — `SortedSet<T>`, `SortedDictionary<TKey, TValue>` and
 * `SortedList<TKey, TValue>`. The defining behavior is that enumeration (and `Keys`/`Values`) is in
 * **sorted key order** under the default comparer, not insertion order. The transpiler emits
 * `$eq.collections.sortedSet` / `sortedDictionary` / `sortedList`.
 *
 * The default comparer matches `Comparer<T>.Default` for numbers and `bigint` (numeric) and orders
 * other values (strings, etc.) by JS relational comparison — i.e. ordinal/code-unit order. Culture-
 * sensitive string ordering is intentionally out of scope (the same documented divergence as the rest
 * of the string subsystem); pass simple/numeric keys for guaranteed .NET parity.
 */

/** `Comparer<T>.Default`-style ordering: numeric for numbers/bigint, relational otherwise. */
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
  initial?: Iterable<T>,
  compare?: (a: T, b: T) => number,
): SortedSet<T> {
  return new SortedSet<T>(initial, compare ?? defaultCompare);
}

/**
 * Backing store for `SortedDictionary` and `SortedList` — a dictionary whose keys are kept sorted, so
 * the indexer/`ContainsKey`/`Add`/`Remove` work as usual while `Keys`/`Values`/`foreach` enumerate in
 * key order. Exposes the same surface as the plain-object dictionary path (`get`/`set`/`has`/`delete`/
 * `clear`/`keys`/`values`/`size` + a `{key,value}` iterator) so the compiler routes it identically.
 */
export class SortedMap<K, V> implements Iterable<{ key: K; value: V }> {
  private readonly entries: { key: K; value: V }[] = [];
  private readonly compare: (a: K, b: K) => number;

  constructor(
    initial?: Iterable<readonly [K, V]>,
    compare: (a: K, b: K) => number = defaultCompare,
  ) {
    this.compare = compare;
    if (initial) {
      for (const [k, v] of initial) this.set(k, v);
    }
  }

  get size(): number {
    return this.entries.length;
  }

  /** Index of `key`, or the bitwise-complement insertion point (`~i`) when absent. */
  private indexOf(key: K): number {
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
    return this;
  }

  delete(key: K): boolean {
    const i = this.indexOf(key);
    if (i < 0) return false;
    this.entries.splice(i, 1);
    return true;
  }

  clear(): void {
    this.entries.length = 0;
  }

  /** Keys in sorted order. */
  keys(): K[] {
    return this.entries.map((e) => e.key);
  }

  /** Values in key order. */
  values(): V[] {
    return this.entries.map((e) => e.value);
  }

  [Symbol.iterator](): Iterator<{ key: K; value: V }> {
    return this.entries.map((e) => ({ key: e.key, value: e.value }))[Symbol.iterator]();
  }
}

export function sortedDictionary<K, V>(
  initial?: Iterable<readonly [K, V]>,
  compare?: (a: K, b: K) => number,
): SortedMap<K, V> {
  return new SortedMap<K, V>(initial, compare ?? defaultCompare);
}

/** `SortedList<TKey, TValue>` — same observable (key-sorted) behavior as `SortedDictionary` here. */
export function sortedList<K, V>(
  initial?: Iterable<readonly [K, V]>,
  compare?: (a: K, b: K) => number,
): SortedMap<K, V> {
  return new SortedMap<K, V>(initial, compare ?? defaultCompare);
}
