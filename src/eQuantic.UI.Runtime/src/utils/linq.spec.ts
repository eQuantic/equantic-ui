import { describe, it, expect } from 'vitest';
import {
  average,
  firstOrDefault,
  iterate,
  lastOrDefault,
  max,
  min,
  sequenceEqual,
  toDictionary,
  zipTuples,
} from './linq';
import { Decimal } from './decimal';
import { is } from './exceptions';
import { Dictionary } from './dictionary';

// Every answer below was measured on .NET 10; the conformance suite runs the same calls on both sides.
describe('max and min (LINQ Max/Min)', () => {
  it('throws for an empty sequence of a value type, and answers null for one that takes a null', () => {
    expect(() => max([], undefined, 'value', false)).toThrow('Sequence contains no elements');
    expect(max([], undefined, 'value', true)).toBe(null);
    expect(max([null, null], undefined, 'value', true)).toBe(null);
    expect(min([null, 2, 5], undefined, 'value', true)).toBe(2);
  });

  it("follows .NET's rules for NaN", () => {
    expect(max([1, NaN, 3], undefined, 'real', false)).toBe(3);
    expect(max([NaN, NaN], undefined, 'real', false)).toBeNaN();
    expect(min([1, NaN, 3], undefined, 'real', false)).toBeNaN();
    let read = 0;
    min([1, NaN, 3], (x: number) => (read++, x), 'real', false);
    expect(read).toBe(2); // Min stops at the NaN
  });

  it('keeps the first of two equal values', () => {
    expect(Object.is(max([-0, 0], undefined, 'real', false), -0)).toBe(true);
    expect(Object.is(min([0, -0], undefined, 'real', false), 0)).toBe(true);
  });

  it('orders text in the culture, longs as BigInts, and the rest by compareTo', () => {
    expect(max(['a', 'B'], undefined, 'text', true)).toBe('B');
    expect(min(['a', 'B'], undefined, 'text', true)).toBe('a');
    expect(max([1n, 5n], undefined, 'value', false)).toBe(5n);
    const at = (n: number) => ({ n, compareTo: (o: { n: number }) => n - o.n });
    expect(max([at(1), at(3), at(2)], undefined, 'comparable', false).n).toBe(3);
  });
});

describe('toDictionary (LINQ ToDictionary)', () => {
  it('maps each element into the dictionary class, in the order of its source', () => {
    const d = toDictionary(
      [3, 1, 2],
      (x) => x,
      (x) => x * 10,
    );
    expect(d).toBeInstanceOf(Dictionary);
    expect(d.keys()).toEqual([3, 1, 2]);
    expect(d.values()).toEqual([30, 10, 20]);
  });

  it('holds "__proto__" as an entry of its own', () => {
    const d = toDictionary(['__proto__', 'a'], (x) => x);
    expect(d.keys()).toEqual(['__proto__', 'a']);
    expect(() => toDictionary(['__proto__', '__proto__'], (x) => x)).toThrow(
      'An item with the same key has already been added. Key: __proto__',
    );
  });

  it('keys a structural value by its value when asked to', () => {
    const point = (x: number, y: number) => ({ x, y });
    const d = toDictionary(
      [point(1, 2), point(3, 4)],
      (p) => p,
      (p) => p.x,
      true,
    );
    expect(d.size).toBe(2);
    expect(d.get(point(3, 4))).toBe(3);
    expect(() => toDictionary([point(1, 2), point(1, 2)], (p) => p, null, true)).toThrow(
      'An item with the same key has already been added.',
    );
  });

  it('writes a bool key in its message as .NET does', () => {
    expect(() => toDictionary([true, true], (x) => x)).toThrow(
      'An item with the same key has already been added. Key: True',
    );
  });

  it("refuses a key twice and a null key, with .NET's words", () => {
    expect(() => toDictionary([1, 2, 1], (x) => x)).toThrow(
      'An item with the same key has already been added. Key: 1',
    );
    expect(() => toDictionary(['a', null], (x) => x)).toThrow(
      "Value cannot be null. (Parameter 'key')",
    );
  });
});

describe("iterate (a twin's [Symbol.iterator], #612)", () => {
  /** An enumerator as a twin of the app's writes one: MoveNext, Current and Dispose. */
  const countdown = (from: number, log: string[]) => {
    let at = from + 1;
    return {
      moveNext: () => --at > 0,
      get current() {
        return at;
      },
      dispose: () => log.push('disposed'),
    };
  };

  it('iterates an enumerator an iterator method filled, and a sequence of its own, as they are', () => {
    const letters = new Set(['a', 'b']).values();
    expect([...{ [Symbol.iterator]: () => iterate([1, 2, 3]) }]).toEqual([1, 2, 3]);
    expect([...{ [Symbol.iterator]: () => iterate(letters) }]).toEqual(['a', 'b']);
  });

  it('walks one the app wrote by its MoveNext and Current, and disposes it when the walk ends', () => {
    const log: string[] = [];
    expect([...{ [Symbol.iterator]: () => iterate(countdown(3, log)) }]).toEqual([3, 2, 1]);
    expect(log).toEqual(['disposed']);
  });

  it('walks a class that is its own enumerator by its MoveNext, where its own iteration came back here', () => {
    // The twin of a class that implements IEnumerable<int> and IEnumerator<int>, `GetEnumerator() => this`.
    class Ticker {
      private at = 0;
      [Symbol.iterator](): Iterator<number> {
        return iterate(this.getEnumerator());
      }
      getEnumerator(): Ticker {
        this.at = 0;
        return this;
      }
      moveNext(): boolean {
        return ++this.at <= 3;
      }
      get current(): number {
        return this.at;
      }
    }
    expect([...new Ticker()]).toEqual([1, 2, 3]);
  });

  it('disposes it when a loop leaves early, as a foreach does', () => {
    const log: string[] = [];
    for (const n of { [Symbol.iterator]: () => iterate(countdown(5, log)) }) if (n === 4) break;
    expect(log).toEqual(['disposed']);
  });

  it("refuses a null enumerator with .NET's NullReferenceException", () => {
    let thrown: unknown;
    try {
      iterate(null as unknown as number[]);
    } catch (error) {
      thrown = error;
    }
    expect(is(thrown, 'System.NullReferenceException')).toBe(true);
  });
});

describe('average (LINQ Average, #719)', () => {
  it('adds longs as BigInts and divides as a double', () => {
    expect(average([1n, 2n, 4n], undefined, 'long', false)).toBe(7 / 3);
    expect(average([9007199254740993n, 9007199254740995n], undefined, 'long', false)).toBe(
      9007199254740994,
    );
    expect(average([1, 2], (x: number) => BigInt(x * 3), 'long', false)).toBe(4.5);
  });

  it('refuses a long total past 64 bits, as .NET checks its sum', () => {
    expect(() => average([9223372036854775807n, 1n], undefined, 'long', false)).toThrow(
      'Arithmetic operation resulted in an overflow.',
    );
  });

  it('passes a null over, and answers null for a nullable sequence with no value', () => {
    expect(average([1, null, 4], undefined, 'number', true)).toBe(2.5);
    expect(average([1n, null, 4n], undefined, 'long', true)).toBe(2.5);
    expect(average([null], undefined, 'long', true)).toBe(null);
    expect(average([], undefined, 'number', true)).toBe(null);
  });

  it('refuses an empty sequence whose answer is not nullable', () => {
    expect(() => average([], undefined, 'number', false)).toThrow('Sequence contains no elements');
    expect(() => average([], undefined, 'long', false)).toThrow('Sequence contains no elements');
  });

  it('rounds a single once, at the end, and keeps a decimal exact', () => {
    expect(average([Math.fround(0.1), Math.fround(0.2)], undefined, 'single', false)).toBe(
      Math.fround((Math.fround(0.1) + Math.fround(0.2)) / 2),
    );
    const mean = average(
      [Decimal.from('1.0'), Decimal.from(2)],
      undefined,
      'decimal',
      false,
    ) as Decimal;
    expect(mean.toString()).toBe('1.5');
  });

  it('refuses a null source and a null selector by name', () => {
    expect(() => average(null as unknown as number[], undefined, 'number', false)).toThrow(
      "(Parameter 'source')",
    );
    expect(() => average([1], null, 'number', false)).toThrow("(Parameter 'selector')");
  });
});

describe('firstOrDefault and lastOrDefault with a default value (#720)', () => {
  it('answers the element, a null one included, or the value handed', () => {
    expect(firstOrDefault([1, 2], undefined, 9)).toBe(1);
    expect(firstOrDefault([], undefined, 9)).toBe(9);
    expect(firstOrDefault([null, 'a'], undefined, 'x')).toBe(null);
    expect(firstOrDefault([1, 2, 3], (x: number) => x > 1, 9)).toBe(2);
    expect(firstOrDefault([1, 2, 3], (x: number) => x > 5, 9)).toBe(9);
    expect(lastOrDefault([1, 2], undefined, 9)).toBe(2);
    expect(lastOrDefault([], undefined, 9)).toBe(9);
    expect(lastOrDefault(['a', null], undefined, 'x')).toBe(null);
    expect(lastOrDefault(new Set([1, 2, 3]), (x: number) => x < 3, 9)).toBe(2);
  });

  it('searches a list from its end, as .NET searches an IList', () => {
    let seen = 0;
    expect(lastOrDefault([1, 2, 3], (x: number) => (seen++, x === 2), 9)).toBe(2);
    expect(seen).toBe(2);
  });

  it('refuses a null source and a null predicate by name', () => {
    expect(() => firstOrDefault(null as unknown as number[], undefined, 9)).toThrow(
      "(Parameter 'source')",
    );
    expect(() => lastOrDefault([1], null, 9)).toThrow("(Parameter 'predicate')");
  });
});

describe('sequenceEqual (LINQ SequenceEqual, #721)', () => {
  it('compares by the equality it is handed', () => {
    expect(sequenceEqual([1n, 2n], [1n, 2n])).toBe(true);
    expect(sequenceEqual([NaN], [NaN])).toBe(true);
    expect(sequenceEqual([null], [NaN])).toBe(false);
    const a = [1];
    expect(sequenceEqual([a], [[1]])).toBe(false);
    expect(sequenceEqual([a], [a])).toBe(true);
    expect(sequenceEqual([Decimal.from('1.0')], [Decimal.from('1.00')], true)).toBe(true);
    expect(sequenceEqual([{ x: 1 }], [{ x: 1 }], true)).toBe(true);
  });

  it('tells sequences of different lengths apart', () => {
    expect(sequenceEqual([1, 2], [1, 2, 3])).toBe(false);
    expect(sequenceEqual(new Set([1, 2, 3]), [1, 2])).toBe(false);
  });

  it('refuses a null sequence by name, and reads a null span as empty', () => {
    expect(() => sequenceEqual(null, [1])).toThrow("(Parameter 'first')");
    expect(() => sequenceEqual([1], null)).toThrow("(Parameter 'second')");
    expect(sequenceEqual(null, [], false, true)).toBe(true);
  });
});

describe('zipTuples (LINQ Zip without a selector, #722)', () => {
  it('answers pairs and triples, as many as the shortest sequence holds', () => {
    expect(zipTuples([1, 2, 3], ['a', 'b'])).toEqual([
      [1, 'a'],
      [2, 'b'],
    ]);
    expect(zipTuples([1, 2], ['a', 'b', 'c'], [true])).toEqual([[1, 'a', true]]);
  });

  it('refuses each sequence by its name', () => {
    expect(() => zipTuples(null as unknown as number[], [1])).toThrow("(Parameter 'first')");
    expect(() => zipTuples([1], [1], null)).toThrow("(Parameter 'third')");
  });
});
