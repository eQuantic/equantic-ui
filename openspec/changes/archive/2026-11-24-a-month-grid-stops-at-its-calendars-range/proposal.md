## Why

#1849, a Bug under #1587, found while fixing #1617. A bounded calendar refuses a move past its range: ar-SA's Um Al Qura calendar writes from April 30th 1900 to November 16th 2077. The month grid draws that calendar's first and last month since #1617, but its chevron past the last month and its keyboard's page and section moves called `CultureCalendar.AddMonths` past the range, which throws on both sides, so the press failed. The browser's refusal was in other words than .NET's: .NET refuses Um Al Qura's move by its year ("Valid values are between 1318 and 1500, inclusive."), the Persian calendar's by its year or its last year's tenth month, and a Gregorian or Thai one as a date a `DateTime` cannot hold. And at the Gregorian calendar's own edges, December 9999 and January of the year 1, the grid failed to build: its weeks reached days no date names.

## What Changes

- `CultureCalendar` gains `FirstDay` and `LastDay`, the calendar's `MinSupportedDateTime` and `MaxSupportedDateTime` as days.
- The browser's calendars say why .NET refuses a move to a month they do not count, in .NET's words, and `CultureCalendar.AddMonths` and `AddYears` refuse with it: Um Al Qura's and the Persian calendar's year range and the Persian calendar's last month, read from their data, and the Gregorian and Thai calendars' un-representable date.
- `Calendar` stops at its calendar's range: a chevron past its first or last month does not page, a page or section move from the keyboard lands on the calendar's first or last day, an arrow stays inside the days it writes, and a day it does not write is no target. The grid counts its cells by day number, so a cell past the year 9999 or before the year 1 is a hole.

A developer writes nothing. `CultureCalendar.FirstDay` and `LastDay` are new public surface of `eQuantic.UI.Primitives`, declared in its API file. Nothing goes. The parts reached are the vocabulary, the runtime's calendars and twin, and the shared `Calendar`.

Principles followed: .NET is the oracle, every refusal measured on .NET 10 and compared on both sides.

## Capabilities

### New Capabilities

### Modified Capabilities

- `month-grid`: a move past the calendar's range is refused in its words, and the grid stops at that range.

## Impact

- `src/eQuantic.UI.Primitives/Text/CultureCalendar.cs` and its API file.
- `src/eQuantic.UI.Runtime/src/utils/calendar.ts`, `src/shared/culture-calendar.ts`.
- `src/eQuantic.UI.Components/Calendar.cs` and its transpiled twin.
- Tests: `CultureCalendarTwinTests`, `CalendarTests`.
