import { describe, it, expect } from 'vitest';
import { dateOnly, dateTime, dateTimeOffset, timeOnly, timeSpan } from './datetime';

// `TryParse(string, out T)` of the five time types: the value Parse reads, or undefined where Parse
// refuses the text or the text is null, and never a throw. The compiler writes the value or the
// type's default through the out argument (#718); DateTimeTryParseConformanceTests runs the same
// texts on both sides.
describe('tryParse of the time types', () => {
  it('answers the value Parse reads', () => {
    expect(dateTime.tryParse('2024-01-15')?.ticks).toBe(dateTime.parse('2024-01-15').ticks);
    expect(dateTimeOffset.tryParse('2024-01-15T09:30:00+02:00')?.offset.ticks).toBe(
      72_000_000_000n,
    );
    expect(dateOnly.tryParse('2024-01-15')?.dayNumber).toBe(738_899);
    expect(timeOnly.tryParse('09:30')?.ticks).toBe(342_000_000_000n);
    expect(timeSpan.tryParse('1.02:03:04.5')?.ticks).toBe(937_845_000_000n);
  });

  it('answers undefined for a text Parse refuses, and for a null one', () => {
    for (const reader of [dateTime, dateTimeOffset, dateOnly, timeOnly, timeSpan]) {
      expect(reader.tryParse('abc')).toBeUndefined();
      expect(reader.tryParse(null)).toBeUndefined();
      expect(reader.tryParse(undefined)).toBeUndefined();
    }
  });

  it('reads a time of day that exists, and the whole text', () => {
    expect(timeOnly.tryParse('25:00')).toBeUndefined();
    expect(timeOnly.tryParse('09:60')).toBeUndefined();
    expect(timeOnly.tryParse('09:30 junk')).toBeUndefined();
    expect(() => timeOnly.parse('25:00')).toThrow(
      "String '25:00' was not recognized as a valid TimeOnly.",
    );
  });

  // Measured on .NET 10 in the invariant culture.
  it('reads an AM or PM designator as .NET does', () => {
    expect(timeOnly.parse('9:30 AM').ticks).toBe(342_000_000_000n);
    expect(timeOnly.parse('9:30 pm').ticks).toBe(774_000_000_000n);
    expect(timeOnly.parse('12:15 AM').ticks).toBe(9_000_000_000n);
    expect(timeOnly.parse('12:15 PM').ticks).toBe(441_000_000_000n);
    expect(timeOnly.parse('13:00 PM').ticks).toBe(468_000_000_000n);
    expect(timeOnly.parse('9PM').ticks).toBe(756_000_000_000n);
    expect(timeOnly.parse('AM 9:30').ticks).toBe(342_000_000_000n);
    expect(timeOnly.tryParse('13:00 AM')).toBeUndefined();
    expect(timeOnly.tryParse('PM 9:30 PM')).toBeUndefined();
    expect(timeOnly.tryParse('9')).toBeUndefined();
  });

  it('rounds a fraction to the tick as .NET does', () => {
    expect(timeOnly.parse('09:30:15.12345678').ticks).toBe(342_151_234_568n);
    expect(timeOnly.parse('9:30:15.00000005').ticks).toBe(342_150_000_001n);
    expect(timeOnly.parse('23:59:59.99999999').ticks).toBe(0n);
    expect(timeOnly.parse('09:30:15.').ticks).toBe(342_150_000_000n);
  });

  it('keeps a fault of its own reader loud', () => {
    const broken = { toString: () => '09:30' } as unknown as string;
    expect(() => timeOnly.tryParse(broken)).toThrow(TypeError);
  });
});
