/**
 * A number as the exact decimal it holds, which is what .NET formats.
 *
 * .NET writes a formatted double from its EXACT binary value: `(0.1).ToString("F20")` is
 * `0.10000000000000000555` and `(1.2345678901234568E+19).ToString("N0")` ends in `567,168`. `Intl`
 * and `toFixed` start from the shortest text that reads back instead (`0.1`, `…567,000`), so the
 * two agree only until a digit past that text matters. Every double is a finite binary fraction,
 * so its decimal expansion is finite too, and BigInt writes it exactly: `m × 2^e` is `m × 5^-e`
 * digits, `-e` places after the point. A long's BigInt and a decimal's mantissa are exact already.
 */

/** The digits of a number and where its point goes: `digits × 10^exponent`. The digits carry no
 * leading zeros and no trailing ones (`"0"` is zero), and the sign is kept apart, because a
 * double's zero has one. */
export interface ExactDecimal {
  readonly negative: boolean;
  readonly digits: string;
  readonly exponent: number;
}

/** How .NET rounds a formatted number that lies exactly halfway: a double's and a float's digits
 * to even, a decimal's and an integer's away from zero (measured on .NET 10, #393). The names are
 * `Intl.NumberFormat`'s, which takes the same choice. */
export type Tie = 'halfEven' | 'halfExpand';

const bits = new DataView(new ArrayBuffer(8));
const fives = new Map<number, bigint>();

/** 5^n, kept: a small double's expansion asks for up to 5^1074, and a page formats many. */
function fivePower(n: number): bigint {
  let power = fives.get(n);
  if (power === undefined) {
    power = 5n ** BigInt(n);
    fives.set(n, power);
  }
  return power;
}

/** The canonical form: no trailing zeros in the digits, and zero as `"0"` at exponent 0. */
function canonical(negative: boolean, digits: string, exponent: number): ExactDecimal {
  let end = digits.length;
  while (end > 1 && digits[end - 1] === '0') end--;
  const trimmed = digits.slice(0, end);
  return trimmed === '0'
    ? { negative, digits: '0', exponent: 0 }
    : { negative, digits: trimmed, exponent: exponent + digits.length - end };
}

/** A finite double's exact decimal, a float's included: a single is a double that already holds
 * the float's value. Negative zero keeps its sign, as .NET's formatting does. */
export function exactOfDouble(value: number): ExactDecimal {
  bits.setFloat64(0, value);
  const high = bits.getUint32(0);
  const negative = high >>> 31 === 1;
  const biased = (high >>> 20) & 0x7ff;
  const fraction = (BigInt(high & 0xfffff) << 32n) | BigInt(bits.getUint32(4));
  // A subnormal has no implicit bit and the smallest exponent; everything else carries both.
  const mantissa = biased === 0 ? fraction : fraction | (1n << 52n);
  const power = (biased === 0 ? 1 : biased) - 1075;
  if (mantissa === 0n) return { negative, digits: '0', exponent: 0 };
  return power >= 0
    ? canonical(negative, (mantissa << BigInt(power)).toString(), 0)
    : canonical(negative, (mantissa * fivePower(-power)).toString(), power);
}

/** A long's or a ulong's BigInt, whose digits are its own. */
export function exactOfBigInt(value: bigint): ExactDecimal {
  const negative = value < 0n;
  return canonical(negative, (negative ? -value : value).toString(), 0);
}

/** A decimal's `mantissa / 10^scale`. A decimal zero has no sign to keep. */
export function exactOfScaled(mantissa: bigint, scale: number): ExactDecimal {
  const negative = mantissa < 0n;
  return canonical(negative, (negative ? -mantissa : mantissa).toString(), -scale);
}

/** Whether the value is zero, whichever sign it carries. */
export function isZero(value: ExactDecimal): boolean {
  return value.digits === '0';
}

/** The value as plain decimal text, `-0.000125` or `1234500`: what `Intl.NumberFormat` formats
 * exactly when it is handed a string. */
export function plainText(value: ExactDecimal): string {
  const { digits, exponent } = value;
  const sign = value.negative ? '-' : '';
  if (exponent >= 0) return sign + digits + '0'.repeat(isZero(value) ? 0 : exponent);
  const point = digits.length + exponent;
  return point > 0
    ? `${sign}${digits.slice(0, point)}.${digits.slice(point)}`
    : `${sign}0.${'0'.repeat(-point)}${digits}`;
}

/** The value times 10^places, exactly: what a percent format shows, before it rounds. */
export function scaled(value: ExactDecimal, places: number): ExactDecimal {
  return isZero(value) ? value : { ...value, exponent: value.exponent + places };
}

/** Rounded to `count` significant digits: `digits` has exactly `count` of them, and `scientific`
 * is the power of ten of the first one (`1.23E+004` is `123` at 4). */
export interface Significant {
  readonly negative: boolean;
  readonly digits: string;
  readonly scientific: number;
}

/**
 * Rounds to `count` significant digits (at least one) by the tie rule given, on the exact digits:
 * a half is a 5 with nothing after it, and anything past the 5 moves the digit up whatever the
 * rule. A carry that runs off the front (`9.99` to `10.0`) moves the point instead.
 */
export function roundSignificant(value: ExactDecimal, count: number, tie: Tie): Significant {
  const width = Math.max(1, count);
  if (isZero(value)) return { negative: value.negative, digits: '0'.repeat(width), scientific: 0 };
  const { digits } = value;
  let scientific = digits.length - 1 + value.exponent;
  if (digits.length <= width) {
    return { negative: value.negative, digits: digits.padEnd(width, '0'), scientific };
  }
  let kept = digits.slice(0, width);
  const next = digits.charCodeAt(width) - 48;
  const beyond = digits.length > width + 1;
  const up =
    next > 5 ||
    (next === 5 && (beyond || tie === 'halfExpand' || (kept.charCodeAt(width - 1) - 48) % 2 === 1));
  if (up) {
    const carried = (BigInt(kept) + 1n).toString();
    if (carried.length > width) {
      scientific++;
      kept = carried.slice(0, width);
    } else {
      kept = carried;
    }
  }
  return { negative: value.negative, digits: kept, scientific };
}
