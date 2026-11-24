import { describe, expect, it } from 'vitest';
import { clamp, clampLong } from './clamp';

/**
 * The runtime's Clamp in .NET's order (#1824). Its answers in each culture and width are compared with
 * .NET's on both sides by `NumberClampConformanceTests`; these pin the order and the refusal's shape.
 */
describe('clamp: .NET order and refusal', () => {
  it('refuses a minimum above the maximum, writing the values as their type writes them', () => {
    expect(() => clamp(2, 3.5, 1.25, 'double')).toThrow("'3.5' cannot be greater than 1.25.");
    expect(() => clamp(0, Math.fround(0.1), Math.fround(0.05), 'single')).toThrow(
      "'0.1' cannot be greater than 0.05.",
    );
    expect(() => clampLong(2n, 3000000000n, 1n)).toThrow("'3000000000' cannot be greater than 1.");
  });

  it('compares the minimum first, so a NaN bound is passed over and a negative zero keeps its sign', () => {
    expect(clamp(2, NaN, 1, 'double')).toBe(1);
    expect(clamp(2, 1, NaN, 'double')).toBe(2);
    expect(Number.isNaN(clamp(NaN, 1, 3, 'double'))).toBe(true);
    expect(Object.is(clamp(-0, 0, 1, 'double'), -0)).toBe(true);
    expect(clampLong(5n, 1n, 3n)).toBe(3n);
  });
});
