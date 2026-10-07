import { afterEach, describe, it, expect } from 'vitest';
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

// Measured on .NET 10; the conformance suite runs the same factories on both sides.
describe("TimeSpan factories — .NET 9's components and .NET 7's tick precision", () => {
  it('counts every component, each of which may be negative', () => {
    expect(timeSpan.fromDays(1, 2, 3n, 4n, 5n, 6n).toString()).toBe('1.02:03:04.0050060');
    expect(timeSpan.fromDays(1, -25).toString()).toBe('-01:00:00');
    expect(timeSpan.fromHours(1, undefined, 5n).toString()).toBe('01:00:05');
    expect(timeSpan.fromMilliseconds(1n, 2n).toString()).toBe('00:00:00.0010020');
    expect(timeSpan.fromMicroseconds(15n).toString()).toBe('00:00:00.0000150');
  });

  it('reads a fractional count to the tick, truncated', () => {
    expect(timeSpan.fromSeconds(0.00001).ticks).toBe(100n);
    expect(timeSpan.fromSeconds(-1.23456789).ticks).toBe(-12345678n);
    expect(timeSpan.fromMilliseconds(0.5).ticks).toBe(5000n);
  });

  it('refuses a span it cannot hold', () => {
    const tooLong = 'TimeSpan overflowed because the duration is too long.';
    expect(timeSpan.fromDays(10675199, 2, 48n, 5n, 477n, 580n).toString()).toBe(
      '10675199.02:48:05.4775800',
    );
    expect(() => timeSpan.fromDays(10675199, 2, 48n, 5n, 477n, 581n)).toThrow(tooLong);
    expect(() => timeSpan.fromSeconds(922337203686n)).toThrow(tooLong);
    expect(() => timeSpan.fromHours(1e20)).toThrow(tooLong);
    expect(() => timeSpan.fromHours(NaN)).toThrow(
      'TimeSpan does not accept floating point Not-a-Number values.',
    );
  });
});

describe('DateTime — .NET semantics', () => {
  it('formats with the invariant default MM/dd/yyyy HH:mm:ss', () => {
    expect(dateTime.of(2024, 1, 15).toString()).toBe('01/15/2024 00:00:00');
    expect(dateTime.of(2024, 1, 5, 9, 3, 7).toString()).toBe('01/05/2024 09:03:07');
  });

  it('exposes calendar components', () => {
    const d = dateTime.of(2024, 2, 29, 13, 45, 30);
    expect([d.year, d.month, d.day, d.hour, d.minute, d.second]).toEqual([2024, 2, 29, 13, 45, 30]);
  });

  it('computes DayOfWeek (Sunday = 0)', () => {
    expect(dateTime.of(2024, 1, 7).dayOfWeek).toBe(0); // Sunday
    expect(dateTime.of(2024, 1, 15).dayOfWeek).toBe(1); // Monday
  });

  it('computes DayOfYear across the leap-day boundary', () => {
    expect(dateTime.of(2024, 3, 1).dayOfYear).toBe(61); // 31 + 29 + 1
  });

  it('adds days crossing a month boundary', () => {
    expect(dateTime.of(2024, 1, 15).addDays(20).toString()).toBe('02/04/2024 00:00:00');
  });

  it('clamps the day when adding months (Jan 31 + 1 month)', () => {
    expect(dateTime.of(2024, 1, 31).addMonths(1).toString()).toBe('02/29/2024 00:00:00'); // leap
    expect(dateTime.of(2023, 1, 31).addMonths(1).toString()).toBe('02/28/2023 00:00:00'); // non-leap
  });

  it('subtracts two DateTimes into a TimeSpan', () => {
    expect(dateTime.of(2024, 1, 20).diff(dateTime.of(2024, 1, 15))).toBeInstanceOf(TimeSpan);
    expect(
      dateTime.of(2024, 1, 20)
        .diff(dateTime.of(2024, 1, 15))
        .toString(),
    ).toBe('5.00:00:00');
  });

  it('adds and subtracts a TimeSpan', () => {
    expect(dateTime.of(2024, 1, 15).add(timeSpan.fromDays(5)).toString()).toBe('01/20/2024 00:00:00');
    expect(dateTime.of(2024, 1, 15).subtract(timeSpan.fromHours(48)).toString()).toBe(
      '01/13/2024 00:00:00',
    );
  });

  it('compares', () => {
    expect(dateTime.of(2024, 1, 16).compareTo(dateTime.of(2024, 1, 15))).toBe(1);
    expect(dateTime.of(2024, 1, 15).equals(dateTime.of(2024, 1, 15))).toBe(true);
  });

  it('supports static helpers', () => {
    expect(dateTime.daysInMonth(2024, 2)).toBe(29);
    expect(dateTime.isLeapYear(2023)).toBe(false);
    expect(dateTime.minValue().year).toBe(1);
    expect(dateTime.maxValue().year).toBe(9999);
  });

  it('formats with custom patterns', () => {
    expect(dateTime.of(2024, 1, 5).format('yyyy-MM-dd')).toBe('2024-01-05');
    expect(dateTime.of(2024, 1, 5, 9, 8, 7).format('yyyy/M/d HH:mm:ss')).toBe('2024/1/5 09:08:07');
  });

  it('serializes to ISO-8601 and parses it back', () => {
    expect(JSON.stringify({ d: dateTime.of(2024, 1, 15) })).toBe('{"d":"2024-01-15T00:00:00"}');
    expect(dateTime.parse('2024-01-15T09:30:00').toString()).toBe('01/15/2024 09:30:00');
    expect(dateTime.parse('01/15/2024 09:30:00').toJSON()).toBe('2024-01-15T09:30:00');
  });
});

// Measured on .NET 10; the conformance suite runs the same calls on both sides (#422).
describe('DateTime.Add* — a fraction lands on the tick', () => {
  const d = dateTime.of(2026, 1, 1);
  const moved = (other: { ticks: bigint }) => other.ticks - d.ticks;

  it('splits whole units from the fraction and truncates the fraction toward zero', () => {
    expect(moved(d.addSeconds(0.00001))).toBe(100n);
    expect(moved(d.addSeconds(-0.00001))).toBe(-100n);
    expect(moved(d.addMilliseconds(0.5))).toBe(5000n);
    expect(moved(d.addDays(1.23456789))).toBe(1_066_666_656_959n);
    expect(moved(d.addMicroseconds(1.99))).toBe(19n);
  });

  it('adds nothing for NaN', () => {
    expect(d.addDays(NaN).ticks).toBe(d.ticks);
  });

  it("refuses a count or a result out of range in .NET's words", () => {
    expect(() => d.addDays(1e10)).toThrow("Value to add was out of range. (Parameter 'value')");
    expect(() => d.addSeconds(Infinity)).toThrow(
      "Value to add was out of range. (Parameter 'value')",
    );
    expect(() => dateTime.maxValue().addSeconds(1)).toThrow(
      "The added or subtracted value results in an un-representable DateTime. (Parameter 'value')",
    );
    expect(() => dateTime.maxValue().addTicks(1n)).toThrow(
      "The added or subtracted value results in an un-representable DateTime. (Parameter 'value')",
    );
    expect(dateTime.minValue().addMilliseconds(-0.00001).ticks).toBe(0n); // truncates to no tick
  });
});

describe('DateTime — the zone a text writes (#606)', () => {
  it('reads the JSON as System.Text.Json reads it: Z is UTC, an offset the instant it names, none no kind', () => {
    const utc = dateTime.fromJson('2026-07-01T12:00:00Z');
    expect(utc.kind).toBe('utc');
    expect(utc.ticks).toBe(dateTime.of(2026, 7, 1, 12, 0, 0).ticks);
    const offset = dateTime.fromJson('2026-07-01T12:00:00-03:00');
    expect(offset.kind).toBe('local');
    expect(offset.toUniversalTime().ticks).toBe(dateTime.of(2026, 7, 1, 15, 0, 0).ticks);
    expect(dateTime.fromJson('2026-07-01T12:00:00').kind).toBe('unspecified');
  });

  it('writes the JSON System.Text.Json writes, the kind as its suffix', () => {
    expect(JSON.stringify(dateTime.of(2026, 1, 2, 3, 4, 5, 6, 7, 'utc'))).toBe('"2026-01-02T03:04:05.006007Z"');
    expect(JSON.stringify(dateTime.of(2026, 1, 2, 3, 4, 5, 6, 7))).toBe('"2026-01-02T03:04:05.006007"');
    const local = dateTime.of(2026, 7, 1, 12, 0, 0, 0, 0, 'local');
    expect(dateTime.fromJson(local.toJSON()).ticks).toBe(local.ticks);
  });

  it('refuses a zone past fourteen hours and text after the zone, as .NET does', () => {
    expect(() => dateTime.parse('2026-07-01T12:00:00+15:00')).toThrow();
    expect(() => dateTime.parse('2026-07-01T12:00:00Zjunk')).toThrow();
    expect(() => dateTime.fromJson('2026-07-01T12:00:00+14:01')).toThrow();
    expect(dateTime.fromJson('2026-07-01T12:00:00+14:00').kind).toBe('local');
  });

  it('moves a written zone to the local time on Parse, as .NET does', () => {
    const parsed = dateTime.parse('2026-07-01T12:00:00Z');
    expect(parsed.kind).toBe('local');
    expect(parsed.toUniversalTime().ticks).toBe(dateTime.of(2026, 7, 1, 12, 0, 0).ticks);
  });
});

// .NET 10's answers with TZ=Europe/Lisbon, where 2026-10-25 01:00 to 01:59 happens twice: a local time
// made from an instant forgot which of the two it was, so its way back to UTC landed an hour off.
describe("DateTime — a repeated hour's daylight occurrence (#606)", () => {
  const zone = process.env.TZ;
  afterEach(() => {
    if (zone === undefined) delete process.env.TZ;
    else process.env.TZ = zone;
  });

  it('goes back to the instant it came from, through arithmetic and Date, until SpecifyKind', () => {
    process.env.TZ = 'Europe/Lisbon';
    const first = dateTime.of(2026, 10, 25, 0, 30, 0, 0, 0, 'utc').toLocalTime();
    expect(first.hour).toBe(1);
    expect(first.toJSON()).toBe('2026-10-25T01:30:00+01:00');
    expect(first.toUniversalTime().toJSON()).toBe('2026-10-25T00:30:00Z');
    expect(first.addMinutes(10).toUniversalTime().toJSON()).toBe('2026-10-25T00:40:00Z');
    expect(first.date.addHours(1.5).toUniversalTime().toJSON()).toBe('2026-10-25T00:30:00Z');
    expect(dateTime.specifyKind(first, 'local').toUniversalTime().toJSON()).toBe('2026-10-25T01:30:00Z');
    expect(dateTime.parse('2026-10-25T00:30:00Z').toUniversalTime().toJSON()).toBe('2026-10-25T00:30:00Z');
    const second = dateTime.of(2026, 10, 25, 1, 30, 0, 0, 0, 'utc').toLocalTime();
    expect(second.toUniversalTime().toJSON()).toBe('2026-10-25T01:30:00Z');
    expect(first.equals(dateTime.specifyKind(first, 'local'))).toBe(true);
  });
});

describe('DateTime — a text read in linear time', () => {
  // Two runs of white space either side of an optional zone made a line of spaces cost the square
  // of its length: 50,000 of them took 1.5 s (CodeQL).
  it('refuses a long run of spaces at once', () => {
    const text = '2026-10-07' + ' '.repeat(100_000) + 'x';
    const started = performance.now();
    expect(() => dateTime.parse(text)).toThrow();
    expect(performance.now() - started).toBeLessThan(200);
  });
});

// .NET 10's refusals: a date or a clock that does not exist normalized into the next day or month.
describe('DateTime — text that names no date', () => {
  const refusal = (act: () => unknown): string => {
    try {
      act();
      return 'no throw';
    } catch (e) {
      return (e as Error).message;
    }
  };

  it('refuses a date or a clock that does not exist, each in .NET\'s words', () => {
    expect(dateTime.parse('2028-02-29').toJSON()).toBe('2028-02-29T00:00:00');
    expect(refusal(() => dateTime.parse('2026-02-29'))).toBe("String '2026-02-29' was not recognized as a valid DateTime.");
    expect(refusal(() => dateTime.parse('2026-01-01T24:00:00'))).toBe(
      "The DateTime represented by the string '2026-01-01T24:00:00' is not supported in calendar 'System.Globalization.GregorianCalendar'.",
    );
    expect(refusal(() => dateTime.parse('2026-01-15T09:30:00 junk'))).toBe(
      "String '2026-01-15T09:30:00 junk' was not recognized as a valid DateTime.",
    );
  });

  it('reads the wire as System.Text.Json does, ISO-8601 and nothing else', () => {
    const json = 'The JSON value could not be converted to System.DateTime.';
    expect(refusal(() => dateTime.fromJson('01/15/2024 09:30:00'))).toBe(json);
    expect(refusal(() => dateTime.fromJson('2026-02-29'))).toBe(json);
    expect(dateTime.fromJson('2028-02-29T10:00:00Z').toJSON()).toBe('2028-02-29T10:00:00Z');
  });
});

