import { describe, expect, it } from 'vitest';
import { Decimal } from './decimal';
import { is } from './exceptions';
import * as guard from './guards';

/** A value written as pt-BR writes a number, as the compiler's text arrow would. */
const ptBR = (value: number): string => String(value).replace('.', ',');
const plain = (value: unknown): string => String(value);
const same = (value: unknown, other: unknown): boolean => value === other || (Number.isNaN(value) && Number.isNaN(other));

/** What the guard threw, as the members C# reads. */
function thrown(run: () => void): { type: string; message: string; paramName: unknown; actualValue: unknown } | 'pass' {
  try {
    run();
    return 'pass';
  } catch (error) {
    const types = ['System.ArgumentOutOfRangeException', 'System.ArgumentNullException', 'System.ArgumentException', 'System.NullReferenceException'];
    const type = types.find((name) => is(error, name)) ?? 'unknown';
    const kept = error as { message: string; paramName?: unknown; actualValue?: unknown };
    return { type, message: kept.message, paramName: kept.paramName, actualValue: kept.actualValue };
  }
}

describe('the range guards throw as .NET throws them', () => {
  it('writes the value in the culture the compiler hands, and the actual value on a line of its own', () => {
    expect(thrown(() => guard.throwIfNegative(-1.5, 'v', ptBR))).toEqual({
      type: 'System.ArgumentOutOfRangeException',
      message: "v ('-1,5') must be a non-negative value. (Parameter 'v')\nActual value was -1,5.",
      paramName: 'v',
      actualValue: -1.5,
    });
  });

  it('takes a NaN and a negative zero for negative, and a negative zero for zero', () => {
    expect(thrown(() => guard.throwIfNegative(Number.NaN, 'v', plain))).not.toBe('pass');
    expect(thrown(() => guard.throwIfNegative(-0, 'v', plain))).not.toBe('pass');
    expect(thrown(() => guard.throwIfZero(-0, 'v', plain))).not.toBe('pass');
    expect(thrown(() => guard.throwIfNegativeOrZero(Number.NaN, 'v', plain))).not.toBe('pass');
    expect(thrown(() => guard.throwIfNegative(0, 'v', plain))).toBe('pass');
  });

  it('reads a long, a decimal and a char by their own sign', () => {
    expect(thrown(() => guard.throwIfNegative(-2n, 'v', plain))).not.toBe('pass');
    expect(thrown(() => guard.throwIfNegative(Decimal.from('-2.5'), 'v', plain))).not.toBe('pass');
    expect(thrown(() => guard.throwIfZero(Decimal.from('0.00'), 'v', plain))).not.toBe('pass');
    expect(thrown(() => guard.throwIfZero('\0', 'v', plain))).not.toBe('pass');
    expect(thrown(() => guard.throwIfNegative('a', 'v', plain))).toBe('pass');
  });

  it('writes a missing name as nothing and leaves the parameter out', () => {
    expect(thrown(() => guard.throwIfNegative(-1, null, plain))).toMatchObject({
      message: " ('-1') must be a non-negative value.\nActual value was -1.",
      paramName: null,
    });
    expect(thrown(() => guard.throwIfNegative(-1, '', plain))).toMatchObject({
      message: " ('-1') must be a non-negative value.\nActual value was -1.",
      paramName: '',
    });
  });

  it('compares by the equality handed, a null written as null and no actual value', () => {
    expect(thrown(() => guard.throwIfEqual(Number.NaN, Number.NaN, 'v', same, plain))).not.toBe('pass');
    expect(thrown(() => guard.throwIfNotEqual<string | null>(null, 'a', 'v', same, plain))).toEqual({
      type: 'System.ArgumentOutOfRangeException',
      message: "v ('null') must be equal to 'a'. (Parameter 'v')",
      paramName: 'v',
      actualValue: null,
    });
    expect(thrown(() => guard.throwIfNotEqual<string | null>('a', null, 'v', same, plain))).toMatchObject({
      message: "v ('a') must be equal to 'null'. (Parameter 'v')\nActual value was a.",
    });
  });

  it('orders by the ordering handed, a NaN equal to a NaN and before every number', () => {
    expect(thrown(() => guard.throwIfGreaterThan(Number.NaN, Number.NaN, 'v', 'real', plain))).toBe('pass');
    expect(thrown(() => guard.throwIfLessThan(Number.NaN, -Infinity, 'v', 'real', plain))).not.toBe('pass');
    expect(thrown(() => guard.throwIfLessThan(-0, 0, 'v', 'real', plain))).toBe('pass');
    expect(thrown(() => guard.throwIfGreaterThan(5, 4, 'v', 'value', plain))).toMatchObject({
      message: "v ('5') must be less than or equal to '4'. (Parameter 'v')\nActual value was 5.",
    });
    expect(thrown(() => guard.throwIfGreaterThanOrEqual(4, 4, 'v', 'value', plain))).toMatchObject({
      message: "v ('4') must be less than '4'. (Parameter 'v')\nActual value was 4.",
    });
    expect(thrown(() => guard.throwIfLessThan(3, 4, 'v', 'value', plain))).toMatchObject({
      message: "v ('3') must be greater than or equal to '4'. (Parameter 'v')\nActual value was 3.",
    });
    expect(thrown(() => guard.throwIfLessThanOrEqual(4, 4, 'v', 'value', plain))).toMatchObject({
      message: "v ('4') must be greater than '4'. (Parameter 'v')\nActual value was 4.",
    });
  });

  it('calls CompareTo through a null value, and writes a null other as nothing', () => {
    expect(thrown(() => guard.throwIfLessThan<string | null>(null, 'a', 'v', 'text', plain))).toMatchObject({
      type: 'System.NullReferenceException',
    });
    expect(thrown(() => guard.throwIfGreaterThan<string | null>('a', null, 'v', 'text', plain))).toMatchObject({
      message: "v ('a') must be less than or equal to ''. (Parameter 'v')\nActual value was a.",
    });
  });

  it("asks a comparable value's own compareTo", () => {
    const asked: unknown[] = [];
    const value = { compareTo: (other: unknown) => (asked.push(other), 1) };
    const other = 'other' as unknown as typeof value;
    expect(thrown(() => guard.throwIfGreaterThan(value, other, 'v', 'comparable', () => 'x'))).not.toBe('pass');
    expect(asked).toEqual(['other']);
  });
});

describe('the null and white guards throw as .NET throws them', () => {
  it('refuses a null with its name, or none', () => {
    expect(thrown(() => guard.throwIfNull(undefined, 'v'))).toEqual({
      type: 'System.ArgumentNullException',
      message: "Value cannot be null. (Parameter 'v')",
      paramName: 'v',
      actualValue: undefined,
    });
    expect(thrown(() => guard.throwIfNull(null, null))).toMatchObject({ message: 'Value cannot be null.', paramName: null });
    expect(thrown(() => guard.throwIfNull(0, 'v'))).toBe('pass');
  });

  it('refuses an empty string, and a white one as char.IsWhiteSpace reads it', () => {
    expect(thrown(() => guard.throwIfNullOrEmpty('', 'v'))).toMatchObject({
      type: 'System.ArgumentException',
      message: "The value cannot be an empty string. (Parameter 'v')",
    });
    expect(thrown(() => guard.throwIfNullOrEmpty(null, 'v'))).toMatchObject({ type: 'System.ArgumentNullException' });
    expect(thrown(() => guard.throwIfNullOrWhiteSpace('\u0085 ', 'v'))).toMatchObject({
      type: 'System.ArgumentException',
      message: "The value cannot be an empty string or composed entirely of whitespace. (Parameter 'v')",
    });
    expect(thrown(() => guard.throwIfNullOrWhiteSpace('', 'v'))).toMatchObject({ type: 'System.ArgumentException' });
    expect(thrown(() => guard.throwIfNullOrWhiteSpace('\u200B', 'v'))).toBe('pass');
  });
});
