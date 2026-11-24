## 1. The range

- [x] 1.1 `CultureCalendar.FirstDay` and `LastDay`, declared in the API file; the runtime's calendars expose their first and last day
- [x] 1.2 Each runtime calendar says why .NET refuses a move to a month it does not count, and the twin refuses with it
- [x] 1.3 `CultureCalendarTwinTests`: the first and last day and the moves past them in fa-IR, ar-SA, th-TH and en-US; all 4 fail on a twin without the calendar's refusal

## 2. The grid

- [x] 2.1 `Calendar` stops at its calendar's range: chevrons, page and section moves, arrows, targets, and cells counted by day number
- [x] 2.2 `CalendarTests`: Um Al Qura's chevrons at 1900 and 2077 and the Gregorian grid at the years 1 and 9999; all 4 fail on main's grid
- [x] 2.3 The transpiled twin regenerated, and the full local suites, Conformance whole, and `TestRuntime`

## 3. Documentation

- [x] 3.1 One ledger entry under `docs/ledger/` citing #1849
