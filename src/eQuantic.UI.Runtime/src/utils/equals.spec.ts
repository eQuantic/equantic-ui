import { describe, it, expect } from 'vitest';
import { equals } from './equals';
import { dec } from './decimal';
import { dateTime } from './datetime';

describe('Structural equality ($eq.equals)', () => {
  it('compares primitives by value', () => {
    expect(equals(1, 1)).toBe(true);
    expect(equals(1, 2)).toBe(false);
    expect(equals('a', 'a')).toBe(true);
    expect(equals('a', 'b')).toBe(false);
    expect(equals(true, true)).toBe(true);
    expect(equals(5n, 5n)).toBe(true);
    expect(equals(5n, 6n)).toBe(false);
  });

  it('treats both-null as equal, one-null as unequal', () => {
    expect(equals(null, null)).toBe(true);
    expect(equals(undefined, undefined)).toBe(true);
    expect(equals(null, undefined)).toBe(true);
    expect(equals(null, { x: 1 })).toBe(false);
    expect(equals({ x: 1 }, null)).toBe(false);
  });

  it('compares records/structs (plain objects) field by field', () => {
    expect(equals({ x: 1, y: 2 }, { x: 1, y: 2 })).toBe(true);
    expect(equals({ x: 1, y: 2 }, { x: 1, y: 3 })).toBe(false);
    expect(equals({ x: 1 }, { x: 1, y: 2 })).toBe(false); // different key count
  });

  it('compares value tuples (arrays) element-wise', () => {
    expect(equals([1, 2], [1, 2])).toBe(true);
    expect(equals([1, 2], [1, 3])).toBe(false);
    expect(equals([1, 2], [1, 2, 3])).toBe(false);
  });

  it('recurses into nested records and tuples', () => {
    expect(equals({ p: { x: 1 }, tags: [1, 2] }, { p: { x: 1 }, tags: [1, 2] })).toBe(true);
    expect(equals({ p: { x: 1 }, tags: [1, 2] }, { p: { x: 2 }, tags: [1, 2] })).toBe(false);
  });

  it('delegates to compat types own .equals (Decimal, DateTime)', () => {
    expect(equals(dec('1.10'), dec('1.10'))).toBe(true);
    expect(equals(dec('1.10'), dec('1.11'))).toBe(false);
    expect(equals(dateTime(2024, 1, 15), dateTime(2024, 1, 15))).toBe(true);
    expect(equals(dateTime(2024, 1, 15), dateTime(2024, 1, 16))).toBe(false);
    // record holding a decimal field
    expect(equals({ price: dec('9.99') }, { price: dec('9.99') })).toBe(true);
  });
});
