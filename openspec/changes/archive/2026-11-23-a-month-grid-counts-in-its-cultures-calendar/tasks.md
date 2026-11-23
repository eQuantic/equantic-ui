## 1. The count

- [x] 1.1 `CultureCalendar` in `eQuantic.UI.Primitives` (`YearOf`, `MonthOf`, `DayOf`, `FirstOfMonth`, `AddMonths`, `AddYears`) over `DateTimeFormatInfo.Calendar`, declared in its API file
- [x] 1.2 Its twin, `shared/culture-calendar.ts`, over `calendarOf` of the format culture's shipped calendar, the Gregorian one where none travelled, exported by the runtime
- [x] 1.3 `CalendarNames`' `Intl` fallback names Gregorian months
- [x] 1.4 Conformance, both sides executed: every member in fa-IR, ar-SA, th-TH and en-US, the days around the year's turn and a month's end, the moves cut to a shorter month; it fails against a twin that counts Gregorian, and against one that keeps a day the target month lacks

## 2. The grid

- [x] 2.1 `Calendar` counts its month, title, cells, announcements, holes and moves through `CultureCalendar`
- [x] 2.2 The transpiled twin regenerated (`EQ_UPDATE_TRANSPILED=1`)
- [x] 2.3 The grid's tests in fa-IR, th-TH and en-US: the title, the first and the selected cell, a hole, a page; they fail on the grid that counts Gregorian
- [x] 2.4 The full local suites, Conformance whole, and `TestRuntime`

## 3. Documentation

- [x] 3.1 One ledger entry under `docs/ledger/` citing #1617
- [x] 3.2 The wiki pages that describe the calendar or the culture's names, in every language they are written in, on a wiki branch of the same name
