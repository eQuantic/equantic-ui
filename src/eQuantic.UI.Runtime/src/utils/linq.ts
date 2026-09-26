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
import { Dictionary, keyText } from './dictionary';
import { compare as compareStrings } from './string-statics';

/**
 * How the values a `Max` or `Min` answers are ordered: `value` by `<` (an integer, a long, a char, a
 * bool), `real` by `<` with .NET's rules for NaN (a double, a float), `text` in the current
 * culture (a string, as `Comparer<string>.Default` orders one), and `comparable` by the value's own
 * `compareTo` (a decimal, a date, a type that implements `IComparable`).
 */
export type Ordering = 'value' | 'real' | 'text' | 'comparable';

const NO_ELEMENTS = 'Sequence contains no elements';

interface Comparable {
  compareTo(other: unknown): number;
}

function comparer(ordering: Ordering): (a: unknown, b: unknown) => number {
  switch (ordering) {
    case 'text':
      return (a, b) => compareStrings(a as string, b as string, 'currentCulture');
    case 'comparable':
      return (a, b) => (a as Comparable).compareTo(b);
    default:
      return (a, b) => ((a as number) < (b as number) ? -1 : (a as number) > (b as number) ? 1 : 0);
  }
}

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
  const compare = comparer(ordering);
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
 * {@link Dictionary}, the one a constructed dictionary is, its keys found by value when `byValue` says
 * so: each element selected before it is added, and a null key or a key twice refused with .NET's
 * words.
 */
export function toDictionary<T, K, V = T>(
  source: Iterable<T>,
  keySelector: (item: T) => K,
  elementSelector?: ((item: T) => V) | null,
  byValue = false,
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
