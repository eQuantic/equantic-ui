- **2026-11-18 · A date is written and read in the calendar of its culture**: a Bug under #1174
  ([#1191](https://github.com/eQuantic/equantic-ui/issues/1191)). th-TH counts the Thai Buddhist years, fa-IR the
  Persian calendar's and ar-SA Um Al Qura's, and the browser wrote and read the Gregorian numbers under those
  calendars' names: `15/1/2567` read as the year 2567. The culture's format data now carries its calendar, measured by
  the server from .NET's own: a count of years ahead of the Gregorian one, or each month's shorter length and a bit a
  year for each month whose length varies, so the Persian leap years .NET computes from the equinox arrive as .NET has
  them. The writer writes a date's year, month, day and era in it and refuses a day it does not write in its own
  words; the reader reads a date's numbers, this year and a year of two digits in it, and the ISO form in the
  Gregorian calendar, as .NET does, and the era's name or abbreviation beside a date, as .NET reads it in every
  culture. A calendar of eras or of a thirteenth month is refused by the server rather than
  written in Gregorian numbers. Measured on both sides by `CultureCalendarConformanceTests` in four cultures, and over
  each calendar's whole range by `CalendarBridgeTests`. Proposed and archived through OpenSpec
  (`openspec/specs/runtime-dates`).
