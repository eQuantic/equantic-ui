import { describe, expect, it, afterEach } from 'vitest';
import { CalendarNames } from './calendar-names';
import { SdkStrings } from './components/SdkStrings';
import { installCulture } from '../utils/culture';
import { calendarFormat, type CalendarSnapshot } from './__fixtures__/calendar-format';
import pinned from './calendar-names.fixture.json';

const fixture = pinned as Record<string, CalendarSnapshot>;

/**
 * What a calendar SAYS, against what the C# side says (C# cross-pin: CalendarNamesFixtureTests).
 *
 * The split below IS the finding that shaped the design. A server-rendered page gets the names
 * SHIPPED, in the format culture's data a date's names come from too, and those are asserted
 * exactly, per culture. The Intl fallback is asserted only for its
 * SHAPE, because it is the host engine's ICU and that is not one thing: between bun and node alone
 * the probe found four disagreements — ar-EG's abbreviations differ by the definite article,
 * ru-RU's differ in case, en-GB abbreviates September differently, and zh-CN does not agree on
 * which day the week starts. A browser is a third answer, so pinning any of it would pin the test
 * runner rather than the contract.
 */
describe('calendar names (C# CalendarNamesFixtureTests cross-pin)', () => {
  afterEach(() => installCulture('en-US', 'en-US', {}));

  for (const [culture, expected] of Object.entries(fixture)) {
    it(`answers the server's catalog exactly for ${culture}`, () => {
      // The SSR path: whatever the server said IS the answer, ICU version notwithstanding.
      installCulture(culture, culture, {}, calendarFormat(expected));
      expect(CalendarNames.firstDayOfWeek).toBe(expected.firstDayOfWeek);
      expect(CalendarNames.dayNamesShort).toEqual(expected.dayNamesShort);
      expect(CalendarNames.dayNamesLong).toEqual(expected.dayNamesLong);
      expect(CalendarNames.monthNames).toEqual(expected.monthNames);
      expect(CalendarNames.monthNamesShort).toEqual(expected.monthNamesShort);
    });
  }

  for (const [culture, expected] of Object.entries(fixture)) {
    it(`builds the same typed-field hint as C# for ${culture}`, () => {
      // The hint is DERIVED on both sides — the culture's pattern arranged, the language's three
      // letters standing in — so it is the derivation that has to agree, not a shipped string.
      installCulture(
        culture,
        culture,
        { 'SdkResources/DateFormatLetters': expected.dateFormatLetters },
        calendarFormat(expected),
      );
      expect(CalendarNames.shortDatePattern).toBe(expected.shortDatePattern);
      expect(SdkStrings.dateFormatHint).toBe(expected.dateFormatHint);
    });
  }

  for (const culture of Object.keys(fixture)) {
    it(`falls back to a WELL-FORMED answer for ${culture}`, () => {
      // Deliberately not pinned against .NET: the fallback is whatever ICU the HOST carries, and
      // that is not one thing. Between bun and node alone, ar-EG's abbreviations differ by the
      // definite article, ru-RU's differ in case, en-GB's September abbreviation differs, and
      // zh-CN does not even agree on which day the week starts. A browser is a third answer.
      // Pinning any of it would pin the test runner's ICU, so the contract is the SHAPE.
      installCulture(culture, culture, {});
      expect(CalendarNames.firstDayOfWeek).toBeGreaterThanOrEqual(0);
      expect(CalendarNames.firstDayOfWeek).toBeLessThanOrEqual(6);
      expect(CalendarNames.dayNamesShort).toHaveLength(7);
      expect(CalendarNames.dayNamesLong).toHaveLength(7);
      expect(CalendarNames.monthNames).toHaveLength(12);
      expect(CalendarNames.monthNamesShort).toHaveLength(12);
      expect(
        [...CalendarNames.dayNamesShort, ...CalendarNames.monthNames].every((n) => n.length > 0),
      ).toBe(true);
    });
  }

  it('the shipped data OVERRIDES the host ICU, which is the whole point', () => {
    // ar-EG under this runner's ICU says "الأحد" where .NET says "أحد" — both correct Arabic, one
    // with the definite article. The server's answer has to win, or the SSR HTML and the hydrated
    // tree carry different day names.
    installCulture('ar-EG', 'ar-EG', {}, calendarFormat(fixture['ar-EG']));
    expect(CalendarNames.dayNamesShort).toEqual(fixture['ar-EG'].dayNamesShort);
    expect(CalendarNames.firstDayOfWeek).toBe(6);
  });

  it('with nothing installed, a calendar is the invariant culture’s, as a date is', () => {
    // A page with no culture installed is in the invariant culture (#471), and a calendar says what
    // a date's names say: the host's locale named the days of a calendar while the formatter wrote
    // the invariant culture's names beside it.
    installCulture('', '', {});
    expect(CalendarNames.dayNamesShort[0]).toBe('Sun');
    expect(CalendarNames.monthNames[0]).toBe('January');
    expect(CalendarNames.firstDayOfWeek).toBe(0);
    expect(CalendarNames.shortDatePattern).toBe('MM/dd/yyyy');
  });

  it('answers even where Intl.Locale does not exist', () => {
    // A minimal Intl build (or an older browser) may carry no Locale constructor at all, and week
    // data is newer still. Constructing it blind would throw in the one branch whose job is to
    // answer without a server.
    const real = Intl.Locale;
    try {
      (Intl as { Locale?: unknown }).Locale = undefined;
      installCulture('fr-FR', 'fr-FR', {});
      expect(CalendarNames.firstDayOfWeek).toBe(1);
      expect(CalendarNames.dayNamesShort).toHaveLength(7);
    } finally {
      (Intl as { Locale?: unknown }).Locale = real;
    }
  });

  it('a culture switch without its data does not keep the OLD culture’s names', () => {
    installCulture('fr-FR', 'fr-FR', {}, calendarFormat(fixture['fr-FR']));
    expect(CalendarNames.monthNames[0]).toBe('janvier');
    // A switch with no server to ask carries no data; falling back to Intl is right, keeping the
    // French names would be the one answer that is certainly wrong.
    installCulture('de-DE', 'de-DE', {});
    expect(CalendarNames.monthNames[0]).toBe('Januar');
  });

  it('answers Sunday-first regardless of where the week starts', () => {
    // The arrays are indexed by System.DayOfWeek, always; the calendar rotates them. A locale
    // whose week starts on Monday must NOT come back rotated, or the rotation happens twice.
    installCulture('fr-FR', 'fr-FR', {});
    expect(CalendarNames.firstDayOfWeek).toBe(1);
    expect(CalendarNames.dayNamesShort[0]).toBe('dim.');
    expect(CalendarNames.dayNamesLong[1]).toBe('lundi');
  });

  it('gives twelve months, never .NET’s empty thirteenth', () => {
    for (const culture of Object.keys(fixture)) {
      installCulture(culture, culture, {});
      expect(CalendarNames.monthNames).toHaveLength(12);
      expect(CalendarNames.monthNames).not.toContain('');
    }
  });
});
