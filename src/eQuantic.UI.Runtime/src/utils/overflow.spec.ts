import { describe, expect, it } from 'vitest';
import { checked, single } from './overflow';

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
