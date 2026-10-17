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

### Requirement: A DateOnly, a TimeOnly and a DateTimeOffset print as .NET prints them

A `DateOnly`, a `TimeOnly` and a `DateTimeOffset` SHALL print through the formatter a `DateTime` prints
through, each with what its type takes: a `DateOnly` SHALL be its culture's `d` with no format and take
the date specifiers, a `TimeOnly` its culture's `t` and the time specifiers, each refusing a specifier
its type does not take with .NET's FormatException, and a `DateTimeOffset` its culture's `G` with its
offset, its `o` and `K` writing the offset and its `u` and `R` converting by it. A provider SHALL be read
as a `DateTime`'s is, and a null value SHALL write nothing.

#### Scenario: A DateOnly's long date

- **WHEN** `new DateOnly(2026, 9, 24).ToString("D")` runs with no culture in force
- **THEN** it prints `Thursday, 24 September 2026`, as .NET does

#### Scenario: A DateTimeOffset's round-trip form

- **WHEN** `new DateTimeOffset(new DateTime(2026, 9, 24, 10, 30, 15, 250), TimeSpan.FromHours(1)).ToString("o")` runs
- **THEN** it prints `2026-09-24T10:30:15.2500000+01:00`, as .NET does

#### Scenario: The invariant provider

- **WHEN** `new DateOnly(2026, 9, 24).ToString("d", CultureInfo.InvariantCulture)` runs with pt-BR in force
- **THEN** it prints `09/24/2026`, and the browser is handed no name it lacks

### Requirement: A custom picture writes the culture's separators, the offset and the era

A custom date picture SHALL write the culture's date separator for `/` and its time separator for
`:`, the offset for `z`, `zz` and `zzz` (the host's for a `DateTime` of no kind, as .NET reads it), and
the era for `g` and `gg`, and SHALL read a run of one letter as one token whose length decides what it
writes, as .NET's `FormatCustomized` does.

#### Scenario: A culture's date separator

- **WHEN** `new DateTime(2026, 9, 24).ToString("dd/MM/yyyy")` runs with de-DE in force
- **THEN** it prints `24.09.2026`, as .NET does, where it printed `24/09/2026`

#### Scenario: The offset and the era

- **WHEN** `new DateTimeOffset(2026, 9, 24, 10, 30, 0, TimeSpan.FromHours(1)).ToString("yyyy g zzz")` runs
- **THEN** it prints `2026 A.D. +01:00`, as .NET does, where it wrote the letters

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
- **THEN** it is 00:30 UTC again, the instant it came from, as in .NET, where it was read as the
  standard occurrence and went back to 01:30 UTC, an hour later

### Requirement: A TimeSpan writes the format it is given

A `TimeSpan` written with a format, through `ToString(format)`, `ToString(format, provider)`, an
interpolation hole or a composite placeholder, SHALL write what .NET 10 writes: `c`, `t` and `T` the
constant form `[-][d.]hh:mm:ss[.fffffff]` in every culture, `g` the short general form
`[-][d:]h:mm:ss[.FFFFFFF]` and `G` the long one `[-]d:hh:mm:ss.fffffff`, both with the culture's
decimal separator, and a custom format token by token without a sign. A null or an empty format SHALL
be the constant form. Any other format SHALL throw .NET's FormatException with its message. The
invariant culture SHALL write the invariant separator, the current culture and a null provider SHALL
be the call with none, and any other provider SHALL fail the build (EQ2108). A format and a provider
SHALL be the arguments bound to those parameters, by name or by place. A null `TimeSpan?` SHALL write
nothing.

#### Scenario: A custom format

- **WHEN** `new TimeSpan(0, 1, 2, 3, 4).ToString(@"hh\:mm")` runs
- **THEN** it answers `01:02`, as in .NET, where it wrote `01:02:03.0040000`

#### Scenario: A general format in a culture

- **WHEN** `$"{new TimeSpan(1, 2, 3, 4, 500):g}"` runs in pt-BR
- **THEN** it answers `1:2:03:04,5`, as in .NET, and `ToString("G", CultureInfo.InvariantCulture)` of
  the same value answers `1:02:03:04.5000000`

#### Scenario: A format .NET refuses

- **WHEN** `TimeSpan.Zero.ToString("h")` and `TimeSpan.Zero.ToString("'abc")` run
- **THEN** each throws a FormatException with .NET's message, where both wrote `00:00:00`

#### Scenario: A provider named before its format

- **WHEN** `new TimeSpan(1, 2, 3, 4, 500).Negate().ToString(formatProvider: null, format: "g")` runs
- **THEN** it answers `-1:2:03:04.5` in the invariant culture, as in .NET, where the null was read as
  the format and the value written in the constant form

#### Scenario: A null TimeSpan

- **WHEN** a null `TimeSpan?` is written with `ToString()` and in a hole with `g`
- **THEN** each writes nothing, as in .NET, where `ToString()` read it through null

### Requirement: A date or a time is read in the culture the call names

`Parse` and `TryParse` of a `DateTime`, a `DateTimeOffset`, a `DateOnly`, a `TimeOnly` and a
`TimeSpan` SHALL read the numeric forms .NET 10 reads in the culture the call names: with no provider,
with `CultureInfo.CurrentCulture` and with a null one, the page's culture, which is the current one;
with `CultureInfo.InvariantCulture`, the invariant one. A date SHALL be three numbers in the order of
the culture's short date pattern, a year of three digits or more wherever it stands and one of two
read against 2049, separated by `/`, `-`, `.` or the culture's own separator; a clock SHALL be one to
three parts separated by `:` or the culture's own separator, with a fraction after the seconds written
with a point or a comma, and `AM`, `PM` or the culture's own designators before or after it; a zone
SHALL be `Z` or a signed offset of hours and minutes within fourteen hours. A text it does not read
SHALL throw .NET's FormatException in .NET's words, and `TryParse` SHALL answer false with the type's
zero. Any other provider SHALL fail the build (EQ2108), styles other than white space and a span SHALL
fail it (EQ1004), and `ParseExact`, `TryParseExact` and `GetDateTimeFormats` SHALL be refused by the
fence (EQ2004). The arguments SHALL be the ones bound to each parameter, by name or by place.

#### Scenario: A date in the page's culture

- **WHEN** `DateTime.TryParse("01/02/2024", out var d)` runs in pt-BR
- **THEN** it answers true and the 1st of February 2024, as in .NET, where it answered the 2nd of
  January

#### Scenario: What a culture writes reads back

- **WHEN** `DateTime.Parse(new DateTime(2024, 11, 5, 9, 5, 7).ToString())` runs in en-US, pt-BR,
  de-DE, sv-SE, ar, fi-FI, ja-JP and ko-KR
- **THEN** it answers the date it wrote in each, as in .NET

#### Scenario: The invariant culture on any page

- **WHEN** `DateTime.Parse("01/02/2024", CultureInfo.InvariantCulture)` runs in pt-BR
- **THEN** it answers the 2nd of January 2024, as in .NET, where the provider crossed as a name no
  browser defines

#### Scenario: A time span's fraction in the culture

- **WHEN** `TimeSpan.TryParse("1:02:03,5", out var t)` runs in pt-BR
- **THEN** it answers true and `01:02:03.5000000`, as in .NET, and `1.02:03:04,5`, which has days,
  is refused

#### Scenario: A refusal in .NET's words

- **WHEN** `DateTime.Parse("2024/01/15 21:30 junk")` runs
- **THEN** it throws a FormatException that names the unknown word at index 17, as in .NET

#### Scenario: Another culture

- **WHEN** `DateTime.Parse(text, CultureInfo.GetCultureInfo("pt-BR"))` is built
- **THEN** the build fails with EQ2108

### Requirement: Convert.ToDateTime reads a text as Parse does

`Convert.ToDateTime` SHALL answer by the overload C# bound: a text SHALL read as `DateTime.Parse` reads
it, in the culture the call names, and a null text SHALL be `DateTime.MinValue`; an object SHALL be
read by what it holds when the call runs, a null one being `MinValue`; a `DateTime` SHALL be itself;
and a value of any other type SHALL throw .NET's InvalidCastException after it is evaluated. A char
held in an object SHALL be read as a text of one character, since the browser holds a char as one and
a boxed value does not carry its type (#552).

#### Scenario: A text in the page's culture

- **WHEN** `Convert.ToDateTime("01/02/2024")` runs in pt-BR
- **THEN** it answers the 1st of February 2024, as in .NET, where it answered the text

#### Scenario: A null text

- **WHEN** `Convert.ToDateTime((string)null)` runs
- **THEN** it answers `DateTime.MinValue`, as in .NET, where it answered `"null"`

#### Scenario: A type with no conversion

- **WHEN** `Convert.ToDateTime(1)` runs
- **THEN** it throws an InvalidCastException, `Invalid cast from 'Int32' to 'DateTime'.`, as in .NET

### Requirement: A date built with a calendar fails the build

A constructor of a `DateTime`, a `DateTimeOffset` or a `DateOnly` that takes a `Calendar` SHALL fail
the build (EQ1004), a null calendar included, since the browser has none of .NET's calendars.

#### Scenario: A DateOnly with a calendar

- **WHEN** `new DateOnly(2024, 1, 2, new GregorianCalendar())` is built
- **THEN** the build fails with EQ1004, where the calendar was dropped

#### Scenario: A DateOnly with a null calendar

- **WHEN** `new DateOnly(2024, 1, 2, null)` is built
- **THEN** the build fails with EQ1004, where .NET throws and the browser answered the date

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

### Requirement: A date, a time, an instant and a span answer every member their twins carry

A member of a `TimeSpan`, a `DateTime`, a `DateOnly`, a `TimeOnly` or a `DateTimeOffset` SHALL answer as
.NET's where the runtime's twin or its factory carries it, and SHALL fail the build with EQ2004 naming
it where neither does. Subtracting a date or an instant SHALL be the span between the two. A named
argument SHALL land in its parameter's place, the arguments evaluated in the order they are written.

#### Scenario: Named arguments in another order

- **WHEN** browser-side code calls `DateTime.Compare(t2: later, t1: earlier)`,
  `DateTimeOffset.Compare(second: o2, first: o1)`, `d.ToDateTime(kind: DateTimeKind.Utc, time: t)`,
  `t.IsBetween(end: late, start: early)` and `new DateOnly(day: 29, month: 2, year: 2024)`
- **THEN** each answers as .NET does, where the arguments were handed over in the order they were written

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
past the range, SHALL throw the OverflowException .NET throws. A factor or a divisor SHALL be any value
C# converts to a double, a long, a ulong, a char and a type with a conversion of its own included, on
either side of the product, and a nullable one SHALL be the lifted operator, null for a null factor:
the operator C# bound decides, never the operand's own type. `*=` and `/=` SHALL be the same operators,
a nullable span lifted and the target read once.

#### Scenario: A factor of another type, and in place

- **WHEN** browser-side code scales `TimeSpan.FromTicks(3)` by a long 2 and a ulong 2 on either side and
  divides it by them, scales `TimeSpan.FromTicks(2)` by a null and a 1.5 `double?` and by the char 'a',
  and runs `t *= 2.5` and `t /= 4` over 10 ticks
- **THEN** they are 6, 6, 2, 6, 6, 2 ticks, null, 3 ticks, 194 ticks, 25 and 6 ticks, as in .NET, where
  each was JavaScript's own operator

#### Scenario: Rounding to the even tick

- **WHEN** browser-side code divides spans of 5, 15, 25 and 35 ticks by 2, 2, 10 and 10
- **THEN** they are 2, 8, 2 and 4 ticks, as in .NET

#### Scenario: A NaN factor and an overflow

- **WHEN** browser-side code runs `TimeSpan.FromSeconds(1).Multiply(double.NaN)` and
  `TimeSpan.MaxValue.Multiply(2)`
- **THEN** the first throws ArgumentException "TimeSpan does not accept floating point Not-a-Number
  values. (Parameter 'factor')" and the second OverflowException "TimeSpan overflowed because the
  duration is too long.", as in .NET

### Requirement: A date beside an instant is converted to one

An operator between a `DateTimeOffset` and a `DateTime`, on either side, SHALL convert the `DateTime`
to a `DateTimeOffset` as C#'s implicit conversion does, at the offset its kind gives, and SHALL answer
as the instant's operator: a difference SHALL be a span, and `==`, `!=`, `<`, `<=`, `>` and `>=`
SHALL compare the two instants. A nullable date SHALL be converted as C#'s lifted conversion does: a
null one SHALL stay null, the operand read once, and an instant's operator over it SHALL answer as the
lifted operator does.

#### Scenario: A difference and a comparison across the two types

- **WHEN** browser-side code holds `a = new DateTimeOffset(2024, 1, 2, 0, 0, 0, TimeSpan.Zero)` and
  `b = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)` and writes `a - b`, `b - a`, `a < b`,
  `a == b`, `a > b` and `a == new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc)`
- **THEN** each answers as in .NET, where the pair matched no operator and JavaScript's own ran

#### Scenario: A nullable date as an instant

- **WHEN** browser-side code assigns a null and a present `DateTime?` to a `DateTimeOffset?`, and writes
  `-`, `==` and `<` between a `DateTimeOffset?` and a `DateTime?` with a null on either side
- **THEN** each answers as in .NET: the null date is a null instant, a difference with a null is null,
  and a comparison answers the lifted operator's bool, where the null was handed to the conversion and
  threw a TypeError
