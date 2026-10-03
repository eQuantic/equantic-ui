import { describe, expect, it } from 'vitest';
import { declaredNames, isDefined, name, parse, text, tryParse, values, zero } from './enums';

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
    const longFlags = { names: ['A', 'B'], values: [1, 2 ** 40], flags: true, digits: 16 };
    expect(parse('High', wide)).toBe('high');
    expect(text('high', wide, 'X')).toBe('80000000');
    expect(parse('A, B', longFlags)).toBe(2 ** 40 + 1);
    expect(text(2 ** 40 + 1, longFlags)).toBe('A, B');
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
    expect(isDefined('mid', rank, 'object')).toBe(true);
    expect(isDefined('Mid', rank, 'object')).toBe(true);
    expect(isDefined(5, rank, 'object')).toBe(true);
    expect(isDefined('Gone', rank, 'object')).toBe(false);
    expect(isDefined(4, perm, 'object')).toBe(true);
  });
});
