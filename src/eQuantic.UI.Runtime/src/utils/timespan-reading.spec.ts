import { describe, it, expect } from 'vitest';
import { readTimeSpan } from './timespan-reading';

/**
 * The reader's shapes, each answer measured with `TimeSpan.Parse(text)` on .NET 10 in a culture whose
 * decimal separator is the one given. The conformance suite (`TimeSpanTextConformanceTests`) runs both
 * sides through the twin; these pin what the reader itself answers, where a failure names the shape.
 */
const TICKS_PER_SECOND = 10_000_000n;
const TICKS_PER_MINUTE = 60n * TICKS_PER_SECOND;
const TICKS_PER_HOUR = 60n * TICKS_PER_MINUTE;
const TICKS_PER_DAY = 24n * TICKS_PER_HOUR;

function span(days: number, hours = 0, minutes = 0, seconds = 0, ticks = 0): bigint {
  return (
    BigInt(days) * TICKS_PER_DAY +
    BigInt(hours) * TICKS_PER_HOUR +
    BigInt(minutes) * TICKS_PER_MINUTE +
    BigInt(seconds) * TICKS_PER_SECOND +
    BigInt(ticks)
  );
}

describe('readTimeSpan', () => {
  it('reads one number as days and two as hours and minutes', () => {
    expect(readTimeSpan('10', '.')).toBe(span(10));
    expect(readTimeSpan('-10', '.')).toBe(-span(10));
    expect(readTimeSpan('1:2', '.')).toBe(span(0, 1, 2));
  });

  it('reads three and four numbers by the runs between them', () => {
    expect(readTimeSpan('1:2:3', '.')).toBe(span(0, 1, 2, 3));
    expect(readTimeSpan('1.2:3', '.')).toBe(span(1, 2, 3));
    expect(readTimeSpan('1:2:.5', '.')).toBe(span(0, 1, 2, 0, 5_000_000));
    expect(readTimeSpan('1:2:3:4', '.')).toBe(span(1, 2, 3, 4));
    expect(readTimeSpan('1.2:3:4', '.')).toBe(span(1, 2, 3, 4));
    expect(readTimeSpan('1:2:3.5', '.')).toBe(span(0, 1, 2, 3, 5_000_000));
  });

  it("reads a fraction with a point always, and with the culture's separator in the culture's pattern", () => {
    expect(readTimeSpan('1:2:3,5', ',')).toBe(span(0, 1, 2, 3, 5_000_000));
    expect(readTimeSpan('1:2:3:4,5', ',')).toBe(span(1, 2, 3, 4, 5_000_000));
    expect(readTimeSpan('1:2:3.5', ',')).toBe(span(0, 1, 2, 3, 5_000_000));
    expect(readTimeSpan('1:2:3,5', '.')).toBe('format');
    expect(readTimeSpan('1.2:3:4,5', ',')).toBe('format');
    expect(readTimeSpan('1:2:3:4.5', ',')).toBe('format');
  });

  it('reads twenty-four as days where no clock reads it, and refuses a part past its bound', () => {
    expect(readTimeSpan('24:00:00', '.')).toBe(span(24));
    expect(readTimeSpan('00:60:00', '.')).toBe('overflow');
    expect(readTimeSpan('24:0', '.')).toBe('overflow');
    expect(readTimeSpan('10675200', '.')).toBe('overflow');
    expect(readTimeSpan('268435456', '.')).toBe('overflow');
  });

  it('reads the range of a TimeSpan to the tick', () => {
    expect(readTimeSpan('10675199.02:48:05.4775807', '.')).toBe(9_223_372_036_854_775_807n);
    expect(readTimeSpan('10675199.02:48:05.4775808', '.')).toBe('overflow');
    expect(readTimeSpan('-10675199.02:48:05.4775808', '.')).toBe(-9_223_372_036_854_775_808n);
    expect(readTimeSpan('-10675199.02:48:05.4775809', '.')).toBe('overflow');
  });

  it('rounds a fraction past seven digits as .NET does, and fails where .NET fails', () => {
    expect(readTimeSpan('0:0:0.0123456789', '.')).toBe(span(0, 0, 0, 0, 123_457));
    expect(readTimeSpan('0:0:0.00000005', '.')).toBe(span(0, 0, 0, 0, 1));
    expect(readTimeSpan('0:0:0.000000001', '.')).toBe(0n);
    expect(readTimeSpan('0:0:0.123456789', '.')).toBe('overflow');
    expect(readTimeSpan('0:0:0.000000123456789', '.')).toBe('index');
  });

  it('trims white space as .NET does and reads a sign only before the first number', () => {
    expect(readTimeSpan(' 1:2 ', '.')).toBe(span(0, 1, 2));
    expect(readTimeSpan('1:2​', '.')).toBe('format');
    expect(readTimeSpan('1 :2', '.')).toBe('format');
    expect(readTimeSpan('- 1:2', '.')).toBe('format');
    expect(readTimeSpan('+1:2', '.')).toBe('format');
    expect(readTimeSpan('-0', '.')).toBe(0n);
    expect(readTimeSpan('   ', '.')).toBe('format');
  });
});
