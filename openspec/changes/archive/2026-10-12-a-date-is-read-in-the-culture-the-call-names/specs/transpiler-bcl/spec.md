## MODIFIED Requirements

### Requirement: A time type's TryParse answers as .NET's

`TryParse(string, out T)` of a `DateTime`, a `DateTimeOffset`, a `DateOnly`, a `TimeOnly` and a
`TimeSpan` SHALL answer true and write the value its `Parse` reads through the out argument, or answer
false and write the type's default there for a text its `Parse` refuses and for a null one, and SHALL
never throw. The out argument SHALL be any place C# can write, a discard, a variable declared before
and an array's element included, and the arguments SHALL be evaluated in the order written.
`TimeOnly.Parse` SHALL read the whole text in the culture the call names (`runtime-dates`): an hour
past 23, a minute past 59 and a second past 59 refused, an AM or PM designator before or after the
time in either case, `12` read as midnight under AM, an hour past 12 refused under AM and kept under
PM, an hour alone only beside a designator, and a fraction only after the seconds, rounded to the tick
as .NET rounds it. `TimeOnly.Parse` and `DateOnly.Parse` SHALL refuse a text in .NET's words. The
overloads of the five's `TryParse` with a provider, with styles or with both SHALL read as
`runtime-dates` says, and one over a span SHALL fail the build (EQ1004).

#### Scenario: A text that is a date

- **WHEN** `DateTime.TryParse("2024-01-15", out var d)` runs
- **THEN** it answers true with that date in `d`, as in .NET, where the page failed with a TypeError

#### Scenario: A text that is no date

- **WHEN** `DateOnly.TryParse("abc", out var d)` runs
- **THEN** it answers false with `DateOnly.MinValue` in `d`, as in .NET, where it answered null

#### Scenario: A clock that does not exist

- **WHEN** `TimeOnly.TryParse("25:00", out var t)` runs
- **THEN** it answers false, as in .NET, where `Parse` read a day and an hour

#### Scenario: A time with a designator

- **WHEN** `TimeOnly.Parse("9:30 PM")` and `TimeOnly.TryParse("13:00 AM", out var t)` run
- **THEN** the first answers 21:30 and the second false, as in .NET, where the first was half past
  nine in the morning

#### Scenario: An overload with a provider

- **WHEN** a page calls `DateTime.TryParse(text, null, out var when)`
- **THEN** it reads the text in the page's culture, as .NET reads the current one, where the build
  failed (EQ1004)

#### Scenario: An overload over a span

- **WHEN** a page calls `TimeSpan.TryParse(text.AsSpan(), out var span)`
- **THEN** the build fails (EQ1004)
