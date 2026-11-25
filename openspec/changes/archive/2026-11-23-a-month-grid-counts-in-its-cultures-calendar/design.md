## Context

`Calendar` is a write-once component: the same C# is transpiled into the runtime for the web and runs as
.NET on Photon. It counts its grid with `DateOnly`, which is always Gregorian, and names its month through
`CalendarNames`, which reads the culture's own `DateTimeFormatInfo`. Since #1191 the browser carries each
culture's calendar (`utils/calendar.ts`: the date a day falls on, the day a date names), so a count in
that calendar is possible on both sides.

## Goals / Non-Goals

**Goals:** the grid's month, title, cells, announcements and moves are the culture calendar's, the same
on the server, in the browser and on Photon; nothing changes in a Gregorian culture.

**Non-Goals:** a browser twin of `System.Globalization.Calendar` for an app's own code, and
`CultureInfo.CurrentCulture.DateTimeFormat` crossing to the browser. Both are wider than the grid and stay
a feature of their own. A lunisolar calendar's thirteenth month is out too: no culture's default calendar
has one.

## Decisions

**How Flutter solves it.** It does not: `CalendarDatePicker` counts with `DateTime` and `DateUtils`,
Gregorian only, and titles through `MaterialLocalizations.formatMonthYear`. `docs/FLUTTER-PARITY.md` has
no row for the picker. The count therefore follows .NET, whose `DateTimeFormatInfo.Calendar` is each
culture's answer.

**A vocabulary type beside `CalendarNames`, not a BCL twin.** The component needs seven questions (a day's
year, month and day, its month's first day and length, and a move by months or years). `CalendarNames` already is
the vocabulary's door to the culture's date data, with a hand-written twin that reads what the server
sent. `CultureCalendar` is its sibling with the same rule. On .NET it delegates to
`DateTimeFormatInfo.Calendar`, so Photon and the server count with .NET itself. A twin of
`System.Globalization.Calendar` would serve an app's code too, but it needs `DateTimeFormatInfo` to cross,
an abstract class and every calendar that derives from it, and `DateTime` arguments the component does not use: a feature of
its own, which this does not block.

**The twin reads the shipped calendar, the Gregorian one otherwise.** It answers through
`calendarOf(activeFormatData()?.dateTimeFormat.calendar)`, the description the server wrote for the
format culture. A culture whose data did not travel counts Gregorian, and `CalendarNames`' `Intl` fallback
then names Gregorian months (`calendar: 'gregory'`), so the count and the names never disagree.

**Moves cut the day as .NET does.** `Calendar.AddMonths` keeps the day of the month and cuts it to the
target month's last day (fa-IR: the 30th of Esfand 1403 plus a year is the 29th of Esfand 1404). The twin
finds that day by asking the calendar for the day a date names, from the kept day down, which is how the
calendar's own data says how long a month is. A day the calendar does not write (past UmAlQura's last
year) is refused with the calendar's own `ArgumentOutOfRangeException`, as .NET refuses it.

**The grid counts through it everywhere a month is meant.** `_month` stays a `DateOnly`, now the first
day of the culture calendar's month; the title, the hole test, the numeral, the announcement, the cursor's
cell and the moves go through `CultureCalendar`. Days and weeks stay `DateOnly` arithmetic, which every
calendar shares.

## Risks / Trade-offs

- The grid's six weeks hold a month of up to 31 days in every calendar a culture defaults to, so the
  layout does not change.
- A culture whose data did not travel counts Gregorian in the browser while .NET would count its own
  calendar. That path already has no server to agree with, and its names now say Gregorian too.
- A calendar of a bounded range (Um Al Qura's, from April 1900 to November 2077) refuses the month of a
  day past it, and the grid's leading and trailing weeks reach such days in its first and last month. The
  grid therefore counts its month by `CultureCalendar.DaysInMonth` from the month's first day, and asks
  nothing of a day outside it. A move past the range is still refused on both sides in other words, and
  the chevrons and arrows are not yet bounded by it: #1849.
