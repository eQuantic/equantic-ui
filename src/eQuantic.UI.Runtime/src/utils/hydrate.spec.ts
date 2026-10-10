import { readFileSync } from 'node:fs';
import { describe, expect, it } from 'vitest';
import { hydrate, type HydrationSpec } from './hydrate';
import { hydrateValue } from './hydrate-value';
import { Decimal, dec } from './decimal';
import { DateOnly, DateTime, TimeSpan, dateOnly, dateTime } from './datetime';
import { Dictionary, dictionary } from './dictionary';
import { HashSet } from './hash-set';
import { SortedMap, sortedDictionary } from './sorted';
import { Rect } from '../shared/value-types';

describe('typed hydration', () => {
  it('restores the compat scalars from their wire strings', () => {
    expect(hydrate('9007199254740993', 'long')).toBe(9007199254740993n);
    expect(hydrate(42, 'long')).toBe(42n);
    const m = hydrate('0.1', 'decimal');
    expect(m).toBeInstanceOf(Decimal);
    expect((m as Decimal).toString()).toBe('0.1');
    expect(hydrate('2026-08-22T10:30:00', 'dateTime')).toBeInstanceOf(DateTime);
    // A UTC value the server wrote comes back UTC, its clock time kept, where the zone was dropped (#606).
    const utc = hydrate('2026-08-22T10:30:00Z', 'dateTime') as DateTime;
    expect(utc.kind).toBe('utc');
    expect(utc.hour).toBe(10);
    expect(hydrate('01:02:03', 'timeSpan')).toBeInstanceOf(TimeSpan);
  });

  it('is idempotent — a value that already has its runtime type passes through', () => {
    expect(hydrate(5n, 'long')).toBe(5n);
    const m = dec('1.5');
    expect(hydrate(m, 'decimal')).toBe(m);
    const parsed = hydrate('2026-08-22T10:30:00', 'dateTime');
    expect(hydrate(parsed, 'dateTime')).toBe(parsed);
  });

  it("rounds a float back to its single: the wire's shortest text names the single, JS reads a double", () => {
    // "0.38" is what .NET writes for 0.38f; JavaScript parses it as the nearest DOUBLE.
    expect(hydrate(0.38, 'single')).toBe(Math.fround(0.38));
    expect(hydrate(0.38, 'single')).not.toBe(0.38);
    // Idempotent, like every tag: a single hydrates to itself.
    const single = Math.fround(0.1);
    expect(hydrate(single, 'single')).toBe(single);
    // A record member tagged `single` rounds with the rest of the map.
    class Point {
      x = 0;
      static $hydration: Record<string, HydrationSpec> = { x: 'single' };
    }
    const point = hydrate({ x: 0.1 }, Point as never) as Point;
    expect(point.x).toBe(Math.fround(0.1));
  });

  it('lets null and undefined pass', () => {
    expect(hydrate(null, 'long')).toBeNull();
    expect(hydrate(undefined, 'decimal')).toBeUndefined();
  });

  it('hydrates every element of a list', () => {
    expect(hydrate(['1', '2'], ['long'])).toEqual([1n, 2n]);
    const nested = hydrate(
      [
        ['0.1', '0.2'],
        ['0.3', '0.4'],
      ],
      [['decimal']],
    ) as Decimal[][];
    expect(nested[1][0]).toBeInstanceOf(Decimal);
    expect(nested[1][0].toString()).toBe('0.3');
  });

  it("hydrates a dictionary's values into the dictionary class, its keys the property names", () => {
    const result = hydrate({ a: '1', b: '2' }, { dict: 'long' }) as Dictionary<string, bigint>;
    expect(result).toBeInstanceOf(Dictionary);
    expect(result.get('a')).toBe(1n);
    expect(result.get('b')).toBe(2n);
  });

  it('builds a sorted dictionary as its own class, and passes a dictionary through', () => {
    const sorted = hydrate({ b: 2, a: 1 }, { dict: null, sorted: 'dictionary' }) as SortedMap<string, number>;
    expect(sorted).toBeInstanceOf(SortedMap);
    expect(sorted.keys()).toEqual(['a', 'b']);
    const d = dictionary<string, number>([['a', 1]]);
    expect(hydrate(d, { dict: null })).toBe(d);
    expect(hydrate('nope', { dict: null })).toBe('nope');
  });

  it('keeps which sorted collection it is, so a SortedList refuses a repeated key in its own words', () => {
    const list = hydrate({ 2: 'b' }, { dict: null, key: 'number', sorted: 'list' }) as SortedMap<number, string>;
    expect(list.kind).toBe('list');
    expect(() => list.add(2, 'c')).toThrow("An item with the same key has already been added. Key: 2 (Parameter 'key')");
    const index = hydrate({ 2: 'b' }, { dict: null, key: 'number', sorted: 'dictionary' }) as SortedMap<number, string>;
    expect(() => index.add(2, 'c')).toThrow('An item with the same key has already been added. Key: [2, c]');
  });

  it('rebuilds a dictionary the witness path meets as its class, never on its prototype', () => {
    const rebuilt = hydrateValue(dictionary<string, number>(), { b: 2, a: 1 }) as Dictionary<string, number>;
    expect(rebuilt).toBeInstanceOf(Dictionary);
    expect(rebuilt.keys()).toEqual(['b', 'a']);
    expect(rebuilt.get('a')).toBe(1);
    expect(hydrateValue(sortedDictionary<string, number>(), { b: 2, a: 1 })).toBeInstanceOf(SortedMap);
    const paired = hydrateValue(dictionary<number, string>(), [
      [3, 'c'],
      [1, 'a'],
    ]) as Dictionary<number, string>;
    expect(paired).toBeInstanceOf(Dictionary);
    expect(paired.keys()).toEqual([3, 1]);
  });

  // The pairs keep the order an object loses: JSON.parse lists integer-like names first (#437). A key
  // arrives as a value of its type, so a number and a bool are already themselves, and a long, a
  // decimal or a date is revived from its text by the tag the spec carries.
  it('builds a dictionary from its pairs, in their order, each key revived by its tag', () => {
    const scores = hydrate(
      [
        [3, 'c'],
        [1, 'a'],
      ],
      { dict: null, key: 'number' },
    ) as Dictionary<number, string>;
    expect(scores).toBeInstanceOf(Dictionary);
    expect(scores.keys()).toEqual([3, 1]);
    const codes = hydrate(
      [
        ['b', 1],
        ['10', 2],
        ['9', 3],
      ],
      { dict: null },
    ) as Dictionary<string, number>;
    expect(codes.keys()).toEqual(['b', '10', '9']);
    const big = hydrate([['9007199254740993', 'x']], { dict: null, key: 'long' }) as Dictionary<bigint, string>;
    expect(big.get(9007199254740993n)).toBe('x');
    // A NaN or an infinite key arrives as its text, which no JSON number holds.
    const odd = hydrate(
      [
        ['NaN', 'n'],
        ['-Infinity', 'm'],
        [1.5, 'x'],
      ],
      { dict: null, key: 'number' },
    ) as Dictionary<number, string>;
    expect(odd.keys()).toEqual([NaN, -Infinity, 1.5]);
    expect(odd.get(NaN)).toBe('n');
    const flags = hydrate(
      [
        [true, 1],
        [false, 0],
      ],
      { dict: null, key: 'bool' },
    ) as Dictionary<boolean, number>;
    expect(flags.keys()).toEqual([true, false]);
    const values = hydrate([['a', '1']], { dict: 'long' }) as Dictionary<string, bigint>;
    expect(values.get('a')).toBe(1n);
    const sorted = hydrate(
      [
        ['b', 2],
        ['a', 1],
      ],
      { dict: null, sorted: 'dictionary' },
    ) as SortedMap<string, number>;
    expect(sorted).toBeInstanceOf(SortedMap);
    expect(sorted.keys()).toEqual(['a', 'b']);
  });

  it('rebuilds a record twin on its prototype and hydrates its spec-named members', () => {
    class Money {
      static $hydration: Record<string, HydrationSpec> = { amount: 'decimal' };
      amount: Decimal = dec('0');
      currency = '';
      doubled(): Decimal {
        return this.amount.add(this.amount);
      }
    }
    const money = hydrate({ amount: '10.50', currency: 'EUR' }, Money) as Money;
    expect(money).toBeInstanceOf(Money);
    expect(money.amount).toBeInstanceOf(Decimal);
    expect(money.currency).toBe('EUR');
    expect(money.doubled().toString()).toBe('21.00'); // C# scale-preserving: 10.50m + 10.50m

    // Already an instance — passes through untouched.
    expect(hydrate(money, Money)).toBe(money);
  });

  it('hydrates a LIST of record twins — the shape the witness path could never type', () => {
    class Todo {
      static $hydration: Record<string, HydrationSpec> = { id: 'long' };
      id = 0n;
      title = '';
    }
    const todos = hydrate(
      [
        { id: '9007199254740993', title: 'a' },
        { id: '2', title: 'b' },
      ],
      [Todo],
    ) as Todo[];
    expect(todos[0]).toBeInstanceOf(Todo);
    expect(todos[0].id).toBe(9007199254740993n);
    expect(todos[1].id).toBe(2n);
  });

  it('hydrates a record nested in a record through the inner class spec', () => {
    class Price {
      static $hydration: Record<string, HydrationSpec> = { value: 'decimal' };
      value: Decimal = dec('0');
    }
    class Item {
      static $hydration: Record<string, HydrationSpec> = { price: Price };
      price: Price = new Price();
      name = '';
    }
    const item = hydrate({ price: { value: '3.99' }, name: 'x' }, Item) as Item;
    expect(item.price).toBeInstanceOf(Price);
    expect(item.price.value.toString()).toBe('3.99');
  });

  // A vocabulary value type the RUNTIME ships (Rect) is described structurally by the compiler and
  // rebuilt on its twin: a plain copy lost `right`, `isEmpty` and every other member of the prototype.
  it('rebuilds a runtime value type on its twin, with its members coerced', () => {
    const spec: HydrationSpec = {
      of: Rect,
      members: { x: 'single', y: 'single', width: 'single', height: 'single' },
    };
    const box = hydrate({ x: 0.1, y: 0.2, width: 10.1, height: 5 }, spec) as Rect;

    expect(box).toBeInstanceOf(Rect);
    expect(box.x).toBe(Math.fround(0.1));
    expect(box.width).toBe(Math.fround(10.1));
    expect(box.right).toBe(Math.fround(Math.fround(0.1) + Math.fround(10.1)));
  });

  it('passes a value that is already the twin straight through', () => {
    const rect = new Rect(1, 2, 3, 4);
    expect(hydrate(rect, { of: Rect, members: { x: 'single' } })).toBe(rect);
  });

  // A twin that revives what it receives of a type argument (ServerTopic<T>) keeps each argument's
  // spec in the member its `$typeArguments` names: the wire never carries one, and a twin rebuilt
  // without it revived nothing it received.
  it('keeps the specs of the type arguments a twin revives, in the members it names', () => {
    class Holder {
      declare readonly name: string;
      declare readonly first: HydrationSpec | null;
      declare readonly second: HydrationSpec | null;
      static readonly $typeArguments = ['first', 'second'] as const;
    }
    const spec: HydrationSpec = { of: Holder, members: {}, typeArguments: ['decimal', null] };

    const holder = hydrate({ name: 'x' }, spec) as Holder;

    expect(holder).toBeInstanceOf(Holder);
    expect(holder.name).toBe('x');
    expect(holder.first).toBe('decimal');
    expect(holder.second).toBeNull();
  });
});

describe('a tuple', () => {
  it('hydrates positionally, and passes a null position through', () => {
    const spec = { tuple: ['decimal', 'long', null] } as const;
    const [amount, id, label] = hydrate(['1.5', '9007199254740993', 'x'], spec) as unknown[];
    expect(amount).toBeInstanceOf(Decimal);
    expect(String(amount)).toBe('1.5');
    expect(id).toBe(9007199254740993n);
    expect(label).toBe('x');
  });

  it('leaves a value that is not an array alone', () => {
    expect(hydrate('nope', { tuple: ['decimal'] })).toBe('nope');
  });
});

// The payloads as the SERVER writes them, not as a test author types them: ServerPayloadFixtureTests
// serializes them with EqJson and pins the file. Read and parsed here, the way a page parses its
// payload: an import goes through the bundler, and what becomes of a `__proto__` key on that path is
// the bundler's business, not the question these cases ask.
const wire = JSON.parse(readFileSync('src/utils/__fixtures__/server-payload.json', 'utf8')) as {
  rect: { right: number; bottom: number; center: { x: number; y: number } };
  balances: [string, string][];
  scores: [number, string][];
  flags: [boolean, number][];
  big: [string, string][];
  prices: [string, number][];
  days: [string, number][];
  names: [string, number][];
};

describe('a payload the server writes', () => {
  const rectSpec: HydrationSpec = {
    of: Rect,
    members: { x: 'single', y: 'single', width: 'single', height: 'single' },
  };

  // System.Text.Json writes every public property, so a Rect arrives with `right`, `center` and
  // `isEmpty` too. The twin only has getters for those, and assigning one throws in a module.
  it('rebuilds a Rect that arrives with its computed members, which the twin answers itself', () => {
    const box = hydrate(wire.rect, rectSpec) as Rect;

    expect(box).toBeInstanceOf(Rect);
    expect(box.right).toBe(Math.fround(wire.rect.right));
    expect(box.bottom).toBe(Math.fround(wire.rect.bottom));
    expect(box.center.x).toBe(Math.fround(wire.rect.center.x));
    expect(box.center.y).toBe(Math.fround(wire.rect.center.y));
    expect(box.isEmpty).toBe(false);
  });

  it('rebuilds it through a twin with no spec of its own, and through the witness path', () => {
    expect(hydrate(wire.rect, Rect)).toBeInstanceOf(Rect);
    expect(hydrateValue(new Rect(), wire.rect)).toBeInstanceOf(Rect);
  });

  it('keeps a dictionary entry keyed __proto__ as an entry, never as a prototype', () => {
    const balances = hydrate(wire.balances, { dict: 'long' }) as Dictionary<string, bigint>;

    expect(balances).toBeInstanceOf(Dictionary);
    expect(balances.keys()).toEqual(['__proto__', 'a']);
    expect(balances.get('__proto__')).toBe(9007199254740993n);
  });

  it('turns each key the server wrote into a key of its type, in the order it wrote them', () => {
    const scores = hydrate(wire.scores, { dict: null, key: 'number' }) as Dictionary<number, string>;
    // The server wrote 3 before 1, which a JSON object would have listed ascending (#437).
    expect(scores.keys()).toEqual([3, 1]);
    expect(scores.get(3)).toBe('c');
    const flags = hydrate(wire.flags, { dict: null, key: 'bool' }) as Dictionary<boolean, number>;
    expect(flags.keys()).toEqual([true, false]);
    const big = hydrate(wire.big, { dict: null, key: 'long' }) as Dictionary<bigint, string>;
    expect(big.get(9007199254740993n)).toBe('x');
    const prices = hydrate(wire.prices, { dict: null, key: 'decimal', byValue: true }) as Dictionary<
      Decimal,
      number
    >;
    expect(prices.get(dec('1.5'))).toBe(1);
    const days = hydrate(wire.days, { dict: null, key: 'dateOnly', byValue: true }) as Dictionary<
      DateOnly,
      number
    >;
    expect(days.get(dateOnly(2026, 1, 2))).toBe(1);
    const names = hydrate(wire.names, { dict: null }) as Dictionary<string, number>;
    expect(names.keys()).toEqual(['b', 'a']);
  });

  it('writes back the JSON the server wrote', () => {
    for (const [value, spec] of [
      [wire.names, { dict: null }],
      [wire.balances, { dict: 'long' }],
      [wire.scores, { dict: null, key: 'number' }],
      [wire.flags, { dict: null, key: 'bool' }],
      [wire.big, { dict: null, key: 'long' }],
      [wire.prices, { dict: null, key: 'decimal', byValue: true }],
      [wire.days, { dict: null, key: 'dateOnly', byValue: true }],
    ] as const)
      expect(JSON.stringify(hydrate(value, spec))).toBe(JSON.stringify(value));
  });

  it('never lets a member replace the prototype of the value rebuilt from it', () => {
    const hostile = JSON.parse('{"x":1,"__proto__":{"planted":true}}') as Record<string, unknown>;
    const rebuilt = [
      hydrate(hostile, rectSpec),
      hydrate(hostile, Rect),
      hydrateValue(new Rect(), hostile),
    ];

    for (const value of rebuilt) {
      expect(Object.getPrototypeOf(value)).toBe(Rect.prototype);
      expect((value as { planted?: boolean }).planted).toBeUndefined();
    }
  });
});

describe('a collection the browser holds as its own class', () => {
  it('becomes the runtime set, its elements hydrated', () => {
    const roles = hydrate(['admin', 'editor'], { collection: 'set', of: null }) as HashSet<string>;
    expect(roles).toBeInstanceOf(HashSet);
    expect(roles.has('admin')).toBe(true);
    expect(roles.size).toBe(2);
    const ids = hydrate(['9007199254740993'], { collection: 'set', of: 'long' }) as HashSet<bigint>;
    expect(ids.has(9007199254740993n)).toBe(true);
  });

  it('finds a date in a set as .NET does, by value, where the spec says so (#531)', () => {
    const days = hydrate(['2026-01-01T00:00:00'], { collection: 'set', of: 'dateTime', byValue: true }) as HashSet<DateTime>;
    expect(days.has(dateTime.parse('2026-01-01T00:00:00'))).toBe(true);
    const byIdentity = hydrate(['2026-01-01T00:00:00'], { collection: 'set', of: 'dateTime' }) as HashSet<DateTime>;
    expect(byIdentity.has(dateTime.parse('2026-01-01T00:00:00'))).toBe(false);
  });

  it('becomes the runtime class, a stack with its top coming off first', async () => {
    const { Queue, Stack, LinkedList } = await import('./collections');
    const { SortedSet } = await import('./sorted');
    // System.Text.Json writes a stack of 1, 2, 3 as [3, 2, 1]: its top first.
    const stack = hydrate([3, 2, 1], { collection: 'stack', of: null }) as InstanceType<typeof Stack<number>>;
    expect(stack).toBeInstanceOf(Stack);
    expect(stack.pop()).toBe(3);
    expect(stack.pop()).toBe(2);
    const queue = hydrate(['1', '2'], { collection: 'queue', of: 'long' }) as InstanceType<typeof Queue<bigint>>;
    expect(queue).toBeInstanceOf(Queue);
    expect(queue.dequeue()).toBe(1n);
    expect(hydrate(['a', 'b'], { collection: 'linkedList', of: null })).toBeInstanceOf(LinkedList);
    const sorted = hydrate([3, 1, 2], { collection: 'sortedSet', of: null }) as InstanceType<typeof SortedSet<number>>;
    expect(sorted).toBeInstanceOf(SortedSet);
    expect(sorted.min).toBe(1);
  });

  it('keeps a sorted one in its element type\'s order, which `<` does not give', async () => {
    const { SortedSet } = await import('./sorted');
    // Decimals compared by their text put 10 before 9 and kept 1.0 beside 1.00, which .NET finds equal.
    const prices = hydrate(['10', '9', '1.0', '1.00'], { collection: 'sortedSet', of: 'decimal', order: 'comparable' });
    expect([...(prices as InstanceType<typeof SortedSet<Decimal>>)].map(String)).toEqual(['1.0', '9', '10']);
    // A string orders in the culture, as Comparer<string>.Default does: B after b, not before a.
    const names = hydrate(['a', 'b', 'B'], { collection: 'sortedSet', of: null, order: 'text' });
    expect([...(names as InstanceType<typeof SortedSet<string>>)]).toEqual(['a', 'b', 'B']);
    const index = hydrate({ a: 3, b: 1, B: 2 }, { dict: null, sorted: 'dictionary', order: 'text' }) as SortedMap<string, number>;
    expect([...index.keys()]).toEqual(['a', 'b', 'B']);
  });

  it('passes a value that is already its class, so hydrating twice is harmless', () => {
    const once = hydrate(['a'], { collection: 'set', of: null });
    expect(hydrate(once, { collection: 'set', of: null })).toBe(once);
    expect(hydrate(null, { collection: 'set', of: null })).toBeNull();
  });
});
