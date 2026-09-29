import type { MidpointRounding } from './dotnet-math';
import {
  NULL_TEXT,
  NumberStyles,
  badFormat,
  readNumber,
  validateRealStyles,
  type NumberText,
} from './number-grammar';

/** The largest mantissa a decimal holds: 96 bits. */
const MAX_MANTISSA = (1n << 96n) - 1n;
/** The most digits a decimal keeps after the point. */
const MAX_SCALE = 28;
/** Past this many digits before the point, no decimal holds the number. */
const PRECISION = 29;
/** A mantissa that rounded past 96 bits, a digit shorter: 2^96 / 10, rounded up. */
const CARRIED = 7922816251426433759354395034n;
const OVERFLOW = 'Value was either too large or too small for a Decimal.';

/**
 * Each power of ten as the double nearest it, as .NET's `DecCalc.DoublePowers10` holds it. Read
 * from text, because `10 ** 23` is not always that double.
 */
const DOUBLE_POWERS_10 = Array.from({ length: MAX_SCALE + 1 }, (_, power) => Number(`1e${power}`));
const bits = new DataView(new ArrayBuffer(8));

/**
 * Whether a rounding moves the truncated quotient one step away from zero, per .NET's
 * `MidpointRounding`: ToEven and AwayFromZero decide a half by `twice` the remainder against the
 * divisor, and the three directed modes move every value that has a remainder.
 */
const STEPS: Readonly<
  Record<
    MidpointRounding,
    (quotient: bigint, remainder: bigint, twice: bigint, divisor: bigint) => boolean
  >
> = {
  toEven: (quotient, _remainder, twice, divisor) =>
    twice > divisor || (twice === divisor && quotient % 2n !== 0n),
  awayFromZero: (_quotient, _remainder, twice, divisor) => twice >= divisor,
  toZero: () => false,
  toNegativeInfinity: (_quotient, remainder) => remainder < 0n,
  toPositiveInfinity: (_quotient, remainder) => remainder > 0n,
};

/**
 * .NET-compat `decimal` — exact base-10 arithmetic.
 *
 * JavaScript only has IEEE-754 doubles, so `1.1 + 2.2` is `3.3000000000000003`. .NET `decimal` is an
 * exact base-10 type; this class reproduces that using a BigInt mantissa and a decimal scale
 * (value = mantissa / 10^scale). The transpiler emits `dec("...")` for decimal literals and routes
 * decimal operators to these methods.
 *
 * Scope: add/sub/mul are exact; div is computed to 28 fractional digits with banker's rounding then
 * trimmed (matching .NET's typical results for terminating quotients). Trailing-zero scale is
 * preserved for +/-/* (so 1.5m * 2m renders "3.0", like .NET).
 */
export class Decimal {
  constructor(
    readonly mantissa: bigint,
    readonly scale: number,
  ) {}

  static from(value: string | number | Decimal): Decimal {
    if (value instanceof Decimal) return value; // pass-through (operands may already be Decimal)
    const text = (typeof value === 'string' ? value : String(value)).trim();
    const match = /^([+-]?)(\d*)(?:\.(\d*))?$/.exec(text);
    if (!match) throw new Error(`Invalid decimal literal: '${text}'`);

    const sign = match[1] === '-' ? -1n : 1n;
    const intPart = match[2] || '0';
    const fracPart = match[3] || '';
    const digits = intPart + fracPart || '0';
    return new Decimal(sign * BigInt(digits), fracPart.length);
  }

  /**
   * `decimal.Parse`: the number `text` holds under `styles`, `NumberStyles.Number` unless the call
   * named others, rounded to what a decimal keeps. It throws what .NET throws, with its words, and
   * in its order: for no text, then for a style a decimal cannot read, then for text that is not a
   * number (a FormatException) and for one no decimal holds (an OverflowException).
   */
  static parse(text: string | null | undefined, styles: number = NumberStyles.Number): Decimal {
    if (text == null) throw new Error(NULL_TEXT);
    validateRealStyles(styles);
    const number = readNumber(text, styles);
    if (number === undefined) throw badFormat(text);
    const value = fromText(number);
    if (value === undefined) throw new Error(OVERFLOW);
    return value;
  }

  /**
   * `decimal.TryParse`: the value `parse` would return, or `undefined` where it would throw for
   * the text. A style a decimal cannot read still throws, and before the text is looked at, as it
   * does in .NET.
   */
  static tryParse(
    text: string | null | undefined,
    styles: number = NumberStyles.Number,
  ): Decimal | undefined {
    validateRealStyles(styles);
    if (text == null) return undefined;
    const number = readNumber(text, styles);
    return number === undefined ? undefined : fromText(number);
  }

  /**
   * A double converted to a decimal, as .NET converts it (`DecCalc.VarDecFromR8`): to 15
   * significant digits, which is all a double is trusted with, so `0.1 + 0.2` becomes 0.3. The
   * digits come from the double scaled by a power of ten in double arithmetic and rounded half to
   * even, the same steps .NET takes; a value of 2^96 or more, an infinity or NaN is an overflow.
   */
  static fromDouble(value: number): Decimal {
    bits.setFloat64(0, value);
    return fromBinary(value, ((bits.getUint16(0) >> 4) & 0x7ff) - 1022, 15);
  }

  /** A float converted to a decimal (`DecCalc.VarDecFromR4`): the same steps, to 7 digits. */
  static fromSingle(value: number): Decimal {
    const single = Math.fround(value);
    bits.setFloat32(0, single);
    return fromBinary(single, ((bits.getUint16(0) >> 7) & 0xff) - 126, 7);
  }

  private static align(a: Decimal, b: Decimal): [bigint, bigint, number] {
    const scale = Math.max(a.scale, b.scale);
    const am = a.mantissa * 10n ** BigInt(scale - a.scale);
    const bm = b.mantissa * 10n ** BigInt(scale - b.scale);
    return [am, bm, scale];
  }

  add(other: Decimal): Decimal {
    const [am, bm, scale] = Decimal.align(this, other);
    return new Decimal(am + bm, scale);
  }

  sub(other: Decimal): Decimal {
    const [am, bm, scale] = Decimal.align(this, other);
    return new Decimal(am - bm, scale);
  }

  /** Unary minus — C#'s `-m`, exact (the sign flips on the mantissa, the scale stays). */
  neg(): Decimal {
    return new Decimal(-this.mantissa, this.scale);
  }

  mul(other: Decimal): Decimal {
    return new Decimal(this.mantissa * other.mantissa, this.scale + other.scale);
  }

  /**
   * `%` — C#'s decimal remainder, exact: the dividend less the divisor times their quotient
   * truncated toward zero, so it takes the dividend's sign, at the larger of the two scales
   * (`5.5m % 2m` is `1.5`, `-5.5m % 2m` is `-1.5`, `0.3m % 0.1m` is `0.0`). It had none, and `%`
   * computed on the two values as doubles. A zero divisor throws what .NET throws.
   */
  mod(other: Decimal): Decimal {
    if (other.mantissa === 0n) throw new Error('Attempted to divide by zero.');
    const [am, bm, scale] = Decimal.align(this, other);
    return new Decimal(am % bm, scale);
  }

  div(other: Decimal): Decimal {
    if (other.mantissa === 0n) throw new Error('Attempted to divide by zero.');

    const targetFrac = 28;
    const shift = other.scale - this.scale + targetFrac;
    const num = shift >= 0 ? this.mantissa * 10n ** BigInt(shift) : this.mantissa;
    const denom = shift < 0 ? other.mantissa * 10n ** BigInt(-shift) : other.mantissa;

    let quotient = num / denom;
    const remainder = num % denom;

    // Round half-to-even on the remainder.
    const absRem2 = (remainder < 0n ? -remainder : remainder) * 2n;
    const absDenom = denom < 0n ? -denom : denom;
    const negativeResult = num < 0n !== denom < 0n;
    if (absRem2 > absDenom || (absRem2 === absDenom && quotient % 2n !== 0n)) {
      quotient += negativeResult ? -1n : 1n;
    }

    return new Decimal(quotient, targetFrac).trimTrailingZeros();
  }

  /**
   * `Math.Round(decimal[, digits][, mode])` and `decimal.Round`, as .NET's decimal has them. The
   * default is half-to-even, the same rule `div` applies to its 28th digit; AwayFromZero moves only
   * a half, and the three directed modes move every value (ToZero truncates, ToNegativeInfinity is
   * a floor, ToPositiveInfinity a ceiling). A value with no more digits than asked for is itself.
   */
  round(digits = 0, mode: MidpointRounding = 'toEven'): Decimal {
    if (!Number.isInteger(digits) || digits < 0 || digits > 28) {
      throw new RangeError('Rounding digits must be between 0 and 28.');
    }
    // The mode is checked whatever the value, as .NET checks it: a value that needs no rounding
    // does not make a mode that is not one valid. Own keys only, or `toString` would read as a rule.
    const steps = Object.prototype.hasOwnProperty.call(STEPS, mode) ? STEPS[mode] : undefined;
    if (steps === undefined) {
      throw new RangeError(
        `The value '${String(mode)}' is not valid for this usage of the type MidpointRounding.`,
      );
    }
    if (this.scale <= digits) return this;
    const divisor = 10n ** BigInt(this.scale - digits);
    const quotient = this.mantissa / divisor;
    const remainder = this.mantissa % divisor;
    if (remainder === 0n) return new Decimal(quotient, digits);
    // BigInt division truncates, so the quotient is the value toward zero and the remainder carries
    // the value's sign: every mode is a choice between staying and one step away from zero.
    const away = remainder < 0n ? -1n : 1n;
    const twice = (remainder < 0n ? -remainder : remainder) * 2n;
    return new Decimal(
      steps(quotient, remainder, twice, divisor) ? quotient + away : quotient,
      digits,
    );
  }

  private trimTrailingZeros(): Decimal {
    let m = this.mantissa;
    let s = this.scale;
    while (s > 0 && m % 10n === 0n) {
      m /= 10n;
      s--;
    }
    return new Decimal(m, s);
  }

  compareTo(other: Decimal): number {
    const [am, bm] = Decimal.align(this, other);
    return am < bm ? -1 : am > bm ? 1 : 0;
  }

  /** `Equals(object)`: a decimal of the same value, whatever its scale, and nothing of another kind. */
  equals(other: unknown): boolean {
    return other instanceof Decimal && this.compareTo(other) === 0;
  }

  toString(): string {
    const negative = this.mantissa < 0n;
    let digits = (negative ? -this.mantissa : this.mantissa).toString();
    if (this.scale === 0) return (negative ? '-' : '') + digits;

    while (digits.length <= this.scale) digits = '0' + digits;
    const cut = digits.length - this.scale;
    return (negative ? '-' : '') + digits.slice(0, cut) + '.' + digits.slice(cut);
  }

  toNumber(): number {
    return Number(this.toString());
  }

  /**
   * Serialize as a JSON **string** (e.g. `"19.99"`), matching the eQuantic wire protocol (`EqJson`
   * emits decimal as a string). A JSON number would round through a double and lose digits beyond
   * ~17 significant figures; the string preserves the exact value and scale so a server round-trip
   * (Server Action arg, SSR state) stays precise.
   */
  toJSON(): string {
    return this.toString();
  }
}

const ZERO = new Decimal(0n, 0);

/**
 * A decimal from the digits of its text, as .NET makes one (`Number.TryNumberToDecimal`), or
 * `undefined` where no decimal holds the value. It keeps as many digits as fit, at most 28 after
 * the point and at most a 96-bit mantissa, and rounds at the first it drops, half to even: a 5
 * followed by nothing but zeros leaves an even mantissa as it is. A rounding that carries past 96
 * bits costs a digit instead, so 7.92281625142643375935439503355 is 7.922816251426433759354395034.
 * A zero keeps the scale its text wrote, up to 28, and so does a value too small to show.
 */
function fromText({ negative, digits, scale }: NumberText): Decimal | undefined {
  if (digits.length === 0) return new Decimal(0n, Math.min(Math.max(-scale, 0), MAX_SCALE));
  if (scale > PRECISION) return undefined;
  let exponent = scale;
  let mantissa = 0n;
  let index = 0;
  while (exponent > 0 || (index < digits.length && exponent > -MAX_SCALE)) {
    const digit = index < digits.length ? BigInt(digits.charCodeAt(index) - 48) : 0n;
    const next = mantissa * 10n + digit;
    if (next > MAX_MANTISSA) break;
    mantissa = next;
    if (index < digits.length) index++;
    exponent--;
  }
  const dropped = index < digits.length ? digits.charCodeAt(index) - 48 : 0;
  const half = dropped === 5 && !/[1-9]/.test(digits.slice(index + 1));
  if (dropped > 5 || (dropped === 5 && (!half || mantissa % 2n !== 0n))) {
    mantissa += 1n;
    if (mantissa > MAX_MANTISSA) {
      mantissa = CARRIED;
      exponent++;
    }
  }
  if (exponent > 0) return undefined;
  if (exponent <= -PRECISION) return new Decimal(0n, MAX_SCALE);
  return new Decimal(negative ? -mantissa : mantissa, -exponent);
}

/**
 * A binary floating-point value as a decimal of `digits` significant digits, by .NET's steps: the
 * power of ten that brings it to that many digits is estimated from the binary `exponent` times
 * log10(2) in 16.16 fixed point, the value is scaled by it in DOUBLE arithmetic, and the result is
 * rounded half to even. Scaling first is what .NET does, so a value whose scaling rounds is
 * converted from the rounded double, as it is there. The trailing zeros then go, never more than
 * the scaling made. An exponent under -94 rounds to zero whatever the digits, and a positive zero
 * is what .NET answers, a negative input included.
 */
function fromBinary(value: number, exponent: number, digits: 7 | 15): Decimal {
  if (exponent < -94) return ZERO;
  if (exponent > 96) throw new Error(OVERFLOW);
  const negative = value < 0;
  const top = digits - 1;
  let scaled = Math.abs(value);
  let power = top - ((exponent * 19728) >> 16);
  if (power >= 0) {
    if (power > MAX_SCALE) power = MAX_SCALE;
    scaled *= DOUBLE_POWERS_10[power];
  } else if (power !== -1 || scaled >= DOUBLE_POWERS_10[digits]) {
    scaled /= DOUBLE_POWERS_10[-power];
  } else {
    power = 0;
  }
  if (scaled < DOUBLE_POWERS_10[top] && power < MAX_SCALE) {
    scaled *= 10;
    power++;
  }
  let whole = Math.trunc(scaled);
  const rest = scaled - whole;
  if (rest > 0.5 || (rest === 0.5 && whole % 2 === 1)) whole++;
  if (whole === 0) return ZERO;
  let mantissa = BigInt(whole);
  if (power < 0) {
    mantissa *= 10n ** BigInt(-power);
    if (mantissa > MAX_MANTISSA) throw new Error(OVERFLOW);
    return new Decimal(negative ? -mantissa : mantissa, 0);
  }
  let spare = Math.min(power, top);
  while (spare > 0 && mantissa % 10n === 0n) {
    mantissa /= 10n;
    power--;
    spare--;
  }
  return new Decimal(negative ? -mantissa : mantissa, power);
}

export function dec(value: string | number | Decimal): Decimal {
  return Decimal.from(value);
}

/** `decimal.Parse`, for the transpiler. */
export function decParse(text: string | null | undefined, styles?: number): Decimal {
  return Decimal.parse(text, styles);
}

/** `decimal.TryParse`, for the transpiler: the value, or `undefined` where the text is not one. */
export function decTryParse(text: string | null | undefined, styles?: number): Decimal | undefined {
  return Decimal.tryParse(text, styles);
}

/** `(decimal)aDouble` and `Convert.ToDecimal(aDouble)`, for the transpiler. */
export function decFromDouble(value: number): Decimal {
  return Decimal.fromDouble(value);
}

/** `(decimal)aFloat` and `Convert.ToDecimal(aFloat)`, for the transpiler. */
export function decFromSingle(value: number): Decimal {
  return Decimal.fromSingle(value);
}

/**
 * `Convert.ToDecimal` of a value whose type the call site cannot settle, an `object` or a
 * nullable: null is 0, where `decimal.Parse` throws; a string parses as `decimal.Parse` reads it; a
 * Decimal is itself; a boolean is 1 or 0; a BigInt (a long) is exact; and a number is the double,
 * or the single, that the call site says it holds. Anything else has no conversion to a decimal.
 */
export function decConvert(value: unknown, numbers: 'double' | 'single' = 'double'): Decimal {
  if (value == null) return ZERO;
  if (value instanceof Decimal) return value;
  if (typeof value === 'string') return Decimal.parse(value);
  if (typeof value === 'boolean') return new Decimal(value ? 1n : 0n, 0);
  if (typeof value === 'bigint') return new Decimal(value, 0);
  if (typeof value === 'number') {
    return numbers === 'single' ? Decimal.fromSingle(value) : Decimal.fromDouble(value);
  }
  const name = (value as { constructor?: { name?: string } }).constructor?.name ?? 'Object';
  throw new Error(`Unable to cast object of type '${name}' to type 'System.IConvertible'.`);
}
