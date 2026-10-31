/**
 * `Convert.To<number>` of what is not text, as .NET converts it (#401): an integer of another width has
 * its range checked, a double is rounded half to even first, a decimal by `decimal.Round`, a 64-bit
 * target answers a BigInt, and an `object` is read by what it holds when the call runs. Each overflow
 * is in .NET's words, which differ by overload: the narrow widths of a double go through
 * `ToInt32(double)` and speak of an Int32, and `ToInt64` and `ToUInt64` of a double are a checked cast
 * and speak of arithmetic.
 *
 * A char or an enum's member held in an `object` cannot be told from a text here, nor an `int` from a
 * `double` of the same value (#552): the first two are read as text, and the last as the integer, whose
 * overflow names the target where a double's narrow width names an Int32.
 */
import { boolConvert } from './boolean-text';
import { DateTime, timeTypeOf } from './datetime';
import { Decimal } from './decimal';
import { round } from './dotnet-math';
import { exception } from './exceptions';
import { intConvert, realConvert, type IntegerType, type LongType, type RealType } from './number-parse';
import { singleFromLong } from './overflow';

type NarrowType = Exclude<IntegerType, LongType>;

/** How .NET's overflow message names each type, the char included. */
const SPOKEN: Record<IntegerType | 'char', string> = {
  sbyte: 'a signed byte',
  byte: 'an unsigned byte',
  short: 'an Int16',
  ushort: 'a UInt16',
  int: 'an Int32',
  uint: 'a UInt32',
  long: 'an Int64',
  ulong: 'a UInt64',
  char: 'a character',
};

/** The .NET name of each type, as an invalid cast names it. */
const NAMED: Record<IntegerType | RealType | 'char' | 'bool', string> = {
  sbyte: 'SByte',
  byte: 'Byte',
  short: 'Int16',
  ushort: 'UInt16',
  int: 'Int32',
  uint: 'UInt32',
  long: 'Int64',
  ulong: 'UInt64',
  double: 'Double',
  single: 'Single',
  char: 'Char',
  bool: 'Boolean',
};

const RANGES: Record<IntegerType | 'char', [low: bigint, high: bigint]> = {
  sbyte: [-128n, 127n],
  byte: [0n, 255n],
  short: [-32768n, 32767n],
  ushort: [0n, 65535n],
  int: [-2147483648n, 2147483647n],
  uint: [0n, 4294967295n],
  long: [-9223372036854775808n, 9223372036854775807n],
  ulong: [0n, 18446744073709551615n],
  char: [0n, 65535n],
};

function tooLargeOrSmall(type: IntegerType | 'char'): Error {
  return exception('System.OverflowException', `Value was either too large or too small for ${SPOKEN[type]}.`);
}

function arithmeticOverflow(): Error {
  return exception('System.OverflowException', 'Arithmetic operation resulted in an overflow.');
}

/** What converting a value of no convertible type throws: a date's own conversions all refuse. */
function refused(value: object, to: keyof typeof NAMED): Error {
  if (value instanceof DateTime) {
    return exception('System.InvalidCastException', `Invalid cast from 'DateTime' to '${NAMED[to]}'.`);
  }
  const name = timeTypeOf(value) ?? value.constructor?.name ?? 'Object';
  return exception('System.InvalidCastException', `Unable to cast object of type '${name}' to type 'System.IConvertible'.`);
}

/** An integer inside its type's range, held as the type is, or .NET's overflow. */
function ranged(value: bigint, type: IntegerType): number | bigint {
  const [low, high] = RANGES[type];
  if (value < low || value > high) throw tooLargeOrSmall(type);
  return type === 'long' || type === 'ulong' ? value : Number(value);
}

/** `Convert.To<type>` of an integer of another width. */
export function integerFrom(value: number | bigint, type: LongType): bigint;
export function integerFrom(value: number | bigint, type: NarrowType): number;
export function integerFrom(value: number | bigint, type: IntegerType): number | bigint;
export function integerFrom(value: number | bigint, type: IntegerType): number | bigint {
  return ranged(BigInt(value), type);
}

/**
 * `Convert.To<type>` of a double, and of a float, which .NET widens first: rounded half to even. The
 * bounds are .NET's, tested before the rounding, so a NaN fails each of them.
 */
export function integerFromDouble(value: number, type: LongType): bigint;
export function integerFromDouble(value: number, type: NarrowType): number;
export function integerFromDouble(value: number, type: IntegerType): number | bigint;
export function integerFromDouble(value: number, type: IntegerType): number | bigint {
  if (type === 'long' || type === 'ulong') {
    const rounded = round(value);
    const fits =
      type === 'long'
        ? rounded >= -9223372036854775808 && rounded < 9223372036854775808
        : rounded > -1 && rounded < 18446744073709551616;
    if (!fits) throw arithmeticOverflow();
    return BigInt(rounded);
  }
  if (type === 'uint') {
    if (!(value >= -0.5 && value < 4294967295.5)) throw tooLargeOrSmall('uint');
    return round(value) + 0;
  }
  if (!(value >= -2147483648.5 && value < 2147483647.5)) throw tooLargeOrSmall('int');
  const whole = round(value) | 0;
  return type === 'int' ? whole : ranged(BigInt(whole), type);
}

/** `Convert.To<type>` of a decimal: `decimal.Round`, then the range. */
export function integerFromDecimal(value: Decimal, type: LongType): bigint;
export function integerFromDecimal(value: Decimal, type: NarrowType): number;
export function integerFromDecimal(value: Decimal, type: IntegerType): number | bigint;
export function integerFromDecimal(value: Decimal, type: IntegerType): number | bigint {
  const rounded = value.round();
  return ranged(rounded.mantissa / 10n ** BigInt(rounded.scale), type);
}

/**
 * `Convert.To<type>` of an `object`: a null is 0, a text reads as the type's Parse reads it, a bool is
 * 1 or 0, and a number converts as the type it holds does.
 */
export function integerFromObject(value: unknown, type: LongType, invariant?: boolean): bigint;
export function integerFromObject(value: unknown, type: NarrowType, invariant?: boolean): number;
export function integerFromObject(value: unknown, type: IntegerType, invariant?: boolean): number | bigint;
export function integerFromObject(value: unknown, type: IntegerType, invariant = false): number | bigint {
  const zero = type === 'long' || type === 'ulong' ? 0n : 0;
  if (value == null) return zero;
  if (typeof value === 'string') return intConvert(value, type, invariant);
  if (typeof value === 'boolean') return ranged(value ? 1n : 0n, type);
  if (typeof value === 'bigint') return ranged(value, type);
  if (typeof value === 'number') {
    return Number.isInteger(value) ? ranged(BigInt(value), type) : integerFromDouble(value, type);
  }
  if (value instanceof Decimal) return integerFromDecimal(value, type);
  throw refused(value as object, type);
}

/** `Convert.ToDouble` and `ToSingle` of an `object`, read as {@link integerFromObject} reads one. */
export function realFromObject(value: unknown, type: RealType, invariant = false): number {
  if (value == null) return 0;
  if (typeof value === 'string') return realConvert(value, type, invariant);
  if (typeof value === 'bigint' && type === 'single') return singleFromLong(value);
  let number: number;
  if (typeof value === 'boolean') number = value ? 1 : 0;
  else if (typeof value === 'number') number = value;
  else if (typeof value === 'bigint') number = Number(value);
  else if (value instanceof Decimal) number = value.toNumber();
  else throw refused(value as object, type);
  return type === 'single' ? Math.fround(number) : number;
}

/** `Convert.ToBoolean` of an `object`: a null is false, a text reads as `bool.Parse`, a number is
 * whether it is not zero, a NaN included. */
export function booleanFromObject(value: unknown): boolean {
  if (value == null) return false;
  if (typeof value === 'string') return boolConvert(value);
  if (typeof value === 'boolean') return value;
  if (typeof value === 'number') return value !== 0;
  if (typeof value === 'bigint') return value !== 0n;
  if (value instanceof Decimal) return value.mantissa !== 0n;
  throw refused(value as object, 'bool');
}

/** `Convert.ToChar` of an integer: its code unit, in the char's range. */
export function charFrom(value: number | bigint): string {
  const big = BigInt(value);
  const [low, high] = RANGES.char;
  if (big < low || big > high) throw tooLargeOrSmall('char');
  return String.fromCharCode(Number(big));
}

/** `Convert.ToChar(string)`: a text of exactly one char. */
export function charFromText(value: string | null): string {
  if (value == null) {
    throw exception('System.ArgumentNullException', "Value cannot be null. (Parameter 'value')");
  }
  if (value.length !== 1) throw exception('System.FormatException', 'String must be exactly one character long.');
  return value;
}

/** `Convert.ToChar` of an `object`: a null is the zero char, and an integer its code unit. A bool, a
 * real number and a decimal have no conversion to a char. */
export function charFromObject(value: unknown): string {
  if (value == null) return '\0';
  if (typeof value === 'string') return charFromText(value);
  if (typeof value === 'bigint' || (typeof value === 'number' && Number.isInteger(value))) return charFrom(value);
  const from = typeof value === 'boolean' ? 'Boolean' : typeof value === 'number' ? 'Double' : value instanceof Decimal ? 'Decimal' : null;
  if (from === null) throw refused(value as object, 'char');
  throw exception('System.InvalidCastException', `Invalid cast from '${from}' to 'Char'.`);
}
