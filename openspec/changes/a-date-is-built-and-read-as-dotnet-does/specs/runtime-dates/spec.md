# Spec Delta

## ADDED Requirements

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
on the way to local time and as local time on the way to UTC.

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

### Requirement: The local time is the browser's time zone

`DateTimeOffset.Now` SHALL be the instant now at the browser's offset for it, and `LocalDateTime` and
`ToLocalTime()` SHALL read the browser's time zone at the value's instant, daylight saving included. A
`DateTimeOffset` built from a `DateTime` that is not UTC, without an offset, SHALL take the zone's
offset for its clock time, as .NET's `TimeZoneInfo.GetUtcOffset` reads it.

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
