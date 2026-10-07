import { describe, expect, it } from 'vitest';
import { memberEquality, pairComparer, sameBy, sameEquality, sameKey, tupleEquality } from './key-equality';
import { dec } from './decimal';
import { equals } from './equals';

// How EqualityComparer<T>.Default finds two values equal, ONE vocabulary for a set, a dictionary and a
// list's searches (#425, #531).
describe('the equality a type compares by', () => {
  it('is identity by default, NaN equal to NaN and -0 to 0, as a double compares', () => {
    const same = sameBy(false);
    expect(same(NaN, NaN)).toBe(true);
    expect(same(-0, 0)).toBe(true);
    expect(same([1], [1])).toBe(false);
    expect(same(dec('1.0'), dec('1.00'))).toBe(false);
  });

  it('is $eq.equals by value, and the value\'s own where the type does not decide', () => {
    expect(sameBy(true)).toBe(equals);
    expect(sameBy('own')).toBe(sameKey);
    expect(sameKey(dec('1.0'), dec('1.00'))).toBe(true);
    expect(sameKey({}, {})).toBe(true);
    expect(sameKey({}, [])).toBe(false);
  });

  it("compares a tuple's elements each by its own type: an array by reference", () => {
    const a = [1];
    const same = tupleEquality(false, false);
    expect(same([a, 1], [a, 1])).toBe(true);
    expect(same([a, 1], [[1], 1])).toBe(false);
    expect(same(null, null)).toBe(true);
    expect(same([a, 1], null)).toBe(false);
    const nested = tupleEquality(tupleEquality(false, false), true);
    expect(nested([[a, 1], dec('2')], [[a, 1], dec('2.0')])).toBe(true);
  });

  it("compares an anonymous type's members each by its own type", () => {
    const b = [1];
    const same = memberEquality({ a: false, b: false });
    expect(same({ a: 1, b }, { a: 1, b })).toBe(true);
    expect(same({ a: 1, b }, { a: 1, b: [1] })).toBe(false);
  });

  it('compares a pair by its halves', () => {
    const same = pairComparer<string, number[]>(false, false);
    const v = [1];
    expect(same({ key: 'a', value: v }, { key: 'a', value: v })).toBe(true);
    expect(same({ key: 'a', value: v }, { key: 'a', value: [1] })).toBe(false);
    expect(pairComparer<string, number[]>(false, true)({ key: 'a', value: v }, { key: 'a', value: [1] })).toBe(true);
  });

  it('is one function per shape, so two sets of one element type hold one comparer', () => {
    expect(tupleEquality(false, true)).toBe(tupleEquality(false, true));
    expect(tupleEquality(false, true)).not.toBe(tupleEquality(true, false));
    expect(sameEquality(tupleEquality(false, false), tupleEquality(false, false))).toBe(true);
    expect(sameEquality(true, equals)).toBe(true);
    expect(sameEquality(false, undefined)).toBe(true);
    expect(sameEquality(true, 'own')).toBe(false);
  });
});
