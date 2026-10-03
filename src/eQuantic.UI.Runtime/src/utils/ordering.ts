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
 *    that implements `IComparable`;
 *  - an enum's values by name ({@link EnumOrder}) — a member crosses as its camelCase name, and
 *    .NET orders it by its value, so `High` after `Low` whatever their names say.
 *
 * A null comes before every value and equals another null, as it does in .NET. One table serves
 * `Max`/`Min`, every sorted collection the compiler builds, and every one a hydration rebuilds, so
 * a set that crossed from the server orders as one the browser makes.
 */
import { compare as compareStrings } from './string-statics';

export type Ordering = 'value' | 'real' | 'text' | 'comparable' | EnumOrder;

/**
 * A non-flags enum's members, by the camelCase name a value crosses as, and the value .NET orders it
 * by. A flags enum is a number here and orders as a `value`.
 */
export type EnumOrder = Readonly<Record<string, number>>;

interface Comparable {
  compareTo(other: unknown): number;
}

/** The comparer of an ordering. */
export function comparerOf(ordering: Ordering): (a: unknown, b: unknown) => number {
  if (typeof ordering === 'object') return byEnum(ordering);
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

function byEnum(values: EnumOrder): (a: unknown, b: unknown) => number {
  // A value no member names is a number already (an undeclared one, cast in): it orders as itself.
  const valueOf = (name: unknown): number =>
    Object.prototype.hasOwnProperty.call(values, name as string) ? values[name as string] : Number(name);
  return (a, b) => {
    if (a == null) return b == null ? 0 : -1;
    if (b == null) return 1;
    const x = valueOf(a);
    const y = valueOf(b);
    return x < y ? -1 : x > y ? 1 : 0;
  };
}

function byCompareTo(a: unknown, b: unknown): number {
  if (a == null) return b == null ? 0 : -1;
  if (b == null) return 1;
  return (a as Comparable).compareTo(b);
}
