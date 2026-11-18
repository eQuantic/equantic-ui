# Tasks

## 1. The calendar travels with the culture

- [x] 1.1 The server measures the culture's calendar from .NET's own and writes it in the date format data (#1191)
- [x] 1.2 A calendar of eras or of a thirteenth month is refused rather than written in Gregorian numbers
- [x] 1.3 Prove the description counts every month as .NET does, over each calendar's whole range: `CalendarBridgeTests`

## 2. The browser writes and reads in it

- [x] 2.1 The writer's year, month, day and era are the calendar's; a day it does not write is refused in its words
- [x] 2.2 A time of day on the first day is written in the invariant sortable form where .NET writes it so
- [x] 2.3 The reader reads a date's numbers in the calendar, this year and a two-digit year as it counts them, and the ISO form in the Gregorian one
- [x] 2.4 Prove it on both sides: `CultureCalendarConformanceTests` in th-TH, fa-IR, ar-SA and en-US, failing against main and passing; the format fixture in the three cultures
