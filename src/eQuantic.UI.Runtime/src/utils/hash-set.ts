/**
 * .NET's `HashSet<T>`, the class every C# `HashSet`, `ISet` and `IReadOnlySet` lowers to, built by eqc
 * wherever one is made (a constructor, an initializer, a collection expression, `ToHashSet`) and by
 * the hydration of one that crossed from the server.
 *
 * Its elements are held in the {@link SlotTable} a `Dictionary` keeps its keys in, so the two follow
 * ONE rule: an element is found as `EqualityComparer<T>.Default` finds it ({@link KeyEquality}, which
 * eqc says from the element type), and kept by SLOT, an insertion taking the slot a removal freed last.
 * A JavaScript `Set` found a date, a decimal, a tuple and a record by reference (#531), and appended
 * where .NET reuses the freed slot (#438): `{ 3, 1 }` less 3, plus 5, enumerates `5, 1` in .NET.
 *
 * It has a `Set`'s surface (`add`, `has`, `delete`, `clear`, `size`, the iterators), which is how the
 * lowering and the TypeScript annotations read it, and .NET's members by their camelCase names, each
 * as .NET's does it step for step: `unionWith`, `intersectWith`, `exceptWith`, `symmetricExceptWith`,
 * `removeWhere` and the copies free and take slots in .NET's order, so a later insertion lands where
 * .NET's lands. .NET's capacity is kept too, for the two things it decides: a copy of a set with free
 * slots keeps them while the source is not much larger than it holds, and `TrimExcess` compacts only
 * past that.
 */
import { collectionModified } from './dictionary';
import { exception } from './exceptions';
import { sameEquality, type KeyEquality } from './key-equality';
import { collectionCount, expandPrime, getPrime, SlotTable } from './slots';

type Element<T> = { key: T };

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

/** `ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.capacity)`'s words. */
function outOfRangeCapacity(): Error {
  return exception(
    'System.ArgumentOutOfRangeException',
    "Specified argument was out of the range of valid values. (Parameter 'capacity')",
  );
}

/** A sequence as C# enumerates it: a string by its chars (UTF-16 code units), anything else as it is. */
function elements<T>(source: Iterable<T> | string): Iterable<T> {
  return typeof source === 'string' ? (source.split('') as unknown as T[]) : source;
}

/** Null and undefined are one value here, as they are one null in C#. */
function held<T>(value: T): T {
  return (value === undefined ? null : value) as T;
}

export class HashSet<T> implements Set<T> {
  private readonly table: SlotTable<T, Element<T>>;
  /** Bumped when a NEW element goes in and when `TrimExcess` moves them, the changes .NET's
   *  enumerator refuses; a removal and `Clear` leave a walk running, as .NET's do (measured). */
  private version = 0;

  constructor(equality: KeyEquality = false) {
    this.table = new SlotTable<T, Element<T>>(equality);
  }

  /** How the elements are found. */
  get equality(): KeyEquality {
    return this.table.equality;
  }

  /** `Count`. */
  get size(): number {
    return this.table.size;
  }

  get [Symbol.toStringTag](): string {
    return 'HashSet';
  }

  /** `Contains`. */
  has(value: T): boolean {
    return this.table.find(held(value)) >= 0;
  }

  /** `Add`, answering whether the element was new, as .NET's does. */
  tryAdd(value: T): boolean {
    return this.addIfNotPresent(held(value)) >= 0;
  }

  /** A `Set`'s `add`, which answers the set; the lowering of C#'s `Add` asks {@link tryAdd}. */
  add(value: T): this {
    this.tryAdd(value);
    return this;
  }

  /** `Remove`: frees the element's slot for the next insertion, and answers whether it was there. */
  delete(value: T): boolean {
    const slot = this.table.find(held(value));
    if (slot < 0) return false;
    this.table.release(slot);
    return true;
  }

  /** `Clear`: every slot goes, the freed ones too; the capacity stays. */
  clear(): void {
    this.table.clear();
  }

  /** The new element's slot, or -1 for one already there. */
  private addIfNotPresent(value: T): number {
    if (this.table.find(value) >= 0) return -1;
    this.version++;
    return this.table.insert({ key: value });
  }

  /** The element's slot, or -1: .NET's `AddOrGetLocation` answers it for an element already there. */
  private slotOf(value: T): number {
    return this.table.find(held(value));
  }

  /**
   * The elements of a constructor's collection, as .NET's `HashSet(IEnumerable<T>)` takes them: a set
   * with the same comparer copied slot for slot, its free slots kept, while it is not much larger
   * than it holds (`ConstructFrom`), and any other collection added element by element, sized first
   * by its count when it has one, and trimmed after when duplicates left it too large.
   */
  constructFrom(source: Iterable<T> | string): void {
    if (source instanceof HashSet && sameEquality(source.equality, this.equality)) {
      const other = source as HashSet<T>;
      if (other.size === 0) return;
      const threshold = expandPrime(other.size + 1);
      if (threshold >= other.table.capacity) {
        this.table.copyFrom(other.table, (entry) => entry);
        return;
      }
      this.table.initialize(other.size);
      for (const entry of other.table.entries) if (entry !== undefined) this.addIfNotPresent(entry.key);
      return;
    }
    const count = collectionCount(source);
    if (count !== null && count > 0) this.table.initialize(count);
    this.unionWith(source);
    if (this.table.size > 0 && Math.floor(this.table.capacity / this.table.size) > 3) this.trimExcess();
  }

  /** `UnionWith`: each element of `other` added, in its order. */
  unionWith(other: Iterable<T> | string): void {
    if (other == null) throw argumentNull('other');
    for (const value of elements(other)) this.addIfNotPresent(held(value));
  }

  /** `IntersectWith`: what `other` does not hold leaves, in slot order. */
  intersectWith(other: Iterable<T> | string): void {
    if (other == null) throw argumentNull('other');
    if (this.size === 0 || other === this) return;
    const count = collectionCount(other);
    if (count === 0) {
      this.clear();
      return;
    }
    if (other instanceof HashSet && sameEquality(other.equality, this.equality)) {
      const set = other as HashSet<T>;
      this.table.entries.forEach((entry) => {
        if (entry !== undefined && !set.has(entry.key)) this.delete(entry.key);
      });
      return;
    }
    const originalCount = this.table.entries.length;
    const found = new Set<number>();
    for (const value of elements(other)) {
      const slot = this.slotOf(value);
      if (slot >= 0) found.add(slot);
    }
    for (let slot = 0; slot < originalCount; slot++) {
      const entry = this.table.entries[slot];
      if (entry !== undefined && !found.has(slot)) this.delete(entry.key);
    }
  }

  /** `ExceptWith`: each element of `other` removed, in its order. */
  exceptWith(other: Iterable<T> | string): void {
    if (other == null) throw argumentNull('other');
    if (this.size === 0) return;
    if (other === this) {
      this.clear();
      return;
    }
    for (const value of elements(other)) this.delete(value);
  }

  /**
   * `SymmetricExceptWith`: each element of `other` toggled. A set with the same comparer is walked
   * once, removing what is there and adding what is not; any other sequence may repeat an element, so
   * what was there is marked and removed after, in slot order, as .NET does.
   */
  symmetricExceptWith(other: Iterable<T> | string): void {
    if (other == null) throw argumentNull('other');
    if (this.size === 0) {
      this.unionWith(other);
      return;
    }
    if (other === this) {
      this.clear();
      return;
    }
    if (other instanceof HashSet && sameEquality(other.equality, this.equality)) {
      for (const value of other as HashSet<T>) if (!this.delete(value)) this.addIfNotPresent(held(value));
      return;
    }
    const originalCount = this.table.entries.length;
    const toRemove = new Set<number>();
    const added = new Set<number>();
    for (const value of elements(other)) {
      const slot = this.addIfNotPresent(held(value));
      if (slot >= 0) {
        added.add(slot);
        continue;
      }
      const location = this.slotOf(value);
      if (location < originalCount && !added.has(location)) toRemove.add(location);
    }
    for (let slot = 0; slot < originalCount; slot++) {
      if (toRemove.has(slot)) this.delete(this.table.entries[slot]!.key);
    }
  }

  /** How many distinct elements of this set `other` holds, and how many of its own it holds that this
   *  one does not: .NET's `CheckUniqueAndUnfoundElements`. */
  private uniqueAndUnfound(other: Iterable<T> | string, returnIfUnfound: boolean): [number, number] {
    if (this.table.entries.length === 0) {
      for (const _ of elements(other)) return [0, 1];
      return [0, 0];
    }
    const seen = new Set<number>();
    let unfound = 0;
    for (const value of elements(other)) {
      const slot = this.slotOf(value);
      if (slot >= 0) {
        seen.add(slot);
      } else {
        unfound++;
        if (returnIfUnfound) break;
      }
    }
    return [seen.size, unfound];
  }

  /** Whether every element of this set is in a set with the same comparer. */
  private within(other: HashSet<T>): boolean {
    for (const value of this) if (!other.has(value)) return false;
    return true;
  }

  /** `IsSubsetOf`. */
  isSubsetOf(other: Iterable<T> | string): boolean {
    if (other == null) throw argumentNull('other');
    if (this.size === 0 || other === this) return true;
    const count = collectionCount(other);
    if (count !== null) {
      if (this.size > count) return false;
      if (other instanceof HashSet && sameEquality(other.equality, this.equality)) return this.within(other as HashSet<T>);
    }
    const [unique, unfound] = this.uniqueAndUnfound(other, false);
    return unique === this.size && unfound >= 0;
  }

  /** `IsProperSubsetOf`. */
  isProperSubsetOf(other: Iterable<T> | string): boolean {
    if (other == null) throw argumentNull('other');
    if (other === this) return false;
    const count = collectionCount(other);
    if (count !== null) {
      if (count <= this.size) return false;
      if (this.size === 0) return true;
      if (other instanceof HashSet && sameEquality(other.equality, this.equality)) return this.within(other as HashSet<T>);
    }
    const [unique, unfound] = this.uniqueAndUnfound(other, false);
    return unique === this.size && unfound > 0;
  }

  /** `IsSupersetOf`. */
  isSupersetOf(other: Iterable<T> | string): boolean {
    if (other == null) throw argumentNull('other');
    if (other === this) return true;
    const count = collectionCount(other);
    if (count !== null) {
      if (count === 0) return true;
      if (other instanceof HashSet && sameEquality(other.equality, this.equality) && other.size > this.size) return false;
    }
    for (const value of elements(other)) if (!this.has(value)) return false;
    return true;
  }

  /** `IsProperSupersetOf`. */
  isProperSupersetOf(other: Iterable<T> | string): boolean {
    if (other == null) throw argumentNull('other');
    if (this.size === 0 || other === this) return false;
    const count = collectionCount(other);
    if (count !== null) {
      if (count === 0) return true;
      if (other instanceof HashSet && sameEquality(other.equality, this.equality)) {
        const set = other as HashSet<T>;
        return set.size < this.size && set.within(this);
      }
    }
    const [unique, unfound] = this.uniqueAndUnfound(other, true);
    return unique < this.size && unfound === 0;
  }

  /** `Overlaps`. */
  overlaps(other: Iterable<T> | string): boolean {
    if (other == null) throw argumentNull('other');
    if (this.size === 0) return false;
    if (other === this) return true;
    for (const value of elements(other)) if (this.has(value)) return true;
    return false;
  }

  /** `SetEquals`. */
  setEquals(other: Iterable<T> | string): boolean {
    if (other == null) throw argumentNull('other');
    if (other === this) return true;
    const count = collectionCount(other);
    if (count !== null) {
      if (this.size === 0) return count === 0;
      if (other instanceof HashSet && sameEquality(other.equality, this.equality)) {
        const set = other as HashSet<T>;
        return this.size === set.size && this.within(set);
      }
      if (this.size > count) return false;
    }
    const [unique, unfound] = this.uniqueAndUnfound(other, true);
    return unique === this.size && unfound === 0;
  }

  /** `RemoveWhere`: each element the predicate matches leaves, in slot order; answers how many did. */
  removeWhere(match: (value: T) => boolean): number {
    if (match == null) throw argumentNull('match');
    let removed = 0;
    for (let slot = 0; slot < this.table.entries.length; slot++) {
      const entry = this.table.entries[slot];
      if (entry === undefined) continue;
      const value = entry.key;
      if (match(value) && this.delete(value)) removed++;
    }
    return removed;
  }

  /** `CopyTo(array)`, `CopyTo(array, arrayIndex)` and `CopyTo(array, arrayIndex, count)`. */
  copyTo(array: T[], arrayIndex = 0, count: number = this.size): void {
    if (array == null) throw argumentNull('array');
    if (arrayIndex < 0) throw negative('arrayIndex', arrayIndex);
    if (count < 0) throw negative('count', count);
    if (arrayIndex > array.length || count > array.length - arrayIndex) {
      throw exception(
        'System.ArgumentException',
        'Destination array is not long enough to copy all the items in the collection. Check array index and length.',
      );
    }
    for (const entry of this.table.entries) {
      if (count === 0) break;
      if (entry === undefined) continue;
      array[arrayIndex++] = entry.key;
      count--;
    }
  }

  /** `EnsureCapacity`: the capacity, grown to hold `capacity` elements without growing again. */
  ensureCapacity(capacity: number): number {
    if (capacity < 0) throw outOfRangeCapacity();
    if (this.table.capacity >= capacity) return this.table.capacity;
    this.table.capacity = getPrime(capacity);
    return this.table.capacity;
  }

  /** `TrimExcess()` and `TrimExcess(capacity)`: the elements packed into the first slots, where the
   *  capacity they need is smaller than the one there is. */
  trimExcess(capacity: number = this.size): void {
    if (capacity < this.size) {
      throw exception(
        'System.ArgumentOutOfRangeException',
        `capacity ('${capacity}') must be greater than or equal to '${this.size}'. (Parameter 'capacity')\nActual value was ${capacity}.`,
      );
    }
    const newSize = getPrime(capacity);
    if (newSize >= this.table.capacity) return;
    this.version++;
    this.table.compact(newSize);
  }

  /** The elements, in slot order. An element added while they are walked ends the walk as .NET's
   *  enumerator ends it, with its InvalidOperationException. */
  *[Symbol.iterator](): Generator<T, undefined, unknown> {
    const version = this.version;
    for (const entry of this.table.entries) {
      if (entry === undefined) continue;
      yield entry.key;
      if (this.version !== version) throw collectionModified();
    }
    return undefined;
  }

  /** A `Set`'s `values`. */
  values(): Generator<T, undefined, unknown> {
    return this[Symbol.iterator]();
  }

  /** A `Set`'s `keys`, its elements. */
  keys(): Generator<T, undefined, unknown> {
    return this[Symbol.iterator]();
  }

  /** A `Set`'s `entries`, each element twice. */
  *entries(): Generator<[T, T], undefined, unknown> {
    for (const value of this) yield [value, value];
    return undefined;
  }

  /** A `Set`'s `forEach`. */
  forEach(callback: (value: T, value2: T, set: Set<T>) => void, thisArg?: unknown): void {
    for (const value of this) callback.call(thisArg, value, value, this);
  }

  /** A set equals only itself, as .NET's does: `$eq.equals` asks a value's own `equals`. */
  equals(other: unknown): boolean {
    return this === other;
  }

  /** The JSON array System.Text.Json writes and reads for a set, in slot order. */
  toJSON(): T[] {
    return [...this];
  }
}

/**
 * `new HashSet<T>()`, `new HashSet<T>(capacity)` and `new HashSet<T>(collection)`, the elements found
 * by `equality` ({@link KeyEquality}). A collection argument that is null is refused as .NET refuses
 * it; a constructor given no collection is called with none.
 */
export function hashSet<T>(equality: KeyEquality = false, ...source: [] | [Iterable<T> | string | number | null]): HashSet<T> {
  const set = new HashSet<T>(equality);
  if (source.length === 0) return set;
  const from = source[0];
  if (typeof from === 'number') {
    if (from < 0) throw outOfRangeCapacity();
    if (from > 0) set.ensureCapacity(from);
    return set;
  }
  if (from == null) throw argumentNull('collection');
  set.constructFrom(from);
  return set;
}

/**
 * A set a collection expression or a collection initializer builds: made with no argument, then each
 * element added in order, as C# lowers both to `Add` per element, the elements found by `equality`.
 */
export function hashSetOf<T>(items: Iterable<T>, equality: KeyEquality = false): HashSet<T> {
  const set = new HashSet<T>(equality);
  for (const value of items) set.tryAdd(value);
  return set;
}
