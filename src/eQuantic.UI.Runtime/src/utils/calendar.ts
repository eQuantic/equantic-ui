/**
 * The calendar a culture counts its dates in (#1191), as .NET's `DateTimeFormatInfo.Calendar` counts them:
 * th-TH writes the 15th of January 2024 as `15/1/2567`, fa-IR as `1402/10/25` and ar-SA as the 3rd of
 * Rajab 1445, and reads each of those back as that day. The writer and the reader ask a culture's
 * calendar for the date of a day and the day of a date, and nothing else.
 *
 * The server describes the calendar by measuring .NET's own (`CalendarBridge`), and it is one of two
 * shapes. The Gregorian months under another count of years is that count's distance (`yearOffset`, 543
 * for the Thai Buddhist calendar, 0 for the Gregorian one). A calendar of months of its own (the Persian
 * one, whose leap years .NET computes from the equinox, and Um Al Qura, whose months come from a table of
 * observations) is each month's shorter length and a bit a year for each month whose length varies, which
 * this module adds up into the day each of its years starts on, the first time a date is asked of it.
 *
 * A LEAF module: `datetime.ts`, `date-reading.ts` and `format.ts` read it, and it reads nothing but the
 * wire's types.
 */
import type { CalendarData, CalendarMonths } from './culture';

/** A date as a calendar counts it: its year, its month (1 to 12) and its day of the month. */
export interface CalendarDate {
  year: number;
  month: number;
  day: number;
}

/** 0001-01-01 is day 0; 1970-01-01 is this many days after it. */
export const DAYS_0001_TO_1970 = 719_162;

/** The day `DateTime.MaxValue` falls on, 9999-12-31, as days since 0001-01-01: the last day a date holds. */
export const LAST_DAY = 3_652_058;

function idiv(a: number, b: number): number {
  return Math.trunc(a / b);
}

/** Days since 0001-01-01 (which is day 0), proleptic Gregorian. */
export function daysFromCivil(year: number, month: number, day: number): number {
  const y = month <= 2 ? year - 1 : year;
  const era = idiv(y >= 0 ? y : y - 399, 400);
  const yoe = y - era * 400;
  const doy = idiv(153 * (month + (month > 2 ? -3 : 9)) + 2, 5) + day - 1;
  const doe = yoe * 365 + idiv(yoe, 4) - idiv(yoe, 100) + doy;
  return era * 146097 + doe - 719468 + DAYS_0001_TO_1970;
}

/** Inverse of {@link daysFromCivil}. */
export function civilFromDays(daysSince0001: number): CalendarDate {
  const z = daysSince0001 - DAYS_0001_TO_1970 + 719468;
  const era = idiv(z >= 0 ? z : z - 146096, 146097);
  const doe = z - era * 146097;
  const yoe = idiv(doe - idiv(doe, 1460) + idiv(doe, 36524) - idiv(doe, 146096), 365);
  const y = yoe + era * 400;
  const doy = doe - (365 * yoe + idiv(yoe, 4) - idiv(yoe, 100));
  const mp = idiv(5 * doy + 2, 153);
  const day = doy - idiv(153 * mp + 2, 5) + 1;
  const month = mp < 10 ? mp + 3 : mp - 9;
  return { year: month <= 2 ? y + 1 : y, month, day };
}

export function isLeapYear(year: number): boolean {
  return (year % 4 === 0 && year % 100 !== 0) || year % 400 === 0;
}

const DAYS_IN_MONTH = [31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31];

export function daysInMonth(year: number, month: number): number {
  return month === 2 && isLeapYear(year) ? 29 : DAYS_IN_MONTH[month - 1];
}

/**
 * A culture's calendar, as the writer and the reader ask it: the date a day falls on and the day a date
 * names, each null where the calendar writes no such day, and a year written in one or two digits read
 * into its century.
 */
export interface Calendar {
  /** The type .NET names in a refusal: `System.Globalization.ThaiBuddhistCalendar`. */
  readonly name: string;
  /** .NET's refusal of a day the calendar does not write, where there is such a day. */
  readonly outOfRange: string | null;
  /** Whether a `DateTime` of the first day, written with no format, is written in the invariant sortable
   * form, as .NET writes one where its calendar cannot write the year 1. */
  readonly timeOfDayInvariant: boolean;
  /** The date day `day` (days since 0001-01-01) is in this calendar, or null outside the days it writes. */
  dateOf(day: number): CalendarDate | null;
  /** The day (days since 0001-01-01) a date of this calendar names, or null where it names none: a
   * month or a day past its count, or a day outside the ones it writes. */
  dayOf(date: CalendarDate): number | null;
  /** A year of one or two digits, as .NET's `ToFourDigitYear` reads it against `TwoDigitYearMax`: 2049
   * in the Gregorian calendar puts 00 to 49 in this century and 50 to 99 in the last. */
  fourDigitYear(year: number): number;
  /** The first and the last day it writes, as days since 0001-01-01: `MinSupportedDateTime` and
   * `MaxSupportedDateTime`'s days (#1849). */
  readonly firstDay: number;
  readonly lastDay: number;
  /** Why .NET refuses a move (`AddMonths`, `AddYears`) to month `month` of year `year`, in its words, or
   * null where the calendar counts that month (#1849). */
  refusedMove(year: number, month: number): MoveRefusal | null;
}

/** .NET's `ArgumentOutOfRangeException` for a move past a calendar's range: its message, and the
 * parameter and value it names where it names them. */
export interface MoveRefusal {
  readonly message: string;
  readonly paramName?: string;
  readonly actualValue?: number;
}

/** What every calendar's description says besides its arithmetic: its name, its refusal, its range and
 * its two-digit years. */
abstract class DescribedCalendar implements Calendar {
  readonly name: string;
  readonly outOfRange: string | null;
  readonly timeOfDayInvariant: boolean;
  readonly firstDay: number;
  readonly lastDay: number;
  private readonly twoDigitYearMax: number;

  constructor(data: CalendarData) {
    this.name = data.name;
    this.outOfRange = data.outOfRange ?? null;
    this.timeOfDayInvariant = data.timeOfDayInvariant === true;
    this.firstDay = data.firstDay ?? 0;
    this.lastDay = data.lastDay ?? LAST_DAY;
    this.twoDigitYearMax = data.twoDigitYearMax;
  }

  /** Whether the calendar writes day `day`. */
  protected writes(day: number): boolean {
    return day >= this.firstDay && day <= this.lastDay;
  }

  abstract dateOf(day: number): CalendarDate | null;
  abstract dayOf(date: CalendarDate): number | null;
  abstract refusedMove(year: number, month: number): MoveRefusal | null;

  fourDigitYear(year: number): number {
    if (year >= 100) return year;
    const max = this.twoDigitYearMax;
    return (Math.trunc(max / 100) - (year > max % 100 ? 1 : 0)) * 100 + year;
  }
}

/** The Gregorian months, under a count of years `offset` ahead of the Gregorian one. */
class OffsetCalendar extends DescribedCalendar {
  private readonly offset: number;

  constructor(data: CalendarData) {
    super(data);
    this.offset = data.yearOffset ?? 0;
  }

  dateOf(day: number): CalendarDate | null {
    if (!this.writes(day)) return null;
    const date = civilFromDays(day);
    return { ...date, year: date.year + this.offset };
  }

  dayOf({ year, month, day }: CalendarDate): number | null {
    const gregorian = year - this.offset;
    if (gregorian < 1 || gregorian > 9999 || month < 1 || month > 12 || day < 1) return null;
    if (day > daysInMonth(gregorian, month)) return null;
    const at = daysFromCivil(gregorian, month, day);
    return this.writes(at) ? at : null;
  }

  /** A year past the Gregorian ones a `DateTime` holds, as .NET's Gregorian and Thai Buddhist calendars
   * word it, with no parameter. */
  refusedMove(year: number): MoveRefusal | null {
    const gregorian = year - this.offset;
    return gregorian >= 1 && gregorian <= 9999
      ? null
      : { message: 'Year, Month, and Day parameters describe an un-representable DateTime.' };
  }
}

/** A calendar of twelve months of its own, its years' starts added up the first time a date is asked. */
class MonthsCalendar extends DescribedCalendar {
  private readonly firstYear: number;
  private readonly years: number;
  private readonly lengths: readonly number[];
  /** For each month, 0 to 11, where its bit is among a year's, or -1 for a month of one length. */
  private readonly slot: number[];
  private readonly width: number;
  private readonly encoded: string;
  private readonly start: number;
  private bits: Uint8Array | null = null;
  /** The day each year starts on, and the day after the last one ends, as days since 0001-01-01. */
  private starts: Int32Array | null = null;

  constructor(data: CalendarData, months: CalendarMonths) {
    super(data);
    this.firstYear = months.firstYear;
    this.years = months.years;
    this.lengths = months.lengths;
    this.slot = this.lengths.map((_, month) => months.varying.indexOf(month + 1));
    this.width = months.varying.length;
    this.encoded = months.longer;
    this.start = months.start;
  }

  /** How long month `month` (0 to 11) of the year at `index` is. */
  private length(index: number, month: number): number {
    const slot = this.slot[month];
    if (slot < 0) return this.lengths[month];
    const bit = index * this.width + slot;
    const bits = this.bits ?? (this.bits = Uint8Array.from(atob(this.encoded), (c) => c.charCodeAt(0)));
    return this.lengths[month] + ((bits[bit >> 3] >> (7 - (bit & 7))) & 1);
  }

  private yearStarts(): Int32Array {
    if (this.starts !== null) return this.starts;
    const starts = new Int32Array(this.years + 1);
    starts[0] = this.start;
    for (let index = 0; index < this.years; index++) {
      let days = 0;
      for (let month = 0; month < 12; month++) days += this.length(index, month);
      starts[index + 1] = starts[index] + days;
    }
    return (this.starts = starts);
  }

  dateOf(day: number): CalendarDate | null {
    if (!this.writes(day)) return null;
    const starts = this.yearStarts();
    // The last year that starts on or before the day.
    let low = 0;
    let high = this.years - 1;
    while (low < high) {
      const middle = (low + high + 1) >> 1;
      if (starts[middle] <= day) low = middle;
      else high = middle - 1;
    }
    let rest = day - starts[low];
    let month = 0;
    while (month < 11 && rest >= this.length(low, month)) rest -= this.length(low, month++);
    return { year: this.firstYear + low, month: month + 1, day: rest + 1 };
  }

  dayOf({ year, month, day }: CalendarDate): number | null {
    const index = year - this.firstYear;
    if (index < 0 || index >= this.years || month < 1 || month > 12 || day < 1) return null;
    if (day > this.length(index, month - 1)) return null;
    let at = this.yearStarts()[index] + day - 1;
    for (let before = 0; before < month - 1; before++) at += this.length(index, before);
    return this.writes(at) ? at : null;
  }

  /** A year outside the ones it counts, then a month past its last year's last, as the Persian and Um Al
   * Qura calendars word them: the Persian calendar's last year, 9378, ends in its tenth month. */
  refusedMove(year: number, month: number): MoveRefusal | null {
    const lastYear = this.firstYear + this.years - 1;
    if (year < this.firstYear || year > lastYear) {
      return { message: `Valid values are between ${this.firstYear} and ${lastYear}, inclusive.`, paramName: 'year', actualValue: year };
    }
    const last = this.dateOf(this.lastDay);
    if (last !== null && year === last.year && month > last.month) {
      return { message: `Valid values are between 1 and ${last.month}, inclusive.`, paramName: 'month', actualValue: month };
    }
    return null;
  }
}

/** The Gregorian calendar, which a culture whose data did not travel and the invariant one count in. */
export const GREGORIAN: Calendar = new OffsetCalendar({
  name: 'System.Globalization.GregorianCalendar',
  twoDigitYearMax: 2049,
  yearOffset: 0,
});

const calendars = new WeakMap<CalendarData, Calendar>();

function isGregorian(data: CalendarData): boolean {
  return (
    data.name === GREGORIAN.name &&
    data.twoDigitYearMax === 2049 &&
    (data.yearOffset ?? 0) === 0 &&
    data.months === undefined &&
    data.firstDay === undefined &&
    data.lastDay === undefined
  );
}

/** The calendar a culture's data describes, built once for each description; the Gregorian one where
 * the data has none or describes it, which is the one the reader knows needs no second reading of the
 * ISO form. */
export function calendarOf(data: CalendarData | null | undefined): Calendar {
  if (data == null || isGregorian(data)) return GREGORIAN;
  let calendar = calendars.get(data);
  if (calendar === undefined) {
    calendar = data.months !== undefined ? new MonthsCalendar(data, data.months) : new OffsetCalendar(data);
    calendars.set(data, calendar);
  }
  return calendar;
}
