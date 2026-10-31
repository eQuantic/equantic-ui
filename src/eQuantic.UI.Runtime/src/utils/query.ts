/**
 * LINQ's deferred operators, run as .NET runs them (#685): an operator checks its arguments when it is
 * called and reads nothing, and its sequence reads the source one element at a time, each element only
 * after the callback before it returned, so a callback that changes its source changes what is read
 * next, and a list changed under the walk ends it with .NET's InvalidOperationException. Every operator
 * was an array method over a copy of the source, read whole before the first callback ran.
 *
 * A {@link Query} is what a deferred operator answers. It is iterable, so a `foreach`, a spread, another
 * operator and every helper that takes a sequence walk it, and it writes itself to JSON as the array of
 * its elements, as System.Text.Json writes an `IEnumerable<T>`.
 *
 * Each operator walks two ways, by the same steps: pulled, by a generator, for a `foreach` and every
 * consumer that takes an iterator, and pushed ({@link Query.each}), each element handed down the chain to
 * a consumer that reads to the end or to an answer: `ToList`, `Count`, `Any`, `First` and the others. A
 * generator per stage cost a chain five to nine times what the array methods cost (measured); a push
 * costs a call per element and stage.
 */
import { count as collectionCount, LinkedList, Queue, Stack } from './collections';
import { Dictionary } from './dictionary';
import { exception } from './exceptions';
import { HashSet } from './hash-set';
import { listEach, listWalk } from './list-version';
import { SortedMap, SortedSet } from './sorted';
import { isFixedSize } from './view';

/** .NET's refusal of a null argument, by the parameter's name. */
function nullArgument(parameter: string): Error {
  return exception('System.ArgumentNullException', `Value cannot be null. (Parameter '${parameter}')`);
}

/** One step of a pushed walk: an element handed to its consumer, which answers whether the walk goes on. */
export type Sink<T> = (item: T) => boolean;

/** A sequence a deferred operator answers: walked each time it is enumerated, as .NET's iterators are. */
export class Query<T> implements Iterable<T> {
  /**
   * @param open the walk, started anew each time the query is enumerated.
   * @param each the same walk pushed: each element handed to the sink until it answers false.
   * @param count how many elements it holds where that is known without walking it, as .NET's
   * iterators answer `GetCount(onlyIfCheap: true)`: a projection of a list counts the list and calls
   * no selector.
   */
  constructor(
    private readonly open: () => Iterator<T>,
    readonly each: (sink: Sink<T>) => void,
    readonly count?: () => number | undefined,
    /** The element at an index where it is read without walking, as .NET's iterators over a list answer
     *  `TryGetElementAt`, `TryGetFirst` and `TryGetLast`: a projection of a list runs its selector on that
     *  element alone. Undefined where the query has no such access. */
    readonly at?: (index: number) => { value: T } | undefined,
  ) {}

  [Symbol.iterator](): Iterator<T> {
    return this.open();
  }

  /** Its elements in a new array, the query walked once, pushed. */
  toArray(): T[] {
    const items: T[] = [];
    this.each((item) => {
      items.push(item);
      return true;
    });
    return items;
  }

  /** The array of its elements, as System.Text.Json writes an `IEnumerable<T>`. */
  toJSON(): T[] {
    return this.toArray();
  }
}

/**
 * A source as C# enumerates it: a list through its version ({@link listWalk}), an array and every other
 * collection by its own iterator, a string by its chars, the UTF-16 code units, and a null refused by
 * the parameter's name. Read when the walk starts, never before.
 */
export function walk<T>(source: Iterable<T> | string | null | undefined, parameter = 'source'): Iterable<T> {
  if (source == null) throw nullArgument(parameter);
  if (typeof source === 'string') return source.split('') as unknown as T[];
  if (Array.isArray(source)) return isFixedSize(source) ? source : listWalk(source);
  if (typeof (source as { [Symbol.iterator]?: unknown })[Symbol.iterator] !== 'function') {
    throw new TypeError(`A sequence was expected: ${Object.prototype.toString.call(source)}`);
  }
  return source;
}

/**
 * {@link walk} pushed: each element of a source handed to `sink` until it answers false, in the same order
 * and with the same checks, a query by its own push and a list through its version.
 */
export function eachOf<T>(source: Iterable<T> | string | null | undefined, sink: Sink<T>, parameter = 'source'): void {
  if (source instanceof Query) {
    source.each(sink);
    return;
  }
  if (source == null) throw nullArgument(parameter);
  if (typeof source === 'string' || (Array.isArray(source) && isFixedSize(source))) {
    const items = source as ArrayLike<T>;
    for (let index = 0; index < items.length; index++) if (!sink(items[index])) return;
    return;
  }
  if (Array.isArray(source)) {
    listEach(source as T[], sink);
    return;
  }
  for (const item of walk(source, parameter)) if (!sink(item)) return;
}

/** The elements of a source in a new array, pushed. */
function arrayOf<T>(source: Iterable<T> | string, parameter = 'source'): T[] {
  const items: T[] = [];
  eachOf(
    source,
    (item) => {
      items.push(item);
      return true;
    },
    parameter,
  );
  return items;
}

/**
 * How many elements a source holds where that is known without walking it, as .NET reads it: a list's
 * or an array's length, and a query's own cheap count; undefined for anything that would have to be
 * walked.
 */
export function cheapCount(source: unknown): number | undefined {
  if (Array.isArray(source)) return source.length;
  if (source instanceof Query) return source.count?.();
  return undefined;
}

/**
 * How many elements a source holds where .NET's `TryGetNonEnumeratedCount` knows it: what
 * {@link cheapCount} knows, and an `ICollection<T>`'s `Count`, which the runtime's dictionary, set,
 * sorted set and its views, sorted dictionary, queue, stack and linked list answer, and a JavaScript `Set`
 * or `Map` its size. `Any()` and the counts of `Concat`, `Append` and `Prepend` ask it, as .NET's do: a
 * sorted set's view counts itself again when asked, and its `ExceptWith` reads that count.
 */
function nonEnumeratedCount(source: unknown): number | undefined {
  const cheap = cheapCount(source);
  if (cheap !== undefined) return cheap;
  if (source instanceof Set || source instanceof Map) return source.size;
  if (
    source instanceof HashSet ||
    source instanceof Dictionary ||
    source instanceof SortedSet ||
    source instanceof SortedMap ||
    source instanceof Queue ||
    source instanceof Stack ||
    source instanceof LinkedList
  ) {
    return collectionCount(source);
  }
  return undefined;
}

/** The element at an index of a source read without walking it: a list's, an array's and a query's own. */
function randomAccess<T>(source: unknown): ((index: number) => { value: T } | undefined) | undefined {
  if (Array.isArray(source)) {
    return (index) => (index >= 0 && index < source.length ? { value: source[index] as T } : undefined);
  }
  if (source instanceof Query) return source.at as ((index: number) => { value: T } | undefined) | undefined;
  return undefined;
}

/** A source checked now, walked later: what every deferred operator holds. */
function checked<T>(source: Iterable<T> | string | null | undefined, parameter = 'source'): Iterable<T> | string {
  if (source == null) throw nullArgument(parameter);
  return source;
}

/** A callback checked now, as .NET checks its arguments when the operator is called. */
function required<F>(callback: F | null | undefined, parameter: string): F {
  if (callback == null) throw nullArgument(parameter);
  return callback;
}

/** `Where(predicate)`, the index overload included. */
export function where<T>(source: Iterable<T> | string, predicate: (item: T, index: number) => boolean): Query<T> {
  const from = checked(source);
  const test = required(predicate, 'predicate');
  return new Query(
    function* () {
      let index = 0;
      for (const item of walk(from)) if (test(item, index++)) yield item;
    },
    (sink) => {
      let index = 0;
      eachOf(from, (item) => !test(item, index++) || sink(item));
    },
  );
}

/** `Select(selector)`, the index overload included. */
export function select<T, R>(source: Iterable<T> | string, selector: (item: T, index: number) => R): Query<R> {
  const from = checked(source);
  const project = required(selector, 'selector');
  const read = randomAccess<T>(from);
  return new Query(
    function* () {
      let index = 0;
      for (const item of walk(from)) yield project(item, index++);
    },
    (sink) => {
      let index = 0;
      eachOf(from, (item) => sink(project(item, index++)));
    },
    () => cheapCount(from),
    read === undefined
      ? undefined
      : (index) => {
          const found = read(index);
          return found === undefined ? undefined : { value: project(found.value, index) };
        },
  );
}

/** `SelectMany(collectionSelector)` and `SelectMany(collectionSelector, resultSelector)`. */
export function selectMany<T, C, R = C>(
  source: Iterable<T> | string,
  collectionSelector: (item: T, index: number) => Iterable<C> | string,
  resultSelector?: (item: T, inner: C) => R,
): Query<R> {
  const from = checked(source);
  const many = required(collectionSelector, 'collectionSelector');
  if (resultSelector === null) throw nullArgument('resultSelector');
  return new Query(
    function* () {
      let index = 0;
      for (const item of walk(from)) {
        for (const inner of walk(many(item, index++), 'collection')) {
          yield resultSelector === undefined ? (inner as unknown as R) : resultSelector(item, inner);
        }
      }
    },
    (sink) => {
      let index = 0;
      eachOf(from, (item) => {
        let going = true;
        eachOf(
          many(item, index++),
          (inner) => (going = sink(resultSelector === undefined ? (inner as unknown as R) : resultSelector(item, inner))),
          'collection',
        );
        return going;
      });
    },
  );
}

/** `Concat(second)`. */
export function concat<T>(first: Iterable<T> | string, second: Iterable<T> | string): Query<T> {
  const a = checked(first, 'first');
  const b = checked(second, 'second');
  return new Query(
    function* () {
      yield* walk(a, 'first');
      yield* walk(b, 'second');
    },
    (sink) => {
      let going = true;
      eachOf(a, (item) => (going = sink(item)), 'first');
      if (going) eachOf(b, sink, 'second');
    },
    () => {
      const first = nonEnumeratedCount(a);
      const second = nonEnumeratedCount(b);
      return first === undefined || second === undefined ? undefined : first + second;
    },
  );
}

/** `Append(element)`. */
export function append<T>(source: Iterable<T> | string, element: T): Query<T> {
  const from = checked(source);
  return new Query(
    function* () {
      yield* walk(from);
      yield element;
    },
    (sink) => {
      let going = true;
      eachOf(from, (item) => (going = sink(item)));
      if (going) sink(element);
    },
    () => {
      const count = nonEnumeratedCount(from);
      return count === undefined ? undefined : count + 1;
    },
  );
}

/** `Prepend(element)`. */
export function prepend<T>(source: Iterable<T> | string, element: T): Query<T> {
  const from = checked(source);
  return new Query(
    function* () {
      yield element;
      yield* walk(from);
    },
    (sink) => {
      if (sink(element)) eachOf(from, sink);
    },
    () => {
      const count = nonEnumeratedCount(from);
      return count === undefined ? undefined : count + 1;
    },
  );
}

/** `TakeWhile(predicate)`, the index overload included. */
export function takeWhile<T>(source: Iterable<T> | string, predicate: (item: T, index: number) => boolean): Query<T> {
  const from = checked(source);
  const test = required(predicate, 'predicate');
  return new Query(
    function* () {
      let index = 0;
      for (const item of walk(from)) {
        if (!test(item, index++)) return;
        yield item;
      }
    },
    (sink) => {
      let index = 0;
      eachOf(from, (item) => test(item, index++) && sink(item));
    },
  );
}

/** `SkipWhile(predicate)`, the index overload included. */
export function skipWhile<T>(source: Iterable<T> | string, predicate: (item: T, index: number) => boolean): Query<T> {
  const from = checked(source);
  const test = required(predicate, 'predicate');
  return new Query(
    function* () {
      let index = 0;
      let skipping = true;
      for (const item of walk(from)) {
        if (skipping && test(item, index++)) continue;
        skipping = false;
        yield item;
      }
    },
    (sink) => {
      let index = 0;
      let skipping = true;
      eachOf(from, (item) => {
        if (skipping && test(item, index++)) return true;
        skipping = false;
        return sink(item);
      });
    },
  );
}

/** `AsEnumerable()`: the source itself, as .NET answers it. */
export function asEnumerable<T>(source: Iterable<T> | string): Iterable<T> | string {
  return source;
}

/**
 * `Any()` and `Any(predicate)`: the walk stops at the first element that answers, as .NET's does, so a
 * callback upstream runs only as far as that.
 */
export function any<T>(source: Iterable<T> | string, predicate?: (item: T) => boolean): boolean {
  const from = checked(source);
  if (predicate === null) throw nullArgument('predicate');
  // Without a predicate, a count known without walking answers, and no selector upstream runs.
  if (predicate === undefined) {
    const count = nonEnumeratedCount(from);
    if (count !== undefined) return count !== 0;
  }
  let found = false;
  eachOf(from, (item) => !(found = predicate === undefined || predicate(item)));
  return found;
}

/** `All(predicate)`: the walk stops at the first element that fails. */
export function all<T>(source: Iterable<T> | string, predicate: (item: T) => boolean): boolean {
  const from = checked(source);
  const test = required(predicate, 'predicate');
  let holds = true;
  eachOf(from, (item) => (holds = test(item)));
  return holds;
}

/** `Skip(count)`: a negative count skips nothing, as .NET's does, where a slice counted it from the end. */
export function skip<T>(source: Iterable<T> | string, count: number): Query<T> {
  const from = checked(source);
  return new Query(
    function* () {
      let left = count;
      for (const item of walk(from)) {
        if (left > 0) {
          left--;
          continue;
        }
        yield item;
      }
    },
    (sink) => {
      let left = count;
      eachOf(from, (item) => {
        if (left > 0) {
          left--;
          return true;
        }
        return sink(item);
      });
    },
    () => {
      const total = cheapCount(from);
      return total === undefined ? undefined : Math.max(0, total - Math.max(0, count));
    },
  );
}

/** `Take(count)`: the walk stops once it has the count, and a negative count takes nothing. */
export function take<T>(source: Iterable<T> | string, count: number): Query<T> {
  const from = checked(source);
  return new Query(
    function* () {
      if (count <= 0) return;
      let left = count;
      for (const item of walk(from)) {
        yield item;
        if (--left === 0) return;
      }
    },
    (sink) => {
      if (count <= 0) return;
      let left = count;
      eachOf(from, (item) => sink(item) && --left > 0);
    },
    () => {
      const total = cheapCount(from);
      return total === undefined ? undefined : Math.min(total, Math.max(0, count));
    },
  );
}

/**
 * An operator the runtime has no walk of its own for, run when its query is walked rather than when it is
 * called, as .NET runs a deferred operator: `build` reads the source, as an array, once per walk. Its
 * other arguments were evaluated when it was called, as C# evaluates them.
 */
export function defer<S, R>(build: (source: S[]) => Iterable<R>, source: Iterable<S> | string, parameter = 'source'): Query<R> {
  const from = checked(source, parameter);
  return new Query(
    () => build(arrayOf(from, parameter))[Symbol.iterator](),
    (sink) => {
      for (const item of build(arrayOf(from, parameter))) if (!sink(item)) return;
    },
  );
}

/**
 * `First()` and `First(predicate)` over a query: the walk stops at the first element that answers, so a
 * callback upstream runs only as far as that, and an empty walk throws .NET's InvalidOperationException.
 */
export function first<T>(source: Iterable<T> | string, predicate?: (item: T) => boolean): T {
  const from = checked(source);
  if (predicate === null) throw nullArgument('predicate');
  if (predicate === undefined) {
    const found = randomAccess<T>(from)?.(0);
    if (found !== undefined) return found.value;
    if (cheapCount(from) === 0) throw exception('System.InvalidOperationException', 'Sequence contains no elements');
  }
  const found = firstOf(from, predicate);
  if (found !== undefined) return found.value;
  throw exception(
    'System.InvalidOperationException',
    predicate === undefined ? 'Sequence contains no elements' : 'Sequence contains no matching element',
  );
}

/** `FirstOrDefault()` and `FirstOrDefault(predicate)` over a query, the element type's default for none. */
export function firstOrDefault<T>(source: Iterable<T> | string, predicate: ((item: T) => boolean) | undefined, fallback: T): T {
  const from = checked(source);
  if (predicate === null) throw nullArgument('predicate');
  if (predicate === undefined) {
    const found = randomAccess<T>(from)?.(0);
    if (found !== undefined) return found.value;
    if (cheapCount(from) === 0) return fallback;
  }
  const found = firstOf(from, predicate);
  return found === undefined ? fallback : found.value;
}

/** The first element that answers, the walk stopped there; undefined for none. */
function firstOf<T>(source: Iterable<T> | string, predicate: ((item: T) => boolean) | undefined): { value: T } | undefined {
  let found: { value: T } | undefined;
  eachOf(source, (item) => {
    if (predicate !== undefined && !predicate(item)) return true;
    found = { value: item };
    return false;
  });
  return found;
}

/**
 * `ElementAt(index)` and `ElementAtOrDefault(index)` over a query: the walk stops at the element, and an
 * index outside the sequence throws .NET's ArgumentOutOfRangeException, or answers the default.
 */
export function elementAt<T>(source: Iterable<T> | string, index: number, fallback?: { value: T }): T {
  const from = checked(source);
  const read = randomAccess<T>(from);
  if (read !== undefined) {
    const found = read(index);
    if (found !== undefined) return found.value;
  } else if (index >= 0) {
    let at = 0;
    let found: { value: T } | undefined;
    eachOf(from, (item) => {
      if (at++ !== index) return true;
      found = { value: item };
      return false;
    });
    if (found !== undefined) return found.value;
  }
  if (fallback !== undefined) return fallback.value;
  // An index outside a walked sequence is .NET's generic out-of-range, which a list's own indexer words otherwise.
  throw exception('System.ArgumentOutOfRangeException', "Specified argument was out of the range of valid values. (Parameter 'index')");
}

/**
 * `Last()` and `Last(predicate)` over a query: a projection of a list reads its last element alone, as
 * .NET's `TryGetLast` does, and anything else is walked whole, the last element that answers kept.
 */
export function last<T>(source: Iterable<T> | string, predicate?: (item: T) => boolean, fallback?: { value: T }): T {
  const from = checked(source);
  if (predicate === null) throw nullArgument('predicate');
  if (predicate === undefined) {
    const count = cheapCount(from);
    const read = randomAccess<T>(from);
    if (count !== undefined && read !== undefined) {
      const found = count > 0 ? read(count - 1) : undefined;
      if (found !== undefined) return found.value;
      if (fallback !== undefined) return fallback.value;
      throw exception('System.InvalidOperationException', 'Sequence contains no elements');
    }
  }
  let found: { value: T } | undefined;
  eachOf(from, (item) => {
    if (predicate === undefined || predicate(item)) found = { value: item };
    return true;
  });
  if (found !== undefined) return found.value;
  if (fallback !== undefined) return fallback.value;
  throw exception(
    'System.InvalidOperationException',
    predicate === undefined ? 'Sequence contains no elements' : 'Sequence contains no matching element',
  );
}
