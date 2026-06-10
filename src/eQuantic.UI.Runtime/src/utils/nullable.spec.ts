import { describe, it, expect } from 'vitest';
import { liftArith, liftCmp } from './nullable';

describe('Nullable lifted operators', () => {
  it('liftArith returns null when either operand is null/undefined', () => {
    // Explicit type args: a literal null/undefined operand would otherwise make TS infer the
    // generic as null, leaving the (never-invoked) callback params possibly-null under strict mode.
    expect(liftArith<number, number, number>(null, 5, (a, b) => a + b)).toBeNull();
    expect(liftArith<number, number, number>(3, null, (a, b) => a + b)).toBeNull();
    expect(liftArith<number, number, number>(undefined, 5, (a, b) => a + b)).toBeNull();
  });

  it('liftArith applies the op when both operands are present', () => {
    expect(liftArith(3, 5, (a, b) => a + b)).toBe(8);
    expect(liftArith(3, 4, (a, b) => a * b)).toBe(12);
    expect(liftArith(7, 2, (a, b) => Math.trunc(a / b))).toBe(3); // integer division
  });

  it('liftArith treats 0 as a present value (not null)', () => {
    expect(liftArith(0, 5, (a, b) => a + b)).toBe(5);
    expect(liftArith(5, 0, (a, b) => a + b)).toBe(5);
  });

  it('liftCmp returns false when either operand is null/undefined', () => {
    // The .NET divergence: null < 5 is FALSE, not a numeric coercion.
    expect(liftCmp<number, number>(null, 5, (a, b) => a < b)).toBe(false);
    expect(liftCmp<number, number>(5, null, (a, b) => a > b)).toBe(false);
    expect(liftCmp<number, number>(null, null, (a, b) => a >= b)).toBe(false);
  });

  it('liftCmp applies the predicate when both operands are present', () => {
    expect(liftCmp(3, 5, (a, b) => a < b)).toBe(true);
    expect(liftCmp(5, 3, (a, b) => a < b)).toBe(false);
    expect(liftCmp(3, 3, (a, b) => a <= b)).toBe(true);
  });

  it('liftCmp treats 0 as a present value', () => {
    expect(liftCmp(0, 5, (a, b) => a < b)).toBe(true);
  });
});
