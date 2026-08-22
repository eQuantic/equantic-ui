import { describe, it, expect } from 'vitest';
import { Decimal, dec } from './decimal';

describe('Decimal — exact base-10 arithmetic', () => {
  it('adds exactly (no binary float error)', () => {
    expect(dec('0.1').add(dec('0.2')).toString()).toBe('0.3'); // not 0.30000000000000004
    expect(dec('1.1').add(dec('2.2')).toString()).toBe('3.3');
  });

  it('subtracts exactly', () => {
    expect(dec('0.3').sub(dec('0.1')).toString()).toBe('0.2');
    expect(dec('5').sub(dec('2.5')).toString()).toBe('2.5');
  });

  it('multiplies and preserves scale like .NET', () => {
    expect(dec('1.5').mul(dec('2')).toString()).toBe('3.0'); // .NET keeps the trailing zero
    expect(dec('0.1').mul(dec('0.1')).toString()).toBe('0.01');
  });

  it('divides terminating quotients', () => {
    expect(dec('10').div(dec('4')).toString()).toBe('2.5');
    expect(dec('1').div(dec('4')).toString()).toBe('0.25');
    expect(dec('6').div(dec('2')).toString()).toBe('3');
  });

  it('compares and tests equality by value', () => {
    expect(dec('0.1').add(dec('0.2')).equals(dec('0.3'))).toBe(true);
    expect(dec('1.10').equals(dec('1.1'))).toBe(true);
    expect(dec('2.5').compareTo(dec('2.50'))).toBe(0);
    expect(dec('2.5').compareTo(dec('2.6'))).toBe(-1);
    expect(dec('3').compareTo(dec('2.6'))).toBe(1);
  });

  it('handles negatives', () => {
    expect(dec('-1.1').add(dec('2.2')).toString()).toBe('1.1');
    expect(dec('-0.3').toString()).toBe('-0.3');
  });

  it('serializes to a JSON string (exact wire protocol)', () => {
    expect(new Decimal(33n, 1).toJSON()).toBe('3.3');
    expect(JSON.stringify({ v: dec('1.1').add(dec('2.2')) })).toBe('{"v":"3.3"}');
  });

  it('round-trips a high-precision value through JSON without loss', () => {
    const original = dec('0.123456789012345678901234567'); // 27 digits — a double would mangle it
    const json = JSON.stringify({ v: original });
    expect(json).toBe('{"v":"0.123456789012345678901234567"}');
    const parsed = dec((JSON.parse(json) as { v: string }).v);
    expect(parsed.toString()).toBe('0.123456789012345678901234567');
  });
});

describe('Decimal.round — half to even', () => {
  it('rounds midpoints to the even neighbour, like Math.Round(decimal)', () => {
    expect(Decimal.from('1.5').round().toString()).toBe('2');
    expect(Decimal.from('2.5').round().toString()).toBe('2');
    expect(Decimal.from('-2.5').round().toString()).toBe('-2');
    expect(Decimal.from('2.51').round().toString()).toBe('3');
    expect(Decimal.from('2.345').round(2).toString()).toBe('2.34');
    expect(Decimal.from('2.355').round(2).toString()).toBe('2.36');
    expect(Decimal.from('7').round(2).toString()).toBe('7');
  });
});
