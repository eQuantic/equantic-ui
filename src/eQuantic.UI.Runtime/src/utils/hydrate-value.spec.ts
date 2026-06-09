import { describe, it, expect } from 'vitest';
import { hydrateValue } from './hydrate-value';
import { Decimal, dec } from './decimal';

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

  it('leaves a plain string field untouched', () => {
    expect(hydrateValue('', '19.99')).toBe('19.99');
  });

  it('leaves a plain number field untouched', () => {
    expect(hydrateValue(0, 42)).toBe(42);
  });

  it('passes null/undefined through', () => {
    expect(hydrateValue(dec('0'), null)).toBeNull();
    expect(hydrateValue(0n, undefined)).toBeUndefined();
  });
});
