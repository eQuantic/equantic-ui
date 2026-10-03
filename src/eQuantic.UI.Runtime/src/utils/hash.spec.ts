import { describe, expect, it } from 'vitest';
import { hash, hashCombine, hashFields, identityHash } from './hash';
import { dec } from './decimal';
import { dateTime } from './datetime';
import { equals } from './equals';

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
    hashesAlike(dateTime(2020, 1, 1), dateTime(2020, 1, 1));
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

  it('combines in order, as HashCode.Combine does', () => {
    expect(hashCombine(1, 'a')).toBe(hashCombine(1, 'a'));
    expect(hashCombine(1, 2)).not.toBe(hashCombine(2, 1));
  });
});
