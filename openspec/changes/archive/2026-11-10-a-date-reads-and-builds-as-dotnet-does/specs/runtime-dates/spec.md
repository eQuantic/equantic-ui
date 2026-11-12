## ADDED Requirements

### Requirement: A date with a name is read as .NET reads it

A date's reader SHALL read a month's name, whole or abbreviated, in any case, in the culture's language and in
English, as the culture spells it, with up to two numbers in any order: a number of three digits or more is the
year, a month and a year are the month's first day, a month and one short number are a day of this year unless
the culture's month-day and year-month patterns make it a year, and two short numbers are told apart by the short
date pattern. A day's name before a date SHALL be that date's day, or the read is refused in .NET's words.

#### Scenario: A month, a day and a year

- **WHEN** `DateTime.TryParse("Jan 15, 2024", out var d)` runs in any culture
- **THEN** it answers true and the 15th of January 2024, as .NET does; it answered false

#### Scenario: A month and one number

- **WHEN** `DateTime.TryParse("Jan 5", out var d)` runs in de-DE
- **THEN** it answers the 1st of January 2005, as .NET does, whose patterns there make the number a year

#### Scenario: A day's name that is not the date's

- **WHEN** `DateTime.TryParse("Tuesday, January 15, 2024", out var d)` runs
- **THEN** it answers false, as .NET does, the 15th being a Monday

### Requirement: A date of few numbers and a clock alone are read as .NET reads them

Two numbers SHALL be a month and a day of this year in the order of the culture's month-day pattern, or a month and
a year where one has three digits or more. White space alone MAY separate a date's numbers, and a point MAY end a
date, except where a point is what separates the culture's date. A clock with no date SHALL be today's. `GMT`, in
any case, SHALL be the zero offset, and takes no offset after it.

#### Scenario: Two numbers

- **WHEN** `DateTime.TryParse("15/1", out var d)` runs in pt-BR
- **THEN** it answers the 15th of January of this year, as .NET does; it answered false

#### Scenario: A clock alone

- **WHEN** `DateTime.TryParse("9:30", out var d)` runs
- **THEN** it answers half past nine today, as .NET does; it answered false

### Requirement: A TimeOnly is built and checked as .NET builds it

`new TimeOnly` SHALL keep every part it is given, the microsecond included, and SHALL refuse a part out of its range
and ticks outside a day with .NET's ArgumentOutOfRangeException in its words.

#### Scenario: A microsecond

- **WHEN** `new TimeOnly(1, 2, 3, 4, 5).Microsecond` runs
- **THEN** it answers 5, as .NET does; the fifth argument was dropped

#### Scenario: An hour past the day

- **WHEN** `new TimeOnly(25, 0)` runs
- **THEN** it throws "Hour, Minute, and Second parameters describe an un-representable DateTime.", as .NET does

### Requirement: What a culture writes reads back

A date with a month's name SHALL be read with the words the culture's long, short, month-day and year-month patterns write between its parts, a point after a day, and a day's name before or after it, and a clock SHALL be read with the words its long and short time patterns quote after it. A word SHALL be read only where a word ends, as .NET's lexer reads it: a letter right after it, in a script with no case too, refuses the text. A `DateTimeOffset` with a zone and no date SHALL be today at the offset it names.

#### Scenario: A long date with words

- **WHEN** a pt-BR page reads `15 de janeiro de 2024`, or a ja-JP page `2024年1月15日 月曜日`
- **THEN** the date is January 15, 2024, as in .NET

#### Scenario: A day's name run onto the date

- **WHEN** a ja-JP page reads `2024年1月15日月曜日`, which Linux's and Windows' culture data write for the long date
- **THEN** the read is refused, as in .NET

#### Scenario: A word after the clock

- **WHEN** an hsb-DE page reads `15.1.2024 9:30 hodź.`, its short time pattern being `H:mm 'hodź'.`
- **THEN** the moment is January 15, 2024 at 9:30, as in .NET

#### Scenario: An offset with no date

- **WHEN** a program reads `10:00 +14:00` as a `DateTimeOffset`
- **THEN** its day is today's at that offset, which may be a day after the local one
