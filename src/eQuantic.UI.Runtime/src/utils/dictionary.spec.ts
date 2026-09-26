import { describe, expect, it } from 'vitest';
import { bagEntries, bagSize, Dictionary, dictionary, keyText, pair, wireKey } from './dictionary';
import { dec } from './decimal';
import { dateTime } from './datetime';
import { equals } from './equals';

// Every order below was measured on .NET 10: a dictionary enumerates by slot, and an insertion takes
// the slot freed last.
const keysOf = <K>(d: Dictionary<K, unknown>): K[] => d.keys();

describe('Dictionary — enumeration by slot, as .NET enumerates', () => {
  it('keeps insertion order for integer keys, which a plain object sorted', () => {
    const d = dictionary<number, number>();
    d.set(3, 30);
    d.set(1, 10);
    expect(d.keys()).toEqual([3, 1]);
    expect(d.values()).toEqual([30, 10]);
  });

  it('keeps insertion order for keys that look like integers', () => {
    const d = dictionary<string, number>([
      ['2', 2],
      ['1', 1],
    ]);
    expect(d.keys()).toEqual(['2', '1']);
  });

  it('gives a new key the slot a removal freed', () => {
    const d = dictionary<string, number>([
      ['a', 1],
      ['b', 2],
      ['c', 3],
    ]);
    d.delete('a');
    d.set('d', 4);
    expect(keysOf(d)).toEqual(['d', 'b', 'c']);
  });

  it('reuses the slot freed last first', () => {
    const abc = (): Dictionary<string, number> =>
      dictionary<string, number>([
        ['a', 1],
        ['b', 2],
        ['c', 3],
      ]);
    const first = abc();
    first.delete('b');
    first.delete('a');
    first.set('x', 0).set('y', 0);
    expect(keysOf(first)).toEqual(['x', 'y', 'c']);
    const second = abc();
    second.delete('a');
    second.delete('b');
    second.set('x', 0).set('y', 0);
    expect(keysOf(second)).toEqual(['y', 'x', 'c']);
  });

  it('keeps a key in its slot when its value is written again', () => {
    const d = dictionary<string, number>([
      ['a', 1],
      ['b', 2],
    ]);
    d.set('a', 10);
    expect([...d].map(([k, v]) => `${k}=${v}`)).toEqual(['a=10', 'b=2']);
  });

  it('enumerates a copy compacted, in the order of what it copies', () => {
    const d = dictionary<string, number>([
      ['a', 1],
      ['b', 2],
      ['c', 3],
    ]);
    d.delete('a');
    const copy = dictionary(d);
    copy.set('a', 1);
    expect(keysOf(copy)).toEqual(['b', 'c', 'a']);
  });

  it('starts from the first slot after Clear', () => {
    const d = dictionary<string, number>([
      ['a', 1],
      ['b', 2],
    ]);
    d.delete('a');
    d.clear();
    d.set('z', 1).set('y', 2);
    expect(keysOf(d)).toEqual(['z', 'y']);
    expect(d.size).toBe(2);
  });

  it('counts what it holds, freed slots aside', () => {
    const d = dictionary<number, number>([
      [1, 1],
      [2, 2],
    ]);
    d.delete(1);
    expect(d.size).toBe(1);
    expect(d.delete(1)).toBe(false);
  });
});

describe('Dictionary — keys found by identity', () => {
  it('keeps each key in its own type', () => {
    const d = dictionary<number, string>([
      [3, 'c'],
      [1, 'a'],
    ]);
    let total = 0;
    for (const key of d.keys()) total += key;
    expect(total).toBe(4);
  });

  it('finds NaN as NaN, the way a double equals itself in .NET', () => {
    const d = dictionary<number, string>([[NaN, 'nan']]);
    expect(d.get(NaN)).toBe('nan');
  });

  it('tells a number from the string of its digits', () => {
    const d = dictionary<unknown, string>([[1, 'number']]);
    expect(d.has('1')).toBe(false);
  });

  it('finds a long by its value', () => {
    const d = dictionary<bigint, string>([[9007199254740993n, 'big']]);
    expect(d.get(9007199254740993n)).toBe('big');
  });

  it('finds a class instance by reference', () => {
    const one = { id: 1 };
    const d = dictionary<object, string>([[one, 'one']]);
    expect(d.has({ id: 1 })).toBe(false);
    expect(d.get(one)).toBe('one');
  });

  it('holds "__proto__" as any other key', () => {
    const d = dictionary<string, number>([['__proto__', 1]]);
    expect(d.get('__proto__')).toBe(1);
    expect(d.keys()).toEqual(['__proto__']);
  });
});

describe('Dictionary — keys found by value', () => {
  const pt = (x: number, y: number) => ({ x, y });

  it('finds a record key by value, not by reference', () => {
    const d = dictionary<{ x: number; y: number }, string>(null, true);
    d.set(pt(1, 2), 'a');
    expect(d.get(pt(1, 2))).toBe('a');
    expect(d.has(pt(9, 9))).toBe(false);
  });

  it('writes an equal key over the one there, in its slot', () => {
    const d = dictionary<{ x: number; y: number }, number>(null, true);
    d.set(pt(1, 2), 10);
    d.set(pt(5, 5), 50);
    d.set(pt(1, 2), 20);
    expect(d.size).toBe(2);
    expect(d.values()).toEqual([20, 50]);
  });

  it('frees the slot of an equal key and reuses it', () => {
    const d = dictionary<{ x: number; y: number }, number>(
      [
        [pt(1, 1), 1],
        [pt(2, 2), 2],
      ],
      true,
    );
    expect(d.delete(pt(1, 1))).toBe(true);
    expect(d.delete(pt(1, 1))).toBe(false);
    d.set(pt(3, 3), 3);
    expect(d.keys()).toEqual([pt(3, 3), pt(2, 2)]);
  });

  it('compares a tuple element by element', () => {
    const d = dictionary<[number, number], string>([[[1, 2], 'a']], true);
    expect(d.get([1, 2])).toBe('a');
    expect(d.get([2, 1])).toBeUndefined();
  });

  it('finds a decimal and a date by their own equals', () => {
    const decimals = dictionary<unknown, string>([[dec('1.50'), 'd']], true);
    expect(decimals.get(dec('1.5'))).toBe('d');
    const dates = dictionary<unknown, number>([[dateTime(2026, 1, 1), 1]], true);
    expect(dates.get(dateTime(2026, 1, 1))).toBe(1);
  });
});

describe('Dictionary — the pairs it enumerates', () => {
  it('destructures a pair and reads its key and value', () => {
    const d = dictionary<number, string>([
      [3, 'c'],
      [1, 'a'],
    ]);
    const seen: string[] = [];
    for (const [key, value] of d) seen.push(`${key}${value}`);
    expect(seen).toEqual(['3c', '1a']);
    expect([...d].map((kv) => kv.key * 10)).toEqual([30, 10]);
    expect([...d].map((kv) => kv.value)).toEqual(['c', 'a']);
  });

  it('compares two pairs by value, as a KeyValuePair does', () => {
    expect(equals(pair(1, 'a'), pair(1, 'a'))).toBe(true);
    expect(equals(pair(1, 'a'), pair(1, 'b'))).toBe(false);
  });

  it('equals only itself, as a .NET dictionary does', () => {
    const d = dictionary<number, number>([[1, 1]]);
    expect(equals(d, d)).toBe(true);
    expect(equals(d, dictionary<number, number>([[1, 1]]))).toBe(false);
    expect(equals(dictionary(), dictionary())).toBe(false);
  });
});

describe('Dictionary — the JSON it writes', () => {
  it('writes the object System.Text.Json reads, by each key wire text', () => {
    const d = dictionary<unknown, number>([
      ['b', 1],
      [3, 2],
      [true, 3],
      [9007199254740993n, 4],
      [dec('1.50'), 5],
    ]);
    expect(JSON.stringify(d)).toBe(
      '{"3":2,"b":1,"True":3,"9007199254740993":4,"1.50":5}',
    );
  });

  it('writes "__proto__" as an entry, not as the prototype', () => {
    const json = dictionary<string, number>([['__proto__', 1]]).toJSON();
    expect(Object.keys(json)).toEqual(['__proto__']);
    expect(JSON.stringify(json)).toBe('{"__proto__":1}');
  });

  it('writes a key the way .NET writes it in a message and on the wire', () => {
    expect(keyText(true)).toBe('True');
    expect(keyText(3)).toBe('3');
    expect(wireKey(false)).toBe('False');
    expect(wireKey(dateTime(2026, 1, 2))).toBe('2026-01-02T00:00:00');
  });
});

describe('bagEntries / bagSize — a DOM bag in either form', () => {
  it('walks a dictionary and a plain object alike', () => {
    const d = dictionary<string, string>([
      ['b', '2'],
      ['a', '1'],
    ]);
    expect([...bagEntries(d)].map(([k, v]) => k + v)).toEqual(['b2', 'a1']);
    expect([...bagEntries({ b: '2', a: '1' })].map(([k, v]) => k + v)).toEqual(['b2', 'a1']);
    expect(bagSize(d)).toBe(2);
    expect(bagSize({ a: '1' })).toBe(1);
  });
});
