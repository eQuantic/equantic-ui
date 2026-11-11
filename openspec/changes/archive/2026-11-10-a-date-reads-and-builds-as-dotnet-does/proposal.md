## Why

Closes #807 and #952, both sub-issues of Feature #849.

- `DateTime`, `DateOnly` and `DateTimeOffset` read the numeric forms a culture writes and refused the rest, where .NET reads a month's name, a day's name, a date of two numbers, a clock alone, `GMT`, numbers separated by white space and a point after a year. The conformance suite measured 187 of 232 such cases failing on main, each form in four cultures.
- `new TimeOnly(h, m, s, ms, µs)` read four numbers and checked none: the microsecond was dropped and `new TimeOnly(25, 0)` built a time past a day.

## What Changes

- `utils/date-reading.ts` reads a month's name as .NET's `GetDayOfMN`, `GetDayOfNM`, `GetDayOfMNN` and `GetDayOfYMN` do. A number of three digits or more is the year; a month and a year are the month's first day; a month and one short number are a day of this year, unless the culture's month-day and year-month patterns make it a year, so `Jan 5` is 2005 in de-DE and `15 Jan` is 2015 in the invariant culture; and two short numbers are told apart by the short date pattern. A name is read in the culture's language and in English, in any case, spelled as the culture spells it, so de-DE's `Sept.` is no `Sept`.
- A day's name before a date must be that date's day, or the read is refused in .NET's words. Two numbers are a month and a day of this year in the month-day pattern's order. White space alone may separate a date's numbers. A point may end a date, except where a point is what separates the culture's date. A clock alone is today's. `GMT` is the zero offset and takes none after it.
- A date's words are read, every word a culture's long, month-day and year-month patterns write (pt-BR's `de`, ru-RU's `г.`, ja-JP's `年`), so what a culture writes reads back, and a day's name may follow the date. A `DateTimeOffset` with no date is today at the offset it names.
- `new TimeOnly` keeps its microsecond and refuses each part out of range, and its ticks overload refuses a value outside a day, in .NET's words.
- A date in a calendar other than the Gregorian one is still read as a Gregorian one: the issue listed it, and it moved to #1191, since reading and writing both need it and the culture document does not say its calendar.

## Impact

- Reaches the runtime (`utils/date-reading.ts`, `utils/datetime.ts`).
- Neither `PublicAPI.Shipped.txt` / `PublicAPI.Unshipped.txt` nor `developer-surface.baseline.txt` moves.
