import { describe, it, expect } from 'vitest';
import {
  exactOfBigInt,
  exactOfDouble,
  exactOfScaled,
  isZero,
  plainText,
  roundSignificant,
  scaled,
} from './exact-decimal';

// The exact decimal a number holds, which is what .NET formats (#393): the conformance suite runs the
// formatter against .NET, and this holds the arithmetic it stands on at the edges a C# case rarely
// reaches, a subnormal and the largest double among them.
describe('exactOfDouble', () => {
  it('writes a double from its binary value, past the shortest text that reads back', () => {
    expect(plainText(exactOfDouble(0.1))).toBe('0.1000000000000000055511151231257827021181583404541015625');
    expect(plainText(exactOfDouble(1.2345678901234568e19))).toBe('12345678901234567168');
  });

  it('keeps the sign of a negative zero, as .NET formatting does', () => {
    const zero = exactOfDouble(-0);
    expect(isZero(zero)).toBe(true);
    expect(zero.negative).toBe(true);
    expect(exactOfDouble(0).negative).toBe(false);
  });

  it('reads a subnormal, which has no implicit bit', () => {
    const smallest = exactOfDouble(5e-324);
    // 2^-1074 is 5^1074 digits, 751 of them, the first at the power -324.
    expect(smallest.digits.startsWith('4940656458412465441765687928682213723650598')).toBe(true);
    expect(smallest.digits.length).toBe(751);
    expect(smallest.digits.length + smallest.exponent).toBe(-323);
  });

  it('reads the largest double exactly', () => {
    expect(plainText(exactOfDouble(Number.MAX_VALUE)).length).toBe(309);
    expect(plainText(exactOfDouble(Number.MAX_VALUE)).startsWith('17976931348623157')).toBe(true);
  });
});

describe('exactOfBigInt and exactOfScaled', () => {
  it('writes a long by its own digits', () => {
    expect(exactOfBigInt(-9007199254740993n)).toEqual({ negative: true, digits: '9007199254740993', exponent: 0 });
  });

  it('writes a decimal as its mantissa over its scale, trailing zeros trimmed', () => {
    expect(plainText(exactOfScaled(12500n, 2))).toBe('125');
    expect(plainText(exactOfScaled(-125n, 4))).toBe('-0.0125');
    expect(exactOfScaled(0n, 3)).toEqual({ negative: false, digits: '0', exponent: 0 });
  });
});

describe('scaled', () => {
  it('moves the point without touching a digit', () => {
    expect(plainText(scaled(exactOfDouble(0.125), 2))).toBe('12.5');
    expect(isZero(scaled(exactOfDouble(0), 2))).toBe(true);
  });
});

describe('roundSignificant', () => {
  it('rounds an exact half to even for a double, and away from zero for a decimal', () => {
    expect(roundSignificant(exactOfDouble(1.25), 2, 'halfEven').digits).toBe('12');
    expect(roundSignificant(exactOfDouble(1.25), 2, 'halfExpand').digits).toBe('13');
    expect(roundSignificant(exactOfDouble(1.35), 2, 'halfEven').digits).toBe('14'); // 1.35 is above its half
  });

  it('moves the point when a carry runs off the front', () => {
    expect(roundSignificant(exactOfDouble(9.99), 2, 'halfEven')).toEqual({
      negative: false,
      digits: '10',
      scientific: 1,
    });
  });

  it('pads a short value, and keeps zero at the power zero', () => {
    expect(roundSignificant(exactOfBigInt(5n), 3, 'halfEven')).toEqual({ negative: false, digits: '500', scientific: 0 });
    expect(roundSignificant(exactOfDouble(-0), 2, 'halfEven')).toEqual({ negative: true, digits: '00', scientific: 0 });
  });
});
