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
    expect(sameKey({ a: 1, b: 2 }, { b: 2, a: 1 })).toBe(false);
    expect(sameKey({}, [])).toBe(false);
    expect(sameBy('item')(null, undefined)).toBe(true);
  });

  // A tuple held as object is an array, as a list and an array are: only its elements can say what
  // it is, so it is found by them (#947's review), each element by what it turns out to be in turn.
  it('compares a tuple held as object by its elements, each by its own equality', () => {
    class Plain {
      a = 1;
    }
    const shared = new Plain();
    expect(sameKey([1, 'a'], [1, 'a'])).toBe(true);
    expect(sameKey([1, 'a'], [1, 'b'])).toBe(false);
    expect(sameKey([1, 2], [1, 2, 3])).toBe(false);
    expect(sameKey([[1, 2], dec('1.0')], [[1, 2], dec('1.00')])).toBe(true);
    expect(sameKey([shared], [shared])).toBe(true);
    expect(sameKey([new Plain()], [new Plain()])).toBe(false);
    expect(sameKey([{ a: 1, b: 2 }], [{ b: 2, a: 1 }])).toBe(false);
    expect(sameKey([null], [undefined])).toBe(true);
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

  // It is Equals(object): a value of another type is unequal before a member is read (#743).
  it('takes only an anonymous object of the same members, in the same order', () => {
    class Point {
      a = 1;
      b = 2;
    }
    const same = memberEquality({ a: false, b: true });
    expect(same({ a: 1, b: 2 }, { a: 1, b: 2 })).toBe(true);
    expect(same({ a: 1, b: 2 }, { b: 2, a: 1 })).toBe(false);
    expect(same({ a: 1, b: 2 }, { a: 1, b: 2, c: 3 })).toBe(false);
    expect(same({ a: 1, b: 2 }, { a: 1 })).toBe(false);
    expect(same({ a: 1, b: 2 }, [1, 2])).toBe(false);
    expect(same({ a: 1, b: 2 }, new Point())).toBe(false);
    expect(same({ a: 1, b: 2 }, null)).toBe(false);
    expect(memberEquality({})({}, {})).toBe(true);
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
