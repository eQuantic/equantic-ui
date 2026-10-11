/**
 * LINQ's `Max`, `Min` and `ToDictionary`, as .NET answers them.
 *
 * `Math.max(...values)` is not `Max`. An empty sequence of a value type throws in .NET, where
 * `Math.max()` answers -Infinity; a NaN is passed over by `Max` unless every value is one, where
 * `Math.max` lets any NaN win; the first of two equal values is kept, so `Max` of -0 and 0 is -0; a
 * null is passed over, and a type that takes one answers null for a sequence with no value; and
 * text, a long, a decimal or a date do not reduce to a number at all: `Math.max` of two strings is
 * NaN, and of two BigInts a TypeError. The compiler knows the type the call answers and says how it
 * orders ({@link Ordering}) and whether it takes a null; a value alone cannot say either.
 *
 * `Object.fromEntries` is not `ToDictionary`: a key twice overwrote the first, where .NET throws,
 * a null key became the text "null", the key "__proto__" went to the prototype's setter instead of
 * the dictionary, and every record key was the same "[object Object]".
 */
import { Decimal } from './decimal';
import { Dictionary, keyText, type KeyEquality } from './dictionary';
import { exception } from './exceptions';
import { sameBy } from './key-equality';
import { comparerOf, type Ordering } from './ordering';
import { checked } from './overflow';

/**
 * How the values a `Max` or `Min` answers are ordered, the table every ordering of the runtime reads
 * ({@link comparerOf}). A real is the exception here: `Max` and `Min` pass a NaN over by .NET's own
 * rules for them, below, rather than order it first.
 */
export type { Ordering };

const NO_ELEMENTS = 'Sequence contains no elements';
const MORE_THAN_ONE = 'Sequence contains more than one element';
const MORE_THAN_ONE_MATCH = 'Sequence contains more than one matching element';

/** .NET's refusal of a null argument, by the parameter's name. */
function nullArgument(parameter: string): Error {
  return exception('System.ArgumentNullException', `Value cannot be null. (Parameter '${parameter}')`);
}

/**
 * The largest (`direction` 1) or smallest (-1) value, by .NET's `MaxFloat`/`MinFloat` for a real
 * and its `Comparer<T>.Default` loop for the rest: `Min` answers the first NaN it meets and reads no
 * further, as .NET's does. A null source, and then a null selector, are refused by name before either
 * is read, as .NET refuses them (#569); the overload without a selector hands `undefined`.
 */
function extreme<T>(
  source: Iterable<T>,
  selector: ((item: T) => unknown) | null | undefined,
  ordering: Ordering,
  nullable: boolean,
  direction: 1 | -1,
): unknown {
  if (source == null) throw nullArgument('source');
  if (selector === null) throw nullArgument('selector');
  const compare = comparerOf(ordering);
  let found = false;
  let value: unknown = null;
  for (const item of source) {
    const current = selector === undefined ? item : selector(item);
    if (nullable && current == null) continue;
    if (!found) {
      found = true;
      value = current;
      if (ordering === 'real' && direction < 0 && Number.isNaN(current)) return current;
      continue;
    }
    if (ordering === 'real') {
      if (direction > 0) {
        if (Number.isNaN(value) || (current as number) > (value as number)) value = current;
      } else if ((current as number) < (value as number)) {
        value = current;
      } else if (Number.isNaN(current)) {
        return current;
      }
      continue;
    }
    if (compare(current, value) * direction > 0) value = current;
  }
  if (found) return value;
  if (nullable) return null;
  throw exception('System.InvalidOperationException', NO_ELEMENTS);
}

/** `Max()` and `Max(selector)`. */
export function max<T>(
  source: Iterable<T>,
  selector: undefined,
  ordering: Ordering,
  nullable: boolean,
): T;
export function max<T, R>(
  source: Iterable<T>,
  selector: (item: T) => R,
  ordering: Ordering,
  nullable: boolean,
): R;
export function max<T>(
  source: Iterable<T>,
  selector: ((item: T) => unknown) | null | undefined,
  ordering: Ordering,
  nullable: boolean,
): unknown {
  return extreme(source, selector, ordering, nullable, 1);
}

/** `Min()` and `Min(selector)`. */
export function min<T>(
  source: Iterable<T>,
  selector: undefined,
  ordering: Ordering,
  nullable: boolean,
): T;
export function min<T, R>(
  source: Iterable<T>,
  selector: (item: T) => R,
  ordering: Ordering,
  nullable: boolean,
): R;
export function min<T>(
  source: Iterable<T>,
  selector: ((item: T) => unknown) | null | undefined,
  ordering: Ordering,
  nullable: boolean,
): unknown {
  return extreme(source, selector, ordering, nullable, -1);
}

/**
 * How `Average` adds the values it means, which the compiler reads from the type they have: an `int` in
 * .NET's checked long, as a number while the total is a safe integer and as a BigInt past it, so the
 * total stays exact where a double's would round; a `double` as a number; a `float` in a double rounded
 * once at the end, as .NET's `(float)(Sum<float, double> / count)`; a `long` as a BigInt checked at 64
 * bits, as .NET's sum is; and a `decimal` as the runtime's Decimal.
 */
export type Mean = 'int' | 'number' | 'single' | 'long' | 'decimal';

/**
 * An `int` added to a mean's total as .NET adds it, in a checked long: exactly, as a number while the
 * sum is a safe integer, which every page's sequence is, and as a BigInt from the first sum that is not.
 */
function addInt(total: number | bigint, value: number): number | bigint {
  if (typeof total === 'number') {
    const next = total + value;
    if (Number.isSafeInteger(next)) return next;
    total = BigInt(total);
  }
  return checked(total + BigInt(value), 64) as bigint;
}

/**
 * `Average()` and `Average(selector)`, as .NET computes the mean for each element type (#719): a long's
 * total exact and divided as the double C# declares, a nullable's over its values alone and null when it
 * has none, and an empty sequence refused where the answer is not nullable. The reduce it replaces added
 * a BigInt to a number seed wherever the type the compiler read was not the long being added, divided a
 * nullable's total by its nulls too, and answered NaN for an empty sequence. A null source, and then a
 * null selector, are refused by name; the overload without a selector hands `undefined`.
 */
export function average<T>(
  source: Iterable<T>,
  selector: ((item: T) => unknown) | null | undefined,
  mean: 'decimal',
  nullable: false,
): Decimal;
export function average<T>(
  source: Iterable<T>,
  selector: ((item: T) => unknown) | null | undefined,
  mean: 'decimal',
  nullable: true,
): Decimal | null;
export function average<T>(
  source: Iterable<T>,
  selector: ((item: T) => unknown) | null | undefined,
  mean: Exclude<Mean, 'decimal'>,
  nullable: false,
): number;
export function average<T>(
  source: Iterable<T>,
  selector: ((item: T) => unknown) | null | undefined,
  mean: Exclude<Mean, 'decimal'>,
  nullable: true,
): number | null;
export function average<T>(
  source: Iterable<T>,
  selector: ((item: T) => unknown) | null | undefined,
  mean: Mean,
  nullable: boolean,
): number | Decimal | null {
  if (source == null) throw nullArgument('source');
  if (selector === null) throw nullArgument('selector');
  let count = 0;
  let total: number | bigint | Decimal =
    mean === 'long' ? 0n : mean === 'decimal' ? Decimal.from(0) : 0;
  for (const item of source) {
    const value = selector === undefined ? item : selector(item);
    if (nullable && value == null) continue;
    count++;
    if (mean === 'int') total = addInt(total as number | bigint, value as number);
    else if (mean === 'long') total = checked((total as bigint) + (value as bigint), 64) as bigint;
    else if (mean === 'decimal') total = (total as Decimal).add(value as Decimal);
    else total = (total as number) + (value as number);
  }
  if (count === 0) {
    if (nullable) return null;
    throw exception('System.InvalidOperationException', NO_ELEMENTS);
  }
  if (mean === 'decimal') return (total as Decimal).div(Decimal.from(count));
  const quotient = (typeof total === 'bigint' ? Number(total) : (total as number)) / count;
  return mean === 'single' ? Math.fround(quotient) : quotient;
}

/**
 * `FirstOrDefault(defaultValue)` and `FirstOrDefault(predicate, defaultValue)`, .NET 6's overloads: the
 * first element that matches, a null one included, or the value handed (#720). They were lowered as the
 * overloads with a predicate, so the default value was called as a function. A null source, and then a
 * null predicate, are refused by name; the overload without a predicate hands `undefined`.
 */
export function firstOrDefault<T>(
  source: Iterable<T>,
  predicate: ((item: T) => boolean) | null | undefined,
  defaultValue: T,
): T {
  if (source == null) throw nullArgument('source');
  if (predicate === null) throw nullArgument('predicate');
  for (const item of source) {
    if (predicate === undefined || predicate(item)) return item;
  }
  return defaultValue;
}

/**
 * `LastOrDefault(defaultValue)` and `LastOrDefault(predicate, defaultValue)`: the last element that
 * matches, or the value handed (#720). An array is searched from its end, as .NET searches an
 * `IList<T>`, so a predicate runs only until it matches; any other iterable is walked whole. The
 * compiler hands every source as an array, so a set or a queue is searched from its end too, and a
 * predicate with side effects sees fewer of its elements than .NET's, which walks it whole: a
 * sequence's laziness is #685's decision.
 */
export function lastOrDefault<T>(
  source: Iterable<T>,
  predicate: ((item: T) => boolean) | null | undefined,
  defaultValue: T,
): T {
  if (source == null) throw nullArgument('source');
  if (predicate === null) throw nullArgument('predicate');
  if (Array.isArray(source)) {
    for (let index = source.length - 1; index >= 0; index--) {
      if (predicate === undefined || predicate(source[index])) return source[index];
    }
    return defaultValue;
  }
  let found = false;
  let last = defaultValue;
  for (const item of source) {
    if (predicate === undefined || predicate(item)) {
      found = true;
      last = item;
    }
  }
  return found ? last : defaultValue;
}

/**
 * `SingleOrDefault(defaultValue)` and `SingleOrDefault(predicate, defaultValue)`, .NET 6's overloads:
 * the one element, or the one that matches, a null one included, or the value handed where there is
 * none (#760). A second element is refused, and so is a second match, the predicate having run on
 * every element up to it, as .NET's walk does. They were lowered as the overloads without a default
 * value, which called it as a predicate. A null source, and then a null predicate, are refused by name.
 */
export function singleOrDefault<T>(
  source: Iterable<T>,
  predicate: ((item: T) => boolean) | null | undefined,
  defaultValue: T,
): T {
  if (source == null) throw nullArgument('source');
  if (predicate === null) throw nullArgument('predicate');
  let found = false;
  let single = defaultValue;
  for (const item of source) {
    if (predicate !== undefined && !predicate(item)) continue;
    if (found) {
      throw exception(
        'System.InvalidOperationException',
        predicate === undefined ? MORE_THAN_ONE : MORE_THAN_ONE_MATCH,
      );
    }
    found = true;
    single = item;
  }
  return single;
}

/**
 * `SequenceEqual(second)` and `SequenceEqual(second, comparer)`, element by element by the element
 * type's default equality (`equality`, {@link KeyEquality}): a record and a decimal by value, a class
 * by its own `Equals` and by reference where it has none, an array by reference, a NaN equal to a NaN
 * (#721). The text of each sequence's JSON was compared
 * instead, which threw for a long, told `1.0m` from `1.00m`, took a null for a NaN and two arrays of
 * the same items for one, and the overload with a comparer answered its first sequence. A comparer that
 * reaches here asks for that equality; the compiler refuses any other. Two lists of different lengths
 * are told apart before either is read, as .NET tells two collections. With `spans`, the call is the one
 * an array binds through `MemoryExtensions`, where a null array is an empty span; otherwise a null
 * sequence is refused by name.
 */
export function sequenceEqual<T>(
  first: Iterable<T> | null,
  second: Iterable<T> | null,
  equality: KeyEquality = false,
  spans = false,
): boolean {
  if (spans) {
    first ??= [];
    second ??= [];
  }
  if (first == null) throw nullArgument('first');
  if (second == null) throw nullArgument('second');
  if (Array.isArray(first) && Array.isArray(second) && first.length !== second.length) return false;
  const same = sameBy(equality);
  const left = first[Symbol.iterator]();
  const right = second[Symbol.iterator]();
  for (;;) {
    const a = left.next();
    const b = right.next();
    if (a.done || b.done) return a.done === true && b.done === true;
    if (!same(a.value, b.value)) return false;
  }
}

/**
 * `Zip(second)` and `Zip(second, third)`: the pairs and the triples .NET answers, value tuples held as
 * arrays here, as many as the shortest sequence holds (#722). The three sequences were lowered as the
 * Zip with a result selector, which called the third as a function, and the pair form answered its
 * first sequence. Each sequence is refused by its name when null; the pair form hands no `third`.
 */
export function zipTuples<A, B>(
  first: Iterable<A>,
  second: Iterable<B>,
  third?: undefined,
): [A, B][];
export function zipTuples<A, B, C>(
  first: Iterable<A>,
  second: Iterable<B>,
  third: Iterable<C> | null,
): [A, B, C][];
export function zipTuples(
  first: Iterable<unknown>,
  second: Iterable<unknown>,
  third?: Iterable<unknown> | null,
): unknown[][] {
  if (first == null) throw nullArgument('first');
  if (second == null) throw nullArgument('second');
  if (third === null) throw nullArgument('third');
  const sequences = third === undefined ? [first, second] : [first, second, third];
  const walkers = sequences.map((sequence) => sequence[Symbol.iterator]());
  const tuples: unknown[][] = [];
  for (;;) {
    const tuple: unknown[] = [];
    for (const walker of walkers) {
      const step = walker.next();
      if (step.done) return tuples;
      tuple.push(step.value);
    }
    tuples.push(tuple);
  }
}

/**
 * `ToDictionary(keySelector)` and `ToDictionary(keySelector, elementSelector)` into the runtime's
 * {@link Dictionary}, the one a constructed dictionary is, its keys found as `byValue` says
 * ({@link KeyEquality}): each element selected before it is added, and a null key or a key twice refused with .NET's
 * words. A null source or key selector is refused by name, as .NET refuses it (#569). A null element
 * selector is read as none: the lowering writes null for the overload without one.
 */
export function toDictionary<T, K, V = T>(
  source: Iterable<T>,
  keySelector: (item: T) => K,
  elementSelector?: ((item: T) => V) | null,
  byValue: KeyEquality = false,
): Dictionary<K, V> {
  if (source == null) throw nullArgument('source');
  if (keySelector == null) throw nullArgument('keySelector');
  const result = new Dictionary<K, V>(null, byValue);
  for (const item of source) {
    const key = keySelector(item);
    const value = elementSelector == null ? (item as unknown as V) : elementSelector(item);
    if (key == null) throw exception('System.ArgumentNullException', "Value cannot be null. (Parameter 'key')");
    if (result.has(key)) {
      throw exception(
        'System.ArgumentException',
        `An item with the same key has already been added. Key: ${keyText(key)}`,
      );
    }
    result.set(key, value);
  }
  return result;
}

/**
 * A sequence as C# enumerates it, as the array the lowered operators call array methods on: an array
 * as it is, a string by its chars (UTF-16 code units, where a spread gives code points), and anything
 * else by its own iterator (a `Set`, a dictionary's pairs, the runtime's sorted set, queue, stack and
 * linked list). LINQ over any of these called an array method the receiver does not have, and threw.
 * `parameter` is the name the operator gives the sequence, which a null one is refused by: `source`
 * for most, `second` for the other side of a `Concat`, `first` for a `Zip`'s own (#569).
 */
export function seq<T>(source: Iterable<T> | string, parameter = 'source'): T[] {
  if (Array.isArray(source)) return source;
  if (typeof source === 'string') return source.split('') as unknown as T[];
  // A null source is .NET's ArgumentNullException, every LINQ operator's first check.
  if (source == null) throw nullArgument(parameter);
  // Array.from reads an object that is not iterable as an EMPTY array: a value that crossed as a plain
  // object where C# holds a sequence would count nothing, in silence. It says so instead.
  if (typeof (source as { [Symbol.iterator]?: unknown })[Symbol.iterator] !== 'function') {
    throw new TypeError(`A sequence was expected: ${Object.prototype.toString.call(source)}`);
  }
  return Array.from(source);
}

/**
 * A sequence as a `foreach` enumerates it, when its static type does not say whether it is a string
 * (`object`, `IEnumerable`, `IEnumerable<char>`): a string by its chars, the UTF-16 code units, and
 * anything else as it is, by its own iterator, which a dictionary's keeps watching for changes.
 * JavaScript iterates a string by code point, so a pair behind an interface was one char.
 */
export function enumerable<T>(source: Iterable<T> | string): Iterable<T> {
  return typeof source === 'string' ? (source.split('') as unknown as T[]) : source;
}

/** An enumerator the app wrote: its twin's `MoveNext`, `Current` and, where it has one, `Dispose`. */
interface Enumerator<T> {
  moveNext(): boolean;
  readonly current: T;
  dispose?(): void;
}

/**
 * The iteration of a twin whose type implements `IEnumerable<T>` (#612): its `[Symbol.iterator]`
 * hands this the enumerator the type's own `GetEnumerator()` returned, so a `for…of`, a spread and
 * every sequence helper here walk the class as a `foreach` walks it, and so does a `foreach` that
 * calls the class's own public `GetEnumerator()` beside the interface's, as C# binds it. An
 * enumerator the app wrote is walked by its `MoveNext` and `Current`, and disposed when the walk
 * ends, however it ends, as a `foreach` disposes it, even where its class is a sequence too
 * (`GetEnumerator() => this`), whose own iteration would hand it back here without end. An
 * enumerator an iterator method filled, or a sequence's own, is iterated as it is. A null one is
 * .NET's NullReferenceException, which `MoveNext` on it throws.
 */
export function iterate<T>(enumerator: Iterable<T> | Enumerator<T>): IterableIterator<T> {
  if (enumerator == null) {
    throw exception(
      'System.NullReferenceException',
      'Object reference not set to an instance of an object.',
    );
  }
  if (typeof (enumerator as Partial<Enumerator<T>>).moveNext === 'function') return walk(enumerator as Enumerator<T>);
  const own = (enumerator as Partial<Iterable<T>>)[Symbol.iterator];
  // Every iterator the platform and the runtime hand out is iterable itself, so a foreach that calls
  // the GetEnumerator() it binds walks what this answers as it is.
  return typeof own === 'function' ? (own.call(enumerator) as IterableIterator<T>) : walk(enumerator as Enumerator<T>);
}

function* walk<T>(enumerator: Enumerator<T>): Generator<T> {
  try {
    while (enumerator.moveNext()) yield enumerator.current;
  } finally {
    enumerator.dispose?.();
  }
}

/**
 * A NEW array of a sequence's elements, as `ToList` and `ToArray` make one: an array is copied too, so
 * the copy and its source are two arrays, as in .NET, whatever the static type hid it behind.
 */
export function toArray<T>(source: Iterable<T> | string): T[] {
  return Array.isArray(source) ? source.slice() : seq(source);
}
