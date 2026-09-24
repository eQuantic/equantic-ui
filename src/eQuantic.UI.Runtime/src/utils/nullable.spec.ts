import { describe, it, expect } from 'vitest';
import { liftArith, liftCmp, liftUnary } from './nullable';

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

  it('liftUnary answers null for an absent operand, where JavaScript reads it as 0', () => {
    // `-null` is -0, `~null` is -1 and `null + 1` is 1 in JavaScript; C#'s lifted operators answer null.
    expect(liftUnary<number, number>(null, (a) => -a)).toBeNull();
    expect(liftUnary<number, number>(undefined, (a) => ~a)).toBeNull();
    expect(liftUnary<number, number>(null, (a) => a + 1)).toBeNull();
  });

  it('liftUnary applies the op to a present value, 0 and a BigInt included', () => {
    expect(liftUnary(5, (a) => a + 1)).toBe(6);
    expect(liftUnary(0, (a) => ~a)).toBe(-1);
    expect(liftUnary(5n, (a) => a - 1n)).toBe(4n);
  });
});
