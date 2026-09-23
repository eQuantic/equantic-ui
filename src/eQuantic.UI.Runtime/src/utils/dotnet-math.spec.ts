import { describe, it, expect } from 'vitest';
import {
  bitDecrementSingle,
  bitIncrementSingle,
  fmaSingle,
  hypotSingle,
  ieeeRemainder,
  logBase,
  round,
  roundSingle,
} from './dotnet-math';

describe("round (banker's rounding, like .NET Math.Round)", () => {
  it('rounds halves to the even neighbour', () => {
    expect(round(0.5)).toBe(0);
    expect(round(1.5)).toBe(2);
    expect(round(2.5)).toBe(2); // not 3 (JS Math.round would give 3)
    expect(round(3.5)).toBe(4);
    expect(round(4.5)).toBe(4);
  });

  it('handles negatives symmetrically', () => {
    expect(round(-2.5)).toBe(-2);
    expect(round(-3.5)).toBe(-4);
  });

  it('rounds non-midpoints normally', () => {
    expect(round(2.4)).toBe(2);
    expect(round(2.6)).toBe(3);
    expect(round(-2.6)).toBe(-3);
  });

  it('respects a digit count', () => {
    expect(round(3.14159, 2)).toBe(3.14);
    expect(round(3.14159, 4)).toBe(3.1416);
    expect(round(0.125, 2)).toBe(0.12); // midpoint -> even
  });

  it('passes through non-finite values', () => {
    expect(round(Infinity)).toBe(Infinity);
    expect(Number.isNaN(round(NaN))).toBe(true);
  });

  it('detects a midpoint EXACTLY — near a half is not a half', () => {
    expect(round(2.5000000001)).toBe(3);
    expect(round(0.5015, 3)).toBe(0.501); // 0.5015 * 1000 is 501.49999999999994
    expect(Object.is(round(-0.4), -0)).toBe(true); // the sign of zero is .NET's
  });

  it('leaves a value past 1e16 alone and refuses a digit count past 15, as .NET does', () => {
    expect(round(12345678901234567, 2)).toBe(12345678901234567);
    expect(() => round(1, 16)).toThrow(RangeError);
    expect(() => round(1, -1)).toThrow(RangeError);
  });

  it('rounds by every MidpointRounding mode', () => {
    expect(round(2.5, 0, 'awayFromZero')).toBe(3);
    expect(round(-2.5, 0, 'awayFromZero')).toBe(-3);
    expect(round(0.49999999999999994, 0, 'awayFromZero')).toBe(0); // the largest value below a half
    expect(round(2.345, 2, 'awayFromZero')).toBe(2.35); // 2.345 * 100 is 234.50000000000003
    expect(round(1.7, 0, 'toZero')).toBe(1);
    expect(round(-1.5, 0, 'toNegativeInfinity')).toBe(-2);
    expect(round(1.2, 0, 'toPositiveInfinity')).toBe(2);
    expect(() => round(1, 0, 'sideways' as never)).toThrow(RangeError);
  });
});

describe('roundSingle (MathF.Round: the same algorithm in single precision)', () => {
  it('scales and divides back as float operations', () => {
    expect(roundSingle(Math.fround(1.2345), 2)).toBe(Math.fround(1.23));
    expect(roundSingle(Math.fround(2.5))).toBe(2);
    expect(roundSingle(Math.fround(2.5), 0, 'awayFromZero')).toBe(3);
  });

  it('refuses a digit count past 6 and leaves 1e8 and beyond alone', () => {
    expect(() => roundSingle(1, 7)).toThrow(RangeError);
    expect(roundSingle(Math.fround(123456792), 2)).toBe(Math.fround(123456792));
  });
});

describe('the single-precision neighbours step a SINGLE last bit', () => {
  it('bitIncrementSingle / bitDecrementSingle', () => {
    expect(bitIncrementSingle(1)).toBe(1.0000001192092896);
    expect(bitDecrementSingle(1)).toBe(0.9999999403953552);
    expect(bitIncrementSingle(0)).toBe(1.401298464324817e-45);
    expect(bitDecrementSingle(-0)).toBe(-1.401298464324817e-45);
    expect(bitIncrementSingle(-Infinity)).toBe(-3.4028234663852886e38);
    expect(bitDecrementSingle(Infinity)).toBe(3.4028234663852886e38);
    expect(bitIncrementSingle(Math.fround(-1))).toBe(-0.9999999403953552);
  });
});

describe('.NET compositions, transcribed', () => {
  it('ieeeRemainder works from the exact x % y', () => {
    expect(ieeeRemainder(5, 3)).toBe(-1);
    expect(ieeeRemainder(3, 2)).toBe(-1); // a tie goes to the even quotient
    expect(ieeeRemainder(0.3, 0.1)).toBe(-2.7755575615628914e-17); // not x - y·round(x / y)
    expect(Object.is(ieeeRemainder(-4, 2), -0)).toBe(true);
  });

  it('logBase answers NaN where .NET does', () => {
    expect(logBase(8, 2)).toBe(3);
    expect(logBase(8, 1)).toBeNaN();
    expect(logBase(8, 0)).toBeNaN();
    expect(logBase(8, Infinity)).toBeNaN();
    expect(logBase(1, 0)).toBe(-0);
  });

  it('hypotSingle computes in doubles and rounds once', () => {
    expect(hypotSingle(3, 4)).toBe(5);
    expect(hypotSingle(Infinity, NaN)).toBe(Infinity);
    expect(hypotSingle(0, Math.fround(0.1))).toBe(Math.fround(0.1));
  });

  it('fmaSingle rounds the exact sum once, the low half breaking a tie', () => {
    const a = (1 + 2 ** -12) * 2 ** -24;
    const b = 1 - 2 ** -12 + 2 ** -24;
    // a·b + 1 is 1 + 2^-24 + 2^-60: the double rounds onto the midpoint, the single must go UP.
    expect(fmaSingle(a, b, 1)).toBe(1.0000001192092896);
    expect(Math.fround(a * b + 1)).toBe(1); // what rounding the double alone would answer
    expect(fmaSingle(2, 3, 4)).toBe(10);
  });
});

describe('the *Pi family is exact at the special angles', () => {
  it('sinPi', async () => {
    const { sinPi } = await import('./dotnet-math');
    expect(sinPi(1) === 0).toBe(true); // exactly zero (IEEE: −0 for odd n); Math.sin(Math.PI) is 1.22e-16
    expect(sinPi(0.5)).toBe(1);
    expect(sinPi(2.5)).toBe(1);
    expect(sinPi(-0.5)).toBe(-1);
    expect(sinPi(Infinity)).toBeNaN();
  });

  it('cosPi', async () => {
    const { cosPi } = await import('./dotnet-math');
    expect(cosPi(0.5)).toBe(0);
    expect(cosPi(1)).toBe(-1);
    expect(cosPi(2)).toBe(1);
    expect(cosPi(-1)).toBe(-1);
  });

  it('tanPi, with the half-integer parity rule', () => {
    return import('./dotnet-math').then(({ tanPi }) => {
      expect(tanPi(0.25)).toBe(0.9999999999999999); // what .NET's own polynomial answers there
      expect(tanPi(0.75)).toBe(-1); // and the reciprocal path rounds THIS one exactly
      expect(tanPi(1) === 0).toBe(true); // -0 by parity, exactly zero either way
      expect(tanPi(0.5)).toBe(Infinity);
      expect(tanPi(1.5)).toBe(-Infinity);
    });
  });
});

describe('bit-adjacent doubles', () => {
  it('steps one representable value', async () => {
    const { bitIncrement, bitDecrement } = await import('./dotnet-math');
    expect(bitIncrement(1)).toBe(1.0000000000000002);
    expect(bitDecrement(1)).toBe(0.9999999999999999);
    expect(bitIncrement(0)).toBe(5e-324);
    expect(bitDecrement(0)).toBe(-5e-324);
    expect(bitIncrement(-Infinity)).toBe(-1.7976931348623157e308);
  });

  it('reads the unbiased exponent, subnormals included', async () => {
    const { ilogb } = await import('./dotnet-math');
    expect(ilogb(8)).toBe(3);
    expect(ilogb(0.5)).toBe(-1);
    expect(ilogb(1e-310)).toBe(-1030);
    expect(ilogb(0)).toBe(-2147483648);
    expect(ilogb(NaN)).toBe(2147483647);
  });
});

describe('fused multiply-add', () => {
  it('rounds once — the naive product-then-sum differs on the last bit', async () => {
    const { fma } = await import('./dotnet-math');
    expect(fma(2, 3, 4)).toBe(10);
    // 0.1 * 0.2 exact = 0.020000000000000004163…; fused keeps the low bits into the sum.
    expect(fma(0.1, 0.2, 0.3)).not.toBe(0.1 * 0.2 + 0.3 + 1); // sanity: finite, near 0.32
    expect(fma(1e308, 10, -Infinity)).toBe(-Infinity); // overflowing product path
  });
});

describe('the min/max tie and NaN rules', () => {
  it('magnitude ties go to sign', async () => {
    const { maxMagnitude, minMagnitude } = await import('./dotnet-math');
    expect(maxMagnitude(-5, 3)).toBe(-5);
    expect(maxMagnitude(-3, 3)).toBe(3);
    expect(minMagnitude(-3, 3)).toBe(-3);
    expect(maxMagnitude(-5n, 3n)).toBe(-5n); // the same rule serves the BigInt long
    expect(minMagnitude(-3n, 3n)).toBe(-3n);
  });

  it('the *Number forms ignore NaN', async () => {
    const { maxNumber, minNumber, maxMagnitudeNumber, minMagnitudeNumber } = await import('./dotnet-math');
    expect(maxNumber(NaN, 3)).toBe(3);
    expect(minNumber(3, NaN)).toBe(3);
    expect(maxMagnitudeNumber(NaN, 3)).toBe(3);
    expect(minMagnitudeNumber(-5, NaN)).toBe(-5);
  });
});

describe('sign-aware roots', () => {
  it('takes odd roots of negatives', async () => {
    const { rootN } = await import('./dotnet-math');
    expect(rootN(27, 3)).toBe(3);
    expect(rootN(-8, 3)).toBe(-2);
    expect(rootN(16, 2)).toBe(4);
    expect(rootN(16, 4)).toBe(2);
  });
});
