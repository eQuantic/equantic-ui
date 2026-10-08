import { INVARIANT_FORMAT, type CultureFormat } from '../../utils/culture';

/** One culture of `calendar-names.fixture.json`, as the C# side wrote it (CalendarNamesFixtureTests). */
export interface CalendarSnapshot {
  firstDayOfWeek: number;
  dayNamesShort: string[];
  dayNamesLong: string[];
  monthNames: string[];
  monthNamesShort: string[];
  shortDatePattern: string;
  dateFormatLetters: string;
  dateFormatHint: string;
}

/**
 * What the server writes for a culture, as far as a calendar reads it: the snapshot's first day,
 * names and short date pattern, which are its `DateTimeFormatInfo`'s, in the format data a page
 * installs (`CultureFormat`), over the invariant culture's for the rest.
 */
export function calendarFormat(snapshot: CalendarSnapshot): CultureFormat {
  return {
    ...INVARIANT_FORMAT,
    dateTimeFormat: {
      ...INVARIANT_FORMAT.dateTimeFormat,
      firstDayOfWeek: snapshot.firstDayOfWeek,
      abbreviatedDayNames: snapshot.dayNamesShort,
      dayNames: snapshot.dayNamesLong,
      monthNames: snapshot.monthNames,
      abbreviatedMonthNames: snapshot.monthNamesShort,
      shortDatePattern: snapshot.shortDatePattern,
    },
  };
}
