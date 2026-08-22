import { describe, expect, it } from 'vitest';
import { hydrate, type HydrationSpec } from './hydrate';
import { Decimal, dec } from './decimal';
import { DateTime, TimeSpan } from './datetime';

describe('typed hydration', () => {
  it('restores the compat scalars from their wire strings', () => {
    expect(hydrate('9007199254740993', 'long')).toBe(9007199254740993n);
    expect(hydrate(42, 'long')).toBe(42n);
    const m = hydrate('0.1', 'decimal');
    expect(m).toBeInstanceOf(Decimal);
    expect((m as Decimal).toString()).toBe('0.1');
    expect(hydrate('2026-08-22T10:30:00', 'dateTime')).toBeInstanceOf(DateTime);
    expect(hydrate('01:02:03', 'timeSpan')).toBeInstanceOf(TimeSpan);
  });

  it('is idempotent — a value that already has its runtime type passes through', () => {
    expect(hydrate(5n, 'long')).toBe(5n);
    const m = dec('1.5');
    expect(hydrate(m, 'decimal')).toBe(m);
    const parsed = hydrate('2026-08-22T10:30:00', 'dateTime');
    expect(hydrate(parsed, 'dateTime')).toBe(parsed);
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

  it("hydrates a dictionary's values and leaves its keys", () => {
    const result = hydrate({ a: '1', b: '2' }, { dict: 'long' }) as Record<string, bigint>;
    expect(result.a).toBe(1n);
    expect(result.b).toBe(2n);
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
