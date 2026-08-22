/**
 * .NET-compat math helpers.
 *
 * Part of the eQuantic.UI .NET-compatibility runtime: faithful implementations of .NET numeric
 * semantics that JavaScript lacks natively. The transpiler emits calls to these where C# behavior
 * differs from the JS built-ins.
 */

/**
 * Rounds like .NET's `Math.Round` — banker's rounding (MidpointRounding.ToEven) — instead of
 * JavaScript's `Math.round` (which rounds halves toward +Infinity).
 *
 * Examples (matching .NET): round(2.5) === 2, round(3.5) === 4, round(-2.5) === -2,
 * round(2.345, 2) === 2.34.
 *
 * Note: this operates on IEEE-754 doubles, so midpoints that aren't exactly representable
 * (e.g. 2.675) follow the double's actual value, which can differ from .NET's decimal-aware
 * rounding. Exact decimal rounding belongs to the Decimal compat type.
 */
export function round(value: number, digits = 0): number {
  if (!Number.isFinite(value)) return value;

  const factor = 10 ** digits;
  const scaled = value * factor;
  return roundHalfToEven(scaled) / factor;
}

function roundHalfToEven(x: number): number {
  const floor = Math.floor(x);
  const fraction = x - floor;
  const EPSILON = 1e-9;

  // Exact midpoint → round to the even neighbour.
  if (Math.abs(fraction - 0.5) < EPSILON) {
    return floor % 2 === 0 ? floor : floor + 1;
  }

  // Otherwise normal rounding.
  return Math.round(x);
}

// ---------------------------------------------------------------------------------------------
// The *Pi trigonometric family — a LITERAL port of .NET's implementation (dotnet/runtime
// Double.cs, itself based on amd/aocl-libm-ose, BSD-3). .NET does NOT call the platform libm for
// these: it reduces the argument in raw double arithmetic (the residue of `0.5 - fractional`
// carries its rounding error on purpose) and evaluates its own minimax polynomials — so the only
// way to answer what .NET answers, bit for bit, is to run the same reduction into the same
// polynomials. SinPi(1) is 0 and CosPi(0.5) is 0, where Math.sin(Math.PI) is 1.22e-16; and
// TanPi(0.25) is 0.9999999999999999 because that is what .NET computes there.
// ---------------------------------------------------------------------------------------------

const TWO_52 = 4503599627370496;
const TWO_53 = 9007199254740992;

/** amd `sin_piby4`: minimax sin on [0, π/4], with an optional tail correction. */
function sinForIntervalPiBy4(x: number, xTail: number): number {
  const C1 = -0.166666666666666646259241729;
  const C2 = +0.833333333333095043065222816e-2;
  const C3 = -0.19841269836761125688538679e-3;
  const C4 = +0.275573161037288022676895908448e-5;
  const C5 = -0.25051132068021699772257377197e-7;
  const C6 = +0.1591814430448591368526682e-9;

  const xx = x * x;
  const xxx = xx * x;

  let result = C6;
  result = result * xx + C5;
  result = result * xx + C4;
  result = result * xx + C3;
  result = result * xx + C2;

  if (xTail === 0.0) {
    result = xx * result + C1;
    result = xxx * result + x;
  } else {
    result = x - (xx * (0.5 * xTail - xxx * result) - xTail - xxx * C1);
  }
  return result;
}

/** amd `cos_piby4`: minimax cos on [0, π/4], with an optional tail correction. */
function cosForIntervalPiBy4(x: number, xTail: number): number {
  const C1 = +0.41666666666666665390037e-1;
  const C2 = -0.13888888888887398280412e-2;
  const C3 = +0.248015872987670414957399e-4;
  const C4 = -0.275573172723441909470836e-6;
  const C5 = +0.208761463822329611076335e-8;
  const C6 = -0.11382639806794485959088e-10;

  const xx = x * x;
  const tmp1 = 0.5 * xx;
  const tmp2 = 1.0 - tmp1;

  let result = C6;
  result = result * xx + C5;
  result = result * xx + C4;
  result = result * xx + C3;
  result = result * xx + C2;
  result = result * xx + C1;

  result *= xx * xx;
  result += 1.0 - tmp2 - tmp1 - x * xTail;
  result += tmp2;
  return result;
}

/** amd `tan_piby4`: Remez [2, 3] tan on [0, 0.68], transformed near π/4, optionally reciprocal. */
function tanForIntervalPiBy4(x: number, xTail: number, isReciprocal: boolean): number {
  const PiBy4Head = 7.85398163397448278999e-1;
  const PiBy4Tail = 3.06161699786838240164e-17;

  let transform = 0;
  if (x > +0.68) {
    transform = 1;
    x = PiBy4Head - x + (PiBy4Tail - xTail);
    xTail = 0.0;
  } else if (x < -0.68) {
    transform = -1;
    x = PiBy4Head + x + (PiBy4Tail + xTail);
    xTail = 0.0;
  }

  const tmp1 = x * x + 2.0 * x * xTail;

  let denominator = -0.232371494088563558304549252913e-3;
  denominator = +0.260656620398645407524064091208e-1 + denominator * tmp1;
  denominator = -0.515658515729031149329237816945 + denominator * tmp1;
  denominator = +1.11713747927937668539901657944 + denominator * tmp1;

  let numerator = +0.224044448537022097264602535574e-3;
  numerator = -0.229345080057565662883358588111e-1 + numerator * tmp1;
  numerator = +0.372379159759792203640806338901 + numerator * tmp1;

  let tmp2 = x * tmp1;
  tmp2 *= numerator / denominator;
  tmp2 += xTail;

  let result = x + tmp2;

  if (transform !== 0) {
    if (isReciprocal) {
      result = transform * ((2 * result) / (result - 1)) - 1.0;
    } else {
      result = transform * (1.0 - (2 * result) / (1 + result));
    }
  } else if (isReciprocal) {
    // -1.0 / (x + tmp2) to full precision: split both into 32-bit heads through the bit view.
    trigFloat[0] = result;
    trigBits[0] &= 0xffffffff00000000n;
    const z1 = trigFloat[0];
    const z2 = tmp2 - (z1 - x);

    const reciprocal = -1.0 / result;
    trigFloat[0] = reciprocal;
    trigBits[0] &= 0xffffffff00000000n;
    const reciprocalHead = trigFloat[0];
    result = reciprocalHead + reciprocal * (1.0 + reciprocalHead * z1 + reciprocalHead * z2);
  }

  return result;
}

const trigFloat = new Float64Array(1);
const trigBits = new BigUint64Array(trigFloat.buffer);

/** sin(πx), exactly as .NET computes it — 0 at integers, ±1 at half-integers. */
export function sinPi(x: number): number {
  if (!Number.isFinite(x)) return NaN;
  const ax = Math.abs(x);
  if (ax >= TWO_52) return x * 0.0; // an integer
  if (ax > 0.25) {
    const integral = Math.trunc(ax); // exact below 2^52
    const fractional = ax - integral;
    const sign = (x > 0.0 ? 1.0 : -1.0) * (integral % 2 === 1 ? -1.0 : 1.0);
    if (fractional <= 0.25)
      return fractional !== 0.0 ? sign * sinForIntervalPiBy4(fractional * Math.PI, 0.0) : x * 0.0;
    if (fractional <= 0.5)
      return fractional !== 0.5 ? sign * cosForIntervalPiBy4((0.5 - fractional) * Math.PI, 0.0) : sign;
    if (fractional <= 0.75) return sign * cosForIntervalPiBy4((fractional - 0.5) * Math.PI, 0.0);
    return sign * sinForIntervalPiBy4((1.0 - fractional) * Math.PI, 0.0);
  }
  if (ax >= 1.220703125e-4) return sinForIntervalPiBy4(x * Math.PI, 0.0);
  if (ax >= 7.450580596923828e-9) {
    const value = x * Math.PI;
    return value - value * value * value * (1.0 / 6.0);
  }
  return x * Math.PI;
}

/** cos(πx), exactly as .NET computes it — ±1 at integers, 0 at half-integers. */
export function cosPi(x: number): number {
  if (!Number.isFinite(x)) return NaN;
  const ax = Math.abs(x);
  if (ax >= TWO_53) return 1.0; // an even integer
  if (ax >= TWO_52) {
    // An integer whose parity is the low bit of its representation in [2^52, 2^53).
    trigFloat[0] = ax;
    return (trigBits[0] & 1n) === 1n ? -1.0 : 1.0;
  }
  if (ax > 0.25) {
    const integral = Math.trunc(ax);
    const fractional = ax - integral;
    const sign = integral % 2 === 1 ? -1.0 : 1.0;
    if (fractional <= 0.25)
      return fractional !== 0.0 ? sign * cosForIntervalPiBy4(fractional * Math.PI, 0.0) : sign;
    if (fractional <= 0.5)
      return fractional !== 0.5 ? sign * sinForIntervalPiBy4((0.5 - fractional) * Math.PI, 0.0) : 0.0;
    if (fractional <= 0.75) return -sign * sinForIntervalPiBy4((fractional - 0.5) * Math.PI, 0.0);
    return -sign * cosForIntervalPiBy4((1.0 - fractional) * Math.PI, 0.0);
  }
  if (ax >= 6.103515625e-5) return cosForIntervalPiBy4(x * Math.PI, 0.0);
  if (ax >= 7.450580596923828e-9) {
    const value = x * Math.PI;
    return 1.0 - value * value * 0.5;
  }
  return 1.0;
}

/** tan(πx), exactly as .NET computes it — ±0 at integers, ±∞ at half-integers by parity. */
export function tanPi(x: number): number {
  if (!Number.isFinite(x)) return NaN;
  const ax = Math.abs(x);
  const sign = x > 0.0 ? 1.0 : -1.0;
  if (ax >= TWO_53) return sign * 0.0; // an even integer
  if (ax >= TWO_52) {
    trigFloat[0] = ax;
    return sign * ((trigBits[0] & 1n) === 1n ? -0.0 : 0.0);
  }
  if (ax > 0.25) {
    const integral = Math.trunc(ax);
    const fractional = ax - integral;
    if (fractional <= 0.25) {
      if (fractional !== 0.0) return sign * tanForIntervalPiBy4(fractional * Math.PI, 0.0, false);
      return sign * (integral % 2 === 1 ? -0.0 : 0.0);
    }
    if (fractional <= 0.5) {
      if (fractional !== 0.5) return -sign * tanForIntervalPiBy4((0.5 - fractional) * Math.PI, 0.0, true);
      return sign * (integral % 2 === 1 ? -Infinity : Infinity);
    }
    if (fractional <= 0.75) return sign * tanForIntervalPiBy4((fractional - 0.5) * Math.PI, 0.0, true);
    return -sign * tanForIntervalPiBy4((1.0 - fractional) * Math.PI, 0.0, false);
  }
  if (ax >= 6.103515625e-5) return tanForIntervalPiBy4(x * Math.PI, 0.0, false);
  if (ax >= 7.450580596923828e-9) {
    const value = x * Math.PI;
    return value + value * value * value * (1.0 / 3.0);
  }
  return x * Math.PI;
}

// ---------------------------------------------------------------------------------------------
// The bit-adjacent double surface: the next representable value, the raw exponent, the fused
// multiply-add. One shared view over the same 8 bytes reads a double's bits.
// ---------------------------------------------------------------------------------------------

const floatView = new Float64Array(1);
const bitsView = new BigInt64Array(floatView.buffer);

/** The next representable double above x (.NET BitIncrement): ±0 → Epsilon, -∞ → -MaxValue. */
export function bitIncrement(x: number): number {
  if (Number.isNaN(x) || x === Infinity) return x;
  if (x === -Infinity) return -1.7976931348623157e308;
  if (x === 0) return 5e-324;
  floatView[0] = x;
  bitsView[0] += x > 0 ? 1n : -1n;
  return floatView[0];
}

/** The next representable double below x (.NET BitDecrement): ±0 → -Epsilon, +∞ → MaxValue. */
export function bitDecrement(x: number): number {
  if (Number.isNaN(x) || x === -Infinity) return x;
  if (x === Infinity) return 1.7976931348623157e308;
  if (x === 0) return -5e-324;
  floatView[0] = x;
  bitsView[0] += x > 0 ? -1n : 1n;
  return floatView[0];
}

/** The unbiased base-2 exponent (.NET ILogB): subnormals count down from the mantissa's top bit;
 * 0 answers Int32.MinValue and NaN/∞ Int32.MaxValue, exactly as .NET defines them. */
export function ilogb(x: number): number {
  if (Number.isNaN(x) || x === Infinity || x === -Infinity) return 2147483647;
  if (x === 0) return -2147483648;
  floatView[0] = x;
  const bits = bitsView[0] & 0x7fffffffffffffffn;
  const exponent = Number(bits >> 52n);
  if (exponent > 0) return exponent - 1023;
  // Subnormal: the value is mantissa × 2^-1074, so the floor-log2 comes off the bit length.
  const mantissa = bits & 0xfffffffffffffn;
  return mantissa.toString(2).length - 1 - 1074;
}

/** a·b + c with ONE rounding (.NET FusedMultiplyAdd) — TwoProduct/TwoSum over Veltkamp splits,
 * so the product's low bits survive into the sum instead of rounding away first. */
export function fma(a: number, b: number, c: number): number {
  if (!Number.isFinite(a) || !Number.isFinite(b)) return a * b + c;
  // a·b is a finite REAL here, so an infinite (or NaN) c absorbs it — even when the ROUNDED
  // product would have overflowed the other way (fma(1e308, 10, -∞) is -∞, not NaN).
  if (!Number.isFinite(c)) return c;
  const product = a * b;
  if (!Number.isFinite(product)) return product;
  // Veltkamp split (2^27 + 1) — exact halves whose products carry no rounding.
  const SPLIT = 134217729;
  const at = SPLIT * a;
  const aHi = at - (at - a);
  const aLo = a - aHi;
  const bt = SPLIT * b;
  const bHi = bt - (bt - b);
  const bLo = b - bHi;
  const productError = aHi * bHi - product + aHi * bLo + aLo * bHi + aLo * bLo;
  // TwoSum of the rounded product and c, then fold both error terms in one final rounding.
  const sum = product + c;
  const cVirtual = sum - product;
  const sumError = product - (sum - cVirtual) + (c - cVirtual);
  return sum + (sumError + productError);
}

// ---------------------------------------------------------------------------------------------
// The min/max family with .NET's tie and NaN rules. Magnitude comparisons serve doubles AND
// longs (a BigInt has no NaN and no -0, so the extra checks are inert for it).
// ---------------------------------------------------------------------------------------------

function isNegativeValue(x: number | bigint): boolean {
  return x < 0 || Object.is(x, -0);
}

function magnitude(x: number | bigint): number | bigint {
  return x < 0 ? -x : x;
}

/** The value with the LARGER magnitude; ties go to the positive one; NaN propagates. */
export function maxMagnitude(a: number, b: number): number;
export function maxMagnitude(a: bigint, b: bigint): bigint;
export function maxMagnitude(a: number | bigint, b: number | bigint): number | bigint {
  if (typeof a === 'number' && Number.isNaN(a)) return a;
  if (typeof b === 'number' && Number.isNaN(b)) return b;
  const am = magnitude(a);
  const bm = magnitude(b);
  if (am > bm) return a;
  if (am < bm) return b;
  return isNegativeValue(a) ? b : a;
}

/** The value with the SMALLER magnitude; ties go to the negative one; NaN propagates. */
export function minMagnitude(a: number, b: number): number;
export function minMagnitude(a: bigint, b: bigint): bigint;
export function minMagnitude(a: number | bigint, b: number | bigint): number | bigint {
  if (typeof a === 'number' && Number.isNaN(a)) return a;
  if (typeof b === 'number' && Number.isNaN(b)) return b;
  const am = magnitude(a);
  const bm = magnitude(b);
  if (am < bm) return a;
  if (am > bm) return b;
  return isNegativeValue(a) ? a : b;
}

/** MaxMagnitude, except NaN is IGNORED — the other operand answers (.NET *Number semantics). */
export function maxMagnitudeNumber(a: number, b: number): number {
  if (Number.isNaN(a)) return b;
  if (Number.isNaN(b)) return a;
  return maxMagnitude(a, b);
}

/** MinMagnitude, except NaN is IGNORED. */
export function minMagnitudeNumber(a: number, b: number): number {
  if (Number.isNaN(a)) return b;
  if (Number.isNaN(b)) return a;
  return minMagnitude(a, b);
}

/** Math.max, except NaN is IGNORED (.NET MaxNumber). +0 beats -0, as Math.max already knows. */
export function maxNumber(a: number, b: number): number {
  if (Number.isNaN(a)) return b;
  if (Number.isNaN(b)) return a;
  return Math.max(a, b);
}

/** Math.min, except NaN is IGNORED (.NET MinNumber). */
export function minNumber(a: number, b: number): number {
  if (Number.isNaN(a)) return b;
  if (Number.isNaN(b)) return a;
  return Math.min(a, b);
}

/** The n-th root, sign-aware for odd roots of negatives (.NET RootN): RootN(-8, 3) is -2. */
export function rootN(x: number, n: number): number {
  if (n === 2) return Math.sqrt(x);
  if (n === 3) return Math.cbrt(x);
  if (x < 0 && n % 2 !== 0) return -Math.pow(-x, 1 / n);
  return Math.pow(x, 1 / n);
}
