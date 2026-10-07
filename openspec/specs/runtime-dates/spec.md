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
local time and moved to UTC, a time a transition skips or repeats read as standard time, and a custom
picture token by token, its fraction of a second exact.
A provider SHALL be read as .NET reads it: the invariant culture writes the invariant patterns,
no specifier, or a null or empty one, is `G` in the current culture, the current culture or a null is the call with none, and any other culture is refused at the build
(EQ2108). A null `DateTime?` SHALL write nothing, and no time zone SHALL move a value's parts but `U`'s.

#### Scenario: A standard specifier

- **WHEN** `new DateTime(2026, 9, 24, 10, 30, 15).ToString("D")` runs with no culture in force
- **THEN** it prints `Thursday, 24 September 2026`, as in .NET

#### Scenario: The invariant provider

- **WHEN** `d.ToString("G", CultureInfo.InvariantCulture)` runs
- **THEN** it prints `09/24/2026 10:30:15`, and the browser is handed no name it lacks

#### Scenario: The round-trip form off UTC

- **WHEN** `new DateTime(2026, 9, 24, 10, 30, 15, 250).ToString("o")` runs on a machine at UTC+1
- **THEN** it prints `2026-09-24T10:30:15.2500000`, the value's own hour and seven fractional digits

#### Scenario: A time a spring-forward gap skips

- **WHEN** `new DateTime(2026, 3, 8, 2, 30, 0).ToString("HH:mm")` runs on a machine in America/New_York
- **THEN** it prints `02:30`, where a local Date moved it to `03:30`, and `U` prints `07:30`, as .NET does

#### Scenario: A time a fall-back repeats

- **WHEN** `new DateTime(2026, 11, 1, 1, 30, 0).ToString("U")` runs on a machine in America/New_York
- **THEN** it prints `Sunday, 01 November 2026 06:30:00`, the time read as standard time as .NET reads
  it, where the Date constructor took the daylight instant and printed `05:30:00`

#### Scenario: A null or an empty format

- **WHEN** `d.ToString((string)null)` or `d.ToString("")` runs with pt-BR in force
- **THEN** it prints `24/09/2026 10:30:15`, the culture's `G`, as .NET does

#### Scenario: A custom picture

- **WHEN** `d.ToString("dd MMM yyyy")` and `d.ToString("HH:mm:ss.fff")` run
- **THEN** they print `24 Sep 2026` and `10:30:15.250`

### Requirement: A DateTimeOffset's unix times are longs cut toward 0001-01-01

`ToUnixTimeSeconds` and `ToUnixTimeMilliseconds` of a `DateTimeOffset` SHALL answer a `long`, which
the browser holds as a BigInt, and SHALL cut the instant's UTC ticks to whole seconds or
milliseconds counted from 0001-01-01 before the Unix epoch is taken off, as .NET does, so an instant
before 1970 with a fraction of the unit rounds down.

#### Scenario: A fraction before the epoch

- **WHEN** `DateTimeOffset.FromUnixTimeMilliseconds(-999).ToUnixTimeSeconds()` and
  `new DateTimeOffset(new DateTime(621355967999999999), TimeSpan.Zero).ToUnixTimeMilliseconds()` are
  computed
- **THEN** each is -1, as in .NET

#### Scenario: A unix time in long arithmetic

- **WHEN** `DateTimeOffset.FromUnixTimeSeconds(10).ToUnixTimeSeconds() * 1000L` is computed
- **THEN** it is 10000, as in .NET, where a JS number met the long as a TypeError

### Requirement: A date is built by its constructor's parameters

Every constructor of `DateTime` and `DateTimeOffset` the compiler lets through SHALL build the value
.NET builds, each argument taken by the parameter it binds and every argument evaluated in the order
it is written, and SHALL refuse what .NET's constructor refuses, in .NET's words. An overload that
takes a `Calendar` SHALL be a build error.

#### Scenario: A kind after the components

- **WHEN** `new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc).Second` is computed
- **THEN** it is 5, as in .NET, where the kind was read as the millisecond

#### Scenario: A microsecond

- **WHEN** `new DateTime(2026, 1, 2, 3, 4, 5, 6, 7).Ticks` is computed
- **THEN** it is 639029198450060070, as in .NET, and its `Microsecond` is 7

#### Scenario: A DateTimeOffset's millisecond

- **WHEN** `new DateTimeOffset(2026, 1, 2, 3, 4, 5, 6, TimeSpan.Zero).Millisecond` is computed
- **THEN** it is 6, as in .NET, where the millisecond was read as the offset

#### Scenario: A component out of range

- **WHEN** `new DateTime(2026, 2, 29)` is built
- **THEN** it throws an `ArgumentOutOfRangeException` with "Year, Month, and Day parameters describe
  an un-representable DateTime.", as .NET does

#### Scenario: Named arguments

- **WHEN** `new DateTime(day: f(2), month: f(1), year: f(2026))` is built with a function that logs
  its argument
- **THEN** the date is 2026-01-02 and the log reads 2, 1, 2026, as in .NET

#### Scenario: A calendar

- **WHEN** `new DateTime(2026, 1, 2, new GregorianCalendar())` is compiled
- **THEN** the build fails with EQ1004

### Requirement: A DateTime carries its kind

A `DateTime` SHALL carry its `Kind` as .NET's does: `Now` and `Today` are local, `UtcNow` is UTC, a
constructor or `SpecifyKind` sets it, arithmetic keeps it, and equality, ordering and the hash leave
it out. `ToLocalTime()` and `ToUniversalTime()` SHALL convert by it, a value of no kind read as UTC
on the way to local time and as local time on the way to UTC. Its text SHALL write it as .NET's does:
`o` and `K` end a UTC time with `Z` and a local one with its offset, `z` writes the zone's offset for
anything not UTC, `U` moves only what is not UTC, and a letter alone that is no standard specifier is
refused.

#### Scenario: Equality leaves the kind out

- **WHEN** `new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc) == new DateTime(2026, 1, 2)` is computed
- **THEN** it is true, as in .NET

#### Scenario: Arithmetic keeps the kind

- **WHEN** `new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc).AddDays(1).Kind` is read
- **THEN** it is `Utc`, as in .NET

#### Scenario: The kind crosses the wire

- **WHEN** `new DateTime(2026, 1, 2, 3, 4, 5, 6, 7, DateTimeKind.Utc)` is written to JSON and the
  page reads "2026-07-01T12:00:00Z" back from the server
- **THEN** the first is "2026-01-02T03:04:05.006007Z" and the second a UTC time, as System.Text.Json
  writes and reads them

#### Scenario: The kind in a date's text

- **WHEN** `new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc).ToString("o")` and the same of the
  local kind are read in Europe/Lisbon
- **THEN** they are "2026-07-01T12:00:00.0000000Z" and "2026-07-01T12:00:00.0000000+01:00", as in .NET,
  where neither wrote a zone

### Requirement: The local time is the browser's time zone

`DateTimeOffset.Now` SHALL be the instant now at the browser's offset for it, and `LocalDateTime` and
`ToLocalTime()` SHALL read the browser's time zone at the value's instant, daylight saving included. A
`DateTimeOffset` built from a `DateTime` that is not UTC, without an offset, SHALL take the zone's
offset for its clock time, as .NET's `TimeZoneInfo.GetUtcOffset` reads it. A local time made from an
instant SHALL keep which occurrence of an hour the zone repeats it is, as .NET keeps it beside the
kind, through arithmetic and `Date` until `SpecifyKind`, so its way back to UTC, its JSON and its
offset are that instant's.

#### Scenario: Now is the right instant

- **WHEN** `Math.Abs((DateTimeOffset.Now - DateTimeOffset.UtcNow).TotalSeconds) < 5` is computed in
  Asia/Kolkata
- **THEN** it is true, as in .NET, where `Now` carried offset zero with the local clock

#### Scenario: The local clock of an instant

- **WHEN** `new DateTimeOffset(2026, 7, 1, 12, 0, 0, TimeSpan.FromHours(-3)).ToLocalTime().ToString()`
  is computed in Asia/Kolkata
- **THEN** it is "07/01/2026 20:30:00 +05:30", as in .NET

#### Scenario: Daylight saving

- **WHEN** `new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero).ToLocalTime().Offset` and the
  same in July are read in Europe/Lisbon
- **THEN** they are 00:00:00 and 01:00:00, as in .NET

#### Scenario: A repeated hour

- **WHEN** `new DateTime(2026, 10, 25, 0, 30, 0, DateTimeKind.Utc).ToLocalTime().ToUniversalTime()` is
  computed in Europe/Lisbon, whose clocks repeat 01:00 to 01:59 that night
- **THEN** it is 00:30 UTC again, as in .NET, where it landed on the standard occurrence, an hour later
