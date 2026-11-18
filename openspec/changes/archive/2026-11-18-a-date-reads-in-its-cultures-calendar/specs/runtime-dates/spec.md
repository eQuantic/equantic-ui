## ADDED Requirements

### Requirement: A date is written and read in the calendar of its culture

A date's year, month, day and era SHALL be written, and a date's numbers SHALL be read, in the calendar
the culture counts its dates in, as .NET 10 counts them: the Thai Buddhist calendar of th-TH, the
Persian calendar of fa-IR and Um Al Qura's of ar-SA among the ones a culture defaults to. The round
trip, sortable and RFC 1123 forms, and the ISO form a year-first date before a `T` is read in, SHALL be
the Gregorian calendar's in every culture.

#### Scenario: A date written in th-TH

- **WHEN** `new DateTime(2024, 1, 15).ToString("d")` is written in th-TH
- **THEN** it is `15/1/2567`, as in .NET, where the browser wrote `15/1/2024`

#### Scenario: A date written in fa-IR and in ar-SA

- **WHEN** `new DateTime(2024, 1, 15).ToString("yyyy-MM-dd")` is written in fa-IR and in ar-SA
- **THEN** it is `1402-10-25` and `1445-07-03`, as in .NET, and the month's name is the Persian or the
  Hijri month's of that day

#### Scenario: A date read in each calendar

- **WHEN** `DateTime.Parse` reads `15/1/2567` in th-TH, `1402/10/25` in fa-IR and `4/7/1445` in ar-SA
- **THEN** it answers the 15th of January 2024, the same day and the next one, as .NET does, where the
  browser read the years 2567, 1402 and 1445

#### Scenario: A year of two digits and a date with no year

- **WHEN** `15/1/67` is read in th-TH and `15/1` in ar-SA
- **THEN** the year is 2567 in the Thai Buddhist count, in the century its `TwoDigitYearMax` (2572)
  ends, and the year of `15/1` is this year in the Um Al Qura count, as .NET reads them

#### Scenario: The era beside a date

- **WHEN** `1/15/2024 AD` is read in en-US, `1402/10/25 ه‍.ش.` in fa-IR and a date with its era written
  after it in ar-SA, as Windows' data writes ar-SA's short date
- **THEN** each is read as .NET reads it, the era's name or its abbreviation before or after the date,
  and a date with ar-SA's Arabic comma, which .NET refuses, is refused

#### Scenario: A designator beside a date

- **WHEN** `1/15/2024 PM` and `PM 1/15/2024` are read in en-US, and `15/1/2024 م` where the Gregorian
  calendar's era is spelled as the PM designator, as Linux's ar-SA writes it
- **THEN** each is the 15th of January 2024 at noon, as in .NET, and `PM` alone names no moment

#### Scenario: The ISO form is Gregorian in every culture

- **WHEN** `2024-01-15T10:30:00` and `2024-01-15` are read in ar-SA
- **THEN** the first is the 15th of January 2024, and the second is refused, a year Um Al Qura does not
  count, as in .NET

### Requirement: A day the culture's calendar does not write is refused in its words

Writing a year, a month, a day or an era of a day the culture's calendar does not write SHALL throw
.NET's `ArgumentOutOfRangeException`, in the calendar's own words and with the value's ticks, and a
time of day held in a `DateTime` of the first day SHALL be written with no format in the invariant
sortable form where .NET writes it so.

#### Scenario: A day before Um Al Qura's first

- **WHEN** `DateTime.MinValue.ToString("d")` is written in ar-SA
- **THEN** it throws `ArgumentOutOfRangeException`, its message naming 04/30/1900 and 11/16/2077, as in
  .NET, where the browser wrote `1/1/0001`

#### Scenario: The first day, with no format

- **WHEN** `default(DateTime).ToString()` is written in fa-IR
- **THEN** it is `0001-01-01T00:00:00`, as in .NET, and not a refusal
