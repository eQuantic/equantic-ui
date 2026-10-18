import { describe, expect, it } from 'vitest';
import {
  equalsOwn,
  equalsOwnGeneric,
  equatable,
  equatableCall,
  memberEquality,
  pairComparer,
  sameBy,
  sameEquality,
  sameGeneric,
  sameItem,
  sameKey,
  tupleEquality,
} from './key-equality';
import { dec } from './decimal';
import { equals } from './equals';
import { hashesByValue } from './hash';
import { dictionary } from './dictionary';

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

  // A vocabulary value type's twin (`Point`) has no `equals` and is no plain data: it compares by its
  // members, alone and inside a tuple held as object, as its record struct does, and only with one of
  // its own type, where a class of the app's stays by reference (Copilot's second review of #947).
  it('compares a registered value twin by its members, and only with one of its own type', () => {
    class KeyEqualitySpecSpot {
      constructor(
        public x: number,
        public y: number,
      ) {}
    }
    class KeyEqualitySpecOther {
      constructor(
        public x: number,
        public y: number,
      ) {}
    }
    hashesByValue(KeyEqualitySpecSpot, KeyEqualitySpecOther);
    expect(sameKey(new KeyEqualitySpecSpot(1, 2), new KeyEqualitySpecSpot(1, 2))).toBe(true);
    expect(sameKey(new KeyEqualitySpecSpot(1, 2), new KeyEqualitySpecSpot(2, 1))).toBe(false);
    expect(sameKey(new KeyEqualitySpecSpot(1, 2), new KeyEqualitySpecOther(1, 2))).toBe(false);
    expect(sameKey([new KeyEqualitySpecSpot(1, 2), 1], [new KeyEqualitySpecSpot(1, 2), 1])).toBe(true);
    const d = dictionary<unknown, number>(null, 'own');
    d.set([new KeyEqualitySpecSpot(1, 2), 1], 7);
    d.set(new KeyEqualitySpecSpot(3, 4), 8);
    expect(d.get([new KeyEqualitySpecSpot(1, 2), 1])).toBe(7);
    expect(d.get(new KeyEqualitySpecSpot(3, 4))).toBe(8);
    expect(d.size).toBe(2);
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

// A typed Equals of the app's own holds a name of its own on its twin, whose `static $equatable` maps each
// interface contract it answers to it: a call through the interface reaches the contract it binds, a set of
// the type asks it with nulls kept apart, and a type parameter asks only IEquatable<T> over the value's own
// type (Copilot's third review of #947).
describe("an interface's typed Equals, by the contract it binds", () => {
  class NullLover {
    static $equatable = { 'System.IEquatable<T>|NullLover': 'equals$1', 'System.IEquatable<T>|@': 'equals$1' };
    calls = 0;
    equals$1(other: unknown): boolean {
      this.calls++;
      return other === null;
    }
  }
  class IntMatcher {
    static $equatable = { 'System.IEquatable<T>|int': 'equals$1' };
    equals$1(n: unknown): boolean {
      return n === 1;
    }
  }
  class TwoWays {
    static $equatable = { 'System.IEquatable<T>|int': 'equals$1', 'System.IEquatable<T>|string': 'equals$2' };
    equals$1(n: unknown): boolean {
      return n === 1;
    }
    equals$2(s: unknown): boolean {
      return s === 'x';
    }
  }
  class Id {
    static $equatable = { 'System.IEquatable<T>|Id': 'equals$1', 'System.IEquatable<T>|@': 'equals$1' };
    constructor(readonly v: number) {}
    equals$1(o: unknown): boolean {
      return o instanceof Id && o.v === this.v;
    }
    getHashCode(): number {
      return this.v;
    }
  }

  it('asks the method the twin answers the contract with', () => {
    expect(equatable('System.IEquatable<T>|int')(new IntMatcher(), 1)).toBe(true);
    expect(equatable('System.IEquatable<T>|int')(new IntMatcher(), 2)).toBe(false);
    expect(equatable('System.IEquatable<T>|int')(new TwoWays(), 1)).toBe(true);
    expect(equatable('System.IEquatable<T>|string')(new TwoWays(), 'x')).toBe(true);
    expect(equatable('System.IEquatable<T>|string')(new TwoWays(), 'y')).toBe(false);
    expect(equatable('System.IEquatable<T>|Id')(new Id(1), new Id(1))).toBe(true);
  });

  it('answers by what a value is where its twin answers no such contract', () => {
    expect(equatable('System.IEquatable<T>|int')(5, 5)).toBe(true);
    expect(equatable('System.IEquatable<T>|string')('a', 'b')).toBe(false);
    expect(equatable('System.IEquatable<T>|Point')([1, 2], [1, 2])).toBe(true);
  });

  it("takes the class's own contract, or its only one, for a contract over a type parameter", () => {
    expect(equatable('System.IEquatable<T>|*')(new Id(1), new Id(1))).toBe(true);
    expect(equatable('System.IEquatable<T>|*')(new IntMatcher(), 1)).toBe(true);
    // Over two other types and not over itself, the erased type argument cannot say which: Equals(object).
    const both = new TwoWays();
    expect(equatable('System.IEquatable<T>|*')(both, 1)).toBe(false);
    expect(equatable('System.IEquatable<T>|*')(both, both)).toBe(true);
  });

  it("is EqualityComparer<T>.Default's for a type that answers it, which answers a null itself", () => {
    const c = new NullLover();
    const same = equatable('System.IEquatable<T>|NullLover');
    expect(same(null, null)).toBe(true);
    expect(same(c, null)).toBe(false);
    expect(same(null, c)).toBe(false);
    expect(c.calls).toBe(0);
    expect(same(c, new NullLover())).toBe(false);
    expect(c.calls).toBe(1);
    expect(equatable('System.IEquatable<T>|NullLover')).toBe(same);
  });

  it("asks a type parameter's value only IEquatable<T> over its own type", () => {
    const c = new NullLover();
    expect(sameBy('gen')).toBe(sameGeneric);
    expect(sameGeneric(null, null)).toBe(true);
    expect(sameGeneric(c, null)).toBe(false);
    expect(c.calls).toBe(0);
    expect(sameGeneric(new Id(1), new Id(1))).toBe(true);
    expect(sameGeneric(new Id(1), new Id(2))).toBe(false);
    // IEquatable<int> is no comparer of IntMatcher's: .NET compares it by Equals(object), by reference here.
    const m = new IntMatcher();
    expect(sameGeneric(m, new IntMatcher())).toBe(false);
    expect(sameGeneric(m, m)).toBe(true);
    expect(sameGeneric([1, 'a'], [1, 'a'])).toBe(true);
    expect(sameGeneric(1, 1)).toBe(true);
    expect(sameEquality('gen', sameGeneric)).toBe(true);
    expect(sameEquality('gen', 'own')).toBe(false);
  });

  it("finds a type parameter's key by its IEquatable<T> over its own type, and any other by what it is", () => {
    const d = dictionary<unknown, string>(null, 'gen');
    d.add(new Id(1), 'one');
    expect(d.has(new Id(1))).toBe(true);
    expect(d.has(new Id(2))).toBe(false);
    const m = new IntMatcher();
    d.add(m, 'm');
    expect(d.has(new IntMatcher())).toBe(false);
    expect(d.has(m)).toBe(true);
    d.add([1, 2], 'pair');
    expect(d.has([1, 2])).toBe(true);
  });

  it('asks the method a call binds with a null argument too, as the call does', () => {
    const c = new NullLover();
    expect(equatableCall('System.IEquatable<T>|NullLover')(c, null)).toBe(true);
    expect(equatableCall('System.IEquatable<T>|*')(c, null)).toBe(true);
    expect(c.calls).toBe(2);
    expect(equatableCall('System.IEquatable<T>|int')(new IntMatcher(), 1)).toBe(true);
    expect(equatableCall('System.IEquatable<T>|int')(5, 5)).toBe(true);
    expect(equatableCall('System.IEquatable<T>|NullLover')).toBe(equatableCall('System.IEquatable<T>|NullLover'));
    expect(equatableCall('System.IEquatable<T>|NullLover')).not.toBe(equatable('System.IEquatable<T>|NullLover'));
  });
});

describe('a value whose own equality the app wrote is asked every time', () => {
  class Counted {
    static calls = 0;
    constructor(readonly n: number) {}
    equals(o: unknown): boolean {
      Counted.calls++;
      return o instanceof Counted && o.n === this.n;
    }
  }

  class Contrary {
    static calls = 0;
    equals(): boolean {
      Contrary.calls++;
      return false;
    }
  }

  class TypedClass {
    static calls = 0;
    static typedCalls = 0;
    static $equatable = { 'System.IEquatable<T>|TypedClass': 'equals$1', 'System.IEquatable<T>|@': 'equals$1' };
    constructor(readonly n: number) {}
    equals$1(other: TypedClass | null): boolean {
      TypedClass.typedCalls++;
      return other !== null && other.n === this.n;
    }
    equals(o: unknown): boolean {
      TypedClass.calls++;
      return o instanceof TypedClass && this.equals$1(o);
    }
  }

  class Plain {
    constructor(readonly n: number) {}
  }

  it('equalsOwn asks the value its own equals, the value itself included, and a refusal of itself stands', () => {
    Counted.calls = 0;
    const a = new Counted(1);
    expect(equalsOwn(a, a)).toBe(true);
    expect(equalsOwn(a, new Counted(1))).toBe(true);
    expect(equalsOwn(a, new Counted(2))).toBe(false);
    expect(Counted.calls).toBe(3);
    Contrary.calls = 0;
    const c = new Contrary();
    expect(equalsOwn(c, c)).toBe(false);
    expect(Contrary.calls).toBe(1);
  });

  it('equalsOwn answers a null without asking: equal only to another null', () => {
    Counted.calls = 0;
    const a = new Counted(1);
    expect(equalsOwn(a, null)).toBe(false);
    expect(equalsOwn(undefined, a)).toBe(false);
    expect(equalsOwn(null, undefined)).toBe(true);
    expect(Counted.calls).toBe(0);
  });

  it('equalsOwn compares a value with no equals of its own as its type says: what it is, or its reference', () => {
    expect(equalsOwn({ r: 1 }, { r: 1 })).toBe(true);
    expect(equalsOwn([1, 2], [1, 2])).toBe(true);
    const p = new Plain(1);
    expect(equalsOwn(p, p)).toBe(true);
    expect(equalsOwn(p, new Plain(1))).toBe(false);
    expect(equalsOwn([1], [1], sameItem)).toBe(false);
  });

  it('equalsOwnGeneric asks the typed IEquatable<T> a twin answers, and its own equals otherwise, every time', () => {
    TypedClass.calls = 0;
    TypedClass.typedCalls = 0;
    const t = new TypedClass(1);
    expect(equalsOwnGeneric(t, t)).toBe(true);
    expect(TypedClass.typedCalls).toBe(1);
    expect(TypedClass.calls).toBe(0);
    Counted.calls = 0;
    const a = new Counted(1);
    expect(equalsOwnGeneric(a, a)).toBe(true);
    expect(Counted.calls).toBe(1);
    expect(equalsOwnGeneric(1, 1)).toBe(true);
    expect(equalsOwnGeneric(Number.NaN, Number.NaN)).toBe(true);
    expect(equalsOwnGeneric(null, null)).toBe(true);
    expect(equalsOwnGeneric(a, null)).toBe(false);
  });
});
