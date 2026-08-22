import { describe, it, expect } from 'vitest';
import { hydrateValue } from './hydrate-value';
import { Decimal, dec } from './decimal';
import { DateTime, dateTime, TimeSpan, timeSpan } from './datetime';

describe('hydrateValue', () => {
  it('restores a decimal field from its wire string, exactly', () => {
    const result = hydrateValue(dec('0'), '0.123456789012345678901234567');
    expect(result).toBeInstanceOf(Decimal);
    // The exact 27-digit value survives — a JS number would have lost it.
    expect((result as Decimal).toString()).toBe('0.123456789012345678901234567');
  });

  it('restores a decimal field from a bare number too (lenient)', () => {
    const result = hydrateValue(dec('0'), 19.99);
    expect(result).toBeInstanceOf(Decimal);
    expect((result as Decimal).toString()).toBe('19.99');
  });

  it('restores a long field (bigint) from its wire string', () => {
    const result = hydrateValue(0n, '9223372036854775807');
    expect(typeof result).toBe('bigint');
    expect(result).toBe(9223372036854775807n);
  });

  it('restores a DateTime field from its ISO-8601 wire string', () => {
    const result = hydrateValue(dateTime(1, 1, 1), '2024-01-15T09:30:00');
    expect(result).toBeInstanceOf(DateTime);
    expect((result as DateTime).toString()).toBe('01/15/2024 09:30:00');
  });

  it('restores a TimeSpan field from its "c" wire string', () => {
    const result = hydrateValue(timeSpan.zero, '1.01:00:00');
    expect(result).toBeInstanceOf(TimeSpan);
    expect((result as TimeSpan).toString()).toBe('1.01:00:00');
  });

  it('leaves a plain string field untouched', () => {
    expect(hydrateValue('', '19.99')).toBe('19.99');
    expect(hydrateValue('', '2024-01-15T00:00:00')).toBe('2024-01-15T00:00:00');
  });

  it('leaves a plain number field untouched', () => {
    expect(hydrateValue(0, 42)).toBe(42);
  });

  it('passes null/undefined through', () => {
    expect(hydrateValue(dec('0'), null)).toBeNull();
    expect(hydrateValue(0n, undefined)).toBeUndefined();
  });

  // --- Record/struct re-hydration (Tier 3): SSR sends a plain object; restore the class instance. ---

  class Point {
    constructor(
      public x: number,
      public y: number,
    ) {}
    sum() {
      return this.x + this.y;
    }
  }

  it('rebuilds a record instance from a plain SSR object (methods + instanceof restored)', () => {
    const result = hydrateValue(new Point(0, 0), { x: 3, y: 4 }) as Point;
    expect(result).toBeInstanceOf(Point);
    expect(result.x).toBe(3);
    expect(result.y).toBe(4);
    expect(result.sum()).toBe(7); // instance method works after hydration
  });

  it('rebuilds nested records recursively via the default witness', () => {
    class Line {
      constructor(
        public start: Point,
        public end: Point,
      ) {}
      dx() {
        return this.end.x - this.start.x;
      }
    }
    const witness = new Line(new Point(0, 0), new Point(0, 0));
    const result = hydrateValue(witness, { start: { x: 1, y: 1 }, end: { x: 5, y: 1 } }) as Line;
    expect(result).toBeInstanceOf(Line);
    expect(result.start).toBeInstanceOf(Point);
    expect(result.start.sum()).toBe(2);
    expect(result.dx()).toBe(4);
  });

  it('restores compat-typed members inside a record (e.g. a Decimal field)', () => {
    class Money {
      constructor(public amount: Decimal) {}
    }
    const result = hydrateValue(new Money(dec('0')), { amount: '9.99' }) as Money;
    expect(result).toBeInstanceOf(Money);
    expect(result.amount).toBeInstanceOf(Decimal);
    expect(result.amount.toString()).toBe('9.99');
  });

  it('leaves a plain object (dictionary / anonymous) untouched', () => {
    const incoming = { a: 1, b: 2 };
    const result = hydrateValue({}, incoming);
    expect(Object.getPrototypeOf(result)).toBe(Object.prototype);
    expect(result).toEqual({ a: 1, b: 2 });
  });
});
