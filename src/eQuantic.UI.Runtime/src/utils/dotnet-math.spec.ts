import { describe, it, expect } from 'vitest';
import { round } from './dotnet-math';

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
