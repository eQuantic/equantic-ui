## Why

#1841, #1842 and #1843, three Bugs under #1587 that the review of #1616 found in the browser's date reader. Each is a text .NET reads, or refuses, in another way than the browser did:

- **#1841.** .NET reads the ISO form, a year-first date, a `T` and its clock, after a designator, a clock or a zone, and reads a zone before anything. `PM 2024-01-15T10:00` is 10:00 on January 15th, and `Z 2024-01-15` is that day in UTC. The browser read the ISO form only where the text began, and named an unknown word or refused plainly.
- **#1842.** .NET reads a day's name once, anywhere between the parts, and asks it of the date last, once the day and the clock exist. `2024-01-15 10:00 Monday` reads, and a second day's name is the plain refusal. The browser read one only beside the date, asked it at once, and named an unknown word after the clock.
- **#1843.** A number of three digits or more is a year to .NET's lexer, and a date has one. `2024-01-001` and `1/001/2024` name no date. The browser read them as January 1st.

## What Changes

- `readMoment` reads the ISO form where a date may start, before the leading date and before the trailing one, in the Gregorian calendar whatever the culture's, as .NET's `ParseISO8601` does. Its clock takes the place of a clock and a designator before it, its zone takes the place of a zone before it, and a day's name beside it is not asked.
- A zone may lead the text. A second zone is the plain refusal.
- A day's name is read once at every boundary between the parts, and beside the date as before. A second is the plain refusal where it stands. Whether it is the date's day is asked last, after a clock that does not exist, and of today where the text names no date, as .NET asks it.
- A numeric date with two numbers of three digits or more is refused.
- An offset past fourteen hours is refused last, after the rest of the text, the date, the clock and the day's name, and not where an ISO form after it writes its own zone, as .NET does. A zone after the clock was refused at once before this change too.
- A `T` before a clock after a trailing date that is not the ISO form's is the plain refusal, as it already was after a leading one.

A developer writes nothing. Neither the public surface nor the developer surface moves. The parts reached are the runtime's date reader (`utils/date-reading.ts`), and through it `DateTime.Parse`, `DateTimeOffset.Parse` and their `TryParse` twins.

Principles followed: .NET is the oracle, every row measured on .NET 10 and compared on both sides by the conformance suite.

## Capabilities

### New Capabilities

### Modified Capabilities

- `runtime-dates`: a date is read wherever .NET's lexer reads it.

## Impact

- `src/eQuantic.UI.Runtime/src/utils/date-reading.ts` and its spec.
- `tests/eQuantic.UI.Conformance.Tests/DateOrderConformanceTests.cs` (new).
