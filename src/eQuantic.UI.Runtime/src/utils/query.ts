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
 */
import { exception } from './exceptions';
import { listWalk } from './list-version';
import { isFixedSize } from './view';

/** .NET's refusal of a null argument, by the parameter's name. */
function nullArgument(parameter: string): Error {
  return exception('System.ArgumentNullException', `Value cannot be null. (Parameter '${parameter}')`);
}

/** A sequence a deferred operator answers: walked each time it is enumerated, as .NET's iterators are. */
export class Query<T> implements Iterable<T> {
  /**
   * @param open the walk, started anew each time the query is enumerated.
   * @param count how many elements it holds where that is known without walking it, as .NET's
   * iterators answer `GetCount(onlyIfCheap: true)`: a projection of a list counts the list and calls
   * no selector.
   */
  constructor(
    private readonly open: () => Iterator<T>,
    readonly count?: () => number | undefined,
  ) {}

  [Symbol.iterator](): Iterator<T> {
    return this.open();
  }

  /** The array of its elements, as System.Text.Json writes an `IEnumerable<T>`. */
  toJSON(): T[] {
    return Array.from(this);
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
 * How many elements a source holds where that is known without walking it, as .NET reads it: a list's
 * or an array's length, and a query's own cheap count; undefined for anything that would have to be
 * walked.
 */
export function cheapCount(source: unknown): number | undefined {
  if (Array.isArray(source)) return source.length;
  if (source instanceof Query) return source.count?.();
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
  return new Query(function* () {
    let index = 0;
    for (const item of walk(from)) if (test(item, index++)) yield item;
  });
}

/** `Select(selector)`, the index overload included. */
export function select<T, R>(source: Iterable<T> | string, selector: (item: T, index: number) => R): Query<R> {
  const from = checked(source);
  const project = required(selector, 'selector');
  return new Query(
    function* () {
      let index = 0;
      for (const item of walk(from)) yield project(item, index++);
    },
    () => cheapCount(from),
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
  return new Query(function* () {
    let index = 0;
    for (const item of walk(from)) {
      for (const inner of walk(many(item, index++), 'collection')) {
        yield resultSelector === undefined ? (inner as unknown as R) : resultSelector(item, inner);
      }
    }
  });
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
    () => {
      const first = cheapCount(a);
      const second = cheapCount(b);
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
    () => {
      const count = cheapCount(from);
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
    () => {
      const count = cheapCount(from);
      return count === undefined ? undefined : count + 1;
    },
  );
}

/** `TakeWhile(predicate)`, the index overload included. */
export function takeWhile<T>(source: Iterable<T> | string, predicate: (item: T, index: number) => boolean): Query<T> {
  const from = checked(source);
  const test = required(predicate, 'predicate');
  return new Query(function* () {
    let index = 0;
    for (const item of walk(from)) {
      if (!test(item, index++)) return;
      yield item;
    }
  });
}

/** `SkipWhile(predicate)`, the index overload included. */
export function skipWhile<T>(source: Iterable<T> | string, predicate: (item: T, index: number) => boolean): Query<T> {
  const from = checked(source);
  const test = required(predicate, 'predicate');
  return new Query(function* () {
    let index = 0;
    let skipping = true;
    for (const item of walk(from)) {
      if (skipping && test(item, index++)) continue;
      skipping = false;
      yield item;
    }
  });
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
    const count = cheapCount(from);
    if (count !== undefined) return count !== 0;
  }
  for (const item of walk(from)) if (predicate === undefined || predicate(item)) return true;
  return false;
}

/** `All(predicate)`: the walk stops at the first element that fails. */
export function all<T>(source: Iterable<T> | string, predicate: (item: T) => boolean): boolean {
  const from = checked(source);
  const test = required(predicate, 'predicate');
  for (const item of walk(from)) if (!test(item)) return false;
  return true;
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
  return new Query(() => build(Array.from(walk(from, parameter)))[Symbol.iterator]());
}
