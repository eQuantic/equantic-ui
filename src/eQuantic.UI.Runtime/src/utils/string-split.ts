/**
 * `string.Split`, every overload, as .NET 10 splits: `String.Manipulation.cs`'s `SplitInternal`,
 * `SplitWithoutPostProcessing` and `SplitWithPostProcessing`, step for step.
 *
 * The compiler picks the twin by the overload C# bound: {@link split} for one separator (a char,
 * which is a string of one here, or a string), {@link splitAny} for a set of them (a char array, a
 * span of chars or a string array). It hands each argument to its parameter, the count and the
 * options as the values they are, so one held in a variable is the one it holds. JavaScript's
 * `split` was handed the first argument whole: an array was one text joined by commas, a null was
 * the text "null", the count was dropped, and the options were read from their spelling (#753).
 *
 * What .NET does that `split` does not:
 * - a null or empty set of separators is .NET's white space, U+0085 and U+00A0 included;
 * - a null or empty single separator splits nothing, and a null or empty one in a set is skipped;
 * - in a set, the FIRST separator in the array's order that matches at a place wins, so
 *   `"abcd".Split(["b", "bc"])` is `a` and `cd`;
 * - the count caps the entries, the last one taking the rest of the text unsplit;
 * - `TrimEntries` trims each entry of .NET's white space, and `RemoveEmptyEntries` drops the empty
 *   ones, the trimmed ones included, the last entry skipping past them;
 * - a negative count and an option .NET does not define are refused, in that order.
 */
import { exception } from './exceptions';
import { receiver } from './exceptions';
import { requireNonNegative } from './string-statics';
import { isWhiteSpace, trim } from './white-space';

/** `int.MaxValue`, the count of the overloads that take none. */
const ALL = 2147483647;
const REMOVE_EMPTY_ENTRIES = 1;
const TRIM_ENTRIES = 2;

/** Where each separator stands in the text, and how long it is. */
interface Places {
  readonly starts: number[];
  readonly lengths: number[];
}

/** `CheckStringSplitOptions`: any flag besides the two .NET defines is refused. */
function requireOptions(options: number): void {
  if ((options & ~(REMOVE_EMPTY_ENTRIES | TRIM_ENTRIES)) !== 0)
    throw exception('System.ArgumentException', "Value of flags is invalid. (Parameter 'options')");
}

/** `CreateSplitArrayOfThisAsSoleValue`: the text as the one entry, trimmed and dropped by the
 * options, and nothing for a count of zero. */
function sole(text: string, options: number, count: number): string[] {
  if (count === 0) return [];
  const candidate = options & TRIM_ENTRIES ? trim(text) : text;
  return options & REMOVE_EMPTY_ENTRIES && candidate.length === 0 ? [] : [candidate];
}

/** `MakeSeparatorList`: each place of one separator, which is not empty, left to right. */
function placesOf(text: string, separator: string): Places {
  const starts: number[] = [];
  let at = text.indexOf(separator);
  while (at >= 0) {
    starts.push(at);
    at = text.indexOf(separator, at + separator.length);
  }
  return { starts, lengths: starts.map(() => separator.length) };
}

/** `MakeSeparatorListAny` over .NET's white space: every white space unit is a separator. */
function whiteSpaceOf(text: string): Places {
  const starts: number[] = [];
  for (let i = 0; i < text.length; i++) if (isWhiteSpace(text[i])) starts.push(i);
  return { starts, lengths: starts.map(() => 1) };
}

/** `MakeSeparatorListAny` over a set: at each place, the first separator in the array's order that
 * matches there, a null or empty one skipped, and the search goes on after it. */
function anyOf(text: string, separators: readonly (string | null | undefined)[]): Places {
  const starts: number[] = [];
  const lengths: number[] = [];
  for (let i = 0; i < text.length; i++) {
    for (const separator of separators) {
      if (separator == null || separator.length === 0 || !text.startsWith(separator, i)) continue;
      starts.push(i);
      lengths.push(separator.length);
      i += separator.length - 1;
      break;
    }
  }
  return { starts, lengths };
}

/** `SplitWithoutPostProcessing`: the entries between the first `count - 1` separators, and the
 * rest of the text as the last one. */
function entries(text: string, places: Places, count: number): string[] {
  const cuts = Math.min(places.starts.length, count - 1);
  const result: string[] = [];
  let current = 0;
  for (let i = 0; i < cuts; i++) {
    result.push(text.slice(current, places.starts[i]));
    current = places.starts[i] + places.lengths[i];
  }
  result.push(text.slice(current));
  return result;
}

/** `SplitWithPostProcessing`: the entries trimmed and the empty ones dropped as the options say. At
 * the last entry the count leaves, the empty entries before the next one that holds something are
 * skipped, so the rest it takes starts there. */
function postProcessed(text: string, places: Places, count: number, options: number): string[] {
  const trimmed = (entry: string) => (options & TRIM_ENTRIES ? trim(entry) : entry);
  const kept = (entry: string) => entry.length !== 0 || (options & REMOVE_EMPTY_ENTRIES) === 0;
  const { starts, lengths } = places;
  const result: string[] = [];
  let current = 0;
  for (let i = 0; i < starts.length; i++) {
    const entry = trimmed(text.slice(current, starts[i]));
    if (kept(entry)) result.push(entry);
    current = starts[i] + lengths[i];
    if (result.length === count - 1) {
      if (options & REMOVE_EMPTY_ENTRIES) {
        while (++i < starts.length) {
          if (trimmed(text.slice(current, starts[i])).length !== 0) break;
          current = starts[i] + lengths[i];
        }
      }
      break;
    }
  }
  const last = trimmed(text.slice(current));
  if (kept(last)) result.push(last);
  return result;
}

function pieces(text: string, places: Places, count: number, options: number): string[] {
  if (places.starts.length === 0) return sole(text, options, count);
  return options === 0 ? entries(text, places, count) : postProcessed(text, places, count, options);
}

/**
 * `Split(char, …)` and `Split(string?, …)`: one separator, a null or an empty one splitting nothing.
 */
export function split(
  text: string | null | undefined,
  separator: string | null | undefined,
  count: number = ALL,
  options: number = 0,
): string[] {
  const source = receiver(text);
  requireNonNegative('count', count);
  requireOptions(options);
  if (count <= 1 || source.length === 0 || separator == null || separator.length === 0)
    return sole(source, options, count);
  return pieces(source, placesOf(source, separator), count, options);
}

/**
 * `Split()`, `Split(params char[]?)`, `Split(params ReadOnlySpan<char>)`, `Split(char[]?, …)` and
 * `Split(string[]?, …)`: a set of separators, a null or an empty one being .NET's white space.
 */
export function splitAny(
  text: string | null | undefined,
  separators?: readonly (string | null | undefined)[] | null,
  count: number = ALL,
  options: number = 0,
): string[] {
  const source = receiver(text);
  requireNonNegative('count', count);
  requireOptions(options);
  if (count <= 1 || source.length === 0) return sole(source, options, count);
  const places =
    separators == null || separators.length === 0
      ? whiteSpaceOf(source)
      : anyOf(source, separators);
  return pieces(source, places, count, options);
}
