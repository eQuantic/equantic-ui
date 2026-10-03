import { describe, expect, it } from 'vitest';
import { isDefined, names, parse, text, tryParse, values, zero } from './enums';

/** The shapes the compiler writes for `enum Rank { Zeta, Alpha, Mid = 5 }` and a flags enum. */
const rank = { names: ['Zeta', 'Alpha', 'Mid'], keys: ['zeta', 'alpha', 'mid'], values: [0, 1, 5], flags: false };
const perm = { names: ['None', 'Read', 'Write', 'Exec'], keys: [0, 1, 2, 4], values: [0, 1, 2, 4], flags: true };

describe('an enum as .NET reads it', () => {
  it('writes a member by its name, a flags combination by its set flags, and a null as nothing', () => {
    expect(text('mid', rank)).toBe('Mid');
    expect(text(7, rank)).toBe('7');
    expect(text(3, perm)).toBe('Read, Write');
    expect(text(0, perm)).toBe('None');
    expect(text(9, perm)).toBe('9');
    expect(text(null, rank)).toBe('');
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

  it('lists names and values in the order of the values', () => {
    expect(names(rank)).toEqual(['Zeta', 'Alpha', 'Mid']);
    expect(values(rank)).toEqual(['zeta', 'alpha', 'mid']);
    expect(values(perm)).toEqual([0, 1, 2, 4]);
  });

  it('finds a value given as the enum, a number or a declared name', () => {
    expect(isDefined('mid', rank, 'held')).toBe(true);
    expect(isDefined(2, rank, 'number')).toBe(false);
    expect(isDefined('Mid', rank, 'name')).toBe(true);
    expect(isDefined('mid', rank, 'name')).toBe(false);
  });
});
