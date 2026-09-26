# runtime-dates Specification

## Purpose
What the runtime's twins of the date and time types (`DateTime`, `DateTimeOffset`, `TimeOnly`)
answer for .NET's date arithmetic, so a date computed on the web is the date .NET computes.

## Requirements

### Requirement: A fractional count lands on the tick .NET lands on

`AddDays`, `AddHours`, `AddMinutes`, `AddSeconds`, `AddMilliseconds` and `AddMicroseconds` of a
`DateTime` or a `DateTimeOffset` SHALL move the date by the ticks .NET 10 moves it: the whole units
and the fraction become ticks apart, and the fraction is truncated toward zero.

#### Scenario: A count below a millisecond

- **WHEN** `new DateTime(2026, 1, 1).AddSeconds(0.00001)` is computed
- **THEN** it is 100 ticks after the date, as in .NET, and `AddSeconds(-0.00001)` is 100 ticks before

#### Scenario: Half a millisecond

- **WHEN** `new DateTime(2026, 1, 1).AddMilliseconds(0.5)` is computed
- **THEN** it is 5000 ticks after the date, as in .NET, not a whole millisecond

#### Scenario: A fraction of a day

- **WHEN** `new DateTime(2026, 1, 1).AddDays(1.23456789)` is computed
- **THEN** it is 1066666656959 ticks after the date, as in .NET

#### Scenario: Microseconds, and the offset type's own

- **WHEN** `new DateTime(2026, 1, 1).AddMicroseconds(1.99)` and
  `new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(3)).AddMilliseconds(-0.5)` are computed
- **THEN** the first is 19 ticks after its date and the second 5000 ticks before its own, as in .NET

### Requirement: A time of day adds one product and wraps

`AddHours` and `AddMinutes` of a `TimeOnly` SHALL take one product of the count and the unit's
ticks, converted to a whole count of ticks as .NET 9 and later convert a double (toward zero, NaN
to zero, saturated at the ends of a long), and wrap it within the day.

#### Scenario: A count below a millisecond

- **WHEN** `new TimeOnly(10, 0).AddHours(0.0000001)` is computed
- **THEN** it is 3600 ticks after ten o'clock, as in .NET

#### Scenario: A fraction taken as one product

- **WHEN** `new TimeOnly(10, 0).AddHours(1.23456789)` is computed
- **THEN** its ticks are 404444444039, as in .NET

#### Scenario: A product past a long

- **WHEN** `new TimeOnly(10, 0).AddHours(1e20)` is computed
- **THEN** its ticks are 460854775807, as in .NET, where the product saturates before it wraps

### Requirement: NaN adds nothing

A NaN count SHALL leave a `DateTime`, a `DateTimeOffset` or a `TimeOnly` where it was, as the
conversion of .NET 9 and later turns NaN into zero ticks.

#### Scenario: NaN days and hours

- **WHEN** `new DateTime(2026, 1, 1).AddDays(double.NaN)` and `new TimeOnly(10, 0).AddHours(double.NaN)` are computed
- **THEN** each equals the value it was computed from

### Requirement: A count or a result out of range throws in .NET's words

An `Add*` SHALL throw an `ArgumentOutOfRangeException` with .NET's message where .NET throws one:
for a count past what a date can move by, for a result outside the calendar, and for a
`DateTimeOffset` whose UTC time leaves the calendar. `AddTicks` SHALL refuse a result outside the
calendar in the same words.

#### Scenario: A count past what a date can move by

- **WHEN** `new DateTime(2026, 1, 1).AddDays(1e10)` or `AddSeconds(double.PositiveInfinity)` is computed
- **THEN** it throws "Value to add was out of range. (Parameter 'value')"

#### Scenario: A result past the calendar

- **WHEN** `DateTime.MaxValue.AddSeconds(1)` or `DateTime.MinValue.AddMilliseconds(-0.0001)` is computed
- **THEN** it throws "The added or subtracted value results in an un-representable DateTime. (Parameter 'value')"

#### Scenario: A fraction that truncates to nothing at the edge

- **WHEN** `DateTime.MinValue.AddMilliseconds(-0.00001)` is computed
- **THEN** it answers `DateTime.MinValue`, since the fraction truncates to zero ticks, as in .NET

#### Scenario: An offset date whose UTC time leaves the calendar

- **WHEN** a `DateTimeOffset` at four hours before `DateTime.MaxValue` with an offset of minus three
  hours adds 90 minutes
- **THEN** it throws "The UTC time represented when the offset is applied must be between year 0 and 10,000. (Parameter 'offset')"

### Requirement: A date prints through its specifier as .NET prints it

A `DateTime`'s `ToString`, with a standard specifier, a custom picture or neither, SHALL print the
text .NET prints for it: a standard specifier from the culture's patterns (the invariant ones with
no culture in force), `o`, `s`, `u` and `R` from the value's own parts, `U` from the value read as
local time and moved to UTC, and a custom picture token by token, its fraction of a second exact.
A provider SHALL be read as .NET reads it: the invariant culture writes the invariant patterns,
the current culture or a null is the call with none, and any other culture is refused at the build
(EQ2108). A null `DateTime?` SHALL write nothing.

#### Scenario: A standard specifier

- **WHEN** `new DateTime(2026, 9, 24, 10, 30, 15).ToString("D")` runs with no culture in force
- **THEN** it prints `Thursday, 24 September 2026`, as in .NET

#### Scenario: The invariant provider

- **WHEN** `d.ToString("G", CultureInfo.InvariantCulture)` runs
- **THEN** it prints `09/24/2026 10:30:15`, and the browser is handed no name it lacks

#### Scenario: The round-trip form off UTC

- **WHEN** `new DateTime(2026, 9, 24, 10, 30, 15, 250).ToString("o")` runs on a machine at UTC+1
- **THEN** it prints `2026-09-24T10:30:15.2500000`, the value's own hour and seven fractional digits

#### Scenario: A custom picture

- **WHEN** `d.ToString("dd MMM yyyy")` and `d.ToString("HH:mm:ss.fff")` run
- **THEN** they print `24 Sep 2026` and `10:30:15.250`
