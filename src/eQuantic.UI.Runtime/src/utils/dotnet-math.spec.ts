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
