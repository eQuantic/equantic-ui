import { describe, expect, it } from 'vitest';
import { checked, intDiv, intRem, longDiv, longRem, single } from './overflow';

describe('checked arithmetic', () => {
  it('hands a value in range back, and throws past the edge', () => {
    expect(checked(2_147_483_647, 32)).toBe(2_147_483_647);
    expect(() => checked(2_147_483_648, 32)).toThrow(/overflow/);
    expect(() => checked(-1, 32, true)).toThrow(/overflow/);
    expect(checked(255, 8, true)).toBe(255);
    expect(() => checked(256, 8, true)).toThrow(/overflow/);
    expect(checked(9_223_372_036_854_775_807n, 64)).toBe(9_223_372_036_854_775_807n);
    expect(() => checked(9_223_372_036_854_775_808n, 64)).toThrow(/overflow/);
  });

  it('checks a BigInt into a NARROWER width — a checked cast off a long', () => {
    expect(checked(2_147_483_647n, 32)).toBe(2_147_483_647n);
    expect(() => checked(2_147_483_648n, 32)).toThrow(/overflow/);
    expect(() => checked(-1n, 32, true)).toThrow(/overflow/);
    expect(checked(65_535n, 16, true)).toBe(65_535n);
    expect(() => checked(70_000n, 16, true)).toThrow(/overflow/);
  });

  it('checks a NUMBER against the 64-bit edge — a checked (long) cast off a double', () => {
    expect(checked(100.9, 64)).toBe(100.9);
    expect(() => checked(1e20, 64)).toThrow(/overflow/);
    expect(() => checked(9_223_372_036_854_775_808, 64)).toThrow(/overflow/); // 2^63 exactly
    expect(checked(-9_223_372_036_854_775_808, 64)).toBe(-9_223_372_036_854_775_808);
    expect(() => checked(-1, 64, true)).toThrow(/overflow/);
    expect(() => checked(18_446_744_073_709_551_616, 64, true)).toThrow(/overflow/); // 2^64 exactly
    expect(() => checked(Number.NaN, 64)).toThrow(/overflow/);
  });
});

describe('a float as text', () => {
  it('prints the shortest decimal that reads back as the same single', () => {
    expect(single(Math.fround(Math.fround(0.1) + Math.fround(0.2)))).toBe('0.3');
    expect(single(Math.fround(0.3))).toBe('0.3');
    expect(single(0.1 + 0.2)).toBe('0.3'); // an unstored double, rounded on the way in
    expect(single(Math.fround(1.1))).toBe('1.1');
    expect(single(Math.fround(16777217))).toBe('16777216');
    expect(single(0)).toBe('0');
    expect(single(Math.fround(-2.5))).toBe('-2.5');
  });
});

/** .NET 10's answers, measured: a zero divisor and `MinValue / -1` throw, the remainder included. */
describe('integer division throws where .NET does', () => {
  const zero = 'Attempted to divide by zero.';
  const overflow = 'Arithmetic operation resulted in an overflow.';

  it('divides and truncates what .NET divides', () => {
    expect(intDiv(-7, 2)).toBe(-3);
    expect(intRem(-7, 2)).toBe(-1);
    expect(intDiv(-2_147_483_648, 1)).toBe(-2_147_483_648);
    expect(intDiv(4_294_967_295, 2)).toBe(2_147_483_647);
    expect(longDiv(9_223_372_036_854_775_807n, 2n)).toBe(4_611_686_018_427_387_903n);
    expect(longRem(-7n, 2n)).toBe(-1n);
  });

  it("refuses a zero divisor with .NET's message", () => {
    expect(() => intDiv(5, 0)).toThrow(zero);
    expect(() => intRem(5, 0)).toThrow(zero);
    expect(() => longDiv(5n, 0n)).toThrow(zero);
    expect(() => longRem(5n, 0n)).toThrow(zero);
  });

  it('refuses MinValue by -1, the remainder included', () => {
    expect(() => intDiv(-2_147_483_648, -1)).toThrow(overflow);
    expect(() => intRem(-2_147_483_648, -1)).toThrow(overflow);
    expect(() => longDiv(-9_223_372_036_854_775_808n, -1n)).toThrow(overflow);
    expect(() => longRem(-9_223_372_036_854_775_808n, -1n)).toThrow(overflow);
    expect(intDiv(-2_147_483_647, -1)).toBe(2_147_483_647);
  });
});
