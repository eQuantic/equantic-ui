/**
 * A `List<T>`'s version, as .NET's `_version`: every change a list makes moves it, and a walk over the
 * list compares it at every step, the step past the last element included, and refuses a change with
 * the InvalidOperationException .NET throws (#863). The twin of a list is the array itself, whose own
 * iterator reads the length at every step, so a walk that added as it went never ended.
 *
 * The version lives on the array under a symbol, which `JSON.stringify`, `Object.keys` and a copy
 * leave behind, so a list built from another starts with a version of its own. An array that stands
 * for a `T[]` behind a face (`fixedSize`) never moves its version: .NET's array has none, and writing
 * an element of one while walking it is allowed.
 */
import { exception, receiver } from './exceptions';
import { isFixedSize } from './view';

const VERSION = Symbol('eq.listVersion');

type Versioned = { [VERSION]?: number };

const MODIFIED = 'Collection was modified; enumeration operation may not execute.';

/** The list's version now: 0 for a list nothing changed yet. */
export function versionOf(list: object): number {
  return (list as Versioned)[VERSION] ?? 0;
}

/** Moves the version, as every change of a list does in .NET. A `T[]` keeps none. */
export function changed(list: object): void {
  if (isFixedSize(list)) return;
  bump(list);
}

/**
 * Moves the version of what is a `List<T>` by its static type, which is never a `T[]` behind a face, so
 * the face is not asked: the helpers below run on every `Add`, and asking cost a third of it (measured).
 */
function bump(list: object): void {
  const version = (list as Versioned)[VERSION];
  if (version !== undefined) (list as Versioned)[VERSION] = version + 1;
  // The first change defines it hidden: an own enumerable symbol is read by a deep comparison, by
  // `Object.assign` and by an object spread, and a list shows nothing but its elements.
  else Object.defineProperty(list, VERSION, { value: 1, writable: true, configurable: true });
}

function modified(): Error {
  return exception('System.InvalidOperationException', MODIFIED);
}

/**
 * The walk `foreach` takes over a `List<T>`: the element at each step, the version compared before it,
 * and once more after the last, as .NET's `MoveNext` compares it, so a change made in the last step's
 * body is refused too.
 */
export function listWalk<T>(list: readonly T[]): IterableIterator<T> {
  receiver(list);
  return new ListWalk(list);
}

/**
 * {@link listWalk}'s iterator, written by hand: a `foreach` over a list is the walk every page takes, and a
 * generator's machinery made it about half as slow again (measured).
 */
class ListWalk<T> implements IterableIterator<T> {
  private index = 0;
  private readonly version: number;

  constructor(private readonly list: readonly T[]) {
    this.version = versionOf(list);
  }

  next(): IteratorResult<T> {
    const list = this.list;
    if (((list as Versioned)[VERSION] ?? 0) !== this.version) throw modified();
    if (this.index >= list.length) return { done: true, value: undefined };
    return { done: false, value: list[this.index++] };
  }

  [Symbol.iterator](): IterableIterator<T> {
    return this;
  }
}

/**
 * {@link listWalk} pushed: each element handed to `sink` until it answers false, with the same checks, for
 * the query that walks a list to its end (`utils/query.ts`).
 */
export function listEach<T>(list: readonly T[], sink: (item: T) => boolean): void {
  const version = versionOf(list);
  for (let index = 0; ; index++) {
    if (((list as Versioned)[VERSION] ?? 0) !== version) throw modified();
    if (index >= list.length) return;
    if (!sink(list[index])) return;
  }
}

/** `List<T>.Add`: the item appended, and the version moved. */
export function listAdd<T>(list: T[], item: T): void {
  receiver(list).push(item);
  bump(list);
}

/** `List<T>.Clear`: every element removed, and the version moved even for an empty list, as .NET's. */
export function listClear<T>(list: T[]): void {
  receiver(list);
  list.length = 0;
  bump(list);
}

/**
 * `List<T>`'s indexer written: the index checked as .NET checks it, the element replaced, and the
 * version moved. It answers the value, as C#'s assignment does. A subscript past the end grew the
 * array with holes where .NET refuses it.
 */
export function listSet<T>(list: T[], index: number, value: T): T {
  receiver(list);
  if (index < 0 || index >= list.length) {
    throw exception(
      'System.ArgumentOutOfRangeException',
      "Index was out of range. Must be non-negative and less than the size of the collection. (Parameter 'index')",
    );
  }
  list[index] = value;
  bump(list);
  return value;
}

/**
 * `List<T>.ForEach(action)`: the action asked of each element in order, and a change it makes to the
 * list refused after it returns, as .NET's loop compares the version once per element and once after
 * the last. It was the array's `forEach`, which walked on over what the action added.
 */
export function listForEach<T>(list: readonly T[], action: ((item: T) => void) | null): void {
  receiver(list);
  if (action == null) throw exception('System.ArgumentNullException', "Value cannot be null. (Parameter 'action')");
  const version = versionOf(list);
  for (let index = 0; index < list.length; index++) {
    if (versionOf(list) !== version) break;
    action(list[index]);
  }
  if (versionOf(list) !== version) throw modified();
}
