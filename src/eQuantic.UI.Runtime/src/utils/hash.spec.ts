import { describe, expect, it } from 'vitest';
import { hash, hashCombine, hashFields, hashGroup, identityHash, instanceHash } from './hash';
import { Point, Rect, TypeStyle } from '../shared/value-types';
import { dec } from './decimal';
import { dateTime } from './datetime';
import { equals } from './equals';
import { is as isException } from './exceptions';

/** Values `equals` finds equal hash equal: .NET's contract, the one the browser can keep (#519). */
function hashesAlike(a: unknown, b: unknown): void {
  expect(equals(a, b)).toBe(true);
  expect(hash(a)).toBe(hash(b));
}

describe('a hash agrees with equals ($eq.hash)', () => {
  it('folds NaN with NaN and -0 with 0, and an int is its own hash', () => {
    hashesAlike(Number.NaN, Number.NaN);
    hashesAlike(0, -0);
    expect(hash(42)).toBe(42);
    expect(hash(0.1)).toBe(hash(0.1));
    expect(hash(2 ** 40)).not.toBe(hash(2 ** 40 + 1));
  });

  it('hashes a string, a long, a bool and null', () => {
    expect(hash('abc')).toBe(hash('ab' + 'c'));
    expect(hash('abc')).not.toBe(hash('abd'));
    expect(hash(5n)).toBe(hash(5n));
    expect(hash(true)).toBe(1);
    expect(hash(null)).toBe(0);
    expect(hash(undefined)).toBe(0);
  });

  it('hashes a tuple in order, and a record or anonymous value by its members in any order', () => {
    hashesAlike([1, 'a'], [1, 'a']);
    expect(hash([1, 2])).not.toBe(hash([2, 1]));
    hashesAlike({ x: 1, y: 2 }, { y: 2, x: 1 });
  });

  it('hashes a decimal by its value, whatever its scale, and a date by its ticks', () => {
    hashesAlike(dec('1.0'), dec('1.00'));
    hashesAlike(dateTime.of(2020, 1, 1), dateTime.of(2020, 1, 1));
  });

  it("asks a value's own getHashCode, and hashes any other class by its identity", () => {
    class Overrides {
      getHashCode(): number {
        return 21;
      }
    }
    class Plain {
      constructor(readonly x: number) {}
    }
    expect(hash(new Overrides())).toBe(21);
    const one = new Plain(1);
    expect(hash(one)).toBe(hash(one));
    expect(hash(one)).toBe(identityHash(one));
    expect(hash(one)).not.toBe(hash(new Plain(1)));
  });

  it('hashes a large array by walking it, and a class with an identity equals by its identity', () => {
    const large = new Array(300_000).fill(1);
    expect(hash(large)).toBe(hash(new Array(300_000).fill(1)));
    class Gate {
      equals(other: unknown): boolean {
        return this === other;
      }
    }
    const gate = new Gate();
    expect(hash(gate)).toBe(identityHash(gate));
    expect(hash(gate)).not.toBe(hash(new Gate()));
    expect(hashFields({ x: 1, y: 2 })).toBe(hash({ y: 2, x: 1 }));
  });

  it("hashes a vocabulary value type's hand-written twin by its members, as equals compares it", () => {
    hashesAlike(new Point(1, 2), new Point(1, 2));
    expect(hash(new Point(1, 2))).not.toBe(hash(new Point(2, 1)));
    hashesAlike(new Rect(0, 0, 4, 3), new Rect(0, 0, 4, 3));
    hashesAlike(new TypeStyle(15, 20, 'semiBold', 0.1, 1.3), new TypeStyle(15, 20, 'semiBold', 0.1, 1.3));
    hashesAlike({ at: new Point(1, 2) }, { at: new Point(1, 2) });
    hashesAlike([new Point(1, 2), 1], [new Point(1, 2), 1]);
  });

  it('walks an array that holds itself to an end', () => {
    const looped: unknown[] = [1];
    looped.push(looped);
    expect(hash(looped)).toBe(hash(looped));
  });

  it('refuses a null receiver where .NET throws, and a method group where its delegate is made', () => {
    expect(() => instanceHash(null)).toThrow('Object reference not set to an instance of an object.');
    // .NET's NullReferenceException, the type a typed catch tests (utils/exceptions.ts).
    const refused = (thrower: () => unknown) => {
      try {
        thrower();
      } catch (error) {
        return isException(error, 'System.NullReferenceException');
      }
      return false;
    };
    expect(refused(() => identityHash(null))).toBe(true);
    expect(refused(() => hashGroup(undefined))).toBe(true);
    expect(instanceHash('abc')).toBe(hash('abc'));
    expect(hashGroup(42)()).toBe(42);
    const items = [1];
    const group = hashGroup(items, true);
    items[0] = 2;
    expect(group()).toBe(identityHash(items));
  });

  it('combines in order, as HashCode.Combine does', () => {
    expect(hashCombine(1, 'a')).toBe(hashCombine(1, 'a'));
    expect(hashCombine(1, 2)).not.toBe(hashCombine(2, 1));
  });
});
