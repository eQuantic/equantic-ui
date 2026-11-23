import { activeFormatData } from '../utils/culture';
import { calendarOf, type Calendar, type CalendarDate } from '../utils/calendar';
import { DateOnly } from '../utils/datetime';
import { exception } from '../utils/exceptions';

/**
 * Client twin of the C# `eQuantic.UI.Primitives.CultureCalendar`: what the active format culture's calendar
 * counts for a day (#1617). January 17th 2024 is the 27th of Dey 1402 in fa-IR, the 5th of Rajab 1445 in
 * ar-SA and the 17th of January 2567 in th-TH.
 *
 * The calendar is the one the culture's data carries to the browser, which the server described by
 * measuring .NET's own (`CalendarBridge`, #1191): the twin counts with what .NET counts, never with the
 * browser's `Intl`, whose calendars are other data. A culture whose data did not travel counts the
 * Gregorian calendar, and `CalendarNames`' fallback names the Gregorian months to match.
 */
export class CultureCalendar {
  /** The year a day falls in, as `Calendar.GetYear` answers it. */
  static yearOf(day: DateOnly): number {
    return dateOf(current(), day.dayNumber).year;
  }

  /** The month a day falls in, 1 for the calendar's first, as `Calendar.GetMonth` answers it. */
  static monthOf(day: DateOnly): number {
    return dateOf(current(), day.dayNumber).month;
  }

  /** The day of its month, as `Calendar.GetDayOfMonth` answers it. */
  static dayOf(day: DateOnly): number {
    return dateOf(current(), day.dayNumber).day;
  }

  /** The first day of the month a day falls in. */
  static firstOfMonth(day: DateOnly): DateOnly {
    return new DateOnly(day.dayNumber + 1 - CultureCalendar.dayOf(day));
  }

  /** The day `months` of the calendar's months away, as `Calendar.AddMonths` moves it: the day of the
   * month kept, and cut to the last day the target month has. */
  static addMonths(day: DateOnly, months: number): DateOnly {
    const calendar = current();
    const { year, month, day: of } = dateOf(calendar, day.dayNumber);
    const counted = year * 12 + (month - 1) + months;
    return moved(calendar, Math.floor(counted / 12), (((counted % 12) + 12) % 12) + 1, of, day);
  }

  /** The day `years` of the calendar's years away, as `Calendar.AddYears` moves it: the 30th of Esfand
   * 1403 plus a year is the 29th of Esfand 1404, the month's last day. */
  static addYears(day: DateOnly, years: number): DateOnly {
    const calendar = current();
    const { year, month, day: of } = dateOf(calendar, day.dayNumber);
    return moved(calendar, year + years, month, of, day);
  }
}

/** The format culture's calendar, the Gregorian one where its data did not travel. */
function current(): Calendar {
  return calendarOf(activeFormatData()?.dateTimeFormat.calendar);
}

/** The date `day` (days since 0001-01-01) is in `calendar`, or .NET's refusal of a day it does not write:
 * ar-SA's Um Al Qura calendar writes from 1900 to 2077. */
function dateOf(calendar: Calendar, day: number): CalendarDate {
  const date = calendar.dateOf(day);
  if (date !== null) return date;
  throw outOfRange(calendar, day);
}

/** The day `year`/`month` names with the day of the month `of`, cut to the month's last: the calendar
 * names no day past a month's count, so the last one it names, from `of` down, is the month's last. */
function moved(
  calendar: Calendar,
  year: number,
  month: number,
  of: number,
  from: DateOnly,
): DateOnly {
  for (let day = of; day >= 1; day--) {
    const named = calendar.dayOf({ year, month, day });
    if (named !== null) return new DateOnly(named);
  }
  throw outOfRange(calendar, from.dayNumber);
}

function outOfRange(calendar: Calendar, day: number): Error {
  return exception('System.ArgumentOutOfRangeException', calendar.outOfRange ?? '', {
    paramName: 'time',
    actualValue: BigInt(day) * 864_000_000_000n,
  });
}
