# month-grid Specification

## Purpose
What the month grid (`Calendar`, and the `DatePicker` that opens it) counts in, and how a day is counted
in the culture's calendar (`CultureCalendar`), so the month a grid shows, titles and pages is the one the
culture's own calendar names, the same on the server, in the browser and on Photon.

## Requirements

### Requirement: A day is counted in the culture's calendar on both sides

`CultureCalendar` SHALL answer a `DateOnly`'s year, month and day, its month's first day and length, and a move by
months or by years in the active format culture's calendar, as .NET's
`CultureInfo.CurrentCulture.DateTimeFormat.Calendar` answers them, in the browser as in .NET. A move SHALL
keep the day of the month and cut it to the last day the target month has.

#### Scenario: A Persian day

- **WHEN** browser-side code in fa-IR asks `CultureCalendar.YearOf`, `MonthOf` and `DayOf` of
  `new DateOnly(2024, 1, 17)`, and `FirstOfMonth` of it
- **THEN** it answers 1402, 10 and 27, and the 22nd of December 2023, as .NET answers

#### Scenario: A move cut to the month's last day

- **WHEN** browser-side code in fa-IR asks `CultureCalendar.AddYears(new DateOnly(2025, 3, 20), 1)`, the
  30th of Esfand 1403
- **THEN** it answers the 20th of March 2026, the 29th of Esfand 1404, as .NET's `Calendar.AddYears` does

#### Scenario: A Gregorian culture

- **WHEN** the same questions are asked in en-US
- **THEN** each answers what `DateOnly` itself answers

### Requirement: The month grid is the culture calendar's month

`Calendar` SHALL lay out, title and page the culture calendar's month: the title names that month with the
culture's month name and that calendar's year, a cell shows that calendar's day and announces it in the
same count, a day outside that month is a hole, and the chevrons and the keyboard's page and section moves
go a month and a year of that calendar. In a Gregorian culture the grid SHALL be what it was.

#### Scenario: January 2024 in fa-IR

- **WHEN** a `Calendar` selecting the 17th of January 2024 is built in fa-IR
- **THEN** its title is `دی 1402`, its first cell of the month is the 22nd of December 2023 showing 1, the
  selected cell shows 27, and the 21st of January 2024 is a hole

#### Scenario: A year that is not the Gregorian count

- **WHEN** the same `Calendar` is built in th-TH
- **THEN** its title names January and the year 2567

#### Scenario: Paging a month

- **WHEN** the next-month chevron is pressed on `دی 1402` in fa-IR
- **THEN** the grid shows `بهمن 1402`, from the 21st of January to the 19th of February 2024

#### Scenario: A bounded calendar's last month

- **WHEN** a `Calendar` showing November 2077 is built in ar-SA, whose Um Al Qura calendar writes up to
  the 16th of November 2077, its last month's last day
- **THEN** it draws that month's 30 days, and its trailing week, past the calendar's range, is holes

### Requirement: A move past the calendar's range is refused in its words

`CultureCalendar.AddMonths` and `AddYears` SHALL refuse a move to a month the culture's calendar does not
count with .NET's `ArgumentOutOfRangeException` in that calendar's words, in the browser as in .NET, and
`CultureCalendar.FirstDay` and `LastDay` SHALL answer the calendar's `MinSupportedDateTime` and
`MaxSupportedDateTime` as days.

#### Scenario: Past Um Al Qura's last year

- **WHEN** browser-side code in ar-SA evaluates `CultureCalendar.AddMonths(CultureCalendar.LastDay, 1)`
- **THEN** it throws "Valid values are between 1318 and 1500, inclusive. (Parameter 'year')", with the actual
  value 1501, as .NET does

#### Scenario: Past the Gregorian calendar's last day

- **WHEN** browser-side code in en-US evaluates `CultureCalendar.AddMonths(CultureCalendar.LastDay, 1)`
- **THEN** it throws "Year, Month, and Day parameters describe an un-representable DateTime.", as .NET does

### Requirement: The month grid stops at its calendar's range

`Calendar` SHALL NOT page past its calendar's first or last month, SHALL keep its keyboard's moves inside the
days the calendar writes, and SHALL draw a day the calendar does not write as a hole.

#### Scenario: The chevron past Um Al Qura's last month

- **WHEN** the next-month chevron is pressed on a `Calendar` counting in Um Al Qura and showing November 2077
- **THEN** the grid still shows that month, and nothing is refused

#### Scenario: December 9999

- **WHEN** a `Calendar` showing December 9999 is built in en-US
- **THEN** it draws the month's 31 days, its last week past them as holes, and its next-month chevron stays
