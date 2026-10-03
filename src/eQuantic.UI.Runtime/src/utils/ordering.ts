/**
 * How .NET's `Comparer<T>.Default` orders a value of a type, which the compiler names from the type it
 * knows, since a value alone cannot say it: a C# string and a C# char are both strings here and order
 * differently there, the one in the current culture and the other by its code unit.
 *
 *  - `value` — by `<`: an integer, a long, a char, a bool;
 *  - `real` — a double or a float as `double.CompareTo` orders it: a NaN before every number, and
 *    equal to another NaN, where `<` finds it neither less nor greater than anything;
 *  - `text` — a string in the current culture, as `string.CompareTo` orders one;
 *  - `comparable` — by the value's own `compareTo`: a decimal, a date, and a type of the app's own
 *    that implements `IComparable`.
 *
 * A null comes before every value and equals another null, as it does in .NET. One table serves
 * `Max`/`Min`, every sorted collection the compiler builds, and every one a hydration rebuilds, so
 * a set that crossed from the server orders as one the browser makes.
 */
import { compare as compareStrings } from './string-statics';

export type Ordering = 'value' | 'real' | 'text' | 'comparable';

interface Comparable {
  compareTo(other: unknown): number;
}

/** The comparer of an ordering. */
export function comparerOf(ordering: Ordering): (a: unknown, b: unknown) => number {
  switch (ordering) {
    case 'text':
      return byText;
    case 'comparable':
      return byCompareTo;
    case 'real':
      return byReal;
    default:
      return byValue;
  }
}

function byValue(a: unknown, b: unknown): number {
  if (a == null) return b == null ? 0 : -1;
  if (b == null) return 1;
  return (a as number) < (b as number) ? -1 : (a as number) > (b as number) ? 1 : 0;
}

function byReal(a: unknown, b: unknown): number {
  if (a == null) return b == null ? 0 : -1;
  if (b == null) return 1;
  if (Number.isNaN(a)) return Number.isNaN(b) ? 0 : -1;
  if (Number.isNaN(b)) return 1;
  return (a as number) < (b as number) ? -1 : (a as number) > (b as number) ? 1 : 0;
}

function byText(a: unknown, b: unknown): number {
  return compareStrings(a as string | null | undefined, b as string | null | undefined, 'currentCulture');
}

function byCompareTo(a: unknown, b: unknown): number {
  if (a == null) return b == null ? 0 : -1;
  if (b == null) return 1;
  return (a as Comparable).compareTo(b);
}
