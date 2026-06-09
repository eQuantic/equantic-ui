import { describe, it, expect } from 'vitest';
import { DateTimeOffset, dateTimeOffset, dateTime, timeSpan } from './datetime';

const minus3 = timeSpan.fromHours(-3);

describe('DateTimeOffset — .NET semantics', () => {
  it('formats with the invariant MM/dd/yyyy HH:mm:ss zzz', () => {
    expect(dateTimeOffset(2024, 1, 15, 13, 30, 0, minus3).toString()).toBe('01/15/2024 13:30:00 -03:00');
  });

  it('exposes local components and the offset', () => {
    const d = dateTimeOffset(2024, 1, 15, 13, 30, 0, minus3);
    expect([d.year, d.month, d.day, d.hour]).toEqual([2024, 1, 15, 13]);
    expect(d.offset.toString()).toBe('-03:00:00');
  });

  it('UtcDateTime converts local to UTC (local − offset)', () => {
    expect(dateTimeOffset(2024, 1, 15, 13, 30, 0, minus3).utcDateTime.toString()).toBe('01/15/2024 16:30:00');
  });

  it('ToOffset re-expresses the same instant at another offset', () => {
    expect(dateTimeOffset(2024, 1, 15, 13, 30, 0, minus3).toOffset(timeSpan.zero).toString())
      .toBe('01/15/2024 16:30:00 +00:00');
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
    expect(dateTimeOffset.fromUnixTimeSeconds(0).utcDateTime.toString()).toBe('01/01/1970 00:00:00');
    expect(dateTimeOffset(1970, 1, 1, 0, 0, 0, timeSpan.zero).toUnixTimeSeconds()).toBe(0);
  });

  it('builds from a DateTime + offset and serializes ISO with offset', () => {
    const d = dateTimeOffset(dateTime(2024, 1, 15, 13, 30, 0), minus3);
    expect(d).toBeInstanceOf(DateTimeOffset);
    expect(JSON.stringify({ d })).toBe('{"d":"2024-01-15T13:30:00-03:00"}');
    expect(dateTimeOffset.parse('2024-01-15T13:30:00-03:00').utcDateTime.toString()).toBe('01/15/2024 16:30:00');
  });
});
