import type { MidpointRounding } from './dotnet-math';
import { exception } from './exceptions';
import {
  NULL_TEXT,
  NumberStyles,
  badFormat,
  readNumber,
  readingFormat,
  validateRealStyles,
  type NumberText,
} from './number-grammar';
import { hash } from './hash';

/** The largest mantissa a decimal holds: 96 bits. */
const MAX_MANTISSA = (1n << 96n) - 1n;
/** The most digits a decimal keeps after the point. */
const MAX_SCALE = 28;
/** Past this many digits before the point, no decimal holds the number. */
const PRECISION = 29;
/** A mantissa that rounded past 96 bits, a digit shorter: 2^96 / 10, rounded up. */
const CARRIED = 7922816251426433759354395034n;
const OVERFLOW = 'Value was either too large or too small for a Decimal.';
/** The largest mantissa .NET multiplies on its 32-bit path. */
const NARROW = 0xffff_ffffn;
/** Past this scale a product of two 32-bit mantissas rounds to nothing (2^64 is 1.8e19). */
const VANISHED = MAX_SCALE + 19;
/** 10^n, for every scale the arithmetic moves by: up to two scales of 28, a product's. */
const POWERS_10 = Array.from({ length: 2 * MAX_SCALE + 2 }, (_, power) => 10n ** BigInt(power));

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
 * Each operation answers what .NET's `DecCalc` answers, its scale included (#764): `+`, `-` and `*`
 * are exact while the result fits 96 bits and 28 places, keeping the trailing zeros .NET keeps (so
 * 1.5m * 2m renders "3.0"), and past that they round half to even to the places that fit, or throw
 * the OverflowException .NET throws. `/` keeps as many places as fit; `%` is exact.
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
   * named others, in the page's culture or, where the call names it, the invariant one, rounded to
   * what a decimal keeps. It throws what .NET throws, with its words, and in its order: for no text,
   * then for a style a decimal cannot read, then for text that is not a number (a FormatException)
   * and for one no decimal holds (an OverflowException).
   */
  static parse(
    text: string | null | undefined,
    styles: number = NumberStyles.Number,
    invariant = false,
  ): Decimal {
    if (text == null) throw exception('System.ArgumentNullException', NULL_TEXT);
    validateRealStyles(styles);
    const number = readNumber(text, styles, readingFormat(invariant));
    if (number === undefined) throw badFormat(text);
    const value = fromText(number);
    if (value === undefined) throw exception('System.OverflowException', OVERFLOW);
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
    invariant = false,
  ): Decimal | undefined {
    validateRealStyles(styles);
    if (text == null) return undefined;
    const number = readNumber(text, styles, readingFormat(invariant));
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

  /** `+`, as .NET's `DecAddSub` adds: exact at the larger scale, then brought into a decimal. */
  add(other: Decimal): Decimal {
    const [am, bm, scale] = Decimal.align(this, other);
    return held(am + bm, scale);
  }

  /** `-`, as `add`. */
  sub(other: Decimal): Decimal {
    const [am, bm, scale] = Decimal.align(this, other);
    return held(am - bm, scale);
  }

  /** Unary minus — C#'s `-m`, exact (the sign flips on the mantissa, the scale stays). */
  neg(): Decimal {
    return new Decimal(-this.mantissa, this.scale);
  }

  /**
   * `*`, as .NET's `VarDecMul` multiplies: exact at the sum of the scales, then brought into a
   * decimal. Its two paths answer a zero differently, and so does this: two mantissas of 32 bits
   * keep a zero product at its scale (`0.00m * 1.5m` is 0.000), and a scale past 47 rounds them to a
   * plain 0, while a wider operand makes any zero product a plain 0.
   */
  mul(other: Decimal): Decimal {
    const scale = this.scale + other.scale;
    const product = this.mantissa * other.mantissa;
    if (narrow(this) && narrow(other)) return scale > VANISHED ? ZERO : held(product, scale);
    return product === 0n ? ZERO : held(product, scale);
  }

  /**
   * `%` — C#'s decimal remainder, as .NET's `VarDecMod` takes it: the dividend less the divisor
   * times their quotient truncated toward zero, exact, with the dividend's sign, at the larger of
   * the two scales (`5.5m % 2m` is `1.5`, `-5.5m % 2m` is `-1.5`, `0.3m % 0.1m` is `0.0`). A
   * dividend that is zero, or smaller than the divisor, is the answer as it is, its own scale kept
   * (`1.5m % 2.00m` is `1.5`). A zero divisor throws what .NET throws.
   */
  mod(other: Decimal): Decimal {
    if (other.mantissa === 0n)
      throw exception('System.DivideByZeroException', 'Attempted to divide by zero.');
    const [am, bm, scale] = Decimal.align(this, other);
    if (am === 0n || magnitude(am) < magnitude(bm)) return this;
    return new Decimal(am % bm, scale);
  }

  /**
   * `/`, as .NET's `VarDecDiv` divides. The quotient starts at the dividend's scale less the
   * divisor's; an exact one keeps it (`4.00m / 2m` is 2.00), a scale under zero brought up to zero
   * or an OverflowException. One with a remainder takes nine more places at a time while the
   * mantissa fits 96 bits and the scale 28, rounds the last half to even, and loses the trailing
   * zeros (`1m / 4m` is 0.25), so a large dividend keeps the 28 or 29 digits a decimal holds.
   */
  div(other: Decimal): Decimal {
    if (other.mantissa === 0n)
      throw exception('System.DivideByZeroException', 'Attempted to divide by zero.');
    const negative = this.mantissa < 0n !== other.mantissa < 0n;
    const divisor = magnitude(other.mantissa);
    let quotient = magnitude(this.mantissa) / divisor;
    let remainder = magnitude(this.mantissa) % divisor;
    let scale = this.scale - other.scale;
    let inexact = false;
    for (;;) {
      let step: number;
      if (remainder === 0n) {
        if (scale >= 0) break;
        step = Math.min(9, -scale);
      } else {
        inexact = true;
        step = scale === MAX_SCALE ? 0 : room(quotient, scale);
        if (step === 0) {
          const twice = remainder * 2n;
          if (twice > divisor || (twice === divisor && quotient % 2n !== 0n)) {
            quotient += 1n;
            if (quotient > MAX_MANTISSA) [quotient, scale] = carried(quotient, scale, true);
          }
          break;
        }
      }
      scale += step;
      quotient *= POWERS_10[step];
      if (quotient > MAX_MANTISSA) throw exception('System.OverflowException', OVERFLOW);
      const rest = remainder * POWERS_10[step];
      quotient += rest / divisor;
      remainder = rest % divisor;
      if (quotient > MAX_MANTISSA) {
        [quotient, scale] = carried(quotient, scale, remainder !== 0n);
        break;
      }
    }
    const result = new Decimal(negative ? -quotient : quotient, scale);
    return inexact ? result.trimTrailingZeros() : result;
  }

  /**
   * `Math.Round(decimal[, digits][, mode])` and `decimal.Round`, as .NET's decimal has them. The
   * default is half-to-even, the same rule `div` applies to its 28th digit; AwayFromZero moves only
   * a half, and the three directed modes move every value (ToZero truncates, ToNegativeInfinity is
   * a floor, ToPositiveInfinity a ceiling). A value with no more digits than asked for is itself.
   */
  round(digits = 0, mode: MidpointRounding = 'toEven'): Decimal {
    if (!Number.isInteger(digits) || digits < 0 || digits > 28) {
      throw exception(
        'System.ArgumentOutOfRangeException',
        'Rounding digits must be between 0 and 28.',
      );
    }
    // The mode is checked whatever the value, as .NET checks it: a value that needs no rounding
    // does not make a mode that is not one valid. Own keys only, or `toString` would read as a rule.
    const steps = Object.prototype.hasOwnProperty.call(STEPS, mode) ? STEPS[mode] : undefined;
    if (steps === undefined) {
      throw exception(
        'System.ArgumentException',
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

  /** `GetHashCode()`: its value's, whatever its scale, as `equals` reads it: 1.0 and 1.00 hash alike. */
  getHashCode(): number {
    return hash(this.toNumber());
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

function magnitude(value: bigint): bigint {
  return value < 0n ? -value : value;
}

/** Whether a decimal's mantissa is one .NET multiplies on its 32-bit path. */
function narrow(value: Decimal): boolean {
  return magnitude(value.mantissa) <= NARROW;
}

/**
 * An exact result as a decimal holds it, by .NET's `DecCalc.ScaleResult`: a mantissa past 96 bits,
 * or a scale past 28, loses the fewest places that bring both back, rounded half to even, and the
 * scale running out first is an OverflowException (`decimal.MaxValue + decimal.MaxValue`). A
 * rounding that carries the mantissa past 96 bits loses one place more and rounds again, as .NET
 * does, which is why 2^96 - 1 rounded up reads 7922816251426433759354395034.
 */
function held(mantissa: bigint, scale: number): Decimal {
  const negative = mantissa < 0n;
  let value = negative ? -mantissa : mantissa;
  let drop = Math.max(scale - MAX_SCALE, 0);
  if (value > MAX_MANTISSA) {
    // .NET's lower bound on the places to lose: the bits past 96 times log10(2) (77/256), and one
    // more, since that product rounds down. The loop below loses another where it still falls short.
    const bound = (((value.toString(2).length - 97) * 77) >> 8) + 1;
    if (bound > scale) throw exception('System.OverflowException', OVERFLOW);
    drop = Math.max(drop, bound);
  }
  if (drop === 0) return new Decimal(mantissa, scale);
  scale -= drop;
  let divisor = POWERS_10[drop];
  let remainder = value % divisor;
  value /= divisor;
  let sticky = false;
  for (;;) {
    while (value > MAX_MANTISSA) {
      if (scale === 0) throw exception('System.OverflowException', OVERFLOW);
      sticky ||= remainder !== 0n;
      remainder = value % 10n;
      value /= 10n;
      divisor = 10n;
      scale--;
    }
    const half = divisor / 2n;
    if (remainder < half || (remainder === half && !sticky && value % 2n === 0n)) break;
    value += 1n;
    if (value <= MAX_MANTISSA) break;
    if (scale === 0) throw exception('System.OverflowException', OVERFLOW);
    sticky = false;
    remainder = value % 10n;
    value /= 10n;
    divisor = 10n;
    scale--;
  }
  return new Decimal(negative ? -value : value, scale);
}

/**
 * The places a quotient can take on, as .NET's `SearchScale` counts them: the most, nine at most and
 * never past a scale of 28, that keep its mantissa within 96 bits. A quotient with room for fewer
 * than nine whose scale they leave under zero is an OverflowException; one with room for nine goes
 * on, and takes nine more on the next turn.
 */
function room(quotient: bigint, scale: number): number {
  let step = Math.min(9, MAX_SCALE - scale);
  while (step > 0 && quotient * POWERS_10[step] > MAX_MANTISSA) step--;
  if (step < 9 && step + scale < 0) throw exception('System.OverflowException', OVERFLOW);
  return step;
}

/**
 * A quotient that passed 96 bits, a place shorter, as .NET's `OverflowUnscale` makes it: its last
 * digit rounds it half to even, a half with anything behind it (`sticky`) upward. A scale that has
 * no place to lose is an OverflowException.
 */
function carried(quotient: bigint, scale: number, sticky: boolean): [bigint, number] {
  if (--scale < 0) throw exception('System.OverflowException', OVERFLOW);
  const digit = quotient % 10n;
  quotient /= 10n;
  if (digit > 5n || (digit === 5n && (sticky || quotient % 2n !== 0n))) quotient += 1n;
  return [quotient, scale];
}

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
  if (exponent > 96) throw exception('System.OverflowException', OVERFLOW);
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
    if (mantissa > MAX_MANTISSA) throw exception('System.OverflowException', OVERFLOW);
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

/** `decimal.Parse`, for the transpiler: in the page's culture unless the call names the invariant one. */
export function decParse(
  text: string | null | undefined,
  styles?: number,
  invariant?: boolean,
): Decimal {
  return Decimal.parse(text, styles, invariant);
}

/** `decimal.TryParse`, for the transpiler: the value, or `undefined` where the text is not one. */
export function decTryParse(
  text: string | null | undefined,
  styles?: number,
  invariant?: boolean,
): Decimal | undefined {
  return Decimal.tryParse(text, styles, invariant);
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
 * nullable: null is 0, where `decimal.Parse` throws; a string parses as `decimal.Parse` reads it, in
 * the culture the call names; a Decimal is itself; a boolean is 1 or 0; a BigInt (a long) is exact;
 * and a number is the double, or the single, that the call site says it holds. Anything else has no
 * conversion to a decimal.
 */
export function decConvert(
  value: unknown,
  numbers: 'double' | 'single' = 'double',
  invariant = false,
): Decimal {
  if (value == null) return ZERO;
  if (value instanceof Decimal) return value;
  if (typeof value === 'string') return Decimal.parse(value, NumberStyles.Number, invariant);
  if (typeof value === 'boolean') return new Decimal(value ? 1n : 0n, 0);
  if (typeof value === 'bigint') return new Decimal(value, 0);
  if (typeof value === 'number') {
    return numbers === 'single' ? Decimal.fromSingle(value) : Decimal.fromDouble(value);
  }
  const name = (value as { constructor?: { name?: string } }).constructor?.name ?? 'Object';
  throw exception(
    'System.InvalidCastException',
    `Unable to cast object of type '${name}' to type 'System.IConvertible'.`,
  );
}
