# Spec Delta

## ADDED Requirements

### Requirement: A TimeSpan is read as .NET reads it

`TimeSpan.Parse` and `TryParse` SHALL read a text as .NET 10 reads one with no format, in the culture
the call names: the text trimmed of .NET's white space, its numbers of ASCII digits read by how many
there are (one is days; two are hours and minutes; three are hours, minutes and seconds, days, hours
and minutes, or hours, minutes and a fraction; four add the seconds or the fraction; five are all of
them) and by the runs between them, matched against the invariant pattern `d.hh:mm:ss.fffffff` and
the culture's `d:hh:mm:ss` with its decimal separator, each positive and then negative, in .NET's
order. Days SHALL be at most 10,675,199, hours 23, and minutes and seconds 59; a fraction past seven
digits SHALL be rounded to seven as .NET rounds it; and the whole SHALL be within a `TimeSpan`'s range.
A text it does not read SHALL throw .NET's FormatException, and one that names a span past those
bounds .NET's OverflowException, each in .NET's words; `TryParse` SHALL answer false for both. A
fraction whose leading zeros and digits come to fifteen or more SHALL throw the
IndexOutOfRangeException .NET throws for it, from `Parse` and `TryParse` alike.

#### Scenario: One number and two

- **WHEN** `TimeSpan.TryParse("10", out var a)` and `TimeSpan.TryParse("1:2", out var b)` run
- **THEN** they answer true with ten days and with an hour and two minutes, as in .NET, where both
  answered false

#### Scenario: A clock no day holds

- **WHEN** `TimeSpan.Parse("24:00:00")` and `TimeSpan.Parse("00:60:00")` run
- **THEN** the first answers twenty-four days, by the culture's pattern, and the second throws an
  OverflowException in .NET's words, as in .NET, where they answered a day and an hour

#### Scenario: The culture's pattern

- **WHEN** `TimeSpan.TryParse("1:2:3:4,5", out var t)` runs in pt-BR
- **THEN** it answers true and `1.02:03:04.5000000`, as in .NET, and in en-US it answers false

#### Scenario: A fraction of more than seven digits

- **WHEN** `TimeSpan.Parse("0:0:0.0123456789")` and `TimeSpan.Parse("0:0:0.123456789")` run
- **THEN** the first answers `00:00:00.0123457` and the second throws an OverflowException, as in .NET

#### Scenario: The range, to the tick

- **WHEN** `TimeSpan.Parse("-10675199.02:48:05.4775808")` runs
- **THEN** it answers `TimeSpan.MinValue`, as in .NET, and one tick more negative throws an
  OverflowException
