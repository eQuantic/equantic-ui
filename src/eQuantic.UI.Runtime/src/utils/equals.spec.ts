import { describe, it, expect } from 'vitest';
import { equals, equalsGroup, equalsInstance } from './equals';
import { is } from './exceptions';
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
    expect(equals(dateTime.of(2024, 1, 15), dateTime.of(2024, 1, 15))).toBe(true);
    expect(equals(dateTime.of(2024, 1, 15), dateTime.of(2024, 1, 16))).toBe(false);
    // record holding a decimal field
    expect(equals({ price: dec('9.99') }, { price: dec('9.99') })).toBe(true);
  });

  it("holds NaN equal to itself, as a double's Equals does", () => {
    // EqualityComparer<double>.Default: two records with a NaN member are one key in .NET.
    expect(equals(NaN, NaN)).toBe(true);
    expect(equals({ x: NaN }, { x: NaN })).toBe(true);
    expect(equals([NaN, 1], [NaN, 1])).toBe(true); // a tuple's Equals
    expect(equals(0, -0)).toBe(true);
    expect(equals(NaN, 0)).toBe(false);
    expect(equals(1, 2)).toBe(false);
  });
});

// The method group `value.Equals` of a value the browser holds as data: the delegate compares the
// value it was made with, as the call does, whatever happens to the variable afterwards (#731).
describe('equalsGroup', () => {
  it('compares the value it was made with, as equals does', () => {
    let held = { x1: 0.2, y1: 0, x2: 0, y2: 1 };
    const same = equalsGroup(held);
    held = { x1: 0, y1: 0, x2: 0, y2: 1 };

    expect(same({ x1: 0.2, y1: 0, x2: 0, y2: 1 })).toBe(true);
    expect(same(held)).toBe(false);
    expect(same('standard')).toBe(false);
  });

  // A tuple holding an array compares that element by reference, which the compiler says by handing
  // the comparison its type compares by.
  it('compares by the comparison it is handed, where the type has one of its own', () => {
    const xs = [1];
    const byReference = (a: unknown, b: unknown) =>
      Array.isArray(a) && Array.isArray(b) && a[0] === b[0] && a[1] === b[1];
    const same = equalsGroup([xs, 1], byReference);

    expect(same([xs, 1])).toBe(true);
    expect(same([[1], 1])).toBe(false);
    expect(equalsGroup([xs, 1])([[1], 1])).toBe(true);
  });
});

// An anonymous object's Equals is an instance call: through null it throws .NET's NullReferenceException,
// after its argument was evaluated, and a value the compiler knows is of another type is unequal.
describe('equalsInstance', () => {
  it('refuses a null receiver as a call through null', () => {
    let caught: unknown;
    try {
      equalsInstance(null, null);
    } catch (error) {
      caught = error;
    }
    expect(is(caught, 'System.NullReferenceException')).toBe(true);
    expect((caught as Error).message).toBe('Object reference not set to an instance of an object.');
  });

  it('compares by the comparison it is handed, and finds a value of another type unequal', () => {
    expect(equalsInstance({ a: 1 }, { a: 1 })).toBe(true);
    expect(equalsInstance({ a: 1 }, null)).toBe(false);
    expect(equalsInstance({ a: [1] }, { a: [1] }, (a, b) => a === b)).toBe(false);
    expect(equalsInstance({ a: 1 }, { a: 1 }, null)).toBe(false);
    expect(equalsInstance([1, 2], [1, 2], null)).toBe(false);
  });
});
