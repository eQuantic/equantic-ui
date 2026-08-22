import { calendarCatalog, formatLocale } from '../utils/culture';

/**
 * Client twin of the C# `eQuantic.UI.Primitives.CalendarNames` — what a calendar has to say in the
 * reader's language.
 *
 * The SERVER's answer wins when there is one. That is not deference for its own sake: the two
 * sides read different ICU builds and they do not always agree. `ar-EG` abbreviates Sunday as
 * "أحد" in .NET and "الأحد" in a JS runtime's ICU — both correct Arabic, one with the definite
 * article — and the probe found two JS engines disagreeing with each other as well. Deriving
 * independently would put one label in the SSR HTML and a different one in the hydrated tree, and
 * the only symptom would be a flicker on a page nobody debugs in Arabic.
 *
 * `Intl` is the FALLBACK, for the render with no server behind it: a client-only mount, or a
 * culture switched in the browser before any request carried the new catalog. There, nothing
 * exists to disagree with.
 *
 * NARROW day names are absent from both sides. .NET's `ShortestDayNames` and CLDR's
 * `weekday: "narrow"` are different data — they differ for seven of the ten cultures probed — and
 * no shared derivation rescues them: taking the first character of the short name gives Chinese
 * seven identical headers, because 周日/周一/周二 all begin with the same glyph.
 */

/** A Sunday, so index 0..6 walks Sunday..Saturday — System.DayOfWeek's own numbering. */
const SUNDAY = Date.UTC(2026, 7, 16);
const DAY = 86_400_000;

function weekdays(style: 'short' | 'long'): string[] {
  const format = new Intl.DateTimeFormat(formatLocale(), { weekday: style, timeZone: 'UTC' });
  return Array.from({ length: 7 }, (_, index) => format.format(new Date(SUNDAY + index * DAY)));
}

function months(style: 'short' | 'long'): string[] {
  const format = new Intl.DateTimeFormat(formatLocale(), { month: style, timeZone: 'UTC' });
  return Array.from({ length: 12 }, (_, index) =>
    format.format(new Date(Date.UTC(2026, index, 15))),
  );
}

export class CalendarNames {
  /**
   * The day the week starts on, in System.DayOfWeek's numbering (0 = Sunday). `Intl.Locale`
   * counts 1..7 from Monday, so Sunday arrives as 7 and converts to 0. Where the runtime does not
   * carry week data (it is the newest part of Intl), the fallback is Monday — the ISO-8601 default
   * and what the majority of locales answer.
   */
  static get firstDayOfWeek(): number {
    const shipped = calendarCatalog();
    if (shipped) return shipped.firstDayOfWeek;
    const locale = new Intl.Locale(formatLocale() ?? 'en-US') as Intl.Locale & {
      weekInfo?: { firstDay: number };
      getWeekInfo?: () => { firstDay: number };
    };
    const info = locale.getWeekInfo?.() ?? locale.weekInfo;
    if (!info) return 1;
    return info.firstDay === 7 ? 0 : info.firstDay;
  }

  /** The seven day names abbreviated, ALWAYS Sunday-first — the calendar rotates them itself. */
  static get dayNamesShort(): string[] {
    return calendarCatalog()?.dayNamesShort ?? weekdays('short');
  }

  /** The seven day names in full, Sunday-first — what a cell announces to a screen reader. */
  static get dayNamesLong(): string[] {
    return calendarCatalog()?.dayNamesLong ?? weekdays('long');
  }

  /** The twelve month names in full, January-first. */
  static get monthNames(): string[] {
    return calendarCatalog()?.monthNames ?? months('long');
  }

  /** The twelve month names abbreviated, January-first. */
  static get monthNamesShort(): string[] {
    return calendarCatalog()?.monthNamesShort ?? months('short');
  }
}
