/**
 * `string.Compare`, `string.CompareOrdinal`, `string.Equals` with a `StringComparison`, and
 * `string.Join` over a range, as .NET answers them.
 *
 * A null orders before every string and equals only a null. A CULTURE comparison is the platform's
 * collator (`Intl.Collator`, ICU underneath, as .NET's is), case-blind at the secondary strength, and
 * answers -1, 0 or 1; a soft hyphen is ignorable there, and a letter with a combining accent equals
 * the precomposed one. An ORDINAL comparison answers what .NET's does, the difference of the first
 * two code units that differ, or of the lengths: `Compare("a", "c", Ordinal)` is -2, not -1.
 *
 * An ordinal comparison that ignores case compares each code point's SIMPLE upper case, as .NET's
 * `OrdinalCasing` does. That is `toUpperCase` wherever it maps one code point to one, with the
 * differences measured against .NET 10 over the BMP: .NET leaves the dotless i and the long s alone
 * (no Turkish i in an ordinal comparison), and takes the Greek letters with an iota subscript to
 * their title case, which `toUpperCase` expands to two letters. So the Kelvin sign is not a k. A
 * surrogate pair is read as the code point it encodes, whose upper case is compared whole, and it
 * orders after anything that is not a pair, whatever the code units say.
 */
import { formatLocale } from './culture';

/** `StringComparison`, as its members cross: by name. */
export type StringComparison =
  | 'currentCulture'
  | 'currentCultureIgnoreCase'
  | 'invariantCulture'
  | 'invariantCultureIgnoreCase'
  | 'ordinal'
  | 'ordinalIgnoreCase';

const COMPARISONS: ReadonlySet<string> = new Set<StringComparison>([
  'currentCulture',
  'currentCultureIgnoreCase',
  'invariantCulture',
  'invariantCultureIgnoreCase',
  'ordinal',
  'ordinalIgnoreCase',
]);

const NOT_SUPPORTED =
  "The string comparison type passed in is currently not supported. (Parameter 'comparisonType')";

/**
 * A comparison held in a variable reaches here as a plain string (a `let` widens the member's name),
 * so the statics take any string and this says which are comparisons, as .NET's own check does.
 */
function requireComparison(comparison: string): asserts comparison is StringComparison {
  if (!COMPARISONS.has(comparison)) throw new Error(NOT_SUPPORTED);
}

/** An `ArgumentOutOfRangeException`'s words; .NET writes the actual value on a line of its own. */
function outOfRange(parameter: string, message: string, actual?: number): Error {
  const value = actual === undefined ? '' : `\nActual value was ${actual}.`;
  return new Error(`${message} (Parameter '${parameter}')${value}`);
}

function requireNonNegative(parameter: string, value: number): void {
  if (value < 0)
    throw outOfRange(parameter, `${parameter} ('${value}') must be a non-negative value.`, value);
}

// ---- culture ---------------------------------------------------------------------------------

const collators = new Map<string, Intl.Collator>();

/** The collator for the current culture (the FORMAT half of the pair the app installed, or the
 * host's) or the invariant one, case-blind or not; built once per culture and strength. */
function collator(invariant: boolean, ignoreCase: boolean): Intl.Collator {
  const locale = invariant ? 'und' : formatLocale();
  const key = `${locale ?? ''}|${ignoreCase}`;
  let found = collators.get(key);
  if (found === undefined) {
    found = new Intl.Collator(locale, { sensitivity: ignoreCase ? 'accent' : 'variant' });
    collators.set(key, found);
  }
  return found;
}

// ---- ordinal ---------------------------------------------------------------------------------

/** Where .NET's `OrdinalCasing` differs from `toUpperCase` kept to one code point. */
const ORDINAL_UPPER = new Map<number, number>([
  [0x0131, 0x0131],
  [0x017f, 0x017f],
  [0x1fb3, 0x1fbc],
  [0x1fc3, 0x1fcc],
  [0x1ff3, 0x1ffc],
]);
for (const start of [0x1f80, 0x1f90, 0x1fa0]) {
  for (let unit = start; unit < start + 8; unit++) ORDINAL_UPPER.set(unit, unit + 8);
}

/** A code point's simple upper case, as .NET's ordinal comparison takes it. */
function ordinalUpper(codePoint: number): number {
  if (codePoint < 0x61) return codePoint;
  const known = ORDINAL_UPPER.get(codePoint);
  if (known !== undefined) return known;
  const text = String.fromCodePoint(codePoint);
  const upper = text.toUpperCase();
  return upper.length === text.length ? (upper.codePointAt(0) as number) : codePoint;
}

function isHigh(unit: number): boolean {
  return unit >= 0xd800 && unit <= 0xdbff;
}

function isLow(unit: number): boolean {
  return unit >= 0xdc00 && unit <= 0xdfff;
}

/** .NET's `CompareOrdinalHelper` over two ranges. */
function ordinal(
  a: string,
  indexA: number,
  lengthA: number,
  b: string,
  indexB: number,
  lengthB: number,
): number {
  const length = Math.min(lengthA, lengthB);
  for (let i = 0; i < length; i++) {
    const difference = a.charCodeAt(indexA + i) - b.charCodeAt(indexB + i);
    if (difference !== 0) return difference;
  }
  return lengthA - lengthB;
}

/** Whether a surrogate pair starts here with both halves inside the range, as .NET reads one. */
function startsPair(text: string, at: number, remaining: number): boolean {
  return remaining > 1 && isHigh(text.charCodeAt(at)) && isLow(text.charCodeAt(at + 1));
}

/**
 * .NET's `OrdinalCasing.CompareStringIgnoreCase` over two ranges, step for step. A code unit is
 * compared by its upper case, and a pair by the upper case of its code point, so the difference is of
 * code units or of code points, the first that differs. A pair against a unit that is not one answers
 * 1 or -1 without comparing them, and past the shorter range it is the difference of the lengths.
 */
function ordinalIgnoreCase(
  a: string,
  indexA: number,
  lengthA: number,
  b: string,
  indexB: number,
  lengthB: number,
): number {
  const length = Math.min(lengthA, lengthB);
  let index = 0;
  while (index < length) {
    const pairA = startsPair(a, indexA + index, lengthA - index);
    const pairB = startsPair(b, indexB + index, lengthB - index);
    if (pairA !== pairB) return pairA ? 1 : -1;
    const valueA = pairA ? (a.codePointAt(indexA + index) as number) : a.charCodeAt(indexA + index);
    const valueB = pairB ? (b.codePointAt(indexB + index) as number) : b.charCodeAt(indexB + index);
    if (valueA !== valueB) {
      const difference = ordinalUpper(valueA) - ordinalUpper(valueB);
      if (difference !== 0) return difference;
    }
    index += pairA ? 2 : 1;
  }
  return lengthA - lengthB;
}

function compareBy(
  a: string,
  indexA: number,
  lengthA: number,
  b: string,
  indexB: number,
  lengthB: number,
  comparison: StringComparison,
): number {
  switch (comparison) {
    case 'ordinal':
      return ordinal(a, indexA, lengthA, b, indexB, lengthB);
    case 'ordinalIgnoreCase':
      return ordinalIgnoreCase(a, indexA, lengthA, b, indexB, lengthB);
    default: {
      const invariant =
        comparison === 'invariantCulture' || comparison === 'invariantCultureIgnoreCase';
      const ignoreCase =
        comparison === 'currentCultureIgnoreCase' || comparison === 'invariantCultureIgnoreCase';
      const left = a.slice(indexA, indexA + lengthA);
      const right = b.slice(indexB, indexB + lengthB);
      return Math.sign(collator(invariant, ignoreCase).compare(left, right));
    }
  }
}

// ---- the statics -----------------------------------------------------------------------------

/**
 * `string.Compare(strA, strB, comparisonType)`, and the overloads without one, which the compiler
 * passes as the current culture's, case-blind where a bool says so. The comparison is checked
 * first, a null or two included.
 */
export function compare(
  a: string | null | undefined,
  b: string | null | undefined,
  comparison: string,
): number {
  requireComparison(comparison);
  if (a == null) return b == null ? 0 : -1;
  if (b == null) return 1;
  return compareBy(a, 0, a.length, b, 0, b.length, comparison);
}

/**
 * `string.Compare(strA, indexA, strB, indexB, length)` and its `ignoreCase` overload: the current
 * culture's comparison of the two ranges, each clamped to its string, checked as .NET's
 * `CompareInfo` checks them — a null has no range to give, so anything but a zero one throws.
 */
export function compareRange(
  a: string | null | undefined,
  indexA: number,
  b: string | null | undefined,
  indexB: number,
  length: number,
  ignoreCase: boolean,
): number {
  const lengthA = a == null ? length : Math.min(length, a.length - indexA);
  const lengthB = b == null ? length : Math.min(length, b.length - indexB);
  const fitsA = a == null ? indexA === 0 && lengthA === 0 : fits(a, indexA, lengthA);
  const fitsB = b == null ? indexB === 0 && lengthB === 0 : fits(b, indexB, lengthB);
  if (!fitsA || !fitsB) {
    requireNonNegative('length1', lengthA);
    requireNonNegative('length2', lengthB);
    requireNonNegative('offset1', indexA);
    requireNonNegative('offset2', indexB);
    throw outOfRange(
      a == null ? 'string1' : 'string2',
      'Offset and length must refer to a position in the string.',
    );
  }
  if (a == null) return b == null ? 0 : -1;
  if (b == null) return 1;
  return compareBy(
    a,
    indexA,
    lengthA,
    b,
    indexB,
    lengthB,
    ignoreCase ? 'currentCultureIgnoreCase' : 'currentCulture',
  );
}

/** A range inside the string, the way .NET's `TryGetSpan` reads one. */
function fits(text: string, index: number, length: number): boolean {
  return index >= 0 && length >= 0 && index + length <= text.length;
}

/**
 * `string.Compare(strA, indexA, strB, indexB, length, comparisonType)` and
 * `string.CompareOrdinal(strA, indexA, strB, indexB, length)`, which check in another order than
 * the overloads above: the comparison first, then a null answers before any range is read, then the
 * ranges, and a zero length is equal.
 */
export function compareRangeBy(
  a: string | null | undefined,
  indexA: number,
  b: string | null | undefined,
  indexB: number,
  length: number,
  comparison: string,
): number {
  requireComparison(comparison);
  if (a == null || b == null) return a == null ? (b == null ? 0 : -1) : 1;
  requireNonNegative('length', length);
  requireNonNegative('indexA', indexA);
  requireNonNegative('indexB', indexB);
  const tooFar =
    'Index was out of range. Must be non-negative and less than or equal to the size of the collection.';
  if (a.length - indexA < 0) throw outOfRange('indexA', tooFar);
  if (b.length - indexB < 0) throw outOfRange('indexB', tooFar);
  if (length === 0) return 0;
  const lengthA = Math.min(length, a.length - indexA);
  const lengthB = Math.min(length, b.length - indexB);
  return compareBy(a, indexA, lengthA, b, indexB, lengthB, comparison);
}

/** `string.Equals(a, b, comparisonType)`: the comparison checked first, a null equal only to a null,
 * and an ordinal comparison of two lengths that differ never equal. */
export function equals(
  a: string | null | undefined,
  b: string | null | undefined,
  comparison: string,
): boolean {
  requireComparison(comparison);
  if (a == null || b == null) return a == null && b == null;
  if (comparison === 'ordinal') return a === b;
  if (comparison === 'ordinalIgnoreCase' && a.length !== b.length) return false;
  return compareBy(a, 0, a.length, b, 0, b.length, comparison) === 0;
}

/**
 * `string.Join(separator, value, startIndex, count)`: the elements of the range, a null one written
 * as nothing, with the checks .NET makes before it reads one.
 */
export function joinRange(
  separator: string | null | undefined,
  value: readonly (string | null | undefined)[] | null | undefined,
  startIndex: number,
  count: number,
): string {
  if (value == null) throw new Error("Value cannot be null. (Parameter 'value')");
  requireNonNegative('startIndex', startIndex);
  requireNonNegative('count', count);
  if (startIndex > value.length - count) {
    throw outOfRange(
      'startIndex',
      `startIndex ('${startIndex}') must be less than or equal to '${value.length - count}'.`,
      startIndex,
    );
  }
  return value.slice(startIndex, startIndex + count).join(separator ?? '');
}
