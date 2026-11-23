## Why

#1617, a Bug under #1587. `Calendar`, and the `DatePicker` that opens it, lays out a Gregorian month and
titles it with `CalendarNames.MonthNames[month - 1]`, which are the names of the culture's own calendar.
In a culture whose calendar is not the Gregorian one, the grid names another calendar's month: January
2024 is titled `دی 2024` in fa-IR (Dey, the Persian month from the 22nd of December to the 20th of
January), `محرم 2024` in ar-SA (a Hijri month), and `มกราคม 2024` in th-TH, whose calendar counts that
year 2567. A cell's announcement carries the same mixture. Server and browser agree, as both read the same
names, so no conformance test saw it.

## What Changes

- The vocabulary gains `CultureCalendar`, beside `CalendarNames`: what the active format culture's calendar
  counts for a day. `YearOf`, `MonthOf` and `DayOf` answer a `DateOnly`'s year, month and day in that
  calendar, `FirstOfMonth` the first day of its month, and `AddMonths` and `AddYears` move a day as .NET's
  `Calendar.AddMonths` and `AddYears` move it, the day of the month cut to the last one the target month
  has. In .NET it reads `CultureInfo.CurrentCulture.DateTimeFormat.Calendar`. Its client twin reads the
  calendar the culture's data carries to the browser (`utils/calendar.ts`, #1191), and the Gregorian one
  where no data travelled.
- `Calendar` counts its grid in that calendar: the month on screen is the culture calendar's month, its
  title names that month and that calendar's year, a cell shows that calendar's day, a day outside the
  month is a hole by that calendar's months, and paging and the keyboard's page and section moves go a
  month and a year of that calendar. In fa-IR, January 17th 2024 opens `دی 1402`, from the 22nd of December
  to the 20th of January, and its cell says 27. In a Gregorian culture nothing changes.
- `CalendarNames`' fallback for a culture whose data did not travel names the Gregorian months
  (`calendar: 'gregory'`), as the fallback counts them, where `Intl` named the locale's own calendar's.

A developer writes nothing: the grid follows the culture the page already sets. `CultureCalendar` is new
public surface of `eQuantic.UI.Primitives` (a contract assembly), declared in its `PublicAPI.Unshipped`
file. Nothing goes. The parts reached are the vocabulary, its runtime twin (exported by the runtime the
Server bundles), the shared `Calendar` component (transpiled into the runtime, and run as .NET on Photon),
and nothing in eqc, the SDKs or the templates.

Principles followed: the write-once component (one `Calendar`, both targets), the vocabulary speaks no
target's language (`CultureCalendar` names .NET's idea, not a DOM or ICU one), the server's culture data
wins over the browser's own tables (`CalendarNames`' rule, #471), and the mechanism .NET already has
(`DateTimeFormatInfo.Calendar`) is the source of truth the twin is measured against.

## Capabilities

### New Capabilities

- `month-grid`: the month grid counts, titles and pages its month in the culture's calendar.

### Modified Capabilities

## Impact

- `src/eQuantic.UI.Primitives/Text/CultureCalendar.cs` (new) and its API declaration.
- `src/eQuantic.UI.Runtime/src/shared/culture-calendar.ts` (new), exported from the runtime;
  `shared/calendar-names.ts`' fallback.
- `src/eQuantic.UI.Components/Calendar.cs`, and its transpiled twin under `src/shared/components/`.
- Tests: a conformance suite of `CultureCalendar` on both sides, in Gregorian and non-Gregorian cultures,
  the grid's own on .NET, and the runtime's specs.
