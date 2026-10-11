/**
 * `List<T>`'s and `Array`'s members that a JavaScript array method only resembles, as .NET answers
 * them, with the checks .NET makes in the order it makes them (#488):
 *
 *  - `Sort`, by .NET's introspective sort and the comparer the compiler names (`utils/sort.ts`):
 *    `sort()` compares the elements' text, and is stable where .NET's is not;
 *  - `BinarySearch`, which answers the complement of the insertion point for a value it does not
 *    find, where a `findIndex` answered -1;
 *  - `IndexOf`, `LastIndexOf` and their ranges, by the element type's equality ({@link KeyEquality}):
 *    `indexOf` never finds a NaN, a record, a tuple or a decimal equal to the one sought (#425);
 *  - `Find` and `FindLast`, which answer the element type's default for no match, and `FindIndex`'s
 *    and `FindLastIndex`'s ranges, which `findIndex` took for its predicate;
 *  - `RemoveAll`, which answers how many it removed, asking the predicate once per element in order;
 *  - `CopyTo`, which writes into the array it is handed, where `[...list]` made a copy nothing read.
 *
 * An array stands for a `List<T>` and for a `T[]` alike. Where the two check differently, each has
 * its own function, named for it.
 */
import { exception } from './exceptions';
import { sameBy, type KeyEquality } from './key-equality';
import { binarySearchIn, introSort, type SortOrder } from './sort';
import { comparerOf, type Ordering } from './ordering';
import { compare as compareStrings, type StringComparison } from './string-statics';

// ---- .NET's words ------------------------------------------------------------------------------

function argumentNull(parameter: string): Error {
  return exception('System.ArgumentNullException', `Value cannot be null. (Parameter '${parameter}')`);
}

function outOfRange(parameter: string, message: string): Error {
  return exception('System.ArgumentOutOfRangeException', `${message} (Parameter '${parameter}')`);
}

const NON_NEGATIVE = 'Non-negative number required.';
const INVALID_OFF_LEN =
  'Offset and length were out of bounds for the array or count is greater than the number of elements from index to the end of the source collection.';
const INDEX_LESS_OR_EQUAL =
  'Index was out of range. Must be non-negative and less than or equal to the size of the collection.';
const INDEX_LESS = 'Index was out of range. Must be non-negative and less than the size of the collection.';
const COUNT = 'Count must be positive and count must refer to a location within the string/array/collection.';
const BIGGER_THAN_COLLECTION = 'Larger than collection size.';

/** `ArgumentOutOfRangeException.ThrowIfNegative`. */
function requireNonNegative(parameter: string, value: number): void {
  if (value < 0) {
    throw exception(
      'System.ArgumentOutOfRangeException',
      `${parameter} ('${value}') must be a non-negative value. (Parameter '${parameter}')\nActual value was ${value}.`,
    );
  }
}

/** `ArgumentOutOfRangeException.ThrowIfLessThan(value, 0)`. */
function requireAtLeastZero(parameter: string, value: number): void {
  if (value < 0) {
    throw exception(
      'System.ArgumentOutOfRangeException',
      `${parameter} ('${value}') must be greater than or equal to '0'. (Parameter '${parameter}')\nActual value was ${value}.`,
    );
  }
}

// ---- a list's capacity --------------------------------------------------------------------------

/**
 * `new List<T>(capacity)`: an array sizes nothing ahead, but .NET's constructor refuses a negative
 * capacity, and the compiler hands it here before the list's elements are evaluated, as C# evaluates
 * the constructor's argument first. The capacity was dropped unread, so a call that made it never ran
 * and a negative one built a list.
 */
export function listCapacity(capacity: number): void {
  if (capacity < 0) throw outOfRange('capacity', NON_NEGATIVE);
}

// ---- the comparers a sort or a search is handed --------------------------------------------------

const orders = new Map<string, SortOrder<unknown>>();

/**
 * The default comparer of a type, `Comparer<T>.Default`, by the ordering the compiler names for it
 * (`ValueOrdering`), with the helper .NET picks: `'comparable'` for an `IComparable<T>`, `'real'` for
 * a double or a float, `'comparer'` for any other (an enum, a `Nullable<T>`). A null ordering is a type
 * .NET's default comparer cannot order, which throws once it is asked to compare two elements.
 */
export function order<T>(ordering: Ordering | null, kind: SortOrder<T>['kind'] = 'comparer'): SortOrder<T> {
  if (typeof ordering === 'object' && ordering !== null) return { compare: comparerOf(ordering), kind, name: '' };
  const key = `${ordering}|${kind}`;
  let found = orders.get(key);
  if (found === undefined) {
    found = { compare: ordering === null ? null : comparerOf(ordering), kind, name: '' };
    orders.set(key, found);
  }
  return found as SortOrder<T>;
}

/** `StringComparer.Ordinal` and its siblings, handed to a sort or a search: `string.Compare` by that
 *  comparison, an ordinal one answering the difference .NET's does. */
export function stringOrder(comparison: StringComparison): SortOrder<string> {
  const key = `string|${comparison}`;
  let found = orders.get(key) as SortOrder<string> | undefined;
  if (found === undefined) {
    found = {
      compare: (a: string, b: string) => compareStrings(a, b, comparison),
      kind: 'comparer',
      name: '',
    };
    orders.set(key, found as SortOrder<unknown>);
  }
  return found;
}

/**
 * An `IComparer<T>` value handed to a sort or a search: its own `compare`, named in .NET's message
 * about an inconsistent comparer, or, for a null one, the type's default comparer, as .NET takes it.
 */
export function comparerOrder<T>(
  comparer: { compare(a: T, b: T): number } | null | undefined,
  fallback: SortOrder<T>,
  name: string,
): SortOrder<T> {
  if (comparer == null) return fallback;
  return { compare: (a, b) => comparer.compare(a, b), kind: 'comparer', name };
}

// ---- Sort ----------------------------------------------------------------------------------------

/** `List<T>.Sort()`, `Sort(IComparer<T>)` and `Sort(int index, int count, IComparer<T>)`. */
export function listSort<T>(list: T[], how: SortOrder<T>, index?: number, count?: number): void {
  if (index === undefined || count === undefined) {
    introSort(list, 0, list.length, how);
    return;
  }
  if (index < 0) throw outOfRange('index', NON_NEGATIVE);
  if (count < 0) throw outOfRange('count', NON_NEGATIVE);
  if (list.length - index < count) throw exception('System.ArgumentException', INVALID_OFF_LEN);
  introSort(list, index, count, how);
}

/** `List<T>.Sort(Comparison<T>)`. */
export function listSortBy<T>(list: T[], comparison: ((a: T, b: T) => number) | null, name: string): void {
  if (comparison == null) throw argumentNull('comparison');
  introSort(list, 0, list.length, { compare: comparison, kind: 'comparer', name });
}

/** `Array.Sort(array)`, `Sort(array, IComparer<T>)` and `Sort(array, int index, int length, IComparer<T>)`. */
export function arraySort<T>(array: T[] | null, how: SortOrder<T>, index?: number, length?: number): void {
  if (array == null) throw argumentNull('array');
  if (index === undefined || length === undefined) {
    introSort(array, 0, array.length, how);
    return;
  }
  if (index < 0) throw outOfRange('index', NON_NEGATIVE);
  if (length < 0) throw outOfRange('length', NON_NEGATIVE);
  if (array.length - index < length) throw exception('System.ArgumentException', INVALID_OFF_LEN);
  introSort(array, index, length, how);
}

/** `Array.Sort(array, Comparison<T>)`. */
export function arraySortBy<T>(array: T[] | null, comparison: ((a: T, b: T) => number) | null, name: string): void {
  if (array == null) throw argumentNull('array');
  if (comparison == null) throw argumentNull('comparison');
  introSort(array, 0, array.length, { compare: comparison, kind: 'comparer', name });
}

// ---- BinarySearch --------------------------------------------------------------------------------

/** `List<T>.BinarySearch(item)`, `(item, IComparer<T>)` and `(index, count, item, IComparer<T>)`. */
export function binarySearch<T>(list: readonly T[], item: T, how: SortOrder<T>, index?: number, count?: number): number {
  if (index === undefined || count === undefined) return binarySearchIn(list, 0, list.length, item, how);
  if (index < 0) throw outOfRange('index', NON_NEGATIVE);
  if (count < 0) throw outOfRange('count', NON_NEGATIVE);
  if (list.length - index < count) throw exception('System.ArgumentException', INVALID_OFF_LEN);
  return binarySearchIn(list, index, count, item, how);
}

// ---- IndexOf and LastIndexOf ---------------------------------------------------------------------

function indexIn<T>(items: readonly T[], value: T, start: number, count: number, equality: KeyEquality): number {
  const same = sameBy(equality);
  for (let at = start; at < start + count; at++) if (same(items[at], value)) return at;
  return -1;
}

function lastIndexIn<T>(items: readonly T[], value: T, start: number, count: number, equality: KeyEquality): number {
  const same = sameBy(equality);
  for (let at = start; at > start - count; at--) if (same(items[at], value)) return at;
  return -1;
}

/** `Array.IndexOf(array, value, startIndex, count)`'s checks. */
function arrayIndexOfChecks(length: number, startIndex: number, count: number): void {
  if (startIndex < 0 || startIndex > length) throw outOfRange('startIndex', INDEX_LESS_OR_EQUAL);
  if (count < 0 || count > length - startIndex) throw outOfRange('count', COUNT);
}

/** `List<T>.IndexOf(item)`, `(item, index)` and `(item, index, count)`, by the element type's equality. */
export function indexOf<T>(list: readonly T[], item: T, equality: KeyEquality = false, index?: number, count?: number): number {
  if (index === undefined) return indexIn(list, item, 0, list.length, equality);
  if (index > list.length) throw outOfRange('index', INDEX_LESS_OR_EQUAL);
  if (count === undefined) {
    arrayIndexOfChecks(list.length, index, list.length - index);
    return indexIn(list, item, index, list.length - index, equality);
  }
  if (count < 0 || index > list.length - count) throw outOfRange('count', COUNT);
  arrayIndexOfChecks(list.length, index, count);
  return indexIn(list, item, index, count, equality);
}

/** `List<T>.LastIndexOf(item)`, `(item, index)` and `(item, index, count)`, by the element type's equality. */
export function lastIndexOf<T>(list: readonly T[], item: T, equality: KeyEquality = false, index?: number, count?: number): number {
  const size = list.length;
  if (index === undefined) return size === 0 ? -1 : lastIndexIn(list, item, size - 1, size, equality);
  if (count === undefined) {
    if (index >= size) throw outOfRange('index', INDEX_LESS);
    count = index + 1;
  }
  if (size !== 0 && index < 0) throw outOfRange('index', NON_NEGATIVE);
  if (size !== 0 && count < 0) throw outOfRange('count', NON_NEGATIVE);
  if (size === 0) return -1;
  if (index >= size) throw outOfRange('index', BIGGER_THAN_COLLECTION);
  if (count > index + 1) throw outOfRange('count', BIGGER_THAN_COLLECTION);
  return lastIndexIn(list, item, index, count, equality);
}

/** `Array.IndexOf(array, value)`, `(array, value, startIndex)` and `(array, value, startIndex, count)`. */
export function arrayIndexOf<T>(
  array: readonly T[] | null,
  value: T,
  equality: KeyEquality = false,
  startIndex?: number,
  count?: number,
): number {
  const items = requireArray(array);
  const start = startIndex ?? 0;
  const length = count ?? items.length - start;
  arrayIndexOfChecks(items.length, start, length);
  return indexIn(items, value, start, length, equality);
}

/** `Array.LastIndexOf(array, value)`, `(array, value, startIndex)` and `(array, value, startIndex, count)`. */
export function arrayLastIndexOf<T>(
  array: readonly T[] | null,
  value: T,
  equality: KeyEquality = false,
  startIndex?: number,
  count?: number,
): number {
  const items = requireArray(array);
  const start = startIndex ?? items.length - 1;
  const length = count ?? (startIndex === undefined ? items.length : items.length === 0 ? 0 : start + 1);
  if (items.length === 0) {
    if (start !== -1 && start !== 0) throw outOfRange('startIndex', INDEX_LESS);
    if (length !== 0) throw outOfRange('count', COUNT);
    return -1;
  }
  if (start < 0 || start >= items.length) throw outOfRange('startIndex', INDEX_LESS);
  if (length < 0 || start - length + 1 < 0) throw outOfRange('count', COUNT);
  return lastIndexIn(items, value, start, length, equality);
}

// ---- Find, FindLast, FindIndex, FindLastIndex ----------------------------------------------------
//
// A list's own members read the list, so a null one is the TypeError of reading it, which is .NET's
// NullReferenceException; `Array`'s statics refuse a null array by name first ({@link requireArray}).

/** `Array`'s statics refuse a null array before anything else, by its parameter's name. */
function requireArray<T>(array: readonly T[] | null): readonly T[] {
  if (array == null) throw argumentNull('array');
  return array;
}

/** `List<T>.Find(match)`: the first match, or the element type's default. */
export function find<T>(items: readonly T[], match: ((item: T) => boolean) | null, fallback: T): T {
  if (match == null) throw argumentNull('match');
  for (let at = 0; at < items.length; at++) if (match(items[at])) return items[at];
  return fallback;
}

/** `List<T>.FindLast(match)`: the last match, or the element type's default. */
export function findLast<T>(items: readonly T[], match: ((item: T) => boolean) | null, fallback: T): T {
  if (match == null) throw argumentNull('match');
  for (let at = items.length - 1; at >= 0; at--) if (match(items[at])) return items[at];
  return fallback;
}

/** `List<T>.FindIndex(match)`, `(startIndex, match)` and `(startIndex, count, match)`. */
export function findIndex<T>(
  items: readonly T[],
  match: ((item: T) => boolean) | null,
  startIndex = 0,
  count?: number,
): number {
  const length = count ?? items.length - startIndex;
  if (startIndex < 0 || startIndex > items.length) throw outOfRange('startIndex', INDEX_LESS_OR_EQUAL);
  if (length < 0 || startIndex > items.length - length) throw outOfRange('count', COUNT);
  if (match == null) throw argumentNull('match');
  for (let at = startIndex; at < startIndex + length; at++) if (match(items[at])) return at;
  return -1;
}

/** `List<T>.FindLastIndex(match)`, `(startIndex, match)` and `(startIndex, count, match)`. */
export function findLastIndex<T>(
  items: readonly T[],
  match: ((item: T) => boolean) | null,
  startIndex?: number,
  count?: number,
): number {
  const start = startIndex ?? items.length - 1;
  const length = count ?? (startIndex === undefined ? items.length : start + 1);
  if (match == null) throw argumentNull('match');
  if (items.length === 0) {
    if (start !== -1) throw outOfRange('startIndex', INDEX_LESS);
  } else if (start < 0 || start >= items.length) {
    throw outOfRange('startIndex', INDEX_LESS);
  }
  if (length < 0 || start - length + 1 < 0) throw outOfRange('count', COUNT);
  for (let at = start; at > start - length; at--) if (match(items[at])) return at;
  return -1;
}

/** `Array.Find(array, match)`. */
export function arrayFind<T>(array: readonly T[] | null, match: ((item: T) => boolean) | null, fallback: T): T {
  return find(requireArray(array), match, fallback);
}

/** `Array.FindLast(array, match)`. */
export function arrayFindLast<T>(array: readonly T[] | null, match: ((item: T) => boolean) | null, fallback: T): T {
  return findLast(requireArray(array), match, fallback);
}

/** `Array.FindIndex(array, match)`, `(array, startIndex, match)` and `(array, startIndex, count, match)`. */
export function arrayFindIndex<T>(
  array: readonly T[] | null,
  match: ((item: T) => boolean) | null,
  startIndex = 0,
  count?: number,
): number {
  return findIndex(requireArray(array), match, startIndex, count);
}

/** `Array.FindLastIndex(array, match)`, `(array, startIndex, match)` and `(array, startIndex, count, match)`. */
export function arrayFindLastIndex<T>(
  array: readonly T[] | null,
  match: ((item: T) => boolean) | null,
  startIndex?: number,
  count?: number,
): number {
  return findLastIndex(requireArray(array), match, startIndex, count);
}

// ---- RemoveAll, CopyTo ---------------------------------------------------------------------------

/**
 * `List<T>.RemoveAll(match)`: .NET's single pass, the predicate asked once per element in order, the
 * kept elements moved down over the removed ones; answers how many were removed.
 */
export function removeAll<T>(list: T[], match: ((item: T) => boolean) | null): number {
  if (match == null) throw argumentNull('match');
  const size = list.length;
  let free = 0;
  while (free < size && !match(list[free])) free++;
  if (free >= size) return 0;
  let current = free + 1;
  while (current < size) {
    while (current < size && match(list[current])) current++;
    if (current < size) list[free++] = list[current++];
  }
  list.length = free;
  return size - free;
}

/**
 * `List<T>.CopyTo(array)` and `CopyTo(array, arrayIndex)`: the list's elements written into `array` from
 * `arrayIndex`, checked as `Array.Copy` checks them. A receiver typed `ICollection<T>` may be another
 * collection when the call runs: a set copies itself, as the interface call reaches the set's own
 * `CopyTo`, and any other collection is copied in its order (#429). Read as an array, a set threw.
 */
export function copyTo<T>(list: readonly T[] | Iterable<T>, array: T[] | null, arrayIndex = 0): void {
  if (Array.isArray(list)) {
    copyRange(list, 0, array, arrayIndex, list.length);
    return;
  }
  const own = (list as { copyTo?: unknown }).copyTo;
  if (typeof own === 'function') {
    (own as (array: T[] | null, arrayIndex: number) => void).call(list, array, arrayIndex);
    return;
  }
  const items = [...list];
  copyRange(items, 0, array, arrayIndex, items.length);
}

/** `List<T>.CopyTo(index, array, arrayIndex, count)`. */
export function copyRangeTo<T>(list: readonly T[], index: number, array: T[] | null, arrayIndex: number, count: number): void {
  if (list.length - index < count) throw exception('System.ArgumentException', INVALID_OFF_LEN);
  copyRange(list, index, array, arrayIndex, count);
}

/** `Array.Copy(source, sourceIndex, destination, destinationIndex, length)`, checked in .NET's order. */
function copyRange<T>(source: readonly T[], sourceIndex: number, destination: T[] | null, destinationIndex: number, length: number): void {
  if (destination == null) throw argumentNull('destinationArray');
  requireNonNegative('length', length);
  requireAtLeastZero('sourceIndex', sourceIndex);
  if (sourceIndex + length > source.length) {
    throw exception(
      'System.ArgumentException',
      "Source array was not long enough. Check the source index, length, and the array's lower bounds. (Parameter 'sourceArray')",
    );
  }
  requireAtLeastZero('destinationIndex', destinationIndex);
  if (destinationIndex + length > destination.length) {
    throw exception(
      'System.ArgumentException',
      "Destination array was not long enough. Check the destination index, length, and the array's lower bounds. (Parameter 'destinationArray')",
    );
  }
  // Read before written, as Array.Copy copies an overlapping range.
  const copied = source.slice(sourceIndex, sourceIndex + length);
  for (let at = 0; at < length; at++) destination[destinationIndex + at] = copied[at];
}
