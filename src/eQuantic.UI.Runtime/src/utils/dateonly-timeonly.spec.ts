import { describe, it, expect } from 'vitest';
import { DateOnly, dateOnly, TimeOnly, timeOnly, dateTime } from './datetime';

describe('DateOnly — .NET semantics', () => {
  it('formats with the invariant short date MM/dd/yyyy', () => {
    expect(dateOnly(2024, 1, 15).toString()).toBe('01/15/2024');
  });

  it('exposes components and DayNumber', () => {
    const d = dateOnly(2024, 2, 29);
    expect([d.year, d.month, d.day]).toEqual([2024, 2, 29]);
    expect(dateOnly(1, 1, 1).dayNumber).toBe(0);
  });

  it('computes DayOfWeek (Sunday = 0) and DayOfYear', () => {
    expect(dateOnly(2024, 1, 7).dayOfWeek).toBe(0); // Sunday
    expect(dateOnly(2024, 1, 15).dayOfWeek).toBe(1); // Monday
    expect(dateOnly(2024, 3, 1).dayOfYear).toBe(61); // leap: 31 + 29 + 1
  });

  it('adds days/months/years with day clamping', () => {
    expect(dateOnly(2024, 1, 15).addDays(20).toString()).toBe('02/04/2024');
    expect(dateOnly(2024, 1, 31).addMonths(1).toString()).toBe('02/29/2024'); // clamp, leap
    expect(dateOnly(2023, 1, 31).addMonths(1).toString()).toBe('02/28/2023');
    expect(dateOnly(2024, 6, 10).addYears(1).toString()).toBe('06/10/2025');
  });

  it('compares', () => {
    expect(dateOnly(2024, 1, 16).compareTo(dateOnly(2024, 1, 15))).toBe(1);
    expect(dateOnly(2024, 1, 15).equals(dateOnly(2024, 1, 15))).toBe(true);
  });

  it('serializes to ISO yyyy-MM-dd and parses back', () => {
    expect(JSON.stringify({ d: dateOnly(2024, 1, 5) })).toBe('{"d":"2024-01-05"}');
    expect(dateOnly.parse('2024-01-05').toString()).toBe('01/05/2024');
    expect(dateOnly.parse('01/05/2024').toJSON()).toBe('2024-01-05');
  });

  it('fromDateTime drops the time', () => {
    expect(dateOnly.fromDateTime(dateTime(2024, 1, 15, 13, 30, 0)).toString()).toBe('01/15/2024');
    expect(dateOnly.fromDateTime(dateTime(2024, 1, 15, 13, 30, 0))).toBeInstanceOf(DateOnly);
  });
});

describe('TimeOnly — .NET semantics', () => {
  it('formats with the invariant short time HH:mm', () => {
    expect(timeOnly(13, 45).toString()).toBe('13:45');
    expect(timeOnly(13, 45, 30).toString()).toBe('13:45'); // short time omits seconds
    expect(timeOnly(9, 5).toString()).toBe('09:05');
  });

  it('exposes components', () => {
    const t = timeOnly(13, 45, 30, 500);
    expect([t.hour, t.minute, t.second, t.millisecond]).toEqual([13, 45, 30, 500]);
  });

  it('adds hours/minutes wrapping within a day', () => {
    expect(timeOnly(23, 0).addHours(2).toString()).toBe('01:00'); // wraps past midnight
    expect(timeOnly(0, 30).addMinutes(-45).toString()).toBe('23:45'); // wraps backwards
  });

  it('compares', () => {
    expect(timeOnly(14, 0).compareTo(timeOnly(13, 0))).toBe(1);
    expect(timeOnly(13, 45).equals(timeOnly(13, 45))).toBe(true);
  });

  it('serializes to ISO HH:mm:ss and parses back', () => {
    expect(JSON.stringify({ t: timeOnly(13, 45, 30) })).toBe('{"t":"13:45:30"}');
    expect(timeOnly.parse('13:45:30').toString()).toBe('13:45');
    expect(timeOnly.parse('09:05:00')).toBeInstanceOf(TimeOnly);
  });
});
