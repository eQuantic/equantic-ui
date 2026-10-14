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
  collectionModifiedAfterInstantiation,
  containsValue,
  keyText,
  mutatingAView,
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
import { liveView } from './view';

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

/** What a sorted set shares with every view taken of it: its elements, kept sorted and unique, and the
 *  version every change bumps (#865). */
interface Tree<T> {
  items: T[];
  version: number;
}

/** A view's range: `GetViewBetween`'s bounds, both included. */
interface Range<T> {
  readonly lower: T;
  readonly upper: T;
}

/** `ArgumentOutOfRangeException`'s words for a parameter, with no value. */
function outOfRange(parameter: string): Error {
  return exception(
    'System.ArgumentOutOfRangeException',
    `Specified argument was out of the range of valid values. (Parameter '${parameter}')`,
  );
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
 * one function per {@link Ordering}, an enum's included).
 *
 * `RemoveWhere` asks its predicate in this set's order, where .NET walks its red-black tree breadth
 * first (#766): a predicate with no effect of its own answers the same, and nothing else here sees
 * the tree's shape.
 *
 * A VIEW (`GetViewBetween`, #865) is a sorted set over the range of the set it was taken from: it shares
 * that set's tree, so each sees what the other changes, holds only the elements in its range, and
 * refuses to add one outside it. It takes .NET's paths through the set operations, which are not a set's:
 * it never merges, and its `ExceptWith` asks the count it last counted ({@link counted}).
 */
export class SortedSet<T> implements Iterable<T> {
  private readonly tree: Tree<T>;
  private readonly compare: (a: T, b: T) => number;
  /** A view's range; a set has none, and holds every element of its tree. */
  private readonly range: Range<T> | undefined;
  /**
   * The count a view last counted, and the version it counted at: .NET's `count` field, which a view
   * counts again only when `Count` is read after a change, and which its `ExceptWith` asks. So a view
   * whose count was never read, or was read while it held nothing, removes nothing there (measured).
   * LINQ and a list's copy read it too ({@link readAsACollection}).
   */
  private counted = 0;
  private countedAt = Number.NaN;

  /**
   * A set of `initial`'s elements in `compare`'s order; or, given the tree and the range of a view, that
   * view, which {@link getViewBetween} is the one to make. The tree's version is bumped where .NET's is:
   * by every `Add`, by every `Remove` of a set that holds anything, each whether or not it changes the
   * set, as .NET's red-black tree may turn on the way down, by `Clear`, and by a set operation along the
   * path .NET takes through it. A walk that sees it move refuses to go on (#765).
   */
  constructor(initial?: Iterable<T>, compare: (a: T, b: T) => number = defaultCompare, view?: { tree: Tree<T>; range: Range<T> }) {
    this.compare = compare;
    this.tree = view?.tree ?? { items: [], version: 0 };
    this.range = view?.range;
    if (initial) {
      for (const v of initial) this.add(v);
    }
  }

  /** `Count`. A view counts its range, and keeps what it counted for its `ExceptWith`. */
  get count(): number {
    const [start, end] = this.span();
    if (this.range !== undefined && this.countedAt !== this.tree.version) {
      this.counted = end - start;
      this.countedAt = this.tree.version;
    }
    return end - start;
  }

  /** Smallest element, or `undefined` when empty (`.NET` `Min`). */
  get min(): T | undefined {
    const [start, end] = this.span();
    return end > start ? this.tree.items[start] : undefined;
  }

  /** Largest element, or `undefined` when empty (`.NET` `Max`). */
  get max(): T | undefined {
    const [start, end] = this.span();
    return end > start ? this.tree.items[end - 1] : undefined;
  }

  /** Where this set's elements are among its tree's: the first index and the one after the last. */
  private span(): [number, number] {
    const items = this.tree.items;
    if (this.range === undefined) return [0, items.length];
    const { lower, upper } = this.range;
    let lo = 0;
    let hi = items.length;
    while (lo < hi) {
      const mid = (lo + hi) >>> 1;
      if (this.compare(lower, items[mid]) > 0) lo = mid + 1;
      else hi = mid;
    }
    const start = lo;
    hi = items.length;
    while (lo < hi) {
      const mid = (lo + hi) >>> 1;
      if (this.compare(upper, items[mid]) >= 0) lo = mid + 1;
      else hi = mid;
    }
    return [start, lo];
  }

  /** This set's elements, in order: a set's own array, a view's copy of its range. Read, never written. */
  private held(): readonly T[] {
    if (this.range === undefined) return this.tree.items;
    const [start, end] = this.span();
    return this.tree.items.slice(start, end);
  }

  /** Whether `value` is in this set's range, as .NET's `IsWithinRange` asks it; a set's holds everything. */
  private inRange(value: T): boolean {
    if (this.range === undefined) return true;
    return this.compare(this.range.lower, value) <= 0 && this.compare(this.range.upper, value) >= 0;
  }

  /** Index of `value` among the tree's elements, or the bitwise-complement insertion point (`~i`) when absent. */
  private indexOf(value: T): number {
    const items = this.tree.items;
    let lo = 0;
    let hi = items.length - 1;
    while (lo <= hi) {
      const mid = (lo + hi) >>> 1;
      const c = this.compare(items[mid], value);
      if (c === 0) return mid;
      if (c < 0) lo = mid + 1;
      else hi = mid - 1;
    }
    return ~lo;
  }

  /** `Add`: a view refuses an element outside its range before it changes anything. */
  add(value: T): boolean {
    if (!this.inRange(value)) throw outOfRange('item');
    this.tree.version++;
    const i = this.indexOf(value);
    if (i >= 0) return false;
    this.tree.items.splice(~i, 0, value);
    return true;
  }

  /** `Remove`: an element outside a view's range is not there to remove. */
  remove(value: T): boolean {
    if (!this.inRange(value) || this.tree.items.length === 0) return false;
    this.tree.version++;
    const i = this.indexOf(value);
    if (i < 0) return false;
    this.tree.items.splice(i, 1);
    return true;
  }

  contains(value: T): boolean {
    return this.inRange(value) && this.indexOf(value) >= 0;
  }

  /** `TryGetValue`: the element the set holds that its comparer finds equal to `value`, which may be
   *  another instance (`1.0m` for `1.00m`), or undefined where .NET answers false (#866). */
  tryGetValue(value: T): T | undefined {
    if (!this.inRange(value)) return undefined;
    const i = this.indexOf(value);
    return i >= 0 ? this.tree.items[i] : undefined;
  }

  /** `Clear`: a set's every element, a view's those of its range, which it leaves alone when it holds none. */
  clear(): void {
    if (this.range === undefined) {
      this.tree.items.length = 0;
      this.tree.version++;
      return;
    }
    if (this.count === 0) return;
    const [start, end] = this.span();
    this.tree.items.splice(start, end - start);
    this.tree.version++;
    this.counted = 0;
  }

  /** In order. A change while it is walked ends the walk at its next step, the one after the last
   *  element included, with .NET's InvalidOperationException in System.Collections' words, where it
   *  walked the elements as they changed, and a walk that added as it went never ended (#765). A view's
   *  walk ends at any change to its set's, inside its range or not. */
  *[Symbol.iterator](): Generator<T, undefined, unknown> {
    const version = this.tree.version;
    const [start, end] = this.span();
    for (let at = start; at < end; at++) {
      yield this.tree.items[at];
      if (this.tree.version !== version) throw collectionModifiedAfterInstantiation();
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
    const version = this.tree.version;
    const [start, end] = this.span();
    for (let at = end - 1; at >= start; at--) {
      yield this.tree.items[at];
      if (this.tree.version !== version) throw collectionModifiedAfterInstantiation();
    }
    return undefined;
  }

  toArray(): T[] {
    return this.held().slice();
  }

  /** `CopyTo(array)`, `CopyTo(array, index)` and `CopyTo(array, index, count)`: in order, from `index`,
   *  at most `count` of them, after .NET's checks in .NET's order. */
  copyTo(array: T[] | null, index = 0, count: number = this.count): void {
    if (array == null) throw argumentNull('array');
    if (index < 0) throw negative('index', index);
    if (count < 0) throw negative('count', count);
    if (count > array.length - index) {
      throw exception(
        'System.ArgumentException',
        'Destination array is not long enough to copy all the items in the collection. Check array index and length.',
      );
    }
    for (const item of this.held().slice(0, count)) array[index++] = item;
  }

  /**
   * `GetViewBetween`: a view of this set's elements from `lower` to `upper`, both included, live both
   * ways (#865). A view is taken of the set a view was taken from, and only within its own range, which
   * refuses a bound outside it before the bounds are compared, as .NET refuses them.
   */
  getViewBetween(lower: T, upper: T): SortedSet<T> {
    if (this.range !== undefined) {
      if (this.compare(this.range.lower, lower) > 0) throw outOfRange('lowerValue');
      if (this.compare(this.range.upper, upper) < 0) throw outOfRange('upperValue');
    }
    if (this.compare(lower, upper) > 0) {
      throw exception('System.ArgumentException', "Must be less than or equal to upperValue. (Parameter 'lowerValue')");
    }
    return new SortedSet<T>(undefined, this.compare, { tree: this.tree, range: { lower, upper } });
  }

  /** Whether `other` is a sorted set this one shares its comparer with: .NET's `HasEqualComparer`. */
  private sharesOrder(other: unknown): other is SortedSet<T> {
    return other instanceof SortedSet && other.compare === this.compare;
  }

  /** The elements, replaced by `items`, which are in this set's order already: the tree .NET builds
   *  anew from a sorted array, which is a change whatever it holds. A set's only: a view never merges. */
  private replace(items: T[]): void {
    this.tree.items = items;
    this.tree.version++;
  }

  /**
   * `UnionWith`. An empty set takes a sorted set's elements; a set of the same comparer no more than
   * twice this one's size is merged in, its element kept of two equal ones; anything else is added
   * element by element, which keeps this set's, each asked with `Contains` first, as .NET asks it. A
   * view always adds element by element, and refuses the first outside its range.
   */
  unionWith(other: Iterable<T> | string): void {
    if (other == null) throw argumentNull('other');
    if (this.range === undefined && other instanceof SortedSet && this.count === 0) {
      // Copied as `new SortedSet<T>(other)` copies it, which counts a view first.
      readAsACollection(other);
      for (const value of (other as SortedSet<T>).held()) this.add(value);
      this.tree.version++;
      return;
    }
    if (this.range === undefined && this.sharesOrder(other) && other.count > Math.trunc(this.count / 2)) {
      const mine = this.tree.items;
      const theirs = other.held();
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
   * `IntersectWith`. A set of the same comparer is merged into a set, its element kept of two equal
   * ones; any other sequence, and any sequence at all into a view, is walked, what this set holds of it
   * saved, and the set rebuilt from what was saved, the other's elements again.
   */
  intersectWith(other: Iterable<T> | string): void {
    if (other == null) throw argumentNull('other');
    if (this.count === 0 || other === this) return;
    if (this.range === undefined && this.sharesOrder(other)) {
      const mine = this.tree.items;
      const theirs = other.held();
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
   *  meets this one's, and only up to this one's largest, walked as it is, so a view of this set's own
   *  tree refuses the walk at the first removal, and any other sequence's elements only where this set
   *  holds them, as .NET asks `Contains` of them first. A view asks the count it last counted. */
  exceptWith(other: Iterable<T> | string): void {
    if (other == null) throw argumentNull('other');
    if ((this.range === undefined ? this.tree.items.length : this.counted) === 0) return;
    if (other === this) {
      this.clear();
      return;
    }
    if (this.sharesOrder(other)) {
      const mine = this.held();
      const theirs = other.held();
      // A view whose last count is out of date may hold nothing now: no range meets an empty one.
      if (mine.length === 0 || theirs.length === 0) return;
      const min = mine[0];
      const max = mine[mine.length - 1];
      if (this.compare(theirs[theirs.length - 1], min) < 0 || this.compare(theirs[0], max) > 0) return;
      for (const value of other) {
        if (this.compare(value, min) < 0) continue;
        if (this.compare(value, max) > 0) break;
        this.remove(value);
      }
      return;
    }
    for (const value of elements(other)) if (this.contains(value)) this.remove(value);
  }

  /**
   * `SymmetricExceptWith`: each distinct element of `other` toggled. A sorted set of the same comparer is
   * walked as it is, as {@link exceptWith} walks one; any other sequence is copied and sorted by this
   * set's comparer first, as .NET's `Array.Sort` sorts it, and the first of each run of equal elements
   * is the one toggled. A view refuses to add an element outside its range.
   */
  symmetricExceptWith(other: Iterable<T> | string): void {
    if (other == null) throw argumentNull('other');
    if (this.count === 0) {
      this.unionWith(other);
      return;
    }
    if (other === this) {
      this.clear();
      return;
    }
    if (this.sharesOrder(other)) {
      for (const value of other) if (!this.remove(value)) this.add(value);
      return;
    }
    readAsACollection(other);
    const sorted = Array.from(elements(other));
    introSort(sorted, 0, sorted.length, { compare: this.compare, kind: 'comparer', name: '' });
    sorted.forEach((value, at) => {
      if (at > 0 && this.compare(value, sorted[at - 1]) === 0) return;
      if (!this.remove(value)) this.add(value);
    });
  }

  /**
   * Whether every element of `other`, a set of this set's comparer that holds anything, is in this set's
   * view of `other`'s range: .NET's path for a sorted set, so a view narrower than that range refuses it
   * with the ArgumentOutOfRangeException `GetViewBetween` throws (measured).
   */
  private holdsAllOf(other: SortedSet<T>): boolean {
    const pruned = this.getViewBetween(other.min as T, other.max as T);
    for (const value of other) if (!pruned.contains(value)) return false;
    return true;
  }

  /**
   * How many distinct elements of this set `other` holds, and how many of its own it holds that this
   * one does not, walking it to its end or, when `stopAtUnfound`, to its first such element: .NET's
   * `CheckUniqueAndUnfoundElements`. An empty set reads at most one element of `other`.
   */
  private uniqueAndUnfound(other: Iterable<T> | string, stopAtUnfound: boolean): [number, number] {
    if (this.count === 0) {
      for (const _ of elements(other)) return [0, 1];
      return [0, 0];
    }
    const found = new Set<number>();
    let unfound = 0;
    for (const value of elements(other)) {
      const at = this.inRange(value) ? this.indexOf(value) : -1;
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
    if (this.count === 0) return true;
    if (this.sharesOrder(other)) return this.count <= other.count && other.holdsAllOf(this);
    const [unique] = this.uniqueAndUnfound(other, false);
    return unique === this.count;
  }

  /** `IsProperSubsetOf`. */
  isProperSubsetOf(other: Iterable<T> | string): boolean {
    if (other == null) throw argumentNull('other');
    // An empty set asks a collection its count before anything else.
    if (this.count === 0) readAsACollection(other);
    if (this.sharesOrder(other)) return this.count < other.count && (this.count === 0 || other.holdsAllOf(this));
    const [unique, unfound] = this.uniqueAndUnfound(other, false);
    return unique === this.count && unfound > 0;
  }

  /** `IsSupersetOf`. */
  isSupersetOf(other: Iterable<T> | string): boolean {
    if (other == null) throw argumentNull('other');
    readAsACollection(other);
    if (this.sharesOrder(other)) return other.count === 0 || (this.count >= other.count && this.holdsAllOf(other));
    for (const value of elements(other)) if (!this.contains(value)) return false;
    return true;
  }

  /** `IsProperSupersetOf`. */
  isProperSupersetOf(other: Iterable<T> | string): boolean {
    if (other == null) throw argumentNull('other');
    if (this.count === 0) return false;
    readAsACollection(other);
    if (this.sharesOrder(other)) return other.count === 0 || (other.count < this.count && this.holdsAllOf(other));
    const [unique, unfound] = this.uniqueAndUnfound(other, true);
    return unique < this.count && unfound === 0;
  }

  /** `Overlaps`. */
  overlaps(other: Iterable<T> | string): boolean {
    if (other == null) throw argumentNull('other');
    if (this.count === 0) return false;
    readAsACollection(other);
    for (const value of elements(other)) if (this.contains(value)) return true;
    return false;
  }

  /** `SetEquals`. */
  setEquals(other: Iterable<T> | string): boolean {
    if (other == null) throw argumentNull('other');
    if (this.sharesOrder(other)) {
      const mine = this.held();
      const theirs = other.held();
      return mine.length === theirs.length && mine.every((value, at) => this.compare(value, theirs[at]) === 0);
    }
    const [unique, unfound] = this.uniqueAndUnfound(other, true);
    return unique === this.count && unfound === 0;
  }

  /** `RemoveWhere`: the predicate asked of every element first, then each it matched removed; answers
   *  how many were. A predicate that throws removes nothing. */
  removeWhere(match: ((value: T) => boolean) | null): number {
    if (match == null) throw argumentNull('match');
    if (this.count === 0) return 0;
    const matched = this.held().filter((value) => match(value));
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
 * What .NET reads of a collection before it copies or counts it: its `Count`, as an `ICollection<T>`,
 * which is where a view counts again (#865). LINQ, `new List<T>(…)`, `new SortedSet<T>(…)` and a sorted
 * set's own operations ask it where .NET's do. Every LINQ operator reads its source through here, where
 * .NET's `First` or `Where` only walk it, and `AddRange` does not, where .NET's counts: a view's
 * `ExceptWith` after one of those may remove what .NET's would not, or keep what it would remove.
 */
export function readAsACollection(source: unknown): void {
  if (source instanceof SortedSet) void source.count;
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
  readAsACollection(initial);
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
  /**
   * Bumped by every change, each of which .NET's sorted enumerators refuse, and where a sorted
   * dictionary's red-black tree bumps its own though nothing changed: by a removal of a key it does not
   * hold and by an `Add` of one it does, either while it holds anything, as the tree may turn on the way
   * down (measured). A sorted list's arrays move only when they change.
   */
  private version = 0;
  /** The live views `Keys` and `Values` answer, made on the first read and answered after it. */
  private keyView?: K[];
  private valueView?: V[];

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
      if (this.kind === 'dictionary') this.version++;
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
    if (i < 0) {
      if (this.kind === 'dictionary' && this.entries.length > 0) this.version++;
      return false;
    }
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

  /** `Keys`, in key order: a LIVE view, as .NET's `KeyCollection` and `KeyList` are, where it was an
   *  array copied when it was read (#864). Walked as the pairs are. */
  keys(): K[] {
    return (this.keyView ??= this.view('key', (entry) => entry.key));
  }

  /** `Values`, in key order: a live view, as .NET's `ValueCollection` and `ValueList` are (#864). */
  values(): V[] {
    return (this.valueView ??= this.view('value', (entry) => entry.value));
  }

  /**
   * A live view of the keys or the values ({@link liveView}). A sorted dictionary's refuses a change in
   * a dictionary's words, as its `KeyCollection` and `ValueCollection` do; a sorted list's views are
   * lists, which refuse a call in their own words and a key written through the indexer in a
   * dictionary's (measured).
   */
  private view<T>(collection: 'key' | 'value', pick: (entry: { key: K; value: V }) => T): T[] {
    return liveView({
      read: () => this.entries.map(pick),
      revision: () => this.version,
      count: () => this.entries.length,
      walk: () => this.walk(pick),
      contains: collection === 'key' ? (key: T) => this.has(key as unknown as K) : undefined,
      refusal: (change) =>
        this.kind === 'list' && (change === 'call' || collection === 'value')
          ? exception(
              'System.NotSupportedException',
              'This operation is not supported on SortedList nested types because they require modifying the original SortedList.',
            )
          : mutatingAView(collection),
    });
  }

  /**
   * The entries, picked, in key order. Any change while they are walked ends the walk at its next step,
   * the one after the last entry included, with .NET's InvalidOperationException in the words of
   * System.Collections, where the two live (#864): an addition, an overwrite, a removal, `Clear`.
   */
  private *walk<T>(pick: (entry: { key: K; value: V }) => T): Generator<T, undefined, unknown> {
    const version = this.version;
    for (let at = 0; at < this.entries.length; at++) {
      yield pick(this.entries[at]);
      if (this.version !== version) throw collectionModifiedAfterInstantiation();
    }
    return undefined;
  }

  /** The pairs in key order, destructuring as `[key, value]` and answering `.key` and `.value`, walked
   *  as the keys and the values are. */
  [Symbol.iterator](): Iterator<Pair<K, V>> {
    return this.walk((entry) => pair(entry.key, entry.value));
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
