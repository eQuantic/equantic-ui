import { describe, expect, it, afterEach } from 'vitest';
import { CalendarNames } from './calendar-names';
import { installCulture } from '../utils/culture';
import pinned from './calendar-names.fixture.json';

type Snapshot = {
  firstDayOfWeek: number;
  dayNamesShort: string[];
  dayNamesLong: string[];
  monthNames: string[];
  monthNamesShort: string[];
};

const fixture = pinned as Record<string, Snapshot>;

/**
 * What a calendar SAYS, against what the C# side says (C# cross-pin: CalendarNamesFixtureTests).
 *
 * The split below IS the finding that shaped the design. A server-rendered page gets the names
 * SHIPPED, and those are asserted exactly, per culture. The Intl fallback is asserted only for its
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
      installCulture(culture, culture, {}, expected);
      expect(CalendarNames.firstDayOfWeek).toBe(expected.firstDayOfWeek);
      expect(CalendarNames.dayNamesShort).toEqual(expected.dayNamesShort);
      expect(CalendarNames.dayNamesLong).toEqual(expected.dayNamesLong);
      expect(CalendarNames.monthNames).toEqual(expected.monthNames);
      expect(CalendarNames.monthNamesShort).toEqual(expected.monthNamesShort);
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

  it('the shipped catalog OVERRIDES the host ICU, which is the whole point', () => {
    // ar-EG under this runner's ICU says "الأحد" where .NET says "أحد" — both correct Arabic, one
    // with the definite article. The server's answer has to win, or the SSR HTML and the hydrated
    // tree carry different day names.
    installCulture('ar-EG', 'ar-EG', {}, fixture['ar-EG']);
    expect(CalendarNames.dayNamesShort).toEqual(fixture['ar-EG'].dayNamesShort);
    expect(CalendarNames.firstDayOfWeek).toBe(6);
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

  it('a culture switch without a catalog does not keep the OLD culture’s names', () => {
    installCulture('fr-FR', 'fr-FR', {}, fixture['fr-FR']);
    expect(CalendarNames.monthNames[0]).toBe('janvier');
    // Switching in the browser carries no catalog; falling back to Intl is right, keeping the
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
