import { describe, it, expect } from 'vitest';
import { fromBase, toBase } from './convert-base';

// Every answer below was measured on .NET 10 (Convert.ToXxx(string, fromBase) and
// Convert.ToString(value, toBase)); the conformance suite runs the same calls on both sides.
describe('fromBase (Convert.ToXxx(value, fromBase))', () => {
  it('reads a base other than 10 as the bits of the type', () => {
    expect(fromBase('ffffffff', 16, 'int')).toBe(-1);
    expect(fromBase('ffffffff', 16, 'uint')).toBe(4294967295);
    expect(fromBase('80', 16, 'sbyte')).toBe(-128);
    expect(fromBase('ffff', 16, 'short')).toBe(-1);
    expect(fromBase('ffffffffffffffff', 16, 'long')).toBe(-1n);
    expect(fromBase('ffffffffffffffff', 16, 'ulong')).toBe(18446744073709551615n);
  });

  it('takes a 0x prefix in base 16 and a sign in base 10', () => {
    expect(fromBase('0xFF', 16, 'int')).toBe(255);
    expect(fromBase('+ff', 16, 'int')).toBe(255);
    expect(fromBase('-2147483648', 10, 'int')).toBe(-2147483648);
    expect(fromBase('-9223372036854775808', 10, 'long')).toBe(-9223372036854775808n);
  });

  it('types an int as a number and a long as a BigInt', () => {
    const int: number = fromBase('7f', 16, 'int');
    const long: bigint = fromBase('7f', 16, 'long');
    expect([int, long]).toEqual([127, 127n]);
  });

  it('reads a null as 0, after checking the base', () => {
    expect(fromBase(null, 16, 'int')).toBe(0);
    expect(fromBase(null, 16, 'long')).toBe(0n);
    expect(() => fromBase(null, 3, 'int')).toThrow('Invalid Base.');
  });

  it("refuses with .NET's words", () => {
    expect(() => fromBase('-1', 16, 'int')).toThrow(
      'String cannot contain a minus sign if the base is not 10.',
    );
    expect(() => fromBase('-1', 10, 'uint')).toThrow(
      'The string was being parsed as an unsigned number and could not have a negative sign.',
    );
    expect(() => fromBase('0x', 16, 'int')).toThrow('Could not find any recognizable digits.');
    expect(() => fromBase(' 1', 10, 'int')).toThrow('Could not find any recognizable digits.');
    expect(() => fromBase('1g', 16, 'int')).toThrow(
      'Additional non-parsable characters are at the end of the string.',
    );
    expect(() => fromBase('2147483648', 10, 'int')).toThrow(
      'Value was either too large or too small for an Int32.',
    );
    expect(() => fromBase('100000000', 16, 'int')).toThrow(
      'Value was either too large or too small for a UInt32.',
    );
    expect(() => fromBase('100', 16, 'byte')).toThrow(
      'Value was either too large or too small for an unsigned byte.',
    );
    expect(() => fromBase('128', 10, 'sbyte')).toThrow(
      'Value was either too large or too small for a signed byte.',
    );
    expect(() => fromBase('9223372036854775808', 10, 'long')).toThrow(
      'Value was either too large or too small for an Int64.',
    );
  });
});

describe('toBase (Convert.ToString(value, toBase))', () => {
  it("writes a negative number as the type's bits outside base 10", () => {
    expect(toBase(-1, 16, 32)).toBe('ffffffff');
    expect(toBase(-1, 16, 16)).toBe('ffff');
    expect(toBase(-8, 8, 32)).toBe('37777777770');
    expect(toBase(-1n, 16, 64)).toBe('ffffffffffffffff');
    expect(toBase(-5, 10, 32)).toBe('-5');
  });

  it('refuses a base it does not know', () => {
    expect(() => toBase(1, 3, 32)).toThrow('Invalid Base.');
  });
});
