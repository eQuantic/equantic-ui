import { describe, it, expect } from 'vitest';
import { dateTime, TimeSpan, timeSpan } from './datetime';

describe('TimeSpan — .NET "c" format and component math', () => {
  it('formats hours:minutes:seconds with no days', () => {
    expect(timeSpan(1, 2, 3).toString()).toBe('01:02:03');
  });

  it('formats days.hh:mm:ss', () => {
    expect(timeSpan(2, 3, 4, 5).toString()).toBe('2.03:04:05');
    expect(timeSpan.fromHours(25).toString()).toBe('1.01:00:00');
    expect(timeSpan.fromDays(5).toString()).toBe('5.00:00:00');
  });

  it('formats fractional seconds as 7 digits', () => {
    expect(timeSpan.fromSeconds(1.5).toString()).toBe('00:00:01.5000000');
  });

  it('formats negative durations with a leading minus', () => {
    expect(timeSpan.fromDays(-1).toString()).toBe('-1.00:00:00');
  });

  it('exposes whole components and fractional totals', () => {
    expect(timeSpan.fromMinutes(90).totalHours).toBe(1.5);
    const t = timeSpan(1, 2, 3, 4); // 1d 2h 3m 4s
    expect([t.days, t.hours, t.minutes, t.seconds]).toEqual([1, 2, 3, 4]);
  });

  it('adds and compares', () => {
    expect(timeSpan.fromHours(1).add(timeSpan.fromMinutes(30)).toString()).toBe('01:30:00');
    expect(timeSpan.fromHours(1).compareTo(timeSpan.fromMinutes(30))).toBe(1);
  });

  it('round-trips through JSON as the "c" string', () => {
    expect(JSON.stringify({ t: timeSpan.fromHours(25) })).toBe('{"t":"1.01:00:00"}');
  });
});

describe('DateTime — .NET semantics', () => {
  it('formats with the invariant default MM/dd/yyyy HH:mm:ss', () => {
    expect(dateTime(2024, 1, 15).toString()).toBe('01/15/2024 00:00:00');
    expect(dateTime(2024, 1, 5, 9, 3, 7).toString()).toBe('01/05/2024 09:03:07');
  });

  it('exposes calendar components', () => {
    const d = dateTime(2024, 2, 29, 13, 45, 30);
    expect([d.year, d.month, d.day, d.hour, d.minute, d.second]).toEqual([2024, 2, 29, 13, 45, 30]);
  });

  it('computes DayOfWeek (Sunday = 0)', () => {
    expect(dateTime(2024, 1, 7).dayOfWeek).toBe(0); // Sunday
    expect(dateTime(2024, 1, 15).dayOfWeek).toBe(1); // Monday
  });

  it('computes DayOfYear across the leap-day boundary', () => {
    expect(dateTime(2024, 3, 1).dayOfYear).toBe(61); // 31 + 29 + 1
  });

  it('adds days crossing a month boundary', () => {
    expect(dateTime(2024, 1, 15).addDays(20).toString()).toBe('02/04/2024 00:00:00');
  });

  it('clamps the day when adding months (Jan 31 + 1 month)', () => {
    expect(dateTime(2024, 1, 31).addMonths(1).toString()).toBe('02/29/2024 00:00:00'); // leap
    expect(dateTime(2023, 1, 31).addMonths(1).toString()).toBe('02/28/2023 00:00:00'); // non-leap
  });

  it('subtracts two DateTimes into a TimeSpan', () => {
    expect(dateTime(2024, 1, 20).diff(dateTime(2024, 1, 15))).toBeInstanceOf(TimeSpan);
    expect(
      dateTime(2024, 1, 20)
        .diff(dateTime(2024, 1, 15))
        .toString(),
    ).toBe('5.00:00:00');
  });

  it('adds and subtracts a TimeSpan', () => {
    expect(dateTime(2024, 1, 15).add(timeSpan.fromDays(5)).toString()).toBe('01/20/2024 00:00:00');
    expect(dateTime(2024, 1, 15).subtract(timeSpan.fromHours(48)).toString()).toBe(
      '01/13/2024 00:00:00',
    );
  });

  it('compares', () => {
    expect(dateTime(2024, 1, 16).compareTo(dateTime(2024, 1, 15))).toBe(1);
    expect(dateTime(2024, 1, 15).equals(dateTime(2024, 1, 15))).toBe(true);
  });

  it('supports static helpers', () => {
    expect(dateTime.daysInMonth(2024, 2)).toBe(29);
    expect(dateTime.isLeapYear(2023)).toBe(false);
    expect(dateTime.minValue().year).toBe(1);
    expect(dateTime.maxValue().year).toBe(9999);
  });

  it('formats with custom patterns', () => {
    expect(dateTime(2024, 1, 5).format('yyyy-MM-dd')).toBe('2024-01-05');
    expect(dateTime(2024, 1, 5, 9, 8, 7).format('yyyy/M/d HH:mm:ss')).toBe('2024/1/5 09:08:07');
  });

  it('serializes to ISO-8601 and parses it back', () => {
    expect(JSON.stringify({ d: dateTime(2024, 1, 15) })).toBe('{"d":"2024-01-15T00:00:00"}');
    expect(dateTime.parse('2024-01-15T09:30:00').toString()).toBe('01/15/2024 09:30:00');
    expect(dateTime.parse('01/15/2024 09:30:00').toJSON()).toBe('2024-01-15T09:30:00');
  });
});
