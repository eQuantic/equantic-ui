/**
 * The argument guards .NET 8 added to its argument exceptions (#1202): `ArgumentNullException.ThrowIfNull`,
 * `ArgumentException.ThrowIfNullOrEmpty` and `ThrowIfNullOrWhiteSpace`, and the nine range guards of
 * `ArgumentOutOfRangeException`, each throwing where .NET throws, the exception .NET throws, with its
 * `ParamName`, its `ActualValue` and its words, measured on .NET 10.
 *
 * What the browser cannot see the compiler hands over: the parameter's name, which C# fills from the
 * caller's argument text; the type's default equality, or its ordering; and how a value of the type is
 * written in the current culture, as `string.Format` writes it in the message, called only to throw.
 */
import { Decimal } from './decimal';
import { exception, receiver } from './exceptions';
import { comparerOf, type Ordering } from './ordering';
import { isWhiteSpace } from './white-space';

/** The type's `EqualityComparer<T>.Default.Equals`. */
type Equality = (value: unknown, other: unknown) => boolean;

/** A value of the app's own type, or a date, which orders by the `CompareTo` its twin carries. */
interface Comparable {
  compareTo(other: unknown): number;
}

/** `ArgumentNullException.ThrowIfNull(argument, paramName)`. */
export function throwIfNull(argument: unknown, paramName: string | null): void {
  if (argument == null) throw nullArgument(paramName);
}

/** `ArgumentException.ThrowIfNullOrEmpty(argument, paramName)`. */
export function throwIfNullOrEmpty(argument: string | null, paramName: string | null): void {
  if (argument == null) throw nullArgument(paramName);
  if (argument.length === 0) throw invalidArgument('The value cannot be an empty string.', paramName);
}

/**
 * `ArgumentException.ThrowIfNullOrWhiteSpace(argument, paramName)`: white space is `char.IsWhiteSpace`'s,
 * U+0085 included and U+200B not, and an empty string is white.
 */
export function throwIfNullOrWhiteSpace(argument: string | null, paramName: string | null): void {
  if (argument == null) throw nullArgument(paramName);
  for (const unit of argument) {
    if (!isWhiteSpace(unit)) return;
  }
  throw invalidArgument('The value cannot be an empty string or composed entirely of whitespace.', paramName);
}

/** `ArgumentOutOfRangeException.ThrowIfNegative(value, paramName)`. */
export function throwIfNegative<T>(value: T, paramName: string | null, text: (value: T) => string): void {
  if (isNegative(value)) throw outOfRange(value, paramName, text, 'must be a non-negative value.');
}

/** `ArgumentOutOfRangeException.ThrowIfZero(value, paramName)`. */
export function throwIfZero<T>(value: T, paramName: string | null, text: (value: T) => string): void {
  if (isZero(value)) throw outOfRange(value, paramName, text, 'must be a non-zero value.');
}

/** `ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value, paramName)`. */
export function throwIfNegativeOrZero<T>(value: T, paramName: string | null, text: (value: T) => string): void {
  if (isNegative(value) || isZero(value)) {
    throw outOfRange(value, paramName, text, 'must be a non-negative and non-zero value.');
  }
}

/** `ArgumentOutOfRangeException.ThrowIfEqual(value, other, paramName)`, by the type's default equality. */
export function throwIfEqual<T>(value: T, other: T, paramName: string | null, equals: Equality, text: (value: T) => string): void {
  if (equals(value, other)) throw outOfRange(value, paramName, text, `must not be equal to '${shownOrNull(other, text)}'.`, shownOrNull);
}

/** `ArgumentOutOfRangeException.ThrowIfNotEqual(value, other, paramName)`. */
export function throwIfNotEqual<T>(value: T, other: T, paramName: string | null, equals: Equality, text: (value: T) => string): void {
  if (!equals(value, other)) throw outOfRange(value, paramName, text, `must be equal to '${shownOrNull(other, text)}'.`, shownOrNull);
}

/** `ArgumentOutOfRangeException.ThrowIfGreaterThan(value, other, paramName)`. */
export function throwIfGreaterThan<T>(value: T, other: T, paramName: string | null, ordering: Ordering, text: (value: T) => string): void {
  if (compared(value, other, ordering) > 0) {
    throw outOfRange(value, paramName, text, `must be less than or equal to '${shown(other, text)}'.`);
  }
}

/** `ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(value, other, paramName)`. */
export function throwIfGreaterThanOrEqual<T>(value: T, other: T, paramName: string | null, ordering: Ordering, text: (value: T) => string): void {
  if (compared(value, other, ordering) >= 0) {
    throw outOfRange(value, paramName, text, `must be less than '${shown(other, text)}'.`);
  }
}

/** `ArgumentOutOfRangeException.ThrowIfLessThan(value, other, paramName)`. */
export function throwIfLessThan<T>(value: T, other: T, paramName: string | null, ordering: Ordering, text: (value: T) => string): void {
  if (compared(value, other, ordering) < 0) {
    throw outOfRange(value, paramName, text, `must be greater than or equal to '${shown(other, text)}'.`);
  }
}

/** `ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value, other, paramName)`. */
export function throwIfLessThanOrEqual<T>(value: T, other: T, paramName: string | null, ordering: Ordering, text: (value: T) => string): void {
  if (compared(value, other, ordering) <= 0) {
    throw outOfRange(value, paramName, text, `must be greater than '${shown(other, text)}'.`);
  }
}

/**
 * `T.IsNegative(value)`: a number below zero, a negative zero, and a NaN, which .NET's `double.NaN`
 * is, its sign bit set, and whose sign the browser does not show; a long below zero; a decimal by its
 * mantissa, which keeps no sign for zero; and never a char, which is unsigned.
 */
function isNegative(value: unknown): boolean {
  if (typeof value === 'number') return value < 0 || Object.is(value, -0) || Number.isNaN(value);
  if (typeof value === 'bigint') return value < 0n;
  if (value instanceof Decimal) return value.mantissa < 0n;
  return false;
}

/** `T.IsZero(value)`: a negative zero is one, and a decimal at any scale, and the char U+0000. */
function isZero(value: unknown): boolean {
  if (typeof value === 'number') return value === 0;
  if (typeof value === 'bigint') return value === 0n;
  if (value instanceof Decimal) return value.mantissa === 0n;
  return value === '\0';
}

/**
 * `value.CompareTo(other)`, as the ordering guards call it: through null, the NullReferenceException; a
 * type of the app's or a date by its own `compareTo`; anything else by the ordering .NET's comparer
 * gives its type, a NaN equal to a NaN and before every other number, a string in the current culture.
 */
function compared(value: unknown, other: unknown, ordering: Ordering): number {
  const held = receiver(value);
  return ordering === 'comparable' ? (held as Comparable).compareTo(other) : comparerOf(ordering)(held, other);
}

/** A value in an ordering guard's message: a null as `string.Format` writes it, nothing. */
function shown<T>(value: T, text: (value: T) => string): string {
  return value == null ? '' : text(value);
}

/** A value in an equality guard's message, where .NET writes a null as `null`. */
function shownOrNull<T>(value: T, text: (value: T) => string): string {
  return value == null ? 'null' : text(value);
}

/**
 * The exception a range guard throws: `{paramName} ('{value}') {rule}`, then the parameter's name and the
 * actual value on a line of its own, which .NET leaves out for a null. A null name is written as nothing.
 */
function outOfRange<T>(
  value: T,
  paramName: string | null,
  text: (value: T) => string,
  rule: string,
  write: (value: T, text: (value: T) => string) => string = shown,
): Error {
  return exception('System.ArgumentOutOfRangeException', `${paramName ?? ''} ('${write(value, text)}') ${rule}`, {
    paramName,
    actualValue: value,
    actualText: value == null ? undefined : text(value),
  });
}

function nullArgument(paramName: string | null): Error {
  return exception('System.ArgumentNullException', 'Value cannot be null.', { paramName });
}

function invalidArgument(message: string, paramName: string | null): Error {
  return exception('System.ArgumentException', message, { paramName });
}
