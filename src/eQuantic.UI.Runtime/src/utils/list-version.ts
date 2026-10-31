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
  (list as Versioned)[VERSION] = versionOf(list) + 1;
}

/**
 * The walk `foreach` takes over a `List<T>`: the element at each step, the version compared before it,
 * and once more after the last, as .NET's `MoveNext` compares it, so a change made in the last step's
 * body is refused too.
 */
export function* listWalk<T>(list: readonly T[]): Generator<T, void, undefined> {
  receiver(list);
  const version = versionOf(list);
  for (let index = 0; ; index++) {
    if (versionOf(list) !== version) throw exception('System.InvalidOperationException', MODIFIED);
    if (index >= list.length) return;
    yield list[index];
  }
}

/** `List<T>.Add`: the item appended, and the version moved. */
export function listAdd<T>(list: T[], item: T): void {
  receiver(list);
  list.push(item);
  changed(list);
}

/** `List<T>.Clear`: every element removed, and the version moved even for an empty list, as .NET's. */
export function listClear<T>(list: T[]): void {
  receiver(list);
  list.length = 0;
  changed(list);
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
  changed(list);
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
  if (versionOf(list) !== version) throw exception('System.InvalidOperationException', MODIFIED);
}
