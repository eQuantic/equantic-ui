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
  collectionModifiedAfterInstantiation,
  containsValue,
  keyText,
  pair,
  pairsOf,
  requireKey,
  type KeyEquality,
  type Pair,
} from './dictionary';
import { comparerOf, type Ordering } from './ordering';
import { identityHash } from './hash';
import { exception } from './exceptions';
import { introSort } from './sort';

/** `ArgumentNullException`'s words for a parameter. */
function argumentNull(parameter: string): Error {
  return exception('System.ArgumentNullException', `Value cannot be null. (Parameter '${parameter}')`);
}

/** `ArgumentOutOfRangeException.ThrowIfNegative`'s words. */
function negative(parameter: string, value: number): Error {
  return exception(
    'System.ArgumentOutOfRangeException',
    `${parameter} ('${value}') must be a non-negative value. (Parameter '${parameter}')\nActual value was ${value}.`,
  );
}

/** A sequence as C# enumerates it: a string by its chars (UTF-16 code units), anything else as it is. */
function elements<T>(source: Iterable<T> | string): Iterable<T> {
  return typeof source === 'string' ? (source.split('') as unknown as T[]) : source;
}

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
 *
 * Its set operations, its questions and `CopyTo` are .NET's, refusals included (#723). Of two elements
 * its order finds equal, a union and an intersection keep the instance .NET keeps, which a decimal
 * shows (`1.0m` and `1.00m`): where .NET merges two sets of one comparer, the other set's; where it
 * adds the other's elements one by one, the first added. Two sets have one comparer here when they
 * compare with the same function, which two sets of one element type do ({@link comparerOf} answers
 * one function per {@link Ordering}), an enum's aside, whose names are equal only when identical, so
 * either path keeps the same elements.
 *
 * `RemoveWhere` asks its predicate in this set's order, where .NET walks its red-black tree breadth
 * first (#766): a predicate with no effect of its own answers the same, and nothing else here sees
 * the tree's shape.
 */
export class SortedSet<T> implements Iterable<T> {
  private readonly items: T[] = [];
  private readonly compare: (a: T, b: T) => number;
  /**
   * Bumped where .NET's `version` is: by every `Add`, by every `Remove` of a set that holds anything,
   * each whether or not it changes the set, as .NET's red-black tree may turn on the way down, by
   * `Clear`, and by a set operation along the path .NET takes through it. A walk that sees it move
   * refuses to go on (#765).
   */
  private version = 0;

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
    this.version++;
    const i = this.indexOf(value);
    if (i >= 0) return false;
    this.items.splice(~i, 0, value);
    return true;
  }

  remove(value: T): boolean {
    if (this.items.length === 0) return false;
    this.version++;
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
    this.version++;
  }

  /** In order. A change while it is walked ends the walk at its next step, the one after the last
   *  element included, with .NET's InvalidOperationException in System.Collections' words, where it
   *  walked the elements as they changed, and a walk that added as it went never ended (#765). */
  *[Symbol.iterator](): Generator<T, undefined, unknown> {
    const version = this.version;
    for (let at = 0; at < this.items.length; at++) {
      yield this.items[at];
      if (this.version !== version) throw collectionModifiedAfterInstantiation();
    }
    return undefined;
  }

  /** `Reverse()`: the elements from the largest, read when they are walked and each time they are,
   *  and refused as the forward walk is when the set changes under it, written to JSON as the array
   *  System.Text.Json writes for a sequence. It was a TypeError (#765). */
  reverse(): Iterable<T> & { toJSON(): T[] } {
    return {
      [Symbol.iterator]: () => this.walkBackwards(),
      toJSON: () => [...this.walkBackwards()],
    };
  }

  private *walkBackwards(): Generator<T, undefined, unknown> {
    const version = this.version;
    for (let at = this.items.length - 1; at >= 0; at--) {
      yield this.items[at];
      if (this.version !== version) throw collectionModifiedAfterInstantiation();
    }
    return undefined;
  }

  toArray(): T[] {
    return this.items.slice();
  }

  /** `CopyTo(array)`, `CopyTo(array, index)` and `CopyTo(array, index, count)`: in order, from `index`,
   *  at most `count` of them, after .NET's checks in .NET's order. */
  copyTo(array: T[] | null, index = 0, count: number = this.items.length): void {
    if (array == null) throw argumentNull('array');
    if (index < 0) throw negative('index', index);
    if (count < 0) throw negative('count', count);
    if (count > array.length - index) {
      throw exception(
        'System.ArgumentException',
        'Destination array is not long enough to copy all the items in the collection. Check array index and length.',
      );
    }
    for (const item of this.items.slice(0, count)) array[index++] = item;
  }

  /** Whether `other` is a sorted set this one shares its comparer with: .NET's `HasEqualComparer`. */
  private sharesOrder(other: unknown): other is SortedSet<T> {
    return other instanceof SortedSet && other.compare === this.compare;
  }

  /** The elements, replaced by `items`, which are in this set's order already: the tree .NET builds
   *  anew from a sorted array, which is a change whatever it holds. */
  private replace(items: T[]): void {
    this.items.splice(0, this.items.length, ...items);
    this.version++;
  }

  /**
   * `UnionWith`. An empty set takes a sorted set's elements; a set of the same comparer no more than
   * twice this one's size is merged in, its element kept of two equal ones; anything else is added
   * element by element, which keeps this set's, each asked with `Contains` first, as .NET asks it.
   */
  unionWith(other: Iterable<T> | string): void {
    if (other == null) throw argumentNull('other');
    if (other instanceof SortedSet && this.items.length === 0) {
      for (const value of (other as SortedSet<T>).items) this.add(value);
      this.version++;
      return;
    }
    if (this.sharesOrder(other) && other.items.length > Math.trunc(this.items.length / 2)) {
      const mine = this.items;
      const theirs = other.items;
      const merged: T[] = [];
      let i = 0;
      let j = 0;
      while (i < mine.length && j < theirs.length) {
        const order = this.compare(mine[i], theirs[j]);
        if (order < 0) merged.push(mine[i++]);
        else if (order === 0) {
          merged.push(theirs[j++]);
          i++;
        } else merged.push(theirs[j++]);
      }
      this.replace([...merged, ...mine.slice(i), ...theirs.slice(j)]);
      return;
    }
    for (const value of elements(other)) if (!this.contains(value)) this.add(value);
  }

  /**
   * `IntersectWith`. A set of the same comparer is merged, its element kept of two equal ones; any
   * other sequence is walked, what this set holds of it saved, and the set rebuilt from what was
   * saved, the other's elements again.
   */
  intersectWith(other: Iterable<T> | string): void {
    if (other == null) throw argumentNull('other');
    if (this.items.length === 0 || other === this) return;
    if (this.sharesOrder(other)) {
      const mine = this.items;
      const theirs = other.items;
      const max = mine[mine.length - 1];
      const merged: T[] = [];
      let i = 0;
      let j = 0;
      while (i < mine.length && j < theirs.length && this.compare(theirs[j], max) <= 0) {
        const order = this.compare(mine[i], theirs[j]);
        if (order < 0) i++;
        else if (order === 0) {
          merged.push(theirs[j++]);
          i++;
        } else j++;
      }
      this.replace(merged);
      return;
    }
    const saved: T[] = [];
    for (const value of elements(other)) if (this.contains(value)) saved.push(value);
    this.clear();
    for (const value of saved) this.add(value);
  }

  /** `ExceptWith`: each element of `other` removed; a set of the same comparer only where its range
   *  meets this one's, and only up to this one's largest, and any other sequence's elements only where
   *  this set holds them, as .NET asks `Contains` of them first. */
  exceptWith(other: Iterable<T> | string): void {
    if (other == null) throw argumentNull('other');
    if (this.items.length === 0) return;
    if (other === this) {
      this.clear();
      return;
    }
    if (this.sharesOrder(other)) {
      const min = this.items[0];
      const max = this.items[this.items.length - 1];
      if (other.items.length === 0 || this.compare(other.items[other.items.length - 1], min) < 0) return;
      if (this.compare(other.items[0], max) > 0) return;
      for (const value of other.items.slice()) {
        if (this.compare(value, min) < 0) continue;
        if (this.compare(value, max) > 0) break;
        this.remove(value);
      }
      return;
    }
    for (const value of elements(other)) if (this.contains(value)) this.remove(value);
  }

  /**
   * `SymmetricExceptWith`: each distinct element of `other` toggled. Any sequence but a sorted set of
   * the same comparer is copied and sorted by this set's comparer first, as .NET's `Array.Sort` sorts
   * it, and the first of each run of equal elements is the one toggled.
   */
  symmetricExceptWith(other: Iterable<T> | string): void {
    if (other == null) throw argumentNull('other');
    if (this.items.length === 0) {
      this.unionWith(other);
      return;
    }
    if (other === this) {
      this.clear();
      return;
    }
    if (this.sharesOrder(other)) {
      for (const value of other.items.slice()) if (!this.remove(value)) this.add(value);
      return;
    }
    const sorted = Array.from(elements(other));
    introSort(sorted, 0, sorted.length, { compare: this.compare, kind: 'comparer', name: '' });
    sorted.forEach((value, at) => {
      if (at > 0 && this.compare(value, sorted[at - 1]) === 0) return;
      if (!this.remove(value)) this.add(value);
    });
  }

  /** Whether every element of this set is in `other`, a set of the same comparer. */
  private within(other: SortedSet<T>): boolean {
    return this.items.every((value) => other.contains(value));
  }

  /**
   * How many distinct elements of this set `other` holds, and how many of its own it holds that this
   * one does not, walking it to its end or, when `stopAtUnfound`, to its first such element: .NET's
   * `CheckUniqueAndUnfoundElements`. An empty set reads at most one element of `other`.
   */
  private uniqueAndUnfound(other: Iterable<T> | string, stopAtUnfound: boolean): [number, number] {
    if (this.items.length === 0) {
      for (const _ of elements(other)) return [0, 1];
      return [0, 0];
    }
    const found = new Set<number>();
    let unfound = 0;
    for (const value of elements(other)) {
      const at = this.indexOf(value);
      if (at >= 0) found.add(at);
      else {
        unfound++;
        if (stopAtUnfound) break;
      }
    }
    return [found.size, unfound];
  }

  /** `IsSubsetOf`. */
  isSubsetOf(other: Iterable<T> | string): boolean {
    if (other == null) throw argumentNull('other');
    if (this.items.length === 0) return true;
    if (this.sharesOrder(other)) return this.items.length <= other.items.length && this.within(other);
    const [unique] = this.uniqueAndUnfound(other, false);
    return unique === this.items.length;
  }

  /** `IsProperSubsetOf`. */
  isProperSubsetOf(other: Iterable<T> | string): boolean {
    if (other == null) throw argumentNull('other');
    if (this.sharesOrder(other)) return this.items.length < other.items.length && this.within(other);
    const [unique, unfound] = this.uniqueAndUnfound(other, false);
    return unique === this.items.length && unfound > 0;
  }

  /** `IsSupersetOf`. */
  isSupersetOf(other: Iterable<T> | string): boolean {
    if (other == null) throw argumentNull('other');
    if (this.sharesOrder(other) && this.items.length >= other.items.length) return other.within(this);
    for (const value of elements(other)) if (!this.contains(value)) return false;
    return true;
  }

  /** `IsProperSupersetOf`. */
  isProperSupersetOf(other: Iterable<T> | string): boolean {
    if (other == null) throw argumentNull('other');
    if (this.items.length === 0) return false;
    if (this.sharesOrder(other)) return other.items.length < this.items.length && other.within(this);
    const [unique, unfound] = this.uniqueAndUnfound(other, true);
    return unique < this.items.length && unfound === 0;
  }

  /** `Overlaps`. */
  overlaps(other: Iterable<T> | string): boolean {
    if (other == null) throw argumentNull('other');
    if (this.items.length === 0) return false;
    for (const value of elements(other)) if (this.contains(value)) return true;
    return false;
  }

  /** `SetEquals`. */
  setEquals(other: Iterable<T> | string): boolean {
    if (other == null) throw argumentNull('other');
    if (this.sharesOrder(other)) {
      const theirs = other.items;
      return this.items.length === theirs.length && this.items.every((value, at) => this.compare(value, theirs[at]) === 0);
    }
    const [unique, unfound] = this.uniqueAndUnfound(other, true);
    return unique === this.items.length && unfound === 0;
  }

  /** `RemoveWhere`: the predicate asked of every element first, then each it matched removed; answers
   *  how many were. A predicate that throws removes nothing. */
  removeWhere(match: ((value: T) => boolean) | null): number {
    if (match == null) throw argumentNull('match');
    const matched = this.items.filter((value) => match(value));
    let removed = 0;
    for (let at = matched.length - 1; at >= 0; at--) if (this.remove(matched[at])) removed++;
    return removed;
  }

  /** The array System.Text.Json writes for a sorted set, in its order: with no `toJSON` it was
   * written as the class's fields (#597). */
  toJSON(): T[] {
    return this.toArray();
  }
}

/**
 * `new SortedSet<T>(…)`: the elements it copies, in the order `compare` says. Where the construction
 * wrote a collection, the compiler names the parameter it binds to (`collection`), and a null one is
 * refused by that name, as .NET refuses it (#726); with no name, a null is no collection, which is how
 * the compiler writes a construction that copies none beside a comparer.
 */
export function sortedSet<T>(
  initial?: Iterable<T> | string | null,
  compare?: ((a: T, b: T) => number) | Ordering,
  collection?: string,
): SortedSet<T> {
  if (collection !== undefined && initial == null) throw argumentNull(collection);
  return new SortedSet<T>(initial == null ? undefined : elements(initial), orderOf(compare));
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
    /** Which of .NET's two it stands for, whose refusals of a key already there are worded apart, and
     * which hydration carries across (`DictionarySpec.sorted`). */
    readonly kind: 'dictionary' | 'list' = 'dictionary',
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
  add(key: K, value: V): this {
    if (this.has(key)) {
      const named = this.kind === 'list' ? `${keyText(key)} (Parameter 'key')` : `[${keyText(key)}, ${value == null ? '' : keyText(value)}]`;
      throw exception('System.ArgumentException', `An item with the same key has already been added. Key: ${named}`);
    }
    return this.set(key, value);
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

  /** `GetHashCode()`: its identity's, as `equals` reads it. */
  getHashCode(): number {
    return identityHash(this);
  }

  /** Its pairs in key order, as the server writes and reads a sorted dictionary (#437). */
  toJSON(): [K | string, V][] {
    return pairsOf(this.entries);
  }
}

/**
 * `new SortedDictionary<K, V>(…)`: the pairs it copies, in the order `compare` says. A null dictionary
 * is refused by the parameter the compiler names (`dictionary`), as {@link sortedSet} refuses its
 * collection (#726).
 */
export function sortedDictionary<K, V>(
  initial?: Iterable<readonly [K, V]> | null,
  compare?: ((a: K, b: K) => number) | Ordering,
  dictionary?: string,
): SortedMap<K, V> {
  if (dictionary !== undefined && initial == null) throw argumentNull(dictionary);
  return new SortedMap<K, V>(initial ?? undefined, orderOf(compare));
}

/** `SortedList<TKey, TValue>` — same observable (key-sorted) behavior as `SortedDictionary` here. */
export function sortedList<K, V>(
  initial?: Iterable<readonly [K, V]> | null,
  compare?: ((a: K, b: K) => number) | Ordering,
  dictionary?: string,
): SortedMap<K, V> {
  if (dictionary !== undefined && initial == null) throw argumentNull(dictionary);
  return new SortedMap<K, V>(initial ?? undefined, orderOf(compare), 'list');
}
