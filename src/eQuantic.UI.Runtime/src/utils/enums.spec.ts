import { describe, expect, it } from 'vitest';
import { declaredNames, isDefined, name, parse, text, tryParse, unbox, values, zero } from './enums';

/** The shapes the compiler writes for `enum Rank { Zeta, Alpha, Mid = 5 }` and a flags enum. */
const rank = { names: ['Zeta', 'Alpha', 'Mid'], keys: ['zeta', 'alpha', 'mid'], values: [0, 1, 5], flags: false, digits: 8 };
const perm = { names: ['None', 'Read', 'Write', 'Exec'], values: [0, 1, 2, 4], flags: true, digits: 8 };

describe('an enum as .NET reads it', () => {
  it('writes a member by its name, a flags combination by its set flags, and a null as nothing', () => {
    expect(text('mid', rank)).toBe('Mid');
    expect(text(7, rank)).toBe('7');
    expect(text(3, perm)).toBe('Read, Write');
    expect(text(0, perm)).toBe('None');
    expect(text(9, perm)).toBe('9');
    expect(text(null, rank)).toBe('');
    expect(text('Pending', rank)).toBe('Pending');
  });

  it('reads a uint\'s high bit and a long\'s wide flags, and refuses a number past the type', () => {
    const wide = { names: ['A', 'High'], keys: ['a', 'high'], values: [1, 2147483648], flags: false, digits: 8, unsigned: true };
    const longFlags = { names: ['A', 'B'], values: [1n, 2n ** 40n], flags: true, digits: 16 };
    expect(parse('High', wide)).toBe('high');
    expect(text('high', wide, 'X')).toBe('80000000');
    expect(parse('A, B', longFlags)).toBe(2n ** 40n + 1n);
    expect(text(2n ** 40n + 1n, longFlags)).toBe('A, B');
    expect(tryParse('4294967296', wide)).toBeUndefined();
    expect(tryParse('-1', wide)).toBeUndefined();
    expect(text(-1, rank, 'X')).toBe('FFFFFFFF');
  });

  it('writes a format as .NET does: D the number, X the hex, F the set flags', () => {
    expect(text('mid', rank, 'D')).toBe('5');
    expect(text('mid', rank, 'X')).toBe('00000005');
    expect(text(6, rank, 'F')).toBe('6');
    expect(text(3, perm, 'F')).toBe('Read, Write');
    expect(() => text('mid', rank, 'Q')).toThrow();
  });

  it('parses a name, names joined by commas and a number, and refuses the rest', () => {
    expect(parse(' Mid ', rank)).toBe('mid');
    expect(parse('5', rank)).toBe('mid');
    expect(parse('Read, Exec', perm)).toBe(5);
    expect(parse('mid', rank, true)).toBe('mid');
    expect(() => parse('mid', rank)).toThrow("Requested value 'mid' was not found.");
    expect(tryParse('nope', rank)).toBeUndefined();
    expect(zero(rank)).toBe('zeta');
  });

  it('refuses a boxed value of another kind, as GetName and IsDefined over an object do', () => {
    expect(() => name(true, rank, 'object')).toThrow('The value passed in must be an enum base');
    expect(() => name('Mid', rank, 'object')).toThrow('The value passed in must be an enum base');
    expect(() => name(1.5, rank, 'object')).toThrow('The value passed in must be an enum base');
    // A member held as object is a box (`utils/box.ts`): a string of its key is a string, refused as .NET refuses one.
    expect(() => name('mid', rank, 'object')).toThrow('The value passed in must be an enum base');
    expect(() => isDefined(true, rank, 'object')).toThrow('Unknown enum type.');
    expect(() => isDefined(1.5, rank, 'object')).toThrow('Unknown enum type.');
  });

  it('names the member with a value, and nothing for a value none has', () => {
    expect(name('mid', rank, 'held')).toBe('Mid');
    expect(name(5, rank, 'number')).toBe('Mid');
    expect(name(7, rank, 'object')).toBeNull();
    expect(name(3, perm, 'held')).toBeNull();
  });

  it('lists names and values in the order of the values', () => {
    expect(declaredNames(rank)).toEqual(['Zeta', 'Alpha', 'Mid']);
    expect(values(rank)).toEqual(['zeta', 'alpha', 'mid']);
    expect(values(perm)).toEqual([0, 1, 2, 4]);
  });

  it('finds a value given as the enum, a number, a declared name or an object holding any of them', () => {
    expect(isDefined('mid', rank, 'held')).toBe(true);
    expect(isDefined(2, rank, 'number')).toBe(false);
    expect(isDefined('Mid', rank, 'name')).toBe(true);
    expect(isDefined('mid', rank, 'name')).toBe(false);
    // A string held as object is a name, matched as it is written: a member held so is a box (`utils/box.ts`).
    expect(isDefined('mid', rank, 'object')).toBe(false);
    expect(isDefined('Mid', rank, 'object')).toBe(true);
    expect(isDefined(5, rank, 'object')).toBe(true);
    expect(isDefined('Gone', rank, 'object')).toBe(false);
    expect(isDefined(4, perm, 'object')).toBe(true);
  });
});

/**
 * A 64-bit enum holds its values as BigInts, as a long is held (#551): a member past 2^53 was a number
 * that is not its value, so its text, its parse and its order lost the low digits.
 */
describe('a 64-bit enum', () => {
  const span = {
    names: ['Min', 'Zero', 'Max'],
    keys: ['min', 'zero', 'max'],
    values: [-(2n ** 63n), 0n, 2n ** 63n - 1n],
    flags: false,
    digits: 16,
  };
  const huge = { names: ['None', 'A', 'Top'], values: [0n, 1n, 2n ** 62n], flags: true, digits: 16 };

  it('writes, parses and orders its members past 2^53 exactly', () => {
    expect(text('max', span, 'D')).toBe('9223372036854775807');
    expect(text('min', span, 'X')).toBe('8000000000000000');
    expect(text(2n ** 62n + 1n, huge)).toBe('A, Top');
    expect(parse('9223372036854775807', span)).toBe('max');
    expect(parse('Top, A', huge)).toBe(2n ** 62n + 1n);
    expect(parse('4611686018427387909', span)).toBe(4611686018427387909n);
    expect(declaredNames(span)).toEqual(['Zero', 'Max', 'Min']);
    expect(values(huge)).toEqual([0n, 1n, 2n ** 62n]);
    expect(zero(huge)).toBe(0n);
    expect(zero(span)).toBe('zero');
  });

  it('reads a value no member names that crossed as its digits', () => {
    expect(text('4611686018427387909', span, 'D')).toBe('4611686018427387909');
    expect(name(2n ** 62n, huge, 'held')).toBe('Top');
    expect(name(1, huge, 'number')).toBe('A');
  });

  it('unboxes and finds only its own underlying type, as .NET does', () => {
    expect(unbox(1n, huge, 'Huge')).toBe(1n);
    expect(() => unbox(1, huge, 'Huge')).toThrow("Unable to cast object of type 'System.Int32' to type 'Huge'.");
    expect(() => unbox(1n, { names: ['A'], keys: ['a'], values: [1], flags: false, digits: 8 }, 'Status')).toThrow(
      "Unable to cast object of type 'System.Int64' to type 'Status'.",
    );
    expect(isDefined(1n, huge, 'object')).toBe(true);
    expect(() => isDefined(1, huge, 'object')).toThrow(
      "Type passed in was 'System.Int32'; the enum underlying type was 'System.Int64'.",
    );
  });
});
