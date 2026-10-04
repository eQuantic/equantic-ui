/**
 * A string's own methods that take a `StringComparison` (`StartsWith`, `EndsWith`, `IndexOf` and
 * `LastIndexOf` with their start and count, `Contains`, `Replace` and `Equals`) and `CompareTo`, as
 * .NET 10 answers them. The comparison crosses as its member's name, so one held in a variable is
 * the value it holds, and it is checked as .NET checks it.
 *
 * An ORDINAL search compares code units. One that IGNORES CASE is .NET's `Ordinal` and
 * `OrdinalCasing`, step for step: a value whose first unit is ASCII is matched where the whole value
 * equals the window by `CompareStringIgnoreCase` (string-statics' `ordinalIgnoreCase`), and any other
 * value unit by unit, a well-formed pair by its code point's upper case and a lone half by itself. So
 * the Kelvin sign is not a k, and half of a pair can be found inside one.
 *
 * A CULTURE search has no JavaScript form. .NET searches with ICU's collation behind an ASCII fast
 * path, and the platform's collator compares two whole strings and searches nothing: a search built
 * from it on grapheme boundaries answers -1 for `"a\r\nb".IndexOf("\n")`, where .NET answers 2. So
 * every step .NET takes before it consults the collation is taken here, its argument checks and the
 * empty value included, and the search itself throws. The compiler refuses a constant culture
 * comparison at build time; this is the one that arrived in a variable.
 *
 * `Equals` and `CompareTo` compare whole strings, which is what the statics already do by the same
 * comparison (`CompareTo` is the current culture's): they only add the instance's null check.
 */
import {
  compare,
  equals,
  isHigh,
  isLow,
  isStringComparison,
  NOT_SUPPORTED,
  ordinalIgnoreCase,
  ordinalUpper,
  outOfRange,
  requireComparison,
} from './string-statics';
import { exception } from './exceptions';

const INDEX_AT_MOST_LENGTH =
  'Index was out of range. Must be non-negative and less than or equal to the size of the collection.';
const INDEX_BELOW_LENGTH =
  'Index was out of range. Must be non-negative and less than the size of the collection.';
const COUNT =
  'Count must be positive and count must refer to a location within the string/array/collection.';

/** The instance a method is called on: .NET throws before the method runs when there is none. */
function receiver(source: string | null | undefined): string {
  if (source == null)
    throw exception(
      'System.NullReferenceException',
      'Object reference not set to an instance of an object.',
    );
  return source;
}

function argumentNull(parameter: string): Error {
  return exception(
    'System.ArgumentNullException',
    `Value cannot be null. (Parameter '${parameter}')`,
  );
}

/** A search .NET makes by ICU's collation, which the browser cannot: not supported here. */
function cultureSearch(member: string, comparison: string): Error {
  const name = comparison[0].toUpperCase() + comparison.slice(1);
  return exception(
    'System.NotSupportedException',
    `${member} by StringComparison.${name} has no search in the browser: .NET searches with ICU's ` +
      'collation, which JavaScript does not expose. Search by Ordinal or OrdinalIgnoreCase.',
  );
}

/** `TryGetSpan`'s check of a range, with the parameter .NET names when it fails. */
function requireRange(text: string, start: number, length: number): void {
  if (start < 0 || start > text.length) throw outOfRange('startIndex', INDEX_AT_MOST_LENGTH);
  if (length < 0 || start + length > text.length) throw outOfRange('count', COUNT);
}

// ---- ordinal ---------------------------------------------------------------------------------

/** `span.IndexOf(value)` over `text[start, start + length)`, relative to `start`. */
function ordinalIndexOf(text: string, start: number, length: number, value: string): number {
  if (value.length === 0) return 0;
  const found = text.indexOf(value, start);
  return found >= 0 && found + value.length <= start + length ? found - start : -1;
}

/** `span.LastIndexOf(value)` over `text[start, start + length)`, relative to `start`. */
function ordinalLastIndexOf(text: string, start: number, length: number, value: string): number {
  if (value.length === 0) return length;
  if (value.length > length) return -1;
  const found = text.lastIndexOf(value, start + length - value.length);
  return found >= start ? found - start : -1;
}

// ---- ordinal, ignoring case ------------------------------------------------------------------

/** `Ordinal.EqualsIgnoreCase` of the window at `at` and the whole value. */
function equalsIgnoreCase(text: string, at: number, value: string): boolean {
  return ordinalIgnoreCase(text, at, value.length, value, 0, value.length) === 0;
}

/** .NET's `Ordinal.IndexOfOrdinalIgnoreCase` over `text[start, start + length)`. */
function ignoreCaseIndexOf(text: string, start: number, length: number, value: string): number {
  if (value.length === 0) return 0;
  if (value.length > length) return -1;
  if (value.charCodeAt(0) >= 0x80) return casingSearch(text, start, length, value, true);
  for (let offset = 0; offset <= length - value.length; offset++) {
    if (equalsIgnoreCase(text, start + offset, value)) return offset;
  }
  return -1;
}

/** .NET's `Ordinal.LastIndexOfOrdinalIgnoreCase` over `text[start, start + length)`. */
function ignoreCaseLastIndexOf(text: string, start: number, length: number, value: string): number {
  if (value.length === 0) return length;
  if (value.length > length) return -1;
  return casingSearch(text, start, length, value, false);
}

/** .NET's `OrdinalCasing.IndexOf` (forward) and `LastIndexOf`: each window, first or last first. */
function casingSearch(
  text: string,
  start: number,
  length: number,
  value: string,
  forward: boolean,
): number {
  const limit = length - value.length;
  for (let step = 0; step <= limit; step++) {
    const offset = forward ? step : limit - step;
    if (casingMatch(text, start + offset, value)) return offset;
  }
  return -1;
}

/**
 * One window of `OrdinalCasing`'s search: a unit that is not a high half, or the value's last unit,
 * matches by itself or by its upper case; a high half with a low one after it in both matches as the
 * code point's upper case; and a high half the source does not pair matches only itself.
 */
function casingMatch(text: string, at: number, value: string): boolean {
  const last = value.length - 1;
  let v = 0;
  let s = at;
  while (v <= last) {
    const unitV = value.charCodeAt(v);
    const unitS = text.charCodeAt(s);
    if (!isHigh(unitV) || v === last) {
      if (unitV !== unitS && ordinalUpper(unitV) !== ordinalUpper(unitS)) return false;
      v++;
      s++;
      continue;
    }
    const lowV = value.charCodeAt(v + 1);
    const lowS = text.charCodeAt(s + 1);
    if (isHigh(unitS) && isLow(lowS) && isLow(lowV)) {
      const pointV = (unitV - 0xd800) * 0x400 + (lowV - 0xdc00) + 0x10000;
      const pointS = (unitS - 0xd800) * 0x400 + (lowS - 0xdc00) + 0x10000;
      if (ordinalUpper(pointV) !== ordinalUpper(pointS)) return false;
      v += 2;
      s += 2;
      continue;
    }
    if (unitV !== unitS) return false;
    v++;
    s++;
  }
  return true;
}

// ---- the methods -----------------------------------------------------------------------------

/** `StartsWith(value, comparisonType)`. */
export function startsWith(
  source: string | null | undefined,
  value: string | null | undefined,
  comparison: string,
): boolean {
  const text = receiver(source);
  if (value == null) throw argumentNull('value');
  if (value === text || value.length === 0) {
    requireComparison(comparison);
    return true;
  }
  switch (comparison) {
    case 'ordinal':
      return text.startsWith(value);
    case 'ordinalIgnoreCase':
      return text.length >= value.length && equalsIgnoreCase(text, 0, value);
    default:
      requireComparison(comparison);
      throw cultureSearch('StartsWith', comparison);
  }
}

/** `EndsWith(value, comparisonType)`. */
export function endsWith(
  source: string | null | undefined,
  value: string | null | undefined,
  comparison: string,
): boolean {
  const text = receiver(source);
  if (value == null) throw argumentNull('value');
  if (value === text || value.length === 0) {
    requireComparison(comparison);
    return true;
  }
  switch (comparison) {
    case 'ordinal':
      return text.endsWith(value);
    case 'ordinalIgnoreCase':
      return (
        text.length >= value.length && equalsIgnoreCase(text, text.length - value.length, value)
      );
    default:
      requireComparison(comparison);
      throw cultureSearch('EndsWith', comparison);
  }
}

/**
 * What a search overload takes after its value, as C# lists it: the comparison alone, a start and
 * the comparison, or a start, a count and the comparison. The arguments arrive in the order they
 * were written, so a call site binds nothing to keep them in the order they run.
 */
export type SearchArguments =
  | [comparison: string]
  | [startIndex: number, comparison: string]
  | [startIndex: number, count: number, comparison: string];

function comparisonOf(args: SearchArguments): string {
  return args.length === 1 ? args[0] : args.length === 2 ? args[1] : args[2];
}

/**
 * `IndexOf(value, [startIndex, [count,]] comparisonType)`, and the char overload: the comparison
 * decides first, and a value that is null names itself rather than the comparison. Without a start
 * the search begins at 0, and without a count it runs to the end.
 */
export function indexOf(
  source: string | null | undefined,
  value: string | null | undefined,
  ...args: SearchArguments
): number {
  const text = receiver(source);
  const comparison = comparisonOf(args);
  const start = args.length === 1 ? 0 : args[0];
  const length = args.length === 3 ? args[1] : text.length - start;
  if (!isStringComparison(comparison)) {
    throw value == null
      ? argumentNull('value')
      : exception('System.ArgumentException', NOT_SUPPORTED);
  }
  if (value == null) throw argumentNull('value');
  requireRange(text, start, length);
  let found: number;
  if (comparison === 'ordinal') found = ordinalIndexOf(text, start, length, value);
  else if (comparison === 'ordinalIgnoreCase')
    found = ignoreCaseIndexOf(text, start, length, value);
  else if (value.length === 0) found = 0;
  else throw cultureSearch('IndexOf', comparison);
  return found >= 0 ? found + start : found;
}

/**
 * `LastIndexOf(value, [startIndex, [count,]] comparisonType)`: `startIndex` is the last unit the
 * search may END on and it runs back `count` units, normalized as .NET's `CompareInfo` normalizes
 * them (a start one past the end steps back once, and an empty string's -1 is its only start).
 */
export function lastIndexOf(
  source: string | null | undefined,
  value: string | null | undefined,
  ...args: SearchArguments
): number {
  const text = receiver(source);
  const comparison = comparisonOf(args);
  let start = args.length === 1 ? text.length - 1 : args[0];
  let length = args.length === 1 ? text.length : args.length === 2 ? start + 1 : args[1];
  if (!isStringComparison(comparison)) {
    throw value == null
      ? argumentNull('value')
      : exception('System.ArgumentException', NOT_SUPPORTED);
  }
  if (value == null) throw argumentNull('value');
  for (;;) {
    if (start >= 0 && start < text.length) break;
    if (start === -1 && text.length === 0) {
      length = 0;
      break;
    }
    if (start !== text.length) throw outOfRange('startIndex', INDEX_BELOW_LENGTH);
    start--;
    if (length > 0) length--;
  }
  const begin = start - length + 1;
  if (begin < 0 || length < 0 || begin + length > text.length) throw outOfRange('count', COUNT);
  let found: number;
  if (comparison === 'ordinal') found = ordinalLastIndexOf(text, begin, length, value);
  else if (comparison === 'ordinalIgnoreCase')
    found = ignoreCaseLastIndexOf(text, begin, length, value);
  else if (value.length === 0) found = length;
  else throw cultureSearch('LastIndexOf', comparison);
  return found >= 0 ? found + begin : found;
}

/** `Contains(value, comparisonType)` and the char overload: `IndexOf` found it. */
export function contains(
  source: string | null | undefined,
  value: string | null | undefined,
  comparison: string,
): boolean {
  return indexOf(source, value, comparison) >= 0;
}

/**
 * `Replace(oldValue, newValue[, comparisonType])`: every match, left to right and never
 * overlapping, by the comparison. The replacement is text, never a pattern (`replaceAll` reads
 * `$&` in it), a null one removes, and an old value that is null or empty is refused.
 */
export function replace(
  source: string | null | undefined,
  oldValue: string | null | undefined,
  newValue: string | null | undefined,
  comparison: string,
): string {
  const text = receiver(source);
  requireComparison(comparison);
  if (oldValue == null) throw argumentNull('oldValue');
  if (oldValue.length === 0)
    throw exception(
      'System.ArgumentException',
      "The value cannot be an empty string. (Parameter 'oldValue')",
    );
  const replacement = newValue ?? '';
  if (comparison === 'ordinal') return text.split(oldValue).join(replacement);
  if (comparison !== 'ordinalIgnoreCase') throw cultureSearch('Replace', comparison);
  let result = '';
  let rest = 0;
  for (;;) {
    const found = ignoreCaseIndexOf(text, rest, text.length - rest, oldValue);
    if (found < 0) break;
    result += text.slice(rest, rest + found) + replacement;
    rest += found + oldValue.length;
  }
  return rest === 0 ? text : result + text.slice(rest);
}

/** `Equals(value, comparisonType)`: the static's rule, on an instance that must exist. */
export function instanceEquals(
  source: string | null | undefined,
  value: string | null | undefined,
  comparison: string,
): boolean {
  return equals(receiver(source), value, comparison);
}

/** `CompareTo(strB)`: the current culture's comparison, as `string.Compare(a, b)`, a null last. */
export function compareTo(
  source: string | null | undefined,
  value: string | null | undefined,
): number {
  return compare(receiver(source), value, 'currentCulture');
}
