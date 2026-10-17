## ADDED Requirements

### Requirement: A date, a time, an instant and a span answer every member their twins carry

A member of a `TimeSpan`, a `DateTime`, a `DateOnly`, a `TimeOnly` or a `DateTimeOffset` SHALL answer as
.NET's where the runtime's twin or its factory carries it, and SHALL fail the build with EQ2004 naming
it where neither does. Subtracting a date or an instant SHALL be the span between the two.

#### Scenario: A span's parts below a millisecond

- **WHEN** browser-side code reads `new TimeSpan(12345678)`'s `Microseconds`, `Nanoseconds`,
  `TotalMicroseconds` and `TotalNanoseconds`
- **THEN** they are 567, 800, 1234567.8 and 1234567800, as in .NET, in the page's culture

#### Scenario: A time of day's members

- **WHEN** browser-side code reads `new TimeOnly(774051234567)`'s `Microsecond` and `Nanosecond`, adds
  three hours to it, and asks whether it is between 23:00 and 01:00
- **THEN** they are 456, 700, `00:30:05.1234567` and False, as in .NET

#### Scenario: An instant's members

- **WHEN** browser-side code reads `new DateTimeOffset(2024, 3, 1, 23, 30, 0, TimeSpan.FromHours(-3))`'s
  `DayOfYear`, `TotalOffsetMinutes` and `ToUniversalTime()`, and compares it with the same instant at
  +00:00 by `EqualsExact` and `Equals`
- **THEN** they are 61, -180, `2024-03-02T02:30:00.0000000+00:00`, False and True, as in .NET

#### Scenario: A member no twin carries

- **WHEN** a component calls `TimeSpan.TryFormat`, `DateTime.ToBinary`, `DateTime.FromOADate`,
  `DateTime.IsDaylightSavingTime` or `DateTimeOffset.ToFileTime`
- **THEN** the build fails with EQ2004 naming the member, where each was a call of a member no twin had

### Requirement: A span scales and divides as .NET's operators do

`TimeSpan.Multiply`, `Divide` and the operators `*` and `/` SHALL round the ticks of a product or a
quotient by a number to the even tick, as .NET's `Math.Round` does; a factor or a divisor that is NaN
SHALL be an ArgumentException naming it, and a result past the range a span holds an
OverflowException. A span divided by a span SHALL be the ratio of their ticks. A sign in front of a
span SHALL negate it or leave it, and the least span's negation and duration, and a sum or a difference
past the range, SHALL throw the OverflowException .NET throws.

#### Scenario: Rounding to the even tick

- **WHEN** browser-side code divides spans of 5, 15, 25 and 35 ticks by 2, 2, 10 and 10
- **THEN** they are 2, 8, 2 and 4 ticks, as in .NET

#### Scenario: A NaN factor and an overflow

- **WHEN** browser-side code runs `TimeSpan.FromSeconds(1).Multiply(double.NaN)` and
  `TimeSpan.MaxValue.Multiply(2)`
- **THEN** the first throws ArgumentException "TimeSpan does not accept floating point Not-a-Number
  values. (Parameter 'factor')" and the second OverflowException "TimeSpan overflowed because the
  duration is too long.", as in .NET
