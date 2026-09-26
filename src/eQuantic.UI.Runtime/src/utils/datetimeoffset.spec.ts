import { describe, it, expect } from 'vitest';
import { DateTimeOffset, dateTimeOffset, dateTime, timeSpan } from './datetime';

const minus3 = timeSpan.fromHours(-3);

describe('DateTimeOffset — .NET semantics', () => {
  it('formats with the invariant MM/dd/yyyy HH:mm:ss zzz', () => {
    expect(dateTimeOffset(2024, 1, 15, 13, 30, 0, minus3).toString()).toBe(
      '01/15/2024 13:30:00 -03:00',
    );
  });

  it('exposes local components and the offset', () => {
    const d = dateTimeOffset(2024, 1, 15, 13, 30, 0, minus3);
    expect([d.year, d.month, d.day, d.hour]).toEqual([2024, 1, 15, 13]);
    expect(d.offset.toString()).toBe('-03:00:00');
  });

  it('UtcDateTime converts local to UTC (local − offset)', () => {
    expect(dateTimeOffset(2024, 1, 15, 13, 30, 0, minus3).utcDateTime.toString()).toBe(
      '01/15/2024 16:30:00',
    );
  });

  it('ToOffset re-expresses the same instant at another offset', () => {
    expect(dateTimeOffset(2024, 1, 15, 13, 30, 0, minus3).toOffset(timeSpan.zero).toString()).toBe(
      '01/15/2024 16:30:00 +00:00',
    );
  });

  it('Add* keeps the offset', () => {
    expect(dateTimeOffset(2024, 1, 15, 13, 30, 0, minus3).addHours(2).hour).toBe(15);
  });

  it('compares/equates by instant (not wall-clock)', () => {
    const a = dateTimeOffset(2024, 1, 15, 12, 0, 0, timeSpan.zero);
    const b = dateTimeOffset(2024, 1, 15, 13, 0, 0, timeSpan.fromHours(1)); // same instant
    expect(a.equals(b)).toBe(true);
    expect(a.compareTo(b)).toBe(0);
  });

  it('Unix time round-trips', () => {
    expect(dateTimeOffset.fromUnixTimeSeconds(0).utcDateTime.toString()).toBe(
      '01/01/1970 00:00:00',
    );
    expect(dateTimeOffset(1970, 1, 1, 0, 0, 0, timeSpan.zero).toUnixTimeSeconds()).toBe(0);
  });

  it('builds from a DateTime + offset and serializes ISO with offset', () => {
    const d = dateTimeOffset(dateTime(2024, 1, 15, 13, 30, 0), minus3);
    expect(d).toBeInstanceOf(DateTimeOffset);
    expect(JSON.stringify({ d })).toBe('{"d":"2024-01-15T13:30:00-03:00"}');
    expect(dateTimeOffset.parse('2024-01-15T13:30:00-03:00').utcDateTime.toString()).toBe(
      '01/15/2024 16:30:00',
    );
  });

  it('MinValue is default(DateTimeOffset) and MaxValue the last tick, both at +00:00', () => {
    expect(dateTimeOffset.minValue().toString()).toBe('01/01/0001 00:00:00 +00:00');
    expect(dateTimeOffset.maxValue().toString()).toBe('12/31/9999 23:59:59 +00:00');
    expect(dateTimeOffset.maxValue().ticks - dateTimeOffset.minValue().ticks).toBe(
      3_155_378_975_999_999_999n,
    );
  });
});

describe('DateTimeOffset.Add* — the clock time, then the UTC time', () => {
  const o = dateTimeOffset(2026, 1, 1, 0, 0, 0, timeSpan.fromHours(3));

  it('lands on the tick, milliseconds and microseconds included', () => {
    expect(o.addSeconds(0.00001).ticks - o.ticks).toBe(100n);
    expect(o.addMilliseconds(-0.5).ticks - o.ticks).toBe(-5000n);
    expect(o.addMicroseconds(1.99).ticks - o.ticks).toBe(19n);
  });

  it('refuses the clock time first, then the UTC time', () => {
    expect(() => dateTimeOffset.maxValue().addSeconds(1)).toThrow(
      "The added or subtracted value results in an un-representable DateTime. (Parameter 'value')",
    );
    const late = dateTimeOffset(9999, 12, 31, 19, 59, 59, minus3);
    expect(late.addMinutes(30).ticks).toBe(3_155_378_849_990_000_000n);
    expect(() => late.addMinutes(90)).toThrow(
      "The UTC time represented when the offset is applied must be between year 0 and 10,000. (Parameter 'offset')",
    );
  });
});
