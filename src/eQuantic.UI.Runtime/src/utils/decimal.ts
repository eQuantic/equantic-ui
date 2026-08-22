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

  mul(other: Decimal): Decimal {
    return new Decimal(this.mantissa * other.mantissa, this.scale + other.scale);
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
   * `Math.Round(decimal[, digits])` — half-to-even, .NET's default MidpointRounding for decimals,
   * the same rule `div` applies to its 28th digit. A value with no more digits than asked for
   * is itself.
   */
  round(digits = 0): Decimal {
    if (digits < 0) throw new Error('Rounding digits must be between 0 and 28.');
    if (this.scale <= digits) return this;
    const divisor = 10n ** BigInt(this.scale - digits);
    let quotient = this.mantissa / divisor;
    const remainder = this.mantissa % divisor;
    const absRem2 = (remainder < 0n ? -remainder : remainder) * 2n;
    if (absRem2 > divisor || (absRem2 === divisor && quotient % 2n !== 0n)) {
      quotient += this.mantissa < 0n ? -1n : 1n;
    }
    return new Decimal(quotient, digits);
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

  equals(other: Decimal): boolean {
    return this.compareTo(other) === 0;
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

export function dec(value: string | number | Decimal): Decimal {
  return Decimal.from(value);
}
