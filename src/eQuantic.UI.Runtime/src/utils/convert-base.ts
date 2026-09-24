/**
 * `Convert.ToInt32(value, fromBase)` and its seven siblings, and `Convert.ToString(value, toBase)`:
 * an integer read from, or written as, text in base 2, 8, 10 or 16, the way .NET does it.
 *
 * `parseInt` is not that. .NET reads a base other than 10 as the BITS of the type, so "ffffffff"
 * is -1 for an int and 4294967295 for a uint; takes a `0x` prefix in base 16 and a minus sign only
 * in base 10; refuses a space, a stray character and a value past the type; and reads a null as 0,
 * after checking the base. Writing is the same bits the other way: `Convert.ToString(-1, 16)` is
 * "ffffffff" where `(-1).toString(16)` is "-1".
 *
 * The reading is a port of .NET 10's `ParseNumbers.StringToInt` and `StringToLong` with
 * `IsTight` (what every `Convert` overload passes), and every refusal carries .NET's words.
 */

/** The integer types a base conversion reads into, as the compiler names them. */
export type BaseTarget = 'byte' | 'sbyte' | 'short' | 'ushort' | 'int' | 'uint' | 'long' | 'ulong';

const INVALID_BASE = 'Invalid Base.';
const EMPTY =
  'Specified argument was out of the range of valid values. ' +
  "(Parameter 'Index was out of range. Must be non-negative and less than the size of the collection.')";
const NEGATIVE_IN_BASE = 'String cannot contain a minus sign if the base is not 10.';
const NEGATIVE_UNSIGNED =
  'The string was being parsed as an unsigned number and could not have a negative sign.';
const NO_DIGITS = 'Could not find any recognizable digits.';
const EXTRA_JUNK = 'Additional non-parsable characters are at the end of the string.';

function overflow(type: string): Error {
  return new Error(`Value was either too large or too small for ${type}.`);
}

function checkBase(base: number): void {
  if (base !== 2 && base !== 8 && base !== 10 && base !== 16) throw new Error(INVALID_BASE);
}

/** A digit's value in the radix, or -1: .NET's `IsDigit`, which knows letters of either case. */
function digit(code: number, radix: number): number {
  let value: number;
  if (code >= 48 && code <= 57) value = code - 48;
  else if (code >= 65 && code <= 90) value = code - 55;
  else if (code >= 97 && code <= 122) value = code - 87;
  else return -1;
  return value < radix ? value : -1;
}

interface Head {
  /** Where the digits start. */
  index: number;
  sign: 1 | -1;
}

/** The sign and the `0x` prefix, in .NET's order: a minus sign is refused outside base 10 before
 * an unsigned target refuses it. */
function head(s: string, radix: number, unsigned: boolean): Head {
  if (s.length === 0) throw new Error(EMPTY);
  let index = 0;
  let sign: 1 | -1 = 1;
  if (s[0] === '-') {
    if (radix !== 10) throw new Error(NEGATIVE_IN_BASE);
    if (unsigned) throw new Error(NEGATIVE_UNSIGNED);
    sign = -1;
    index = 1;
  } else if (s[0] === '+') {
    index = 1;
  }
  if (
    radix === 16 &&
    index + 1 < s.length &&
    s[index] === '0' &&
    (s[index + 1] === 'x' || s[index + 1] === 'X')
  ) {
    index += 2;
  }
  return { index, sign };
}

/** Nothing read, or something left after the digits. */
function tail(s: string, start: number, end: number): void {
  if (end === start) throw new Error(NO_DIGITS);
  if (end < s.length) throw new Error(EXTRA_JUNK);
}

/** `ParseNumbers`' flags that change a read: an unsigned target, and the 8- and 16-bit ranges. */
const UNSIGNED = 1;
const I1 = 2;
const I2 = 4;

/** `ParseNumbers.StringToInt`: the 32-bit read, answering the int .NET's does. */
function stringToInt(s: string, r: number, flags: number): number {
  const unsigned = (flags & UNSIGNED) !== 0;
  const { index, sign } = head(s, r, unsigned);
  let i = index;
  // The accumulator is the uint .NET keeps: `>>> 0` is its wrap.
  let result = 0;
  if (r === 10 && !unsigned) {
    const max = Math.floor(0x7fffffff / 10);
    for (let d; i < s.length && (d = digit(s.charCodeAt(i), r)) >= 0; i++) {
      if (result > max || result > 0x7fffffff) throw overflow('an Int32');
      result = (result * r + d) >>> 0;
    }
    if (result > 0x7fffffff && result !== 0x80000000) throw overflow('an Int32');
  } else {
    const max = Math.floor(0xffffffff / r);
    for (let d; i < s.length && (d = digit(s.charCodeAt(i), r)) >= 0; i++) {
      if (result > max) throw overflow('a UInt32');
      const next = (result * r + d) >>> 0;
      if (next < result) throw overflow('a UInt32');
      result = next;
    }
  }
  tail(s, index, i);
  if ((flags & I1) !== 0) {
    if (result > 0xff) throw overflow('a signed byte');
  } else if ((flags & I2) !== 0) {
    if (result > 0xffff) throw overflow('an Int16');
  } else if (result === 0x80000000 && sign === 1 && r === 10 && !unsigned) {
    throw overflow('an Int32');
  }
  // `(int)result`, times the sign in base 10 only, with an int's wrap: -int.MinValue is itself.
  return r === 10 ? Math.imul(result | 0, sign) : result | 0;
}

/** `ParseNumbers.StringToLong`: the 64-bit read, answering the long .NET's does. */
function stringToLong(s: string, r: number, unsigned: boolean): bigint {
  const { index, sign } = head(s, r, unsigned);
  const big = BigInt(r);
  let i = index;
  let result = 0n;
  if (r === 10 && !unsigned) {
    const max = 0x7fffffffffffffffn / 10n;
    for (let d; i < s.length && (d = digit(s.charCodeAt(i), r)) >= 0; i++) {
      if (result > max || result > 0x7fffffffffffffffn) throw overflow('an Int64');
      result = BigInt.asUintN(64, result * big + BigInt(d));
    }
    if (result > 0x7fffffffffffffffn && result !== 0x8000000000000000n) throw overflow('an Int64');
  } else {
    const max = 0xffffffffffffffffn / big;
    for (let d; i < s.length && (d = digit(s.charCodeAt(i), r)) >= 0; i++) {
      if (result > max) throw overflow('a UInt64');
      const next = BigInt.asUintN(64, result * big + BigInt(d));
      if (next < result) throw overflow('a UInt64');
      result = next;
    }
  }
  tail(s, index, i);
  if (result === 0x8000000000000000n && sign === 1 && r === 10 && !unsigned)
    throw overflow('an Int64');
  const value = BigInt.asIntN(64, result);
  return r === 10 ? BigInt.asIntN(64, value * BigInt(sign)) : value;
}

/**
 * `Convert.To{target}(value, fromBase)`: the base is checked first, then a null is 0, then the
 * text is read as .NET's overload for that type reads it. A long or a ulong answers a BigInt, as
 * every long does on this side, and the overloads say which, so a type-checked twin that keeps an
 * int in a `number` compiles.
 */
export function fromBase(
  value: string | null | undefined,
  base: number,
  target: 'long' | 'ulong',
): bigint;
export function fromBase(
  value: string | null | undefined,
  base: number,
  target: 'byte' | 'sbyte' | 'short' | 'ushort' | 'int' | 'uint',
): number;
export function fromBase(
  value: string | null | undefined,
  base: number,
  target: BaseTarget,
): number | bigint {
  checkBase(base);
  const wide = target === 'long' || target === 'ulong';
  if (value === null || value === undefined) return wide ? 0n : 0;
  switch (target) {
    case 'byte': {
      const r = stringToInt(value, base, UNSIGNED);
      if (r >>> 0 > 0xff) throw overflow('an unsigned byte');
      return r;
    }
    case 'sbyte': {
      const r = stringToInt(value, base, I1);
      if (base !== 10 && r <= 0xff) return (r << 24) >> 24;
      if (r < -128 || r > 127) throw overflow('a signed byte');
      return r;
    }
    case 'short': {
      const r = stringToInt(value, base, I2);
      if (base !== 10 && r <= 0xffff) return (r << 16) >> 16;
      if (r < -32768 || r > 32767) throw overflow('an Int16');
      return r;
    }
    case 'ushort': {
      const r = stringToInt(value, base, UNSIGNED);
      if (r >>> 0 > 0xffff) throw overflow('a UInt16');
      return r;
    }
    case 'int':
      return stringToInt(value, base, 0);
    case 'uint':
      return stringToInt(value, base, UNSIGNED) >>> 0;
    case 'long':
      return stringToLong(value, base, false);
    case 'ulong':
      return BigInt.asUintN(64, stringToLong(value, base, true));
  }
}

/**
 * `Convert.ToString(value, toBase)` for an integer of `bits` width (a byte is written as the int
 * it widens to): base 10 is the signed number, and 2, 8 and 16 are the type's bits, in lower case.
 */
export function toBase(value: number | bigint, base: number, bits: 16 | 32 | 64): string {
  checkBase(base);
  if (base === 10) return String(value);
  return BigInt.asUintN(bits, BigInt(value)).toString(base);
}
