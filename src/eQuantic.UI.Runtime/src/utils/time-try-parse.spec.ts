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
    expect(() => timeOnly.parse('25:00')).toThrow("Unrecognized TimeOnly format: '25:00'");
  });
});
