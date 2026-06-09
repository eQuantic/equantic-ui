import { describe, it, expect } from 'vitest';
import { long } from './long';

describe('long (Int64 via BigInt)', () => {
  it('passes through BigInt', () => {
    expect(long(5n)).toBe(5n);
  });

  it('coerces numbers and strings', () => {
    expect(long(5)).toBe(5n);
    expect(long('42')).toBe(42n);
    expect(long(3.9)).toBe(3n); // truncates toward zero like an int->long conversion
  });

  it('preserves precision beyond 2^53 from a string', () => {
    expect(long('9007199254740993').toString()).toBe('9007199254740993'); // would be ...992 as a double
  });

  it('supports native BigInt arithmetic (incl. truncating division)', () => {
    expect((long(7n) / long(2n)).toString()).toBe('3'); // long division truncates
    expect((long(9007199254740993n) + long(1n)).toString()).toBe('9007199254740994');
  });

  it('is JSON-serializable as a string (toJSON installed)', () => {
    expect(JSON.stringify({ id: 9007199254740993n })).toBe('{"id":"9007199254740993"}');
  });
});
