import {
  NULL_TEXT,
  NumberStyles,
  badFormat,
  isWhite,
  readNumber,
  validateIntegerStyles,
  validateRealStyles,
  type NumberText,
} from './number-grammar';
import { equals } from './string-statics';
import { trim } from './white-space';

/**
 * `int.Parse`, `double.Parse` and the other integer and binary floating-point readers, and
 * `Convert.ToInt32` and its siblings over text, as .NET reads text in the invariant culture the twin
 * always reads in (EQ2110 names what a culture changes).
 *
 * `parseInt` and `parseFloat` read the longest prefix that looks like a number and stop there:
 * "12abc" was 12, "1e3" was 1 as an int, "0x1F" was 31 and "1,5" was 1. .NET reads the WHOLE text,
 * under the NumberStyles the call names or the type's own (`Integer` for an integer, `Float` with
 * `AllowThousands` for a real), and refuses what the style does not allow. So the digits come from
 * `readNumber`, the grammar `decimal.Parse` reads, and each type holds them its own way:
 *
 * - An integer takes the digits exactly and refuses what its width cannot hold. A fraction that is
 *   not zero is an overflow, not a format error, and a zero read with a decimal point keeps its
 *   sign, so an unsigned type reads "-0" and overflows on "-0.0". Hex and binary are the type's
 *   BITS, with no sign: "FFFFFFFF" is -1 for an int. A long or a ulong is a BigInt, as every long is
 *   on this side.
 * - A double is the one nearest the digits, which `Number()` answers for any text. Past the largest
 *   it is infinity rather than an overflow, as it has been since .NET Core 3.0.
 * - A float is the single nearest the DIGITS. Through a double it rounds twice, and a value just past
 *   the midpoint between two singles lands ON the midpoint at the first step, so the second goes to
 *   the even neighbour; that one case is settled against the digits themselves.
 * - Text the grammar refuses may still be one of the invariant culture's symbols for a real,
 *   whatever the style: "Infinity", "-Infinity" or "NaN", case-blind as an ordinal comparison is,
 *   after trimming what `char.IsWhiteSpace` calls white (which is not `trim()`'s set), and "+" before
 *   either or "-" before NaN.
 *
 * Every refusal throws .NET's exception with .NET's words, in .NET's order: Parse refuses a null
 * text before it looks at the style, where TryParse checks the style first.
 */

/** The integer types text is read into, as the compiler names them. */
export type IntegerType = 'byte' | 'sbyte' | 'short' | 'ushort' | 'int' | 'uint' | 'long' | 'ulong';

/** The widths a long holds, which answer a BigInt; every other integer is a number. */
export type LongType = 'long' | 'ulong';

/** The binary floating-point types: a double, and a float, which this side calls a single. */
export type RealType = 'double' | 'single';

interface Width {
  bits: number;
  signed: boolean;
  /** How .NET's OverflowException names the type. */
  name: string;
}

const WIDTHS: Readonly<Record<IntegerType, Width>> = {
  byte: { bits: 8, signed: false, name: 'an unsigned byte' },
  sbyte: { bits: 8, signed: true, name: 'a signed byte' },
  short: { bits: 16, signed: true, name: 'an Int16' },
  ushort: { bits: 16, signed: false, name: 'a UInt16' },
  int: { bits: 32, signed: true, name: 'an Int32' },
  uint: { bits: 32, signed: false, name: 'a UInt32' },
  long: { bits: 64, signed: true, name: 'an Int64' },
  ulong: { bits: 64, signed: false, name: 'a UInt64' },
};

/** No integer type holds a number of more digits than a ulong's 20. */
const MOST_DIGITS = 20;

/** `double.Parse` and `float.Parse` read `NumberStyles.Float | NumberStyles.AllowThousands`. */
const REAL_STYLES = NumberStyles.Float | NumberStyles.AllowThousands;

/** Why a text is not an integer: .NET's `ParsingStatus.Failed` and `ParsingStatus.Overflow`. */
const FORMAT = 'format';
const OVERFLOW = 'overflow';
type Refusal = typeof FORMAT | typeof OVERFLOW;

/**
 * The integer `text` holds under `styles`, or why it holds none. Hex and binary have their own
 * walk (.NET's `TryParseBinaryIntegerHexOrBinaryNumberStyle`); every other style reads the number
 * grammar, whose answer for the `Integer` style is the one .NET's fast path gives.
 */
function readInteger(text: string, styles: number, width: Width): bigint | Refusal {
  if ((styles & NumberStyles.AllowHexSpecifier) !== 0) return readBits(text, styles, 4, width);
  if ((styles & NumberStyles.AllowBinarySpecifier) !== 0) return readBits(text, styles, 1, width);
  const number = readNumber(text, styles);
  if (number === undefined) return FORMAT;
  return integerOf(number, width) ?? OVERFLOW;
}

/**
 * The integer the digits are, or `undefined` where the width cannot hold it (.NET's
 * `TryNumberBufferToBinaryInteger`): a fraction that is not zero, a sign an unsigned type cannot
 * carry, or a value past the range.
 */
function integerOf(
  { negative, digits, scale, point }: NumberText,
  width: Width,
): bigint | undefined {
  if (digits.length === 0) return negative && point && !width.signed ? undefined : 0n;
  if (scale <= 0 || scale > MOST_DIGITS || /[1-9]/.test(digits.slice(scale))) return undefined;
  if (negative && !width.signed) return undefined;
  const magnitude = BigInt(digits.slice(0, scale).padEnd(scale, '0'));
  const value = negative ? -magnitude : magnitude;
  return fits(value, width) ? value : undefined;
}

function fits(value: bigint, { bits, signed }: Width): boolean {
  return (signed ? BigInt.asIntN(bits, value) : BigInt.asUintN(bits, value)) === value;
}

function isBitDigit(ch: number, bitsPerDigit: 1 | 4): boolean {
  if (bitsPerDigit === 1) return ch === 0x30 || ch === 0x31;
  return (ch >= 0x30 && ch <= 0x39) || (ch >= 0x41 && ch <= 0x46) || (ch >= 0x61 && ch <= 0x66);
}

/**
 * Hex or binary digits and the whitespace the style allows around them, then only `\0`s: the
 * type's bits, the top one its sign. A digit past what the width holds is an overflow, unless the
 * text is not a number at all, which .NET reports first.
 */
function readBits(
  text: string,
  styles: number,
  bitsPerDigit: 1 | 4,
  width: Width,
): bigint | Refusal {
  const end = text.length;
  let p = 0;
  if ((styles & NumberStyles.AllowLeadingWhite) !== 0) {
    while (p < end && isWhite(text.charCodeAt(p))) p++;
  }
  const start = p;
  while (p < end && isBitDigit(text.charCodeAt(p), bitsPerDigit)) p++;
  if (p === start) return FORMAT;
  const digits = text.slice(start, p).replace(/^0+/, '');
  if (p < end && isWhite(text.charCodeAt(p))) {
    if ((styles & NumberStyles.AllowTrailingWhite) === 0) return FORMAT;
    while (p < end && isWhite(text.charCodeAt(p))) p++;
  }
  for (; p < end; p++) {
    if (text.charCodeAt(p) !== 0) return FORMAT;
  }
  if (digits.length * bitsPerDigit > width.bits) return OVERFLOW;
  const bits = digits.length === 0 ? 0n : BigInt((bitsPerDigit === 4 ? '0x' : '0b') + digits);
  return width.signed ? BigInt.asIntN(width.bits, bits) : bits;
}

/** The value as the type holds it on this side: a BigInt for a long, a number for the rest. */
function held(value: bigint, type: IntegerType): number | bigint {
  return WIDTHS[type].bits === 64 ? value : Number(value);
}

function overflow(type: IntegerType): Error {
  return new Error(`Value was either too large or too small for ${WIDTHS[type].name}.`);
}

/**
 * `int.Parse` and the other integer types' Parse, over text, under the call's styles or
 * `NumberStyles.Integer`. The null text is refused first, then the style, then the text: a
 * FormatException where it is not a number, an OverflowException where the type cannot hold it.
 */
export function intParse(text: string | null | undefined, type: LongType, styles?: number): bigint;
export function intParse(
  text: string | null | undefined,
  type: Exclude<IntegerType, LongType>,
  styles?: number,
): number;
export function intParse(
  text: string | null | undefined,
  type: IntegerType,
  styles: number = NumberStyles.Integer,
): number | bigint {
  if (text == null) throw new Error(NULL_TEXT);
  validateIntegerStyles(styles);
  return parsed(text, type, styles);
}

/** The text read under a style already checked: the value, or the exception .NET throws. */
function parsed(text: string, type: IntegerType, styles: number): number | bigint {
  const value = readInteger(text, styles, WIDTHS[type]);
  if (value === FORMAT) throw badFormat(text);
  if (value === OVERFLOW) throw overflow(type);
  return held(value, type);
}

/**
 * The integer types' TryParse: the value, or `undefined` where Parse would refuse the text,
 * including a null one. A style the type cannot read still throws, before the text is looked at.
 */
export function intTryParse(
  text: string | null | undefined,
  type: LongType,
  styles?: number,
): bigint | undefined;
export function intTryParse(
  text: string | null | undefined,
  type: Exclude<IntegerType, LongType>,
  styles?: number,
): number | undefined;
export function intTryParse(
  text: string | null | undefined,
  type: IntegerType,
  styles: number = NumberStyles.Integer,
): number | bigint | undefined {
  validateIntegerStyles(styles);
  if (text == null) return undefined;
  const value = readInteger(text, styles, WIDTHS[type]);
  return typeof value === 'bigint' ? held(value, type) : undefined;
}

/** `Convert.ToInt32(string)` and its siblings: a null text is 0, and any other is Parse's. */
export function intConvert(text: string | null | undefined, type: LongType): bigint;
export function intConvert(
  text: string | null | undefined,
  type: Exclude<IntegerType, LongType>,
): number;
export function intConvert(text: string | null | undefined, type: IntegerType): number | bigint {
  if (text == null) return held(0n, type);
  return parsed(text, type, NumberStyles.Integer);
}

/** The double nearest the digits, a zero keeping its sign. */
function doubleOf({ negative, digits, scale }: NumberText): number {
  if (digits.length === 0) return negative ? -0 : 0;
  return Number(`${negative ? '-' : ''}0.${digits}e${scale}`);
}

const singleBox = new Float32Array(1);
const singleBits = new Uint32Array(singleBox.buffer);

/** The single one step away from a finite, non-negative single: up, or down. */
function stepSingle(value: number, up: boolean): number {
  singleBox[0] = value;
  singleBits[0] += up ? 1 : -1;
  return singleBox[0];
}

const doubleBits = new DataView(new ArrayBuffer(8));

/** Whether the digits' magnitude is above, at or below a positive, finite double: 1, 0 or -1. */
function compareDigits(digits: string, scale: number, value: number): number {
  doubleBits.setFloat64(0, value);
  const bits = doubleBits.getBigUint64(0);
  const exponent = Number((bits >> 52n) & 0x7ffn);
  const fraction = bits & 0xfffffffffffffn;
  // value = mantissa × 2^power, and the digits are D × 10^(scale - length).
  let right = exponent === 0 ? fraction : fraction | 0x10000000000000n;
  const power = (exponent === 0 ? 1 : exponent) - 1075;
  let left = BigInt(digits);
  const tens = scale - digits.length;
  if (tens >= 0) left *= 10n ** BigInt(tens);
  else right *= 10n ** BigInt(-tens);
  if (power >= 0) right <<= BigInt(power);
  else left <<= BigInt(-power);
  return left === right ? 0 : left > right ? 1 : -1;
}

/**
 * The single nearest the digits, rounded once. The double is the nearest double, and a single
 * midpoint is itself a double, so rounding the double to a single can only go wrong where the
 * double IS a midpoint and the digits are not: then they say which neighbour is nearer. Past the
 * largest single the midpoint is the one with 2^128, where a tie rounds to infinity.
 */
function singleOf(number: NumberText): number {
  const double = doubleOf(number);
  const single = Math.fround(double);
  if (single === double) return single;
  const magnitude = Math.abs(double);
  const rounded = Math.abs(single);
  const below = rounded < magnitude ? rounded : stepSingle(rounded, false);
  const above = rounded < magnitude ? stepSingle(rounded, true) : rounded;
  const midpoint = (below + (above === Infinity ? 2 ** 128 : above)) / 2;
  if (midpoint !== magnitude) return single;
  const side = compareDigits(number.digits, number.scale, magnitude);
  if (side === 0) return single;
  const nearest = side > 0 ? above : below;
  return number.negative ? -nearest : nearest;
}

function sameIgnoringCase(text: string, symbol: string): boolean {
  return equals(text, symbol, 'ordinalIgnoreCase');
}

/** The invariant culture's symbols, where the grammar refused the text (.NET's `TryParseFloat`). */
function readSymbol(text: string): number | undefined {
  const trimmed = trim(text);
  if (sameIgnoringCase(trimmed, 'Infinity')) return Infinity;
  if (sameIgnoringCase(trimmed, '-Infinity')) return -Infinity;
  if (sameIgnoringCase(trimmed, 'NaN')) return NaN;
  if (trimmed.startsWith('+')) {
    const unsigned = trimmed.slice(1);
    if (sameIgnoringCase(unsigned, 'Infinity')) return Infinity;
    if (sameIgnoringCase(unsigned, 'NaN')) return NaN;
    return undefined;
  }
  if (trimmed.startsWith('-') && sameIgnoringCase(trimmed.slice(1), 'NaN')) return NaN;
  return undefined;
}

function readReal(text: string, styles: number, type: RealType): number | undefined {
  const number = readNumber(text, styles);
  if (number === undefined) return readSymbol(text);
  return type === 'single' ? singleOf(number) : doubleOf(number);
}

/**
 * `double.Parse` and `float.Parse` over text, under the call's styles or `NumberStyles.Float` with
 * `AllowThousands`. The null text is refused first, then the style, then text that is neither a
 * number nor a symbol, with a FormatException. Nothing overflows: past the largest is infinity.
 */
export function realParse(
  text: string | null | undefined,
  type: RealType,
  styles: number = REAL_STYLES,
): number {
  if (text == null) throw new Error(NULL_TEXT);
  validateRealStyles(styles);
  const value = readReal(text, styles, type);
  if (value === undefined) throw badFormat(text);
  return value;
}

/**
 * `double.TryParse` and `float.TryParse`: the value, or `undefined` where Parse would refuse the
 * text, including a null one. A style a real cannot read still throws, before the text is looked at.
 */
export function realTryParse(
  text: string | null | undefined,
  type: RealType,
  styles: number = REAL_STYLES,
): number | undefined {
  validateRealStyles(styles);
  if (text == null) return undefined;
  return readReal(text, styles, type);
}

/** `Convert.ToDouble(string)` and `Convert.ToSingle(string)`: a null text is 0, any other Parse's. */
export function realConvert(text: string | null | undefined, type: RealType): number {
  return text == null ? 0 : realParse(text, type);
}
