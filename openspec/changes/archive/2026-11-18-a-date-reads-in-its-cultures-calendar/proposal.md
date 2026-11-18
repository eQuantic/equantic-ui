# Proposal

Closes #1191, a Bug under #1174 (The transpiler's fences hold on every path, part four): a date written
and read in the browser in the Gregorian numbers, in a culture whose calendar is another.

## Why

- Three cultures .NET ships count their dates in a calendar that is not the Gregorian one: th-TH in the
  Thai Buddhist years, 543 ahead, fa-IR (and eleven more Persian-speaking, Pashto, Kurdish, Luri,
  Mazanderani and Uzbek cultures) in the Persian calendar, and ar-SA in Um Al Qura. .NET writes the
  15th of January 2024 as `15/1/2567`, `1402/10/25` and `3 رجب، 1445` there, and reads each back as that
  day. The browser wrote `15/1/2024` under the Thai era's name and Persian month names on Gregorian
  months, and read `15/1/2567` as the year 2567, with no refusal.
- The culture's format data carried the calendar's names and patterns and nothing that said which
  calendar they belonged to.

## What Changes

- The culture's date format data carries its calendar, MEASURED by the server from .NET's own
  `Calendar`: the Gregorian months under another count of years is that count's distance (543 for the
  Thai Buddhist calendar, 0 for the Gregorian one); any other calendar of twelve months a year is each
  month's shorter length and a bit a year for each month whose length varies, so the Persian calendar's
  leap years, which .NET computes from the equinox, and Um Al Qura's observed months arrive as .NET
  counts them. A calendar of eras (the Japanese one) or of a thirteenth month (the Hebrew one) is refused
  by the server rather than written in the Gregorian numbers.
- The writer writes a date's year, month, day and era in the culture's calendar, and a part that names
  no date in any calendar. A day the calendar does not write (Um Al Qura's before 1900 or after 2077, the
  Persian calendar's before 622) is refused with .NET's `ArgumentOutOfRangeException`, in the calendar's
  own words; a time of day held in a `DateTime` of the first day is written, with no format, in the
  invariant sortable form there, as .NET writes it.
- The reader reads a date's numbers in the culture's calendar: a date of three numbers, a date with a
  month's name, a date of two numbers in this year as that calendar counts it, and a year of two digits
  against that calendar's `TwoDigitYearMax`. The ISO form, a year-first date before a `T`, is the
  Gregorian calendar's in every culture, as .NET reads it, and a clock that does not exist is refused in
  the name of the culture's calendar.

## Impact

- `src/eQuantic.UI.Web/CalendarBridge.cs` (new) and `CultureFormatBridge.cs`: the `calendar` of the
  date format data, every page's and every culture switch's.
- `src/eQuantic.UI.Runtime/src/utils/calendar.ts` (new), which also takes the Gregorian arithmetic from
  `datetime.ts`; `date-reading.ts`, `format.ts`, `culture.ts`.
- The served runtime grows 1,444 bytes gzipped, measured against `main`; a Persian or an Um Al Qura
  culture's data, which every page in it carries, grows by its months, about 400 bytes gzipped.
