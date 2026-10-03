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
import { Dictionary, keyText, type KeyEquality } from './dictionary';
import { comparerOf, type Ordering } from './ordering';

/**
 * How the values a `Max` or `Min` answers are ordered, the table every ordering of the runtime reads
 * ({@link comparerOf}). A real is the exception here: `Max` and `Min` pass a NaN over by .NET's own
 * rules for them, below, rather than order it first.
 */
export type { Ordering };

const NO_ELEMENTS = 'Sequence contains no elements';

/**
 * The largest (`direction` 1) or smallest (-1) value, by .NET's `MaxFloat`/`MinFloat` for a real
 * and its `Comparer<T>.Default` loop for the rest: `Min` answers the first NaN it meets and reads no
 * further, as .NET's does.
 */
function extreme<T>(
  source: Iterable<T>,
  selector: ((item: T) => unknown) | undefined,
  ordering: Ordering,
  nullable: boolean,
  direction: 1 | -1,
): unknown {
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
  throw new Error(NO_ELEMENTS);
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
  selector: ((item: T) => unknown) | undefined,
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
  selector: ((item: T) => unknown) | undefined,
  ordering: Ordering,
  nullable: boolean,
): unknown {
  return extreme(source, selector, ordering, nullable, -1);
}

/**
 * `ToDictionary(keySelector)` and `ToDictionary(keySelector, elementSelector)` into the runtime's
 * {@link Dictionary}, the one a constructed dictionary is, its keys found as `byValue` says
 * ({@link KeyEquality}): each element selected before it is added, and a null key or a key twice refused with .NET's
 * words.
 */
export function toDictionary<T, K, V = T>(
  source: Iterable<T>,
  keySelector: (item: T) => K,
  elementSelector?: ((item: T) => V) | null,
  byValue: KeyEquality = false,
): Dictionary<K, V> {
  const result = new Dictionary<K, V>(null, byValue);
  for (const item of source) {
    const key = keySelector(item);
    const value = elementSelector == null ? (item as unknown as V) : elementSelector(item);
    if (key == null) throw new Error("Value cannot be null. (Parameter 'key')");
    if (result.has(key)) {
      throw new Error(`An item with the same key has already been added. Key: ${keyText(key)}`);
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
 */
export function seq<T>(source: Iterable<T> | string): T[] {
  if (Array.isArray(source)) return source;
  if (typeof source === 'string') return source.split('') as unknown as T[];
  return Array.from(source);
}
